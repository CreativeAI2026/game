using UnityEngine;
using UnityEngine.Pool;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 矢の飛翔制御を担当する。射出前はボーンの子として静的に存在し、
    /// Launch() 呼び出し時に初めて物理挙動を付与する設計。
    /// ObjectPool による生成・回収に対応するため、Destroy の代わりに ReturnToPool を使う。
    /// AudioSource はこのオブジェクトに固定でアタッチされ、着弾音の再生に使用される。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ArrowProjectile : MonoBehaviour
    {
        [Tooltip("矢が自動回収されるまでの時間（秒）。0以下で自動回収しない。"), SerializeField]
        private float _lifetime = 8f;

        /// <summary>ArrowTipから着弾音の再生に使用するAudioSource。</summary>
        public AudioSource ArrowAudioSource { get; private set; }

        public bool IsFlying { get; private set; }

        // ObjectPoolから設定される（ArrowPoolが呼び出す）
        private IObjectPool<ArrowProjectile> _pool;
        private float _lifetimeTimer;

        private void Awake()
        {
            ArrowAudioSource = GetComponent<AudioSource>();
        }

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
        public void Launch(Vector3 direction, float speed)
        {
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

            Debug.Log("着弾");

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach (var col in cols)
            {
                col.enabled = false;
            }
        }

        /// <summary>
        /// プールへ矢を返却する。プールが未設定の場合はDestroyにフォールバックする。
        /// </summary>
        public void ReturnToPool()
        {
            IsFlying = false;

            if (_pool != null)
            {
                _pool.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsFlying)
                return;

            // 物理イベントはRigidbodyを持つルートに飛んでくるため、先端のArrowTipに転送する
            ArrowTip tip = GetComponentInChildren<ArrowTip>();
            if (tip != null)
            {
                tip.ProcessCollision(collision);
            }
        }
    }
}
