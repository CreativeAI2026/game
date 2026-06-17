using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 矢の飛翔スクリプト。BowController から Launch() を呼ばれて動作する。
    ///
    /// ■ 矢Prefabの構造
    ///   ArrowNockRoot（このスクリプトをアタッチ）
    ///     - CapsuleCollider（初期状態は disabled にしておく）
    ///     └── ArrowMesh（ノック端が親の原点に来るようオフセット配置）
    ///
    /// ■ Rigidbodyについて
    ///   つがえ中（BowControllerが保持中）は Rigidbody を持たない。
    ///   Launch() 呼び出し時に AddComponent で追加するため、
    ///   ボーン追従中の Rigidbody 干渉は一切発生しない。
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        [Header("飛翔設定")]
        [Tooltip("矢が自動消滅するまでの時間（秒）")]
        [SerializeField]
        private float _lifetime = 5f;

        public bool IsFlying { get; private set; }

        // -------------------------------------------------------
        // 発射
        // -------------------------------------------------------

        /// <summary>
        /// BowController から呼ばれる。
        /// Rigidbodyを動的に追加し、direction方向へ speed で飛翔させる。
        /// transform.rotation も進行方向に合わせるため、矢が水平に飛ぶ。
        /// </summary>
        public void Launch(Vector3 direction, float speed)
        {
            IsFlying = true;

            // 進行方向に矢を向ける（これで飛行方向に対して矢が水平になる）
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);

            // 先端の Collider など子も含めて有効化する
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                col.enabled = true;
            }

            // Rigidbody を動的追加（つがえ中は持たないためボーン追従に干渉しない）
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = direction.normalized * speed;

            Destroy(gameObject, _lifetime);
        }

        // -------------------------------------------------------
        // 停止処理
        // -------------------------------------------------------

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

        // -------------------------------------------------------
        // 衝突イベントの転送
        // -------------------------------------------------------

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsFlying)
                return;

            // 物理イベントはRigidbodyを持つルートに飛んでくるため、先端に転送する
            ArrowTip tip = GetComponentInChildren<ArrowTip>();
            if (tip != null)
            {
                tip.ProcessCollision(collision);
            }
        }
    }
}
