#if UNITY_EDITOR
using System.IO;
using CreativeAI.Core.SceneManagement;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 本番の PlayerRig が出来るまでの仮リグ(カプセル + カメラ + <see cref="DummyPlayerController"/>)を
    /// Prefab として作り、Field_Area01 を直接 Play したときに生成されるよう配線する。
    ///
    /// 生成経路は本番と同じ <c>GameStarter.EnsurePlayerRig</c> なので、本番リグが出来たら
    /// ResidentBootstrapConfig の Player Rig Prefab を差し替えるだけで乗り換えられる。
    /// Tools &gt; CreativeAI &gt; Player から実行。
    /// </summary>
    public static class DummyPlayerRigSetup
    {
        const string PrefabDir = "Assets/_Project/Features/Player/Prefabs";
        const string PrefabPath = PrefabDir + "/PlayerRig_Dummy.prefab";
        const string ConfigPath = "Assets/_Project/Resources/ResidentBootstrapConfig.asset";
        const string MaterialPath = "Assets/_Project/Art/Materials/Dev_DummyPlayer.mat";
        const string FieldScenePath = "Assets/_Project/Scenes/Field/Field_Area01.unity";

        // 1F の歩ける所(MapLayout.md 1F row 10 / col 55 = ワールド X=2, Z=10, 床 Y=-48)。
        static readonly Vector3 SpawnPosition = new Vector3(2f, -48f, 10f);
        const string SpawnId = "start";
        const string SpawnObjectName = "PlayerSpawn_start";

        // 当たり寸法。建物は階高 9.6u もあるので実寸(身長1.8u)だと世界に対して小さすぎ、
        // 扉(MapLayout.md の DoorScale = 2.5倍 / 開口 2.55 × 5.40u)とも釣り合わない。
        // そこで扉と同じ 2.5 倍に揃える: 4.5u なら開口高さの 83% で、人と実物の扉の比率に近い。
        // 半径も同じ倍率にする(高さだけ伸ばすと 16:1 の棒になるため)。1.4u 幅なので開口は通れる。
        // 本番の PlayerRig ができたら、当たり寸法はこちらに合わせること(階段の登れる/登れないが変わる)。
        const float BodyHeight = 4.5f;
        const float BodyRadius = 0.7f;

        // 視点と三人称カメラの距離も同じ倍率で伸ばす(画角の見え方を保つため)
        const float EyeHeight = 3.75f; // 1.5 × 2.5
        const float CameraDistance = 11.25f; // 4.5 × 2.5

        [MenuItem("Tools/CreativeAI/Player/Setup Dummy PlayerRig (Field_Area01)")]
        public static void Setup()
        {
            var prefab = CreatePrefab();
            AssignToConfig(prefab);
            PlaceBootstrap();
            Debug.Log(
                $"[DummyPlayerRigSetup] 完了。Field_Area01 を開いて Play すると仮プレイヤーが SpawnPoint "
                    + $"'{SpawnId}' {SpawnPosition} に出ます"
                    + "(WASD/矢印=移動, マウス=視点, Shift=ダッシュ, Space=ジャンプ, Esc=カーソル解放)。"
            );
        }

        // ---------------------------------------------------------------- Prefab

        static GameObject CreatePrefab()
        {
            var root = new GameObject("PlayerRig_Dummy") { tag = "Player" };

            var controller = root.AddComponent<CharacterController>();
            controller.height = BodyHeight;
            controller.radius = BodyRadius;
            controller.center = new Vector3(0f, BodyHeight * 0.5f + 0.03f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.25f;
            controller.skinWidth = 0.02f;
            controller.minMoveDistance = 0f;

            // EventTrigger の OnTriggerEnter を確実に飛ばすため(PlayerImplementation.md ①)
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            CreateBody(root.transform);
            var pivot = CreateCameraPivot(root.transform);

            var dummy = root.AddComponent<DummyPlayerController>();
            SetPrivate(dummy, "_cameraPivot", pivot);

            Directory.CreateDirectory(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[DummyPlayerRigSetup] {PrefabPath} を作成しました。");
            return prefab;
        }

        /// <summary>見た目のカプセル。当たりは CharacterController が持つのでコライダーは外す。</summary>
        static void CreateBody(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Body";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            // Capsule プリミティブは 高さ2u / 半径0.5u なので、当たりに合わせて潰す
            go.transform.localScale = new Vector3(
                BodyRadius * 2f,
                BodyHeight * 0.5f,
                BodyRadius * 2f
            );
            go.transform.localPosition = new Vector3(0f, BodyHeight * 0.5f + 0.03f, 0f);
            go.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial();
        }

        static Transform CreateCameraPivot(Transform parent)
        {
            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(0f, EyeHeight, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(pivot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -CameraDistance);

            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f; // マップは約440u四方あるので遠くまで映す
            camGo.AddComponent<AudioListener>();

            return pivot.transform;
        }

        static Material GetOrCreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null)
                return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(MaterialPath) };
            var color = new Color(0.85f, 0.45f, 0.2f);
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath)!);
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        // ---------------------------------------------------------------- 配線

        static void AssignToConfig(GameObject prefab)
        {
            var config = AssetDatabase.LoadAssetAtPath<ResidentBootstrapConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError(
                    $"[DummyPlayerRigSetup] {ConfigPath} がありません。"
                        + "Tools/CreativeAI/UI/Create Resident Bootstrap Config を先に実行してください。"
                );
                return;
            }

            config.playerRigPrefab = prefab;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[DummyPlayerRigSetup] ResidentBootstrapConfig.playerRigPrefab に割り当てました。"
            );
        }

        /// <summary>Field_Area01 に到着位置(SpawnPoint)と FieldDevBootstrap を1つずつ置く。</summary>
        static void PlaceBootstrap()
        {
            if (!File.Exists(FieldScenePath))
            {
                Debug.LogError($"[DummyPlayerRigSetup] {FieldScenePath} がありません。");
                return;
            }

            var scene = SceneManager.GetSceneByPath(FieldScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(FieldScenePath, OpenSceneMode.Single);

            var spawn = FindOrCreate<SpawnPoint>(scene, SpawnObjectName);
            spawn.transform.position = SpawnPosition;
            SetPrivate(spawn, "_id", SpawnId);

            var bootstrap = FindOrCreate<FieldDevBootstrap>(scene, "FieldDevBootstrap");
            bootstrap.transform.position = Vector3.zero; // 位置は使わない(出現位置は SpawnPoint 側)
            SetPrivate(bootstrap, "_spawnPlayerRig", true);
            SetPrivate(bootstrap, "_spawnPointId", SpawnId);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                $"[DummyPlayerRigSetup] Field_Area01 に SpawnPoint '{SpawnId}' を {SpawnPosition} に設置し、"
                    + "FieldDevBootstrap から参照するよう配線しました。"
            );
        }

        /// <summary>シーン直下から T を持つオブジェクトを探し、無ければ作る。</summary>
        static T FindOrCreate<T>(Scene scene, string name)
            where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponent<T>();
                if (found != null)
                    return found;
            }

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.AddComponent<T>();
        }

        static void SetPrivate(Object target, string field, Object value) =>
            SetPrivate(target, field, p => p.objectReferenceValue = value);

        static void SetPrivate(Object target, string field, string value) =>
            SetPrivate(target, field, p => p.stringValue = value);

        static void SetPrivate(Object target, string field, bool value) =>
            SetPrivate(target, field, p => p.boolValue = value);

        static void SetPrivate(
            Object target,
            string field,
            System.Action<SerializedProperty> assign
        )
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError(
                    $"[DummyPlayerRigSetup] {target.GetType().Name}.{field} が見つかりません。"
                );
                return;
            }
            assign(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
