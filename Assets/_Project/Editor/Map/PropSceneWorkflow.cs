#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 小物を複数人で同時に置くための段取り。マップ(<see cref="MapLayoutBuilder"/> の生成物)は触らず、各担当は
    /// 自分の小物シーンだけを Additive で重ねて編集する(同じ .unity を触らないので git 競合が起きない)。
    /// 手順: `雛形を作成` → `実行時に重ねる設定`(<see cref="PropSceneSetup"/>) → `1F を開く` → 置いて保存。
    /// 競合が復活するので1枚に畳む手段は置かない。マップがずれても `Rebuild Field_Area01` は `Map` ルートだけ作り直す。
    /// </summary>
    public static class PropSceneWorkflow
    {
        const string SceneDir = "Assets/_Project/Scenes/Field";
        const string Prefix = "Field_Area01_Props_";
        static readonly string[] Floors = { "1F", "2F", "3F" };

        static string ScenePath(string floor) => $"{SceneDir}/{Prefix}{floor}.unity";

        static string RootName(string floor) => $"Props_{floor}";

        // ------------------------------------------------------------------ 雛形

        [MenuItem("Tools/CreativeAI/Map/小物シーン/雛形を作成(未作成のぶんだけ)")]
        public static void CreateMissingScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // 名前の付いていない(未保存の)シーンが開いていると Additive で新規シーンを作れない。
            // Editor を開いた直後やバッチ実行がこれに当たるので、先にマップシーンを開く。
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(MapLayoutBuilder.MapScenePath, OpenSceneMode.Single);

            var created = 0;
            foreach (var floor in Floors)
            {
                var path = ScenePath(floor);
                if (File.Exists(path))
                    continue;

                // 空シーンを Additive で作り、ルートを1つだけ入れて保存する。
                // (Additive にしないと、いま開いているシーンを閉じてしまう)
                var scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive
                );
                var root = new GameObject(RootName(floor));
                SceneManager.MoveGameObjectToScene(root, scene); // 新規 GO はアクティブなシーンに入るため
                Directory.CreateDirectory(SceneDir);
                EditorSceneManager.SaveScene(scene, path);
                EditorSceneManager.CloseScene(scene, true);
                created++;
            }

            AssetDatabase.Refresh();
            Debug.Log(
                created == 0
                    ? "[PropScene] 小物シーンは3階ぶんとも作成済みです。"
                    : $"[PropScene] 小物シーンを {created} 枚作成しました({SceneDir}/{Prefix}*.unity)。"
            );
        }

        // ------------------------------------------------------------------ 開く

        [MenuItem("Tools/CreativeAI/Map/小物シーン/1F を開く")]
        public static void Open1F() => OpenForWork("1F");

        [MenuItem("Tools/CreativeAI/Map/小物シーン/2F を開く")]
        public static void Open2F() => OpenForWork("2F");

        [MenuItem("Tools/CreativeAI/Map/小物シーン/3F を開く")]
        public static void Open3F() => OpenForWork("3F");

        /// <summary>
        /// マップ + 指定階の小物シーンを開き、小物シーンをアクティブにする(しないと小物がマップ側に入る)。
        /// `Map` ルートはピッキング無効にして壁や床を掴めないようにする。
        /// </summary>
        public static void OpenForWork(string floor)
        {
            var propPath = ScenePath(floor);
            if (!File.Exists(propPath))
            {
                Debug.LogError(
                    $"[PropScene] {propPath} がありません。先に「雛形を作成」を実行してください。"
                );
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var map = EditorSceneManager.OpenScene(
                MapLayoutBuilder.MapScenePath,
                OpenSceneMode.Single
            );
            var props = EditorSceneManager.OpenScene(propPath, OpenSceneMode.Additive);
            EditorSceneManager.SetActiveScene(props);
            LockMap(map);

            Debug.Log(
                $"[PropScene] {floor} の小物シーンを開きました。\n"
                    + $"・新しく置いたものは「{props.name}」に入ります(アクティブ)。\n"
                    + "・マップは掴めないようにしてあります(Hierarchy の手のアイコンで解除可)。\n"
                    + "・保存(Ctrl+S)すると、変更したシーンだけが保存されます。"
            );
        }

        /// <summary>マップを Scene ビューで選択できなくする(誤ドラッグ防止)。</summary>
        static void LockMap(Scene map)
        {
            var root = map.GetRootGameObjects()
                .FirstOrDefault(g => g.name == MapLayoutBuilder.MapRoot);
            if (root == null)
                return;
            SceneVisibilityManager.instance.DisablePicking(root, true);
        }
    }
}
#endif
