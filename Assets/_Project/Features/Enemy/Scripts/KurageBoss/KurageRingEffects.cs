using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスのわっかに合わせて出す光のエフェクトを持ち、出す。KurageBossController と同じ GameObject に置く。
    /// 今あるのは、プレイヤーを締め付けている間のオーラだけ。
    /// いつ締めているかは <see cref="KurageRingRestraint"/> が知っていて、ここは見た目だけを受け持つ。
    ///
    /// オーラはわっかの子にしない(わっかのメッシュは拡大率が 100 倍で、子にすると大きさが変わるため)。
    /// 代わりに、毎フレーム輪の中心へ動かし、輪の半径に合わせて大きさを変える。
    /// </summary>
    [RequireComponent(typeof(KurageRingRestraint))]
    public sealed class KurageRingEffects : MonoBehaviour
    {
        [Tooltip(
            "頭上のわっか(天使のわっか)に付けた RingRadiusDeformer。オーラを出す位置と大きさに使う"
        )]
        [SerializeField]
        private RingRadiusDeformer _halo;

        [Tooltip(
            "締め付けている間のオーラ(ループ)。輪の半径 1m に合わせて作り、実際の半径に合わせて拡大縮小する"
        )]
        [SerializeField]
        private ParticleSystem _restraintAuraPrefab;

        [Tooltip("オーラを止めてから、残った粒が消えて片付けるまで（秒）")]
        [SerializeField]
        private float _auraCleanupDelay = 2.5f;

        private KurageRingRestraint _restraint;
        private ParticleSystem _aura;

        private void Awake()
        {
            Assert.IsNotNull(_halo, $"{name}: わっか(RingRadiusDeformer)が設定されていません");
            Assert.IsNotNull(
                _restraintAuraPrefab,
                $"{name}: 締め付けのオーラのエフェクトが設定されていません"
            );
            _restraint = GetComponent<KurageRingRestraint>();
        }

        private void OnDisable()
        {
            EndAura();
        }

        private void LateUpdate()
        {
            if (_restraint.IsRestraining && _aura == null)
                _aura = Instantiate(_restraintAuraPrefab);
            else if (!_restraint.IsRestraining)
                EndAura();

            if (_aura == null)
                return;

            // わっかはプレイヤーに付いて動くので、Animator とわっかが動いた後の位置に合わせる
            Transform aura = _aura.transform;
            aura.position = _halo.WorldCenter;
            aura.localScale = Vector3.one * _halo.CurrentRadius;
        }

        /// <summary>オーラを止める。出ている粒は寿命まで残してから片付ける。</summary>
        private void EndAura()
        {
            if (_aura == null)
                return;
            _aura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(_aura.gameObject, _auraCleanupDelay);
            _aura = null;
        }
    }
}
