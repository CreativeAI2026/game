using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 装備によるバフのアイコンを、HP バーの左端を始点に右へ並べる。幅に収まらない分は次の段へ折り返す。
    /// HP バーの上に並べるか下に並べるかを Inspector で切り替えられる(段は HP バーから離れる向きに増える)。
    /// 装備中は永続なので、PlayerStatus の OnStatsChanged(装備変更)でだけ並べ直す。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class EquipmentBuffIconBarView : MonoBehaviour
    {
        /// <summary>HP バーに対してアイコンを並べる位置。</summary>
        public enum Placement
        {
            [InspectorName("HPバーの上")]
            AboveHpBar,

            [InspectorName("HPバーの下")]
            BelowHpBar,
        }

        [Tooltip("バフの効果とアイコンの組")]
        [SerializeField]
        private EquipmentBuffIconSet _iconSet;

        [Tooltip("並べる基準にする HP バーの見た目の矩形。このオブジェクトと同じ親に置くこと")]
        [SerializeField]
        private RectTransform _hpBarRect;

        [Header("並べ方(編集中に変えてもすぐ反映)")]
        [SerializeField]
        private Placement _placement = Placement.AboveHpBar;

        [Tooltip("アイコン1つの大きさ(px)")]
        [SerializeField]
        private float _iconSize = 40f;

        [Tooltip("アイコン同士の間隔(px)")]
        [SerializeField]
        private float _iconSpacing = 4f;

        [Tooltip("HP バーとアイコンの間隔(px)")]
        [SerializeField]
        private float _gapFromHpBar = 4f;

        private readonly List<Image> _iconPool = new();
        private readonly List<Sprite> _visibleIcons = new();
        private readonly HashSet<StatType> _warnedMissingEffects = new();

        private RectTransform _rectTransform;
        private PlayerStatus _playerStatus;

        /// <summary>確認用に、付いているかに関係なく全アイコンを並べているか。</summary>
        public bool IsPreviewing { get; private set; }

        private RectTransform RectTransform =>
            _rectTransform != null ? _rectTransform : _rectTransform = (RectTransform)transform;

        private void Awake()
        {
            Assert.IsNotNull(_iconSet, $"{name}: バフアイコンの組(_iconSet)が未設定です");
            Assert.IsNotNull(_hpBarRect, $"{name}: 基準の HP バー(_hpBarRect)が未設定です");
            foreach (var entry in _iconSet.Entries)
            {
                if (entry.icon == null)
                    Debug.LogError($"{name}: バフ {entry.effect} のアイコンが未設定です", _iconSet);
            }
        }

        private void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
        }

        private void OnDisable()
        {
            PlayerRegistry.Changed -= Bind;
            Bind(null);
        }

        /// <summary>
        /// 確認用: 付いているかに関係なく、組に登録した全アイコンを並べる。false で実際のバフ表示に戻す。
        /// 編集中(プレイしていない時)でも使え、そのとき作るアイコンはシーン・プレハブに保存されない。
        /// </summary>
        public void SetPreview(bool preview)
        {
            IsPreviewing = preview;
            Refresh();
        }

        /// <summary>購読先のプレイヤーを切り替える(未生成・破棄時は null)。</summary>
        private void Bind(PlayerController player)
        {
            if (_playerStatus != null)
                _playerStatus.OnStatsChanged -= Refresh;

            _playerStatus = player != null ? player.GetComponent<PlayerStatus>() : null;

            if (_playerStatus != null)
                _playerStatus.OnStatsChanged += Refresh;

            Refresh();
        }

        private void Refresh()
        {
            CollectVisibleIcons();
            Layout();
        }

        private void CollectVisibleIcons()
        {
            _visibleIcons.Clear();
            if (_iconSet == null)
                return;

            if (IsPreviewing)
            {
                foreach (var entry in _iconSet.Entries)
                    _visibleIcons.Add(entry.icon);
                return;
            }

            if (_playerStatus == null)
                return;

            EquipmentBonus bonus = _playerStatus.EquipmentBonus;
            foreach (var entry in _iconSet.Entries)
            {
                if (bonus.Get(entry.effect) > 0f)
                    _visibleIcons.Add(entry.icon);
            }
            WarnEffectsWithoutIcon(bonus);
        }

        /// <summary>付いているのに組にアイコンが無い効果を、種類ごとに1回だけ知らせる(表示漏れに気付けるように)。</summary>
        private void WarnEffectsWithoutIcon(EquipmentBonus bonus)
        {
            foreach (StatType effect in Enum.GetValues(typeof(StatType)))
            {
                if (bonus.Get(effect) <= 0f || _warnedMissingEffects.Contains(effect))
                    continue;
                bool registered = false;
                foreach (var entry in _iconSet.Entries)
                    registered |= entry.effect == effect;
                if (registered)
                    continue;

                _warnedMissingEffects.Add(effect);
                Debug.LogWarning(
                    $"{name}: 装備のバフ {effect} が付いていますが、アイコンが登録されていないため表示しません",
                    _iconSet
                );
            }
        }

        /// <summary>自分の矩形を HP バーの上(下)に合わせ、見えるアイコンを左から並べる。</summary>
        private void Layout()
        {
            if (_hpBarRect == null)
                return;

            FitToHpBar();

            float pitch = _iconSize + _iconSpacing;
            float width = RectTransform.rect.width;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((width + _iconSpacing) / pitch));
            bool above = _placement == Placement.AboveHpBar;
            // 上に並べるときは下端、下に並べるときは上端を HP バー側の基準にする
            var corner = new Vector2(0f, above ? 0f : 1f);

            // 編集中の確認で作ったものがプレイ開始等で消えている場合に備え、壊れた参照は捨てる
            _iconPool.RemoveAll(image => image == null);
            // スクリプト再読み込みで一覧だけ消え、確認用に作ったアイコンが残っている場合は拾い直す
            if (_iconPool.Count == 0)
            {
                foreach (Transform child in transform)
                {
                    if (child.TryGetComponent(out Image image))
                        _iconPool.Add(image);
                }
            }

            for (int i = 0; i < _visibleIcons.Count; i++)
            {
                Image icon = GetOrCreateIcon(i);
                icon.sprite = _visibleIcons[i];
                icon.enabled = icon.sprite != null;

                int column = i % perRow;
                int row = i / perRow;
                // 段は HP バーから離れる向きへ増やす
                var slotCenter = new Vector2(
                    column * pitch + _iconSize * 0.5f,
                    (row * pitch + _iconSize * 0.5f) * (above ? 1f : -1f)
                );

                var rt = icon.rectTransform;
                rt.anchorMin = corner;
                rt.anchorMax = corner;
                // スプライトの Pivot をマスの中心に合わせる(絵が画像の中央に無い画像は、Pivot を絵の中心にすればそろう)
                rt.pivot = icon.sprite != null ? icon.sprite.pivot / icon.sprite.rect.size : new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = slotCenter;
                rt.sizeDelta = Vector2.one * _iconSize;
                icon.gameObject.SetActive(true);
            }

            for (int i = _visibleIcons.Count; i < _iconPool.Count; i++)
                _iconPool[i].gameObject.SetActive(false);
        }

        /// <summary>横幅を HP バーと揃え、縦は HP バーの上端(下端)から間隔を空けた1段分にする。</summary>
        private void FitToHpBar()
        {
            Assert.AreEqual(
                _hpBarRect.parent,
                RectTransform.parent,
                $"{name}: 基準の HP バーと同じ親に置いてください"
            );

            var rt = RectTransform;
            bool above = _placement == Placement.AboveHpBar;
            float barEdgeAnchorY = above ? _hpBarRect.anchorMax.y : _hpBarRect.anchorMin.y;
            float barEdgeOffsetY = above ? _hpBarRect.offsetMax.y : _hpBarRect.offsetMin.y;

            rt.anchorMin = new Vector2(_hpBarRect.anchorMin.x, barEdgeAnchorY);
            rt.anchorMax = new Vector2(_hpBarRect.anchorMax.x, barEdgeAnchorY);
            rt.pivot = new Vector2(0f, above ? 0f : 1f);

            float nearEdge = barEdgeOffsetY + (above ? _gapFromHpBar : -_gapFromHpBar);
            float farEdge = nearEdge + (above ? _iconSize : -_iconSize);
            rt.offsetMin = new Vector2(_hpBarRect.offsetMin.x, Mathf.Min(nearEdge, farEdge));
            rt.offsetMax = new Vector2(_hpBarRect.offsetMax.x, Mathf.Max(nearEdge, farEdge));
        }

        private Image GetOrCreateIcon(int index)
        {
            while (_iconPool.Count <= index)
            {
                var go = new GameObject("BuffIcon", typeof(RectTransform), typeof(Image));
                if (!Application.isPlaying)
                    go.hideFlags = HideFlags.DontSave; // 確認用。シーン・プレハブに保存しない
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                _iconPool.Add(image);
            }
            return _iconPool[index];
        }

        private void OnValidate()
        {
            // 編集中・プレイ中とも、並べ方を変えたらその場で並べ直す(OnValidate 中は RectTransform を触れないため1拍遅らせる)
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                // プレハブアセット本体(シーンに無いもの)は並べ直さない
                if (this != null && gameObject.scene.IsValid())
                    Layout();
            };
#endif
        }
    }
}
