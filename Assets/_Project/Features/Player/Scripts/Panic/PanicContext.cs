using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度の各指標が1フレームの計測に使う観測値。PanicDetector が毎フレーム詰め直して各指標へ渡す。
    /// 指標がプレイヤーや敵のコンポーネントを直接読まないようにし、何を根拠に判定したかをここに集める。
    /// </summary>
    public class PanicContext
    {
        // --- 交戦状況 ---

        /// <summary>プレイヤーに気付いて交戦している敵。いなければ null。</summary>
        public EnemyBaseController EngagedEnemy { get; set; }

        /// <summary>交戦中の敵がいるか。回避失敗以外の指標はこれが true の間だけ計測する。</summary>
        public bool IsInCombat => EngagedEnemy != null;

        /// <summary>交戦中の敵が攻撃ステートの最中か。交戦中の敵がいなければ false。</summary>
        public bool IsEnemyAttacking => EngagedEnemy != null && EngagedEnemy.IsAttacking;

        /// <summary>プレイヤーから交戦中の敵への水平方向（正規化済み）。交戦中でなければ zero。</summary>
        public Vector3 ToEnemyDirectionXZ { get; set; }

        /// <summary>プレイヤーと交戦中の敵との水平距離（m）。交戦中でなければ 0。</summary>
        public float EnemyDistance { get; set; }

        // --- プレイヤーの状態 ---

        /// <summary>移動入力（スティック / WASD）。</summary>
        public Vector2 MoveInput { get; set; }

        /// <summary>移動入力があるか。</summary>
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0.01f;

        /// <summary>実際の水平移動速度（m/s）。</summary>
        public float HorizontalSpeed { get; set; }

        /// <summary>移動が許可されているか（掴み・攻撃中などで封じられていれば false）。</summary>
        public bool CanMove { get; set; }

        /// <summary>弓を構えているか。</summary>
        public bool IsAiming { get; set; }

        /// <summary>カメラ（見ている方向）の水平成分（正規化済み）。</summary>
        public Vector3 CameraForwardXZ { get; set; }

        /// <summary>カメラ追従ターゲットのヨー角（度）。</summary>
        public float CameraYaw { get; set; }
    }
}
