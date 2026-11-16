#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreativeAI.Core;
using CreativeAI.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// dev_state を編集して「好きなイベントの直前の状態」から Play するためのウィンドウ。保存先は個人用の
    /// dev_state.local.json(.gitignore 済み)で、開始位置など人ごとに違う設定も、ここなら git に入らない。
    /// イベントを選ぶと発火条件を満たす進行度・フラグ・大事なものを自動で入れる(手でも直せる)。
    /// 反映は Field シーンを直接 Play したとき(FieldDevBootstrap)。Title 経由では使われない。
    /// </summary>
    public sealed class DevStateWindow : EditorWindow
    {
        private const string EventDataDir = "Assets/_Project/Features/Event/Data";
        private const string EventsScenePath =
            "Assets/_Project/Scenes/Field/Field_Area01_Events.unity";

        // Events シーンのトリガーは、床から この高さに置く決まり(どのイベントも同じ)。
        private const float TriggerHeightAboveFloor = 4f;
        private static readonly string[] WeaponKeys = { "sword", "bow", "scythe", "hammer" };

        private List<EventDefinition> _events = new();
        private string[] _eventLabels = Array.Empty<string>();
        private int _eventIndex;
        private bool _includeEarlierRewards = true;
        private bool _setSpawnFromEvent = true;
        private DevState _state = new() { hp = DevStateBuilder.DefaultHp };
        private string _itemsText = string.Empty;
        private Vector2 _scroll;
        private string _status;

        [MenuItem("Tools/CreativeAI/Dev State Window")]
        public static void Open() => GetWindow<DevStateWindow>("Dev State");

        private void OnEnable()
        {
            ReloadEvents();
            LoadFromFile();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Field シーンを開いて Play すると、ここで保存した状態から始まります(Title 経由では使われません)。",
                MessageType.Info
            );

            DrawEventPicker();
            EditorGUILayout.Space();
            DrawManualFields();
            EditorGUILayout.Space();
            DrawButtons();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void DrawEventPicker()
        {
            EditorGUILayout.LabelField("イベントの直前から始める", EditorStyles.boldLabel);
            if (_events.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"{EventDataDir} にイベントがありません。Tools > CreativeAI > Import Events を実行してください。",
                    MessageType.Warning
                );
                return;
            }

            _eventIndex = Mathf.Clamp(_eventIndex, 0, _events.Count - 1);
            _eventIndex = EditorGUILayout.Popup("イベント", _eventIndex, _eventLabels);
            _includeEarlierRewards = EditorGUILayout.ToggleLeft(
                "それ以前のイベントで渡される武器・アイテムも持たせる",
                _includeEarlierRewards
            );
            _setSpawnFromEvent = EditorGUILayout.ToggleLeft(
                "開始位置もそのイベントの位置にする",
                _setSpawnFromEvent
            );
            if (GUILayout.Button("このイベントの発火直前の状態にする"))
            {
                _state = DevStateBuilder.ForEvent(
                    _events[_eventIndex],
                    _events,
                    _includeEarlierRewards,
                    _state
                );
                _itemsText = string.Join(", ", _state.importantItems);
                _status =
                    $"「{_events[_eventIndex].Id}」の直前の状態を入れました(まだ保存していません)。";
                if (_setSpawnFromEvent)
                    ApplyEventSpawn(_events[_eventIndex]);
            }
        }

        private void DrawManualFields()
        {
            EditorGUILayout.LabelField("状態(手で直せます)", EditorStyles.boldLabel);
            _state.progress = Mathf.Max(0, EditorGUILayout.IntField("進行度", _state.progress));

            EditorGUILayout.LabelField("フラグ(キー / 値)");
            var flags = (_state.flags ?? Array.Empty<DevState.FlagEntry>()).ToList();
            int removeAt = -1;
            for (int i = 0; i < flags.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                flags[i].key = EditorGUILayout.TextField(flags[i].key);
                flags[i].value = EditorGUILayout.TextField(flags[i].value);
                if (GUILayout.Button("−", GUILayout.Width(24f)))
                    removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0)
                flags.RemoveAt(removeAt);
            if (GUILayout.Button("フラグを追加", GUILayout.Width(120f)))
                flags.Add(new DevState.FlagEntry { key = string.Empty, value = string.Empty });
            _state.flags = flags.ToArray();

            _itemsText = EditorGUILayout.TextField("大事なもの(カンマ区切り)", _itemsText);

            EditorGUILayout.LabelField("武器");
            var owned = new HashSet<string>(_state.weapons ?? Array.Empty<string>());
            EditorGUILayout.BeginHorizontal();
            foreach (string key in WeaponKeys)
            {
                bool has = EditorGUILayout.ToggleLeft(
                    key,
                    owned.Contains(key),
                    GUILayout.Width(80f)
                );
                if (has)
                    owned.Add(key);
                else
                    owned.Remove(key);
            }
            EditorGUILayout.EndHorizontal();
            _state.weapons = WeaponKeys.Where(owned.Contains).ToArray();

            var choices = new[] { string.Empty }.Concat(_state.weapons).ToArray();
            int selected = Mathf.Max(0, Array.IndexOf(choices, _state.selectedWeapon));
            selected = EditorGUILayout.Popup("選んでおく武器", selected, choices);
            _state.selectedWeapon = choices[selected];

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("開始位置(自分だけの設定)", EditorStyles.boldLabel);
            _state.useSpawn = EditorGUILayout.Toggle("開始位置を指定する", _state.useSpawn);
            using (new EditorGUI.DisabledScope(!_state.useSpawn))
            {
                _state.spawnPosition = EditorGUILayout.Vector3Field(
                    "位置(足元)",
                    _state.spawnPosition
                );
                _state.spawnYaw = EditorGUILayout.FloatField("向き(Y 角度)", _state.spawnYaw);
                var selectedObject = Selection.activeTransform;
                using (new EditorGUI.DisabledScope(selectedObject == null))
                {
                    if (GUILayout.Button("Hierarchy で選んだオブジェクトの位置・向きにする"))
                    {
                        _state.spawnPosition = selectedObject.position;
                        _state.spawnYaw = selectedObject.eulerAngles.y;
                    }
                }
            }

            _state.hp = Mathf.Max(0f, EditorGUILayout.FloatField("HP", _state.hp));
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存"))
                Save();
            if (GUILayout.Button("保存して Play") && Save())
                EditorApplication.EnterPlaymode();
            if (GUILayout.Button("ファイルから読み直す"))
                LoadFromFile();
            if (GUILayout.Button("イベント一覧を更新"))
                ReloadEvents();
            EditorGUILayout.EndHorizontal();
        }

        private bool Save()
        {
            _state.importantItems = _itemsText
                .Split(',')
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            _state.flags = _state
                .flags.Where(flag => flag != null && !string.IsNullOrWhiteSpace(flag.key))
                .ToArray();
            try
            {
                File.WriteAllText(DevState.LocalAssetPath, JsonUtility.ToJson(_state, true));
                AssetDatabase.ImportAsset(DevState.LocalAssetPath);
                _status = $"保存しました(自分用・git に入りません): {DevState.LocalAssetPath}";
                return true;
            }
            catch (Exception e)
            {
                _status = $"保存に失敗しました: {e.Message}";
                return false;
            }
        }

        private void LoadFromFile()
        {
            try
            {
                var path = DevState.ResolvePath();
                if (File.Exists(path))
                {
                    var loaded = DevState.Parse(File.ReadAllText(path));
                    if (loaded != null)
                        _state = loaded;
                }
            }
            catch (Exception e)
            {
                _status = $"読み込みに失敗しました: {e.Message}";
            }
            if (!_state.HasHp)
                _state.hp = DevStateBuilder.DefaultHp;
            _itemsText = string.Join(", ", _state.importantItems ?? Array.Empty<string>());
        }

        // 選んだイベントのトリガーの位置を、開始位置に入れる(床の高さに直して)。
        private void ApplyEventSpawn(EventDefinition ev)
        {
            if (!TryGetTriggerFloor(ev, out var floor))
            {
                _status +=
                    $"\n開始位置: 「{ev.Id}」のトリガーが {EventsScenePath} に見つかりません。";
                return;
            }
            _state.useSpawn = true;
            _state.spawnPosition = floor;
            _status += $"\n開始位置をイベントの位置にしました: {floor}";
        }

        /// <summary>
        /// Events シーンから、そのイベントを発火するトリガーを探して、足元(床)の座標を返す。
        /// Events シーンのトリガーはどれも床から <see cref="TriggerHeightAboveFloor"/> 上に置く決まりなので、その分を引く。
        /// シーンが開いていなければ、読むあいだだけ Additive で開いて閉じる。
        /// </summary>
        private static bool TryGetTriggerFloor(EventDefinition ev, out Vector3 floor)
        {
            floor = default;
            // 再生中はシーンを開け閉めできない。
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(EventsScenePath))
                return false;

            var scene = SceneManager.GetSceneByPath(EventsScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(EventsScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var trigger in root.GetComponentsInChildren<EventTrigger>(true))
                    {
                        // フィールド名が変わった場合などで見つからなくても、例外にせず次のトリガーを見る。
                        var eventProperty = new SerializedObject(trigger).FindProperty("_event");
                        if (eventProperty == null || eventProperty.objectReferenceValue != ev)
                            continue;
                        floor = trigger.transform.position - Vector3.up * TriggerHeightAboveFloor;
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private void ReloadEvents()
        {
            var loaded = AssetDatabase
                .FindAssets("t:EventDefinition", new[] { EventDataDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EventDefinition>)
                .Where(ev => ev != null);
            _events = DevStateBuilder.SortByProgress(loaded);
            _eventLabels = _events
                .Select(ev => $"[進行度 {DevStateBuilder.StartProgress(ev)}] {ev.Id}")
                .ToArray();
        }
    }
}
#endif
