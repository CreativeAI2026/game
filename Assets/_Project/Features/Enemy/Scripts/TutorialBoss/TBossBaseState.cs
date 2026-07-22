using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TutorialBossの全ステートが共有する基底クラス。
    /// コントローラへの参照を保持し、共通ロジックを提供する。
    /// </summary>
    public class TBossBaseState : IEnemyState
    {
        protected TutorialBossController boss;

        public TBossBaseState(TutorialBossController controller)
        {
            boss = controller;
        }

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void Exit() { }
    }
}
