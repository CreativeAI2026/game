using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 矢の飛翔制御を担当する。射出前はボーンの子として静的に存在し、
    /// Launch() 呼び出し時に初めて物理挙動を付与する設計。
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        [Tooltip("矢が自動消滅するまでの時間（秒）"), SerializeField]
        private float _lifetime = 5f;

        public bool IsFlying { get; private set; }

        /// <summary>
        /// 射出時にRigidbodyを動的追加して飛翔を開始する。
        /// つがえ中（ボーンの子オブジェクト状態）にRigidbodyがあると
        /// アニメーションと物理が干渉するため、射出時に初めて追加する。
        /// </summary>
        public void Launch(Vector3 direction, float speed)
        {
            IsFlying = true;

            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);

            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                col.enabled = true;
            }

            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // 高速で飛翔する矢が薄いコライダーを貫通するのを防ぐため、ContinuousDynamicを使用
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = direction.normalized * speed;

            Destroy(gameObject, _lifetime);
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
