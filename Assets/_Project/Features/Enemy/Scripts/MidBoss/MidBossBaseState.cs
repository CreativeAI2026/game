using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// MidBossの全ステートが共有する基底クラス。
    /// コントローラへの参照を保持し、共通ロジックを提供する。
    /// </summary>
    public class MidBossBaseState : IEnemyState
    {
        protected MidBossController con;

        public MidBossBaseState(MidBossController controller)
        {
            con = controller;
        }

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void Exit() { }
    }
}
