using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public class RecipeCraftPanel : MonoBehaviour
    {
        [Header("Recipes")]
        [SerializeField]
        private CraftRecipeDB _recipeDB;

        [SerializeField]
        private List<CraftRecipeData> _recipes = new();

        [SerializeField]
        private GameObject _recipeSlotPrefab;

        [SerializeField]
        private GameObject _materialRowPrefab;

        [Header("Craft Flow")]
        [SerializeField]
        private float _testCraftDuration = 5f;

        [SerializeField]
        private float _gearRotationSpeed = 180f;

        private Transform _recipeList;
        private Transform _recipeContent;
        private ItemDetailPanel _detailPanel;
        private Transform _materialList;
        private GameObject _quantityDialogPanel;
        private GameObject _quantityDialog;
        private RectTransform _quantityDialogRect;
        private CanvasGroup _quantityDialogCanvasGroup;

        [Header("Dialog Animation")]
        [SerializeField]
        private float _dialogAnimationDuration = 0.22f;

        [SerializeField]
        private float _dialogStartScale = 0.82f;

        [Header("Warning")]
        [SerializeField]
        private string _missingMaterialsMessage = "素材が足りません！";

        [SerializeField]
        private float _warningShakeDistance = 8f;

        [SerializeField]
        private float _warningFadeDelay = 0.8f;

        [SerializeField]
        private float _warningFadeDuration = 0.6f;

        private Image _dialogItemImage;
        private TMP_Text _dialogItemName;
        private TMP_Text _dialogCounts;
        private TMP_Text _warningText;
        private RectTransform _warningTextRect;
        private CanvasGroup _warningTextCanvasGroup;
        private Sequence _warningSequence;
        private TMP_InputField _quantityInput;
        private Button _minButton;
        private Button _minusButton;
        private Button _plusButton;
        private Button _maxButton;
        private Button _cancelButton;
        private Button _dialogCraftButton;
        private GameObject _loadingPanel;
        private RectTransform _loadingGear;
        private GameObject _resultPanel;
        private Image _resultItemImage;
        private TMP_Text _resultItemName;
        private GameObject _closeButton;
        private ResultPanelClickCatcher _resultClickCatcher;
        private ResultPanelClickCatcher _quantityDialogPanelClickCatcher;
        private readonly List<RecipeSlot> _slots = new();
        private CraftRecipeData _selectedRecipe;
        private int _quantity = 1;
        private bool _isCrafting;
        private Coroutine _craftRoutine;
        private Coroutine _initializeRoutine;

        private void Awake()
        {
            FindReferences();
            BindDialog();
            ResetView();
        }

        private void OnEnable()
        {
            if (_initializeRoutine != null)
                StopCoroutine(_initializeRoutine);

            _initializeRoutine = StartCoroutine(InitializeViewRoutine());
        }

        private void OnDisable()
        {
            if (_craftRoutine != null)
            {
                StopCoroutine(_craftRoutine);
                _craftRoutine = null;
            }

            if (_initializeRoutine != null)
            {
                StopCoroutine(_initializeRoutine);
                _initializeRoutine = null;
            }

            _isCrafting = false;
            HideWarningImmediately();
            if (_quantityDialogPanel != null)
                _quantityDialogPanel.SetActive(false);
            if (_quantityDialog != null)
                _quantityDialog.SetActive(false);
            if (_resultPanel != null)
                _resultPanel.SetActive(false);
            if (_loadingPanel != null)
                _loadingPanel.SetActive(false);
            SetCloseButtonVisible(true);
        }

        private void Update()
        {
            if (_isCrafting && _loadingGear != null)
                _loadingGear.Rotate(0f, 0f, -_gearRotationSpeed * Time.unscaledDeltaTime);
        }

        private void FindReferences()
        {
            if (_recipeList == null)
                _recipeList = Find("RecipeList");
            if (_recipeDB == null)
                _recipeDB = Resources.Load<CraftRecipeDB>("Crafting/CraftRecipeDB");
            if (_recipeContent == null)
                _recipeContent = FindRecipeContent();
            if (_detailPanel == null)
                _detailPanel = GetComponentInChildren<ItemDetailPanel>(true);
            if (_materialList == null)
                _materialList = Find("MaterialList");
            if (_warningText == null)
                _warningText = FindComponentIn<TMP_Text>(transform, "WarningText");
            if (_warningText != null)
            {
                _warningTextRect ??= _warningText.rectTransform;
                _warningTextCanvasGroup ??= _warningText.GetComponent<CanvasGroup>();
                if (_warningTextCanvasGroup == null)
                    _warningTextCanvasGroup = _warningText.gameObject.AddComponent<CanvasGroup>();
            }
            if (_quantityDialogPanel == null)
                _quantityDialogPanel = FindGameObjectIn(transform, "CQD-Panel");
            if (_quantityDialog == null)
            {
                var quantityDialogTransform =
                    _quantityDialogPanel != null
                        ? FindIn(_quantityDialogPanel.transform, "CraftQuantityDialog")
                        : Find("CraftQuantityDialog");
                _quantityDialog =
                    quantityDialogTransform != null ? quantityDialogTransform.gameObject : null;
            }
            if (_quantityDialog != null)
            {
                _quantityDialogRect ??= _quantityDialog.GetComponent<RectTransform>();
                _quantityDialogCanvasGroup ??= _quantityDialog.GetComponent<CanvasGroup>();
                if (_quantityDialogCanvasGroup == null)
                    _quantityDialogCanvasGroup = _quantityDialog.AddComponent<CanvasGroup>();

                var dialogImage = _quantityDialog.GetComponent<Image>();
                if (dialogImage == null)
                {
                    dialogImage = _quantityDialog.AddComponent<Image>();
                    dialogImage.color = Color.clear;
                }
                dialogImage.raycastTarget = true;

                if (_quantityDialog.GetComponent<DialogClickBlocker>() == null)
                    _quantityDialog.AddComponent<DialogClickBlocker>();
            }

            if (_quantityDialogPanel != null)
            {
                var panelImage = _quantityDialogPanel.GetComponent<Image>();
                if (panelImage == null)
                {
                    panelImage = _quantityDialogPanel.AddComponent<Image>();
                    panelImage.color = Color.clear;
                }
                panelImage.raycastTarget = true;

                _quantityDialogPanelClickCatcher ??=
                    _quantityDialogPanel.GetComponent<ResultPanelClickCatcher>();
                _quantityDialogPanelClickCatcher ??=
                    _quantityDialogPanel.AddComponent<ResultPanelClickCatcher>();
                _quantityDialogPanelClickCatcher.SetClickAction(CloseQuantityDialog);
            }

            if (_quantityDialog != null)
            {
                if (_dialogItemImage == null)
                    _dialogItemImage = FindComponentIn<Image>(
                        _quantityDialog.transform,
                        "ItemImage"
                    );
                if (_dialogItemName == null)
                    _dialogItemName = FindComponentIn<TMP_Text>(
                        _quantityDialog.transform,
                        "ItemName"
                    );
                if (_dialogCounts == null)
                    _dialogCounts = FindComponentIn<TMP_Text>(_quantityDialog.transform, "Counts");
                if (_dialogCounts == null)
                    _dialogCounts = FindComponentIn<TMP_Text>(
                        _quantityDialog.transform,
                        "CountLabel"
                    );
                if (_dialogCounts == null)
                    _dialogCounts = FindComponentIn<TMP_Text>(
                        _quantityDialog.transform,
                        "QuantityLabel"
                    );
                if (_quantityInput == null)
                    _quantityInput = FindComponentIn<TMP_InputField>(
                        _quantityDialog.transform,
                        "InputField"
                    );
                if (_minButton == null)
                    _minButton = FindButton(_quantityDialog.transform, "MIN");
                if (_minusButton == null)
                    _minusButton = FindButton(_quantityDialog.transform, "-");
                if (_plusButton == null)
                    _plusButton = FindButton(_quantityDialog.transform, "+");
                if (_maxButton == null)
                    _maxButton = FindButton(_quantityDialog.transform, "MAX");
                if (_cancelButton == null)
                    _cancelButton = FindButton(_quantityDialog.transform, "CancelButton");
                if (_dialogCraftButton == null)
                    _dialogCraftButton = FindButton(_quantityDialog.transform, "CraftButton");
            }

            var craftPanelRoot = transform.parent;
            if (_loadingPanel == null)
                _loadingPanel = FindGameObjectIn(craftPanelRoot, "LoadingPanel");
            if (_loadingGear == null)
                _loadingGear = FindIn(craftPanelRoot, "LoadingGear") as RectTransform;
            if (_resultPanel == null)
                _resultPanel = FindGameObjectIn(craftPanelRoot, "ResultPanel");
            if (_resultPanel != null)
            {
                if (_resultItemImage == null)
                    _resultItemImage = FindComponentIn<Image>(_resultPanel.transform, "ItemImage");
                if (_resultItemName == null)
                    _resultItemName = FindComponentIn<TMP_Text>(_resultPanel.transform, "ItemName");
            }
            if (_closeButton == null)
                _closeButton = FindGameObjectIn(craftPanelRoot, "CloseButton");

            if (_resultPanel != null)
            {
                _resultClickCatcher ??= _resultPanel.GetComponent<ResultPanelClickCatcher>();
                _resultClickCatcher ??= _resultPanel.AddComponent<ResultPanelClickCatcher>();
            }
        }

        private IEnumerator InitializeViewRoutine()
        {
            yield return null;

            FindReferences();
            BuildRecipeList();
            BindDialog();
            ResetView();

            yield return null;

            foreach (var slot in _slots)
                slot?.RefreshDisplay();

            SelectInitialRecipe();
            Canvas.ForceUpdateCanvases();

            if (_recipeContent is RectTransform contentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            if (_materialList is RectTransform materialRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(materialRect);

            _initializeRoutine = null;
        }

        private void BuildRecipeList()
        {
            if (_recipeContent == null)
                return;

            UnbindSlots();

            var recipes = GetVisibleRecipes().ToList();
            if (_recipeSlotPrefab != null)
            {
                for (int i = _recipeContent.childCount - 1; i >= 0; i--)
                    Destroy(_recipeContent.GetChild(i).gameObject);

                foreach (var recipe in recipes)
                {
                    var slotObject = Instantiate(_recipeSlotPrefab, _recipeContent, false);
                    var slot = slotObject.GetComponent<RecipeSlot>();
                    slot ??= slotObject.AddComponent<RecipeSlot>();
                    slot.SetRecipe(recipe);
                    BindSlot(slot);
                }
            }
            else
            {
                foreach (var slot in _recipeContent.GetComponentsInChildren<RecipeSlot>(true))
                {
                    bool isVisible =
                        slot != null
                        && slot.Recipe != null
                        && (
                            _recipeDB != null
                                ? _recipeDB.IsVisible(slot.Recipe)
                                : slot.Recipe.showInRecipeCraft && slot.Recipe.resultItem != null
                        );

                    if (slot != null)
                        slot.gameObject.SetActive(isVisible);

                    if (isVisible)
                        BindSlot(slot);
                }
            }

            if (_recipeContent is RectTransform contentRect)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }
        }

        private void BindSlot(RecipeSlot slot)
        {
            if (slot == null)
                return;

            slot.Clicked += OnRecipeClicked;
            slot.DoubleClicked += OnRecipeDoubleClicked;
            _slots.Add(slot);
        }

        private void UnbindSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clicked -= OnRecipeClicked;
                slot.DoubleClicked -= OnRecipeDoubleClicked;
            }

            _slots.Clear();
        }

        private IEnumerable<CraftRecipeData> GetVisibleRecipes()
        {
            if (_recipeDB != null)
                return _recipeDB.VisibleRecipes;

            return _recipes.Where(recipe =>
                recipe != null && recipe.showInRecipeCraft && recipe.resultItem != null
            );
        }

        private void OnRecipeClicked(RecipeSlot slot)
        {
            SelectRecipeSlot(slot);
        }

        private void OnRecipeDoubleClicked(RecipeSlot slot)
        {
            SelectRecipeSlot(slot);
            if (GetMaximumCraftable() <= 0)
            {
                CloseQuantityDialogImmediately();
                PlayMissingMaterialsWarning();
                return;
            }

            OpenQuantityDialog();
        }

        private void SelectRecipeSlot(RecipeSlot selectedSlot)
        {
            _selectedRecipe = selectedSlot?.Recipe;

            foreach (var slot in _slots)
                if (slot != null)
                    slot.SetSelected(slot == selectedSlot);

            _detailPanel?.Show(_selectedRecipe?.resultItem, "レシピ不所持");
            RebuildMaterialRows();
        }

        private void SelectInitialRecipe()
        {
            var firstSlot = _slots.FirstOrDefault(slot =>
                slot != null && slot.Recipe != null && slot.Recipe.resultItem != null
            );

            if (firstSlot != null)
            {
                SelectRecipeSlot(firstSlot);
                return;
            }

            _selectedRecipe = null;
            foreach (var slot in _slots)
                slot?.SetSelected(false);

            _detailPanel?.Show(null, "レシピ不所持");
            RebuildMaterialRows();
        }

        private void RebuildMaterialRows()
        {
            if (_materialList == null)
                return;

            for (int i = _materialList.childCount - 1; i >= 0; i--)
                Destroy(_materialList.GetChild(i).gameObject);

            if (_selectedRecipe == null || _materialRowPrefab == null)
                return;

            foreach (var material in _selectedRecipe.Materials)
            {
                if (material == null)
                    continue;

                var rowObject = Instantiate(_materialRowPrefab, _materialList, false);
                var row = rowObject.GetComponent<RecipeMaterialRow>();
                row ??= rowObject.AddComponent<RecipeMaterialRow>();
                row.Show(material);
            }

            if (_materialList is RectTransform materialRect)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(materialRect);
            }
        }

        private void BindDialog()
        {
            BindButton(_minButton, SetMinimum);
            BindButton(_minusButton, Decrease);
            BindButton(_plusButton, Increase);
            BindButton(_maxButton, SetMaximum);
            BindButton(_cancelButton, CloseQuantityDialog);
            BindButton(_dialogCraftButton, StartCraft);

            if (_quantityInput != null)
            {
                _quantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                _quantityInput.onEndEdit.RemoveListener(OnQuantityInput);
                _quantityInput.onEndEdit.AddListener(OnQuantityInput);
            }
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void OpenQuantityDialog()
        {
            FindReferences();
            int max = GetMaximumCraftable();
            if (_selectedRecipe == null)
                return;

            if (max <= 0)
            {
                PlayMissingMaterialsWarning();
                return;
            }

            _quantity = Mathf.Clamp(_quantity, 1, Mathf.Max(1, max));
            RefreshQuantityDialog();
            PlayDialogOpenAnimation();
        }

        private void CloseQuantityDialog()
        {
            PlayDialogCloseAnimation();
        }

        private void CloseQuantityDialogImmediately()
        {
            KillDialogAnimation();

            if (_quantityDialog != null)
                _quantityDialog.SetActive(false);
            if (_quantityDialogPanel != null)
                _quantityDialogPanel.SetActive(false);
            SetCloseButtonVisible(true);
        }

        private void PlayDialogOpenAnimation()
        {
            if (_quantityDialog == null)
                return;

            KillDialogAnimation();
            SetCloseButtonVisible(false);
            if (_quantityDialogPanel != null)
                _quantityDialogPanel.SetActive(true);
            _quantityDialog.SetActive(true);

            if (_quantityDialogRect != null)
                _quantityDialogRect.localScale = Vector3.one * _dialogStartScale;
            if (_quantityDialogCanvasGroup != null)
            {
                _quantityDialogCanvasGroup.alpha = 0f;
                _quantityDialogCanvasGroup.interactable = false;
                _quantityDialogCanvasGroup.blocksRaycasts = false;
            }

            var sequence = DOTween.Sequence().SetUpdate(true);
            if (_quantityDialogRect != null)
            {
                sequence.Join(
                    _quantityDialogRect
                        .DOScale(Vector3.one, _dialogAnimationDuration)
                        .SetEase(Ease.OutBack)
                );
            }
            if (_quantityDialogCanvasGroup != null)
            {
                sequence.Join(
                    _quantityDialogCanvasGroup.DOFade(1f, _dialogAnimationDuration * 0.75f)
                );
            }

            sequence.OnComplete(() =>
            {
                if (_quantityDialogCanvasGroup == null)
                    return;

                _quantityDialogCanvasGroup.interactable = true;
                _quantityDialogCanvasGroup.blocksRaycasts = true;
            });
        }

        private void PlayDialogCloseAnimation()
        {
            if (_quantityDialog == null || !_quantityDialog.activeSelf)
                return;

            KillDialogAnimation();
            if (_quantityDialogCanvasGroup != null)
            {
                _quantityDialogCanvasGroup.interactable = false;
                _quantityDialogCanvasGroup.blocksRaycasts = false;
            }

            var sequence = DOTween.Sequence().SetUpdate(true);
            if (_quantityDialogRect != null)
            {
                sequence.Join(
                    _quantityDialogRect
                        .DOScale(Vector3.one * 0.9f, _dialogAnimationDuration * 0.75f)
                        .SetEase(Ease.InBack)
                );
            }
            if (_quantityDialogCanvasGroup != null)
            {
                sequence.Join(
                    _quantityDialogCanvasGroup.DOFade(0f, _dialogAnimationDuration * 0.65f)
                );
            }

            sequence.OnComplete(() =>
            {
                _quantityDialog.SetActive(false);
                if (_quantityDialogPanel != null)
                    _quantityDialogPanel.SetActive(false);
                if (!_isCrafting)
                    SetCloseButtonVisible(true);
            });
        }

        private void KillDialogAnimation()
        {
            _quantityDialogRect?.DOKill();
            _quantityDialogCanvasGroup?.DOKill();
        }

        private void SetMinimum() => SetQuantity(1);

        private void Decrease() => SetQuantity(_quantity - 1);

        private void Increase() => SetQuantity(_quantity + 1);

        private void SetMaximum() => SetQuantity(GetMaximumCraftable());

        private void OnQuantityInput(string value)
        {
            SetQuantity(int.TryParse(value, out int parsed) ? parsed : 1);
        }

        private void SetQuantity(int quantity)
        {
            _quantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, GetMaximumCraftable()));
            RefreshQuantityDialog();
        }

        private void RefreshQuantityDialog()
        {
            FindReferences();
            int max = GetMaximumCraftable();

            if (_dialogItemImage != null)
            {
                _dialogItemImage.sprite = _selectedRecipe?.resultItem?.icon;
                _dialogItemImage.gameObject.SetActive(_dialogItemImage.sprite != null);
            }

            if (_dialogItemName != null)
                _dialogItemName.text = _selectedRecipe?.resultItem?.itemName ?? string.Empty;
            if (_dialogCounts != null)
                _dialogCounts.text = $"作成数（最大 {max}）";
            if (_quantityInput != null)
                _quantityInput.SetTextWithoutNotify(_quantity.ToString());
            if (_dialogCraftButton != null)
                _dialogCraftButton.interactable = max > 0 && !_isCrafting;
        }

        private int GetMaximumCraftable()
        {
            if (_selectedRecipe == null)
                return 0;

            int max = int.MaxValue;
            var materials = _selectedRecipe.Materials.ToList();
            if (materials.Count != 2)
                return 0;

            foreach (var group in materials.GroupBy(material => material))
            {
                if (group.Key == null)
                    return 0;

                int required = group.Count();
                int owned = InventoryManager.Instance?.GetItemCount(group.Key) ?? 0;
                max = Mathf.Min(max, owned / required);
            }

            return Mathf.Max(0, max);
        }

        private void StartCraft()
        {
            if (_isCrafting || _selectedRecipe == null)
                return;

            SetQuantity(_quantity);
            if (!(InventoryManager.Instance?.CanCraft(_selectedRecipe, _quantity) ?? false))
            {
                RefreshQuantityDialog();
                RebuildMaterialRows();
                PlayMissingMaterialsWarning();
                return;
            }

            CloseQuantityDialog();
            _craftRoutine = StartCoroutine(CraftRoutine());
        }

        private IEnumerator CraftRoutine()
        {
            _isCrafting = true;
            SetCloseButtonVisible(false);

            if (_loadingPanel != null)
                _loadingPanel.SetActive(true);
            if (_loadingGear != null)
            {
                _loadingGear.localRotation = Quaternion.identity;
                _loadingGear.gameObject.SetActive(true);
            }
            if (_resultPanel != null)
                _resultPanel.SetActive(false);

            yield return new WaitForSecondsRealtime(_testCraftDuration);

            bool crafted = InventoryManager.Instance?.TryCraft(_selectedRecipe, _quantity) ?? false;
            _isCrafting = false;
            _craftRoutine = null;

            if (_loadingGear != null)
                _loadingGear.gameObject.SetActive(false);

            if (!crafted)
            {
                if (_loadingPanel != null)
                    _loadingPanel.SetActive(false);
                SetCloseButtonVisible(true);
                RebuildMaterialRows();
                yield break;
            }

            if (_resultPanel != null)
            {
                RefreshResultPanel();
                _resultClickCatcher?.SetClickAction(CloseResult);
                _resultPanel.SetActive(true);
            }
        }

        private void RefreshResultPanel()
        {
            FindReferences();
            var resultItem = _selectedRecipe?.resultItem;

            if (_resultItemImage != null)
            {
                _resultItemImage.sprite = resultItem?.icon;
                _resultItemImage.color = resultItem?.icon != null ? Color.white : Color.clear;
                _resultItemImage.gameObject.SetActive(resultItem?.icon != null);
            }

            if (_resultItemName != null)
            {
                int totalResultCount = Mathf.Max(1, _quantity);
                _resultItemName.text =
                    resultItem == null ? string.Empty
                    : totalResultCount > 1 ? $"{resultItem.itemName} ×{totalResultCount}"
                    : resultItem.itemName;
            }
        }

        private void CloseResult()
        {
            if (_resultPanel != null)
                _resultPanel.SetActive(false);
            if (_loadingPanel != null)
                _loadingPanel.SetActive(false);

            SetCloseButtonVisible(true);
            SelectRecipeSlot(_slots.FirstOrDefault(slot => slot?.Recipe == _selectedRecipe));
            transform.parent?.GetComponentInChildren<Inventory>(true)?.RefreshCurrentTab();
        }

        private void ResetView()
        {
            _selectedRecipe = null;
            _quantity = 1;
            foreach (var slot in _slots)
                slot?.SetSelected(false);

            if (_detailPanel != null)
                _detailPanel.Clear();
            RebuildMaterialRows();
            HideWarningImmediately();
            if (_quantityDialogPanel != null)
                _quantityDialogPanel.SetActive(false);
            if (_quantityDialog != null)
                _quantityDialog.SetActive(false);
            if (_resultPanel != null)
                _resultPanel.SetActive(false);
            if (_loadingPanel != null)
                _loadingPanel.SetActive(false);
            SetCloseButtonVisible(true);
        }

        private void SetCloseButtonVisible(bool visible)
        {
            if (_closeButton != null)
                _closeButton.SetActive(visible);
        }

        private void PlayMissingMaterialsWarning()
        {
            FindReferences();
            if (_warningText == null)
                return;

            _warningSequence?.Kill();
            _warningText.DOKill();
            _warningTextRect?.DOKill();
            _warningTextCanvasGroup?.DOKill();

            _warningText.text = _missingMaterialsMessage;
            _warningText.gameObject.SetActive(true);
            if (_warningTextCanvasGroup != null)
                _warningTextCanvasGroup.alpha = 1f;

            if (_warningTextRect == null)
                return;

            Vector2 basePosition = _warningTextRect.anchoredPosition;
            _warningSequence = DOTween.Sequence().SetUpdate(true);
            for (int i = 0; i < 4; i++)
            {
                _warningSequence.Append(
                    _warningTextRect.DOAnchorPosX(basePosition.x + _warningShakeDistance, 0.04f)
                );
                _warningSequence.Append(
                    _warningTextRect.DOAnchorPosX(basePosition.x - _warningShakeDistance, 0.04f)
                );
            }

            _warningSequence.Append(_warningTextRect.DOAnchorPosX(basePosition.x, 0.04f));
            _warningSequence.AppendInterval(_warningFadeDelay);
            if (_warningTextCanvasGroup != null)
                _warningSequence.Append(_warningTextCanvasGroup.DOFade(0f, _warningFadeDuration));

            _warningSequence.OnComplete(() =>
            {
                _warningTextRect.anchoredPosition = basePosition;
                _warningText.gameObject.SetActive(false);
                _warningSequence = null;
            });
        }

        private void HideWarningImmediately()
        {
            _warningSequence?.Kill();
            _warningSequence = null;

            if (_warningText == null)
                return;

            _warningText.DOKill();
            _warningTextRect?.DOKill();
            _warningTextCanvasGroup?.DOKill();
            if (_warningTextCanvasGroup != null)
                _warningTextCanvasGroup.alpha = 0f;
            _warningText.gameObject.SetActive(false);
        }

        private Transform Find(string objectName) => FindIn(transform, objectName);

        private Transform FindRecipeContent()
        {
            if (_recipeList == null)
                return null;

            var scrollRect = _recipeList.GetComponent<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
                return scrollRect.content;

            return FindIn(_recipeList, "Content") ?? _recipeList;
        }

        private static Transform FindIn(Transform root, string objectName)
        {
            if (root == null)
                return null;

            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);
        }

        private static Button FindButton(Transform root, string objectName)
        {
            return FindIn(root, objectName)?.GetComponent<Button>();
        }

        private static T FindComponentIn<T>(Transform root, string objectName)
            where T : Component
        {
            var target = FindIn(root, objectName);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static GameObject FindGameObjectIn(Transform root, string objectName)
        {
            var target = FindIn(root, objectName);
            return target != null ? target.gameObject : null;
        }
    }
}
