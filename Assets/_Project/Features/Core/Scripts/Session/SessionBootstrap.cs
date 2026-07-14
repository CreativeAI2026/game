using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// セッション常駐(進行度・モード…)を「はじめる/続きから」時に生成する入口。
    /// 生成順は マネージャ → プレイヤー(プレイヤーが GameModeManager を購読するため。spec §6.1)。
    /// 各マネージャの Awake が Instance で二重生成をガードするため、再入場で呼んでも冪等。
    /// ここで生成するのは Core 内のマネージャのみ。Inventory(Gameplay) は Core→Gameplay の循環参照になるため
    /// ここでは生成せず、Title フロー(UI 層)から InventoryManager.EnsureResident() で生成する。
    /// プレイヤーリグは GameStarter.EnsurePlayer()。生成順は マネージャ → Inventory → プレイヤー(spec §6.1)。
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
