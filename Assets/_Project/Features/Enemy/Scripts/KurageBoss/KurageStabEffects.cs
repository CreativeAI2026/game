using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの突きに合わせて出す光のエフェクトを持ち、出す。KurageBossController と同じ GameObject に置く。
    /// いつ出すかは突きのステート(<see cref="KurageStabState"/>)が決め、ここは見た目だけを受け持つ。
    ///
    /// 時間順:
    ///   溜め   … 触手の先端へ光の粒が集まり続ける(<see cref="BeginGather"/>)
    ///   閃き   … 集まった光が一点に絞られ、針のような光が走る(<see cref="PlayGlint"/>)
    ///   突き   … 軌道に沿って輪が連なり、届いた先で輪が広がってオーブが散る(<see cref="PlayThrust"/>)
    ///
    /// エフェクトは触手のボーンの子にしない(ボーンの拡大率で大きさが変わるため)。
    /// 代わりに、先端に付いて動くものは毎フレーム位置と向きを先端へ合わせる。
    /// </summary>
    [RequireComponent(typeof(KurageStabReach))]
    public class KurageStabEffects : MonoBehaviour
    {
        [Tooltip("溜めの間、触手の先端へ光が集まり続けるエフェクト(ループ)")]
        [SerializeField]
        private ParticleSystem _gatherPrefab;

        [Tooltip("突き出す直前の閃き(1回再生)")]
        [SerializeField]
        private ParticleSystem _glintPrefab;

        [Tooltip("突きの軌道と届いた先のエフェクト")]
        [SerializeField]
        private KurageStabThrustEffect _thrustPrefab;

        [Tooltip(
            "軌道の光を出し始める位置。触手の根元から狙いの点までの割合（0〜1）。根元はボスの体の中なので、少し先から出す"
        )]
        [SerializeField, Range(0f, 0.5f)]
        private float _thrustStartRatio = 0.12f;

        [Tooltip("集まる光を止めてから、残った粒が消えて片付けるまで（秒）")]
        [SerializeField]
        private float _gatherCleanupDelay = 1f;

        private KurageStabReach _reach;

        // 今、触手の先端に付いて動いているエフェクト(集まる光か閃き)
        private ParticleSystem _attached;
        private ParticleSystem _gather;

        private void Awake()
        {
            Assert.IsNotNull(_gatherPrefab, $"{name}: 突きの溜めのエフェクトが設定されていません");
            Assert.IsNotNull(_glintPrefab, $"{name}: 突きの閃きのエフェクトが設定されていません");
            Assert.IsNotNull(_thrustPrefab, $"{name}: 突きの軌道のエフェクトが設定されていません");
            _reach = GetComponent<KurageStabReach>();
        }

        /// <summary>触手の先端へ光を集め始める。狙いを決めて立ち止まった時に呼ぶ。</summary>
        public void BeginGather()
        {
            EndGather();
            _gather = Instantiate(_gatherPrefab, _reach.Tip.position, AimRotation());
            _attached = _gather;
        }

        /// <summary>光を集めるのをやめる。途中で怯んだ時など、閃きへ進まずに終わる時にも呼ぶ。</summary>
        public void EndGather()
        {
            if (_gather == null)
                return;

            _gather.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(_gather.gameObject, _gatherCleanupDelay);
            if (_attached == _gather)
                _attached = null;
            _gather = null;
        }

        /// <summary>突き出す直前の閃きを出す。</summary>
        public void PlayGlint()
        {
            EndGather();
            _attached = Instantiate(_glintPrefab, _reach.Tip.position, AimRotation());
            Destroy(_attached.gameObject, _attached.main.duration);
        }

        /// <summary>突きの軌道と届いた先のエフェクトを出す。触手が伸び始めた時に呼ぶ。</summary>
        /// <param name="extendDuration">触手が伸び切るまでの時間（秒）</param>
        public void PlayThrust(float extendDuration)
        {
            EndGather();
            // 閃きの残りの光は閃いた場所に置いていく(先端に付けたままだと、伸びる触手と一緒にプレイヤーの所まで運ばれる)
            _attached = null;
            // 伸びた触手は根元から狙いの点へまっすぐ通る。伸びる前の先端の位置から線を引くと、触手が輪の中を通らない
            Vector3 from = Vector3.Lerp(_reach.Root.position, _reach.Target, _thrustStartRatio);
            Instantiate(_thrustPrefab).Play(from, _reach.Target, extendDuration);
        }

        private void LateUpdate()
        {
            if (_attached != null)
                _attached.transform.SetPositionAndRotation(_reach.Tip.position, AimRotation());
        }

        /// <summary>触手の先端から狙いの点への向き。エフェクトの +Z をこの向きに合わせる。</summary>
        private Quaternion AimRotation()
        {
            Vector3 toTarget = _reach.Target - _reach.Tip.position;
            return toTarget.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(toTarget)
                : transform.rotation;
        }
    }
}
