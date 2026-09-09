#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    public sealed class Room363LayoutWindow : EditorWindow
    {
        const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area01_Props_3F.unity";
        const string Marker = "__GeneratedByRoom363LayoutWindow";
        const int CurrentConfigurationVersion = 3;

        [SerializeField]
        Transform _propsRoot,
            _leftWall,
            _rightWall,
            _door;

        [SerializeField]
        Transform _referenceDeskSet;

        [SerializeField]
        float _leftOffset = 0.2f,
            _rightOffset = 0.2f;

        [SerializeField]
        float _minimumAisle = 1.2f,
            _deskGap;

        [SerializeField]
        float _pairDeskGap;

        [SerializeField]
        float _islandCenterGap,
            _islandColumnAisleWidth = 1.2f,
            _islandRowAisleWidth = 1.2f;

        [SerializeField]
        int _requestedIslandColumns = 2,
            _requestedIslandRows = 2;

        [SerializeField]
        float _frontWallOffset = 0.5f,
            _doorClearanceLeft = 1f,
            _doorClearanceRight = 1f;

        [SerializeField]
        float _sideWallStartOffsetFromFront = 1f,
            _sideWallEndClearanceFromBack = 0.6f,
            _islandFrontClearance = 1.2f,
            _islandBackClearance = 0.6f;

        [SerializeField]
        Transform _teacherSetReference,
            _printerReference,
            _clockReference,
            _room364Left,
            _room364Right;

        [SerializeField]
        float _manualRoomDepth = 12f;

        [SerializeField]
        float _floorYOffset;

        [SerializeField]
        int _configurationVersion;
        Vector2 _scroll;

        [MenuItem("Tools/CreativeAI/Map/363教室/小物レイアウト")]
        static void Open() => GetWindow<Room363LayoutWindow>("363 小物レイアウト").Show();

        void OnEnable()
        {
            if (_configurationVersion >= CurrentConfigurationVersion)
                return;
            _deskGap = 0f;
            _pairDeskGap = 0f;
            _islandCenterGap = 0f;
            _frontWallOffset = 0.5f;
            _islandColumnAisleWidth = 1.2f;
            _islandRowAisleWidth = 1.2f;
            _sideWallStartOffsetFromFront = 1f;
            _sideWallEndClearanceFromBack = 0.6f;
            _islandFrontClearance = 1.2f;
            _islandBackClearance = 0.6f;
            _requestedIslandColumns = 2;
            _requestedIslandRows = 2;
            _configurationVersion = CurrentConfigurationVersion;
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "左右壁のRenderer Bounds内側表面からRoom Widthを計算します。壁は変更しません。",
                MessageType.Info
            );
            _propsRoot = Field("Props Root", _propsRoot);
            _referenceDeskSet = Field("Reference DeskSet (364)", _referenceDeskSet);
            EditorGUILayout.LabelField("Room Boundaries", EditorStyles.boldLabel);
            _leftWall = Field("Left Boundary Wall", _leftWall);
            _rightWall = Field("Right Boundary Wall", _rightWall);
            _door = Field("Door Transform", _door);
            using (new EditorGUI.DisabledScope(true))
            {
                if (TryRoom(out var room, out _))
                {
                    EditorGUILayout.FloatField("Room Width", room.Width);
                    EditorGUILayout.FloatField("Room Depth", room.Depth);
                }
                else
                    EditorGUILayout.TextField("Room Width", "--");
            }
            _manualRoomDepth = Mathf.Max(
                0.1f,
                EditorGUILayout.FloatField("Manual Room Depth (Fallback)", _manualRoomDepth)
            );
            _floorYOffset = EditorGUILayout.FloatField("Floor Y Offset", _floorYOffset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wall", EditorStyles.boldLabel);
            _leftOffset = Positive("Left Wall Offset", _leftOffset);
            _rightOffset = Positive("Right Wall Offset", _rightOffset);
            _deskGap = Positive("Wall Desk Gap", _deskGap);
            _frontWallOffset = Positive("Front Wall Offset", _frontWallOffset);
            _sideWallStartOffsetFromFront = Positive(
                "Side Wall Start Offset From Front",
                _sideWallStartOffsetFromFront
            );
            _sideWallEndClearanceFromBack = Positive(
                "Side Wall End Clearance From Back",
                _sideWallEndClearanceFromBack
            );
            _doorClearanceLeft = Positive("Door Clearance Left", _doorClearanceLeft);
            _doorClearanceRight = Positive("Door Clearance Right", _doorClearanceRight);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Island", EditorStyles.boldLabel);
            _requestedIslandColumns = Mathf.Clamp(
                EditorGUILayout.IntField("Island Columns", _requestedIslandColumns),
                1,
                4
            );
            _requestedIslandRows = Mathf.Clamp(
                EditorGUILayout.IntField("Island Rows", _requestedIslandRows),
                1,
                Mathf.Max(1, 4 / _requestedIslandColumns)
            );
            _pairDeskGap = EditorGUILayout.FloatField("Pair Desk Gap", _pairDeskGap);
            _islandCenterGap = EditorGUILayout.FloatField("Island Center Gap", _islandCenterGap);
            _islandColumnAisleWidth = Positive(
                "Island Column Aisle Width",
                _islandColumnAisleWidth
            );
            _islandRowAisleWidth = Positive("Island Row Aisle Width", _islandRowAisleWidth);
            _minimumAisle = Mathf.Max(
                0.1f,
                Positive("Minimum Wall To Island Aisle", _minimumAisle)
            );
            _islandFrontClearance = Positive("Island Front Clearance", _islandFrontClearance);
            _islandBackClearance = Positive("Island Back Clearance", _islandBackClearance);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("364 Room Props References", EditorStyles.boldLabel);
            _teacherSetReference = Field("364 Teacher Set Reference", _teacherSetReference);
            _printerReference = Field("364 Printer Reference", _printerReference);
            _clockReference = Field("364 Clock Reference", _clockReference);
            _room364Left = Field("364 Left Boundary", _room364Left);
            _room364Right = Field("364 Right Boundary", _room364Right);
            if (TryLayout(out var layout, out var error))
                EditorGUILayout.HelpBox(
                    $"Wall: Left {layout.wallRows} / Right {layout.wallRows} / Front {layout.frontSeatCount}\n"
                        + $"Islands: {layout.islandColumns} x {layout.islandRows} (4 desks each)\n"
                        + $"Set Footprint: {layout.bounds.size.x:0.###} × {layout.bounds.size.z:0.###}m",
                    MessageType.None
                );
            else
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Generate / Regenerate 363", GUILayout.Height(36)))
                    Generate();
            EditorGUILayout.EndScrollView();
        }

        static Transform Field(string label, Transform value) =>
            (Transform)EditorGUILayout.ObjectField(label, value, typeof(Transform), true);

        static float Positive(string label, float value) =>
            Mathf.Max(0, EditorGUILayout.FloatField(label, value));

        void Generate()
        {
            if (!Validate(out var scene, out var old, out var layout, out var error))
            {
                EditorUtility.DisplayDialog("363 小物レイアウト", error, "OK");
                return;
            }
            if (
                old != null
                && !EditorUtility.DisplayDialog(
                    "363 小物レイアウト",
                    "Props_363/Generatedだけを削除して再生成します。",
                    "再生成",
                    "キャンセル"
                )
            )
                return;
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("363の机・椅子・PCを生成");
            try
            {
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);
                var root = Child("Generated", _propsRoot, scene);
                Child(Marker, root.transform, scene).hideFlags = HideFlags.HideInHierarchy;
                var left = Child("LeftWallSeats", root.transform, scene).transform;
                var right = Child("RightWallSeats", root.transform, scene).transform;
                var front = Child("FrontWallSeats", root.transform, scene).transform;
                var center = Child("CenterIslands", root.transform, scene).transform;
                var number = 1;
                for (var row = 0; row < layout.wallRows; row++)
                {
                    var f = layout.wallFirst + row * layout.wallPitch;
                    DeskSet(
                        number++,
                        WallPosition(layout, true, f),
                        Quaternion.LookRotation(-layout.room.right),
                        left,
                        scene,
                        layout.room
                    );
                    DeskSet(
                        number++,
                        WallPosition(layout, false, f),
                        Quaternion.LookRotation(layout.room.right),
                        right,
                        scene,
                        layout.room
                    );
                }
                foreach (var x in layout.frontSeatX)
                {
                    var p =
                        layout.room.right * x
                        + layout.room.forward * layout.frontPivotZ
                        + Vector3.up * layout.room.floorY;
                    DeskSet(number++, p, layout.room.rotation, front, scene, layout.room);
                }
                var islandNumber = 1;
                for (var islandRow = 0; islandRow < layout.islandRows; islandRow++)
                for (var islandColumn = 0; islandColumn < layout.islandColumns; islandColumn++)
                {
                    var islandRotation = layout.room.rotation;
                    var island = Child($"Island_{islandNumber++:00}", center, scene).transform;
                    island.position = Vector3.zero;
                    island.rotation = islandRotation;
                    island.localScale = Vector3.one;
                    var startX = layout.islandAreaLeft + islandColumn * layout.islandPitchX;
                    var originZ =
                        layout.islandAreaBack + islandRow * layout.islandPitchZ - layout.islandMin;
                    var complete = true;
                    for (var deskIndex = 0; deskIndex < 2; deskIndex++)
                    {
                        var x = startX + layout.firstDeskOffsetX + deskIndex * layout.deskPitchX;
                        foreach (var side in new[] { 0, 1 })
                        {
                            var z = originZ + (side == 0 ? layout.sideAOffset : layout.sideBOffset);
                            var p =
                                layout.room.right * x
                                + layout.room.forward * z
                                + Vector3.up * layout.room.floorY;
                            complete &= DeskSet(
                                number++,
                                p,
                                islandRotation,
                                island,
                                scene,
                                layout.room
                            );
                        }
                    }
                    if (complete)
                        complete = NormalizeAndValidateIsland(island, islandRotation);
                    if (!complete)
                        Undo.DestroyObjectImmediate(island.gameObject);
                }
                var hasRoomProps =
                    _teacherSetReference != null
                    || _printerReference != null
                    || _clockReference != null;
                if (hasRoomProps)
                {
                    if (
                        !TryCalculate364Room(
                            layout.room,
                            out var sourceRoom,
                            out var sourceRoomError
                        )
                    )
                        throw new InvalidOperationException(sourceRoomError);
                    var teacherArea = Child("TeacherArea", root.transform, scene).transform;
                    var equipmentArea = Child("Equipment", root.transform, scene).transform;
                    var fixturesArea = Child("Fixtures", root.transform, scene).transform;
                    CopyMappedReference(
                        _teacherSetReference,
                        "TeacherSet",
                        teacherArea,
                        sourceRoom,
                        layout.room,
                        false
                    );
                    CopyMappedReference(
                        _printerReference,
                        "Printer",
                        equipmentArea,
                        sourceRoom,
                        layout.room,
                        false
                    );
                    CopyMappedReference(
                        _clockReference,
                        "Clock",
                        fixturesArea,
                        sourceRoom,
                        layout.room,
                        true
                    );
                }
                var hasOutsideBounds = HasBoundsOutsideRoom(root.transform, layout.room);
                if (hasOutsideBounds)
                    Debug.LogWarning(
                        "[Room363Layout] 生成物の一部が363 Room Bounds外にあります。"
                            + "Teacher Set / Printer / Clockの参照境界を確認してください。"
                    );
                Selection.activeGameObject = root;
                Undo.CollapseUndoOperations(group);
                if (hasOutsideBounds)
                    EditorUtility.DisplayDialog(
                        "363 小物レイアウト",
                        "生成物の一部がRoom Bounds外にあります。"
                            + "Teacher Set / Printer / Clockと364境界参照を確認してください。",
                        "OK"
                    );
            }
            catch (Exception e)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(e);
            }
        }

        Vector3 WallPosition(Layout l, bool left, float forward)
        {
            var rotation = Quaternion.LookRotation(left ? -l.room.right : l.room.right);
            Projection(l.bounds, rotation, l.room.right, out var min, out var max);
            var x = left ? l.room.left + _leftOffset - min : l.room.rightEdge - _rightOffset - max;
            return l.room.right * x + l.room.forward * forward + Vector3.up * l.room.floorY;
        }

        List<float> CalculateFrontSeatPositions(
            Room room,
            float rangeLeft,
            float rangeRight,
            float boundsMin,
            float boundsMax,
            float deskMin,
            float deskMax
        )
        {
            var doorMin = Vector3.Dot(_door.position, room.right);
            var doorMax = doorMin;
            if (Points(_door, out var doorPoints))
            {
                doorMin = doorPoints.Min(point => Vector3.Dot(point, room.right));
                doorMax = doorPoints.Max(point => Vector3.Dot(point, room.right));
            }

            var result = new List<float>();
            PackFrontSegment(
                rangeLeft,
                Mathf.Min(rangeRight, doorMin - _doorClearanceLeft),
                boundsMin,
                boundsMax,
                deskMin,
                deskMax,
                result
            );
            PackFrontSegment(
                Mathf.Max(rangeLeft, doorMax + _doorClearanceRight),
                rangeRight,
                boundsMin,
                boundsMax,
                deskMin,
                deskMax,
                result
            );
            return result;
        }

        void PackFrontSegment(
            float start,
            float end,
            float boundsMin,
            float boundsMax,
            float deskMin,
            float deskMax,
            ICollection<float> result
        )
        {
            var width = boundsMax - boundsMin;
            var pitch = deskMax - deskMin + _deskGap;
            var length = end - start;
            var count = length < width ? 0 : Mathf.FloorToInt((length - width) / pitch) + 1;
            if (count < 1)
                return;
            var usedWidth = width + (count - 1) * pitch;
            var first = start + (length - usedWidth) * 0.5f - boundsMin;
            for (var index = 0; index < count; index++)
                result.Add(first + index * pitch);
        }

        bool Validate(out Scene scene, out Transform old, out Layout layout, out string error)
        {
            scene = _propsRoot != null ? _propsRoot.gameObject.scene : default;
            old = null;
            if (
                _propsRoot == null
                || _propsRoot.name != "Props_363"
                || scene.path != ScenePath
                || EditorUtility.IsPersistent(_propsRoot)
            )
            {
                layout = default;
                error = "Field_Area01_Props_3F.unity内のProps_363を指定してください。";
                return false;
            }
            if (!ValidateReference(out error))
            {
                layout = default;
                return false;
            }
            foreach (
                var source in new[] { _teacherSetReference, _printerReference, _clockReference }
            )
            {
                if (source == null)
                    continue;
                if (
                    source.gameObject.scene.path != ScenePath
                    || !IsUnderNamedAncestor(source, "Props_364")
                )
                {
                    layout = default;
                    error =
                        $"{source.name}はField_Area01_Props_3F.unity内のProps_364配下から指定してください。";
                    return false;
                }
            }
            if (!TryLayout(out layout, out error))
                return false;
            old = Direct(_propsRoot, "Generated");
            if (old != null && Direct(old, Marker) == null)
            {
                error = "既存Generatedはこのツールの生成物ではないため変更できません。";
                return false;
            }
            return true;
        }

        bool TryLayout(out Layout l, out string error)
        {
            l = default;
            if (
                !TryRoom(out var room, out error)
                || !TrySetBounds(out var bounds, out var deskBounds, out error)
            )
                return false;
            var sideDepth =
                room.Depth - _sideWallStartOffsetFromFront - _sideWallEndClearanceFromBack;
            var wallRotation = Quaternion.LookRotation(-room.right);
            Projection(deskBounds, wallRotation, room.forward, out var wMin, out var wMax);
            Projection(
                bounds,
                wallRotation,
                room.forward,
                out var wallFullMin,
                out var wallFullMax
            );
            var wallPitch = wMax - wMin + _deskGap;
            var wallFullLength = wallFullMax - wallFullMin;
            var wallRows =
                sideDepth < wallFullLength
                    ? 0
                    : Mathf.FloorToInt((sideDepth - wallFullLength) / wallPitch) + 1;
            var wallUsedLength = wallFullLength + Mathf.Max(0, wallRows - 1) * wallPitch;
            var wallFirst =
                room.back
                + _sideWallEndClearanceFromBack
                + (sideDepth - wallUsedLength) * 0.5f
                - wallFullMin;
            Projection(bounds, wallRotation, room.right, out var lMin, out var lMax);
            var leftMax = room.left + _leftOffset - lMin + lMax;
            var rightRotation = Quaternion.LookRotation(room.right);
            Projection(bounds, rightRotation, room.right, out var rMin, out var rMax);
            var rightMin = room.rightEdge - _rightOffset - rMax + rMin;
            var centerLeft = leftMax + _minimumAisle;
            var centerRight = rightMin - _minimumAisle;
            var centerWidth = centerRight - centerLeft;

            Projection(bounds, room.rotation, room.right, out var frontXMin, out var frontXMax);
            Projection(
                deskBounds,
                room.rotation,
                room.right,
                out var frontDeskXMin,
                out var frontDeskXMax
            );
            Projection(bounds, room.rotation, room.forward, out var frontZMin, out var frontZMax);
            var frontPivotZ = room.front - _frontWallOffset - frontZMax;
            var frontSeatX = CalculateFrontSeatPositions(
                room,
                centerLeft,
                centerRight,
                frontXMin,
                frontXMax,
                frontDeskXMin,
                frontDeskXMax
            );
            var islandBack = room.back + _islandBackClearance;
            var islandFront =
                frontSeatX.Count > 0
                    ? frontPivotZ + frontZMin - _minimumAisle
                    : room.front - _islandFrontClearance;
            islandFront = Mathf.Min(islandFront, room.front - _islandFrontClearance);
            var islandAreaDepth = islandFront - islandBack;

            Projection(deskBounds, room.rotation, room.right, out var deskXMin, out var deskXMax);
            var deskPitchX = deskXMax - deskXMin + _pairDeskGap;
            if (deskPitchX <= 0.001f)
            {
                error = "Island Desk Gap Xが小さすぎます。";
                return false;
            }
            Projection(bounds, room.rotation, room.right, out var fullXMin, out var fullXMax);
            var fullSetWidth = fullXMax - fullXMin;
            var islandWidth = fullSetWidth + deskPitchX;

            Projection(bounds, room.rotation, room.forward, out var fullAMin, out var fullAMax);
            var fullBMin = fullAMin;
            var fullBMax = fullAMax;
            var sideAOffset = _islandCenterGap * 0.5f - fullAMin;
            var sideBOffset = -_islandCenterGap * 0.5f - fullBMax;
            var islandMin = Mathf.Min(sideAOffset + fullAMin, sideBOffset + fullBMin);
            var islandMax = Mathf.Max(sideAOffset + fullAMax, sideBOffset + fullBMax);
            var islandDepth = islandMax - islandMin;
            var maximumIslandColumns = Mathf.FloorToInt(
                (centerWidth + _islandColumnAisleWidth) / (islandWidth + _islandColumnAisleWidth)
            );
            var maximumIslandRows = Mathf.FloorToInt(
                (islandAreaDepth + _islandRowAisleWidth) / (islandDepth + _islandRowAisleWidth)
            );
            var requestedColumns = Mathf.Clamp(_requestedIslandColumns, 1, 4);
            var requestedRows = Mathf.Clamp(
                _requestedIslandRows,
                1,
                Mathf.Max(1, 4 / requestedColumns)
            );
            var islandColumns = Mathf.Min(requestedColumns, maximumIslandColumns);
            var islandRows = Mathf.Min(requestedRows, maximumIslandRows);
            if (sideDepth <= 0 || wallRows < 1 || islandColumns < 1 || islandRows < 1)
            {
                error =
                    "最低通路幅を確保すると自動配置できません。OffsetまたはClearanceを見直してください。";
                return false;
            }
            var islandGroupWidth =
                islandColumns * islandWidth + (islandColumns - 1) * _islandColumnAisleWidth;
            var islandGroupDepth =
                islandRows * islandDepth + (islandRows - 1) * _islandRowAisleWidth;
            l = new Layout
            {
                room = room,
                bounds = bounds,
                wallFirst = wallFirst,
                wallRows = wallRows,
                wallPitch = wallPitch,
                frontSeatX = frontSeatX,
                frontPivotZ = frontPivotZ,
                islandAreaLeft = centerLeft + (centerWidth - islandGroupWidth) * 0.5f,
                islandAreaBack = islandBack + (islandAreaDepth - islandGroupDepth) * 0.5f,
                islandWidth = islandWidth,
                islandDepth = islandDepth,
                islandColumns = islandColumns,
                islandRows = islandRows,
                islandPitchX = islandWidth + _islandColumnAisleWidth,
                islandPitchZ = islandDepth + _islandRowAisleWidth,
                firstDeskOffsetX = -fullXMin,
                deskPitchX = deskPitchX,
                sideAOffset = sideAOffset,
                sideBOffset = sideBOffset,
                islandMin = islandMin,
            };
            return true;
        }

        bool TryRoom(out Room room, out string error)
        {
            room = default;
            error = "";
            if (_leftWall == null || _rightWall == null || _door == null)
            {
                error = "Left / Right Boundary WallとDoor Transformを指定してください。";
                return false;
            }
            if (!Points(_leftWall, out var lp) || !Points(_rightWall, out var rp))
            {
                error = "左右壁のRenderer Boundsを取得できません。";
                return false;
            }
            var lc = Average(lp);
            var rc = Average(rp);
            var right = Vector3.ProjectOnPlane(rc - lc, Vector3.up).normalized;
            var forward = Vector3.Cross(Vector3.up, right).normalized;
            var left = lp.Max(p => Vector3.Dot(p, right));
            var rightEdge = rp.Min(p => Vector3.Dot(p, right));
            var leftMin = lp.Min(p => Vector3.Dot(p, forward));
            var leftMax = lp.Max(p => Vector3.Dot(p, forward));
            var rightMin = rp.Min(p => Vector3.Dot(p, forward));
            var rightMax = rp.Max(p => Vector3.Dot(p, forward));
            var rangeMin = Mathf.Max(leftMin, rightMin);
            var rangeMax = Mathf.Min(leftMax, rightMax);
            var doorProjection = Vector3.Dot(_door.position, forward);
            if (Mathf.Abs(doorProjection - rangeMin) < Mathf.Abs(doorProjection - rangeMax))
            {
                forward = -forward;
                rangeMin = -rangeMax;
                rangeMax = -Mathf.Max(leftMin, rightMin);
            }

            var back = rangeMin;
            var front = rangeMax;
            if (front - back <= 0.1f)
            {
                forward = Vector3.ProjectOnPlane(-_door.forward, Vector3.up).normalized;
                front = Vector3.Dot(_door.position, forward);
                back = front - _manualRoomDepth;
            }
            if (
                right.sqrMagnitude < .9f
                || forward.sqrMagnitude < .9f
                || rightEdge <= left
                || front <= back
            )
            {
                error = "境界から有効なRoom Width / Depthを計算できません。";
                return false;
            }
            room = new Room
            {
                right = right,
                forward = forward,
                rotation = Quaternion.LookRotation(forward),
                left = left,
                rightEdge = rightEdge,
                back = back,
                front = front,
                floorY = _door.position.y,
            };
            return true;
        }

        bool TrySetBounds(out Bounds bounds, out Bounds deskBounds, out string error)
        {
            bounds = default;
            deskBounds = default;
            error = "";
            if (!ValidateReference(out error))
                return false;
            if (!PrefabBounds(_referenceDeskSet, out var referenceBounds))
            {
                error = "Reference DeskSetのRenderer Boundsを取得できません。";
                return false;
            }
            bounds = Transformed(referenceBounds, Matrix4x4.Scale(_referenceDeskSet.localScale));
            var desk = Descendant(_referenceDeskSet, "Desk");
            if (desk == null || !BoundsRelativeToRoot(_referenceDeskSet, desk, out deskBounds))
            {
                error = "Reference DeskSet内のDesk Renderer Boundsを取得できません。";
                return false;
            }
            deskBounds = Transformed(deskBounds, Matrix4x4.Scale(_referenceDeskSet.localScale));
            return true;
        }

        bool ValidateReference(out string error)
        {
            error = "";
            if (
                _referenceDeskSet == null
                || _referenceDeskSet.gameObject.scene.path != ScenePath
                || EditorUtility.IsPersistent(_referenceDeskSet)
                || !_referenceDeskSet.name.StartsWith("DeskSet_", StringComparison.Ordinal)
            )
            {
                error = "Field_Area01_Props_3F.unity内の364完成DeskSetを指定してください。";
                return false;
            }
            var ancestor = _referenceDeskSet.parent;
            while (ancestor != null && ancestor.name != "Props_364")
                ancestor = ancestor.parent;
            if (ancestor == null)
            {
                error = "Reference DeskSetはProps_364配下から指定してください。";
                return false;
            }
            foreach (var name in new[] { "Desk", "Chair", "Monitor_C", "Keyboard", "Mouse" })
            {
                if (Descendant(_referenceDeskSet, name) != null)
                    continue;
                error = $"Reference DeskSet内に{name}がありません。";
                return false;
            }
            return true;
        }

        bool DeskSet(int n, Vector3 p, Quaternion r, Transform parent, Scene scene, Room room)
        {
            var clone = Instantiate(_referenceDeskSet.gameObject);
            Undo.RegisterCreatedObjectUndo(clone, $"DeskSet_{n:00}を生成");
            clone.name = $"DeskSet_{n:00}";
            Undo.SetTransformParent(clone.transform, parent, $"DeskSet_{n:00}を配置");
            clone.transform.localScale = _referenceDeskSet.localScale;
            clone.transform.SetPositionAndRotation(p, r);
            if (SnapToFloorAndValidate(clone, room))
            {
                clone.transform.rotation = r;
                return true;
            }
            Undo.DestroyObjectImmediate(clone);
            return false;
        }

        bool NormalizeAndValidateIsland(Transform island, Quaternion islandRotation)
        {
            island.rotation = islandRotation;
            island.localScale = Vector3.one;
            var sets = island
                .Cast<Transform>()
                .Where(child => child.name.StartsWith("DeskSet_", StringComparison.Ordinal))
                .OrderBy(child => child.name, StringComparer.Ordinal)
                .ToArray();
            if (sets.Length != 4)
            {
                Debug.LogWarning($"[Room363Layout] {island.name}のDeskSet数が{sets.Length}です。");
                return false;
            }

            foreach (var set in sets)
                set.rotation = islandRotation;

            var first = sets[0];
            var rotationsMatch = true;
            foreach (var set in sets)
            {
                var angle = Quaternion.Angle(set.rotation, first.rotation);
                rotationsMatch &= angle <= 0.01f;
                Debug.Log(
                    $"[Room363Layout] Island Transform: name={set.name}, "
                        + $"hierarchy={GetHierarchyPath(set)}, worldPosition={set.position}, "
                        + $"worldRotation={set.rotation}, localRotation={set.localRotation}, "
                        + $"localScale={set.localScale}, lossyScale={set.lossyScale}, "
                        + $"forward={set.forward}, parentWorldRotation={set.parent.rotation}, "
                        + $"parentLocalRotation={set.parent.localRotation}, "
                        + $"parentLocalScale={set.parent.localScale}, "
                        + $"parentLossyScale={set.parent.lossyScale}, angleFromFirst={angle:0.######}"
                );
            }

            if (!rotationsMatch)
                Debug.LogWarning(
                    $"[Room363Layout] {island.name}内のDeskSet World Rotationが一致しません。"
                );
            if (!HaveMatchingChildTransforms(first, sets.Skip(1), out var mismatch))
                Debug.LogWarning(
                    $"[Room363Layout] Rotationは一致していますが子Transformが異なります: {mismatch}"
                );
            return rotationsMatch;
        }

        static bool HaveMatchingChildTransforms(
            Transform reference,
            IEnumerable<Transform> others,
            out string mismatch
        )
        {
            var referenceChildren = RelativeDescendants(reference);
            foreach (var other in others)
            {
                var children = RelativeDescendants(other);
                if (
                    !referenceChildren
                        .Keys.OrderBy(key => key)
                        .SequenceEqual(children.Keys.OrderBy(key => key))
                )
                {
                    mismatch = $"{other.name}の子Hierarchy構造";
                    return false;
                }
                foreach (var pair in referenceChildren)
                {
                    var candidate = children[pair.Key];
                    if (
                        (pair.Value.localPosition - candidate.localPosition).sqrMagnitude
                            > 0.000001f
                        || Quaternion.Angle(pair.Value.localRotation, candidate.localRotation)
                            > 0.001f
                        || (pair.Value.localScale - candidate.localScale).sqrMagnitude > 0.000001f
                    )
                    {
                        mismatch = $"{other.name}/{pair.Key}";
                        return false;
                    }
                }
            }
            mismatch = "";
            return true;
        }

        static Dictionary<string, Transform> RelativeDescendants(Transform root)
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root)
                    continue;
                result[GetRelativePath(root, child)] = child;
            }
            return result;
        }

        static string GetRelativePath(Transform root, Transform child)
        {
            var names = new Stack<string>();
            for (var current = child; current != null && current != root; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        bool SnapToFloorAndValidate(GameObject deskSet, Room room)
        {
            if (!Points(deskSet.transform, out var points))
                throw new InvalidOperationException(
                    $"{deskSet.name}のRenderer Boundsがありません。"
                );

            var targetFloor = room.floorY + _floorYOffset;
            var minY = points.Min(point => point.y);
            deskSet.transform.position += Vector3.up * (targetFloor - minY);
            if (!Points(deskSet.transform, out points))
                return false;
            var minX = points.Min(point => Vector3.Dot(point, room.right));
            var maxX = points.Max(point => Vector3.Dot(point, room.right));
            var minZ = points.Min(point => Vector3.Dot(point, room.forward));
            var maxZ = points.Max(point => Vector3.Dot(point, room.forward));
            var allowedMinX = room.left + _leftOffset;
            var allowedMaxX = room.rightEdge - _rightOffset;
            var allowedMinZ = room.back;
            var allowedMaxZ = room.front;
            const float tolerance = 0.002f;
            return minX >= allowedMinX - tolerance
                && maxX <= allowedMaxX + tolerance
                && minZ >= allowedMinZ - tolerance
                && maxZ <= allowedMaxZ + tolerance;
        }

        bool TryCalculate364Room(Room targetRoom, out Room room, out string error)
        {
            room = default;
            error = "";
            if (_room364Left == null || _room364Right == null)
            {
                error =
                    "Teacher / Printer / Clockをコピーする場合は364 Left / Right Boundaryを指定してください。";
                return false;
            }
            if (
                !Points(_room364Left, out var leftPoints)
                || !Points(_room364Right, out var rightPoints)
            )
            {
                error = "364 Left / Right BoundaryのRenderer Boundsを取得できません。";
                return false;
            }
            var leftCenter = Average(leftPoints);
            var rightCenter = Average(rightPoints);
            var right = Vector3.ProjectOnPlane(rightCenter - leftCenter, Vector3.up).normalized;
            var forward = Vector3.Cross(Vector3.up, right).normalized;
            if (Vector3.Dot(forward, targetRoom.forward) < 0f)
                forward = -forward;
            var left = leftPoints.Max(point => Vector3.Dot(point, right));
            var rightEdge = rightPoints.Min(point => Vector3.Dot(point, right));
            var back = Mathf.Max(
                leftPoints.Min(point => Vector3.Dot(point, forward)),
                rightPoints.Min(point => Vector3.Dot(point, forward))
            );
            var front = Mathf.Min(
                leftPoints.Max(point => Vector3.Dot(point, forward)),
                rightPoints.Max(point => Vector3.Dot(point, forward))
            );
            if (rightEdge <= left || front <= back)
            {
                error = "364境界から有効なRoom領域を計算できません。";
                return false;
            }
            room = new Room
            {
                right = right,
                forward = forward,
                rotation = Quaternion.LookRotation(forward),
                left = left,
                rightEdge = rightEdge,
                back = back,
                front = front,
                floorY = targetRoom.floorY,
            };
            return true;
        }

        void CopyMappedReference(
            Transform source,
            string objectName,
            Transform parent,
            Room sourceRoom,
            Room targetRoom,
            bool attachToWall
        )
        {
            if (source == null)
                return;
            var sourceX = Vector3.Dot(source.position, sourceRoom.right);
            var sourceZ = Vector3.Dot(source.position, sourceRoom.forward);
            var normalizedX = Mathf.InverseLerp(sourceRoom.left, sourceRoom.rightEdge, sourceX);
            var normalizedZ = Mathf.InverseLerp(sourceRoom.back, sourceRoom.front, sourceZ);
            var targetX = Mathf.Lerp(targetRoom.left, targetRoom.rightEdge, normalizedX);
            var targetZ = Mathf.Lerp(targetRoom.back, targetRoom.front, normalizedZ);
            if (attachToWall)
            {
                var distances = new[]
                {
                    sourceX - sourceRoom.left,
                    sourceRoom.rightEdge - sourceX,
                    sourceZ - sourceRoom.back,
                    sourceRoom.front - sourceZ,
                };
                var wall = Array.IndexOf(distances, distances.Min());
                if (wall == 0)
                    targetX = targetRoom.left + distances[0];
                else if (wall == 1)
                    targetX = targetRoom.rightEdge - distances[1];
                else if (wall == 2)
                    targetZ = targetRoom.back + distances[2];
                else
                    targetZ = targetRoom.front - distances[3];
            }
            var clone = Instantiate(source.gameObject);
            Undo.RegisterCreatedObjectUndo(clone, $"{objectName}を生成");
            clone.name = objectName;
            Undo.SetTransformParent(clone.transform, parent, $"{objectName}を配置");
            clone.transform.localScale = source.localScale;
            clone.transform.SetPositionAndRotation(
                targetRoom.right * targetX
                    + targetRoom.forward * targetZ
                    + Vector3.up * (targetRoom.floorY + source.position.y - sourceRoom.floorY),
                targetRoom.rotation * Quaternion.Inverse(sourceRoom.rotation) * source.rotation
            );
        }

        static bool HasBoundsOutsideRoom(Transform root, Room room)
        {
            if (!Points(root, out var points))
                return false;
            const float tolerance = 0.01f;
            return points.Any(point =>
            {
                var x = Vector3.Dot(point, room.right);
                var z = Vector3.Dot(point, room.forward);
                return x < room.left - tolerance
                    || x > room.rightEdge + tolerance
                    || z < room.back - tolerance
                    || z > room.front + tolerance;
            });
        }

        static Vector3 Center(Transform root) =>
            Points(root, out var points) ? Average(points) : root.position;

        static float BoundaryProjection(Transform root, Vector3 axis, bool maximum)
        {
            if (!Points(root, out var points))
                return Vector3.Dot(root.position, axis);
            return maximum
                ? points.Max(point => Vector3.Dot(point, axis))
                : points.Min(point => Vector3.Dot(point, axis));
        }

        static GameObject Child(string n, Transform p, Scene s)
        {
            var o = new GameObject(n);
            SceneManager.MoveGameObjectToScene(o, s);
            Undo.RegisterCreatedObjectUndo(o, $"{n}を生成");
            Undo.SetTransformParent(o.transform, p, $"{n}を配置");
            o.transform.localPosition = Vector3.zero;
            return o;
        }

        static Transform Direct(Transform p, string n)
        {
            foreach (Transform c in p)
                if (c.name == n)
                    return c;
            return null;
        }

        static Transform Descendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child =>
                    string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)
                );
        }

        static bool IsUnderNamedAncestor(Transform transform, string ancestorName)
        {
            for (var current = transform; current != null; current = current.parent)
                if (current.name == ancestorName)
                    return true;
            return false;
        }

        static Vector3 Average(List<Vector3> p) =>
            p.Aggregate(Vector3.zero, (a, b) => a + b) / p.Count;

        static bool Points(Transform root, out List<Vector3> p)
        {
            p = new List<Vector3>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                p.AddRange(Corners(r.bounds));
            return p.Count > 0;
        }

        static bool PrefabBounds(Transform root, out Bounds b)
        {
            b = default;
            var init = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            foreach (var c in Corners(r.localBounds))
            {
                var p = (root.worldToLocalMatrix * r.transform.localToWorldMatrix).MultiplyPoint3x4(
                    c
                );
                if (!init)
                {
                    b = new Bounds(p, Vector3.zero);
                    init = true;
                }
                else
                    b.Encapsulate(p);
            }
            return init;
        }

        static bool BoundsRelativeToRoot(Transform root, Transform subtree, out Bounds bounds)
        {
            bounds = default;
            var initialized = false;
            foreach (var renderer in subtree.GetComponentsInChildren<Renderer>(true))
            foreach (var corner in Corners(renderer.localBounds))
            {
                var point = (
                    root.worldToLocalMatrix * renderer.transform.localToWorldMatrix
                ).MultiplyPoint3x4(corner);
                if (!initialized)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                    bounds.Encapsulate(point);
            }
            return initialized;
        }

        static Bounds Transformed(Bounds b, Matrix4x4 m)
        {
            var c = Corners(b).ToArray();
            var r = new Bounds(m.MultiplyPoint3x4(c[0]), Vector3.zero);
            foreach (var p in c.Skip(1))
                r.Encapsulate(m.MultiplyPoint3x4(p));
            return r;
        }

        static void Encapsulate(ref Bounds d, Bounds s, Matrix4x4 m)
        {
            foreach (var c in Corners(s))
                d.Encapsulate(m.MultiplyPoint3x4(c));
        }

        static void Projection(Bounds b, Quaternion r, Vector3 axis, out float min, out float max)
        {
            var v = Corners(b).Select(c => Vector3.Dot(r * c, axis));
            min = v.Min();
            max = v.Max();
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
                yield return b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
        }

        struct Room
        {
            public Vector3 right,
                forward;
            public Quaternion rotation;
            public float left,
                rightEdge,
                back,
                front,
                floorY;
            public float Width => rightEdge - left;
            public float Depth => front - back;
        }

        struct Layout
        {
            public Room room;
            public Bounds bounds;
            public List<float> frontSeatX;
            public float wallFirst,
                wallPitch,
                frontPivotZ,
                islandAreaLeft,
                islandAreaBack,
                islandWidth,
                islandDepth,
                islandPitchX,
                islandPitchZ,
                firstDeskOffsetX,
                deskPitchX,
                sideAOffset,
                sideBOffset,
                islandMin;
            public int wallRows,
                islandColumns,
                islandRows;
            public int frontSeatCount => frontSeatX?.Count ?? 0;
        }
    }
}
#endif
