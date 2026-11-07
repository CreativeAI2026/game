using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 弓の構えを、プレイせずに編集中のシーンへ再現する(<see cref="BowAimPoseWindow"/> から使う)。
    ///
    /// 姿勢は AnimationMode で付けるので、止めると骨は元に戻り、シーンやプレハブに構えの姿勢は残らない。
    /// 弓(実行時はソケットへ付け替わる)と弦の引き点は、開始時の値を控えておき、止めた時に戻す。
    /// 矢は保存されない一時オブジェクトとして出し、実行時(BowController)と同じ置き方をする。
    /// </summary>
    internal sealed class BowAimPosePreview
    {
        // BowController / ArrowPool / PlayerSocketAttachment の、Inspector にだけ出している参照の名前
        private const string StringNockBoneField = "_stringNockBone";
        private const string AttachSocketField = "_socket";

        // 構えクリップの中で、矢を出す位置とつがえる位置(アニメーションイベント名)
        private const string GenerateArrowEvent = "GenerateArrow";
        private const string NockEvent = "DrawComplete";

        private const string PreviewArrowName = "【プレビュー】矢 (保存されません)";

        private readonly PlayerController _player;
        private readonly Transform _modelRoot;
        private readonly Transform _bow;
        private readonly Transform _bowGrip;
        private readonly Transform _stringNockBone;

        private TransformSnapshot[] _restore = System.Array.Empty<TransformSnapshot>();
        private GameObject _arrowPrefab;
        private GameObject _previewArrow;
        private Vector3 _modelLocalPosition;
        private Quaternion _modelLocalRotation;

        public Transform ArrowHold { get; }
        public Transform ArrowRest { get; }

        /// <summary>プレビュー中の矢(つがえる前の時間では非表示)。</summary>
        public GameObject PreviewArrow => _previewArrow;

        private BowAimPosePreview(PlayerController player, BowController bow)
        {
            _player = player;
            PlayerModel model = PlayerModel.Of(bow);
            _modelRoot = model.Animator.transform;
            _bow = bow.transform;
            ArrowRest = bow.ArrowRestPoint;
            ArrowHold = model.GetSocket(PlayerSocketId.ArrowHold);
            _stringNockBone = (Transform)
                new SerializedObject(bow).FindProperty(StringNockBoneField).objectReferenceValue;

            var attachment = bow.GetComponent<PlayerSocketAttachment>();
            var socketId = (PlayerSocketId)
                new SerializedObject(attachment).FindProperty(AttachSocketField).enumValueIndex;
            _bowGrip = model.GetSocket(socketId);
        }

        /// <summary>プレイヤーに弓が揃っていればプレビューを作る。足りなければ理由を返す。</summary>
        public static BowAimPosePreview TryCreate(PlayerController player, out string error)
        {
            BowController bow = player.GetComponentInChildren<BowController>(true);
            if (bow == null)
            {
                error = $"{player.name} の子に BowController がありません";
                return null;
            }
            if (bow.GetComponent<PlayerSocketAttachment>() == null)
            {
                error = $"{bow.name} に PlayerSocketAttachment がありません";
                return null;
            }

            var preview = new BowAimPosePreview(player, bow);
            error =
                preview.ArrowHold == null ? "ArrowHold ソケットがありません"
                : preview.ArrowRest == null ? "BowController の ArrowRestPoint が未設定です"
                : preview._stringNockBone == null ? "BowController の弦の骨が未設定です"
                : preview._bowGrip == null ? "弓の持ち手ソケットがありません"
                : null;
            return error == null ? preview : null;
        }

        public void Start(GameObject arrowPrefab)
        {
            Resume();
            SetArrowPrefab(arrowPrefab);
        }

        public void Stop()
        {
            Suspend();
            DestroyPreviewArrow();
        }

        /// <summary>
        /// 構えの姿勢・弓・弦を元に戻す。プレビューの矢(動かした子を含む)は残す。
        /// 保存の直前に呼び、構えの骨や持ち手へ動かした弓がシーン・Prefab に保存されないようにする。
        /// </summary>
        public void Suspend()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            foreach (TransformSnapshot snapshot in _restore)
                snapshot.Restore();
            _restore = System.Array.Empty<TransformSnapshot>();
        }

        /// <summary><see cref="Suspend"/> から戻す。姿勢は次の <see cref="Sample"/> で付く。</summary>
        public void Resume()
        {
            // 弓と弦の引き点は AnimationMode で動かす物ではない(元へ戻してくれない)ので、元の値を控えて Suspend で戻す
            _restore = new[]
            {
                new TransformSnapshot(_modelRoot),
                new TransformSnapshot(_bow),
                new TransformSnapshot(_stringNockBone),
            };
            _modelRoot.GetLocalPositionAndRotation(out _modelLocalPosition, out _modelLocalRotation);
            AnimationMode.StartAnimationMode();
        }

        private readonly struct TransformSnapshot
        {
            private readonly Transform _target;
            private readonly Vector3 _localPosition;
            private readonly Quaternion _localRotation;

            public TransformSnapshot(Transform target)
            {
                _target = target;
                target.GetLocalPositionAndRotation(out _localPosition, out _localRotation);
            }

            public void Restore()
            {
                if (_target != null)
                    _target.SetLocalPositionAndRotation(_localPosition, _localRotation);
            }
        }

        /// <summary>見た目確認用の矢を作り直す(Prefab が差し替わった時・保存した時)。</summary>
        public void SetArrowPrefab(GameObject arrowPrefab)
        {
            DestroyPreviewArrow();
            _arrowPrefab = arrowPrefab;
            if (arrowPrefab == null)
                return;

            _previewArrow = Object.Instantiate(arrowPrefab);
            _previewArrow.name = PreviewArrowName;
            // Prefab モードで開いている時は、そちらのシーンに置かないと Scene ビューに映らない
            SceneManager.MoveGameObjectToScene(_previewArrow, _player.gameObject.scene);
            // シーンに保存させない。矢のスクリプトは Edit モードでは動かないので外さなくてよい
            foreach (Transform t in _previewArrow.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
        }

        /// <summary>
        /// clip の time 秒の姿勢を作り、弓・弦・矢を実行時と同じ場所に置く。
        /// alwaysNocked は、つがえた後の保持(AimHold)のクリップのとき true。
        /// </summary>
        public void Sample(AnimationClip clip, float time, bool alwaysNocked)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_modelRoot.gameObject, clip, time);
            // クリップのルート移動でモデルがずれないよう戻す(実行時は Root Motion OFF)
            _modelRoot.SetLocalPositionAndRotation(_modelLocalPosition, _modelLocalRotation);
            AnimationMode.EndSampling();

            // PlayerSocketAttachment: 弓は持ち手ソケットの位置・向きに付く
            _bow.SetPositionAndRotation(_bowGrip.position, _bowGrip.rotation);

            bool hasArrow = alwaysNocked || time >= EventTime(clip, GenerateArrowEvent, 0f);
            bool nocked = alwaysNocked || time >= EventTime(clip, NockEvent, clip.length);

            // BowStringPull: つがえた後は弦の引き点が引き手に付く
            if (nocked)
                _stringNockBone.position = ArrowHold.position;

            if (_previewArrow != null)
            {
                _previewArrow.SetActive(hasArrow);
                if (hasArrow)
                    PlaceArrow(nocked);
            }
        }

        /// <summary>BowController.GenerateArrow / PlaceNockedArrow と同じ置き方。</summary>
        private void PlaceArrow(bool nocked)
        {
            // 実行時の矢はプールで Prefab の大きさのまま作られ、手や弓の子になっても見た目の大きさを保つ
            Transform arrow = _previewArrow.transform;
            arrow.localScale = _arrowPrefab.transform.localScale;

            if (!nocked)
            {
                arrow.SetPositionAndRotation(ArrowHold.position, ArrowHold.rotation);
                return;
            }

            Vector3 nock = _stringNockBone.position;
            Vector3 toRest = ArrowRest.position - nock;
            Quaternion rotation =
                toRest.sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(toRest, ArrowRest.up)
                    : arrow.rotation;
            arrow.SetPositionAndRotation(nock, rotation);
        }

        /// <summary>
        /// 矢の見た目の部品(ルートの直下の子)をまとめて前後へずらし、いちばん後ろ(尾)を矢の原点に合わせる。
        /// 矢の原点は弦に掛かる位置なので、尾がここより後ろにあると引き手の腕を突き抜ける。
        /// 先端の当たり判定(子)も一緒にずらすので、先端と見た目の位置関係は変わらない。
        /// </summary>
        public void AlignArrowTailToOrigin()
        {
            if (_previewArrow == null)
                return;

            Transform root = _previewArrow.transform;
            float tailZ = float.PositiveInfinity;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    // バウンディングボックスの8隅を、矢のルートから見た座標にして、いちばん後ろを探す
                    Vector3 corner = b.center + Vector3.Scale(b.extents, Corner(i));
                    Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    tailZ = Mathf.Min(tailZ, local.z);
                }
            }
            if (float.IsInfinity(tailZ))
                return;

            foreach (Transform child in root)
                child.localPosition -= new Vector3(0f, 0f, tailZ);
        }

        private static Vector3 Corner(int i) =>
            new((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);

        /// <summary>プレビューの矢で動かした子の位置・向き・大きさを、矢の Prefab へ書き込む。</summary>
        public bool SaveArrowToPrefab()
        {
            if (_previewArrow == null)
                return false;

            string path = AssetDatabase.GetAssetPath(_arrowPrefab);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform previewChild in _previewArrow.transform)
                {
                    Transform target = contents.transform.Find(previewChild.name);
                    if (target == null)
                    {
                        Debug.LogError($"[弓の構え調整] {path} に子「{previewChild.name}」がありません");
                        continue;
                    }
                    previewChild.GetLocalPositionAndRotation(out Vector3 p, out Quaternion r);
                    target.SetLocalPositionAndRotation(p, r);
                    target.localScale = previewChild.localScale;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            return true;
        }

        /// <summary>プレビューの矢の子が、矢の Prefab と違う位置・向き・大きさになっているか。</summary>
        public bool HasUnsavedArrowChanges()
        {
            if (_previewArrow == null || _arrowPrefab == null)
                return false;
            foreach (Transform child in _previewArrow.transform)
            {
                Transform source = _arrowPrefab.transform.Find(child.name);
                if (
                    source == null
                    || child.localPosition != source.localPosition
                    || child.localRotation != source.localRotation
                    || child.localScale != source.localScale
                )
                    return true;
            }
            return false;
        }

        /// <summary>
        /// ArrowHold ソケットと ArrowRestPoint の位置・向きを、シーンのプレイヤーの元の Prefab へ反映する。
        /// プレビュー中に呼ぶと構えの骨まで Prefab に入るおそれがあるので、止めてから呼ぶ。
        /// </summary>
        public bool ApplySocketsToPrefab(out string message)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(_player))
            {
                message = "シーンの Prefab インスタンスではありません。Prefab モードなら Prefab を保存してください";
                return false;
            }

            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                PrefabUtility.GetOutermostPrefabInstanceRoot(_player)
            );
            foreach (Transform t in new[] { ArrowHold, ArrowRest })
                PrefabUtility.ApplyObjectOverride(t, path, InteractionMode.UserAction);
            message = $"{path} へ反映しました";
            return true;
        }

        /// <summary>見張っている値(時間以外)から、作り直しが要るかを判定するための値を作る。</summary>
        public int WatchedStateHash()
        {
            var hash = new System.HashCode();
            AddLocal(ref hash, ArrowHold);
            AddLocal(ref hash, ArrowRest);
            if (_previewArrow != null)
            {
                foreach (Transform child in _previewArrow.transform)
                    AddLocal(ref hash, child);
            }
            return hash.ToHashCode();
        }

        private static void AddLocal(ref System.HashCode hash, Transform t)
        {
            hash.Add(t.localPosition);
            hash.Add(t.localRotation);
            hash.Add(t.localScale);
        }

        private void DestroyPreviewArrow()
        {
            if (_previewArrow != null)
                Object.DestroyImmediate(_previewArrow);
            _previewArrow = null;
        }

        private static float EventTime(AnimationClip clip, string functionName, float fallback)
        {
            foreach (AnimationEvent evt in AnimationUtility.GetAnimationEvents(clip))
            {
                if (evt.functionName == functionName)
                    return evt.time;
            }
            return fallback;
        }

        /// <summary>この時間に矢を出す・つがえる(イベントの時間)。スライダーの目印用。</summary>
        public static float GenerateArrowTime(AnimationClip clip) => EventTime(clip, GenerateArrowEvent, 0f);

        public static float NockTime(AnimationClip clip) => EventTime(clip, NockEvent, clip.length);
    }
}
