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
    /// <summary>
    /// 364教室に、基準机を含む16組の机・椅子・PC周辺機器を配置する。
    /// Field_Area01_Props_3F 以外のシーンや、元のGLBアセットは変更しない。
    /// </summary>
    public sealed class Room364DeskLayoutWindow : EditorWindow
    {
        const string TargetScenePath = "Assets/_Project/Scenes/Field/Field_Area01_Props_3F.unity";
        const string PropsRootName = "Props_364";
        const string FloorPropsRootName = "Props_3F";
        const string GeneratedMarkerName = "__GeneratedByRoom364DeskLayoutWindow";
        const int CurrentConfigurationVersion = 2;

        const string DeskAssetPath =
            "Assets/_Project/Art/Models/Environment/Furniture/RectangularTable.glb";
        const string ChairAssetPath = "Assets/_Project/Art/Models/Environment/Furniture/Chair.glb";
        const string WorkstationAssetPath =
            "Assets/_Project/Art/Models/Environment/Furniture/Workstation.glb";

        static readonly string[] EquipmentNodeNames = { "Monitor_C", "Keyboard", "Mouse" };

        [SerializeField]
        Transform _referenceDesk;

        [SerializeField]
        RoomDeskFurnitureSettings _sharedFurnitureSettings;

        [SerializeField]
        GameObject _deskPrefab;

        [SerializeField]
        GameObject _chairPrefab;

        [SerializeField]
        GameObject _workstationPrefab;

        [SerializeField]
        bool _useRendererBounds = true;

        [SerializeField]
        Vector2 _deskGap = new(0.15f, 0.15f);

        [SerializeField]
        Vector2 _manualDeskPitch = new(2f, 1.5f);

        [SerializeField]
        Vector2 _aisleWidth = new(2f, 2f);

        [SerializeField]
        Vector3 _deskScale = new(3f, 6f, 5f);

        [SerializeField]
        Vector3 _chairScale = new(3.46f, 3.14f, 3.81f);

        [SerializeField]
        Vector3 _monitorScale = Vector3.one * 3f;

        [SerializeField]
        Vector3 _keyboardScale = Vector3.one * 3f;

        [SerializeField]
        Vector3 _mouseScale = Vector3.one * 3f;

        [SerializeField]
        float _seatHorizontalOffset = 0.9f;

        [SerializeField]
        Vector3 _chairLocalPosition = new(0f, 0f, -1.2f);

        [SerializeField]
        Vector3 _chairLocalEuler = new(0f, 0f, 0f);

        [SerializeField]
        Vector3 _monitorLocalPosition = new(0f, 0.8f, 0.15f);

        [SerializeField]
        Vector3 _monitorLocalEuler = Vector3.zero;

        [SerializeField]
        Vector3 _keyboardLocalPosition = new(0f, 0.78f, -0.2f);

        [SerializeField]
        Vector3 _keyboardLocalEuler = Vector3.zero;

        [SerializeField]
        Vector3 _mouseLocalPosition = new(0.35f, 0.78f, -0.2f);

        [SerializeField]
        Vector3 _mouseLocalEuler = Vector3.zero;

        [SerializeField]
        bool _equipmentOffsetsInitialized;

        [SerializeField]
        bool _scaleDefaultsInitialized;

        [SerializeField]
        int _configurationVersion;

        Vector2 _scroll;

        [MenuItem("Tools/CreativeAI/Map/364教室/机レイアウト")]
        public static void Open()
        {
            var window = GetWindow<Room364DeskLayoutWindow>("364 机レイアウト");
            window.minSize = new Vector2(430f, 650f);
            window.Show();
        }

        void OnEnable()
        {
            _sharedFurnitureSettings ??= RoomDeskFurnitureSettings.LoadDefault();
            _deskPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(DeskAssetPath);
            _chairPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(ChairAssetPath);
            _workstationPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(WorkstationAssetPath);

            if (_workstationPrefab != null && !_equipmentOffsetsInitialized)
            {
                ApplyWorkstationEquipmentOffsets(false);
                _equipmentOffsetsInitialized = true;
            }

            if (
                _referenceDesk != null
                && (
                    !_scaleDefaultsInitialized
                    || _configurationVersion != CurrentConfigurationVersion
                )
            )
                ApplyBoundsBasedScaleDefaults(false);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "基準机を1台目として再利用し、364教室用の机セットを合計16組生成します。"
                    + "対象シーン以外は変更せず、シーンも自動保存しません。",
                MessageType.Info
            );

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("基準とモデル", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var referenceDesk = (Transform)
                EditorGUILayout.ObjectField("基準机", _referenceDesk, typeof(Transform), true);
            if (EditorGUI.EndChangeCheck())
            {
                _referenceDesk = referenceDesk;
                if (_referenceDesk != null)
                    ApplyBoundsBasedScaleDefaults(false);
            }
            _deskPrefab = (GameObject)
                EditorGUILayout.ObjectField("Desk Prefab", _deskPrefab, typeof(GameObject), false);
            _chairPrefab = (GameObject)
                EditorGUILayout.ObjectField(
                    "Chair Prefab",
                    _chairPrefab,
                    typeof(GameObject),
                    false
                );
            _workstationPrefab = (GameObject)
                EditorGUILayout.ObjectField(
                    "Workstation Prefab",
                    _workstationPrefab,
                    typeof(GameObject),
                    false
                );

            if (GUILayout.Button("選択中のGameObjectを基準机にする"))
            {
                _referenceDesk = Selection.activeTransform;
                if (_referenceDesk != null)
                    ApplyBoundsBasedScaleDefaults(false);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("生成物のScale", EditorStyles.boldLabel);
            _deskScale = EditorGUILayout.Vector3Field("Desk Scale", _deskScale);
            _chairScale = EditorGUILayout.Vector3Field("Chair Scale", _chairScale);
            _monitorScale = EditorGUILayout.Vector3Field("Monitor Scale", _monitorScale);
            _keyboardScale = EditorGUILayout.Vector3Field("Keyboard Scale", _keyboardScale);
            _mouseScale = EditorGUILayout.Vector3Field("Mouse Scale", _mouseScale);
            _sharedFurnitureSettings = (RoomDeskFurnitureSettings)
                EditorGUILayout.ObjectField(
                    "Shared Furniture Settings",
                    _sharedFurnitureSettings,
                    typeof(RoomDeskFurnitureSettings),
                    false
                );
            if (GUILayout.Button("Save / Update Shared Furniture Settings"))
                SaveOrUpdateSharedFurnitureSettings();
            if (GUILayout.Button("基準机とBoundsからScale初期値を再計算"))
                ApplyBoundsBasedScaleDefaults(true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2席の配置", EditorStyles.boldLabel);
            _seatHorizontalOffset = EditorGUILayout.FloatField(
                "Seat Horizontal Offset",
                _seatHorizontalOffset
            );
            _chairLocalPosition.z = EditorGUILayout.FloatField(
                "Chair Z Offset",
                _chairLocalPosition.z
            );
            _monitorLocalPosition.z = EditorGUILayout.FloatField(
                "Monitor Z Offset",
                _monitorLocalPosition.z
            );
            _keyboardLocalPosition.z = EditorGUILayout.FloatField(
                "Keyboard Z Offset",
                _keyboardLocalPosition.z
            );
            var mouseOffset = EditorGUILayout.Vector2Field(
                "Mouse X / Z Offset",
                new Vector2(_mouseLocalPosition.x, _mouseLocalPosition.z)
            );
            _mouseLocalPosition.x = mouseOffset.x;
            _mouseLocalPosition.z = mouseOffset.y;
            if (GUILayout.Button("DeskSet_01の1席から設定を取得"))
                ApplyDeskSet01Reference();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("机と島の間隔", EditorStyles.boldLabel);
            _useRendererBounds = EditorGUILayout.Toggle(
                "Renderer Boundsを使用",
                _useRendererBounds
            );
            using (new EditorGUI.DisabledScope(!_useRendererBounds))
                _deskGap = EditorGUILayout.Vector2Field("机間の追加隙間 X / Z", _deskGap);
            using (new EditorGUI.DisabledScope(_useRendererBounds))
                _manualDeskPitch = EditorGUILayout.Vector2Field(
                    "机の中心間隔 X / Z",
                    _manualDeskPitch
                );
            _aisleWidth = EditorGUILayout.Vector2Field("島間の通路幅 X / Z", _aisleWidth);

            ShowCalculatedDeskPitch();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("椅子（Seatローカル）", EditorStyles.boldLabel);
            _chairLocalPosition.y = EditorGUILayout.FloatField("Y Offset", _chairLocalPosition.y);
            _chairLocalEuler = EditorGUILayout.Vector3Field("Rotation", _chairLocalEuler);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Monitor_C（Seatローカル）", EditorStyles.boldLabel);
            _monitorLocalPosition.y = EditorGUILayout.FloatField(
                "Y Offset",
                _monitorLocalPosition.y
            );
            _monitorLocalEuler = EditorGUILayout.Vector3Field("Rotation", _monitorLocalEuler);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Keyboard（Seatローカル）", EditorStyles.boldLabel);
            _keyboardLocalPosition.y = EditorGUILayout.FloatField(
                "Y Offset",
                _keyboardLocalPosition.y
            );
            _keyboardLocalEuler = EditorGUILayout.Vector3Field("Rotation", _keyboardLocalEuler);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mouse（Seatローカル）", EditorStyles.boldLabel);
            _mouseLocalPosition.y = EditorGUILayout.FloatField("Y Offset", _mouseLocalPosition.y);
            _mouseLocalEuler = EditorGUILayout.Vector3Field("Rotation", _mouseLocalEuler);

            if (GUILayout.Button("Workstation Boundsから位置・Scaleを再計算"))
                ApplyBoundsBasedScaleDefaults(true);

            EditorGUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Preview 1 Set", GUILayout.Height(30f)))
                    PreviewOneSet();
                if (GUILayout.Button("Generate / Regenerate All 16 Sets", GUILayout.Height(34f)))
                    GenerateAllSets();
            }

            EditorGUILayout.EndScrollView();
        }

        void SaveOrUpdateSharedFurnitureSettings()
        {
            if (_workstationPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Shared Furniture Settings",
                    "Workstation Prefabを指定してください。",
                    "OK"
                );
                return;
            }

            var monitor = FindDescendant(_workstationPrefab.transform, "Monitor_C");
            var keyboard = FindDescendant(_workstationPrefab.transform, "Keyboard");
            var mouse = FindDescendant(_workstationPrefab.transform, "Mouse");
            if (monitor == null || keyboard == null || mouse == null)
            {
                EditorUtility.DisplayDialog(
                    "Shared Furniture Settings",
                    "Workstation内のMonitor_C / Keyboard / Mouseを確認できません。",
                    "OK"
                );
                return;
            }

            var existing = RoomDeskFurnitureSettings.LoadDefault();
            if (
                existing != null
                && !EditorUtility.DisplayDialog(
                    "Shared Furniture Settings",
                    $"既存の共有設定を現在の364設定で更新します。\n{RoomDeskFurnitureSettings.DefaultAssetPath}",
                    "更新",
                    "キャンセル"
                )
            )
                return;

            _sharedFurnitureSettings = existing ?? RoomDeskFurnitureSettings.GetOrCreateDefault();
            Undo.RecordObject(_sharedFurnitureSettings, "364家具設定を共通設定へ反映");
            _sharedFurnitureSettings.deskPrefab = _deskPrefab;
            _sharedFurnitureSettings.chairPrefab = _chairPrefab;
            _sharedFurnitureSettings.workstationPrefab = _workstationPrefab;
            _sharedFurnitureSettings.monitorSource = monitor.gameObject;
            _sharedFurnitureSettings.keyboardSource = keyboard.gameObject;
            _sharedFurnitureSettings.mouseSource = mouse.gameObject;
            _sharedFurnitureSettings.deskScale = _deskScale;
            _sharedFurnitureSettings.chairScale = _chairScale;
            _sharedFurnitureSettings.monitorScale = _monitorScale;
            _sharedFurnitureSettings.keyboardScale = _keyboardScale;
            _sharedFurnitureSettings.mouseScale = _mouseScale;
            _sharedFurnitureSettings.seatHorizontalOffset = _seatHorizontalOffset;
            _sharedFurnitureSettings.chairLocalPosition = _chairLocalPosition;
            _sharedFurnitureSettings.chairLocalEuler = _chairLocalEuler;
            _sharedFurnitureSettings.monitorLocalPosition = _monitorLocalPosition;
            _sharedFurnitureSettings.monitorLocalEuler = _monitorLocalEuler;
            _sharedFurnitureSettings.keyboardLocalPosition = _keyboardLocalPosition;
            _sharedFurnitureSettings.keyboardLocalEuler = _keyboardLocalEuler;
            _sharedFurnitureSettings.mouseLocalPosition = _mouseLocalPosition;
            _sharedFurnitureSettings.mouseLocalEuler = _mouseLocalEuler;
            EditorUtility.SetDirty(_sharedFurnitureSettings);
            AssetDatabase.SaveAssetIfDirty(_sharedFurnitureSettings);
            Selection.activeObject = _sharedFurnitureSettings;
            EditorUtility.DisplayDialog(
                "Shared Furniture Settings",
                $"364の家具設定を保存しました。\n{RoomDeskFurnitureSettings.DefaultAssetPath}",
                "OK"
            );
        }

        void ShowCalculatedDeskPitch()
        {
            if (!_useRendererBounds || _referenceDesk == null)
                return;

            if (TryCalculateLocalRendererBounds(_referenceDesk, out var bounds))
            {
                var pitch = new Vector2(
                    bounds.size.x * Mathf.Abs(_deskScale.x) + _deskGap.x,
                    bounds.size.z * Mathf.Abs(_deskScale.z) + _deskGap.y
                );
                EditorGUILayout.LabelField(
                    "計算される中心間隔",
                    $"X {pitch.x:F3} / Z {pitch.y:F3}"
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "基準机からRenderer Boundsを取得できません。手動間隔を使用します。",
                    MessageType.Warning
                );
            }
        }

        void PreviewOneSet()
        {
            if (!TryValidate(out var scene, out var error))
            {
                EditorUtility.DisplayDialog("364 机レイアウト", error, "OK");
                return;
            }

            var existingProps = FindInScene(scene, PropsRootName);
            if (
                existingProps != null
                && existingProps.transform.Find("DeskSet_01") != null
                && !TryValidateGeneratedDeskSet(
                    existingProps.transform.Find("DeskSet_01"),
                    out error
                )
            )
            {
                EditorUtility.DisplayDialog("364 机レイアウト", error, "OK");
                return;
            }

            RunGeneration(scene, true, existingProps, Array.Empty<GameObject>());
        }

        void GenerateAllSets()
        {
            if (!TryValidate(out var scene, out var error))
            {
                EditorUtility.DisplayDialog("364 机レイアウト", error, "OK");
                return;
            }

            var propsRoot = FindInScene(scene, PropsRootName);
            IReadOnlyList<GameObject> generatedSets = Array.Empty<GameObject>();
            if (
                propsRoot != null
                && !TryCollectGeneratedDeskSets(propsRoot, out generatedSets, out error)
            )
            {
                EditorUtility.DisplayDialog("364 机レイアウト", error, "OK");
                return;
            }

            RunGeneration(scene, false, propsRoot, generatedSets);
        }

        void RunGeneration(
            Scene scene,
            bool previewOnly,
            GameObject existingProps,
            IReadOnlyList<GameObject> generatedSets
        )
        {
            var pitch = CalculateDeskPitch();
            var equipmentSources = EquipmentNodeNames.ToDictionary(
                name => name,
                name => FindDescendant(_workstationPrefab.transform, name),
                StringComparer.OrdinalIgnoreCase
            );
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(
                previewOnly ? "364教室の1セットをプレビュー" : "364教室を16セットへ展開"
            );
            var previousActiveScene = SceneManager.GetActiveScene();

            try
            {
                SceneManager.SetActiveScene(scene);
                var propsRoot = existingProps ?? CreatePropsRoot(scene);
                var anchorPosition = _referenceDesk.position;
                var anchorRotation = _referenceDesk.rotation;

                if (previewOnly)
                {
                    var previewSet = propsRoot.transform.Find("DeskSet_01");
                    if (previewSet != null)
                    {
                        PreserveReferenceDeskBeforeRegeneration(
                            propsRoot.transform,
                            new[] { previewSet.gameObject }
                        );
                        Undo.DestroyObjectImmediate(previewSet.gameObject);
                    }
                    CreateDeskSet(
                        1,
                        anchorPosition,
                        anchorRotation,
                        scene,
                        propsRoot.transform,
                        equipmentSources,
                        true
                    );
                }
                else
                {
                    PreserveReferenceDeskBeforeRegeneration(propsRoot.transform, generatedSets);
                    foreach (var generatedSet in generatedSets)
                        Undo.DestroyObjectImmediate(generatedSet);

                    var deskNumber = 1;
                    for (var islandZ = 0; islandZ < 2; islandZ++)
                    for (var islandX = 0; islandX < 2; islandX++)
                    for (var deskZ = 0; deskZ < 2; deskZ++)
                    for (var deskX = 0; deskX < 2; deskX++)
                    {
                        var offset = new Vector3(
                            deskX * pitch.x + islandX * (2f * pitch.x + _aisleWidth.x),
                            0f,
                            deskZ * pitch.y + islandZ * (2f * pitch.y + _aisleWidth.y)
                        );
                        var deskSetRotation =
                            anchorRotation
                            * (deskZ == 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
                        CreateDeskSet(
                            deskNumber,
                            anchorPosition + anchorRotation * offset,
                            deskSetRotation,
                            scene,
                            propsRoot.transform,
                            equipmentSources,
                            deskNumber == 1
                        );
                        deskNumber++;
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = propsRoot;
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log(
                    previewOnly
                        ? "[Room364DeskLayout] DeskSet_01を1机2席で生成しました。シーンは自動保存していません。"
                        : "[Room364DeskLayout] 1机2席のDeskSetを現在の設定で16セットへ再生成しました。"
                            + "シーンは自動保存していません。"
                );
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "364 机レイアウト",
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

        GameObject CreatePropsRoot(Scene scene)
        {
            var propsRoot = CreateGameObject(PropsRootName, scene);
            var floorRoot = FindRoot(scene, FloorPropsRootName);
            if (floorRoot != null)
                Undo.SetTransformParent(
                    propsRoot.transform,
                    floorRoot.transform,
                    "Props_364をProps_3Fの子に移動"
                );
            return propsRoot;
        }

        void CreateDeskSet(
            int deskNumber,
            Vector3 position,
            Quaternion rotation,
            Scene scene,
            Transform propsRoot,
            System.Collections.Generic.IReadOnlyDictionary<string, Transform> equipmentSources,
            bool reuseReferenceDesk
        )
        {
            var deskSet = CreateGameObject($"DeskSet_{deskNumber:00}", scene);
            deskSet.transform.SetPositionAndRotation(position, rotation);
            Undo.SetTransformParent(
                deskSet.transform,
                propsRoot,
                $"DeskSet_{deskNumber:00}をProps_364の子に移動"
            );
            CreateGeneratedMarker(deskSet.transform, scene);

            GameObject desk;
            if (reuseReferenceDesk)
            {
                desk = _referenceDesk.gameObject;
                Undo.SetTransformParent(
                    desk.transform,
                    deskSet.transform,
                    "基準机をDeskSet_01の子に移動"
                );
                Undo.RecordObject(desk, "基準机の名前を変更");
                desk.name = "Desk";
            }
            else
            {
                desk = (GameObject)PrefabUtility.InstantiatePrefab(_deskPrefab, scene);
                Undo.RegisterCreatedObjectUndo(desk, "机を生成");
                desk.name = "Desk";
                desk.transform.SetPositionAndRotation(position, rotation);
                Undo.SetTransformParent(
                    desk.transform,
                    deskSet.transform,
                    $"机をDeskSet_{deskNumber:00}の子に移動"
                );
            }

            SetWorldScale(desk.transform, _deskScale, "机のScaleを設定");
            var longAxisIsX = true;
            if (TryCalculateLocalRendererBounds(desk.transform, out var deskBounds))
            {
                longAxisIsX =
                    deskBounds.size.x * Mathf.Abs(_deskScale.x)
                    >= deskBounds.size.z * Mathf.Abs(_deskScale.z);
            }
            var seatRotation =
                rotation * (longAxisIsX ? Quaternion.identity : Quaternion.Euler(0f, -90f, 0f));
            CreateSeat(
                1,
                -_seatHorizontalOffset,
                position,
                seatRotation,
                deskSet.transform,
                equipmentSources,
                scene
            );
            CreateSeat(
                2,
                _seatHorizontalOffset,
                position,
                seatRotation,
                deskSet.transform,
                equipmentSources,
                scene
            );
        }

        void CreateSeat(
            int seatNumber,
            float horizontalOffset,
            Vector3 deskPosition,
            Quaternion seatRotation,
            Transform deskSet,
            IReadOnlyDictionary<string, Transform> equipmentSources,
            Scene scene
        )
        {
            var seat = CreateGameObject($"Seat_{seatNumber:00}", scene);
            seat.transform.SetPositionAndRotation(
                deskPosition + seatRotation * Vector3.right * horizontalOffset,
                seatRotation
            );
            Undo.SetTransformParent(
                seat.transform,
                deskSet,
                $"Seat_{seatNumber:00}を机セットの子に移動"
            );
            CreatePrefabRelativeToDesk(
                _chairPrefab,
                "Chair",
                seat.transform,
                seat.transform,
                _chairLocalPosition,
                _chairLocalEuler,
                _chairScale,
                scene
            );

            var pc = CreateGameObject("PC", scene);
            pc.transform.SetPositionAndRotation(seat.transform.position, seat.transform.rotation);
            Undo.SetTransformParent(pc.transform, seat.transform, "PCをSeatの子に移動");
            CloneWorkstationNodeRelativeToDesk(
                equipmentSources["Monitor_C"],
                "Monitor_C",
                seat.transform,
                pc.transform,
                _monitorLocalPosition,
                _monitorLocalEuler,
                _monitorScale
            );
            CloneWorkstationNodeRelativeToDesk(
                equipmentSources["Keyboard"],
                "Keyboard",
                seat.transform,
                pc.transform,
                _keyboardLocalPosition,
                _keyboardLocalEuler,
                _keyboardScale
            );
            CloneWorkstationNodeRelativeToDesk(
                equipmentSources["Mouse"],
                "Mouse",
                seat.transform,
                pc.transform,
                _mouseLocalPosition,
                _mouseLocalEuler,
                _mouseScale
            );
        }

        bool TryCollectGeneratedDeskSets(
            GameObject propsRoot,
            out IReadOnlyList<GameObject> generatedSets,
            out string error
        )
        {
            var result = new List<GameObject>();
            var numbers = new HashSet<int>();
            foreach (Transform child in propsRoot.transform)
            {
                if (!TryGetDeskSetNumber(child.name, out var number))
                    continue;

                if (!numbers.Add(number))
                {
                    generatedSets = Array.Empty<GameObject>();
                    error = $"{child.name}が重複しています。勝手に削除せず処理を中止します。";
                    return false;
                }

                if (!TryValidateGeneratedDeskSet(child, out error))
                {
                    generatedSets = Array.Empty<GameObject>();
                    return false;
                }

                result.Add(child.gameObject);
            }

            generatedSets = result;
            error = string.Empty;
            return true;
        }

        static bool TryValidateGeneratedDeskSet(Transform deskSet, out string error)
        {
            var childNames = deskSet.Cast<Transform>().Select(child => child.name).ToArray();
            var isTwoSeatLayout = childNames.Contains("Seat_01") || childNames.Contains("Seat_02");
            var expectedNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "Desk",
                GeneratedMarkerName,
            };
            foreach (
                var name in isTwoSeatLayout
                    ? new[] { "Seat_01", "Seat_02" }
                    : new[] { "Chair", "Monitor_C", "Keyboard", "Mouse" }
            )
                expectedNames.Add(name);

            var unexpectedNames = childNames.Where(name => !expectedNames.Contains(name)).ToArray();
            if (unexpectedNames.Length > 0)
            {
                error =
                    $"{deskSet.name}内にツール生成物ではない可能性があるオブジェクトがあります: "
                    + string.Join(", ", unexpectedNames)
                    + "\n手動オブジェクトを保護するため再生成を中止します。";
                return false;
            }

            var requiredNames = isTwoSeatLayout
                ? new[] { "Desk", "Seat_01", "Seat_02" }
                : new[] { "Desk", "Chair", "Monitor_C", "Keyboard", "Mouse" };
            foreach (var requiredName in requiredNames)
            {
                if (childNames.Count(name => name == requiredName) == 1)
                    continue;
                error =
                    $"{deskSet.name}の{requiredName}構成が想定と異なります。"
                    + "勝手に削除せず再生成を中止します。";
                return false;
            }

            if (childNames.Count(name => name == GeneratedMarkerName) > 1)
            {
                error = $"{deskSet.name}の生成マーカーが重複しています。再生成を中止します。";
                return false;
            }

            var marker = deskSet.Find(GeneratedMarkerName);
            if (marker != null && marker.childCount > 0)
            {
                error =
                    $"{deskSet.name}の生成マーカー内に手動オブジェクトの可能性がある子があります。"
                    + "削除せず再生成を中止します。";
                return false;
            }

            if (!TryValidatePrefabInstanceHasNoManualChildren(deskSet.Find("Desk"), out error))
                return false;

            if (isTwoSeatLayout)
            {
                foreach (var seatName in new[] { "Seat_01", "Seat_02" })
                {
                    if (!TryValidateGeneratedSeat(deskSet.Find(seatName), out error))
                        return false;
                }
            }
            else
            {
                if (!TryValidatePrefabInstanceHasNoManualChildren(deskSet.Find("Chair"), out error))
                    return false;
                foreach (var equipmentName in new[] { "Monitor_C", "Keyboard", "Mouse" })
                {
                    if (deskSet.Find(equipmentName).childCount == 0)
                        continue;
                    error =
                        $"{deskSet.name}/{equipmentName}内に手動追加された可能性がある子があります。"
                        + "削除せず再生成を中止します。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        static bool TryValidateGeneratedSeat(Transform seat, out string error)
        {
            var childNames = seat.Cast<Transform>().Select(child => child.name).ToArray();
            if (
                childNames.Length != 2
                || childNames.Count(name => name == "Chair") != 1
                || childNames.Count(name => name == "PC") != 1
            )
            {
                error =
                    $"{seat.parent.name}/{seat.name}内に手動追加または不足しているオブジェクトがあります。"
                    + "削除せず再生成を中止します。";
                return false;
            }

            if (!TryValidatePrefabInstanceHasNoManualChildren(seat.Find("Chair"), out error))
                return false;

            var pc = seat.Find("PC");
            var pcChildNames = pc.Cast<Transform>().Select(child => child.name).ToArray();
            foreach (var equipmentName in new[] { "Monitor_C", "Keyboard", "Mouse" })
            {
                if (pcChildNames.Count(name => name == equipmentName) != 1)
                {
                    error =
                        $"{seat.parent.name}/{seat.name}/PCの{equipmentName}構成が想定と異なります。"
                        + "削除せず再生成を中止します。";
                    return false;
                }
                if (pc.Find(equipmentName).childCount == 0)
                    continue;
                error =
                    $"{seat.parent.name}/{seat.name}/PC/{equipmentName}内に手動追加された可能性がある子があります。"
                    + "削除せず再生成を中止します。";
                return false;
            }

            if (pcChildNames.Length != 3)
            {
                error =
                    $"{seat.parent.name}/{seat.name}/PC内に手動追加された可能性があるオブジェクトがあります。"
                    + "削除せず再生成を中止します。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        static bool TryValidatePrefabInstanceHasNoManualChildren(
            Transform prefabRoot,
            out string error
        )
        {
            var manuallyAdded = prefabRoot
                .GetComponentsInChildren<Transform>(true)
                .Any(transform =>
                    PrefabUtility.GetCorrespondingObjectFromOriginalSource(transform.gameObject)
                    == null
                );
            if (!manuallyAdded)
            {
                error = string.Empty;
                return true;
            }

            error =
                $"{prefabRoot.name}内に手動追加された可能性がある子があります。"
                + "削除せず再生成を中止します。";
            return false;
        }

        static bool TryGetDeskSetNumber(string name, out int number)
        {
            number = 0;
            const string prefix = "DeskSet_";
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            if (!int.TryParse(name.Substring(prefix.Length), out number))
                return false;
            return number is >= 1 and <= 16 && name == $"{prefix}{number:00}";
        }

        void PreserveReferenceDeskBeforeRegeneration(
            Transform propsRoot,
            IReadOnlyList<GameObject> generatedSets
        )
        {
            if (
                !generatedSets.Any(set =>
                    _referenceDesk == set.transform || _referenceDesk.IsChildOf(set.transform)
                )
            )
                return;

            Undo.SetTransformParent(_referenceDesk, propsRoot, "再生成前に基準机をProps_364へ退避");
        }

        bool TryValidate(out Scene scene, out string error)
        {
            scene = default;
            error = string.Empty;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "Play Mode中は生成できません。";
                return false;
            }

            if (_referenceDesk == null)
            {
                error = "364教室に手動配置した基準机を指定してください。";
                return false;
            }

            scene = _referenceDesk.gameObject.scene;
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                error =
                    "基準机は Field_Area01_Props_3F.unity に所属している必要があります。\n"
                    + $"現在: {(scene.IsValid() ? scene.path : "シーンなし")}";
                return false;
            }

            if (!scene.isLoaded)
            {
                error = "Field_Area01_Props_3F.unity が読み込まれていません。";
                return false;
            }

            if (!ValidatePrefab(_deskPrefab, DeskAssetPath, "Desk", out error))
                return false;
            if (!ValidatePrefab(_chairPrefab, ChairAssetPath, "Chair", out error))
                return false;
            if (!ValidatePrefab(_workstationPrefab, WorkstationAssetPath, "Workstation", out error))
                return false;

            var referenceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(
                _referenceDesk.gameObject
            );
            if (referenceRoot != _referenceDesk.gameObject)
            {
                error = "基準机にはRectangularTable Prefabインスタンスのルートを指定してください。";
                return false;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(referenceRoot);
            if (source == null || AssetDatabase.GetAssetPath(source) != DeskAssetPath)
            {
                error = "基準机は RectangularTable.glb のPrefabインスタンスではありません。";
                return false;
            }

            foreach (var nodeName in EquipmentNodeNames)
            {
                if (FindDescendant(_workstationPrefab.transform, nodeName) != null)
                    continue;
                error = $"Workstation Prefab内に {nodeName} が見つかりません。";
                return false;
            }

            if (_deskGap.x < 0f || _deskGap.y < 0f)
            {
                error = "机間の追加隙間には0以上の値を指定してください。";
                return false;
            }

            if (_manualDeskPitch.x <= 0f || _manualDeskPitch.y <= 0f)
            {
                error = "机の中心間隔には0より大きい値を指定してください。";
                return false;
            }

            if (_aisleWidth.x < 0f || _aisleWidth.y < 0f)
            {
                error = "島間の通路幅には0以上の値を指定してください。";
                return false;
            }

            if (_seatHorizontalOffset < 0f)
            {
                error = "Seat Horizontal Offsetには0以上の値を指定してください。";
                return false;
            }

            foreach (
                var scale in new[]
                {
                    _deskScale,
                    _chairScale,
                    _monitorScale,
                    _keyboardScale,
                    _mouseScale,
                }
            )
            {
                if (scale.x > 0f && scale.y > 0f && scale.z > 0f)
                    continue;
                error = "Scaleには各軸とも0より大きい値を指定してください。";
                return false;
            }

            return true;
        }

        static bool ValidatePrefab(
            GameObject prefab,
            string requiredPath,
            string label,
            out string error
        )
        {
            error = string.Empty;
            if (prefab == null)
            {
                error = $"{label} Prefabを指定してください。";
                return false;
            }

            if (!EditorUtility.IsPersistent(prefab))
            {
                error = $"{label}にはProject内のPrefabアセットを指定してください。";
                return false;
            }

            var path = AssetDatabase.GetAssetPath(prefab);
            if (path != requiredPath)
            {
                error = $"{label}には次のモデルを指定してください。\n{requiredPath}\n現在: {path}";
                return false;
            }

            return true;
        }

        Vector2 CalculateDeskPitch()
        {
            if (
                _useRendererBounds
                && TryCalculateLocalRendererBounds(_referenceDesk, out var bounds)
            )
            {
                return new Vector2(
                    bounds.size.x * Mathf.Abs(_deskScale.x) + _deskGap.x,
                    bounds.size.z * Mathf.Abs(_deskScale.z) + _deskGap.y
                );
            }

            return _manualDeskPitch;
        }

        void ApplyDeskSet01Reference()
        {
            if (!TryValidate(out var scene, out var error))
            {
                EditorUtility.DisplayDialog("364 机レイアウト", error, "OK");
                return;
            }

            var propsRoot = FindInScene(scene, PropsRootName);
            var deskSet = propsRoot == null ? null : propsRoot.transform.Find("DeskSet_01");
            if (deskSet == null || !TryValidateGeneratedDeskSet(deskSet, out error))
            {
                EditorUtility.DisplayDialog(
                    "364 机レイアウト",
                    deskSet == null ? "参照できるDeskSet_01がありません。" : error,
                    "OK"
                );
                return;
            }

            var desk = deskSet.Find("Desk");
            var seat = deskSet.Find("Seat_01");
            Transform origin;
            Transform chair;
            Transform monitor;
            Transform keyboard;
            Transform mouse;
            if (seat != null)
            {
                origin = seat;
                chair = seat.Find("Chair");
                var pc = seat.Find("PC");
                monitor = pc.Find("Monitor_C");
                keyboard = pc.Find("Keyboard");
                mouse = pc.Find("Mouse");
                _seatHorizontalOffset = Mathf.Abs(
                    Vector3.Dot(seat.position - desk.position, seat.right)
                );
            }
            else
            {
                origin = desk;
                chair = deskSet.Find("Chair");
                monitor = deskSet.Find("Monitor_C");
                keyboard = deskSet.Find("Keyboard");
                mouse = deskSet.Find("Mouse");
            }

            GetRelativePoseWithoutScale(
                origin,
                chair,
                out _chairLocalPosition,
                out _chairLocalEuler
            );
            GetRelativePoseWithoutScale(
                origin,
                monitor,
                out _monitorLocalPosition,
                out _monitorLocalEuler
            );
            GetRelativePoseWithoutScale(
                origin,
                keyboard,
                out _keyboardLocalPosition,
                out _keyboardLocalEuler
            );
            GetRelativePoseWithoutScale(
                origin,
                mouse,
                out _mouseLocalPosition,
                out _mouseLocalEuler
            );
            var mouseRightOffset = Mathf.Abs(_mouseLocalPosition.x - _keyboardLocalPosition.x);
            _chairLocalPosition.x = 0f;
            _monitorLocalPosition.x = 0f;
            _keyboardLocalPosition.x = 0f;
            _mouseLocalPosition.x = mouseRightOffset;
            _deskScale = Abs(desk.lossyScale);
            _chairScale = Abs(chair.lossyScale);
            _monitorScale = Abs(monitor.lossyScale);
            _keyboardScale = Abs(keyboard.lossyScale);
            _mouseScale = Abs(mouse.lossyScale);
            _scaleDefaultsInitialized = true;
            _configurationVersion = CurrentConfigurationVersion;
            Repaint();
            EditorUtility.DisplayDialog(
                "364 机レイアウト",
                "DeskSet_01の1席からScale・前後位置・回転を取得しました。"
                    + "左右位置だけをSeat_01/02へ分けて適用します。",
                "OK"
            );
        }

        static void GetRelativePoseWithoutScale(
            Transform origin,
            Transform target,
            out Vector3 position,
            out Vector3 euler
        )
        {
            position = Quaternion.Inverse(origin.rotation) * (target.position - origin.position);
            euler = (Quaternion.Inverse(origin.rotation) * target.rotation).eulerAngles;
        }

        void ApplyBoundsBasedScaleDefaults(bool showResult)
        {
            if (
                _referenceDesk == null
                || _deskPrefab == null
                || _chairPrefab == null
                || _workstationPrefab == null
            )
            {
                if (showResult)
                    EditorUtility.DisplayDialog(
                        "364 机レイアウト",
                        "基準机と3種類のモデルを指定してください。",
                        "OK"
                    );
                return;
            }

            var workstationDesk = FindDescendant(_workstationPrefab.transform, "Desk");
            var workstationChair = FindDescendant(_workstationPrefab.transform, "ChairPivot");
            var monitor = FindDescendant(_workstationPrefab.transform, "Monitor_C");
            var keyboard = FindDescendant(_workstationPrefab.transform, "Keyboard");
            var mouse = FindDescendant(_workstationPrefab.transform, "Mouse");
            if (
                workstationDesk == null
                || workstationChair == null
                || monitor == null
                || keyboard == null
                || mouse == null
                || !TryCalculateLocalRendererBounds(_deskPrefab.transform, out var deskBounds)
                || !TryCalculateLocalRendererBounds(_chairPrefab.transform, out var chairBounds)
                || !TryCalculateLocalRendererBounds(
                    _workstationPrefab.transform,
                    out var workstationBounds
                )
                || !TryCalculateLocalRendererBounds(workstationDesk, out var workstationDeskBounds)
                || !TryCalculateLocalRendererBounds(
                    workstationChair,
                    out var workstationChairBounds
                )
                || !TryCalculateLocalRendererBounds(monitor, out var monitorBounds)
                || !TryCalculateLocalRendererBounds(keyboard, out var keyboardBounds)
                || !TryCalculateLocalRendererBounds(mouse, out var mouseBounds)
                || !TryCalculateRendererBounds(
                    _workstationPrefab.transform,
                    workstationDesk,
                    out var workstationDeskPlacedBounds
                )
                || !TryCalculateRendererBounds(
                    _workstationPrefab.transform,
                    workstationChair,
                    out var workstationChairPlacedBounds
                )
                || !TryCalculateRendererBounds(
                    _workstationPrefab.transform,
                    monitor,
                    out var monitorPlacedBounds
                )
                || !TryCalculateRendererBounds(
                    _workstationPrefab.transform,
                    keyboard,
                    out var keyboardPlacedBounds
                )
                || !TryCalculateRendererBounds(
                    _workstationPrefab.transform,
                    mouse,
                    out var mousePlacedBounds
                )
            )
            {
                if (showResult)
                    EditorUtility.DisplayDialog(
                        "364 机レイアウト",
                        "モデルからRenderer Boundsを取得できませんでした。",
                        "OK"
                    );
                return;
            }

            _deskScale = Abs(_referenceDesk.lossyScale);
            var targetDeskSize = Vector3.Scale(deskBounds.size, _deskScale);
            var workstationScale = GeometricMean(
                Divide(targetDeskSize, workstationDeskBounds.size)
            );
            var equipmentScale = Vector3.one * workstationScale;
            _monitorScale = equipmentScale;
            _keyboardScale = equipmentScale;
            _mouseScale = equipmentScale;
            _chairScale = Divide(workstationChairBounds.size * workstationScale, chairBounds.size);
            GetRelativePose(workstationDesk, workstationChair, out _, out _chairLocalEuler);
            GetRelativePose(workstationDesk, monitor, out _, out _monitorLocalEuler);
            GetRelativePose(workstationDesk, keyboard, out _, out _keyboardLocalEuler);
            GetRelativePose(workstationDesk, mouse, out _, out _mouseLocalEuler);
            var mappedChairPosition = CalculateMappedLocalPosition(
                deskBounds,
                workstationDeskPlacedBounds,
                workstationChairPlacedBounds,
                chairBounds,
                _chairScale,
                _chairLocalEuler,
                workstationScale,
                false
            );
            var mappedMonitorPosition = CalculateMappedLocalPosition(
                deskBounds,
                workstationDeskPlacedBounds,
                monitorPlacedBounds,
                monitorBounds,
                _monitorScale,
                _monitorLocalEuler,
                workstationScale,
                true
            );
            var mappedKeyboardPosition = CalculateMappedLocalPosition(
                deskBounds,
                workstationDeskPlacedBounds,
                keyboardPlacedBounds,
                keyboardBounds,
                _keyboardScale,
                _keyboardLocalEuler,
                workstationScale,
                true
            );
            var mappedMousePosition = CalculateMappedLocalPosition(
                deskBounds,
                workstationDeskPlacedBounds,
                mousePlacedBounds,
                mouseBounds,
                _mouseScale,
                _mouseLocalEuler,
                workstationScale,
                true
            );
            _seatHorizontalOffset = Mathf.Max(targetDeskSize.x, targetDeskSize.z) * 0.25f;
            _chairLocalPosition = new Vector3(
                0f,
                mappedChairPosition.y * _deskScale.y,
                mappedChairPosition.z * _deskScale.z
            );
            _monitorLocalPosition = new Vector3(
                0f,
                mappedMonitorPosition.y * _deskScale.y,
                mappedMonitorPosition.z * _deskScale.z
            );
            _keyboardLocalPosition = new Vector3(
                0f,
                mappedKeyboardPosition.y * _deskScale.y,
                mappedKeyboardPosition.z * _deskScale.z
            );
            _mouseLocalPosition = new Vector3(
                Mathf.Abs(mappedMousePosition.x - mappedKeyboardPosition.x) * _deskScale.x,
                mappedMousePosition.y * _deskScale.y,
                mappedMousePosition.z * _deskScale.z
            );
            _scaleDefaultsInitialized = true;
            _configurationVersion = CurrentConfigurationVersion;

            if (!showResult)
                return;

            Repaint();
            Debug.Log(
                "[Room364DeskLayout] GLB Renderer Bounds (X, Y, Z)\n"
                    + $"RectangularTable: {FormatSize(deskBounds.size)}\n"
                    + $"Chair: {FormatSize(chairBounds.size)}\n"
                    + $"Workstation全体: {FormatSize(workstationBounds.size)}\n"
                    + $"Workstation/Desk: {FormatSize(workstationDeskBounds.size)}\n"
                    + $"Workstation/ChairPivot: {FormatSize(workstationChairBounds.size)}\n"
                    + $"Monitor_C: {FormatSize(monitorBounds.size)}\n"
                    + $"Keyboard: {FormatSize(keyboardBounds.size)}\n"
                    + $"Mouse: {FormatSize(mouseBounds.size)}\n"
                    + $"推定Workstation Scale: {workstationScale:F3}"
            );
            EditorUtility.DisplayDialog(
                "364 机レイアウト",
                "基準机の現在Scaleを維持し、Workstation内の机・椅子とのBounds比から"
                    + "ChairとPC周辺機器のScale初期値を再計算しました。詳細はConsoleに出力しました。",
                "OK"
            );
        }

        static Vector3 Divide(Vector3 value, Vector3 divisor) =>
            new(
                value.x / Mathf.Max(divisor.x, 0.0001f),
                value.y / Mathf.Max(divisor.y, 0.0001f),
                value.z / Mathf.Max(divisor.z, 0.0001f)
            );

        static Vector3 Abs(Vector3 value) =>
            new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        static float GeometricMean(Vector3 value) =>
            Mathf.Pow(
                Mathf.Max(value.x, 0.0001f)
                    * Mathf.Max(value.y, 0.0001f)
                    * Mathf.Max(value.z, 0.0001f),
                1f / 3f
            );

        static string FormatSize(Vector3 size) => $"{size.x:F3} × {size.y:F3} × {size.z:F3}";

        Vector3 CalculateMappedLocalPosition(
            Bounds targetDeskBounds,
            Bounds sourceDeskBounds,
            Bounds sourceObjectBounds,
            Bounds objectLocalBounds,
            Vector3 objectScale,
            Vector3 objectEuler,
            float workstationScale,
            bool alignToDeskTop
        )
        {
            var scaledDeskBounds = ScaleBounds(targetDeskBounds, _deskScale, Quaternion.identity);
            var scaledObjectBounds = ScaleBounds(
                objectLocalBounds,
                objectScale,
                Quaternion.Euler(objectEuler)
            );
            var desiredCenter =
                scaledDeskBounds.center
                + new Vector3(
                    (sourceObjectBounds.center.x - sourceDeskBounds.center.x) * workstationScale,
                    0f,
                    (sourceObjectBounds.center.z - sourceDeskBounds.center.z) * workstationScale
                );
            var sourceVerticalAnchor = alignToDeskTop
                ? sourceObjectBounds.min.y - sourceDeskBounds.max.y
                : sourceObjectBounds.min.y - sourceDeskBounds.min.y;
            var targetVerticalAnchor = alignToDeskTop
                ? scaledDeskBounds.max.y
                : scaledDeskBounds.min.y;
            var rootPosition = new Vector3(
                desiredCenter.x - scaledObjectBounds.center.x,
                targetVerticalAnchor
                    + sourceVerticalAnchor * workstationScale
                    - scaledObjectBounds.min.y,
                desiredCenter.z - scaledObjectBounds.center.z
            );
            return Divide(rootPosition, _deskScale);
        }

        static Bounds ScaleBounds(Bounds bounds, Vector3 scale, Quaternion rotation)
        {
            var result = default(Bounds);
            var found = false;
            var min = bounds.min;
            var max = bounds.max;
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var point = new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z
                );
                point = rotation * Vector3.Scale(point, scale);
                if (!found)
                {
                    result = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                {
                    result.Encapsulate(point);
                }
            }

            return result;
        }

        void ApplyWorkstationEquipmentOffsets(bool showResult)
        {
            if (_workstationPrefab == null)
            {
                if (showResult)
                    EditorUtility.DisplayDialog(
                        "364 机レイアウト",
                        "Workstation Prefabを指定してください。",
                        "OK"
                    );
                return;
            }

            var desk = FindDescendant(_workstationPrefab.transform, "Desk");
            var monitor = FindDescendant(_workstationPrefab.transform, "Monitor_C");
            var keyboard = FindDescendant(_workstationPrefab.transform, "Keyboard");
            var mouse = FindDescendant(_workstationPrefab.transform, "Mouse");
            if (desk == null || monitor == null || keyboard == null || mouse == null)
            {
                if (showResult)
                    EditorUtility.DisplayDialog(
                        "364 机レイアウト",
                        "Workstation内の Desk・Monitor_C・Keyboard・Mouse を確認できませんでした。",
                        "OK"
                    );
                return;
            }

            GetRelativePose(desk, monitor, out _monitorLocalPosition, out _monitorLocalEuler);
            GetRelativePose(desk, keyboard, out _keyboardLocalPosition, out _keyboardLocalEuler);
            GetRelativePose(desk, mouse, out _mouseLocalPosition, out _mouseLocalEuler);
            _equipmentOffsetsInitialized = true;

            if (showResult)
            {
                Repaint();
                EditorUtility.DisplayDialog(
                    "364 机レイアウト",
                    "Workstation内のDeskを基準に、PC周辺機器の初期位置と回転を取得しました。",
                    "OK"
                );
            }
        }

        static void GetRelativePose(
            Transform origin,
            Transform target,
            out Vector3 position,
            out Vector3 euler
        )
        {
            position = origin.InverseTransformPoint(target.position);
            euler = (Quaternion.Inverse(origin.rotation) * target.rotation).eulerAngles;
        }

        static GameObject CreateGameObject(string name, Scene scene)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            Undo.RegisterCreatedObjectUndo(gameObject, $"{name}を生成");
            return gameObject;
        }

        static void CreateGeneratedMarker(Transform deskSet, Scene scene)
        {
            var marker = CreateGameObject(GeneratedMarkerName, scene);
            Undo.SetTransformParent(
                marker.transform,
                deskSet,
                $"{deskSet.name}に生成マーカーを追加"
            );
        }

        static void CreatePrefabRelativeToDesk(
            GameObject prefab,
            string name,
            Transform desk,
            Transform parent,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 scale,
            Scene scene
        )
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, $"{name}を生成");
            instance.name = name;
            SetPoseRelativeToDesk(instance.transform, desk, localPosition, localEuler);
            Undo.SetTransformParent(instance.transform, parent, $"{name}を机セットの子に移動");
            SetWorldScale(instance.transform, scale, $"{name}のScaleを設定");
        }

        static void CloneWorkstationNodeRelativeToDesk(
            Transform source,
            string name,
            Transform desk,
            Transform parent,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 scale
        )
        {
            var clone = Instantiate(source.gameObject, parent, false);
            clone.name = name;
            Undo.RegisterCreatedObjectUndo(clone, $"{name}を生成");
            SetPoseRelativeToDesk(clone.transform, desk, localPosition, localEuler);
            SetWorldScale(clone.transform, scale, $"{name}のScaleを設定");
        }

        static void SetPoseRelativeToDesk(
            Transform target,
            Transform desk,
            Vector3 localPosition,
            Vector3 localEuler
        )
        {
            target.SetPositionAndRotation(
                desk.TransformPoint(localPosition),
                desk.rotation * Quaternion.Euler(localEuler)
            );
        }

        static void SetWorldScale(Transform target, Vector3 worldScale, string undoName)
        {
            Undo.RecordObject(target, undoName);
            var parentScale = target.parent == null ? Vector3.one : Abs(target.parent.lossyScale);
            target.localScale = Divide(worldScale, parentScale);
        }

        static Transform FindDescendant(Transform root, string name)
        {
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                return root;

            foreach (Transform child in root)
            {
                var found = FindDescendant(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

        static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindDescendant(root.transform, name);
                if (found != null)
                    return found.gameObject;
            }

            return null;
        }

        static bool TryCalculateLocalRendererBounds(Transform root, out Bounds result) =>
            TryCalculateRendererBounds(root, root, out result);

        static bool TryCalculateRendererBounds(
            Transform coordinateRoot,
            Transform rendererRoot,
            out Bounds result
        )
        {
            result = default;
            var found = false;

            foreach (var meshFilter in rendererRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null)
                    continue;
                EncapsulateBounds(
                    coordinateRoot,
                    meshFilter.transform,
                    meshFilter.sharedMesh.bounds,
                    ref result,
                    ref found
                );
            }

            foreach (
                var renderer in rendererRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            )
                EncapsulateBounds(
                    coordinateRoot,
                    renderer.transform,
                    renderer.localBounds,
                    ref result,
                    ref found
                );

            return found;
        }

        static void EncapsulateBounds(
            Transform root,
            Transform child,
            Bounds childBounds,
            ref Bounds result,
            ref bool found
        )
        {
            var min = childBounds.min;
            var max = childBounds.max;
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var childPoint = new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z
                );
                var rootPoint = root.InverseTransformPoint(child.TransformPoint(childPoint));
                if (!found)
                {
                    result = new Bounds(rootPoint, Vector3.zero);
                    found = true;
                }
                else
                {
                    result.Encapsulate(rootPoint);
                }
            }
        }
    }
}
#endif
