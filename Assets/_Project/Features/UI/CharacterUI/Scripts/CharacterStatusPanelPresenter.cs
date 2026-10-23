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

        [Header("文字アニメーション")]
        [SerializeField]
        private bool _animateTextOnOpen = true;

        [SerializeField, Min(0f)]
        private float _textStartDelay = 0.08f;

        [SerializeField, Min(0.001f)]
        private float _characterInterval = 0.045f;

        [SerializeField, Min(0f)]
        private float _rowDelay = 0.04f;

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

        private TmpTypewriterPlayer _typewriter;

        private void Awake()
        {
            _typewriter = new TmpTypewriterPlayer(this);
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            _typewriter ??= new TmpTypewriterPlayer(this);
            if (!_useTestValues)
                Subscribe();
            RefreshAll(false, _animateTextOnOpen);
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
                RefreshAll(false, false);
            }
        }

        private void OnDisable()
        {
            _typewriter?.CompleteAll();
            Unsubscribe();
        }

        [ContextMenu("テスト値を反映してHPバーを再生")]
        private void ReplayTestStatus()
        {
            ResolveReferences();
            _typewriter ??= new TmpTypewriterPlayer(this);
            RefreshAll(false, _animateTextOnOpen);
            _hpBar?.PlayOpenAnimation();
        }

        private void OnHpChanged(float current, float maximum)
        {
            if (_useTestValues)
                return;

            SetHp(current, maximum, true, false);
        }

        private void OnStatsChanged()
        {
            if (_useTestValues)
                return;

            RefreshAll(true, false);
        }

        private void RefreshAll(bool animateHp, bool animateText)
        {
            if (_useTestValues)
            {
                SetHp(_testCurrentHp, _testMaxHp, animateHp, animateText);
                SetNumber(_attackValue, _testAttack, 1, animateText);
                SetNumber(_defenseValue, _testDefense, 2, animateText);
                SetPercent(_criticalRateValue, _testCriticalRate, 3, animateText);
                SetPercent(_criticalDamageValue, _testCriticalDamage, 4, animateText);
                return;
            }

            if (_playerStatus == null)
                return;

            SetHp(_playerStatus.CurrentHp, _playerStatus.CurrentMaxHp, animateHp, animateText);
            SetNumber(_attackValue, _playerStatus.CurrentAttackPower, 1, animateText);
            SetNumber(_defenseValue, _playerStatus.CurrentDefense, 2, animateText);
            SetPercent(_criticalRateValue, _playerStatus.CurrentCriticalChance, 3, animateText);
            SetPercent(
                _criticalDamageValue,
                _playerStatus.CurrentCriticalDamageRatio,
                4,
                animateText
            );
        }

        private void SetHp(float current, float maximum, bool animateBar, bool animateText)
        {
            float clampedMaximum = Mathf.Max(0f, maximum);
            float clampedCurrent = Mathf.Clamp(current, 0f, clampedMaximum);

            PlayText(
                _hpValue,
                $"{Mathf.RoundToInt(clampedCurrent)}/{Mathf.RoundToInt(clampedMaximum)}",
                0,
                animateText
            );

            _hpBar?.SetValue(clampedCurrent, clampedMaximum, animateBar);
        }

        private void SetNumber(TMP_Text text, float value, int rowIndex, bool animate)
        {
            PlayText(text, Mathf.RoundToInt(value).ToString(), rowIndex, animate);
        }

        private void SetPercent(TMP_Text text, float value, int rowIndex, bool animate)
        {
            PlayText(text, $"{value:0.##}%", rowIndex, animate);
        }

        private void PlayText(TMP_Text text, string value, int rowIndex, bool animate)
        {
            _typewriter ??= new TmpTypewriterPlayer(this);
            _typewriter.Play(
                text,
                value,
                _textStartDelay + _rowDelay * rowIndex,
                _characterInterval,
                animate
            );
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
