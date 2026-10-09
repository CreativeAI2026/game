using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEditor;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// CSV 取り込みから CraftRecipeCatalog のレシピ一覧を書き換える。
    /// 同じ完成品のレシピがあれば置き換え、無ければ末尾に追加する。
    /// </summary>
    public static class CraftRecipeCatalogWriter
    {
        private const string RecipesField = "_recipes";

        public static void SetRecipes(CraftRecipeCatalog catalog, IEnumerable<CraftRecipe> recipes)
        {
            var serializedCatalog = new SerializedObject(catalog);
            var list =
                serializedCatalog.FindProperty(RecipesField)
                ?? throw new InvalidOperationException(
                    $"{nameof(CraftRecipeCatalog)} に {RecipesField} が見つかりません。フィールド名を確認してください。"
                );

            foreach (var recipe in recipes)
            {
                int index = FindIndexByResult(list, recipe.resultItem);
                if (index < 0)
                {
                    index = list.arraySize;
                    list.arraySize++;
                }

                var element = list.GetArrayElementAtIndex(index);
                SetReference(element, nameof(CraftRecipe.material1), recipe.material1);
                SetReference(element, nameof(CraftRecipe.material2), recipe.material2);
                SetReference(element, nameof(CraftRecipe.resultItem), recipe.resultItem);
            }

            serializedCatalog.ApplyModifiedProperties();
        }

        private static int FindIndexByResult(SerializedProperty list, ItemData resultItem)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                var existing = list.GetArrayElementAtIndex(i)
                    .FindPropertyRelative(nameof(CraftRecipe.resultItem));
                if (existing.objectReferenceValue == resultItem)
                    return i;
            }
            return -1;
        }

        private static void SetReference(SerializedProperty element, string field, ItemData value)
        {
            element.FindPropertyRelative(field).objectReferenceValue = value;
        }
    }
}
