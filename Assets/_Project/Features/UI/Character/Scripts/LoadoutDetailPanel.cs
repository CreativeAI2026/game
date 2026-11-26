using System.Globalization;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// キャラクター画面の右の詳細欄。選んだアイテムのアイコン・名前・説明・所持数・パラメータと、
    /// 「登録 / 装備 / 解除」のボタンを出す。名前と説明は1文字ずつ出す。
    /// パラメータが無いアイテムのときは、パラメータ行の分だけ欄を縮める。
    /// </summary>
    public sealed class LoadoutDetailPanel : MonoBehaviour
    {
        /// <summary>詳細欄のボタンで行うこと。</summary>
        public enum ButtonAction
        {
            None,
            Assign,
            RemoveQuick,
            RemoveEquipment,
        }

        [Header("アイテム")]
        [SerializeField]
        private GameObject _iconArea;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _description;

        [SerializeField]
        private GameObject _buttonArea;

        [SerializeField]
        private TMP_Text _count;

        [SerializeField]
        private Button _actionButton;

        [SerializeField]
        private TMP_Text _actionLabel;

        [Header("パラメータ")]
        [SerializeField]
        private LayoutElement _areaLayout; // この欄の高さ

        [SerializeField]
        private LayoutElement _parameterRow;

        [SerializeField]
        private GameObject _quickParameterPattern;

        [SerializeField]
        private TMP_Text _quickParameterLabel;

        [SerializeField]
        private TMP_Text _quickParameterValue;

        [SerializeField]
        private GameObject _equipmentParameterPattern;

        [SerializeField]
        private TMP_Text _attackValue;

        [SerializeField]
        private TMP_Text _defenseValue;

        [SerializeField]
        private TMP_Text _maxHpValue;

        [SerializeField]
        private TMP_Text _criticalRateValue;

        [SerializeField]
        private TMP_Text _criticalDamageValue;

        [Header("文字送り")]
        [SerializeField]
        private bool _animateText = true;

        [SerializeField, Min(0f)]
        private float _textStartDelay = 0.05f;

        [SerializeField, Min(0.001f)]
        private float _characterInterval = 0.035f;

        [SerializeField, Min(0f)]
        private float _sectionDelay = 0.06f; // 名前の次に説明を出すまでの遅れ

        private TmpTypewriterPlayer _typewriter;
        private bool _heightsMeasured;
        private float _expandedHeight;
        private float _parameterHeight;

        /// <summary>今ボタンで行うこと。</summary>
        public ButtonAction CurrentAction { get; private set; }

        private TmpTypewriterPlayer Typewriter => _typewriter ??= new TmpTypewriterPlayer(this);

        /// <summary>詳細欄のボタンが押されたときの処理を足す。</summary>
        public void AddActionListener(UnityAction action) =>
            _actionButton.onClick.AddListener(action);

        /// <summary>詳細欄のボタンが押されたときの処理を外す。</summary>
        public void RemoveActionListener(UnityAction action) =>
            _actionButton.onClick.RemoveListener(action);

        /// <summary>アイテムの詳細を出す。stack は中身があるものを渡す。</summary>
        public void Show(ItemStack stack, ButtonAction action)
        {
            ItemData item = stack.Data;
            _iconArea.SetActive(true);
            _buttonArea.SetActive(true);

            _icon.sprite = item.icon;
            _icon.color = item.icon != null ? Color.white : Color.clear;
            _icon.preserveAspect = true;

            PlayText(_name, item.itemName, 0);
            PlayText(_description, item.description, 1);
            _count.SetText("x{0}", stack.Count);

            RefreshParameters(stack);
            SetAction(action, item);
        }

        /// <summary>何も選んでいないときの案内を出す。</summary>
        public void ShowEmpty(bool replayText)
        {
            _iconArea.SetActive(false);
            _buttonArea.SetActive(false);
            _icon.sprite = null;
            _icon.color = Color.clear;

            PlayText(_name, "アイテムを選択", 0, replayText);
            PlayText(_description, "一覧からアイテムを選んでください。", 1, replayText);
            _count.text = string.Empty;

            RefreshParameters(null);
            SetAction(ButtonAction.None, null);
        }

        /// <summary>文字送りの途中なら、最後まで出しきる。</summary>
        public void CompleteText() => Typewriter.CompleteAll();

        private void RefreshParameters(ItemStack stack)
        {
            ItemData item = stack?.Data;
            bool showsQuick = item is FoodData;
            bool showsEquipment = item != null && item.category == ItemCategory.Equipment;
            bool showsParameters = showsQuick || showsEquipment;
            MeasureHeights();
            _parameterRow.gameObject.SetActive(showsParameters);
            _areaLayout.preferredHeight = showsParameters
                ? _expandedHeight
                : Mathf.Max(0, _expandedHeight - _parameterHeight);
            _quickParameterPattern.SetActive(showsQuick);
            _equipmentParameterPattern.SetActive(showsEquipment);

            if (item is FoodData food)
            {
                _quickParameterLabel.text = "回復量";
                SetParameterValue(_quickParameterValue, food.HealFraction * 100f);
            }
            else if (showsEquipment)
            {
                var bonus = new EquipmentBonus();
                if (stack.IsInstance)
                    bonus.Add(stack.RolledStats);

                SetParameterValue(_attackValue, bonus.attackPct);
                SetParameterValue(_defenseValue, bonus.defensePct);
                SetParameterValue(_maxHpValue, bonus.maxHpPct);
                SetParameterValue(_criticalRateValue, bonus.criticalChance);
                SetParameterValue(_criticalDamageValue, bonus.criticalDamage);
            }
        }

        private static void SetParameterValue(TMP_Text text, float value)
        {
            string sign = value > 0f ? "+" : string.Empty;
            text.text = $"{sign}{value.ToString("0.##", CultureInfo.InvariantCulture)}%";
        }

        private void PlayText(TMP_Text text, string value, int sectionIndex, bool replay = false)
        {
            string content = value ?? string.Empty;
            if (!replay && text.text == content)
                return;

            Typewriter.Play(
                text,
                content,
                _textStartDelay + _sectionDelay * sectionIndex,
                _characterInterval,
                _animateText
            );
        }

        private void SetAction(ButtonAction action, ItemData item)
        {
            CurrentAction = action;
            _actionButton.interactable = action != ButtonAction.None;
            _actionButton.gameObject.SetActive(action != ButtonAction.None);
            _actionLabel.text = action switch
            {
                ButtonAction.Assign when item is FoodData => "登録",
                ButtonAction.Assign when item != null && item.category == ItemCategory.Equipment =>
                    "装備",
                ButtonAction.RemoveQuick => "解除",
                ButtonAction.RemoveEquipment => "解除",
                _ => string.Empty,
            };
        }

        // 縮める前の高さを覚えておく(最初の1回だけ)
        private void MeasureHeights()
        {
            if (_heightsMeasured)
                return;
            _heightsMeasured = true;
            _expandedHeight = _areaLayout.preferredHeight;
            _parameterHeight = Mathf.Max(0, _parameterRow.preferredHeight);
        }
    }
}
