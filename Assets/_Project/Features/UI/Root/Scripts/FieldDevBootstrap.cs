using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// UI 確認/開発用シーン(Scenes/UI 配下・旧 Field_Area01 等)を Title を経由せず直接 Play したときに、
    /// 常駐システム(マネージャ / Inventory / UIRoot / 会話UI)を Title と同じ手順で生成する開発用ブートストラップ。
    ///
    /// Title 経由なら常駐は既に在るので何もしない(冪等)。これにより Field シーン側に常駐のコピー
    /// (InventoryManager や HUD/パネル)を持たせずに済み、二重管理を解消する。
    /// 直接 Play 時は所持品にテスト品を積み、実物の常駐UIでブラッシュアップできる。
    /// 生成順は Title(TitleUIController.EnsureSessionAndPlayer)と揃える(spec §6.1)。
    /// </summary>
    public class FieldDevBootstrap : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("直接 Play 時に所持品へテスト品を積む(UI開発用)。Title 経由では積まれない。")]
        private bool _seedTestItems = true;

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
            // ③ UI レイヤー(会話UI・即時食材使用UI を子として同梱=§6)
            UIRoot.EnsureResident(config != null ? config.uiRootPrefab : null);
            // ④ 戦闘実行
            BattleRunnerService.Current ??= new BattleRunner();

            // 開発用: テスト品を積む(本番 Title フローでは呼ばれない = まっさら)。
            if (_seedTestItems && inventory != null)
                inventory.SeedTestItems();
        }
    }
}
