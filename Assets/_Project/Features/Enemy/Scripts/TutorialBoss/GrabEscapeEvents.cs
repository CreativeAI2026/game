using System;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TutorialBoss などの敵から UI（別アセンブリ）へ掴み・脱出ゲージの更新を通知するためのイベント。
    /// UI 側でこれを購読して表示を切り替えることで、アセンブリ間の循環参照を防ぐ。
    /// </summary>
    public static class GrabEscapeEvents
    {
        /// <summary>
        /// ゲージを表示するイベント。引数は (現在の値, 最大値)。
        /// </summary>
        public static Action<float, float> OnShowGauge;

        /// <summary>
        /// ゲージの値を更新するイベント。引数は (現在の値, 最大値)。
        /// </summary>
        public static Action<float, float> OnUpdateGauge;

        /// <summary>
        /// ゲージを非表示にするイベント。
        /// </summary>
        public static Action OnHideGauge;

        // ─── カメラフェーズ通知 ───────────────────────────────

        /// <summary>
        /// 掴み引き寄せフェーズ開始。vcamPull を優先させる。
        /// </summary>
        public static Action OnCameraPull;

        /// <summary>
        /// 電撃ダメージフェーズ開始。vcamDamage を優先させる。
        /// </summary>
        public static Action OnCameraDamage;

        /// <summary>
        /// 脱出フェーズ開始。vcamEscape を優先させる。
        /// </summary>
        public static Action OnCameraEscape;

        /// <summary>
        /// 掴み終了。すべての掴みカメラを解除してメインカメラに戻す。
        /// </summary>
        public static Action OnCameraEnd;
    }
}
