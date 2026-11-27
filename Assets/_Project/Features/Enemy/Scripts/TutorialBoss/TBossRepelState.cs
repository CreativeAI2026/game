using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 連打の最後の一撃を、腕の鎌の短い薙ぎ払いではじき返すステート。
    /// 攻撃が当たったその瞬間（<see cref="TBossGuard.Repelled"/>）に入り、ボスの払いとプレイヤーののけぞりを同じフレームで始める。
    /// はじいたのが近接攻撃なら、プレイヤーは掴みから逃れた時と同じモーションでのけぞって下がる（<see cref="PlayerEscapeHandler"/>）。
    /// 矢・投げた鎌は武器だけをはじき、離れているプレイヤーは動かさない。
    /// はじき返したら、モーションの終わりを待たずに通常攻撃2か掴みへ流す（Animator を強制的に切り替えず、短いモーションから遷移させる）。
    /// </summary>
    public sealed class TBossRepelState : TBossBaseState
    {
        private static readonly int RepelTriggerHash = Animator.StringToHash("Repel");
        private static readonly int RepelStateHash = Animator.StringToHash("Repel");

        // Repel ステートに入れないまま経過したら、トリガー取りこぼしとみなして復帰する
        private const float RepelStateTimeout = 2f;

        private readonly PlayerAttackKind _repelledKind;
        private float _timer;

        /// <param name="repelledKind">はじいた攻撃の種類。近接攻撃ならプレイヤーものけぞらせる</param>
        public TBossRepelState(TutorialBossController controller, PlayerAttackKind repelledKind)
            : base(controller)
        {
            _repelledKind = repelledKind;
        }

        public override bool IsAttack => true;
        public override bool IsCommitted => true;
        public override bool IgnoresFlinch => true;
        public override bool BlocksOpeningGuardAfter => true;

        public override void Enter()
        {
            DevLog.Log($"[TutorialBoss] はじき返し（{_repelledKind}）");

            _timer = 0f;

            // 振り払いの最中も正面の攻撃は鎌で受け止める
            boss.Guard.IsGuarding = true;

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.ResetPath();
                boss.SetAgentStopped(true);
                boss.Agent.updateRotation = false;
            }

            FacePlayerImmediately();
            boss.SetScytheTrailVisible(true);
            boss.Animator.SetTrigger(RepelTriggerHash);

            if (_repelledKind == PlayerAttackKind.Melee)
            {
                KnockBackPlayer();
            }
        }

        public override void Update()
        {
            _timer += Time.deltaTime;

            if (!TryGetRepelProgress(out float progress))
            {
                if (_timer >= RepelStateTimeout)
                {
                    Debug.LogWarning(
                        "[TutorialBoss] Repelステートに入れませんでした。タイムアウトで様子見へ戻ります。"
                    );
                    boss.ChangeState(new TBossWatchState(boss));
                }
                return;
            }

            if (progress >= boss.Guard.Settings.RepelFollowUpNormalizedTime)
            {
                // 通常攻撃2か掴みへ。どちらも間合い・射線が合わなければ様子見に戻る
                if (!boss.TryStartRepelFollowUp())
                {
                    boss.ChangeState(new TBossWatchState(boss));
                }
            }
        }

        public override void Exit()
        {
            boss.Guard.IsGuarding = false;
            boss.SetScytheTrailVisible(false);

            if (boss.Agent != null)
            {
                if (boss.Agent.isOnNavMesh)
                {
                    boss.SetAgentStopped(false);
                }
                boss.Agent.updateRotation = true;
            }
        }

        /// <summary>Repel ステートの再生位置（正規化時間）。遷移中は遷移先の値を見る。まだ入っていなければ false。</summary>
        private bool TryGetRepelProgress(out float progress)
        {
            AnimatorStateInfo state = boss.Animator.IsInTransition(0)
                ? boss.Animator.GetNextAnimatorStateInfo(0)
                : boss.Animator.GetCurrentAnimatorStateInfo(0);
            progress = state.normalizedTime;
            return state.shortNameHash == RepelStateHash;
        }

        /// <summary>はじかれたプレイヤーをのけぞらせて下がらせ、設定によってはダメージも与える。</summary>
        private void KnockBackPlayer()
        {
            if (!boss.Player.TryGetComponent(out PlayerEscapeHandler playerEscape))
            {
                Debug.LogError(
                    "[TutorialBoss] プレイヤーに PlayerEscapeHandler がありません。はじき返してもプレイヤーがのけぞりません。"
                );
                return;
            }

            TBossGuardSettings settings = boss.Guard.Settings;
            if (
                settings.RepelDealsDamage
                && boss.Player.TryGetComponent(out PlayerStatus playerStatus)
            )
            {
                // のけぞりのモーションと重ならないよう、被弾の怯みは出さない
                playerStatus.TakeDamage(
                    boss.Status.CurrentAttackPower * settings.RepelDamageMultiplier,
                    false,
                    suppressFlinch: true
                );
            }

            if (!boss.IsPlayerDead)
            {
                Vector3 away = boss.Player.transform.position - boss.transform.position;
                playerEscape.TriggerRepelled(away);
            }
        }

        private void FacePlayerImmediately()
        {
            Vector3 toPlayer = boss.Player.transform.position - boss.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                boss.transform.rotation = Quaternion.LookRotation(toPlayer);
            }
        }
    }
}
