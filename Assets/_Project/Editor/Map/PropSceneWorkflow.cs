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
    /// 小物(机・椅子・本棚…)を<b>複数人で同時に置く</b>ための段取り。
    ///
    /// マップ(`Field_Area01.unity`)は <see cref="MapLayoutBuilder"/> の生成物なので誰も直接触らず、
    /// 各担当は<b>自分の小物シーンだけ</b>を編集する。シーンは Additive で重ねて開くので、
    /// 見た目は1つの空間で作業するのと同じまま、保存先のファイルだけが分かれる
    /// (= 同じ .unity を複数人が触らないので git の競合が起きない)。
    ///
    /// 使い方:
    /// 1. `小物シーン/雛形を作成` … 未作成の階ぶんだけ空シーンを作る(初回だけ)
    /// 2. `小物シーン/実行時に重ねる設定` … Field_Area01 に <c>AdditiveScenes</c> を配線し、
    ///    小物3枚を Build Settings に登録する(初回だけ / <see cref="PropSceneSetup"/>)
    /// 3. `小物シーン/1F を開く` … マップ + 担当シーンを開き、担当シーンをアクティブにし、
    ///    マップを掴めなくする(Scene ビューで壁をドラッグして動かす事故を防ぐ)
    /// 4. 置いて保存。保存されるのは自分のシーンだけ
    ///
    /// <b>1枚に畳む手段は用意しない。</b> 小物シーンは実行時に <c>AdditiveScenes</c> が重ねて読むので、
    /// 分けたままリリースまで通せる。畳むと 1F/2F/3F の担当者が同じ Field_Area01.unity を
    /// 触ることになり競合が復活するため、そういう操作を置かないこと自体を仕組みにしている。
    ///
    /// 万一マップがずれても、`Rebuild Field_Area01` で図(documents/MapLayout.md)から作り直せる。
    /// 作り直すのは `Map` ルートだけなので、小物は消えない。
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
        /// マップ + 指定階の小物シーンを開き、<b>小物シーンをアクティブに</b>する。
        /// アクティブにしないと、置いた小物がマップシーン側に入ってしまう(= 競合の元)。
        /// あわせてマップの `Map` ルートを<b>ピッキング無効</b>にして、Scene ビューで
        /// 壁や床を掴めないようにする。
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
