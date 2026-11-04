using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスを周回路(<see cref="KurageOrbitPath"/>)に沿って左右に動かし、向きを変える。
    /// 移動の向きに合わせて左移動・右移動のアニメーションを切り替える(Animator の MoveDirection)。
    /// どちらへ・どれだけ動くかはステートが毎フレーム指示する。
    /// </summary>
    public class KurageOrbitMover : MonoBehaviour
    {
        [Tooltip("周回路")]
        [SerializeField]
        private KurageOrbitPath _path;

        [Tooltip("移動アニメーションの左右を切り替える速さ。大きいほど即座に切り替わる")]
        [SerializeField]
        private float _moveDirectionBlendSpeed = 4f;

        private Animator _animator;
        private float _moveDirection;

        /// <summary>周回路上の現在位置（周回路に沿った距離）。</summary>
        public float CurrentS { get; private set; }

        public KurageOrbitPath Path => _path;

        private void Awake()
        {
            Assert.IsNotNull(_path, $"{name}: 周回路(KurageOrbitPath)が設定されていません");
            _animator = GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, $"{name}: 子に Animator がありません");
        }

        private void Start()
        {
            // 配置した場所に一番近い周回路上の点から回り始める
            CurrentS = _path.FindClosestS(transform.position);
            transform.position = _path.Evaluate(CurrentS, out Vector3 outward);
            transform.rotation = Quaternion.LookRotation(-outward);
        }

        /// <summary>
        /// 周回路に沿って動かす。毎フレーム呼ぶ。
        /// </summary>
        /// <param name="direction">+1 で s が増える向き(上から見て反時計回り)、-1 で逆、0 で止まる</param>
        /// <param name="speed">移動の速さ（m/秒）</param>
        public void Move(float direction, float speed)
        {
            Vector3 before = transform.position;
            CurrentS = Mathf.Repeat(CurrentS + direction * speed * Time.deltaTime, _path.Length);
            transform.position = _path.Evaluate(CurrentS);

            // クリップの左右は正面から見た向きで付いている(move_left は体の +X 側へ進む。触手のなびく向きで確かめた)。
            // そのため体の右(+X)へ動く時は左移動(-1)のクリップにする。止まっている間は直前の向きのまま漂わせる
            Vector3 velocity = transform.position - before;
            if (velocity.sqrMagnitude > 1e-8f)
            {
                float target = Vector3.Dot(velocity, transform.right) >= 0f ? -1f : 1f;
                _moveDirection = Mathf.MoveTowards(
                    _moveDirection,
                    target,
                    _moveDirectionBlendSpeed * Time.deltaTime
                );
            }
            _animator.SetFloat(KurageAnimatorParams.MoveDirection, _moveDirection);
        }

        /// <summary>
        /// 正面(+Z)をステージの方へ向ける。毎フレーム呼ぶ。
        /// 移動のクリップは正面を向いたまま左右へ動く作りなので、周回中は常にステージへ正面を向けておく。
        /// </summary>
        /// <param name="turnSpeed">回る速さ（度/秒）。突きで向きを変えた後に戻す速さになる</param>
        public void FaceStage(float turnSpeed)
        {
            _path.Evaluate(CurrentS, out Vector3 outward);
            RotateTowards(-outward, turnSpeed);
        }

        /// <summary>水平方向だけ正面(+Z)を point の方へ向ける（体ごと向く）。毎フレーム呼ぶ。</summary>
        /// <param name="turnSpeed">回る速さ（度/秒）</param>
        public void FaceTowards(Vector3 point, float turnSpeed)
        {
            Vector3 toPoint = point - transform.position;
            toPoint.y = 0f;
            RotateTowards(toPoint, turnSpeed);
        }

        private void RotateTowards(Vector3 direction, float turnSpeed)
        {
            if (direction.sqrMagnitude < 1e-4f)
                return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction),
                turnSpeed * Time.deltaTime
            );
        }

        /// <summary>周回路に沿って target の真横(最も近い点)へ向かう時の向き(+1/-1)。十分近ければ 0。</summary>
        public float DirectionToward(Vector3 target, float arriveDistance)
        {
            float goal = _path.FindClosestS(target);
            float signed = _path.SignedDistance(CurrentS, goal);
            if (Mathf.Abs(signed) <= arriveDistance)
                return 0f;
            return Mathf.Sign(signed);
        }
    }
}
