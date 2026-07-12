using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 敵のStateパターン用インターフェース。
    /// 各ステートはこのインターフェースを実装し、EnemyBaseController.ChangeStateで切り替えられる。
    /// </summary>
    public interface IEnemyState
    {
        void Enter();

        void Update();

        void Exit();
    }
}
