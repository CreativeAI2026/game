using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 【仮アニメーションの間だけ】弓の持ち方を Edit モードの Scene ビューで合わせるための調整用コンポーネント。弓に付ける。
    /// TBossAttack2Tuner と同じく、ON の間だけプレイヤーにクリップを当てて姿勢を作り、OFF で元の姿勢に戻す。
    ///
    /// - 弓: 持ち手ソケットに付いた位置(実行時と同じ)に置かれる。ギズモで動かすと MockWeaponPose の今の姿勢(構え/通常)の値に取り込む
    /// - 手に持った矢(つがえる前): 引き手ソケット(ArrowHold)をギズモで動かす
    /// - つがえた矢: 尾は引き手ソケット(弦が付いていく)。向きは弓の子の ArrowRestPoint をギズモで動かして合わせる
    /// 矢は、再生位置がつがえる前なら手に、後なら弓に、実行時と同じ大きさで描く(選択はできないので、ソケットを選んで動かす)。
    /// 値はシーンのプレイヤーに入るので、合わせたらプレハブへ Apply する。専用アニメーションが来たらこのコンポーネントごと外す。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MockWeaponPose))]
    [RequireComponent(typeof(BowController))]
    public class BowGripTuner : MonoBehaviour
    {
        [Tooltip("構えのクリップ(UpperBody の Aim ステートのもの)")]
        [SerializeField]
        private AnimationClip _aimClip;

        [Tooltip("構えていない時のクリップ(待機)")]
        [SerializeField]
        private AnimationClip _idleClip;

        [Tooltip("矢の Prefab(見た目の確認用)")]
        [SerializeField]
        private GameObject _arrowPrefab;

        [Tooltip("ON にすると Edit モードで姿勢を作る。OFF で元の姿勢へ戻す")]
        [SerializeField]
        private bool _previewEnabled;

        [Tooltip("どちらの姿勢を合わせるか。弓の値は姿勢ごとに別に持つ")]
        [SerializeField]
        private MockWeaponPoseKey _previewPose = MockWeaponPoseKey.Aim;

        [Tooltip("構えクリップの再生位置（0 引き始め 〜 1 引き切り）。矢をつがえる前は矢が手に付く")]
        [Range(0f, 1f)]
        [SerializeField]
        private float _previewNormalizedTime = 1f;

#if UNITY_EDITOR
        // 構えクリップの中で矢をつがえる位置(DrawComplete イベント)。イベントが無ければ引き切りでつがえる
        private const string NockEventName = "DrawComplete";

        private bool _isPreviewing;
        private Vector3 _placedPosition;
        private Quaternion _placedRotation;

        private bool IsAiming => _previewPose == MockWeaponPoseKey.Aim;

        private void OnDisable() => StopPreview();

        private void OnValidate()
        {
            // Inspector の変更はその場で反映する(Edit モードの Update はシーンに変化がある時しか呼ばれない)
            UnityEditor.EditorApplication.delayCall += Refresh;
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            if (this == null)
                return;

            AnimationClip clip = IsAiming ? _aimClip : _idleClip;
            if (Application.isPlaying || !_previewEnabled || clip == null)
            {
                StopPreview();
                return;
            }

            Transform modelRoot = PlayerModel.Of(this).Animator.transform;
            modelRoot.GetLocalPositionAndRotation(out Vector3 position, out Quaternion rotation);
            clip.SampleAnimation(modelRoot.gameObject, IsAiming ? _previewNormalizedTime * clip.length : 0f);
            // クリップのルート移動でモデルがずれないよう戻す
            modelRoot.SetLocalPositionAndRotation(position, rotation);

            if (!_isPreviewing)
            {
                // 初回は保存済みの値の位置に置くだけにする(元の置き場所を「動かした」と判定しないため)
                PlaceBow();
                _isPreviewing = true;
            }
            else
            {
                SyncBow();
            }
            UnityEditor.SceneView.RepaintAll();
        }

        /// <summary>
        /// 骨をモデルの元ファイルの姿勢に、弓をプレハブの値に戻す。
        /// 元の姿勢を覚えておく方式だと、再コンパイル等で覚えた値が消えた時に戻せなくなるため。
        /// 骨の戻し先をプレハブにしないのは、構えたままプレハブへ Apply されると戻し先ごと構えになってしまうため。
        /// ソケットは Humanoid の骨ではないので戻さない(調整した値は残る)。
        /// </summary>
        private void StopPreview()
        {
            if (!_isPreviewing)
                return;
            _isPreviewing = false;

            ResetBonesToModelFile(PlayerModel.Of(this).Animator);
            Transform bowSource = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(transform);
            if (bowSource != null)
                transform.SetLocalPositionAndRotation(bowSource.localPosition, bowSource.localRotation);
            UnityEditor.SceneView.RepaintAll();
        }

        /// <summary>Humanoid の骨を、Avatar の元になったモデルファイル(FBX)の姿勢に戻す。</summary>
        public static void ResetBonesToModelFile(Animator animator)
        {
            string modelPath = UnityEditor.AssetDatabase.GetAssetPath(animator.avatar);
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"[BowGripTuner] Avatar の元のモデルが見つからず、姿勢を戻せません: {modelPath}", animator);
                return;
            }

            for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
            {
                Transform t = animator.GetBoneTransform(bone);
                if (t == null)
                    continue;
                Transform source = model.transform.Find(UnityEditor.AnimationUtility.CalculateTransformPath(t, animator.transform));
                if (source != null)
                    t.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            }
        }

        /// <summary>
        /// 弓がギズモで動かされていたら、その位置を持ち手ソケットからの値に直して今の姿勢に取り込む。
        /// その後、値どおりの位置(実行時と同じ)に置き直す。動かしていなければ姿勢の変化(手の移動)にだけ追従する。
        /// </summary>
        private void SyncBow()
        {
            bool moved =
                (transform.position - _placedPosition).sqrMagnitude > 1e-10f
                || Quaternion.Angle(transform.rotation, _placedRotation) > 0.01f;
            if (moved)
            {
                Transform grip = PlayerModel.Of(this).GetSocket(PlayerSocketId.BowGrip);
                GetComponent<MockWeaponPose>()
                    .EditorSetPoseValues(
                        _previewPose,
                        (Quaternion.Inverse(grip.rotation) * transform.rotation).eulerAngles,
                        grip.InverseTransformPoint(transform.position)
                    );
            }
            PlaceBow();
        }

        private void PlaceBow()
        {
            Transform grip = PlayerModel.Of(this).GetSocket(PlayerSocketId.BowGrip);
            GetComponent<MockWeaponPose>().GetPoseValues(_previewPose, out Vector3 euler, out Vector3 offset);
            transform.SetPositionAndRotation(grip.TransformPoint(offset), grip.rotation * Quaternion.Euler(euler));
            transform.GetPositionAndRotation(out _placedPosition, out _placedRotation);
        }

        /// <summary>矢を、実行時と同じ位置・大きさで描く(つがえる前は手、後は尾を引き手の弦に掛けて矢の通り道へ向ける)。</summary>
        private void OnDrawGizmos()
        {
            if (!_isPreviewing || !IsAiming || _arrowPrefab == null)
                return;

            Transform hold = PlayerModel.Of(this).GetSocket(PlayerSocketId.ArrowHold);
            Transform rest = GetComponent<BowController>().ArrowRestPoint;
            Vector3 prefabScale = _arrowPrefab.transform.localScale;
            bool nocked = _previewNormalizedTime >= NockNormalizedTime();

            // 実行時の矢は、プールで作られた見た目の大きさのまま手や弓に付く(SetParent で大きさを保つ)。
            // つがえた後は弦(根本ボーン)が引き手に付くので、尾は引き手の位置になる
            Vector3 toRest = rest.position - hold.position;
            Matrix4x4 arrow =
                nocked && toRest.sqrMagnitude > 0.000001f
                    ? Matrix4x4.TRS(hold.position, Quaternion.LookRotation(toRest, rest.up), prefabScale)
                    : Matrix4x4.TRS(hold.position, hold.rotation, prefabScale);

            Gizmos.color = nocked ? Color.yellow : Color.cyan;
            foreach (MeshFilter filter in _arrowPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                // Prefab のルートからの相対の変換(ルート自体の拡大率は上の行列に含めた)
                Matrix4x4 local = _arrowPrefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Gizmos.matrix = arrow * local;
                Gizmos.DrawMesh(filter.sharedMesh);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }

        private float NockNormalizedTime()
        {
            foreach (AnimationEvent evt in UnityEditor.AnimationUtility.GetAnimationEvents(_aimClip))
            {
                if (evt.functionName == NockEventName && _aimClip.length > 0f)
                    return evt.time / _aimClip.length;
            }
            return 1f;
        }
#endif
    }
}
