using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TutorialBossの全ステートが共有する基底クラス。
    /// コントローラへの参照を保持し、共通ロジックを提供する。
    /// ステートごとの性質（攻撃中か・見渡してよいか等）はここの virtual を各ステートが上書きして表し、
    /// コントローラはステートの型を見ずにこれらへ問い合わせる。
    /// </summary>
    public class TBossBaseState : IEnemyState
    {
        protected TutorialBossController boss;

        public TBossBaseState(TutorialBossController controller)
        {
            boss = controller;
        }

        /// <summary>
        /// ボス本体を水平に <paramref name="target"/> の方へ滑らかに向ける。
        /// 水平の距離の2乗が <paramref name="minSqrDistance"/> 未満なら、向きが定まらないので回さない。
        /// </summary>
        /// <param name="speed">1秒あたりの補間の速さ(Slerp の t に deltaTime と掛けて使う)</param>
        protected void FaceTowards(Vector3 target, float speed, float minSqrDistance = 0.0001f)
        {
            Vector3 direction = target - boss.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < minSqrDistance)
            {
                return;
            }

            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                Quaternion.LookRotation(direction),
                Time.deltaTime * speed
            );
        }

        /// <summary>
        /// 攻撃の行動中か（通常攻撃・通常攻撃2・特殊攻撃・はじき返し。走り込みの接近フェーズも含む）。
        /// 捕獲は攻撃が当たった後の別の行動なので含めない。
        /// </summary>
        public virtual bool IsAttack => false;

        /// <summary>周りを見渡すモーションを出してよいか（巡回・捜索・音の調査）。</summary>
        public virtual bool AllowsLookAround => false;

        /// <summary>顔（と頭の懐中電灯）をプレイヤーへ向けるか。攻撃・怯み・捕獲・未発見時はアニメーションに任せる。</summary>
        public virtual bool AimsHeadAtPlayer => false;

        /// <summary>プレイヤーと交戦中（追跡・様子見）か。遠距離の防御を引けるのもこの間だけ。</summary>
        public virtual bool IsEngaging => false;

        /// <summary>プレイヤーの攻撃ボタンの入力に反応して防御してよいか（様子見の間だけ）。</summary>
        public virtual bool ReactsToAttackInput => false;

        /// <summary>連打されたらはじき返せるか。それ以外の行動中に殴られた分は数えない。</summary>
        public virtual bool CanRepel => false;

        /// <summary>被弾や音で差し替えてはいけない、やり切るべき行動の最中か。</summary>
        public virtual bool IsCommitted => false;

        /// <summary>プレイヤーを捕獲している最中か。</summary>
        public virtual bool IsCapturingPlayer => false;

        /// <summary>死亡した後か。</summary>
        public virtual bool IsDead => false;

        /// <summary>被弾しても怯まずにやり切るか。</summary>
        public virtual bool IgnoresFlinch => false;

        /// <summary>このステートの後に様子見へ入った時、「攻撃後の防御」を引くか（攻撃・捕獲の後）。</summary>
        public virtual bool TriggersPostAttackGuard => false;

        /// <summary>このステートの後に様子見へ入った時、防御を引かないか（防御が続けて出るのを防ぐ）。</summary>
        public virtual bool BlocksOpeningGuardAfter => false;

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void LateUpdate() { }

        public virtual void Exit() { }

        /// <summary>クリップのアニメーションイベントを受ける。使うステートだけが上書きする。</summary>
        public virtual void OnAnimationEvent(TBossAnimationEvent animationEvent) { }

        /// <summary>プレイヤーが矢を撃った・鎌を投げた瞬間に呼ばれる。</summary>
        public virtual void OnPlayerRangedAttack() { }
    }
}
