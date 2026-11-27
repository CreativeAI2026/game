using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの HP と、攻撃力・防御力などの最終ステータスを持つ。
    /// 素の値は PlayerParameterData、補正は装備(EquipmentSlots)と選択中の武器(WeaponManager)から、読むたびに計算する
    /// (補正の写しは持たない)。最大HPが変わったときに現在HPを合わせるため、前回の最大HPだけを覚えておく。
    /// 食材を食べるなど、所持品と HP の両方を変える処理は <see cref="FoodUse"/> 側に置く。
    /// </summary>
    public sealed class PlayerStatus : MonoBehaviour, IDamageable
    {
        [SerializeField]
        private PlayerParameterData _playerData;

        public float CurrentHp { get; private set; }

        /// <summary>HP が 0 になったか。死亡後は被ダメージ・回復を受け付けない。</summary>
        public bool IsDead { get; private set; }

        /// <summary>HP が 0 になった瞬間に1回だけ通知する(操作停止は PlayerDeathHandler、ゲームオーバー進行は購読側)。</summary>
        public event Action Died;

        /// <summary>ダメージを受けた瞬間に通知する(死亡時も含む)。</summary>
        public event Action Damaged;

        /// <summary>装備品による補正合計(武器は含まない)。バフ表示などの読み取り用。</summary>
        public EquipmentBonus EquipmentBonus =>
            InventoryManager.CurrentEquipment != null
                ? InventoryManager.CurrentEquipment.GetBonus()
                : default;

        // 選択中の武器による補正合計(武器の見た目には頼らない)。
        private static EquipmentBonus WeaponBonus =>
            WeaponManager.Instance != null ? WeaponManager.Instance.GetSelectedBonus() : default;

        // 前回の最大HP。補正が変わったとき、増えた分だけ現在HPを足すために使う。
        private float _lastMaxHp;

        // HPが変動したときに通知するデリゲート。引数は（現在のHP、最大HP）
        public event Action<float, float> HpChanged;

        // 装備変更などで攻撃/防御/最大HP等が変わったときの通知(HUD等が最終値を読み直す)。
        public event Action StatsChanged;

        private PlayerFlinchHandler _flinchHandler;

        private PlayerHitInvincibility _invincible;

        // 購読している装備の枠(所持品の一部)と武器の常駐(補正の計算し直しの合図を受ける)。
        private EquipmentSlots _equipmentSlots;
        private WeaponManager _weaponManager;

        private void Awake()
        {
            TryGetComponent(out _flinchHandler);
            TryGetComponent(out _invincible);
            _lastMaxHp = CurrentMaxHp;
        }

        private void OnEnable()
        {
            BindEquipment();
        }

        private void OnDisable()
        {
            if (_equipmentSlots != null)
                _equipmentSlots.Changed -= OnBonusChanged;
            _equipmentSlots = null;
            if (_weaponManager != null)
                _weaponManager.SelectionChanged -= OnWeaponSwitched;
            _weaponManager = null;
        }

        public float CurrentMaxHp
        {
            get
            {
                float buffBonus = 0f;
                float pct = EquipmentBonus.maxHpPct + WeaponBonus.maxHpPct; // %ポイント合計
                return _playerData.baseMaxLife * (1f + pct / 100f) + buffBonus;
            }
        }

        /// <summary>
        /// 最大HPのうち、装備のバフ(最大HP%増加)で増えた分。HUD がバフ分のゲージを分けて表示するための読み取り用。
        /// 装備で最大HPが下がっている場合は負になる。
        /// </summary>
        public float EquipmentMaxHpBonus =>
            _playerData.baseMaxLife * EquipmentBonus.maxHpPct / 100f;

        public float CurrentAttackPower
        {
            get
            {
                float buffBonus = 0f;
                float pct = EquipmentBonus.attackPct + WeaponBonus.attackPct; // %ポイント合計
                return _playerData.baseAttackPower * (1f + pct / 100f) + buffBonus;
            }
        }

        public float CurrentDefense
        {
            get
            {
                float buffBonus = 0f;
                float pct = EquipmentBonus.defensePct + WeaponBonus.defensePct; // %ポイント合計
                return _playerData.baseDefense * (1f + pct / 100f) + buffBonus;
            }
        }

        public float CurrentCriticalChance
        {
            get
            {
                float buffBonus = 0f;

                float finalCriticalChance =
                    _playerData.baseCriticalChance
                    + EquipmentBonus.criticalChance
                    + WeaponBonus.criticalChance
                    + buffBonus;

                if (finalCriticalChance > 100f)
                {
                    return 100f;
                }
                else if (finalCriticalChance < 0f)
                {
                    return 0f;
                }

                return finalCriticalChance;
            }
        }

        public float CurrentCriticalDamageRatio
        {
            get
            {
                float buffBonus = 0f;
                return _playerData.baseCriticalDamageRatio
                    + EquipmentBonus.criticalDamage
                    + WeaponBonus.criticalDamage
                    + buffBonus;
            }
        }

        private void Start()
        {
            BindEquipment();
            CurrentHp = CurrentMaxHp; // 装備・武器込みの最大HPで満タン開始
            _lastMaxHp = CurrentMaxHp;
            HpChanged?.Invoke(CurrentHp, CurrentMaxHp);
            StatsChanged?.Invoke();
        }

        // 装備の枠と武器の常駐を購読する。常駐はふつうプレイヤーより先に作られるが、
        // シーンに置いたリグが常駐より先に有効になったときに備え、Start でももう一度試す。
        private void BindEquipment()
        {
            if (_equipmentSlots == null && InventoryManager.CurrentEquipment != null)
            {
                _equipmentSlots = InventoryManager.CurrentEquipment;
                _equipmentSlots.Changed += OnBonusChanged;
            }
            if (_weaponManager == null && WeaponManager.Instance != null)
            {
                _weaponManager = WeaponManager.Instance;
                _weaponManager.SelectionChanged += OnWeaponSwitched;
            }
        }

        private void OnWeaponSwitched(bool _) => OnBonusChanged();

        // 装備か選択中の武器が変わったとき。最大HPが増えたら増えた分だけ現在HPも増やし、減ったら現在HPを抑える
        // (増えた分を足さないと、途中で付けたバフの増加分が空のまま始まるため)。死亡後は HP を戻さない。
        private void OnBonusChanged()
        {
            float maxHp = CurrentMaxHp;
            float gained = maxHp - _lastMaxHp;
            _lastMaxHp = maxHp;
            if (gained > 0f && !IsDead)
                CurrentHp += gained;
            if (CurrentHp > maxHp)
                CurrentHp = maxHp;
            StatsChanged?.Invoke();
            HpChanged?.Invoke(CurrentHp, maxHp);
        }

        public void TakeDamage(float damage, bool isCritical, bool suppressFlinch = false)
        {
            if (IsDead)
                return;

            // 無敵は攻撃の連続被弾を防ぐためのもの。毒・拘束などの持続ダメージ(suppressFlinch)は止めない
            if (!suppressFlinch && _invincible != null && _invincible.IsInvincible)
                return;

            float finalDamage = DamageFormula.ApplyDefense(damage, CurrentDefense);

            CurrentHp = Mathf.Max(0f, CurrentHp - finalDamage);
            HpChanged?.Invoke(CurrentHp, CurrentMaxHp);
            DevLog.Log(
                $"プレイヤーは {finalDamage} ダメージを受けた！ 残りHP: {CurrentHp}/{CurrentMaxHp}"
            );

            if (CameraShakeManager.Instance != null)
                CameraShakeManager.Instance.Shake(0.5f);
            // 画面演出(赤ビネット等)は UI 側がこのイベントを購読して行う(プレイヤーは UI を知らない)
            Damaged?.Invoke();

            if (CurrentHp <= 0f)
            {
                Die();
                return; // 死亡時は怯ませない
            }

            if (!suppressFlinch)
            {
                if (_flinchHandler != null)
                    _flinchHandler.TriggerFlinch();
            }
        }

        /// <summary>
        /// 武器固有の倍率を受け取り、会心判定を含めた最終ダメージを算出する。
        /// ダメージ計算を一元化することで、装備やバフの影響を武器側に意識させない設計。
        /// </summary>
        /// <param name="weaponMultiplier">攻撃ごとの倍率（通常攻撃は1.0、ハンマーの叩きつけ等の大技のみ1.0超）</param>
        /// <param name="isCritical">会心が発生したかどうかの結果を返す</param>
        public float RollDamage(float weaponMultiplier, out bool isCritical)
        {
            float baseDmg = CurrentAttackPower * weaponMultiplier;

            // Random.Range(0,100) は両端を含むため、会心率0%のときは抽選しない(0 が出ると会心になってしまう)
            isCritical =
                CurrentCriticalChance > 0f
                && UnityEngine.Random.Range(0f, 100f) <= CurrentCriticalChance;
            if (isCritical)
            {
                // 会心ダメージは%ポイント(会心率と同スケール)。÷100 して攻撃力へ上乗せする。
                baseDmg += baseDmg * (CurrentCriticalDamageRatio / 100f);
            }

            return baseDmg;
        }

        /// <summary>HP を amount だけ回復する(最大HPまで)。死亡中は何もしない。</summary>
        public void Heal(float amount)
        {
            if (IsDead)
                return;

            CurrentHp = Mathf.Min(CurrentHp + amount, CurrentMaxHp);
            HpChanged?.Invoke(CurrentHp, CurrentMaxHp);
            DevLog.Log($"プレイヤーは {amount} 回復した！ 残りHP: {CurrentHp}/{CurrentMaxHp}");
        }

        private void Die()
        {
            IsDead = true;
            Died?.Invoke();
        }

        // --- セーブ(SaveService・LoadService から呼ばれる) ---

        /// <summary>保存時の現在HPを返す。</summary>
        public float CaptureHp() => CurrentHp;

        /// <summary>
        /// 復元時に現在HPを設定する。装備込みの最大HPでクランプする
        /// (装備の復元が先に済んでいる前提。LoadService 側で順序を保証)。
        /// </summary>
        public void RestoreHp(float hp)
        {
            CurrentHp = Mathf.Clamp(hp, 0f, CurrentMaxHp);
            _lastMaxHp = CurrentMaxHp;
            IsDead = CurrentHp <= 0f;
            HpChanged?.Invoke(CurrentHp, CurrentMaxHp);
        }
    }
}
