using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 会話ログの1行。種類(セリフ・選択肢・地の文・入手)ごとに Prefab があり、その種類で使う欄だけを設定してある。
    /// </summary>
    public sealed class DialogueHistoryRow : MonoBehaviour
    {
        private const float PortraitZoom = 1.25f;
        private static readonly Color FrameColor = new(0.055f, 0.075f, 0.1f, 0.98f);
        private static readonly Color ObscuredFrameColor = new(0.018f, 0.025f, 0.04f, 1f);
        private static readonly Color OutlineColor = new(0.3f, 0.55f, 0.75f, 0.42f);
        private static readonly Color ObscuredOutlineColor = new(0.16f, 0.24f, 0.34f, 0.42f);
        private static readonly Color ObscuredPortraitColor = new(0.045f, 0.06f, 0.085f, 1f);
        private static readonly Color ItemBackgroundColor = new(0.045f, 0.12f, 0.15f, 0.88f);
        private static readonly Color WeaponBackgroundColor = new(0.14f, 0.105f, 0.045f, 0.88f);
        private static readonly Color ItemBadgeColor = new(0.52f, 0.9f, 1f, 0.96f);
        private static readonly Color WeaponBadgeColor = new(1f, 0.78f, 0.38f, 0.96f);

        [SerializeField]
        private TMP_Text _recordNumber;

        [SerializeField]
        private TMP_Text _body;

        [SerializeField]
        private GameObject _currentMarker; // 最新の行の印

        [Header("セリフ")]
        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private HorizontalLayoutGroup _rowLayout;

        [SerializeField]
        private Image _portraitFrame;

        [SerializeField]
        private Outline _portraitOutline;

        [SerializeField]
        private Image _portrait;

        [Header("入手")]
        [SerializeField]
        private Image _background;

        [SerializeField]
        private TMP_Text _badge;

        [Header("選択肢")]
        [SerializeField]
        private DialogueChoiceHistoryToggle _choiceToggle;

        /// <summary>この行に記録を出す。</summary>
        internal void Set(DialogueHistoryEntry entry)
        {
            _recordNumber.text = $"{Mathf.Max(1, entry.Sequence):000}";
            switch (entry.Kind)
            {
                case DialogueHistoryEntryKind.Dialogue:
                    SetDialogue(entry);
                    break;
                case DialogueHistoryEntryKind.Choice:
                    _choiceToggle.SetText(entry.Body);
                    break;
                case DialogueHistoryEntryKind.RewardItem:
                case DialogueHistoryEntryKind.RewardWeapon:
                    bool weapon = entry.Kind == DialogueHistoryEntryKind.RewardWeapon;
                    _background.color = weapon ? WeaponBackgroundColor : ItemBackgroundColor;
                    _badge.text = weapon ? "WEAPON" : "ITEM";
                    _badge.color = weapon ? WeaponBadgeColor : ItemBadgeColor;
                    _body.text = entry.Body;
                    break;
                default:
                    _body.text = entry.Body;
                    break;
            }
        }

        /// <summary>検索語に当たる部分を目立たせる(空なら元に戻す)。</summary>
        internal void ApplySearch(DialogueHistoryEntry entry, string query)
        {
            if (_choiceToggle != null)
            {
                _choiceToggle.SetSearchText(
                    string.IsNullOrEmpty(query)
                        ? null
                        : DialogueHistorySearch.Highlight(entry.Body, query)
                );
                return;
            }
            if (_name != null)
                _name.text = DialogueHistorySearch.Highlight(entry.Speaker, query);
            _body.text = DialogueHistorySearch.Highlight(entry.Body, query);
        }

        /// <summary>最新の行の印を出す・消す。</summary>
        public void SetCurrent(bool current) => _currentMarker.SetActive(current);

        private void SetDialogue(DialogueHistoryEntry entry)
        {
            _name.text = entry.Speaker;
            _body.text = entry.Body;

            bool obscured = entry.PortraitObscured;
            _portraitFrame.color = obscured ? ObscuredFrameColor : FrameColor;
            _portraitOutline.effectColor = obscured ? ObscuredOutlineColor : OutlineColor;
            _portrait.gameObject.SetActive(entry.Portrait != null);
            _portrait.sprite = entry.Portrait;
            _portrait.color = obscured ? ObscuredPortraitColor : Color.white;

            bool right = entry.Side == DialoguePortraitSide.Right;
            _portrait.rectTransform.localScale = new Vector3(
                (right ? -1f : 1f) * PortraitZoom,
                PortraitZoom,
                1f
            );
            // 右に立つ人は、アイコンを行の右端に置く
            if (right)
            {
                _portraitFrame.transform.SetAsLastSibling();
                _rowLayout.childAlignment = TextAnchor.UpperRight;
            }
        }
    }
}
