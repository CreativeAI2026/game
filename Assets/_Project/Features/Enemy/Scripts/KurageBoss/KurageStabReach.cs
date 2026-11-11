using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 突きの触手を、立ち止まった時に決めた狙いの点(プレイヤーの胸)へ一直線に伸ばす。
    /// 突きのクリップだけでは根元から十数m・斜め下へしか届かず、ステージ上のプレイヤーにほとんど当たらないため、
    /// 伸びている間だけ、各ボーンを狙いの点の向きへ回し、根元から狙いの点までの直線の上へ並べて「一気に針を伸ばす」ように見せる。
    /// 狙いは立ち止まった時に固定するので、その後に動けば避けられる。
    ///
    /// 根元だけを狙いへ向ける作りだと、クリップの曲がった姿勢のまま引き伸ばされ、触手の中ほどが
    /// 狙いへの直線から十数m下へ垂れ、先端も狙いの点を数m通り過ぎた(再生して測った)。
    /// そのため根元から順に全てのボーンを狙いの向きへ揃える。
    /// 長さは、ボーンの間隔を足して合わせるのではなく、直線の上の位置へ直接置いて合わせる
    /// (クリップがボーンを拡大して伸ばしている区間があり、回すと間隔が変わって先端が狙いの点を十数m通り過ぎたため)。
    ///
    /// Animator はこの触手のボーンを毎フレーム書き戻すとは限らない(再生して確かめたところ、足した分が積み重なって触手が飛んでいった)。
    /// そのため、前のフレームに自分が回した・足した分が残っていれば、取り除いてから掛け直す。
    /// </summary>
    public class KurageStabReach : MonoBehaviour
    {
        [Tooltip("突きの触手のボーン(根元から先端の順)。それぞれが1つ前のボーンの子であること")]
        [SerializeField]
        private Transform[] _chain;

        [Tooltip("クリップの姿勢の全長から、さらに伸ばせる長さの上限（m）")]
        [SerializeField]
        private float _maxExtension = 30f;

        [Tooltip("狙う高さ。プレイヤーの足元からの高さ（m）")]
        [SerializeField]
        private float _targetHeight = 1.0f;

        private Vector3 _target;
        private Vector3[] _appliedOffsets;
        private Vector3[] _writtenPositions;

        // 直線の上へ並べる時の作業用(毎フレーム作り直さないため)
        private Vector3[] _clipLocalPositions;
        private Vector3[] _placedPositions;

        // クリップの姿勢での向きと、前のフレームに自分が書いた向き(ボーンごと)
        private Quaternion[] _baseRotations;
        private Quaternion[] _writtenRotations;

        /// <summary>伸ばす割合(0〜1)。突きのステートが伸び始め・戻りに合わせて動かす。</summary>
        public float Weight { get; set; }

        /// <summary>狙いの点(ワールド座標)。<see cref="AimAt"/> で決まる。</summary>
        public Vector3 Target => _target;

        /// <summary>突きの触手の根元のボーン。伸びた触手は、ここから狙いの点へまっすぐ通る。</summary>
        public Transform Root => _chain[0];

        /// <summary>突きの触手の先端のボーン。</summary>
        public Transform Tip => _chain[_chain.Length - 1];

        private void Awake()
        {
            Assert.IsTrue(
                _chain != null && _chain.Length >= 2,
                $"{name}: 突きの触手のボーンが2本未満です"
            );
            _appliedOffsets = new Vector3[_chain.Length];
            _writtenPositions = new Vector3[_chain.Length];
            _clipLocalPositions = new Vector3[_chain.Length];
            _placedPositions = new Vector3[_chain.Length];
            _baseRotations = new Quaternion[_chain.Length];
            _writtenRotations = new Quaternion[_chain.Length];
            for (int i = 0; i < _chain.Length; i++)
            {
                _baseRotations[i] = _chain[i].localRotation;
                _writtenRotations[i] = _baseRotations[i];
            }
        }

        /// <summary>狙いの点を決める。立ち止まって狙いを決めた時に呼ぶ。</summary>
        public void AimAt(Vector3 playerFeet)
        {
            _target = playerFeet + Vector3.up * _targetHeight;
        }

        private void LateUpdate()
        {
            RestoreClipPose();
            for (int i = 0; i < _chain.Length; i++)
                _clipLocalPositions[i] = _chain[i].localPosition;

            float weight = Mathf.Clamp01(Weight);
            if (weight > 0f)
            {
                Straighten(weight);
                PlaceOnAimLine(weight);
            }

            for (int i = 0; i < _chain.Length; i++)
            {
                Transform bone = _chain[i];
                _writtenRotations[i] = bone.localRotation;
                _writtenPositions[i] = bone.localPosition;
                _appliedOffsets[i] = bone.localPosition - _clipLocalPositions[i];
            }
        }

        /// <summary>Animator が書き戻していなければ、前のフレームに回した・動かした分を取り除いてクリップの姿勢に戻す。</summary>
        private void RestoreClipPose()
        {
            for (int i = 0; i < _chain.Length; i++)
            {
                Transform bone = _chain[i];
                if (bone.localRotation != _writtenRotations[i])
                    _baseRotations[i] = bone.localRotation;
                bone.localRotation = _baseRotations[i];

                if (i > 0 && bone.localPosition == _writtenPositions[i])
                    bone.localPosition -= _appliedOffsets[i];
            }
        }

        /// <summary>
        /// 根元から順に、各ボーンから次のボーンへの向きを狙いの点への向きへ回す。
        /// 回したボーンより先は一緒に動くので、根元から順に揃えれば全体がほぼ一直線になる。
        /// メッシュ(触手の太さの向き)を直線に沿わせるのが目的で、位置は <see cref="PlaceOnAimLine"/> が合わせる。
        /// </summary>
        private void Straighten(float weight)
        {
            Vector3 aim = _target - _chain[0].position;
            for (int i = 0; i < _chain.Length - 1; i++)
            {
                Transform bone = _chain[i];
                Quaternion toAim = Quaternion.FromToRotation(
                    _chain[i + 1].position - bone.position,
                    aim
                );
                bone.rotation =
                    Quaternion.Slerp(Quaternion.identity, toAim, weight) * bone.rotation;
            }
        }

        /// <summary>
        /// 各ボーンを、根元から狙いの点までの直線の上へ寄せる。ボーンの間隔の比は今の姿勢のまま保ち、
        /// 伸び切った時(weight=1)に先端がちょうど狙いの点に来るよう、全体を引き伸ばす。
        /// </summary>
        private void PlaceOnAimLine(float weight)
        {
            Vector3 root = _chain[0].position;
            Vector3 toTarget = _target - root;
            float chainLength = ChainLength();
            if (chainLength < 1e-4f || toTarget.sqrMagnitude < 1e-8f)
                return;

            float reach = Mathf.Min(toTarget.magnitude, chainLength + _maxExtension);
            Vector3 direction = toTarget.normalized;

            // ボーンを動かすとその先のボーンも一緒に動くので、置く位置を先に全て決めてから根元側から順に置く
            float lengthSoFar = 0f;
            for (int i = 1; i < _chain.Length; i++)
            {
                Vector3 current = _chain[i].position;
                lengthSoFar += Vector3.Distance(current, _chain[i - 1].position);
                Vector3 onLine = root + direction * (reach * lengthSoFar / chainLength);
                _placedPositions[i] = Vector3.Lerp(current, onLine, weight);
            }
            for (int i = 1; i < _chain.Length; i++)
                _chain[i].position = _placedPositions[i];
        }

        /// <summary>根元から先端までボーンをたどった長さ（m）。</summary>
        private float ChainLength()
        {
            float length = 0f;
            for (int i = 1; i < _chain.Length; i++)
                length += Vector3.Distance(_chain[i].position, _chain[i - 1].position);
            return length;
        }
    }
}
