using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// セッション常駐(進行度・モード…)を「はじめる/続きから」時に生成する入口。
    /// 生成順は マネージャ → プレイヤー(プレイヤーが GameModeManager を購読するため。spec §6.1)。
    /// 各マネージャの Awake が Instance で二重生成をガードするため、再入場で呼んでも冪等。
    /// ここで生成するのは Core 内のもの(マネージャ + EventPlayer)のみ。Inventory(Gameplay) と
    /// UIRoot(UI 層) は Core→Gameplay / Core→UI の循環参照になるためここでは生成せず、
    /// Title フローから InventoryManager.EnsureResident() / UIRoot.EnsureResident() で生成する
    /// (spec §6 の常駐一覧では UIRoot もセッション常駐だが、層の都合で生成場所だけ Title 側)。
    /// プレイヤーリグは GameStarter.EnsurePlayer()。生成順は マネージャ → Inventory → UIRoot → プレイヤー(spec §6.1)。
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
