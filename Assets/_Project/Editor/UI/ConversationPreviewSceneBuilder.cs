using System.IO;
using CreativeAI.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CreativeAI.EditorTools
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

            // 会話UIの Prefab は Canvas を持たないので、UIRoot の ConversationUI と同じ設定の Canvas の下に置く。
            var canvasObject = new GameObject(
                "ConversationUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var instance = (GameObject)
                PrefabUtility.InstantiatePrefab(prefab, canvasObject.transform);

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
