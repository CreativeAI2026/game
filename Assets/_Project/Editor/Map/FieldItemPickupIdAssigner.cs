using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// <see cref="FieldItemPickup"/> の識別子(拾い済みの記録に使う)を自動で振る。
    /// 置いたとき・複製したとき(hierarchyChanged)とシーン保存時に、未設定または重複しているものへ新しい識別子を振る。
    /// 識別子は <c>シーン名/GUID</c> なので、別エリアの <c>_Items</c> シーンとも重ならず、手入力は要らない。
    /// </summary>
    [InitializeOnLoad]
    public static class FieldItemPickupIdAssigner
    {
        private static bool _scheduled;

        static FieldItemPickupIdAssigner()
        {
            EditorApplication.hierarchyChanged += Schedule;
            EditorSceneManager.sceneSaving += (scene, _) => AssignInScene(scene);
        }

        private static void Schedule()
        {
            if (_scheduled || Application.isPlaying)
                return;
            _scheduled = true;
            // hierarchyChanged の最中に値を書き換えないよう、1拍おいてまとめて振る。
            EditorApplication.delayCall += () =>
            {
                _scheduled = false;
                if (Application.isPlaying)
                    return;
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    AssignInScene(SceneManager.GetSceneAt(i));
            };
        }

        /// <summary>シーン内の <see cref="FieldItemPickup"/> に重ならない識別子を振る。振った数を返す。</summary>
        public static int AssignInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return 0;

            var pickups = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FieldItemPickup>(true))
                .ToList();
            var assigned = Assign(pickups, scene.name);
            if (assigned > 0)
                EditorSceneManager.MarkSceneDirty(scene); // 振った識別子をシーンに保存させる
            return assigned;
        }

        /// <summary>
        /// 未設定または重複している識別子を振り直す(先に出てきたものを残す)。振った数を返す。
        /// <paramref name="used"/> に他シーンの識別子を渡せば、シーンをまたいで重ならないことも保証できる。
        /// </summary>
        public static int Assign(
            IReadOnlyList<FieldItemPickup> pickups,
            string scenePrefix,
            ISet<string> used = null
        )
        {
            used ??= new HashSet<string>();
            var assigned = 0;
            foreach (var pickup in pickups)
            {
                if (pickup == null)
                    continue;
                if (!string.IsNullOrEmpty(pickup.PickupId) && used.Add(pickup.PickupId))
                    continue;

                string id;
                do
                    id = $"{scenePrefix}/{Guid.NewGuid():N}";
                while (!used.Add(id));

                var so = new SerializedObject(pickup);
                so.FindProperty("_pickupId").stringValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
                assigned++;
            }
            return assigned;
        }
    }
}
