using CreativeAI.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 弓の構えで、矢・引き手・矢の通り道を、プレイせずに Scene ビューのギズモで合わせるウィンドウ。
    ///
    /// プレビュー中は、構えのクリップの姿勢・弓・弦・矢を実行時と同じ位置に再現する(<see cref="BowAimPosePreview"/>)。
    /// 動かせるのは次の3つで、どれもプレビューを止めても残る:
    /// - 矢のモデル: プレビューの矢の子をギズモで動かし、「矢の Prefab へ保存」で矢の Prefab に書き込む
    /// - ArrowHold ソケット(引き手。弦と矢の尾が付く位置)
    /// - ArrowRestPoint(弓の子。つがえた矢はここへ向く)
    /// </summary>
    internal sealed class BowAimPoseWindow : EditorWindow
    {
        private enum Phase
        {
            Draw,
            Hold,
        }

        // 引き切った後に構えを保つステート(UpperBody Layer)。Aim の最後から自動で移る
        private const string AimHoldStateName = "AimHold";
        private const string ArrowPrefabField = "_arrowPrefab";

        private PlayerController _player;
        private GameObject _arrowPrefab;
        private Phase _phase = Phase.Draw;
        private float _time;

        private BowAimPosePreview _preview;
        private AnimationClip _drawClip;
        private AnimationClip _holdClip;
        private int _lastSampledHash;
        private bool _suspendedForSave;

        [MenuItem("Tools/CreativeAI/弓の構え調整")]
        private static void Open() => GetWindow<BowAimPoseWindow>("弓の構え調整");

        private bool IsPreviewing => _preview != null;
        private AnimationClip CurrentClip => _phase == Phase.Draw ? _drawClip : _holdClip;

        private void OnEnable()
        {
            FindDefaults();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // 構えた骨や動かした弓が保存されないよう、保存の前に一時停止して元に戻す(保存の後で再開する)
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            PrefabStage.prefabSaving -= OnPrefabSaving;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            StopPreview();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                StopPreview();
        }

        private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) =>
            SuspendForSave();

        private void OnPrefabSaving(GameObject prefab) => SuspendForSave();

        /// <summary>
        /// 保存の直前に姿勢・弓・弦を元に戻す。Prefab モードの自動保存は変更のたびに走るので、止めずに一時停止にとどめる。
        /// 保存は呼び出しの中で終わるので、次の更新(<see cref="OnEditorUpdate"/>)で再開する。
        /// </summary>
        private void SuspendForSave()
        {
            if (!IsPreviewing || _suspendedForSave)
                return;
            _preview.Suspend();
            _suspendedForSave = true;
        }

        /// <summary>開いている Prefab モード、なければシーンから、プレイヤーと矢の Prefab を探す。</summary>
        private void FindDefaults()
        {
            if (_player == null)
            {
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                _player =
                    stage != null
                        ? stage.prefabContentsRoot.GetComponentInChildren<PlayerController>(true)
                        : FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            }
            if (_arrowPrefab == null)
            {
                ArrowPool pool = FindAnyObjectByType<ArrowPool>(FindObjectsInactive.Include);
                if (pool != null)
                {
                    var prefab =
                        new SerializedObject(pool)
                            .FindProperty(ArrowPrefabField)
                            .objectReferenceValue as Component;
                    _arrowPrefab = prefab != null ? prefab.gameObject : null;
                }
            }
        }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(IsPreviewing || EditorApplication.isPlaying))
            {
                _player = (PlayerController)
                    EditorGUILayout.ObjectField(
                        "プレイヤー",
                        _player,
                        typeof(PlayerController),
                        true
                    );
                _arrowPrefab = (GameObject)
                    EditorGUILayout.ObjectField(
                        "矢の Prefab",
                        _arrowPrefab,
                        typeof(GameObject),
                        false
                    );
                if (GUILayout.Button("シーンから探し直す"))
                {
                    _player = null;
                    _arrowPrefab = null;
                    FindDefaults();
                }
            }

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "プレイ中は使えません。止めてから使ってください。",
                    MessageType.Info
                );
                return;
            }

            EditorGUILayout.Space();
            if (!IsPreviewing)
            {
                if (GUILayout.Button("プレビュー開始", GUILayout.Height(28)))
                    StartPreview();
                EditorGUILayout.HelpBox(
                    "開始すると、プレイヤーが構えの姿勢になり、弓と矢が実行時と同じ位置に出ます。"
                        + "止めると姿勢・弓・弦は元に戻ります(動かした矢・ソケット・ArrowRestPoint は残ります)。",
                    MessageType.None
                );
                DrawSocketApplyButton();
                return;
            }

            if (GUILayout.Button("プレビュー停止", GUILayout.Height(28)))
            {
                StopPreview();
                GUIUtility.ExitGUI();
            }

            DrawTimeControls();
            EditorGUILayout.Space();
            DrawArrowSection();
            EditorGUILayout.Space();
            DrawSocketSection();
        }

        private void DrawTimeControls()
        {
            EditorGUI.BeginChangeCheck();
            _phase = (Phase)
                GUILayout.Toolbar(
                    (int)_phase,
                    new[] { $"引く ({_drawClip.name})", $"引き切って保持 ({_holdClip.name})" }
                );
            AnimationClip clip = CurrentClip;
            _time = EditorGUILayout.Slider(
                "再生位置(秒)",
                Mathf.Min(_time, clip.length),
                0f,
                clip.length
            );
            if (EditorGUI.EndChangeCheck())
                Resample();

            if (_phase == Phase.Draw)
            {
                float generate = BowAimPosePreview.GenerateArrowTime(clip);
                float nock = BowAimPosePreview.NockTime(clip);
                EditorGUILayout.LabelField(
                    $"矢を出す {generate:F2} 秒 / つがえる {nock:F2} 秒",
                    _time < generate ? "矢はまだ無い"
                        : _time < nock ? "矢は引き手(ArrowHold)の向き"
                        : "つがえ済み(尾は弦、ArrowRestPoint へ向く)"
                );
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("矢を出した瞬間"))
                        SetTime(generate);
                    if (GUILayout.Button("つがえた瞬間"))
                        SetTime(nock);
                    if (GUILayout.Button("引き切り"))
                        SetTime(clip.length);
                }
            }
        }

        private void SetTime(float time)
        {
            _time = time;
            Resample();
        }

        private void DrawArrowSection()
        {
            EditorGUILayout.LabelField("矢のモデル", EditorStyles.boldLabel);
            GameObject arrow = _preview.PreviewArrow;
            if (arrow == null)
            {
                EditorGUILayout.HelpBox("矢の Prefab が未設定です。", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "矢の原点(ルート)は弦に掛かる位置です。ルートは毎回置き直すので、動かすのは子(モデル・先端)にしてください。",
                MessageType.None
            );
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (Transform child in arrow.transform)
                {
                    if (GUILayout.Button($"「{child.name}」を選択"))
                        Select(child);
                }
            }
            if (GUILayout.Button("尾を原点(弦)に合わせる"))
            {
                Undo.RegisterFullObjectHierarchyUndo(arrow, "Align Arrow Tail");
                _preview.AlignArrowTailToOrigin();
                Resample();
            }

            bool unsaved = _preview.HasUnsavedArrowChanges();
            using (new EditorGUI.DisabledScope(!unsaved))
            {
                if (
                    GUILayout.Button(
                        unsaved ? "矢の Prefab へ保存（未保存の変更あり）" : "矢の Prefab へ保存"
                    )
                )
                {
                    if (_preview.SaveArrowToPrefab())
                    {
                        // 保存した Prefab から矢を作り直し、表示と保存内容を一致させる
                        _preview.SetArrowPrefab(_arrowPrefab);
                        Resample();
                    }
                }
            }
        }

        private void DrawSocketSection()
        {
            EditorGUILayout.LabelField("引き手と矢の通り道", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("ArrowHold（引き手）を選択"))
                    Select(_preview.ArrowHold);
                if (GUILayout.Button("ArrowRestPoint（弓）を選択"))
                    Select(_preview.ArrowRest);
            }
            EditorGUILayout.HelpBox(
                "Prefab モードでは、保存(自動保存を含む)で Prefab に入ります。保存の間だけ構えを外すので、構えの骨は保存されません。\n"
                    + "シーンのプレイヤーで調整した時は、プレビューを止めてから、出てくるボタンで Prefab へ反映してください"
                    + "(Overrides メニューで構えたまま反映すると、構えの骨まで Prefab に入ります)。",
                MessageType.None
            );
        }

        private void DrawSocketApplyButton()
        {
            if (_player == null || !PrefabUtility.IsPartOfPrefabInstance(_player))
                return;
            if (GUILayout.Button("ArrowHold と ArrowRestPoint をプレイヤーの Prefab へ反映"))
            {
                BowAimPosePreview preview = BowAimPosePreview.TryCreate(_player, out string error);
                if (preview == null)
                    Debug.LogError($"[弓の構え調整] {error}", _player);
                else if (preview.ApplySocketsToPrefab(out string message))
                    Debug.Log($"[弓の構え調整] ArrowHold と ArrowRestPoint を {message}", _player);
                else
                    Debug.LogError($"[弓の構え調整] {message}", _player);
            }
        }

        private static void Select(Transform t)
        {
            Selection.activeTransform = t;
            EditorGUIUtility.PingObject(t);
            Tools.current = Tool.Move;
        }

        private void StartPreview()
        {
            if (_player == null)
            {
                Debug.LogError("[弓の構え調整] プレイヤーが未設定です");
                return;
            }
            if (!FindClips(out string clipError))
            {
                Debug.LogError($"[弓の構え調整] {clipError}", _player);
                return;
            }

            _preview = BowAimPosePreview.TryCreate(_player, out string error);
            if (_preview == null)
            {
                Debug.LogError($"[弓の構え調整] {error}", _player);
                return;
            }
            _preview.Start(_arrowPrefab);
            _time = Mathf.Min(_time, CurrentClip.length);
            Resample();
        }

        private void StopPreview()
        {
            if (_preview == null)
                return;
            _preview.Stop();
            _preview = null;
            _suspendedForSave = false;
            SceneView.RepaintAll();
            Repaint();
        }

        /// <summary>プレイヤーの AnimatorController の上半身レイヤーから、構え(Aim)と保持(AimHold)のクリップを取る。</summary>
        private bool FindClips(out string error)
        {
            _drawClip = null;
            _holdClip = null;
            Animator animator = PlayerModel.Of(_player).Animator;
            RuntimeAnimatorController runtime = animator.runtimeAnimatorController;
            var overrides = runtime as AnimatorOverrideController;
            var controller =
                (overrides != null ? overrides.runtimeAnimatorController : runtime)
                as AnimatorController;
            if (controller == null)
            {
                error = $"{animator.name} の Animator に AnimatorController がありません";
                return false;
            }

            int holdHash = Animator.StringToHash(AimHoldStateName);
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != PlayerAnimatorStates.UpperBodyLayerName)
                    continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    var clip = child.state.motion as AnimationClip;
                    if (clip != null && overrides != null)
                        clip = overrides[clip];
                    if (child.state.nameHash == PlayerAnimatorStates.Aim)
                        _drawClip = clip;
                    else if (child.state.nameHash == holdHash)
                        _holdClip = clip;
                }
            }

            error =
                _drawClip == null
                    ? $"「{PlayerAnimatorStates.UpperBodyLayerName}」に Aim ステートのクリップがありません"
                : _holdClip == null
                    ? $"「{PlayerAnimatorStates.UpperBodyLayerName}」に {AimHoldStateName} ステートのクリップがありません"
                : null;
            return error == null;
        }

        private void Resample()
        {
            if (!IsPreviewing)
                return;
            _preview.Sample(CurrentClip, _time, alwaysNocked: _phase == Phase.Hold);
            _lastSampledHash = _preview.WatchedStateHash();
            SceneView.RepaintAll();
            Repaint();
        }

        // ギズモや Undo で、ソケット・ArrowRestPoint・矢の子が動いたら置き直す(弦や矢が付いてくるように)
        private void OnEditorUpdate()
        {
            if (!IsPreviewing)
                return;
            if (_player == null || _preview.PreviewArrow == null && _arrowPrefab != null)
            {
                // シーンを閉じた・プレビューの矢が消された
                StopPreview();
                return;
            }
            if (_suspendedForSave)
            {
                _suspendedForSave = false;
                _preview.Resume();
                Resample();
                return;
            }
            if (_preview.WatchedStateHash() != _lastSampledHash)
                Resample();
        }
    }
}
