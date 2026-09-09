#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    public sealed class RoomDeskFurnitureSettings : ScriptableObject
    {
        public const string DefaultAssetPath =
            "Assets/_Project/Settings/RoomDeskFurnitureSettings_364.asset";

        public GameObject deskPrefab;
        public GameObject chairPrefab;
        public GameObject workstationPrefab;
        public GameObject monitorSource;
        public GameObject keyboardSource;
        public GameObject mouseSource;
        public Vector3 deskScale = new(3f, 6f, 5f);
        public Vector3 chairScale = new(3.46f, 3.14f, 3.81f);
        public Vector3 monitorScale = Vector3.one * 3f;
        public Vector3 keyboardScale = Vector3.one * 3f;
        public Vector3 mouseScale = Vector3.one * 3f;
        public float seatHorizontalOffset = 0.9f;
        public Vector3 chairLocalPosition = new(0f, 0f, -1.2f);
        public Vector3 chairLocalEuler;
        public Vector3 monitorLocalPosition = new(0f, 0.8f, 0.15f);
        public Vector3 monitorLocalEuler;
        public Vector3 keyboardLocalPosition = new(0f, 0.78f, -0.2f);
        public Vector3 keyboardLocalEuler;
        public Vector3 mouseLocalPosition = new(0.35f, 0.78f, -0.2f);
        public Vector3 mouseLocalEuler;

        public static RoomDeskFurnitureSettings LoadDefault()
        {
            return AssetDatabase.LoadAssetAtPath<RoomDeskFurnitureSettings>(DefaultAssetPath);
        }

        public static RoomDeskFurnitureSettings GetOrCreateDefault()
        {
            var settings = LoadDefault();
            if (settings != null)
                return settings;

            settings = CreateInstance<RoomDeskFurnitureSettings>();
            AssetDatabase.CreateAsset(settings, DefaultAssetPath);
            return settings;
        }
    }
}
#endif
