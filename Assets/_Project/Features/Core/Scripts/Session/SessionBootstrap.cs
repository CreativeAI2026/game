using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// セッション常駐(進行度・モード…)を「はじめる/続きから」時に生成する入口。
    /// 生成順は マネージャ → プレイヤー(プレイヤーが GameModeManager を購読するため。spec §6.1)。
    /// 各マネージャの Awake が Instance で二重生成をガードするため、再入場で呼んでも冪等。
    /// プレイヤーリグ・Inventory は各担当が同フローに追加していく。
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
        }
    }
}
