using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace CreativeAI.UI.ConversationUI.Editor
{
    /// <summary>Conversation UIの確認用Sceneを生成する。</summary>
    internal static class ConversationPreviewSceneBuilder
    {
        public static void Build(GameObject prefab, string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.10f, 0.13f, 1f);
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetAsLastSibling();

            var driverObject = new GameObject(
                "ConversationPreviewDriver",
                typeof(ConversationPreviewDriver)
            );
            var serializedDriver = new SerializedObject(
                driverObject.GetComponent<ConversationPreviewDriver>()
            );
            serializedDriver.FindProperty("_view").objectReferenceValue =
                instance.GetComponent<ConversationView>();
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EditorSceneManager.SaveScene(scene, scenePath);
        }
    }
}
