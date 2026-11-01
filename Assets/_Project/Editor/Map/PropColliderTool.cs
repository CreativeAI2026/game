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
    /// 小物シーン(<see cref="PropSceneWorkflow"/>)に置いた小物へ当たりを付ける。椅子のような小さい小物は全体を囲む
    /// BoxCollider を1つ、机の島やカウンターのような大きい小物は通路を塞がないようメッシュごとに形へ合わせた BoxCollider を付け、
    /// Map の壁と同じ Obstacle レイヤーにする(プレイヤー・カメラ・敵の弾が壁と同じに扱う)。
    /// このツールが付けた当たりは付け直し、手で付けた当たりのある小物は触らないので、何度実行しても二重に付かない。
    /// 卓上の小物や壁掛けのように、ぶつかる必要のない物は <see cref="ExcludedModels"/> で外す。
    /// NavMesh は Map シーンだけから焼くので、ここで付けた当たりを敵は避けない。
    /// </summary>
    public static class PropColliderTool
    {
        const string ObstacleLayerName = "Obstacle";

        // 全体を1つの箱にまとめる小物の大きさの上限(水平の幅・奥行き、m)。これより大きいと箱が通路や隙間を塞ぐ
        public const float MergeMaxFootprint = 2.5f;

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
            "Blinder",
            "Projector screen",
        };

        public readonly struct Result
        {
            public readonly int Added; // 付けた BoxCollider の数
            public readonly int Removed; // 付け直すために外した、以前このツールが付けた BoxCollider の数
            public readonly int Excluded; // 除外リストで飛ばしたメッシュの数
            public readonly int AlreadyHad; // 当たりが付け済み(手で付けたものを含む)で飛ばしたメッシュの数

            public Result(int added, int removed, int excluded, int alreadyHad)
            {
                Added = added;
                Removed = removed;
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
                if (r.Added > 0 || r.Removed > 0)
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
                if (r.Added > 0 || r.Removed > 0)
                    EditorSceneManager.SaveScene(scene);
                EditorSceneManager.CloseScene(scene, true);
                Log(scene.name, r);
            }
        }

        // ------------------------------------------------------------------ 本体

        static Result AddToScene(Scene scene, int layer)
        {
            int added = 0,
                removed = 0,
                excluded = 0,
                alreadyHad = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                var r = AddColliders(root, layer);
                added += r.Added;
                removed += r.Removed;
                excluded += r.Excluded;
                alreadyHad += r.AlreadyHad;
            }
            return new Result(added, removed, excluded, alreadyHad);
        }

        /// <summary>
        /// root 配下の小物に BoxCollider を付けて <paramref name="layer"/> にする。小さい小物は全体で1つ、
        /// 大きい小物はメッシュごと。以前このツールが付けた当たりが今の付け方と違えば外して付け直す。
        /// 手で付けた当たりが1つでもある小物は丸ごと飛ばす。
        /// </summary>
        public static Result AddColliders(GameObject root, int layer)
        {
            var props = root.GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f.sharedMesh != null && f.GetComponent<Renderer>() != null)
                .GroupBy(f => PropOf(f.gameObject))
                .ToArray();

            int added = 0,
                removed = 0,
                excluded = 0,
                alreadyHad = 0;
            foreach (var group in props)
            {
                var prop = group.Key;
                var parts = group.ToArray();
                if (ExcludedModels.Contains(ModelName(prop)))
                {
                    excluded += parts.Length;
                    continue;
                }

                var merged = LocalBounds(prop.transform, parts);
                bool merge = parts.Length > 1 && FitsInFootprint(prop.transform, merged);
                // 子に入れ子で置いた別の小物の当たりは、その小物の分なので数えない
                var colliders = prop.GetComponentsInChildren<Collider>(true)
                    .Where(c => c.gameObject == prop || PropOf(c.gameObject) == prop)
                    .ToArray();
                var ours = colliders
                    .Where(c => IsPartBox(c) || IsMergedBox(c, prop, merged))
                    .ToArray();
                if (ours.Length != colliders.Length)
                {
                    alreadyHad += parts.Length; // 手で付けた当たりがある
                    continue;
                }
                bool done = merge
                    ? ours.Length == 1 && IsMergedBox(ours[0], prop, merged)
                    : ours.Length == parts.Length && ours.All(IsPartBox);
                if (done)
                {
                    alreadyHad += parts.Length;
                    continue;
                }

                foreach (var c in ours)
                    Undo.DestroyObjectImmediate(c);
                removed += ours.Length;

                if (merge)
                {
                    AddBox(prop, merged, layer);
                    added++;
                }
                else
                {
                    foreach (var filter in parts)
                        AddBox(filter.gameObject, filter.sharedMesh.bounds, layer);
                    added += parts.Length;
                }
            }
            return new Result(added, removed, excluded, alreadyHad);
        }

        static void AddBox(GameObject go, Bounds bounds, int layer)
        {
            var box = Undo.AddComponent<BoxCollider>(go);
            box.center = bounds.center;
            box.size = bounds.size;

            Undo.RecordObject(go, "Set Obstacle Layer");
            go.layer = layer;
            // Prefab インスタンスの中は記録しないとシーン保存時に上書きが残らない
            if (PrefabUtility.IsPartOfPrefabInstance(go))
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        }

        /// <summary>メッシュの形どおりにこのツールが付けた箱か。</summary>
        static bool IsPartBox(Collider c) =>
            c is BoxCollider box
            && c.TryGetComponent<MeshFilter>(out var filter)
            && filter.sharedMesh != null
            && SameBox(box, filter.sharedMesh.bounds);

        /// <summary>小物全体を囲むようにこのツールが付けた箱か。</summary>
        static bool IsMergedBox(Collider c, GameObject prop, Bounds merged) =>
            c is BoxCollider box && c.gameObject == prop && SameBox(box, merged);

        static bool SameBox(BoxCollider box, Bounds b) =>
            (box.center - b.center).sqrMagnitude < 1e-6f
            && (box.size - b.size).sqrMagnitude < 1e-6f;

        /// <summary>メッシュ全部を囲む、小物の根から見た箱。</summary>
        static Bounds LocalBounds(Transform prop, IEnumerable<MeshFilter> parts)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var filter in parts)
            {
                var b = filter.sharedMesh.bounds;
                var toProp = prop.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (var i = 0; i < 8; i++)
                {
                    var corner =
                        b.center
                        + Vector3.Scale(
                            b.extents,
                            new Vector3(
                                (i & 1) == 0 ? -1 : 1,
                                (i & 2) == 0 ? -1 : 1,
                                (i & 4) == 0 ? -1 : 1
                            )
                        );
                    var p = toProp.MultiplyPoint3x4(corner);
                    if (first)
                        bounds = new Bounds(p, Vector3.zero);
                    else
                        bounds.Encapsulate(p);
                    first = false;
                }
            }
            return bounds;
        }

        /// <summary>小物の根の向きで見た水平の幅・奥行きが、まとめてよい大きさに収まるか(拡大縮小も込み)。</summary>
        static bool FitsInFootprint(Transform prop, Bounds local)
        {
            var width = prop.TransformVector(new Vector3(local.size.x, 0f, 0f)).magnitude;
            var depth = prop.TransformVector(new Vector3(0f, 0f, local.size.z)).magnitude;
            return width <= MergeMaxFootprint && depth <= MergeMaxFootprint;
        }

        /// <summary>
        /// メッシュが属する小物。一番外側の Prefab インスタンス、無ければメッシュだけを子に並べた親
        /// (配置ツールが生成した椅子など)、どちらでもなければそのメッシュ自身。
        /// </summary>
        static GameObject PropOf(GameObject go)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (root != null) // UnityEngine.Object は ?? だと破棄済みを見分けない
                return root;
            var parent = go.transform.parent;
            if (parent != null && IsMeshAssembly(parent))
                return parent.gameObject;
            return go;
        }

        // 自分は描画せず、子が2つ以上で、子が全部メッシュだけの末端
        static bool IsMeshAssembly(Transform t)
        {
            if (t.GetComponent<Renderer>() != null || t.childCount < 2)
                return false;
            foreach (Transform child in t)
                if (child.childCount > 0 || child.GetComponent<MeshFilter>() == null)
                    return false;
            return true;
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
                $"[PropCollider] {sceneName}: 当たりを {r.Added} 個付け、{r.Removed} 個外しました"
                    + $"(除外リスト {r.Excluded} / 当たり済み {r.AlreadyHad} は飛ばしました)。"
            );
    }
}
#endif
