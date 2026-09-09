#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 364教室の完成レイアウトをテンプレートとして、PC周辺機器を除いた365教室を生成する。
    /// 対象シーンを自動保存せず、コピー元のProps_364には一切変更を加えない。
    /// </summary>
    public sealed class Room365LayoutWindow : EditorWindow
    {
        const string TargetScenePath = "Assets/_Project/Scenes/Field/Field_Area01_Props_3F.unity";
        const string SourcePropsName = "Props_364";
        const string DestinationPropsName = "Props_365";
        const string StudentAreaName = "StudentArea";
        const string GeneratedMarkerName = "__GeneratedByRoom364DeskLayoutWindow";

        static readonly HashSet<string> StudentEquipmentNames = new(
            new[] { "Monitor_C", "Keyboard", "Mouse" },
            StringComparer.OrdinalIgnoreCase
        );

        [SerializeField]
        Transform _sourceProps;

        [SerializeField]
        Transform _room364Origin;

        [SerializeField]
        Transform _room365Origin;

        Vector2 _scroll;

        [MenuItem("Tools/CreativeAI/Map/365教室/364からレイアウト生成")]
        public static void Open()
        {
            var window = GetWindow<Room365LayoutWindow>("365 レイアウト");
            window.minSize = new Vector2(460f, 440f);
            window.Show();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("365教室レイアウト", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Props_364全体を複製し、364/365 Room Origin間の相対Transformを適用します。"
                    + "コピー側のStudentAreaにある生成マーカー付きDeskSetからのみ、"
                    + "Monitor_C / Keyboard / Mouseを除外します。シーンは自動保存しません。",
                MessageType.Info
            );

            EditorGUILayout.Space();
            _sourceProps = (Transform)
                EditorGUILayout.ObjectField(
                    new GUIContent("Source Props", "完成済みのProps_364を指定します。"),
                    _sourceProps,
                    typeof(Transform),
                    true
                );
            _room364Origin = (Transform)
                EditorGUILayout.ObjectField(
                    new GUIContent("364 Room Origin"),
                    _room364Origin,
                    typeof(Transform),
                    true
                );
            _room365Origin = (Transform)
                EditorGUILayout.ObjectField(
                    new GUIContent("365 Room Origin"),
                    _room365Origin,
                    typeof(Transform),
                    true
                );

            DrawOffsetPreview();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "生成先にProps_365が既に存在する場合は上書きしません。生成処理全体はCtrl+Z 1回で取り消せます。",
                MessageType.Warning
            );

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Generate Props_365", GUILayout.Height(36f)))
                    Generate();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawOffsetPreview()
        {
            if (_room364Origin == null || _room365Origin == null)
                return;

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field(
                    "Origin Position Delta",
                    _room365Origin.position - _room364Origin.position
                );
                EditorGUILayout.Vector3Field(
                    "Origin Rotation Delta",
                    (
                        _room365Origin.rotation * Quaternion.Inverse(_room364Origin.rotation)
                    ).eulerAngles
                );
            }
        }

        void Generate()
        {
            if (!TryValidate(out var targetScene, out var error))
            {
                EditorUtility.DisplayDialog("365 レイアウト", error, "OK");
                return;
            }

            var undoGroup = -1;
            var previousActiveScene = SceneManager.GetActiveScene();

            try
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("364教室から365教室のレイアウトを生成");

                SceneManager.SetActiveScene(targetScene);
                var destination = Instantiate(_sourceProps.gameObject);
                destination.name = DestinationPropsName;
                SceneManager.MoveGameObjectToScene(destination, targetScene);
                Undo.RegisterCreatedObjectUndo(destination, "Props_365を生成");

                if (_sourceProps.parent != null)
                    Undo.SetTransformParent(
                        destination.transform,
                        _sourceProps.parent,
                        "Props_365を3F小物階層へ移動"
                    );

                ApplyRoomRelativeTransform(destination.transform);
                RemoveStudentEquipment(destination.transform);

                Selection.activeGameObject = destination;
                EditorSceneManager.MarkSceneDirty(targetScene);
                Undo.CollapseUndoOperations(undoGroup);

                Debug.Log(
                    "[Room365Layout] Props_364を基準にProps_365を生成しました。"
                        + "学生机のPC周辺機器は除外済みです。シーンは自動保存していません。"
                );
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0)
                    Undo.RevertAllDownToGroup(undoGroup);

                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "365 レイアウト",
                    "生成中にエラーが発生したため、変更をUndoしました。\n" + exception.Message,
                    "OK"
                );
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
            }
        }

        void ApplyRoomRelativeTransform(Transform destination)
        {
            var relativePosition = _room364Origin.InverseTransformPoint(_sourceProps.position);
            var relativeRotation =
                Quaternion.Inverse(_room364Origin.rotation) * _sourceProps.rotation;

            Undo.RecordObject(destination, "Props_365の部屋基準Transformを適用");
            destination.SetPositionAndRotation(
                _room365Origin.TransformPoint(relativePosition),
                _room365Origin.rotation * relativeRotation
            );
            destination.localScale = _sourceProps.localScale;
        }

        static void RemoveStudentEquipment(Transform destinationProps)
        {
            var studentArea = FindDirectChild(destinationProps, StudentAreaName);
            if (studentArea == null)
                throw new InvalidOperationException(
                    "コピーしたProps_365内にStudentAreaがありません。"
                );

            var generatedDeskSetCount = 0;
            var equipmentToRemove = new List<GameObject>();
            foreach (Transform deskSet in studentArea)
            {
                if (FindDirectChild(deskSet, GeneratedMarkerName) == null)
                    continue;

                generatedDeskSetCount++;
                foreach (var child in deskSet.GetComponentsInChildren<Transform>(true))
                {
                    if (child == deskSet || !StudentEquipmentNames.Contains(child.name))
                        continue;

                    equipmentToRemove.Add(child.gameObject);
                }
            }

            if (generatedDeskSetCount == 0)
                throw new InvalidOperationException(
                    "StudentArea内に364ツールの生成マーカー付きDeskSetがありません。"
                        + "名前だけでは学生机を判定しないため、生成を中止しました。"
                );

            foreach (var equipment in equipmentToRemove)
                Undo.DestroyObjectImmediate(equipment);
        }

        bool TryValidate(out Scene targetScene, out string error)
        {
            targetScene = default;
            error = string.Empty;

            if (_sourceProps == null || _room364Origin == null || _room365Origin == null)
            {
                error = "Source Props、364 Room Origin、365 Room Originをすべて指定してください。";
                return false;
            }

            if (_sourceProps.name != SourcePropsName)
            {
                error = "Source PropsにはProps_364を指定してください。";
                return false;
            }

            targetScene = _sourceProps.gameObject.scene;
            if (
                !targetScene.IsValid()
                || !targetScene.isLoaded
                || targetScene.path != TargetScenePath
            )
            {
                error =
                    "Source Propsは、開いているField_Area01_Props_3F.unity内のProps_364に限定されます。";
                return false;
            }

            if (FindDirectChild(_sourceProps, StudentAreaName) == null)
            {
                error =
                    "Props_364の直下にStudentAreaがありません。先に364のHierarchyを整理してください。";
                return false;
            }

            if (FindRoot(targetScene, DestinationPropsName) != null)
            {
                error =
                    "Field_Area01_Props_3F.unity内にProps_365が既に存在します。上書きは行いません。";
                return false;
            }

            if (
                !_room364Origin.gameObject.scene.IsValid()
                || !_room364Origin.gameObject.scene.isLoaded
                || !_room365Origin.gameObject.scene.IsValid()
                || !_room365Origin.gameObject.scene.isLoaded
            )
            {
                error = "Room Originには、現在開いているシーン内のTransformを指定してください。";
                return false;
            }

            if (
                _room364Origin == _room365Origin
                || _room364Origin.IsChildOf(_sourceProps)
                || _room365Origin.IsChildOf(_sourceProps)
            )
            {
                error =
                    "Room Originは互いに別のTransformとし、Props_364の子は指定しないでください。";
                return false;
            }

            return true;
        }

        static Transform FindDirectChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;

                var descendant = FindDescendant(root.transform, name);
                if (
                    descendant != null
                    && descendant.parent != null
                    && descendant.parent.name == "Props_3F"
                )
                    return descendant.gameObject;
            }

            return null;
        }

        static Transform FindDescendant(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                var descendant = FindDescendant(child, name);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }
    }
}
#endif
