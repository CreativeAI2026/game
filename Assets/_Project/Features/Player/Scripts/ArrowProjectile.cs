using UnityEngine;
using UnityEngine.Pool;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 矢の飛翔制御を担当する。射出前はボーンの子として静的に存在し、
    /// Launch() 呼び出し時に初めて物理挙動を付与する設計。
    /// ObjectPool による生成・回収に対応するため、Destroy の代わりに ReturnToPool を使う。
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        [Tooltip("矢が自動回収されるまでの時間（秒）。0以下で自動回収しない。"), SerializeField]
        private float _lifetime = 8f;

        public bool IsFlying { get; private set; }

        /// <summary>この矢を撃ったプレイヤー(Launch で設定)。</summary>
        public PlayerStatus Owner { get; private set; }

        // ObjectPoolから設定される（ArrowPoolが呼び出す）
        private IObjectPool<ArrowProjectile> _pool;
        private float _lifetimeTimer;

        private void Update()
        {
            if (!IsFlying)
            {
                return;
            }

            if (_lifetime > 0f)
            {
                _lifetimeTimer += Time.deltaTime;
                if (_lifetimeTimer >= _lifetime)
                {
                    ReturnToPool();
                }
            }
        }

        /// <summary>
        /// ObjectPoolがこの矢をプールに返却する際に使うコールバックを設定する。
        /// ArrowPool.Get() から呼ばれる。
        /// </summary>
        public void SetPool(IObjectPool<ArrowProjectile> pool)
        {
            _pool = pool;
        }

        /// <summary>
        /// 射出時にRigidbodyを動的追加して飛翔を開始する。
        /// つがえ中（ボーンの子オブジェクト状態）にRigidbodyがあると
        /// アニメーションと物理が干渉するため、射出時に初めて追加する。
        /// </summary>
        /// <param name="owner">撃ったプレイヤー。命中時のダメージ計算(攻撃力・会心)に使う。</param>
        public void Launch(Vector3 direction, float speed, PlayerStatus owner)
        {
            Owner = owner;
            IsFlying = true;
            _lifetimeTimer = 0f;

            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);

            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                col.enabled = true;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // 高速で飛翔する矢が薄いコライダーを貫通するのを防ぐため、ContinuousDynamicを使用
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = direction.normalized * speed;
        }

        public void StopFlying()
        {
            IsFlying = false;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                // 刺さった物が動いても矢が付いていくよう、飛翔用の補間を切る(残すと古い位置に描かれる)
                rb.interpolation = RigidbodyInterpolation.None;
            }

            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach (var col in cols)
            {
                col.enabled = false;
            }
        }

        /// <summary>
        /// プールから借りた直後に、手に持てる状態へ戻す(ArrowPool が呼ぶ)。
        /// 前回飛んだ時の Rigidbody(物理有効・速度あり)とコライダーが残っていると、
        /// 手の子にしても物理で飛んでいったり周囲に当たったりして、手に握られない。
        /// </summary>
        public void ResetForHold()
        {
            IsFlying = false;
            Owner = null;
            _lifetimeTimer = 0f;

            // Rigidbody は初回の Launch で追加されるので、まだ一度も飛んでいない矢には無い
            // (壁に刺さって回収された矢は既に kinematic。kinematic への速度設定は警告になるので避ける)
            if (TryGetComponent(out Rigidbody rb))
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
                // 飛翔用の補間が残っていると、手の子にしても描画位置が物理側の古い位置から補間で上書きされ、
                // 手から大きくずれた位置・角度に見える(再利用した矢だけで起きる)
                rb.interpolation = RigidbodyInterpolation.None;
            }

            foreach (var col in GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        /// <summary>プールへ矢を返却する(矢は必ず ArrowPool から借りる)。</summary>
        public void ReturnToPool()
        {
            IsFlying = false;
            _pool.Release(this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsFlying)
                return;

            // 物理イベントはRigidbodyを持つルートに飛んでくるため、先端のArrowTipに転送する
            GetComponentInChildren<ArrowTip>().ProcessCollision(collision);
        }
    }
}
