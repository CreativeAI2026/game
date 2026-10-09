using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    /// <summary>
    /// Character画面のStatusPanelへ、プレイヤーの現在ステータスを反映する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterStatusPanelPresenter : MonoBehaviour
    {
        private PlayerStatus _playerStatus;

        [SerializeField]
        private TMP_Text _hpValue;

        [SerializeField]
        private TMP_Text _attackValue;

        [SerializeField]
        private TMP_Text _defenseValue;

        [SerializeField]
        private TMP_Text _criticalRateValue;

        [SerializeField]
        private TMP_Text _criticalDamageValue;

        [SerializeField]
        private AnimatedValueBar _hpBar;

        [Header("テスト表示")]
        [SerializeField, InspectorName("テスト値を使う")]
        private bool _useTestValues;

        [SerializeField, Min(0f), InspectorName("HP")]
        private float _testCurrentHp = 80f;

        [SerializeField, Min(0f), InspectorName("Max HP")]
        private float _testMaxHp = 100f;

        [SerializeField, Min(0f), InspectorName("ATK")]
        private float _testAttack = 70f;

        [SerializeField, Min(0f), InspectorName("DEF")]
        private float _testDefense = 30f;

        [SerializeField, Min(0f), InspectorName("CRTR")]
        private float _testCriticalRate = 3f;

        [SerializeField, Min(0f), InspectorName("CRTD")]
        private float _testCriticalDamage = 150f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (!_useTestValues)
                Subscribe();
            RefreshAll(false);
            _hpBar?.PlayOpenAnimation();
        }

        private void OnValidate()
        {
            _testMaxHp = Mathf.Max(0f, _testMaxHp);
            _testCurrentHp = Mathf.Clamp(_testCurrentHp, 0f, _testMaxHp);

            if (Application.isPlaying && isActiveAndEnabled)
            {
                Unsubscribe();
                if (!_useTestValues)
                    Subscribe();
                RefreshAll(false);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        [ContextMenu("テスト値を反映してHPバーを再生")]
        private void ReplayTestStatus()
        {
            ResolveReferences();
            RefreshAll(false);
            _hpBar?.PlayOpenAnimation();
        }

        private void OnHpChanged(float current, float maximum)
        {
            if (_useTestValues)
                return;

            SetHp(current, maximum, true);
        }

        private void OnStatsChanged()
        {
            if (_useTestValues)
                return;

            RefreshAll(true);
        }

        private void RefreshAll(bool animateHp)
        {
            if (_useTestValues)
            {
                SetHp(_testCurrentHp, _testMaxHp, animateHp);
                SetNumber(_attackValue, _testAttack);
                SetNumber(_defenseValue, _testDefense);
                SetPercent(_criticalRateValue, _testCriticalRate);
                SetPercent(_criticalDamageValue, _testCriticalDamage);
                return;
            }

            if (_playerStatus == null)
                return;

            SetHp(_playerStatus.CurrentHp, _playerStatus.CurrentMaxHp, animateHp);
            SetNumber(_attackValue, _playerStatus.CurrentAttackPower);
            SetNumber(_defenseValue, _playerStatus.CurrentDefense);
            SetPercent(_criticalRateValue, _playerStatus.CurrentCriticalChance);
            SetPercent(_criticalDamageValue, _playerStatus.CurrentCriticalDamageRatio);
        }

        private void SetHp(float current, float maximum, bool animate)
        {
            float clampedMaximum = Mathf.Max(0f, maximum);
            float clampedCurrent = Mathf.Clamp(current, 0f, clampedMaximum);

            if (_hpValue != null)
            {
                _hpValue.text =
                    $"{Mathf.RoundToInt(clampedCurrent)}/{Mathf.RoundToInt(clampedMaximum)}";
            }

            _hpBar?.SetValue(clampedCurrent, clampedMaximum, animate);
        }

        private static void SetNumber(TMP_Text text, float value)
        {
            if (text != null)
                text.text = Mathf.RoundToInt(value).ToString();
        }

        private static void SetPercent(TMP_Text text, float value)
        {
            if (text != null)
                text.text = $"{value:0.##}%";
        }

        private void Subscribe()
        {
            if (_playerStatus == null)
                return;

            _playerStatus.OnHpChanged -= OnHpChanged;
            _playerStatus.OnHpChanged += OnHpChanged;
            _playerStatus.OnStatsChanged -= OnStatsChanged;
            _playerStatus.OnStatsChanged += OnStatsChanged;
        }

        private void Unsubscribe()
        {
            if (_playerStatus == null)
                return;

            _playerStatus.OnHpChanged -= OnHpChanged;
            _playerStatus.OnStatsChanged -= OnStatsChanged;
        }

        private void ResolveReferences()
        {
            if (_playerStatus == null)
                _playerStatus = FindAnyObjectByType<PlayerStatus>();

            _hpValue ??= FindValueText("HP");
            _attackValue ??= FindValueText("ATK");
            _defenseValue ??= FindValueText("DEF");
            _criticalRateValue ??= FindValueText("CRIR");
            _criticalDamageValue ??= FindValueText("CRID");
            _hpBar ??= transform.Find("StatusArea/HP/Bar")?.GetComponent<AnimatedValueBar>();
        }

        private TMP_Text FindValueText(string rowName) =>
            transform.Find($"StatusArea/{rowName}/TextArea/ValueText")?.GetComponent<TMP_Text>();
    }
}
