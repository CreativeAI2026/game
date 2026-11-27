using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>会話イベントの再生が、会話の始まりと終わりを要求する契約。</summary>
    public interface IEventGameMode
    {
        /// <summary>会話を始められるなら会話にして true。探索中でない(戦闘中・メニューを開いている等)なら false。</summary>
        bool TryStartDialogue();

        /// <summary>会話を終えて探索へ戻す。</summary>
        void EndDialogue();
    }

    /// <summary>ゲームがいま何を進めているか。メニューを開いているかは別(<see cref="GameModeManager.IsMenuOpen"/>)。</summary>
    public enum GameMode
    {
        /// <summary>フィールドを探索している。</summary>
        Field,

        /// <summary>会話イベントを再生している(操作不能)。</summary>
        Dialogue,

        /// <summary>戦っている。会話イベントの battle ステップもこれになる。</summary>
        Battle,
    }

    /// <summary>
    /// ゲームの進行の状態(探索 / 会話 / 戦闘)と、メニューを開いているかを持つ常駐の状態機械。
    /// 状態を変えられるのはここだけで、外からは「会話を始めたい」「戦闘が終わった」のように要求する。
    /// 要求を受けてよいかの判定と、戦闘が終わったあとの戻り先もここが決める。
    ///
    /// 遷移:
    /// - 探索 ⇄ 会話: 会話はメニューを閉じた探索中からだけ始まる。終われば探索へ。
    /// - 探索 / 会話 → 戦闘: 戦闘は探索中か会話中に始まる。終われば始まる前の状態へ戻る(会話の途中で探索を挟まない)。
    /// - メニュー: 探索中にだけ開ける。開いている間も進行の状態は探索のまま。戦闘が始まったら閉じる。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameModeManager : MonoBehaviour, IEventGameMode
    {
        public static GameModeManager Instance { get; private set; }

        /// <summary>セッション常駐のゲームモードを1つだけ生成する(既存ならそれを返す)。</summary>
        public static GameModeManager EnsureResident()
        {
            if (Instance != null)
                return Instance;
            return new GameObject(nameof(GameModeManager)).AddComponent<GameModeManager>();
        }

        /// <summary>今の進行の状態。マネージャ未生成(開発シーン直 Play など)は Field 扱い。</summary>
        public static GameMode Current => Instance != null ? Instance.Mode : GameMode.Field;

        /// <summary>今メニューを開いているか。マネージャ未生成なら false。</summary>
        public static bool MenuOpen => Instance != null && Instance.IsMenuOpen;

        /// <summary>プレイヤーが自由に動けるか(探索中でメニューも開いていない)。マネージャ未生成なら true。</summary>
        public static bool PlayerCanAct => Instance == null || Instance.CanPlayerAct;

        /// <summary>今の進行の状態。</summary>
        public GameMode Mode { get; private set; } = GameMode.Field;

        /// <summary>メニュー(キャラ / インベ / セーブ / 合成 / マップのパネル)を開いているか。</summary>
        public bool IsMenuOpen { get; private set; }

        /// <summary>探索中でメニューも開いていないか。</summary>
        public bool CanPlayerAct => Mode == GameMode.Field && !IsMenuOpen;

        /// <summary>進行の状態かメニューの開閉が変わったら通知。購読側は読み直す。</summary>
        public event Action Changed;

        // 戦闘が終わったら戻る状態(探索か会話)
        private GameMode _modeAfterBattle = GameMode.Field;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>メニューを開けるなら開いた扱いにして true。探索中でなければ false。既に開いていれば true。</summary>
        public bool TryOpenMenu()
        {
            if (Mode != GameMode.Field)
                return false;
            if (!IsMenuOpen)
            {
                IsMenuOpen = true;
                Changed?.Invoke();
            }
            return true;
        }

        /// <summary>メニューを閉じた扱いにする。</summary>
        public void CloseMenu()
        {
            if (!IsMenuOpen)
                return;
            IsMenuOpen = false;
            Changed?.Invoke();
        }

        /// <summary>IEventGameMode。会話はメニューを閉じた探索中からだけ始まる。</summary>
        public bool TryStartDialogue()
        {
            if (!CanPlayerAct)
                return false;
            SetMode(GameMode.Dialogue);
            return true;
        }

        /// <summary>
        /// IEventGameMode。会話を終えて探索へ戻す。会話の battle の途中で打ち切られたときは、戦闘が終わったら探索へ戻す。
        /// </summary>
        public void EndDialogue()
        {
            if (Mode == GameMode.Dialogue)
                SetMode(GameMode.Field);
            else if (Mode == GameMode.Battle)
                _modeAfterBattle = GameMode.Field;
        }

        /// <summary>戦闘を始められるなら戦闘にして true。探索中か会話中なら始まり、メニューを開いていれば閉じる。</summary>
        public bool TryStartBattle()
        {
            if (Mode == GameMode.Battle)
                return false;
            _modeAfterBattle = Mode;
            IsMenuOpen = false;
            Mode = GameMode.Battle;
            Changed?.Invoke();
            return true;
        }

        /// <summary>戦闘を終え、始まる前の状態(探索か会話)へ戻す。</summary>
        public void EndBattle()
        {
            if (Mode != GameMode.Battle)
                return;
            SetMode(_modeAfterBattle);
        }

        /// <summary>プレイを切り替えるとき(タイトルへ戻る・はじめから)、探索・メニューを閉じた状態へ戻す。</summary>
        public void ResetToField()
        {
            _modeAfterBattle = GameMode.Field;
            bool changed = Mode != GameMode.Field || IsMenuOpen;
            Mode = GameMode.Field;
            IsMenuOpen = false;
            if (changed)
                Changed?.Invoke();
        }

        private void SetMode(GameMode mode)
        {
            if (mode == Mode)
                return;
            Mode = mode;
            Changed?.Invoke();
        }
    }
}
