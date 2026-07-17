using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// レシピの解禁(発見)状態だけを持つセッション常駐。自由調合(FreeCraftPanel)で新しい組み合わせを
    /// 成功させると、そのレシピがここに解禁され、レシピ一覧(RecipeCraftPanelController)に並ぶ=再利用できる。
    ///
    /// 解禁状態は「プレイごとの状態」なので、進行度(ProgressManager)や所持品(InventoryManager)とは別概念として
    /// ここに独立させる(=セッション常駐)。新規開始で自動的にまっさら・タイトルに戻ると破棄・続きからは
    /// SaveService が復元し、マニュアルセーブで保存される(documents/Specification.md §6)。
    /// カタログ本体(CraftRecipeDB / ItemDB)は読み取り専用アセットで、解禁状態は持たない。
    ///
    /// レシピの識別キーは結果アイテムの id(resultItem.id)。Scene 読み込み前に自動生成する。
    /// </summary>
    public sealed class RecipeBookManager : MonoBehaviour
    {
        public static RecipeBookManager Instance { get; private set; }

        // 解禁済みレシピのキー集合(= resultItem.id)。
        private readonly HashSet<int> _revealed = new();

        public event Action<CraftRecipeData> RecipeRevealed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureResident();
        }

        /// <summary>セッション常駐生成の入口。既に在ればそれを返す。</summary>
        public static RecipeBookManager EnsureResident()
        {
            if (Instance != null)
                return Instance;
            return new GameObject(nameof(RecipeBookManager)).AddComponent<RecipeBookManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // タイトル復帰・連打での二重生成をガード(冪等)
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private static bool TryKey(CraftRecipeData recipe, out int key)
        {
            key = 0;
            if (recipe == null || recipe.resultItem == null)
                return false;
            key = recipe.resultItem.id;
            return true;
        }

        public bool IsRevealed(CraftRecipeData recipe) =>
            TryKey(recipe, out var key) && _revealed.Contains(key);

        /// <summary>レシピを解禁する。新規に解禁できたら true(既に解禁済み/無効なら false)。</summary>
        public bool Reveal(CraftRecipeData recipe)
        {
            if (!TryKey(recipe, out var key))
                return false;

            if (!_revealed.Add(key))
                return false;

            RecipeRevealed?.Invoke(recipe);
            return true;
        }

        // --- セーブ(SaveService が読み書きする) ---

        /// <summary>解禁済みレシピキー(resultItem.id)のスナップショット。</summary>
        public IReadOnlyCollection<int> CaptureRevealed() => _revealed;

        /// <summary>解禁状態を丸ごと差し替える(続きから復元用)。null は空扱い。</summary>
        public void RestoreRevealed(IEnumerable<int> ids)
        {
            _revealed.Clear();
            if (ids == null)
                return;
            foreach (var id in ids)
                _revealed.Add(id);
        }
    }
}
