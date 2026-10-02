using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 投げた鎌(ブーメラン)の飛行。まっすぐ最大距離まで飛び、手元(戻り先 Transform)へ追尾して戻る。
    /// 行きで障害物に当たった場合はその場で折り返す。
    ///
    /// ダメージは同じ GameObject の <see cref="PlayerMeleeHitbox"/> に任せる。
    /// 行きと帰りの開始時にそれぞれ EnableHitbox して当たり済みリストを空にするため、
    /// 同じ敵には行きで1回・帰りで1回ヒットする。
    /// </summary>
    [RequireComponent(typeof(PlayerMeleeHitbox))]
    public class ScytheBoomerang : MonoBehaviour
    {
        private enum Phase
        {
            Idle,
            Outbound,
            Returning,
        }

        [Header("飛行")]
        [Tooltip("投げてから折り返すまでの最大距離(m)")]
        [SerializeField]
        private float _maxDistance = 10f;

        [Tooltip("行きの速度(m/秒)")]
        [SerializeField]
        private float _outboundSpeed = 20f;

        [Tooltip("帰りの速度(m/秒)")]
        [SerializeField]
        private float _returnSpeed = 25f;

        [Tooltip("戻り先にこの距離(m)まで近づいたら受け取ったとみなす")]
        [SerializeField]
        private float _catchDistance = 0.5f;

        [Header("障害物")]
        [Tooltip("行きでこのレイヤーに当たったら折り返す(Ground / Obstacle 等)")]
        [SerializeField]
        private LayerMask _obstacleLayer;

        [Tooltip("障害物判定の太さ(半径 m)")]
        [SerializeField]
        private float _obstacleProbeRadius = 0.3f;

        [Header("見た目の回転")]
        [Tooltip("回転させるモデル(子オブジェクト)。ルートを回すと進行方向の計算に影響するため子を回す")]
        [SerializeField]
        private Transform _spinRoot;

        [Tooltip("回転軸(_spinRoot のローカル)")]
        [SerializeField]
        private Vector3 _spinAxis = Vector3.up;

        [Tooltip("回転速度(度/秒)")]
        [SerializeField]
        private float _spinSpeed = 1080f;

        private PlayerMeleeHitbox _hitbox;
        private Phase _phase = Phase.Idle;
        private Vector3 _direction;
        private float _traveled;
        private Transform _returnTarget;

        /// <summary>手元に戻って受け取られたときに通知。</summary>
        public event Action Caught;

        public bool IsFlying => _phase != Phase.Idle;

        private void Awake()
        {
            _hitbox = GetComponent<PlayerMeleeHitbox>();
            Assert.IsNotNull(_spinRoot, $"{name}: 回転させるモデル(_spinRoot)が未設定です");
        }

        /// <summary>
        /// 投げたプレイヤーを設定する(ダメージ計算に使う)。
        /// この投擲体はプレイヤーの階層の外に置かれるため、攻撃判定は親から持ち主を辿れない。
        /// </summary>
        public void SetOwner(PlayerStatus owner) => _hitbox.SetOwner(owner);

        /// <param name="origin">投げ始めの位置。</param>
        /// <param name="direction">行きの進行方向(正規化済み)。</param>
        /// <param name="returnTarget">戻り先。移動する手元を毎フレーム追う。</param>
        public void Launch(Vector3 origin, Vector3 direction, Transform returnTarget)
        {
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            _direction = direction;
            _returnTarget = returnTarget;
            _traveled = 0f;
            _phase = Phase.Outbound;

            gameObject.SetActive(true);
            _hitbox.EnableHitbox();
        }

        /// <summary>飛行中なら即座に受け取り扱いにする(武器切替・破棄時の後始末用)。</summary>
        public void ForceCatch()
        {
            if (IsFlying)
                Catch();
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Outbound:
                    UpdateOutbound();
                    break;
                case Phase.Returning:
                    UpdateReturning();
                    break;
            }

            _spinRoot.Rotate(_spinAxis, _spinSpeed * Time.deltaTime, Space.Self);
        }

        private void UpdateOutbound()
        {
            float step = _outboundSpeed * Time.deltaTime;

            bool hitObstacle = Physics.SphereCast(
                transform.position,
                _obstacleProbeRadius,
                _direction,
                out _,
                step,
                _obstacleLayer,
                QueryTriggerInteraction.Ignore
            );
            if (hitObstacle)
            {
                BeginReturn();
                return;
            }

            transform.position += _direction * step;
            _traveled += step;

            if (_traveled >= _maxDistance)
                BeginReturn();
        }

        private void UpdateReturning()
        {
            Vector3 target = _returnTarget.position;
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                _returnSpeed * Time.deltaTime
            );

            Vector3 toTarget = target - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toTarget);

            if (toTarget.magnitude <= _catchDistance)
                Catch();
        }

        private void BeginReturn()
        {
            _phase = Phase.Returning;
            // 当たり済みリストを空にし、行きで当たった敵にも帰りでもう1回当たるようにする
            _hitbox.EnableHitbox();
        }

        private void Catch()
        {
            _phase = Phase.Idle;
            _hitbox.DisableHitbox();
            gameObject.SetActive(false);
            Caught?.Invoke();
        }
    }
}
