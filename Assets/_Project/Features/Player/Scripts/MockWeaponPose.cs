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
    /// 【仮モデル専用】専用アニメーションが無い間、武器の向きをスクリプトで切り替えて見た目を補う。
    ///
    /// 本番アニメーションで武器の向きが付いたら、このコンポーネントを外すだけでよい
    /// (武器側は「付いていれば呼ぶ」だけで、仮モデル用の値をコードに持たない)。
    /// 角度はソケットに対するローカル角度。
    /// </summary>
    public class MockWeaponPose : MonoBehaviour
    {
        [Serializable]
        private struct Pose
        {
            public MockWeaponPoseKey key;
            public Vector3 localEulerAngles;
        }

        [Tooltip("向きを変える Transform。空欄ならこのオブジェクト")]
        [SerializeField]
        private Transform _target;

        [Tooltip("姿勢ごとのローカル角度。登録していない姿勢を指定された場合は向きを変えない")]
        [SerializeField]
        private Pose[] _poses = Array.Empty<Pose>();

        [Tooltip("0 なら即座に切り替える。0 より大きいとこの速さで補間する")]
        [SerializeField]
        private float _blendSpeed;

        private Quaternion _goal;
        private bool _hasGoal;

        // 武器側の OnEnable(このコンポーネントの Awake より先になりうる)から呼ばれるため遅延取得する
        private Transform Target => _target != null ? _target : _target = transform;

        public void SetPose(MockWeaponPoseKey key)
        {
            foreach (var pose in _poses)
            {
                if (pose.key != key)
                    continue;

                _goal = Quaternion.Euler(pose.localEulerAngles);
                _hasGoal = true;
                if (_blendSpeed <= 0f)
                    Target.localRotation = _goal;
                return;
            }
        }

        private void Update()
        {
            if (_blendSpeed > 0f && _hasGoal)
                Target.localRotation = Quaternion.Slerp(
                    Target.localRotation,
                    _goal,
                    Time.deltaTime * _blendSpeed
                );
        }
    }
}
