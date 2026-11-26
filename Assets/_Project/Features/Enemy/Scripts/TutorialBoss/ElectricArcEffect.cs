using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 2点の間を流れる電流のエフェクト。ワイヤーなどの両端を毎フレーム受け取り、
    /// その間に稲妻の線を這わせ、途中から火花を散らし、終点(感電している側)の光を明滅させる。
    /// 自身は常に終点へ移動するので、終点で出すパーティクルや音はこのオブジェクトの子・同じ位置に置く。
    /// </summary>
    public sealed class ElectricArcEffect : MonoBehaviour
    {
        [Header("稲妻の線")]
        [Tooltip("両端の間に張る線。1本ずつ別の形に揺らす")]
        [SerializeField]
        private LineRenderer[] _arcs;

        [Tooltip("線1本あたりの折れ点の数。多いほど細かくギザギザになる")]
        [SerializeField]
        private int _pointsPerArc = 14;

        [Tooltip("線が、両端を結ぶ直線から離れる最大の距離(メートル)。両端では 0 になる")]
        [SerializeField]
        private float _jitterWidth = 0.25f;

        [Tooltip("線の形と光の強さを変える間隔(秒)。短いほど激しくちらつく")]
        [SerializeField]
        private float _flickerInterval = 0.05f;

        [Header("途中の火花")]
        [Tooltip("両端の間のランダムな位置から散らす火花")]
        [SerializeField]
        private ParticleSystem _wireSparks;

        [Tooltip("1秒あたりに散らす火花の数")]
        [SerializeField]
        private float _wireSparksPerSecond = 30f;

        [Tooltip("火花が飛び出す速さ(メートル/秒)")]
        [SerializeField]
        private float _wireSparkSpeed = 4f;

        [Header("終点の光")]
        [Tooltip("終点を照らす光。強さを明滅させる")]
        [SerializeField]
        private Light _contactLight;

        [Tooltip("明滅で最も暗くなる時の強さ(Light の Intensity に対する割合)")]
        [SerializeField]
        private float _lightMinRatio = 0.35f;

        private ParticleSystem[] _particleSystems;
        private Vector3[] _arcPoints;
        private float _lightMaxIntensity;
        private Vector3 _start;
        private Vector3 _end;
        private bool _hasEndpoints;
        private bool _isFlowing = true;
        private float _flickerTimer;

        // 1個に満たない端数を持ち越し、フレームレートによらず同じ数だけ火花を出す
        private float _pendingWireSparks;

        private void Awake()
        {
            Assert.IsNotNull(_wireSparks, $"{name}: 途中の火花(_wireSparks)が未設定です");
            Assert.IsNotNull(_contactLight, $"{name}: 終点の光(_contactLight)が未設定です");

            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            _arcPoints = new Vector3[_pointsPerArc];
            _lightMaxIntensity = _contactLight.intensity;

            // 両端を受け取るまでは、原点に線や光が出ないよう隠しておく
            SetArcsVisible(false);
            _contactLight.enabled = false;
            foreach (LineRenderer arc in _arcs)
            {
                arc.useWorldSpace = true;
                arc.positionCount = _pointsPerArc;
            }
        }

        /// <summary>
        /// 電流の両端(ワールド座標)を渡す。両端が動く間は毎フレーム呼ぶ。
        /// start が流れ出す側、end が感電している側。
        /// </summary>
        public void SetEndpoints(Vector3 start, Vector3 end)
        {
            _start = start;
            _end = end;
            transform.position = end;

            if (_hasEndpoints || !_isFlowing)
                return;

            _hasEndpoints = true;
            SetArcsVisible(true);
            _contactLight.enabled = true;
            Flicker();
        }

        /// <summary>
        /// 電流を止める。線と光はすぐ消え、出ている火花は寿命まで残る。
        /// オブジェクトの破棄は呼び出し側が行う。
        /// </summary>
        public void StopFlow()
        {
            _isFlowing = false;
            SetArcsVisible(false);
            _contactLight.enabled = false;
            foreach (ParticleSystem particle in _particleSystems)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Update()
        {
            if (!_isFlowing || !_hasEndpoints)
                return;

            _flickerTimer += Time.deltaTime;
            if (_flickerTimer >= _flickerInterval)
            {
                _flickerTimer = 0f;
                Flicker();
            }

            EmitWireSparks();
        }

        private void LateUpdate()
        {
            if (!_isFlowing || !_hasEndpoints)
                return;

            // 形を変えない間も、両端の動きには毎フレーム追従させる
            foreach (LineRenderer arc in _arcs)
            {
                arc.SetPosition(0, _start);
                arc.SetPosition(_pointsPerArc - 1, _end);
            }
        }

        private void Flicker()
        {
            foreach (LineRenderer arc in _arcs)
                RebuildArc(arc);

            _contactLight.intensity = _lightMaxIntensity * Random.Range(_lightMinRatio, 1f);
        }

        /// <summary>両端を結ぶ直線を、途中だけランダムに横へずらしてギザギザの線にする。</summary>
        private void RebuildArc(LineRenderer arc)
        {
            Vector3 direction = _end - _start;
            // 直線に垂直な2軸を作る。直線が真上・真下を向いている時は up と平行になるので、別の軸を使う
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < 0.0001f)
                side = Vector3.Cross(direction, Vector3.right);
            side.Normalize();
            Vector3 lift = Vector3.Cross(direction, side).normalized;

            for (int i = 0; i < _pointsPerArc; i++)
            {
                float t = i / (float)(_pointsPerArc - 1);
                // 両端では 0、中央で最大。線が両端からは離れないようにする
                float spread = Mathf.Sin(t * Mathf.PI) * _jitterWidth;
                _arcPoints[i] =
                    Vector3.Lerp(_start, _end, t)
                    + side * (Random.Range(-1f, 1f) * spread)
                    + lift * (Random.Range(-1f, 1f) * spread);
            }
            arc.SetPositions(_arcPoints);
        }

        private void EmitWireSparks()
        {
            _pendingWireSparks += _wireSparksPerSecond * Time.deltaTime;
            while (_pendingWireSparks >= 1f)
            {
                _pendingWireSparks -= 1f;
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = Vector3.Lerp(_start, _end, Random.value),
                    velocity = Random.onUnitSphere * _wireSparkSpeed,
                    applyShapeToPosition = false,
                };
                _wireSparks.Emit(emitParams, 1);
            }
        }

        private void SetArcsVisible(bool visible)
        {
            foreach (LineRenderer arc in _arcs)
                arc.enabled = visible;
        }
    }
}
