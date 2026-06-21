using UnityEngine;

namespace CreativeAI.Gameplay
{
    public interface IEnemyState
    {
        // その状態に入った瞬間に1回だけ呼ばれる（アニメーションの再生など）
        void Enter();

        // その状態の間、毎フレーム呼ばれる（移動や距離の計算など）
        void Update();

        // 別の状態に切り替わる瞬間に1回だけ呼ばれる（フラグのリセットなど）
        void Exit();
    }
}
