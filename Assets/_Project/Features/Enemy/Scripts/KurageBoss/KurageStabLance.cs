using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの突きで、触手の中ほどから先端までを包む「槍の穂先」の光。突きエフェクトのプレハブの子に置く。
    /// 細い円錐のメッシュ(子の MeshRenderer。+Z が先端、根元の半径1・長さ1)を、尖った先を触手の先端に合わせて置く。
    /// 触手が伸びる間は先端に付いて進み(<see cref="Follow"/>)、届いたら少し留まってから細くなって消える(<see cref="Release"/>)。
    /// 形と光り方はシェーダー(CreativeAI/KurageHolyLance)が持ち、ここは位置・大きさ・濃さだけを動かす。
    /// </summary>
    public class KurageStabLance : MonoBehaviour
    {
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        [Header("大きさ")]
        [Tooltip(
            "穂先の長さ。突きの軌道全体に対する割合（0〜1）。0.5 なら軌道の中ほどから先を包む"
        )]
        [SerializeField, Range(0.1f, 1f)]
        private float _lengthRatio = 0.55f;

        [Tooltip("穂先の長さの上限（m）。遠くを突いた時に間延びしないようにする")]
        [SerializeField]
        private float _maxLength = 22f;

        [Tooltip("根元側（開いた口）の半径（m）")]
        [SerializeField]
        private float _radius = 1.3f;

        [Header("出方")]
        [Tooltip(
            "現れ始める・濃くなり切る時点。触手の伸びの進み具合（0〜1）。伸び始めは出さず、中盤から現れる"
        )]
        [SerializeField]
        private Vector2 _appearProgressRange = new Vector2(0.3f, 0.65f);

        [Tooltip("届いてから消え始めるまで（秒）")]
        [SerializeField]
        private float _holdDuration = 0.1f;

        [Tooltip("消え始めてから消え切るまで（秒）")]
        [SerializeField]
        private float _fadeDuration = 0.28f;

        [Tooltip(
            "消え切る時の太さ（元の半径に対する倍率）。細く絞りながら消して、突き抜けた余韻にする"
        )]
        [SerializeField, Range(0f, 1f)]
        private float _fadeRadiusScale = 0.25f;

        private MeshRenderer[] _renderers;
        private MaterialPropertyBlock _block;
        private float _length;
        private bool _released;
        private float _releaseTimer;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<MeshRenderer>();
            Assert.IsTrue(_renderers.Length > 0, $"{name}: 子に槍の MeshRenderer がありません");
            _block = new MaterialPropertyBlock();
            Apply(0f, 1f);
        }

        /// <summary>伸びている触手の先端に合わせて置く。伸びている間、毎フレーム呼ぶ。</summary>
        /// <param name="pathStart">軌道の始まり</param>
        /// <param name="pathEnd">狙いの点</param>
        /// <param name="progress">伸びの進み具合（0〜1）</param>
        public void Follow(Vector3 pathStart, Vector3 pathEnd, float progress)
        {
            Vector3 path = pathEnd - pathStart;
            float pathLength = path.magnitude;
            if (pathLength < 1e-3f)
                return;

            Vector3 direction = path / pathLength;
            // 軌道の始まりより手前へはみ出さないよう、今の先端までの距離を上限にする
            _length = Mathf.Min(_maxLength, pathLength * _lengthRatio, pathLength * progress);
            Vector3 tip = pathStart + direction * (pathLength * progress);
            transform.SetPositionAndRotation(
                tip - direction * _length,
                Quaternion.LookRotation(direction)
            );
            Apply(Mathf.InverseLerp(_appearProgressRange.x, _appearProgressRange.y, progress), 1f);
        }

        /// <summary>届いた後の消え方を始める。少し留まってから、細くなりながら消える。</summary>
        public void Release()
        {
            _released = true;
            _releaseTimer = 0f;
        }

        private void Update()
        {
            if (!_released)
                return;

            _releaseTimer += Time.deltaTime;
            float fade = Mathf.Clamp01((_releaseTimer - _holdDuration) / _fadeDuration);
            Apply(1f - fade, Mathf.Lerp(1f, _fadeRadiusScale, fade));
            if (fade >= 1f)
                _released = false;
        }

        private void Apply(float strength, float radiusScale)
        {
            float radius = _radius * radiusScale;
            transform.localScale = new Vector3(radius, radius, Mathf.Max(_length, 1e-3f));

            bool visible = strength > 0f;
            _block.SetFloat(StrengthId, strength);
            foreach (MeshRenderer meshRenderer in _renderers)
            {
                meshRenderer.enabled = visible;
                meshRenderer.SetPropertyBlock(_block);
            }
        }
    }
}
