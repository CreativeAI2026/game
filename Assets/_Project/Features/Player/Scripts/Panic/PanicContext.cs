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

        /// <summary>実際の水平移動速度ベクトル（m/s、y は 0）。</summary>
        public Vector3 HorizontalVelocity { get; set; }

        /// <summary>実際の水平移動速度（m/s）。</summary>
        public float HorizontalSpeed => HorizontalVelocity.magnitude;

        /// <summary>入力から決まる、今出そうとしている水平移動速度（m/s）。移動できない時・入力が無い時は 0。</summary>
        public float DesiredSpeed { get; set; }

        /// <summary>入力から決まる、今進もうとしている水平方向（正規化済み）。移動できない時・入力が無い時は zero。</summary>
        public Vector3 DesiredMoveDirection { get; set; }

        /// <summary>通常の歩き速度（m/s）。速さのしきい値をこれに対する割合で決め、体格・速度調整に追従させる。</summary>
        public float WalkSpeed { get; set; }

        /// <summary>直前に敵の体へ接触したか（敵に押し当てて止まっているだけの状態を「動けない」と区別するため）。</summary>
        public bool IsTouchingEnemy { get; set; }

        /// <summary>移動が許可されているか（掴み・攻撃中などで封じられていれば false）。</summary>
        public bool CanMove { get; set; }

        /// <summary>弓を構えているか。</summary>
        public bool IsAiming { get; set; }

        /// <summary>サブアクション（剣の防御・弓の構えなど）のボタンを押し続けているか。</summary>
        public bool IsSubActionHeld { get; set; }

        /// <summary>
        /// 自分の行動（攻撃モーションなど）で移動が封じられているか。怯み・掴みで封じられている場合は含めない。
        /// 自分から動いている最中なので「何もできずに固まっている」のとは区別する。
        /// </summary>
        public bool IsBusyWithOwnAction { get; set; }

        /// <summary>カメラ（見ている方向）の水平成分（正規化済み）。</summary>
        public Vector3 CameraForwardXZ { get; set; }

        /// <summary>カメラ追従ターゲットのヨー角（度）。</summary>
        public float CameraYaw { get; set; }
    }
}
