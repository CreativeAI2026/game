#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// Field_Area01 のオクルージョンカリング(壁の向こうを描かない設定)を焼く。
    /// マップを作り直すと壁と食い違うので、MapLayoutBuilder の Build / Rebuild の最後にも呼ばれる。
    /// </summary>
    public static class AreaOcclusionBaker
    {
        // 1マス(4u)の壁片が遮蔽物として残るように、既定の 5 から下げる
        const float OcclusionSmallestOccluder = 4f;
        const float OcclusionSmallestHole = 0.25f;

        /// <summary>Field_Area01 のオクルージョンカリングを焼き直す。Build / Rebuild の最後にも自動で走る。</summary>
        [MenuItem("Tools/CreativeAI/Map/Bake Occlusion Field_Area01")]
        public static void BakeOcclusionArea01()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = MapLayoutBuilder.FindOpenScene(MapLayoutBuilder.MapScenePath);
            if (!scene.IsValid())
                scene = EditorSceneManager.OpenScene(
                    MapLayoutBuilder.MapScenePath,
                    OpenSceneMode.Single
                );
            Bake(scene);
        }

        /// <summary>
        /// そのシーンだけでオクルージョンカリングを焼いて保存する。小物は Static でないので焼く対象に要らず、
        /// 小物シーンを開いたまま焼くとそちらにも参照が書き込まれて担当者と競合するため、
        /// 他のシーンは一時的に閉じて焼き終わったら開き直す。
        /// </summary>
        public static bool Bake(Scene scene)
        {
            var others = new List<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s != scene && s.isLoaded)
                    others.Add(s.path);
            }
            if (
                others.Count > 0
                && !EditorSceneManager.SaveModifiedScenesIfUserWantsTo(
                    others.Select(p => SceneManager.GetSceneByPath(p)).ToArray()
                )
            )
            {
                Debug.LogWarning(
                    "[AreaOcclusionBaker] オクルージョンカリングを焼きませんでした。Bake Occlusion Field_Area01 で焼き直してください。"
                );
                return false;
            }
            foreach (var path in others)
                EditorSceneManager.CloseScene(SceneManager.GetSceneByPath(path), false);

            var ok = false;
            try
            {
                StaticOcclusionCulling.smallestOccluder = OcclusionSmallestOccluder;
                StaticOcclusionCulling.smallestHole = OcclusionSmallestHole;
                StaticOcclusionCulling.backfaceThreshold = 100f;
                ok = StaticOcclusionCulling.Compute();
                if (ok)
                {
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[AreaOcclusionBaker] オクルージョンカリングを焼きました。");
                }
                else
                    Debug.LogError(
                        "[AreaOcclusionBaker] オクルージョンカリングのベイクに失敗しました。"
                    );
            }
            finally
            {
                foreach (var path in others)
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            return ok;
        }
    }
}
#endif
