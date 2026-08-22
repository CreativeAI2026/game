#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 小物シーンを<b>畳まずに遊べる</b>ようにする配線。
    ///
    /// 1. `Field_Area01` に <see cref="AdditiveScenes"/> を置き、小物3枚を宣言する
    /// 2. 小物3枚を Build Settings に登録する(名前で読むので未登録だと実行時に読めない)
    ///
    /// これで「担当ごとに別ファイル」を保ったままゲームに出せる。
    /// 統合(MergeIntoMapScene)は使わない運用になる — documents/PropPlacementWorkflow.md 参照。
    /// </summary>
    public static class PropSceneSetup
    {
        const string HolderName = "PropScenes";
        static readonly string[] Floors = { "1F", "2F", "3F" };

        static string ScenePath(string floor) =>
            $"Assets/_Project/Scenes/Field/Field_Area01_Props_{floor}.unity";

        static string SceneName(string floor) => $"Field_Area01_Props_{floor}";

        [MenuItem("Tools/CreativeAI/Map/小物シーン/実行時に重ねる設定(配線 + Build Settings 登録)")]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var missing = Floors.Where(f => !File.Exists(ScenePath(f))).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError(
                    $"[PropSceneSetup] 小物シーンがありません: {string.Join(", ", missing)}。"
                        + "先に「雛形を作成」を実行してください。"
                );
                return;
            }

            RegisterInBuildSettings();
            WireUpMapScene();
        }

        /// <summary>小物3枚を Build Settings の末尾に足す(既にあれば触らない)。</summary>
        static void RegisterInBuildSettings()
        {
            var list = EditorBuildSettings.scenes.ToList();
            var added = new List<string>();
            foreach (var floor in Floors)
            {
                var path = ScenePath(floor);
                if (list.Any(s => s.path == path))
                    continue;
                list.Add(new EditorBuildSettingsScene(path, true));
                added.Add(Path.GetFileNameWithoutExtension(path));
            }
            if (added.Count == 0)
            {
                Debug.Log("[PropSceneSetup] 小物シーンは Build Settings に登録済みです。");
                return;
            }
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log(
                $"[PropSceneSetup] Build Settings に追加しました: {string.Join(", ", added)}\n"
                    + "登録順(= ビルドインデックス)は末尾なので、既存シーンの番号は変わりません。"
            );
        }

        /// <summary>Field_Area01 に AdditiveScenes を置いて小物3枚を宣言する。</summary>
        static void WireUpMapScene()
        {
            var scene = EditorSceneManager.OpenScene(
                MapLayoutBuilder.MapScenePath,
                OpenSceneMode.Single
            );

            // Map の外(ルート直下)に置く。Rebuild は Map ルートだけを作り直すので消えない。
            var holder = scene.GetRootGameObjects().FirstOrDefault(g => g.name == HolderName);
            if (holder == null)
            {
                holder = new GameObject(HolderName);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, scene);
            }

            var comp = holder.GetComponent<AdditiveScenes>();
            if (comp == null)
                comp = holder.AddComponent<AdditiveScenes>();

            var names = Floors.Select(SceneName).ToArray();
            var so = new SerializedObject(comp);
            var arr = so.FindProperty("_sceneNames");
            arr.arraySize = names.Length;
            for (var i = 0; i < names.Length; i++)
                arr.GetArrayElementAtIndex(i).stringValue = names[i];
            so.FindProperty("_skipAlreadyLoaded").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                $"[PropSceneSetup] {MapLayoutBuilder.MapScenePath} の '{HolderName}' に "
                    + $"AdditiveScenes を配線しました: {string.Join(", ", names)}\n"
                    + "Play すると小物シーンが重なって読み込まれます(統合は不要)。"
            );
        }
    }
}
#endif
