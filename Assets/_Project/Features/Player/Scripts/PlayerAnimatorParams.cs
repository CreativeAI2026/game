using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの AnimatorController(PlayerAnimator)のパラメータ名とステート名。
    /// 文字列の直書きを散らさず、ここを唯一の対応表にする。Animator 側で名前を変えたらここも変える。
    /// 必要なパラメータが揃っているかは <see cref="PlayerModel"/> が起動時に検査する。
    /// </summary>
    public static class PlayerAnimatorParams
    {
        // --- 移動(PlayerController) ---
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int MotionSpeed = Animator.StringToHash("MotionSpeed");
        public static readonly int Grounded = Animator.StringToHash("Grounded");
        public static readonly int Jump = Animator.StringToHash("Jump");
        public static readonly int FreeFall = Animator.StringToHash("FreeFall");
        public static readonly int Aim = Animator.StringToHash("Aim");
        public static readonly int StrafeX = Animator.StringToHash("StrafeX");
        public static readonly int StrafeZ = Animator.StringToHash("StrafeZ");

        // --- 行動(Trigger 名 = 行き先のステート名) ---
        /// <summary>3段コンボの各段。index 0 が1段目。</summary>
        public static readonly int[] Attacks =
        {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3"),
        };
        public static readonly int Approach = Animator.StringToHash("Approach");
        public static readonly int Parry = Animator.StringToHash("Parry");
        public static readonly int Throw = Animator.StringToHash("Throw");
        public static readonly int Slam = Animator.StringToHash("Slam");
        public static readonly int Fire = Animator.StringToHash("Fire");
        public static readonly int IsGuarding = Animator.StringToHash("IsGuarding");

        /// <summary>行動ステートを抜けて移動へ戻す(ActionEnd イベント・硬直キャンセル時にスクリプトが送る)。</summary>
        public static readonly int ReturnToLocomotion = Animator.StringToHash("ReturnToLocomotion");

        // --- 被弾・拘束 ---
        public static readonly int Flinch = Animator.StringToHash("PlayerFlinch");
        public static readonly int Grabbed = Animator.StringToHash("Grabbed");
        public static readonly int Escaped = Animator.StringToHash("Escaped");

        /// <summary>起動時の検査に使う、必須パラメータの一覧(名前, 種類)。</summary>
        public static readonly (string name, AnimatorControllerParameterType type)[] Required =
        {
            ("Speed", AnimatorControllerParameterType.Float),
            ("MotionSpeed", AnimatorControllerParameterType.Float),
            ("Grounded", AnimatorControllerParameterType.Bool),
            ("Jump", AnimatorControllerParameterType.Bool),
            ("FreeFall", AnimatorControllerParameterType.Bool),
            ("Aim", AnimatorControllerParameterType.Bool),
            ("StrafeX", AnimatorControllerParameterType.Float),
            ("StrafeZ", AnimatorControllerParameterType.Float),
            ("Attack1", AnimatorControllerParameterType.Trigger),
            ("Attack2", AnimatorControllerParameterType.Trigger),
            ("Attack3", AnimatorControllerParameterType.Trigger),
            ("Approach", AnimatorControllerParameterType.Trigger),
            ("Parry", AnimatorControllerParameterType.Trigger),
            ("Throw", AnimatorControllerParameterType.Trigger),
            ("Slam", AnimatorControllerParameterType.Trigger),
            ("Fire", AnimatorControllerParameterType.Trigger),
            ("IsGuarding", AnimatorControllerParameterType.Bool),
            ("ReturnToLocomotion", AnimatorControllerParameterType.Trigger),
            ("PlayerFlinch", AnimatorControllerParameterType.Trigger),
            ("Grabbed", AnimatorControllerParameterType.Trigger),
            ("Escaped", AnimatorControllerParameterType.Trigger),
        };
    }

    /// <summary>プレイヤーの AnimatorController のステート名(shortNameHash で比較する。サブステートマシンに入れても変わらない)。</summary>
    public static class PlayerAnimatorStates
    {
        /// <summary>3段コンボの各段。index 0 が1段目。</summary>
        public static readonly int[] Attacks =
        {
            Animator.StringToHash("Attack1"),
            Animator.StringToHash("Attack2"),
            Animator.StringToHash("Attack3"),
        };
        public static readonly int Approach = Animator.StringToHash("Approach");
        public static readonly int Guard = Animator.StringToHash("Guard");
        public static readonly int Parry = Animator.StringToHash("Parry");
        public static readonly int Throw = Animator.StringToHash("Throw");
        public static readonly int Slam = Animator.StringToHash("Slam");
        public static readonly int Flinch = Animator.StringToHash("Flinch");

        /// <summary>弓の構え・発射を再生する上半身レイヤーの名前。</summary>
        public const string UpperBodyLayerName = "UpperBody Layer";

        // UpperBody Layer
        public static readonly int Aim = Animator.StringToHash("Aim");
        public static readonly int Fire = Animator.StringToHash("Fire");

        /// <summary>ログ用に、3段コンボの段数からステート名を得る。</summary>
        public static string AttackName(int step) => "Attack" + step;
    }
}
