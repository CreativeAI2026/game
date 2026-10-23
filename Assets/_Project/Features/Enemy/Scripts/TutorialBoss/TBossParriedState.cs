using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 大きく崩れるステート。Animator の Parried モーションを最後まで再生してから交戦に戻る。
    /// 入るのは、近接攻撃をジャストパリィされた時(ForceParried)と、掴みをレバガチャで振りほどかれた時(TBossCapturedState)。
    /// 通常の被弾で怯む時は TBossFlinchState(Flinch モーション)を使う。
    /// 崩れている間は顔向けも切れるため（IsHeadAimActive が false）、プレイヤーは反撃するか視界から抜けるかを選べる。
    ///
    /// Parried のクリップ(TBoss_Escaped_InPlace)は腰の後退移動を切ってあるため、後退はここで NavMeshAgent を動かして行う。
    /// 距離と区間は元クリップの腰の移動に合わせてある(足の運びと移動がずれないように)。
    /// </summary>
    public class TBossParriedState : TBossBaseState
    {
        // Parriedステートに入れないまま経過したら、トリガー取りこぼしとみなして復帰する
        private const float ParriedStateTimeout = 3f;

        // 後退距離(ボスのローカル単位)。元クリップの腰はボスのローカルで約1.36後退する。
        // 見た目の大きさに比例させるため、ワールド距離はボスのスケールを掛けて求める(現在のスケール2.5で約3.4m)
        private const float RecoilLocalDistance = 1.36f;

        // 後退する区間(Parriedステートの正規化時間)。元クリップの腰は先頭から約8割の時点でほぼ止まる
        private const float RecoilEndNormalizedTime = 0.8f;

        private static readonly int ParriedStateHash = Animator.StringToHash("Parried");

        private float _timer;

        public TBossParriedState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 大きく崩れる(パリィ/掴みからの脱出)");

            _timer = 0f;

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.ResetPath();
                boss.Agent.velocity = Vector3.zero;
                boss.SetAgentStopped(true);
            }

            boss.StopSwingEffects();

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Parried");
            }
        }

        public override void Update()
        {
            _timer += Time.deltaTime;

            if (boss.Animator == null)
            {
                ReturnToCombat();
                return;
            }

            UpdateRecoil();

            AnimatorStateInfo stateInfo = boss.Animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Parried") && stateInfo.normalizedTime >= 1.0f)
            {
                ReturnToCombat();
                return;
            }

            if (!stateInfo.IsName("Parried") && _timer >= ParriedStateTimeout)
            {
                Debug.LogWarning(
                    "[TutorialBoss] Parriedステートに入れませんでした。タイムアウトで復帰します。"
                );
                ReturnToCombat();
            }
        }

        public override void Exit()
        {
            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.SetAgentStopped(false);
            }
        }

        /// <summary>Parried モーションの再生位置に合わせて、ボスの後方へ等速で下げる。</summary>
        private void UpdateRecoil()
        {
            if (boss.Agent == null || !boss.Agent.isOnNavMesh)
                return;

            // Parried へ遷移している最中から動かし始める(遷移中は行き先が next 側にある)
            AnimatorStateInfo recoilState = boss.Animator.IsInTransition(0)
                ? boss.Animator.GetNextAnimatorStateInfo(0)
                : boss.Animator.GetCurrentAnimatorStateInfo(0);
            if (
                recoilState.shortNameHash != ParriedStateHash
                || recoilState.normalizedTime >= RecoilEndNormalizedTime
            )
                return;

            float distance = RecoilLocalDistance * boss.transform.lossyScale.z;
            float speed = distance / (recoilState.length * RecoilEndNormalizedTime);
            boss.Agent.Move(-boss.transform.forward * (speed * Time.deltaTime));
        }

        private void ReturnToCombat()
        {
            // パリィも掴みからの脱出も交戦中にしか起きないので、警戒は保ったまま追跡へ戻る（見えていなければ追跡側で見失い処理に入る）
            boss.IsAlerted = true;
            boss.ChangeState(new TBossChaseState(boss));
        }
    }
}
