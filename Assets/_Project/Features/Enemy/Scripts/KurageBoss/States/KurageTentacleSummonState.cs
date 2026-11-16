using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 地面から触手を出す攻撃。ドリルのクリップで触手をポータルへ突っ込み(TentaclesDive)、
    /// パターンが決めた位置・時間差で地面の触手(<see cref="KurageGroundTentacle"/>)を出す。
    /// 出した触手は自分で最後まで動くので、ボスはクリップが終われば次の行動へ移る。
    /// </summary>
    public class KurageTentacleSummonState : KurageBaseState
    {
        private readonly List<KurageTentacleSpawn> _pending = new List<KurageTentacleSpawn>();
        private KurageTentaclePattern _pattern;
        private float _elapsedSinceDive;
        private bool _dived;

        public KurageTentacleSummonState(KurageBossController controller)
            : base(controller) { }

        /// <param name="pattern">使う出し方。null ならボスに並べた中から選ぶ(デバッグで決めて出す時に指定する)</param>
        public KurageTentacleSummonState(
            KurageBossController controller,
            KurageTentaclePattern pattern
        )
            : base(controller)
        {
            _pattern = pattern;
        }

        public override void Enter()
        {
            _elapsedSinceDive = 0f;
            _dived = false;
            boss.Animator.SetTrigger(KurageAnimatorParams.Drill);
        }

        public override void Update()
        {
            boss.Mover.Move(0f, 0f);
            FaceStage();

            if (_dived)
            {
                _elapsedSinceDive += Time.deltaTime;
                SpawnDueTentacles();
            }

            if (IsAnimationFinished(KurageAnimatorParams.Drill) && _pending.Count == 0)
                boss.FinishAttack();
        }

        public override void OnAnimationEvent(KurageAnimationEvent animationEvent)
        {
            if (animationEvent == KurageAnimationEvent.TentaclesDive && !_dived)
                Dive();
        }

        private void Dive()
        {
            _dived = true;
            _elapsedSinceDive = 0f;
            if (_pattern == null)
                _pattern = boss.PickTentaclePattern();
            _pending.AddRange(
                _pattern.Build(boss.Player.transform.position, boss.Mover.Path.StageBounds)
            );
        }

        private void SpawnDueTentacles()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                KurageTentacleSpawn spawn = _pending[i];
                if (_elapsedSinceDive < spawn.Delay)
                    continue;

                // 位置は出す瞬間に決め直せる(プレイヤーを追いかけるパターン用)
                Vector3 position = _pattern.ResolvePosition(
                    spawn,
                    boss.Player.transform.position,
                    boss.Mover.Path.StageBounds
                );
                boss.SpawnGroundTentacle(position, spawn.Sweep);
                _pending.RemoveAt(i);
            }
        }
    }
}
