using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 連打してくる相手を、腕の鎌の短い薙ぎ払いではじき返すステート。
    /// 届く距離のプレイヤーには、掴みから逃れた時と同じ後退モーション（<see cref="PlayerEscapeHandler"/>）を取らせる。
    /// 押し返しとして扱うので、剣のガード・パリィでは防げない。遠くから矢を撃っている相手には振り払う動きだけになる。
    /// はじき返したら、モーションの終わりを待たずに通常攻撃2か掴みへ流す（Animator を強制的に切り替えず、短いモーションから遷移させる）。
    /// </summary>
    public sealed class TBossRepelState : TBossBaseState
    {
        private static readonly int RepelTriggerHash = Animator.StringToHash("Repel");
        private static readonly int RepelStateHash = Animator.StringToHash("Repel");

        // Repel ステートに入れないまま経過したら、トリガー取りこぼしとみなして復帰する
        private const float RepelStateTimeout = 2f;

        private PlayerEscapeHandler _playerEscape;
        private PlayerStatus _playerStatus;
        private float _timer;
        private bool _hasImpacted;

        public TBossRepelState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            DevLog.Log("[TutorialBoss] はじき返し");

            _timer = 0f;
            _hasImpacted = false;
            _playerEscape = boss.Player.GetComponent<PlayerEscapeHandler>();
            _playerStatus = boss.Player.GetComponent<PlayerStatus>();
            if (_playerEscape == null)
            {
                Debug.LogError(
                    "[TutorialBoss] プレイヤーに PlayerEscapeHandler がありません。はじき返してもプレイヤーが下がりません。"
                );
            }

            // 振り払いの最中も正面の攻撃は鎌で受け止める。殴られた回数は、はじき返したのでやり直す
            boss.Guard.IsGuarding = true;
            boss.Guard.ClearMash();

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.ResetPath();
                boss.SetAgentStopped(true);
                boss.Agent.updateRotation = false;
            }

            boss.StopSwingEffects();
            FacePlayerImmediately();
            boss.ShowScytheTrail();
            boss.Sounds.PlayRepel();
            boss.Animator.SetTrigger(RepelTriggerHash);
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

            TBossGuardSettings settings = boss.Guard.Settings;
            if (!_hasImpacted && progress >= settings.RepelImpactNormalizedTime)
            {
                _hasImpacted = true;
                ApplyRepel(settings);
            }

            if (progress >= settings.RepelFollowUpNormalizedTime)
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
            boss.HideScytheTrail();

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

        /// <summary>振り払いが当たった瞬間。届く距離のプレイヤーを下がらせ、設定によってはダメージも与える。</summary>
        private void ApplyRepel(TBossGuardSettings settings)
        {
            Vector3 away = boss.Player.transform.position - boss.transform.position;
            away.y = 0f;
            if (away.magnitude > settings.RepelRadius || boss.IsPlayerDead)
            {
                return;
            }

            if (settings.RepelDealsDamage)
            {
                // 後退モーションと重ならないよう、被弾の怯みは出さない
                _playerStatus.TakeDamage(
                    boss.Status.CurrentAttackPower * settings.RepelDamageMultiplier,
                    false,
                    suppressFlinch: true
                );
            }

            if (_playerEscape != null && !boss.IsPlayerDead)
            {
                _playerEscape.TriggerEscape(away);
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
