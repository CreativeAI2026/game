using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの突きの軌道に出す光。突きエフェクトのプレハブ(KurageStabThrust)のルートに置く。
    /// 槍の穂先(<see cref="KurageStabLance"/>)には、先端の進みと届いたことを伝えるだけで、動かし方は任せる。
    /// 突きの長さは毎回変わるため、軌道に沿って並べるもの(光の輪・光の筋・光の粒・芯の線)は
    /// パーティクルの放出設定ではなく、ここで触手の先端の進みに合わせて1つずつ置く。
    /// 届いた先で広がる輪と散るオーブ(_impact)は位置を変えるだけで、中身はプレハブ側のパーティクルが持つ。
    /// </summary>
    public class KurageStabThrustEffect : MonoBehaviour
    {
        [Header("軌道に並べる光の輪")]
        [Tooltip("軌道に垂直な光の輪。放出はせず、ここから1つずつ置く")]
        [SerializeField]
        private ParticleSystem _pathRings;

        [Tooltip("輪を置く間隔（m）")]
        [SerializeField]
        private float _ringSpacing = 5f;

        [Tooltip("輪の数の下限と上限。近くを突いても遠くを突いても、輪の連なりに見える数にする")]
        [SerializeField]
        private Vector2Int _ringCountRange = new Vector2Int(3, 6);

        [Tooltip("根元側の輪の直径（m）")]
        [SerializeField]
        private float _ringStartDiameter = 8f;

        [Tooltip("先端側の輪の直径（m）。根元より小さくして、一点へ絞られていくように見せる")]
        [SerializeField]
        private float _ringEndDiameter = 3f;

        [Header("軌道を走る光の筋")]
        [SerializeField]
        private ParticleSystem _pathStreaks;

        [Tooltip("軌道1mあたりの本数")]
        [SerializeField]
        private float _streaksPerMeter = 1.5f;

        [Tooltip(
            "筋を出す範囲。軌道の根元からの割合（0〜1）。狙いの点の近くでは出さない(筋が狙いの点を突き抜けてカメラの手前に伸び、画面を覆うため)"
        )]
        [SerializeField, Range(0f, 1f)]
        private float _streakPathEndRatio = 0.8f;

        [Tooltip("軌道の中心から散らす半径（m）")]
        [SerializeField]
        private float _streakSpread = 1f;

        [Tooltip("筋が先端へ向かって走る速さ（m/秒）の下限と上限")]
        [SerializeField]
        private Vector2 _streakSpeedRange = new Vector2(30f, 55f);

        [Header("軌道に残る光の粒")]
        [SerializeField]
        private ParticleSystem _pathOrbs;

        [Tooltip("軌道1mあたりの数")]
        [SerializeField]
        private float _orbsPerMeter = 1.2f;

        [Tooltip("軌道から外へ漂う速さ（m/秒）の下限と上限")]
        [SerializeField]
        private Vector2 _orbDriftSpeedRange = new Vector2(0.4f, 1.8f);

        [Header("芯の線")]
        [Tooltip("根元から先端まで一直線に走る細い光")]
        [SerializeField]
        private LineRenderer _coreLine;

        [Tooltip("線の太さ（m）")]
        [SerializeField]
        private float _coreLineWidth = 0.8f;

        [Tooltip("伸び切ってから消えるまで（秒）")]
        [SerializeField]
        private float _coreLineFadeDuration = 0.25f;

        [Header("槍の穂先")]
        [Tooltip("触手の中ほどから先端までを包む、細い円錐の光")]
        [SerializeField]
        private KurageStabLance _lance;

        [Header("届いた先")]
        [Tooltip("届いた先で広がる輪・散るオーブ。再生はここから行う")]
        [SerializeField]
        private ParticleSystem _impact;

        [Tooltip("置いた光がすべて消えるまでの時間（秒）。過ぎたらエフェクトごと片付ける")]
        [SerializeField]
        private float _cleanupDelay = 3f;

        private Color _coreLineColor;
        private Vector3 _pathEnd;

        private void Awake()
        {
            Assert.IsNotNull(_pathRings, $"{name}: 軌道の光の輪が設定されていません");
            Assert.IsNotNull(_pathStreaks, $"{name}: 軌道の光の筋が設定されていません");
            Assert.IsNotNull(_pathOrbs, $"{name}: 軌道の光の粒が設定されていません");
            Assert.IsNotNull(_coreLine, $"{name}: 芯の線が設定されていません");
            Assert.IsNotNull(_lance, $"{name}: 槍の穂先が設定されていません");
            Assert.IsNotNull(_impact, $"{name}: 届いた先のエフェクトが設定されていません");
            _coreLineColor = _coreLine.startColor;
            _coreLine.enabled = false;
        }

        /// <summary>突きの軌道に光を出す。触手が伸び始めた時に呼ぶ。</summary>
        /// <param name="from">光を出し始める位置</param>
        /// <param name="to">狙いの点</param>
        /// <param name="extendDuration">伸び切るまでの時間（秒）。この時間をかけて根元側から順に光を置く</param>
        public void Play(Vector3 from, Vector3 to, float extendDuration)
        {
            Vector3 path = to - from;
            // 輪の向き(軌道に垂直)は、輪のパーティクルがこの Transform の向きに合わせて描くことで決まる
            if (path.sqrMagnitude > 1e-6f)
                transform.SetPositionAndRotation(from, Quaternion.LookRotation(path));
            else
                transform.position = from;

            StartCoroutine(Run(from, to, extendDuration));
        }

        private IEnumerator Run(Vector3 from, Vector3 to, float extendDuration)
        {
            float length = Vector3.Distance(from, to);
            _pathEnd = to;
            int ringCount = Mathf.Clamp(
                Mathf.RoundToInt(length / _ringSpacing),
                _ringCountRange.x,
                _ringCountRange.y
            );
            int ringsPlaced = 0;
            // 小数の本数を次のフレームへ持ち越す(1フレームに進む距離が短い時に0本へ切り捨てられないように)
            float streakCarry = 0f;
            float orbCarry = 0f;

            _coreLine.enabled = true;
            _coreLine.positionCount = 2;
            _coreLine.SetPosition(0, from);
            SetCoreLine(from, 1f);

            float progress = 0f;
            while (progress < 1f)
            {
                float next =
                    extendDuration > 0f
                        ? Mathf.Min(1f, progress + Time.deltaTime / extendDuration)
                        : 1f;

                // 輪は両端を避けて等間隔に置く(根元はボスの体に、先端は届いた先の輪に重なるため)
                while (ringsPlaced < ringCount && (ringsPlaced + 1f) / (ringCount + 1f) <= next)
                {
                    float ringAt = (ringsPlaced + 1f) / (ringCount + 1f);
                    PlaceRing(Vector3.Lerp(from, to, ringAt), ringAt);
                    ringsPlaced++;
                }

                float covered = (next - progress) * length;
                Vector3 segmentStart = Vector3.Lerp(from, to, progress);
                Vector3 segmentEnd = Vector3.Lerp(from, to, next);
                if (next <= _streakPathEndRatio)
                    PlaceAlong(
                        segmentStart,
                        segmentEnd,
                        covered * _streaksPerMeter,
                        ref streakCarry,
                        PlaceStreak
                    );
                PlaceAlong(
                    segmentStart,
                    segmentEnd,
                    covered * _orbsPerMeter,
                    ref orbCarry,
                    PlaceOrb
                );

                SetCoreLine(segmentEnd, 1f);
                _lance.Follow(from, to, next);
                progress = next;
                yield return null;
            }

            _impact.transform.position = to;
            _impact.Play(true);
            _lance.Release();

            for (float t = 0f; t < _coreLineFadeDuration; t += Time.deltaTime)
            {
                SetCoreLine(to, 1f - t / _coreLineFadeDuration);
                yield return null;
            }
            _coreLine.enabled = false;

            Destroy(gameObject, _cleanupDelay);
        }

        private delegate void PlaceAt(Vector3 position);

        private static void PlaceAlong(
            Vector3 start,
            Vector3 end,
            float count,
            ref float carry,
            PlaceAt place
        )
        {
            carry += count;
            while (carry >= 1f)
            {
                place(Vector3.Lerp(start, end, Random.value));
                carry -= 1f;
            }
        }

        private void PlaceRing(Vector3 position, float pathRatio)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                startSize = Mathf.Lerp(_ringStartDiameter, _ringEndDiameter, pathRatio),
            };
            _pathRings.Emit(emit, 1);
        }

        private void PlaceStreak(Vector3 position)
        {
            // 狙いの点を通り過ぎてプレイヤーやカメラの手前まで飛んでこないよう、消えるまでに進む距離を残りの距離までにする
            float remaining = Vector3.Distance(position, _pathEnd);
            float maxSpeed = remaining / _pathStreaks.main.startLifetime.constantMax;
            float speed = Mathf.Min(
                Random.Range(_streakSpeedRange.x, _streakSpeedRange.y),
                maxSpeed
            );
            var emit = new ParticleSystem.EmitParams
            {
                position = position + AcrossPath(Random.insideUnitCircle * _streakSpread),
                velocity = transform.forward * speed,
            };
            _pathStreaks.Emit(emit, 1);
        }

        private void PlaceOrb(Vector3 position)
        {
            Vector3 outward = AcrossPath(Random.insideUnitCircle.normalized);
            var emit = new ParticleSystem.EmitParams
            {
                position = position + outward * (_streakSpread * Random.value),
                velocity = outward * Random.Range(_orbDriftSpeedRange.x, _orbDriftSpeedRange.y),
            };
            _pathOrbs.Emit(emit, 1);
        }

        /// <summary>軌道に垂直な面の中の向き(x=右, y=上)をワールドの向きへ直す。</summary>
        private Vector3 AcrossPath(Vector2 offset) =>
            transform.right * offset.x + transform.up * offset.y;

        /// <summary>芯の線の先端の位置と濃さ(0〜1)を決める。消える時は細くもする。</summary>
        private void SetCoreLine(Vector3 tip, float strength)
        {
            _coreLine.SetPosition(1, tip);
            _coreLine.widthMultiplier = _coreLineWidth * strength;
            Color color = _coreLineColor;
            color.a *= strength;
            _coreLine.startColor = color;
            _coreLine.endColor = color;
        }
    }
}
