using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 会話イベント再生中(= 操作不能)かどうかを UI に伝える seam。EventPlayer が再生の開始/終了で
    /// 更新し、HudIconBarView が購読して会話中は右上ナビ(セーブ/インベ入口)を隠す
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
        public static bool BlocksPlayerControl => IsPlaying && !IsInBattle;

        /// <summary>戦闘モード中か。マネージャ未生成(開発シーン直 Play など)は Field 扱いで false。</summary>
        public static bool IsInBattle =>
            GameModeManager.Instance != null
            && GameModeManager.Instance.CurrentMode == GameMode.Battle;

        /// <summary>
        /// フィールドを自由に動いているか(戦闘中でも会話イベント再生中でもない)。
        /// セーブ・拾う・調べる・HUD の出し分けなど、「移動中だけ」の判定はこれを使う。
        /// </summary>
        public static bool IsFreeRoaming => !IsPlaying && !IsInBattle;

        public static void SetPlaying(bool playing)
        {
            if (IsPlaying == playing)
                return;
            IsPlaying = playing;
            PlayingChanged?.Invoke(playing);
        }

        /// <summary>購読者には通知せずに初期状態へ戻す。</summary>
        // Domain Reload を切った Editor で Play を繰り返したとき、前回の Play の値や購読を持ち越さないよう、Play 開始時に戻す
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetState()
        {
            IsPlaying = false;
            PlayingChanged = null;
        }
    }
}
