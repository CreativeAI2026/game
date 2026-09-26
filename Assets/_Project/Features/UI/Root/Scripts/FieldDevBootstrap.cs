using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Core.SceneManagement;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 開発用シーンを Title を経由せず直接 Play したとき、常駐システム(マネージャ / Inventory / UIRoot / 会話UI)を
    /// Title と同じ順(TitleUIController.EnsureSessionAndPlayer)で生成する開発用ブートストラップ。Title 経由なら何もしない(冪等)。
    /// Field シーン側に常駐のコピーを持たせないためのもの。直接 Play 時は所持品にテスト品を積む。
    /// </summary>
    public class FieldDevBootstrap : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("直接 Play 時に所持品へテスト品を積む(UI開発用)。Title 経由では積まれない。")]
        private bool _seedTestItems = true;

        [SerializeField]
        [Tooltip(
            "直接 Play 時にプレイヤーリグも出す。フィールドシーンだけ ON にする"
                + "(UI 確認シーンでは要らないし、リグのカメラが二重になるので既定 OFF)。"
        )]
        private bool _spawnPlayerRig;

        [SerializeField]
        [Tooltip("リグを出す位置の SpawnPoint ID。本番の Start Spawn / Dest Spawn と同じ仕組み。")]
        private string _spawnPointId = "start";

        private void Awake()
        {
            // Title 経由で既に常駐が生成済みなら何もしない(UIRoot の有無で判定)。
            if (UIRoot.Instance != null)
                return;

            var config = Resources.Load<ResidentBootstrapConfig>(nameof(ResidentBootstrapConfig));
            if (config == null)
            {
                Debug.LogWarning(
                    "[FieldDevBootstrap] ResidentBootstrapConfig が Resources に見つかりません。"
                        + "Tools/CreativeAI/UI/Create Resident Bootstrap Config で作成してください。"
                );
            }

            // ① マネージャ(ProgressManager / GameModeManager / EventPlayer)
            SessionBootstrap.EnsureSession();
            // ② 所持品
            var inventory = InventoryManager.EnsureResident();
            // ②' レシピ解禁状態
            RecipeBookManager.EnsureResident();
            // ③ UI レイヤー(会話UI・即時食材使用UI を子として同梱)
            UIRoot.EnsureResident(config != null ? config.uiRootPrefab : null);
            // ④ 戦闘実行
            BattleRunnerService.Current ??= new BattleRunner();

            // ⑤ プレイヤーリグ(Title と同じ GameStarter.EnsurePlayerRig を通し、
            //    配置も本番と同じ SpawnPoint 経由にする)。
            if (_spawnPlayerRig)
            {
                var player = GameStarter.EnsurePlayerRig(
                    config != null ? config.playerRigPrefab : null
                );
                if (player != null)
                    SpawnPoint.Place(player, _spawnPointId);
            }

            // 開発用: テスト品を積む(本番 Title フローでは呼ばれない = まっさら)。
            if (_seedTestItems && inventory != null)
                inventory.SeedTestItems();
        }
    }
}
