using System.Collections.Generic;
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
    /// </summary>
    internal sealed class LoadoutDetailPanel
    {
        /// <summary>詳細欄のボタンで行うこと。</summary>
        public enum ButtonAction
        {
            None,
            Assign,
            RemoveQuick,
            RemoveEquipment,
        }

        /// <summary>名前と説明を1文字ずつ出すときの間合い。</summary>
        public readonly struct TextTiming
        {
            public readonly bool Animate;
            public readonly float StartDelay;
            public readonly float CharacterInterval;
            public readonly float SectionDelay; // 名前の次に説明を出すまでの遅れ

            public TextTiming(
                bool animate,
                float startDelay,
                float characterInterval,
                float sectionDelay
            )
            {
                Animate = animate;
                StartDelay = startDelay;
                CharacterInterval = characterInterval;
                SectionDelay = sectionDelay;
            }
        }

        private readonly Transform _root;
        private readonly TextTiming _timing;
        private readonly TmpTypewriterPlayer _typewriter;
        private Image _icon;
        private TMP_Text _name;
        private TMP_Text _description;
        private TMP_Text _count;
        private Button _actionButton;
        private TMP_Text _actionLabel;
        private GameObject _quickParameterPattern;
        private GameObject _equipmentParameterPattern;
        private TMP_Text _quickParameterLabel;
        private TMP_Text _quickParameterValue;
        private TMP_Text _attackValue;
        private TMP_Text _defenseValue;
        private TMP_Text _maxHpValue;
        private TMP_Text _criticalRateValue;
        private TMP_Text _criticalDamageValue;
        private LayoutElement _areaLayout;
        private GameObject _parameterRow;
        private GameObject _iconArea;
        private GameObject _buttonArea;
        private float _expandedHeight;
        private float _parameterHeight;

        /// <param name="host">文字送りのコルーチンを動かす MonoBehaviour(画面のルート)</param>
        public LoadoutDetailPanel(MonoBehaviour host, TextTiming timing)
        {
            _root = host.transform;
            _timing = timing;
            _typewriter = new TmpTypewriterPlayer(host);
            Resolve();
        }

        /// <summary>今ボタンで行うこと。</summary>
        public ButtonAction CurrentAction { get; private set; }

        /// <summary>部品を画面の決まった場所から探す。見つかっていないものだけ探し直す。</summary>
        public void Resolve()
        {
            if (_areaLayout == null)
            {
                var detail = _root.Find("InventoryArea/DetailArea");
                _areaLayout = detail != null ? detail.GetComponent<LayoutElement>() : null;
                _expandedHeight = _areaLayout != null ? _areaLayout.preferredHeight : 0;
                _parameterRow = FindGameObject(detail, "ParameterRow");
                var parameterLayout =
                    _parameterRow != null ? _parameterRow.GetComponent<LayoutElement>() : null;
                _parameterHeight =
                    parameterLayout != null ? Mathf.Max(0, parameterLayout.preferredHeight) : 0;
                _iconArea = FindGameObject(detail, "ItemRow/IconArea");
                _buttonArea = FindGameObject(detail, "ItemRow/ButtonArea");
            }
            const string itemRow = "InventoryArea/DetailArea/ItemRow";
            if (_icon == null)
                _icon = FindComponent<Image>(_root, itemRow + "/IconArea/Background/Icon");
            if (_name == null)
                _name = FindComponent<TMP_Text>(_root, itemRow + "/DescriptionArea/Title");
            if (_description == null)
                _description = FindComponent<TMP_Text>(
                    _root,
                    itemRow + "/DescriptionArea/Description"
                );
            if (_count == null)
                _count = FindComponent<TMP_Text>(_root, itemRow + "/ButtonArea/Quantity");
            if (_actionButton == null)
                _actionButton = FindComponent<Button>(_root, itemRow + "/ButtonArea/Button");
            if (_actionLabel == null)
                _actionLabel =
                    _actionButton != null
                        ? _actionButton.GetComponentInChildren<TMP_Text>(true)
                        : null;

            Transform parameterRoot = _root.Find("InventoryArea/DetailArea/ParameterRow");
            if (_quickParameterPattern == null)
                _quickParameterPattern = FindGameObject(parameterRoot, "QuickItemPattern");
            if (_equipmentParameterPattern == null)
                _equipmentParameterPattern = FindGameObject(parameterRoot, "EquipPattern");

            Transform quickParameter =
                parameterRoot != null
                    ? parameterRoot.Find("QuickItemPattern/ItemParameterArea")
                    : null;
            if (_quickParameterLabel == null)
                _quickParameterLabel = FindComponent<TMP_Text>(quickParameter, "Label");
            if (_quickParameterValue == null)
                _quickParameterValue = FindComponent<TMP_Text>(quickParameter, "Value");

            if (_attackValue == null)
                _attackValue = FindParameterValue(parameterRoot, "EquipPattern/UpperArea/ATK");
            if (_defenseValue == null)
                _defenseValue = FindParameterValue(parameterRoot, "EquipPattern/UpperArea/DEF");
            if (_maxHpValue == null)
                _maxHpValue = FindParameterValue(parameterRoot, "EquipPattern/UpperArea/MaxHP");
            if (_criticalRateValue == null)
                _criticalRateValue = FindParameterValue(
                    parameterRoot,
                    "EquipPattern/LowerArea/CRTR"
                );
            if (_criticalDamageValue == null)
                _criticalDamageValue = FindParameterValue(
                    parameterRoot,
                    "EquipPattern/LowerArea/CRTD"
                );
        }

        /// <summary>見つからなかった部品の名前を足す(警告用)。</summary>
        public void CollectMissing(List<string> missing)
        {
            if (_icon == null)
                missing.Add("詳細アイコン");
            if (_name == null)
                missing.Add("詳細アイテム名");
            if (_description == null)
                missing.Add("詳細説明文");
            if (_count == null)
                missing.Add("詳細所持数");
            if (_actionButton == null || _actionLabel == null)
                missing.Add("SETボタンとラベル");
        }

        public void AddActionListener(UnityAction action)
        {
            if (_actionButton != null)
                _actionButton.onClick.AddListener(action);
        }

        public void RemoveActionListener(UnityAction action)
        {
            if (_actionButton != null)
                _actionButton.onClick.RemoveListener(action);
        }

        /// <summary>アイテムの詳細を出す。stack は中身があるものを渡す。</summary>
        public void Show(ItemStack stack, ButtonAction action)
        {
            ItemData item = stack.Data;
            if (_iconArea != null)
                _iconArea.SetActive(true);
            if (_buttonArea != null)
                _buttonArea.SetActive(true);

            if (_icon != null)
            {
                _icon.sprite = item.icon;
                _icon.color = item.icon != null ? Color.white : Color.clear;
                _icon.preserveAspect = true;
            }

            PlayText(_name, item.itemName, 0);
            PlayText(_description, item.description, 1);
            if (_count != null)
                _count.SetText("x{0}", stack.Count);

            RefreshParameters(stack);
            SetAction(action, item);
        }

        /// <summary>何も選んでいないときの案内を出す。</summary>
        public void ShowEmpty(bool replayText)
        {
            if (_iconArea != null)
                _iconArea.SetActive(false);
            if (_buttonArea != null)
                _buttonArea.SetActive(false);
            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.color = Color.clear;
            }

            PlayText(_name, "アイテムを選択", 0, replayText);
            PlayText(_description, "一覧からアイテムを選んでください。", 1, replayText);
            if (_count != null)
                _count.text = string.Empty;

            RefreshParameters(null);
            SetAction(ButtonAction.None, null);
        }

        /// <summary>文字送りの途中なら、最後まで出しきる。</summary>
        public void CompleteText() => _typewriter.CompleteAll();

        private void RefreshParameters(ItemStack stack)
        {
            ItemData item = stack?.Data;
            bool showsQuick = item is FoodData;
            bool showsEquipment = item != null && item.category == ItemCategory.Equipment;
            bool showsParameters = showsQuick || showsEquipment;
            if (_parameterRow != null)
                _parameterRow.SetActive(showsParameters);
            if (_areaLayout != null)
                _areaLayout.preferredHeight = showsParameters
                    ? _expandedHeight
                    : Mathf.Max(0, _expandedHeight - _parameterHeight);
            if (_quickParameterPattern != null)
                _quickParameterPattern.SetActive(showsQuick);
            if (_equipmentParameterPattern != null)
                _equipmentParameterPattern.SetActive(showsEquipment);

            if (item is FoodData food)
            {
                if (_quickParameterLabel != null)
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
            if (text == null)
                return;

            string sign = value > 0f ? "+" : string.Empty;
            text.text = $"{sign}{value.ToString("0.##", CultureInfo.InvariantCulture)}%";
        }

        private void PlayText(TMP_Text text, string value, int sectionIndex, bool replay = false)
        {
            if (text == null)
                return;

            string content = value ?? string.Empty;
            if (!replay && text.text == content)
                return;

            _typewriter.Play(
                text,
                content,
                _timing.StartDelay + _timing.SectionDelay * sectionIndex,
                _timing.CharacterInterval,
                _timing.Animate
            );
        }

        private void SetAction(ButtonAction action, ItemData item)
        {
            CurrentAction = action;
            if (_actionButton != null)
            {
                _actionButton.interactable = action != ButtonAction.None;
                _actionButton.gameObject.SetActive(action != ButtonAction.None);
            }
            if (_actionLabel != null)
                _actionLabel.text = action switch
                {
                    ButtonAction.Assign when item is FoodData => "登録",
                    ButtonAction.Assign
                        when item != null && item.category == ItemCategory.Equipment => "装備",
                    ButtonAction.RemoveQuick => "解除",
                    ButtonAction.RemoveEquipment => "解除",
                    _ => string.Empty,
                };
        }

        private static TMP_Text FindParameterValue(Transform root, string path)
        {
            Transform t = root != null ? root.Find(path) : null;
            return FindComponent<TMP_Text>(t, "Value");
        }

        private static T FindComponent<T>(Transform root, string path)
            where T : Component
        {
            Transform t = root != null ? root.Find(path) : null;
            return t != null ? t.GetComponent<T>() : null;
        }

        private static GameObject FindGameObject(Transform root, string path)
        {
            Transform t = root != null ? root.Find(path) : null;
            return t != null ? t.gameObject : null;
        }
    }
}
