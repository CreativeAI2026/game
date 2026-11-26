using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 持ち物ピッカーの1マス(と「やめる」ボタン)。マウスが乗ったことを知らせる。
    /// EventTrigger はスクロールやドラッグまで受け取って親の ScrollRect に届かなくしてしまうため、乗ったときだけを受ける。
    /// </summary>
    public sealed class DialogueItemPickerCell : MonoBehaviour, IPointerEnterHandler
    {
        private static readonly Color NormalColor = new(1f, 1f, 1f, 0.1f);
        private static readonly Color FocusColor = new(0.55f, 0.75f, 1f, 0.4f);

        [SerializeField]
        private Button _button;

        [SerializeField]
        private Image _background;

        [Header("持ち物のマスだけ")]
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _nameFallback; // 絵の無いアイテムは、マスの中に名前を出して見分けられるようにする

        [SerializeField]
        private TMP_Text _count;

        private Action _onHover;

        /// <summary>持ち物を出す。</summary>
        internal void Set(ItemPickerEntries.Entry entry)
        {
            bool hasIcon = entry.Icon != null;
            _icon.gameObject.SetActive(hasIcon);
            _icon.sprite = entry.Icon;
            _nameFallback.gameObject.SetActive(!hasIcon);
            _nameFallback.text = entry.Name;
            _count.gameObject.SetActive(entry.Count > 1);
            _count.SetText("×{0}", entry.Count);
        }

        /// <summary>押したとき・マウスが乗ったときの処理を設定する。</summary>
        public void Bind(Action onClick, Action onHover)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick());
            _onHover = onHover;
        }

        /// <summary>選択中の色にする・戻す。</summary>
        public void SetFocused(bool focused) =>
            _background.color = focused ? FocusColor : NormalColor;

        public void OnPointerEnter(PointerEventData eventData) => _onHover?.Invoke();
    }
}
