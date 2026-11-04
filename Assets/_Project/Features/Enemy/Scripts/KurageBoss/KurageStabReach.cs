using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 突きの触手を、立ち止まった時に決めた狙いの点(プレイヤーの胸)へまっすぐ伸ばす。
    /// 突きのクリップだけでは根元から十数m・斜め下へしか届かず、ステージ上のプレイヤーにほとんど当たらないため、
    /// 伸びている間だけ根元の向きを狙いの点へ回し、届かない分を各ボーンへ均等に足して「一気に針を伸ばす」ように見せる。
    /// 狙いは立ち止まった時に固定するので、その後に動けば避けられる。
    ///
    /// Animator はこの触手のボーンを毎フレーム書き戻すとは限らない(再生して確かめたところ、足した分が積み重なって触手が飛んでいった)。
    /// そのため、前のフレームに自分が足した分が残っていれば差し引いてから足し直す。
    /// </summary>
    public class KurageStabReach : MonoBehaviour
    {
        [Tooltip("突きの触手のボーン(根元から先端の順)")]
        [SerializeField]
        private Transform[] _chain;

        [Tooltip("足す長さの上限（m）")]
        [SerializeField]
        private float _maxExtension = 30f;

        [Tooltip("狙う高さ。プレイヤーの足元からの高さ（m）")]
        [SerializeField]
        private float _targetHeight = 1.0f;

        private Vector3 _target;
        private Vector3[] _appliedOffsets;
        private Vector3[] _writtenPositions;
        private Quaternion _rootBaseRotation;
        private Quaternion _rootWrittenRotation;

        /// <summary>伸ばす割合(0〜1)。突きのステートが伸び始め・戻りに合わせて動かす。</summary>
        public float Weight { get; set; }

        private void Awake()
        {
            Assert.IsTrue(
                _chain != null && _chain.Length >= 2,
                $"{name}: 突きの触手のボーンが2本未満です"
            );
            _appliedOffsets = new Vector3[_chain.Length];
            _writtenPositions = new Vector3[_chain.Length];
            _rootBaseRotation = _chain[0].localRotation;
            _rootWrittenRotation = _rootBaseRotation;
        }

        /// <summary>狙いの点を決める。立ち止まって狙いを決めた時に呼ぶ。</summary>
        public void AimAt(Vector3 playerFeet)
        {
            _target = playerFeet + Vector3.up * _targetHeight;
        }

        private void LateUpdate()
        {
            Transform root = _chain[0];

            // Animator が書き戻していなければ、前のフレームに回した・足した分を取り除いてクリップの姿勢に戻す
            if (root.localRotation != _rootWrittenRotation)
                _rootBaseRotation = root.localRotation;
            root.localRotation = _rootBaseRotation;
            for (int i = 1; i < _chain.Length; i++)
            {
                if (_chain[i].localPosition == _writtenPositions[i])
                    _chain[i].localPosition -= _appliedOffsets[i];
            }

            Transform tip = _chain[_chain.Length - 1];
            float weight = Mathf.Clamp01(Weight);
            float extension = 0f;
            if (weight > 0f)
            {
                float natural = Vector3.Distance(root.position, tip.position);
                float needed = Vector3.Distance(root.position, _target);
                extension = Mathf.Clamp(needed - natural, 0f, _maxExtension) * weight;
            }

            float perBone = extension / (_chain.Length - 1);
            for (int i = 1; i < _chain.Length; i++)
            {
                Transform bone = _chain[i];
                Vector3 local = bone.localPosition;
                Vector3 offset = Vector3.zero;
                if (perBone > 0f && local.sqrMagnitude > 1e-12f)
                {
                    // localPosition は親のスケールを通してワールドへ変換されるので、メートルを親のスケールで割る
                    offset = local.normalized * (perBone / bone.parent.lossyScale.x);
                }
                bone.localPosition = local + offset;
                _appliedOffsets[i] = offset;
                _writtenPositions[i] = bone.localPosition;
            }

            if (weight > 0f)
            {
                // 根元から先端への向きを、狙いの点への向きへ回す
                Quaternion aim = Quaternion.FromToRotation(
                    tip.position - root.position,
                    _target - root.position
                );
                root.rotation = Quaternion.Slerp(Quaternion.identity, aim, weight) * root.rotation;
            }
            _rootWrittenRotation = root.localRotation;
        }
    }
}
