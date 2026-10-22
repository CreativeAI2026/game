using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>武器の見た目の姿勢の種類(<see cref="MockWeaponPose"/> 用)。</summary>
    public enum MockWeaponPoseKey
    {
        Idle,
        Guard,
        Attack,
        Aim,
    }

    /// <summary>
    /// 【仮モデル専用】専用アニメーションが無い間、武器の向き・位置をスクリプトで切り替えて見た目を補う。
    ///
    /// 本番アニメーションで武器の向きが付いたら、このコンポーネントを外すだけでよい
    /// (武器側は「付いていれば呼ぶ」だけで、仮モデル用の値をコードに持たない)。
    /// 角度はソケットに対するローカル角度、位置は最初の置き位置からのずれ(ローカル)。
    ///
    /// 弓の値は、プレイせずに Scene ビューのギズモで合わせられる(<see cref="BowGripTuner"/>)。
    /// </summary>
    public class MockWeaponPose : MonoBehaviour
    {
        [Serializable]
        private struct Pose
        {
            public MockWeaponPoseKey key;
            public Vector3 localEulerAngles;

            [Tooltip("最初の置き位置からのずれ（ローカル）")]
            public Vector3 localPositionOffset;
        }

        [Tooltip("向きを変える Transform。空欄ならこのオブジェクト")]
        [SerializeField]
        private Transform _target;

        [Tooltip(
            "姿勢ごとのローカル角度と位置のずれ。登録していない姿勢を指定された場合は変えない"
        )]
        [SerializeField]
        private Pose[] _poses = Array.Empty<Pose>();

        [Tooltip("0 なら即座に切り替える。0 より大きいとこの速さで補間する")]
        [SerializeField]
        private float _blendSpeed;

        private Quaternion _goalRotation;
        private Vector3 _goalOffset;
        private bool _hasGoal;
        private Vector3 _baseLocalPosition;
        private bool _hasBase;

        // 武器側の OnEnable(このコンポーネントの Awake より先になりうる)から呼ばれるため遅延取得する
        private Transform Target => _target != null ? _target : _target = transform;

        public void SetPose(MockWeaponPoseKey key)
        {
            foreach (var pose in _poses)
            {
                if (pose.key != key)
                    continue;

                _goalRotation = Quaternion.Euler(pose.localEulerAngles);
                _goalOffset = pose.localPositionOffset;
                _hasGoal = true;
                if (_blendSpeed <= 0f)
                    ApplyGoalImmediately();
                return;
            }
        }

        /// <summary>姿勢の値を返す。登録していなければ 0 を返す。</summary>
        public void GetPoseValues(
            MockWeaponPoseKey key,
            out Vector3 localEulerAngles,
            out Vector3 localPositionOffset
        )
        {
            int index = Array.FindIndex(_poses, p => p.key == key);
            localEulerAngles = index >= 0 ? _poses[index].localEulerAngles : Vector3.zero;
            localPositionOffset = index >= 0 ? _poses[index].localPositionOffset : Vector3.zero;
        }

#if UNITY_EDITOR
        /// <summary>調整用(BowGripTuner): 姿勢の値を書き換える。登録していなければ追加する。</summary>
        public void EditorSetPoseValues(
            MockWeaponPoseKey key,
            Vector3 localEulerAngles,
            Vector3 localPositionOffset
        )
        {
            UnityEditor.Undo.RecordObject(this, "Tune Weapon Pose");
            int index = Array.FindIndex(_poses, p => p.key == key);
            if (index < 0)
            {
                Array.Resize(ref _poses, _poses.Length + 1);
                index = _poses.Length - 1;
                _poses[index].key = key;
            }
            _poses[index].localEulerAngles = localEulerAngles;
            _poses[index].localPositionOffset = localPositionOffset;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
#endif

        private void Start()
        {
            // 位置の基準は、武器がソケットへ取り付けられた後(PlayerSocketAttachment.Awake)の置き位置。
            // 武器側の OnEnable から先に姿勢を指定されることがあるため、全員の Awake が済んだ Start で取る
            _baseLocalPosition = Target.localPosition;
            _hasBase = true;
            if (_hasGoal && _blendSpeed <= 0f)
                ApplyGoalImmediately();
        }

        private void ApplyGoalImmediately()
        {
            Target.localRotation = _goalRotation;
            // 基準位置が決まる(Start)までは位置を動かさない
            if (_hasBase)
                Target.localPosition = _baseLocalPosition + _goalOffset;
        }

        private void Update()
        {
            if (_blendSpeed <= 0f || !_hasGoal || !_hasBase)
                return;

            float t = Time.deltaTime * _blendSpeed;
            Target.localRotation = Quaternion.Slerp(Target.localRotation, _goalRotation, t);
            Target.localPosition = Vector3.Lerp(
                Target.localPosition,
                _baseLocalPosition + _goalOffset,
                t
            );
        }
    }
}
