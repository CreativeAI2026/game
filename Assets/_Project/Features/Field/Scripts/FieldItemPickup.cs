using System;
using System.Collections.Generic;
using CreativeAI.Core;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// フィールドに置かれた取得可能アイテム。プレイヤーが触れると在庫へ入れて自分を消す。拾えるのは移動中(Field)だけ。
    /// 装備品は拾った瞬間に付与ステータスをロールするので1個ずつ別スタックで持つ(食材・大事なものは数量ぶん積む)。
    /// 配置はシーンに手置き: Collider(Is Trigger)とこのコンポーネントを付け、Item と Sparkle をアサインする。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FieldItemPickup : MonoBehaviour
    {
        /// <summary>拾った装備品に付く能力値の数の上限。</summary>
        public const int MaxDropStatCount = 2;

        /// <summary>
        /// 付く型の抽選重み(攻撃%/防御%/最大HP% 各2、会心ダメージ/会心率 各1 = 合計8)。
        /// 列挙順を固定して抽選を再現可能にするため配列にしている。食材専用の HealAmount は含めない。
        /// </summary>
        public static readonly IReadOnlyList<(StatType Type, int Weight)> DropStatWeights = new[]
        {
            (StatType.AttackPct, 2),
            (StatType.DefensePct, 2),
            (StatType.MaxHpPct, 2),
            (StatType.CritDamage, 1),
            (StatType.CritRate, 1),
        };

        [Tooltip(
            "拾えるアイテム(装備品 / 食材 / 大事なもの)。武器は在庫外なのでここには置けない。"
        )]
        [SerializeField]
        private ItemData _item;

        [Tooltip("拾える個数。装備品は1個ずつ別個体としてロールする。")]
        [SerializeField, Min(1)]
        private int _count = 1;

        [Tooltip("キラキラエフェクト(任意)。拾った瞬間に消す。")]
        [SerializeField]
        private GameObject _sparkle;

        [SerializeField]
        private string _playerTag = "Player";

        [Tooltip("OFF にすると拾っても GameObject を残す(再配置・演出を配置側で管理したいとき)。")]
        [SerializeField]
        private bool _destroyOnPickup = true;

        private bool _picked;

        /// <summary>すでに拾われたか(二重取得防止)。</summary>
        public bool IsPicked => _picked;

        /// <summary>拾えるアイテム。配置ツール・テストからの確認用。</summary>
        public ItemData Item => _item;

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag(_playerTag))
                return;
            TryPickup();
        }

        /// <summary>
        /// 在庫へ入れて自分を片付ける。拾えたら true。
        /// 拾えない条件(既に拾われた / 未設定 / 戦闘中 / 会話イベント中 / 在庫が無い)では
        /// <b>何も消費・変更しない</b>。
        /// </summary>
        public bool TryPickup()
        {
            if (_picked)
                return false;

            if (_item == null)
            {
                Debug.LogWarning(
                    $"[FieldItemPickup] '{name}': Item が未設定のため拾得をスキップしました。"
                );
                return false;
            }

            // 移動中(Field)のみ。マネージャ未生成(開発シーン直 Play)は Field 扱いで許可する。
            var mode = GameModeManager.Instance;
            if (mode != null && mode.CurrentMode != GameMode.Field)
                return false;
            if (EventPlaybackService.IsPlaying)
                return false;

            var inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                Debug.LogWarning(
                    $"[FieldItemPickup] '{name}': InventoryManager が無いため拾得をスキップしました。"
                );
                return false;
            }

            if (_item is EquipmentData equipment)
            {
                // 装備品は拾った瞬間にロール。個体差があるので1個ずつ別スタック。
                var rng = new SystemRandomSource();
                double seedPower = EquipmentData.ToStatVector(equipment).Power;
                for (int i = 0; i < _count; i++)
                    inventory.AddInstance(
                        equipment,
                        RolledStat.FromVector(RollDropStats(seedPower, rng))
                    );
            }
            else
            {
                // 食材(HP即時回復の固定ルール)・大事なもの。ロールは通さない。
                inventory.AddItem(_item, _count);
            }

            _picked = true;
            if (_sparkle != null)
                _sparkle.SetActive(false);

            if (!_destroyOnPickup)
                return true;

            if (Application.isPlaying)
                Destroy(gameObject);
            else
                gameObject.SetActive(false); // EditMode(テスト)では Destroy を呼べない
            return true;
        }

        // --- 拾った装備品の能力値の抽選 ---
        // 調合と同じ「総パワー × ディリクレ配分」。総パワー = 装備品の固定ステータス合計(C_cap でクランプ)、
        // 型 = 重み付き非復元抽出、配分の期待値 = 均等(親が居ないため)。

        /// <param name="seedPower">拾った装備品の固定ステータス合計(= 総パワー)。</param>
        /// <param name="statCount">付与数。既定は上限の2(範囲外は丸める)。</param>
        public static StatVector RollDropStats(
            double seedPower,
            IRandomSource rng,
            StatRollParameters parameters = null,
            int statCount = MaxDropStatCount
        )
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            var p = parameters ?? StatRollParameters.Default;
            double budget = Math.Min(seedPower, p.PowerCap);
            if (budget <= 0.0)
                return StatVector.Empty;

            var types = RollDropStatTypes(rng, Math.Min(statCount, p.MaxStatCount));
            if (types.Count == 0)
                return StatVector.Empty;

            double[] shares;
            if (types.Count == 1)
            {
                shares = new[] { 1.0 };
            }
            else
            {
                var alpha = new double[types.Count];
                for (int i = 0; i < alpha.Length; i++)
                    alpha[i] = p.Alpha0 / types.Count;
                shares = ProbabilityDistributions.SampleDirichlet(alpha, rng);
            }

            return p.Distribute(types, shares, budget);
        }

        /// <summary>付く型を重み付き非復元抽出で選ぶ(同じ型は重複しない)。選ばれた順に返す。</summary>
        /// <param name="count">選ぶ型の数。0〜<see cref="MaxDropStatCount"/> に丸める。</param>
        public static IReadOnlyList<StatType> RollDropStatTypes(
            IRandomSource random,
            int count = MaxDropStatCount
        )
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            int n = Math.Clamp(count, 0, MaxDropStatCount);
            var picked = new List<StatType>(n);
            var remaining = new List<(StatType Type, int Weight)>(DropStatWeights);
            int totalWeight = 0;
            foreach (var w in remaining)
                totalWeight += w.Weight;

            for (int i = 0; i < n && remaining.Count > 0; i++)
            {
                // [0, totalWeight) の一様値を累積重みで引く。境界の丸めで溢れても最後の候補に落とす。
                double r = random.NextDouble() * totalWeight;
                int index = remaining.Count - 1;
                double acc = 0;
                for (int j = 0; j < remaining.Count; j++)
                {
                    acc += remaining[j].Weight;
                    if (r < acc)
                    {
                        index = j;
                        break;
                    }
                }

                picked.Add(remaining[index].Type);
                totalWeight -= remaining[index].Weight; // 非復元: 選んだ型のぶん母数を減らす
                remaining.RemoveAt(index);
            }

            return picked;
        }
    }
}
