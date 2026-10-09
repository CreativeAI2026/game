using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Core
{
    /// <summary>
    /// EventTrigger から託された会話イベントを順に再生し、終了時に進行度を進める指揮役。
    /// 常駐生成され EventPlayerService.Current に自身を登録する(per-field 配線は不要)。
    /// </summary>
    public sealed class EventPlayer : MonoBehaviour, IEventPlayer
    {
        public static EventPlayer Instance { get; private set; }

        /// <summary>セッション常駐生成の入口。既に在ればそれを返す(UI 層の GameSession から呼ぶ)。</summary>
        public static EventPlayer EnsureResident()
        {
            if (Instance != null)
                return Instance;
            return new GameObject(nameof(EventPlayer)).AddComponent<EventPlayer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EventPlayerService.Current = this; // EventTrigger の発火先 seam に自身を登録
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (Instance == this)
                Instance = null;
            if (ReferenceEquals(EventPlayerService.Current, this))
                EventPlayerService.Current = null;
        }

        // シーンが切り替わるたびに進む番号。再生開始時から変わっていたら、そのイベントは打ち切る。
        // 常駐なので再生中のコルーチンはシーンをまたいで生き残る。戦闘に負けてセーブ再開(シーン再読込)すると
        // 敵が消えて battle が抜け、そのまま続きを流して進行度を進めてしまうのを防ぐ。
        private int _sceneGeneration;

        private void OnActiveSceneChanged(Scene previous, Scene next) => _sceneGeneration++;

        // Inject されたものを優先し、無ければ各 seam / Instance にフォールバックする。
        private ProgressManager _progress;
        private GameModeManager _gameMode;
        private IDialogueView _view;
        private IItemGiver _items;
        private IWeaponGiver _weapons;
        private IBattleRunner _battle;

        private IDialogueView View => _view ??= DialogueViewService.Current;
        private IItemGiver Items => _items ??= ItemGiverService.Current;
        private IWeaponGiver Weapons => _weapons ??= WeaponGiverService.Current;
        private IBattleRunner BattleRunner => _battle ??= BattleRunnerService.Current;
        private ProgressManager Progress =>
            _progress != null ? _progress : ProgressManager.Instance;
        private GameModeManager GameModes =>
            _gameMode != null ? _gameMode : GameModeManager.Instance;

        /// <summary>seam の代わりに依存を注入する(プレビュー / テスト用)。</summary>
        public void Inject(
            ProgressManager progress,
            IDialogueView view,
            IItemGiver items,
            IBattleRunner battle = null,
            GameModeManager gameMode = null,
            IWeaponGiver weapons = null
        )
        {
            _progress = progress;
            _view = view;
            _items = items;
            _battle = battle;
            _gameMode = gameMode;
            _weapons = weapons;
        }

        public void Play(EventDefinition ev, BattleSetup battle = default)
        {
            if (ev == null)
                return;
            StartCoroutine(PlayRoutine(ev, battle));
        }

        /// <summary>
        /// 会話ステップを順に再生し、終了時に AdvanceTo する本体。
        /// battle ステップは <paramref name="battle"/>(トリガーが配線した敵)を使う。
        /// テストは fake を注入し、この IEnumerator を駆動して検証する。
        /// </summary>
        public IEnumerator PlayRoutine(EventDefinition ev, BattleSetup battle = default)
        {
            if (ev == null)
                yield break;

            // 同時に再生できるのは1本だけ。2本目が会話UIを取り合い、先に終わった側が再生中フラグを戻してしまうため弾く。
            if (EventPlaybackService.IsPlaying)
            {
                Debug.LogWarning(
                    $"[EventPlayer] 別のイベントを再生中のため '{ev.Id}' を無視しました。"
                );
                yield break;
            }

            if (View == null)
                Debug.LogWarning(
                    $"[EventPlayer] IDialogueView 未設定 (event={ev.Id}). 会話は表示されません。"
                );

            // 会話イベント中は操作不能。右上ナビ(セーブ/インベ入口)を隠すため再生中フラグを立てる。
            // 途中で打ち切っても finally で必ず戻す。
            int generation = _sceneGeneration;
            EventPlaybackService.SetPlaying(true);
            try
            {
                foreach (var step in ev.Steps)
                {
                    if (step == null)
                        continue;

                    // 戦闘中に会話ウィンドウが残らないよう、battle の前に閉じる。
                    if (step.Kind == StepKind.Battle && View != null)
                        yield return View.Close();

                    switch (step.Kind)
                    {
                        case StepKind.Line:
                            if (View != null)
                                yield return View.ShowLine(step.Speaker, step.Portrait, step.Text);
                            break;

                        case StepKind.Choice:
                            string picked = null;
                            if (View != null)
                                yield return View.ShowChoice(step.Options, v => picked = v);
                            if (!string.IsNullOrEmpty(picked))
                                Progress?.SetFlag(step.FlagKey, picked);
                            break;

                        case StepKind.GiveItem:
                            if (Items == null)
                                Debug.LogWarning(
                                    $"[EventPlayer] IItemGiver 未登録 (event={ev.Id}). giveItem '{step.ItemKey}' は演出だけで所持品には入りません。"
                                );
                            else
                                Items.Give(step.ItemKey);
                            // 入手演出は会話UI側(絵と名前は itemKey から UI が引く)。
                            // 会話UIが無い場面(テスト等)は在庫に入るだけで演出は出ない。
                            if (View != null)
                                yield return View.ShowItemGet(step.ItemKey, step.Message);
                            break;

                        case StepKind.GiveWeapon:
                            if (Weapons == null)
                                Debug.LogWarning(
                                    $"[EventPlayer] IWeaponGiver 未登録 (event={ev.Id}). giveWeapon '{step.WeaponKey}' をスキップ。"
                                        + " プレイヤーリグ(WeaponManager)が居ないシーンでは武器は渡されません。"
                                );
                            else
                                Weapons.GiveWeapon(step.WeaponKey);
                            if (View != null)
                                yield return View.ShowWeaponGet(step.WeaponKey, step.Message);
                            break;

                        case StepKind.Battle:
                            var mode = GameModes;
                            mode?.EnterBattle();
                            if (BattleRunner == null)
                                Debug.LogWarning(
                                    $"[EventPlayer] IBattleRunner 未設定 (event={ev.Id}). 戦闘をスキップ。"
                                );
                            else if (!battle.HasEnemy)
                                Debug.LogWarning(
                                    $"[EventPlayer] battle ステップに敵 Prefab が未配線 (event={ev.Id})."
                                        + " EventTrigger の Enemy スロットにアサインしてください。戦闘をスキップ。"
                                );
                            else
                                yield return BattleRunner.Run(battle);
                            mode?.ExitBattle();
                            break;

                        case StepKind.Command:
                            if (View != null)
                                yield return View.RunCommand(step.CommandName, step.Arg);
                            break;
                    }

                    if (generation != _sceneGeneration)
                    {
                        Debug.LogWarning(
                            $"[EventPlayer] 再生中にシーンが切り替わったため '{ev.Id}' を打ち切りました(進行度は進めない)。"
                        );
                        if (View != null)
                            yield return View.Close();
                        yield break;
                    }
                }

                // 最後の台詞を出したまま終わらないよう、会話ウィンドウを閉じてから進行度を進める。
                if (View != null)
                    yield return View.Close();
                Progress?.AdvanceTo(ev.NextProgress);
            }
            finally
            {
                EventPlaybackService.SetPlaying(false);
            }
        }
    }

    /// <summary>
    /// 会話イベント再生の指揮役。EventTrigger が条件成立時に発火を託す。
    /// </summary>
    public interface IEventPlayer
    {
        /// <summary>
        /// イベントを再生する。battle ステップがあれば <paramref name="battle"/> の Prefab を
        /// トリガー位置に出して戦う(敵未配線なら警告してスキップ)。battle が無いイベントでは
        /// <paramref name="battle"/> は使われない(default で可)。
        /// </summary>
        void Play(EventDefinition ev, BattleSetup battle = default);
    }

    /// <summary>
    /// 実行時の IEventPlayer を登録する seam。EventPlayer は常駐生成で drag 配線できないため EnsureResident 時に登録し、
    /// 非常駐の EventTrigger は Inspector 未配線時のフォールバックとして見る。
    /// </summary>
    public static class EventPlayerService
    {
        public static IEventPlayer Current { get; set; }
    }

    /// <summary>
    /// 会話イベント再生中(= 操作不能)かどうかを UI に伝える seam。EventPlayer が再生の開始/終了で
    /// 更新し、HudIconBar が購読して会話中は右上ナビ(セーブ/インベ入口)を隠す
    /// (会話UI中はセーブ・インベントリ使用不可)。
    /// </summary>
    public static class EventPlaybackService
    {
        public static bool IsPlaying { get; private set; }
        public static event System.Action<bool> PlayingChanged;

        /// <summary>
        /// プレイヤー操作を止めるべきか。再生中でも battle ステップ(Battle モード)の間は戦うので止めない。
        /// PlayerInputHandler が入力を捨てる判定に使う。
        /// </summary>
        public static bool BlocksPlayerControl
        {
            get
            {
                if (!IsPlaying)
                    return false;
                var mode = GameModeManager.Instance;
                return mode == null || mode.CurrentMode != GameMode.Battle;
            }
        }

        public static void SetPlaying(bool playing)
        {
            if (IsPlaying == playing)
                return;
            IsPlaying = playing;
            PlayingChanged?.Invoke(playing);
        }

        /// <summary>購読者には通知せずに初期状態へ戻す(<see cref="EventStatics"/> 用)。</summary>
        internal static void ResetState()
        {
            IsPlaying = false;
            PlayingChanged = null;
        }
    }

    /// <summary>
    /// このプロジェクトは Editor の Enter Play Mode で Domain Reload を切っているため、static な値が前回の Play から持ち越される。
    /// 会話の途中で Play を止めると再生中フラグが立ったまま残り、次の Play で HUD が出ない・動けない・発火しない状態になるので、
    /// Play 開始時に Event 関連の static(再生中フラグ・各 seam)を初期化する。ビルドでは起動時に1回走るだけで無害。
    /// </summary>
    public static class EventStatics
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForPlaySession()
        {
            EventPlaybackService.ResetState();
            EventPlayerService.Current = null;
            DialogueViewService.Current = null;
            BattleRunnerService.Current = null;
            ItemGiverService.Current = null;
            WeaponGiverService.Current = null;
        }
    }

    /// <summary>
    /// 会話UIの seam。実体は UI アセンブリ(CreativeAI.UI)で実装し、EventPlayer に注入する。
    /// Core を最下層に保つため、EventPlayer は具象UIではなくこの契約に依存する。
    /// </summary>
    public interface IDialogueView
    {
        /// <summary>1行表示し、プレイヤーが送るまで待つ(コルーチン)。</summary>
        IEnumerator ShowLine(string speaker, string portrait, string text);

        /// <summary>選択肢を提示し、選ばれた値を onSelected で返す(コルーチン)。</summary>
        IEnumerator ShowChoice(IReadOnlyList<ChoiceOption> options, Action<string> onSelected);

        /// <summary>
        /// giveItem の入手演出。itemKey から絵と名前を引くのは UI 側(Core は Gameplay を参照しないため)。
        /// message 省略時は UI が「〜を手に入れた。」を組み立てる。送り入力まで待つ(コルーチン)。
        /// </summary>
        IEnumerator ShowItemGet(string itemKey, string message);

        /// <summary>
        /// giveWeapon の入手演出。ShowItemGet の武器版(3Dモデルを回して見せる)。
        /// </summary>
        IEnumerator ShowWeaponGet(string weaponKey, string message);

        /// <summary>
        /// command ステップの演出コマンド(window.hide / portrait.left.shake / wait など)を実行する。
        /// </summary>
        IEnumerator RunCommand(string command, string argument);

        /// <summary>
        /// 会話ウィンドウを閉じる。イベント終了時と battle の直前に EventPlayer が呼ぶ。既に閉じていれば何もしない。
        /// </summary>
        IEnumerator Close();
    }

    /// <summary>
    /// 実行時の IDialogueView を登録する seam。Core は UI を参照できず常駐同士で drag 配線もできないため、
    /// 会話UIが生成時に登録し、EventPlayer は Inspector 未配線時のフォールバックとして見る。
    /// </summary>
    public static class DialogueViewService
    {
        public static IDialogueView Current { get; set; }
    }

    /// <summary>
    /// 戦闘の入力一式。敵は events.json ではなくシーンの EventTrigger の Enemy スロットに
    /// 配線した Prefab を使い、トリガー位置(または子の SpawnPoint)へ出す。
    /// </summary>
    public readonly struct BattleSetup
    {
        public readonly GameObject EnemyPrefab;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public BattleSetup(GameObject enemyPrefab, Vector3 position, Quaternion rotation)
        {
            EnemyPrefab = enemyPrefab;
            Position = position;
            Rotation = rotation;
        }

        /// <summary>敵 Prefab が配線されているか。false ならこの戦闘は警告してスキップする。</summary>
        public bool HasEnemy => EnemyPrefab != null;
    }

    /// <summary>
    /// battle ステップの seam(実体は戦闘班)。敵 Prefab をトリガー位置に出し、勝利まで待つコルーチン。
    /// 敗北時はシーン再読込で再開されるので完了しない想定。
    /// </summary>
    public interface IBattleRunner
    {
        IEnumerator Run(BattleSetup setup);
    }

    /// <summary>
    /// 実行時の IBattleRunner を登録する seam。Core は Gameplay を参照できず drag 配線もできないため、
    /// GameSession が登録し、EventPlayer は Inspector 未配線時のフォールバックとして見る。
    /// </summary>
    public static class BattleRunnerService
    {
        public static IBattleRunner Current { get; set; }
    }

    /// <summary>
    /// giveItem ステップの seam。実体は Gameplay(InventoryManager のラッパ)で実装し、
    /// EventPlayer に注入する。Core は Gameplay を参照しないためこの契約を挟む。
    /// </summary>
    public interface IItemGiver
    {
        void Give(string itemKey);

        /// <summary>
        /// itemKey の「大事なもの」を1つ以上所持しているか(hasItem 条件の判定)。
        /// 実体(InventoryManager)は itemKey を ItemDB で引き、カテゴリが 大事なもの の場合のみ
        /// 所持数を見る。装備品/食材/武器のキーや未登録キーは対象外で false を返す。
        /// </summary>
        bool HasImportantItem(string itemKey);
    }

    /// <summary>
    /// 実行時の IItemGiver を登録する seam。Core は Gameplay を参照できず drag 配線もできないため、
    /// InventoryManager が Awake で登録し、EventPlayer は Inspector 未配線時のフォールバックとして見る。
    /// </summary>
    public static class ItemGiverService
    {
        public static IItemGiver Current { get; set; }
    }

    /// <summary>
    /// giveWeapon ステップの seam(実体は WeaponManager、<see cref="IItemGiver"/> と対称)。
    /// 未登録(プレイヤーリグが居ない)なら EventPlayer が警告してスキップする。
    /// </summary>
    public interface IWeaponGiver
    {
        /// <summary>weaponKey(sword/bow/scythe)の武器を1本入手する。既に所持なら何もしない。</summary>
        void GiveWeapon(string weaponKey);
    }

    /// <summary>
    /// 実行時に有効な IWeaponGiver を Core 側へ登録する seam。実装者(プレイヤーリグの WeaponManager)が
    /// Awake で自身を登録し、EventPlayer は Inspector 未配線時のフォールバックとしてここを見る
    /// (<see cref="ItemGiverService"/> と同じ思想)。
    /// </summary>
    public static class WeaponGiverService
    {
        public static IWeaponGiver Current { get; set; }
    }
}
