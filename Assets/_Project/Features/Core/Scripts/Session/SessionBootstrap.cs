using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// セッション常駐(進行度・モード…)を「はじめる/続きから」時に生成する入口(冪等)。Core 内のもの(マネージャ + EventPlayer)だけ作る。
    /// Inventory / UIRoot は循環参照を避けるため Title フローから各 EnsureResident() で生成する。
    /// 生成順は マネージャ → Inventory → UIRoot → プレイヤー(GameStarter.EnsurePlayer())。
    /// </summary>
    public static class SessionBootstrap
    {
        /// <summary>未生成のセッション常駐を生成する。既に在ればそのまま。</summary>
        public static void EnsureSession()
        {
            if (ProgressManager.Instance == null)
                new GameObject(nameof(ProgressManager)).AddComponent<ProgressManager>();

            if (GameModeManager.Instance == null)
                new GameObject(nameof(GameModeManager)).AddComponent<GameModeManager>();

            // 会話イベントの指揮役。常駐化により EventTrigger の per-field 配線が不要になる
            // (EventTrigger は EventPlayerService.Current にフォールバックする)。EventPlayer は Core なのでここで直接生成する。
            EventPlayer.EnsureResident();
        }
    }
}
