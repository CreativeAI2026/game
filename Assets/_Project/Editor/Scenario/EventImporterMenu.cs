#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// EventImporter を叩いて events.json を EventDefinition(.asset)に書き出すエディタ拡張。
    /// 手動: Tools > CreativeAI > Import Events / バッチ:
    /// Unity -batchmode -quit -executeMethod CreativeAI.EditorTools.EventImporterMenu.Run
    /// </summary>
    public static class EventImporterMenu
    {
        private const string DefaultSource = "Assets/_Project/Features/Event/events.json";
        private const string OutputDir = "Assets/_Project/Features/Event/Data";
        private const string ItemDataDir = "Assets/_Project/Features/Inventory/Data";
        private const string CharacterDataDir =
            "Assets/_Project/Features/UI/ConversationUI/Data/Characters";

        [MenuItem("Tools/CreativeAI/Import Events")]
        public static void Import()
        {
            var start = File.Exists(DefaultSource)
                ? Path.GetDirectoryName(Path.GetFullPath(DefaultSource))
                : Application.dataPath;
            var picked = EditorUtility.OpenFilePanel("Import events.json", start, "json");
            if (string.IsNullOrEmpty(picked))
                return; // キャンセル
            RunImport(picked);
        }

        /// <summary>バッチ実行の入口。既定パスの events.json を取り込む。</summary>
        public static void Run() => RunImport(DefaultSource);

        private static void RunImport(string path) => ImportInto(path, OutputDir);

        /// <summary>
        /// events.json を検証し、<paramref name="outputDir"/>(Assets 相対)へ 1イベント = 1 .asset で書き出す。
        /// 1件でもエラーがあれば何も書き出さず false。出力先を指定できるのはテストのため。
        /// </summary>
        public static bool ImportInto(string path, string outputDir)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[EventImporter] ファイルが見つかりません: {path}");
                return false;
            }

            var report = EventImporter.Parse(File.ReadAllText(path), BuildCatalog());

            foreach (var d in report.Diagnostics)
            {
                if (d.Severity == EventImporter.Severity.Error)
                    Debug.LogError($"[EventImporter] {d}");
                else
                    Debug.LogWarning($"[EventImporter] {d}");
            }

            if (report.HasErrors)
            {
                Debug.LogError(
                    $"[EventImporter] エラー {report.ErrorCount} 件のため中止しました(1件も書き出していません)。"
                );
                return false;
            }

            EnsureFolder(outputDir);

            int created = 0,
                updated = 0;
            foreach (var built in report.Events)
            {
                var assetPath = $"{outputDir}/{built.Id}.asset";
                built.name = built.Id; // アセット名をファイル名と揃える(空だと Unity が警告する)
                var existing = AssetDatabase.LoadAssetAtPath<EventDefinition>(assetPath);
                if (existing != null)
                {
                    // 既存 asset に上書き。GUID を保つのでシーンの EventTrigger 参照が壊れない。
                    EditorUtility.CopySerialized(built, existing);
                    EditorUtility.SetDirty(existing);
                    updated++;
                }
                else
                {
                    AssetDatabase.CreateAsset(built, assetPath);
                    created++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WarnLeftoverAssets(outputDir, report.Events.Select(e => e.Id));
            Debug.Log(
                $"[EventImporter] 完了: 新規 {created} / 更新 {updated}(警告 {report.WarningCount} 件)→ {outputDir}"
            );
            return true;
        }

        /// <summary>
        /// events.json から消えた(id を変えた)イベントの .asset は自動では消さない(シーンの参照を勝手に壊すため)。
        /// 古い台詞のまま残り、トリガーが参照していると黙って流れ続けるので、警告で一覧を出す。
        /// </summary>
        private static void WarnLeftoverAssets(string outputDir, IEnumerable<string> importedIds)
        {
            var ids = importedIds.ToHashSet(StringComparer.Ordinal);
            var leftovers = AssetDatabase
                .FindAssets("t:EventDefinition", new[] { outputDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !ids.Contains(Path.GetFileNameWithoutExtension(p)))
                .ToList();
            if (leftovers.Count == 0)
                return;
            Debug.LogWarning(
                $"[EventImporter] events.json に無いイベントの .asset が残っています({leftovers.Count} 件)。"
                    + " id を変えた・消した場合は、シーンのトリガーの割り当てを確認してから削除してください: "
                    + string.Join(", ", leftovers)
            );
        }

        /// <summary>
        /// ItemData(key)から有効キー集合を作る(giveItem は全カテゴリ、hasItem は Important のみ)。
        /// アセットが無ければ null(警告どまり)、あれば存在検証する。敵は EventTrigger 配線なので照合しない。
        /// </summary>
        private static EventImporter.ImportCatalog BuildCatalog()
        {
            var keyed = AssetDatabase
                .FindAssets("t:ItemData", new[] { ItemDataDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                .Where(i => i != null && !string.IsNullOrEmpty(i.key))
                .ToList();

            var itemKeys = keyed.Select(i => i.key).ToHashSet(StringComparer.Ordinal);
            var keyItemKeys = keyed
                .Where(i => i.category == ItemCategory.Important)
                .Select(i => i.key)
                .ToHashSet(StringComparer.Ordinal);

            // 立ち絵アセットに実際に登録済みの portrait キー(絵の準備待ちを警告で可視化する)。
            var portraitKeys = AssetDatabase
                .FindAssets("t:DialogueCharacterDefinition", new[] { CharacterDataDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DialogueCharacterDefinition>)
                .Where(c => c != null)
                .SelectMany(c => c.Expressions)
                .Where(e => !string.IsNullOrEmpty(e.PortraitKey) && e.Sprite != null)
                .Select(e => e.PortraitKey)
                .ToHashSet(StringComparer.Ordinal);

            return new EventImporter.ImportCatalog(
                itemKeys.Count > 0 ? itemKeys : null,
                keyItemKeys.Count > 0 ? keyItemKeys : null,
                portraitKeys.Count > 0 ? portraitKeys : null
            );
        }

        /// <summary>Assets 相対フォルダを親から順に作成する。</summary>
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
