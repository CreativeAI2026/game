#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 小物シーン(<see cref="PropSceneWorkflow"/>)に置いた小物へ当たりを付ける。見た目のメッシュごとに形へ合わせた
    /// BoxCollider を付け、Map の壁と同じ Obstacle レイヤーにする(プレイヤー・カメラ・敵の弾が壁と同じに扱う)。
    /// 既に当たりのある小物(手で付けたもの)は触らないので、何度実行しても二重に付かない。
    /// 卓上の小物や壁掛けのように、ぶつかる必要のない物は <see cref="ExcludedModels"/> で外す。
    /// NavMesh は Map シーンだけから焼くので、ここで付けた当たりを敵は避けない。
    /// </summary>
    public static class PropColliderTool
    {
        const string ObstacleLayerName = "Obstacle";

        // 当たりを付けないモデル(拡張子を除いたファイル名)。机や棚の当たりで足りる卓上の物と、壁や天井に付く物
        public static readonly HashSet<string> ExcludedModels = new HashSet<string>
        {
            "Book_A",
            "Book_B",
            "Book_C",
            "Book_D",
            "Paper",
            "Pen_black",
            "Pen_blue",
            "Pen_red",
            "File",
            "Cardboard_paper",
            "Cardboard_paper_2",
            "Chopstick holder",
            "Fork holder",
            "Spoon holder",
            "Seasoning",
            "Electric kettle",
            "Microwave",
            "Cash register",
            "portrait monitor",
            "Clock",
            "light",
            "Blinder",
            "Projector screen",
        };

        public readonly struct Result
        {
            public readonly int Added; // 付けた BoxCollider の数
            public readonly int Excluded; // 除外リストで飛ばしたメッシュの数
            public readonly int AlreadyHad; // 既に当たりがあって飛ばしたメッシュの数

            public Result(int added, int excluded, int alreadyHad)
            {
                Added = added;
                Excluded = excluded;
                AlreadyHad = alreadyHad;
            }
        }

        // ------------------------------------------------------------------ メニュー

        /// <summary>開いている小物シーンに付ける。保存はしないので、確かめてから Ctrl+S(Ctrl+Z で戻せる)。</summary>
        [MenuItem("Tools/CreativeAI/Map/小物シーン/当たり判定を付ける(開いている小物シーン)")]
        public static void AddToOpenPropScenes()
        {
            var scenes = Enumerable
                .Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(s => s.isLoaded && IsPropScene(s.path))
                .ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogWarning(
                    "[PropCollider] 小物シーンが開いていません。"
                        + "「小物シーン/1F を開く」などで開いてから実行してください。"
                );
                return;
            }

            var layer = ObstacleLayer();
            if (layer < 0)
                return;
            foreach (var scene in scenes)
            {
                var r = AddToScene(scene, layer);
                if (r.Added > 0)
                    EditorSceneManager.MarkSceneDirty(scene);
                Log(scene.name, r);
            }
            Debug.Log(
                "[PropCollider] まだ保存していません。確かめてから Ctrl+S で保存してください。"
            );
        }

        /// <summary>全部の小物シーンを順に開いて付け、保存する。</summary>
        [MenuItem(
            "Tools/CreativeAI/Map/小物シーン/当たり判定を付ける(全部の小物シーンに付けて保存)"
        )]
        public static void AddToAllPropScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var layer = ObstacleLayer();
            if (layer < 0)
                return;

            // マップを土台に開き、小物シーンを1枚ずつ重ねて閉じる(Additive で開くには名前のあるシーンが要る)
            EditorSceneManager.OpenScene(MapLayoutBuilder.MapScenePath, OpenSceneMode.Single);
            foreach (var part in PropSceneWorkflow.Parts)
            {
                var path = PropSceneWorkflow.ScenePath(part);
                if (!File.Exists(path))
                    continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var r = AddToScene(scene, layer);
                if (r.Added > 0)
                    EditorSceneManager.SaveScene(scene);
                EditorSceneManager.CloseScene(scene, true);
                Log(scene.name, r);
            }
        }

        // ------------------------------------------------------------------ 本体

        static Result AddToScene(Scene scene, int layer)
        {
            int added = 0,
                excluded = 0,
                alreadyHad = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                var r = AddColliders(root, layer);
                added += r.Added;
                excluded += r.Excluded;
                alreadyHad += r.AlreadyHad;
            }
            return new Result(added, excluded, alreadyHad);
        }

        /// <summary>
        /// root 配下のメッシュに BoxCollider を付けて <paramref name="layer"/> にする。
        /// 小物1つ(一番外側の Prefab インスタンス。Prefab でなければメッシュの GameObject)の中に
        /// 当たりが1つでもあれば、その小物は丸ごと飛ばす。
        /// </summary>
        public static Result AddColliders(GameObject root, int layer)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f.sharedMesh != null && f.GetComponent<Renderer>() != null)
                .ToArray();

            // 付ける前に調べる(1つ目のメッシュに付けた当たりで、同じ小物の残りを飛ばさないように)
            var hadCollider = new HashSet<GameObject>(
                filters
                    .Select(f => PropOf(f.gameObject))
                    .Distinct()
                    .Where(p => p.GetComponentInChildren<Collider>(true) != null)
            );

            int added = 0,
                excluded = 0,
                alreadyHad = 0;
            foreach (var filter in filters)
            {
                var go = filter.gameObject;
                var prop = PropOf(go);
                if (hadCollider.Contains(prop))
                {
                    alreadyHad++;
                    continue;
                }
                if (ExcludedModels.Contains(ModelName(prop)))
                {
                    excluded++;
                    continue;
                }

                var bounds = filter.sharedMesh.bounds;
                var box = Undo.AddComponent<BoxCollider>(go);
                box.center = bounds.center;
                box.size = bounds.size;

                Undo.RecordObject(go, "Set Obstacle Layer");
                go.layer = layer;
                // Prefab インスタンスの中は記録しないとシーン保存時に上書きが残らない
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                added++;
            }
            return new Result(added, excluded, alreadyHad);
        }

        /// <summary>メッシュが属する小物(一番外側の Prefab インスタンス。無ければそのメッシュ自身)。</summary>
        static GameObject PropOf(GameObject go)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            return root != null ? root : go; // UnityEngine.Object は ?? だと破棄済みを見分けない
        }

        /// <summary>
        /// 小物のモデル名。Prefab インスタンスなら元アセットのファイル名、そうでなければ
        /// 配置時に付く連番(`_01` や ` (1)`)を除いた GameObject 名。
        /// </summary>
        public static string ModelName(GameObject prop)
        {
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prop);
            if (!string.IsNullOrEmpty(path))
                return Path.GetFileNameWithoutExtension(path);
            return Regex.Replace(prop.name, @"(_\d+| \(\d+\))$", "");
        }

        static bool IsPropScene(string path) =>
            PropSceneWorkflow.Parts.Any(p => PropSceneWorkflow.ScenePath(p) == path);

        static int ObstacleLayer()
        {
            var layer = LayerMask.NameToLayer(ObstacleLayerName);
            if (layer < 0)
                Debug.LogError(
                    $"[PropCollider] レイヤー {ObstacleLayerName} が Tags and Layers にありません。"
                );
            return layer;
        }

        static void Log(string sceneName, Result r) =>
            Debug.Log(
                $"[PropCollider] {sceneName}: 当たりを {r.Added} 個付けました"
                    + $"(除外リスト {r.Excluded} / 当たり済み {r.AlreadyHad} は飛ばしました)。"
            );
    }
}
#endif
