using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 地面から触手を出す攻撃。ドリルのクリップで触手をポータルへ突っ込み(TentaclesDive)、
    /// パターンが決めた位置・時間差で地面の触手(<see cref="KurageGroundTentacle"/>)を出す。
    /// 出した触手は自分で最後まで動くので、ボスはクリップが終われば周回に戻る。
    /// </summary>
    public class KurageTentacleSummonState : KurageBaseState
    {
        // TentaclesDive のイベントが来なかった場合に出し始める時間（秒）。クリップにイベントを置き忘れても攻撃が成立するように
        private const float DiveFallbackTime = 0.8f;

        private readonly List<KurageTentacleSpawn> _pending = new List<KurageTentacleSpawn>();
        private float _timer;
        private float _diveTime = -1f;

        public KurageTentacleSummonState(KurageBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            _timer = 0f;
            boss.Animator.SetTrigger(KurageAnimatorParams.Drill);
        }

        public override void Update()
        {
            _timer += Time.deltaTime;
            boss.Mover.Move(0f, 0f);
            FaceStage();

            if (_diveTime < 0f && _timer >= DiveFallbackTime)
                Dive();

            if (_diveTime >= 0f)
                SpawnDueTentacles();

            if (_timer >= boss.DrillRecoverDuration && _pending.Count == 0)
                boss.ChangeState(new KurageOrbitState(boss));
        }

        public override void OnAnimationEvent(KurageAnimationEvent animationEvent)
        {
            if (animationEvent == KurageAnimationEvent.TentaclesDive && _diveTime < 0f)
                Dive();
        }

        private void Dive()
        {
            _diveTime = _timer;
            KurageTentaclePattern pattern = boss.PickTentaclePattern();
            _pending.AddRange(
                pattern.Build(boss.Player.transform.position, boss.Mover.Path.StageBounds)
            );
        }

        private void SpawnDueTentacles()
        {
            float elapsed = _timer - _diveTime;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                KurageTentacleSpawn spawn = _pending[i];
                if (elapsed < spawn.Delay)
                    continue;

                KurageGroundTentacle tentacle = Object.Instantiate(
                    boss.GroundTentaclePrefab,
                    spawn.Position,
                    Quaternion.identity
                );
                tentacle.Begin(
                    boss.Status,
                    boss.Sounds,
                    boss.Restraint,
                    boss.Player.transform,
                    spawn.Sweep
                );
                _pending.RemoveAt(i);
            }
        }
    }
}
