#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    public sealed class LectureHallSeatingLayoutWindow : EditorWindow
    {
        const string TargetScenePath = "Assets/_Project/Scenes/Field/Field_Area01_Props_3F.unity";
        const string LayoutRootName = "Props_LectureHall";
        const string LayoutConfigName = "LayoutConfig";
        const string GeneratedRootName = "Generated";
        const string GeneratedMarkerName = "__GeneratedByLectureHallSeatingLayoutWindow";
        const string AdditionalPropsRootName = "AdditionalProps";
        const string AdditionalPropsMarkerName = "__GeneratedByLectureHallAdditionalPropsWindow";

        [SerializeField]
        Transform _cornerRoot;

        [SerializeField]
        GameObject _referenceRow;

        [SerializeField]
        GameObject _teacherSetReference;

        [SerializeField]
        float _teacherPositionOffsetX;

        [SerializeField]
        float _teacherPositionOffsetZ = 2f;

        [SerializeField]
        Vector3 _teacherRotationOffset;

        [SerializeField]
        float _teacherFloorYOffset;

        [SerializeField]
        GameObject _clockReference;

        [SerializeField]
        float _clockWallOffset = 0.15f;

        [SerializeField]
        float _clockHorizontalOffset;

        [SerializeField]
        float _clockHeight = 2.5f;

        [SerializeField]
        Vector3 _clockRotationOffset;

        [SerializeField]
        float _frontMargin = 1.5f;

        [SerializeField]
        float _backMargin = 1.5f;

        [SerializeField]
        float _rowGap = 0.5f;

        [SerializeField]
        float _mainAisleWidth = 2f;

        [SerializeField]
        int _mainAisleAfterRow = 8;

        [SerializeField]
        float _floorYOffset;

        [NonSerialized]
        Transform _frontLeft;

        [NonSerialized]
        Transform _frontRight;

        [NonSerialized]
        Transform _backLeft;

        [NonSerialized]
        Transform _backRight;

        Vector2 _scroll;

        [MenuItem("Tools/CreativeAI/Map/大講義室/学生席レイアウト")]
        public static void Open()
        {
            var window = GetWindow<LectureHallSeatingLayoutWindow>("大講義室 学生席");
            window.minSize = new Vector2(480f, 600f);
            window.Show();
        }

        void OnEnable()
        {
            ResolveCorners();
            SceneView.duringSceneGui -= DrawRoomBoundary;
            SceneView.duringSceneGui += DrawRoomBoundary;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= DrawRoomBoundary;
        }

        void OnGUI()
        {
            ResolveCorners();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("大講義室 Reference Row配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "完成済みReference Rowを読み取り専用テンプレートとして複製し、前後方向だけ自動配置します。"
                    + "横位置・回転・Scale・子Transformは変更しません。シーンは自動保存しません。",
                MessageType.Info
            );

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            _cornerRoot = (Transform)
                EditorGUILayout.ObjectField("Corner Root", _cornerRoot, typeof(Transform), true);
            _referenceRow = (GameObject)
                EditorGUILayout.ObjectField(
                    "Reference Row",
                    _referenceRow,
                    typeof(GameObject),
                    true
                );
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Resolved Corners", HasAllCorners() ? "OK" : "Missing");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Margins", EditorStyles.boldLabel);
            _frontMargin = NonNegative("Front Margin", _frontMargin);
            _backMargin = NonNegative("Back Margin", _backMargin);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spacing", EditorStyles.boldLabel);
            _rowGap = NonNegative("Row Gap", _rowGap);
            _mainAisleWidth = NonNegative("Main Aisle Width", _mainAisleWidth);
            _mainAisleAfterRow = Mathf.Max(
                0,
                EditorGUILayout.IntField("Main Aisle After Row", _mainAisleAfterRow)
            );

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Height", EditorStyles.boldLabel);
            _floorYOffset = EditorGUILayout.FloatField("Floor Y Offset", _floorYOffset);

            EditorGUILayout.Space();
            DrawPreview();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Additional Props", EditorStyles.boldLabel);
            _teacherSetReference = (GameObject)
                EditorGUILayout.ObjectField(
                    "Teacher Set Reference",
                    _teacherSetReference,
                    typeof(GameObject),
                    true
                );
            _teacherPositionOffsetX = EditorGUILayout.FloatField(
                "Teacher Position Offset X",
                _teacherPositionOffsetX
            );
            _teacherPositionOffsetZ = EditorGUILayout.FloatField(
                "Teacher Position Offset Z",
                _teacherPositionOffsetZ
            );
            _teacherRotationOffset = EditorGUILayout.Vector3Field(
                "Teacher Rotation Offset",
                _teacherRotationOffset
            );
            _teacherFloorYOffset = EditorGUILayout.FloatField(
                "Teacher Floor Y Offset",
                _teacherFloorYOffset
            );

            EditorGUILayout.Space();
            _clockReference = (GameObject)
                EditorGUILayout.ObjectField(
                    "Clock Reference",
                    _clockReference,
                    typeof(GameObject),
                    true
                );
            _clockWallOffset = NonNegative("Clock Wall Offset", _clockWallOffset);
            _clockHorizontalOffset = EditorGUILayout.FloatField(
                "Clock Horizontal Offset",
                _clockHorizontalOffset
            );
            _clockHeight = EditorGUILayout.FloatField("Clock Height", _clockHeight);
            _clockRotationOffset = EditorGUILayout.Vector3Field(
                "Clock Rotation Offset",
                _clockRotationOffset
            );

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Adopt Existing Generated"))
                    AdoptExistingGenerated();
                if (GUILayout.Button("Generate / Regenerate", GUILayout.Height(38f)))
                    GenerateOrRegenerate();
                if (
                    GUILayout.Button(
                        "Generate / Regenerate Additional Props",
                        GUILayout.Height(38f)
                    )
                )
                    GenerateOrRegenerateAdditionalProps();
            }

            EditorGUILayout.HelpBox(
                "再生成時に削除するのはProps_LectureHall/Generatedだけです。"
                    + "LayoutConfig / Corner / Reference Rowは保持します。",
                MessageType.Warning
            );
            EditorGUILayout.EndScrollView();
        }

        static float NonNegative(string label, float value)
        {
            return Mathf.Max(0f, EditorGUILayout.FloatField(label, value));
        }

        void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            if (!TryCalculateLayout(out var layout, out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Room Depth", layout.Room.Depth);
                EditorGUILayout.FloatField("Available Depth", layout.AvailableDepth);
                EditorGUILayout.FloatField("Reference Row Depth", layout.RowDepth);
                EditorGUILayout.IntField("Calculated Row Count", layout.RowCount);
                EditorGUILayout.Vector3Field("Room Forward", layout.Room.Forward);
                EditorGUILayout.FloatField("Floor Y", layout.Room.FloorY + _floorYOffset);
                EditorGUILayout.Toggle("Main Aisle Generated", layout.HasMainAisle);
            }
            if (_mainAisleAfterRow > 0 && !layout.HasMainAisle)
                EditorGUILayout.HelpBox(
                    "Main Aisle After RowがRow Count以上のため、Main Aisleは生成されません。",
                    MessageType.Info
                );
        }

        void GenerateOrRegenerate()
        {
            if (
                !TryValidate(
                    out var scene,
                    out var layoutRoot,
                    out var existing,
                    out var layout,
                    out var error
                )
            )
            {
                EditorUtility.DisplayDialog("大講義室 学生席", error, "OK");
                return;
            }

            if (
                existing != null
                && !EditorUtility.DisplayDialog(
                    "大講義室 学生席",
                    "既存のProps_LectureHall/Generatedだけを削除して再生成します。",
                    "再生成",
                    "キャンセル"
                )
            )
                return;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("大講義室のReference Rowを再生成");
            var previousScene = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                if (existing != null)
                    Undo.DestroyObjectImmediate(existing);

                var generated = CreateChild(GeneratedRootName, layoutRoot, scene);
                CreateChild(GeneratedMarkerName, generated.transform, scene).hideFlags =
                    HideFlags.HideInHierarchy;

                for (var rowIndex = 0; rowIndex < layout.RowCount; rowIndex++)
                {
                    var row = Instantiate(_referenceRow);
                    row.name = $"Row_{rowIndex + 1:00}";
                    SceneManager.MoveGameObjectToScene(row, scene);
                    Undo.RegisterCreatedObjectUndo(row, $"{row.name}を生成");
                    Undo.SetTransformParent(
                        row.transform,
                        generated.transform,
                        $"{row.name}を配置"
                    );

                    // Instantiate直後のworld poseと全localScaleを維持する。
                    row.transform.SetPositionAndRotation(
                        _referenceRow.transform.position,
                        _referenceRow.transform.rotation
                    );
                    row.transform.localScale = _referenceRow.transform.localScale;

                    if (!TryCalculateProjectedBounds(row, layout.Room, out var rowBounds))
                        throw new InvalidOperationException(
                            $"{row.name}のRenderer Boundsを取得できません。"
                        );
                    var targetFront =
                        layout.Room.Front
                        - _frontMargin
                        - CalculateRowOffset(rowIndex, layout.RowDepth, layout.HasMainAisle);
                    row.transform.position +=
                        layout.Room.Forward * (targetFront - rowBounds.MaxForward);
                    GroundRow(row, layout.Room.FloorY + _floorYOffset);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = generated;
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log(
                    $"[LectureHallSeatingLayout] Reference Rowを{layout.RowCount}行生成しました。"
                        + "横方向のTransformは変更していません。シーンは自動保存していません。"
                );
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "大講義室 学生席",
                    "生成中にエラーが発生したため、変更をUndoしました。\n" + exception.Message,
                    "OK"
                );
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
            }
        }

        void GenerateOrRegenerateAdditionalProps()
        {
            if (
                !TryValidateAdditionalProps(
                    out var scene,
                    out var layoutRoot,
                    out var existing,
                    out var room,
                    out var error
                )
            )
            {
                EditorUtility.DisplayDialog("大講義室 Additional Props", error, "OK");
                return;
            }

            if (
                existing != null
                && !EditorUtility.DisplayDialog(
                    "大講義室 Additional Props",
                    "既存のProps_LectureHall/AdditionalPropsだけを削除して再生成します。",
                    "再生成",
                    "キャンセル"
                )
            )
                return;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("大講義室の教師セットと時計を再生成");
            var previousScene = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                if (existing != null)
                    Undo.DestroyObjectImmediate(existing);

                var additional = CreateChild(AdditionalPropsRootName, layoutRoot, scene);
                CreateChild(AdditionalPropsMarkerName, additional.transform, scene).hideFlags =
                    HideFlags.HideInHierarchy;
                var teacherArea = CreateChild("TeacherArea", additional.transform, scene);
                var fixtures = CreateChild("Fixtures", additional.transform, scene);

                var teacher = CloneReference(
                    _teacherSetReference,
                    "TeacherSet",
                    teacherArea.transform,
                    scene
                );
                var teacherRotation =
                    Quaternion.LookRotation(-room.Forward, Vector3.up)
                    * Quaternion.Euler(_teacherRotationOffset);
                var teacherPosition =
                    room.Right * (room.CenterRight + _teacherPositionOffsetX)
                    + room.Forward * (room.Front - _teacherPositionOffsetZ)
                    + Vector3.up * (room.FloorY + _teacherFloorYOffset);
                teacher.transform.SetPositionAndRotation(teacherPosition, teacherRotation);
                GroundRow(teacher, room.FloorY + _teacherFloorYOffset);

                var clock = CloneReference(_clockReference, "Clock", fixtures.transform, scene);
                var clockRotation =
                    Quaternion.LookRotation(-room.Forward, Vector3.up)
                    * Quaternion.Euler(_clockRotationOffset);
                var clockPosition =
                    room.Right * (room.CenterRight + _clockHorizontalOffset)
                    + room.Forward * (room.Front - _clockWallOffset)
                    + Vector3.up * (room.FloorY + _clockHeight);
                clock.transform.SetPositionAndRotation(clockPosition, clockRotation);

                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = additional;
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log(
                    "[LectureHallAdditionalProps] TeacherSetとClockを生成しました。"
                        + "学生席Generatedは変更していません。シーンは自動保存していません。"
                );
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "大講義室 Additional Props",
                    "生成中にエラーが発生したため、変更をUndoしました。\n" + exception.Message,
                    "OK"
                );
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
            }
        }

        bool TryValidateAdditionalProps(
            out Scene scene,
            out Transform layoutRoot,
            out GameObject existing,
            out Room room,
            out string error
        )
        {
            scene = FindLoadedScene(TargetScenePath);
            layoutRoot = null;
            existing = null;
            room = default;
            error = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "Field_Area01_Props_3F.unityを開いてください。";
                return false;
            }
            if (!TryCalculateRoom(out room, out error))
                return false;
            if (!IsValidAdditionalReference(_teacherSetReference, "Teacher Set", out error))
                return false;
            if (!IsValidAdditionalReference(_clockReference, "Clock", out error))
                return false;

            layoutRoot = FindAncestor(_cornerRoot, LayoutRootName);
            var layoutConfig = FindDirectChild(layoutRoot, LayoutConfigName);
            if (layoutRoot == null || layoutConfig == null || !_cornerRoot.IsChildOf(layoutConfig))
            {
                error = "Corner RootはProps_LectureHall/LayoutConfig配下に配置してください。";
                return false;
            }

            existing = FindDirectChild(layoutRoot, AdditionalPropsRootName)?.gameObject;
            if (
                existing != null
                && FindDirectChild(existing.transform, AdditionalPropsMarkerName) == null
            )
            {
                error = "既存AdditionalPropsに管理マーカーがないため変更できません。";
                return false;
            }
            if (
                existing != null
                && (
                    _teacherSetReference == existing
                    || _teacherSetReference.transform.IsChildOf(existing.transform)
                    || _clockReference == existing
                    || _clockReference.transform.IsChildOf(existing.transform)
                )
            )
            {
                error = "Referenceは再生成対象のAdditionalProps外から指定してください。";
                return false;
            }
            return true;
        }

        static bool IsValidAdditionalReference(GameObject reference, string label, out string error)
        {
            error = string.Empty;
            if (reference == null || EditorUtility.IsPersistent(reference))
            {
                error = $"Field_Area01_Props_3F.unity内の{label} Referenceを指定してください。";
                return false;
            }
            if (reference.scene.path != TargetScenePath)
            {
                error = $"{label} ReferenceはField_Area01_Props_3F.unity内から指定してください。";
                return false;
            }
            if (reference.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                error = $"{label} ReferenceにRendererがありません。";
                return false;
            }
            return true;
        }

        static GameObject CloneReference(
            GameObject reference,
            string name,
            Transform parent,
            Scene scene
        )
        {
            var clone = Instantiate(reference);
            clone.name = name;
            SceneManager.MoveGameObjectToScene(clone, scene);
            Undo.RegisterCreatedObjectUndo(clone, $"{name}を生成");
            Undo.SetTransformParent(clone.transform, parent, $"{name}を配置");
            clone.transform.localScale = reference.transform.localScale;
            return clone;
        }

        float CalculateRowOffset(int rowIndex, float rowDepth, bool hasMainAisle)
        {
            var offset = rowIndex * (rowDepth + _rowGap);
            if (hasMainAisle && rowIndex >= _mainAisleAfterRow)
                offset += _mainAisleWidth - _rowGap;
            return offset;
        }

        static void GroundRow(GameObject row, float floorY)
        {
            if (!TryCalculateWorldRendererBounds(row, out var bounds))
                throw new InvalidOperationException($"{row.name}の床接地Boundsを取得できません。");
            row.transform.position += Vector3.up * (floorY - bounds.min.y);
        }

        bool TryCalculateLayout(out Layout layout, out string error)
        {
            layout = default;
            if (!TryCalculateRoom(out var room, out error))
                return false;
            if (!TryValidateReferenceRow(out error))
                return false;
            if (!TryCalculateProjectedBounds(_referenceRow, room, out var rowBounds))
            {
                error = "Reference RowのRenderer Boundsを取得できません。";
                return false;
            }

            var availableDepth = room.Depth - _frontMargin - _backMargin;
            if (availableDepth <= 0f)
            {
                error = "Front / Back Marginを除いた配置可能な奥行きがありません。";
                return false;
            }
            var rowDepth = rowBounds.Depth;
            if (rowDepth <= 0.0001f)
            {
                error = "Reference RowのRoom Forward方向Footprintが不正です。";
                return false;
            }

            var rowCount = CalculateMaximumRows(availableDepth, rowDepth);
            if (rowCount < 1)
            {
                error = "現在のReference RowとMarginでは1行も配置できません。";
                return false;
            }
            layout = new Layout
            {
                Room = room,
                RowDepth = rowDepth,
                RowCount = rowCount,
                AvailableDepth = availableDepth,
                HasMainAisle = _mainAisleAfterRow > 0 && _mainAisleAfterRow < rowCount,
            };
            return true;
        }

        int CalculateMaximumRows(float availableDepth, float rowDepth)
        {
            var rowCount = 0;
            while (rowCount < 1000)
            {
                var candidate = rowCount + 1;
                var usedDepth = candidate * rowDepth + Mathf.Max(0, candidate - 1) * _rowGap;
                if (_mainAisleAfterRow > 0 && candidate > _mainAisleAfterRow)
                    usedDepth += _mainAisleWidth - _rowGap;
                if (usedDepth > availableDepth + 0.0001f)
                    break;
                rowCount = candidate;
            }
            return rowCount;
        }

        bool TryCalculateRoom(out Room room, out string error)
        {
            room = default;
            error = string.Empty;
            ResolveCorners();
            if (!HasAllCorners())
            {
                error =
                    "Corner Root直下にFrontLeft / FrontRight / BackLeft / BackRightが必要です。";
                return false;
            }

            var frontCenter = (_frontLeft.position + _frontRight.position) * 0.5f;
            var backCenter = (_backLeft.position + _backRight.position) * 0.5f;
            var right = Vector3
                .ProjectOnPlane(
                    (_frontRight.position - _frontLeft.position)
                        + (_backRight.position - _backLeft.position),
                    Vector3.up
                )
                .normalized;
            var forward = Vector3.ProjectOnPlane(frontCenter - backCenter, Vector3.up);
            forward -= Vector3.Dot(forward, right) * right;
            forward.Normalize();
            if (right.sqrMagnitude < 0.99f || forward.sqrMagnitude < 0.99f)
            {
                error = "CornerからRoom Right / Forwardを計算できません。";
                return false;
            }

            var up = Vector3.Cross(forward, right).normalized;
            if (Vector3.Dot(up, Vector3.up) < 0.5f)
            {
                error = "CornerのLeft/RightまたはFront/Backの並びを確認してください。";
                return false;
            }

            var front = AverageProjection(_frontLeft.position, _frontRight.position, forward);
            var back = AverageProjection(_backLeft.position, _backRight.position, forward);
            if (front <= back)
            {
                error = "Cornerから有効なRoom Depthを計算できません。";
                return false;
            }

            room = new Room
            {
                Right = right,
                Forward = forward,
                Front = front,
                Back = back,
                CenterRight = Vector3.Dot((frontCenter + backCenter) * 0.5f, right),
                Depth = Vector3.Distance(frontCenter, backCenter),
                FloorY =
                    (
                        _frontLeft.position.y
                        + _frontRight.position.y
                        + _backLeft.position.y
                        + _backRight.position.y
                    ) * 0.25f,
            };
            return true;
        }

        bool TryValidateReferenceRow(out string error)
        {
            error = string.Empty;
            if (_referenceRow == null || EditorUtility.IsPersistent(_referenceRow))
            {
                error = "Field_Area01_Props_3F.unity内のReference Rowを指定してください。";
                return false;
            }
            if (_referenceRow.scene.path != TargetScenePath)
            {
                error = "Reference RowはField_Area01_Props_3F.unity内から指定してください。";
                return false;
            }
            if (_referenceRow.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                error = "Reference RowにRendererがありません。";
                return false;
            }
            return true;
        }

        bool TryValidate(
            out Scene scene,
            out Transform layoutRoot,
            out GameObject existing,
            out Layout layout,
            out string error
        )
        {
            scene = FindLoadedScene(TargetScenePath);
            layoutRoot = null;
            existing = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                layout = default;
                error = "Field_Area01_Props_3F.unityを開いてください。";
                return false;
            }
            if (!TryCalculateLayout(out layout, out error))
                return false;
            if (_cornerRoot.gameObject.scene.path != TargetScenePath)
            {
                error = "Corner RootはField_Area01_Props_3F.unity内に配置してください。";
                return false;
            }

            layoutRoot = FindAncestor(_cornerRoot, LayoutRootName);
            var layoutConfig = FindDirectChild(layoutRoot, LayoutConfigName);
            if (layoutRoot == null || layoutConfig == null || !_cornerRoot.IsChildOf(layoutConfig))
            {
                error = "Corner RootはProps_LectureHall/LayoutConfig配下に配置してください。";
                return false;
            }
            if (
                _referenceRow.transform != layoutConfig
                && !_referenceRow.transform.IsChildOf(layoutConfig)
            )
            {
                error = "Reference RowはProps_LectureHall/LayoutConfig配下に配置してください。";
                return false;
            }

            existing = FindDirectChild(layoutRoot, GeneratedRootName)?.gameObject;
            if (
                existing != null
                && FindDirectChild(existing.transform, GeneratedMarkerName) == null
            )
            {
                error =
                    "既存Generatedに管理マーカーがありません。先にAdopt Existing Generatedを実行してください。";
                return false;
            }
            return true;
        }

        void AdoptExistingGenerated()
        {
            var scene = FindLoadedScene(TargetScenePath);
            var layoutRoot = FindAncestor(_cornerRoot, LayoutRootName);
            var generated = FindDirectChild(layoutRoot, GeneratedRootName);
            if (!scene.IsValid() || layoutRoot == null || generated == null)
            {
                EditorUtility.DisplayDialog(
                    "Adopt Existing Generated",
                    "安全なGeneratedを確認できません。",
                    "OK"
                );
                return;
            }
            if (generated.parent != layoutRoot || generated.name != GeneratedRootName)
            {
                EditorUtility.DisplayDialog(
                    "Adopt Existing Generated",
                    "対象Hierarchyが不正です。",
                    "OK"
                );
                return;
            }
            if (
                _cornerRoot == generated
                || _cornerRoot.IsChildOf(generated)
                || (
                    _referenceRow != null
                    && (
                        _referenceRow.transform == generated
                        || _referenceRow.transform.IsChildOf(generated)
                    )
                )
            )
            {
                EditorUtility.DisplayDialog(
                    "Adopt Existing Generated",
                    "Generated内にCornerまたはReference RowがあるためAdoptできません。",
                    "OK"
                );
                return;
            }
            if (FindDirectChild(generated, GeneratedMarkerName) != null)
            {
                EditorUtility.DisplayDialog("Adopt Existing Generated", "既に管理対象です。", "OK");
                return;
            }

            var looksGenerated = generated
                .Cast<Transform>()
                .Any(child =>
                    child.name.StartsWith("Row_", StringComparison.Ordinal)
                    || child.name == "StudentArea"
                    || child.name == "Aisles"
                );
            if (!looksGenerated)
            {
                EditorUtility.DisplayDialog(
                    "Adopt Existing Generated",
                    "旧生成物らしいRow / StudentArea構造を確認できません。",
                    "OK"
                );
                return;
            }
            if (
                !EditorUtility.DisplayDialog(
                    "Adopt Existing Generated",
                    "このProps_LectureHall/Generatedを管理対象にします。",
                    "Adopt",
                    "キャンセル"
                )
            )
                return;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("大講義室の既存Generatedを管理対象にする");
            CreateChild(GeneratedMarkerName, generated, scene).hideFlags =
                HideFlags.HideInHierarchy;
            Undo.CollapseUndoOperations(undoGroup);
        }

        static bool TryCalculateProjectedBounds(
            GameObject root,
            Room room,
            out ProjectedBounds bounds
        )
        {
            bounds = default;
            var initialized = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var corner in GetCorners(renderer.localBounds))
                {
                    var world = renderer.transform.localToWorldMatrix.MultiplyPoint3x4(corner);
                    var right = Vector3.Dot(world, room.Right);
                    var forward = Vector3.Dot(world, room.Forward);
                    if (!initialized)
                    {
                        bounds = new ProjectedBounds
                        {
                            MinRight = right,
                            MaxRight = right,
                            MinForward = forward,
                            MaxForward = forward,
                        };
                        initialized = true;
                    }
                    else
                    {
                        bounds.MinRight = Mathf.Min(bounds.MinRight, right);
                        bounds.MaxRight = Mathf.Max(bounds.MaxRight, right);
                        bounds.MinForward = Mathf.Min(bounds.MinForward, forward);
                        bounds.MaxForward = Mathf.Max(bounds.MaxForward, forward);
                    }
                }
            }
            return initialized;
        }

        static bool TryCalculateWorldRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            var initialized = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }
            return initialized;
        }

        void ResolveCorners()
        {
            _frontLeft = FindDirectChild(_cornerRoot, "FrontLeft");
            _frontRight = FindDirectChild(_cornerRoot, "FrontRight");
            _backLeft = FindDirectChild(_cornerRoot, "BackLeft");
            _backRight = FindDirectChild(_cornerRoot, "BackRight");
        }

        bool HasAllCorners()
        {
            return _frontLeft != null
                && _frontRight != null
                && _backLeft != null
                && _backRight != null;
        }

        void DrawRoomBoundary(SceneView sceneView)
        {
            if (!HasAllCorners())
                return;
            Handles.color = new Color(0.15f, 0.9f, 1f, 1f);
            Handles.DrawAAPolyLine(
                4f,
                _frontLeft.position,
                _frontRight.position,
                _backRight.position,
                _backLeft.position,
                _frontLeft.position
            );
        }

        static float AverageProjection(Vector3 a, Vector3 b, Vector3 axis)
        {
            return (Vector3.Dot(a, axis) + Vector3.Dot(b, axis)) * 0.5f;
        }

        static Transform FindAncestor(Transform start, string name)
        {
            var current = start;
            while (current != null)
            {
                if (current.name == name)
                    return current;
                current = current.parent;
            }
            return null;
        }

        static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
                return null;
            foreach (Transform child in parent)
                if (child.name == name)
                    return child;
            return null;
        }

        static GameObject CreateChild(string name, Transform parent, Scene scene)
        {
            var child = new GameObject(name);
            SceneManager.MoveGameObjectToScene(child, scene);
            Undo.RegisterCreatedObjectUndo(child, $"{name}を生成");
            Undo.SetTransformParent(child.transform, parent, $"{name}を配置");
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        static Scene FindLoadedScene(string path)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.path == path)
                    return scene;
            }
            return default;
        }

        static IEnumerable<Vector3> GetCorners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
                yield return new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z
                );
        }

        struct Room
        {
            public Vector3 Right;
            public Vector3 Forward;
            public float Front;
            public float Back;
            public float CenterRight;
            public float Depth;
            public float FloorY;
        }

        struct ProjectedBounds
        {
            public float MinRight;
            public float MaxRight;
            public float MinForward;
            public float MaxForward;
            public float Depth => MaxForward - MinForward;
        }

        struct Layout
        {
            public Room Room;
            public float RowDepth;
            public int RowCount;
            public float AvailableDepth;
            public bool HasMainAisle;
        }
    }
}
#endif
