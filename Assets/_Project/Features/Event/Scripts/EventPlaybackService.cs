namespace CreativeAI.Core
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
}
