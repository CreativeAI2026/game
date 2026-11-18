using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 地面に刺した鎌を引きずる間、先端の真下の地面から火花を出し続ける。刺さった瞬間の火花もここで出す。
    /// 火花の見た目はパーティクル側で作り、ここは出す位置・向き・タイミングだけを決める。
    /// </summary>
    public sealed class TBossGroundDragSparks : MonoBehaviour
    {
        // 先端が地面へ少し潜っていても地面を拾えるよう、この高さから下へ探す
        private const float GroundProbeHeight = 1.5f;
        private const float GroundProbeDistance = 4f;

        // 火花を出す高さ（地面からの浮き、m）。地面の中から出すと、地面との衝突で出た瞬間に消えるため浮かせる
        private const float EmitHeight = 0.08f;

        [Tooltip("鎌の先端")]
        [SerializeField]
        private Transform _tip;

        [Tooltip("火花のパーティクル。引きずっている間だけ放出を有効にする（+Z が火花の飛ぶ向き）")]
        [SerializeField]
        private ParticleSystem _sparks;

        [Tooltip("地面として扱うレイヤー")]
        [SerializeField]
        private LayerMask _groundLayer;

        [Tooltip("刺さった瞬間に一度に出す火花の数")]
        [SerializeField]
        private int _impactBurstCount = 40;

        private bool _isDragging;

        private void Awake()
        {
            Assert.IsNotNull(_tip, $"{name}: TBossGroundDragSparks の先端が未設定です");
            Assert.IsNotNull(_sparks, $"{name}: TBossGroundDragSparks の火花が未設定です");
            SetEmission(false);
        }

        /// <summary>鎌が地面に刺さった瞬間の火花を出す。</summary>
        public void PlayImpact()
        {
            MoveToGround();
            _sparks.Emit(_impactBurstCount);
        }

        /// <summary>引きずりの火花を出し始める。</summary>
        public void BeginDrag()
        {
            _isDragging = true;
            MoveToGround();
            SetEmission(true);
        }

        /// <summary>引きずりの火花を止める。出ている火花は消えるまで残る。</summary>
        public void EndDrag()
        {
            _isDragging = false;
            SetEmission(false);
        }

        // アニメーションが骨を動かした後の先端の位置を使うため LateUpdate で追う
        private void LateUpdate()
        {
            if (_isDragging)
            {
                MoveToGround();
            }
        }

        private void SetEmission(bool enabled)
        {
            ParticleSystem.EmissionModule emission = _sparks.emission;
            emission.enabled = enabled;
        }

        /// <summary>火花を先端の真下の地面へ移し、引きずる向きの逆（ボスから遠ざかる側）へ飛ぶよう向ける。</summary>
        private void MoveToGround()
        {
            Vector3 tipPosition = _tip.position;
            Vector3 groundPoint = tipPosition;
            if (
                Physics.Raycast(
                    tipPosition + Vector3.up * GroundProbeHeight,
                    Vector3.down,
                    out RaycastHit hit,
                    GroundProbeDistance,
                    _groundLayer,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                groundPoint = hit.point;
            }

            Vector3 awayFromBoss = groundPoint - transform.position;
            awayFromBoss.y = 0f;
            Quaternion rotation =
                awayFromBoss.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(awayFromBoss.normalized, Vector3.up)
                    : transform.rotation;

            _sparks.transform.SetPositionAndRotation(
                groundPoint + Vector3.up * EmitHeight,
                rotation
            );
        }
    }
}
