using System;
using CreativeAI.Gameplay;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 調合中のローディング(回る歯車)と、できたアイテムを見せる結果パネルの表示・出入りアニメーションを受け持つ。
    /// </summary>
    internal sealed class CraftResultPopup
    {
        private const float GearRotationSpeed = 180f;
        private const float FadeDuration = 0.16f;
        private const float AnimationDuration = 0.22f;
        private const float HiddenScale = 0.9f;

        private readonly GameObject _loadingRoot;
        private readonly RectTransform _loadingGear;
        private readonly CanvasGroup _canvasGroup;
        private readonly CloseOnSelfClick _closeOnClick;
        private readonly Image _itemImage;
        private readonly TMP_Text _itemName;
        private readonly TMP_Text _itemParameters;
        private readonly Action _closeRequested;
        private Action _closedAction;
        private Sequence _sequence;

        /// <param name="closeRequested">結果パネルをクリックしたときに呼ぶ処理(閉じるボタンと同じもの)</param>
        public CraftResultPopup(
            GameObject loadingRoot,
            RectTransform loadingGear,
            CanvasGroup canvasGroup,
            CloseOnSelfClick closeOnClick,
            Image itemImage,
            TMP_Text itemName,
            TMP_Text itemParameters,
            Action closeRequested
        )
        {
            _loadingRoot = loadingRoot;
            _loadingGear = loadingGear;
            _canvasGroup = canvasGroup;
            _closeOnClick = closeOnClick;
            _itemImage = itemImage;
            _itemName = itemName;
            _itemParameters = itemParameters;
            _closeRequested = closeRequested;
        }

        /// <summary>結果パネルが出きっていて、閉じる入力を受け付ける状態か。</summary>
        public bool IsWaitingForClose =>
            _canvasGroup != null && Root.activeSelf && _canvasGroup.interactable;

        private GameObject Root => _canvasGroup.gameObject;

        /// <summary>毎フレーム呼ぶ。ローディング中なら歯車を回す。</summary>
        public void Tick()
        {
            if (_loadingGear != null && _loadingGear.gameObject.activeInHierarchy)
                _loadingGear.Rotate(0f, 0f, -GearRotationSpeed * Time.unscaledDeltaTime);
        }

        public void ShowLoading()
        {
            _loadingRoot.SetActive(true);
            _loadingGear.localRotation = Quaternion.identity;
            _loadingGear.gameObject.SetActive(true);
        }

        public void HideLoading()
        {
            if (_loadingGear != null)
            {
                _loadingGear.localRotation = Quaternion.identity;
                _loadingGear.gameObject.SetActive(false);
            }
            if (_loadingRoot != null)
                _loadingRoot.SetActive(false);
        }

        /// <summary>結果パネルを出す。閉じ終わったら closeAction を呼ぶ。</summary>
        public void Show(ItemStack crafted, Action closeAction)
        {
            _closedAction = closeAction;
            var item = crafted?.Data;
            SetContent(
                item != null ? item.icon : null,
                item != null ? item.itemName : string.Empty,
                ItemStatTextFormatter.BuildStatsText(crafted)
            );
            if (_closeOnClick != null)
                _closeOnClick.SetClickAction(_closeRequested);
            PlayIn();
        }

        /// <summary>アニメーションしながら閉じ、閉じ終わったら Show で渡された処理を呼ぶ。</summary>
        public void Close()
        {
            if (_canvasGroup == null)
                return;

            Action closedAction = _closedAction;
            _closedAction = null;
            if (_closeOnClick != null)
                _closeOnClick.ClearClickAction(_closeRequested);
            PlayOut(() =>
            {
                SetContent(null, string.Empty, string.Empty);
                closedAction?.Invoke();
            });
        }

        /// <summary>アニメーションせずに隠す。Show で渡された処理は呼ばない。</summary>
        public void HideImmediately()
        {
            if (_canvasGroup == null)
                return;

            _closedAction = null;
            if (_closeOnClick != null)
                _closeOnClick.ClearClickAction(_closeRequested);
            ResetPanel();
            SetContent(null, string.Empty, string.Empty);
        }

        /// <summary>破棄のときに呼ぶ。動いているアニメーションと登録を外す。</summary>
        public void Dispose()
        {
            _sequence?.Kill();
            _sequence = null;
            if (_closeOnClick != null)
                _closeOnClick.ClearClickAction(_closeRequested);
            _closedAction = null;
        }

        private void SetContent(Sprite icon, string itemName, string itemParameters)
        {
            if (_itemImage != null)
            {
                _itemImage.sprite = icon;
                _itemImage.color = icon != null ? Color.white : Color.clear;
                _itemImage.gameObject.SetActive(icon != null);
            }

            if (_itemName != null)
                _itemName.text = itemName ?? string.Empty;

            if (_itemParameters != null)
            {
                _itemParameters.text = itemParameters ?? string.Empty;
                _itemParameters.gameObject.SetActive(!string.IsNullOrWhiteSpace(itemParameters));
            }
        }

        private void PlayIn()
        {
            var rect = Root.transform as RectTransform;
            KillTweens(rect);

            if (rect != null)
                rect.localScale = Vector3.one * HiddenScale;
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = true;

            Root.SetActive(true);

            var sequence = _sequence = DOTween.Sequence().SetTarget(Root).SetUpdate(true);
            if (rect != null)
                sequence.Join(rect.DOScale(Vector3.one, AnimationDuration).SetEase(Ease.OutBack));
            sequence.Join(_canvasGroup.DOFade(1f, FadeDuration));
            sequence.OnComplete(() =>
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            });
        }

        private void PlayOut(Action onComplete)
        {
            if (!Root.activeSelf)
            {
                onComplete?.Invoke();
                return;
            }

            var rect = Root.transform as RectTransform;
            KillTweens(rect);
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            var sequence = _sequence = DOTween.Sequence().SetTarget(Root).SetUpdate(true);
            if (rect != null)
            {
                sequence.Join(
                    rect.DOScale(Vector3.one * HiddenScale, AnimationDuration).SetEase(Ease.InBack)
                );
            }
            sequence.Join(_canvasGroup.DOFade(0f, FadeDuration));
            sequence.OnComplete(() =>
            {
                ResetPanel();
                onComplete?.Invoke();
            });
        }

        // アニメーションを止め、結果パネルを非表示の初期状態に戻す。
        private void ResetPanel()
        {
            var rect = Root.transform as RectTransform;
            KillTweens(rect);
            if (rect != null)
                rect.localScale = Vector3.one;
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            Root.SetActive(false);
        }

        private void KillTweens(RectTransform rect)
        {
            _sequence?.Kill();
            _sequence = null;
            DOTween.Kill(Root);
            if (rect != null)
                rect.DOKill();
            _canvasGroup.DOKill();
        }
    }
}
