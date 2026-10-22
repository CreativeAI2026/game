using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 追跡・様子見の間だけ、首と頭をプレイヤーへ向ける。
    /// 懐中電灯（視界）は頭ボーンの子に置くため、顔の向き＝視界の向きになり、移動方向と見る方向を分けられる。
    /// 攻撃・怯み・捕獲・未発見時は効きを 0 にしてアニメーションに任せる。
    /// 攻撃ポーズを崩さないためと、攻撃後の硬直中に視界から抜ける逃げ道をプレイヤーに与えるため。
    ///
    /// Animation Rigging の Multi-Aim はこのボスの骨格では頭の位置を実際より高く計算して真下を向いてしまったため使わない。
    /// Animator がポーズを書き込んだ後（LateUpdate）に、実際の骨の位置から回転を求めて上書きする。
    /// Animator は毎フレームポーズを書き直すため、上書きが積み重なることはない。
    /// クラス名は Rig 方式の名残だが、シーン上の参照を壊さないため変えていない。
    /// </summary>
    public class TBossAimRigController : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField]
        private TutorialBossController boss;

        [Tooltip("首ボーン。顔向けの一部をここで負担させ、頭だけが折れ曲がって見えるのを防ぐ。")]
        [SerializeField]
        private Transform neckBone;

        [Tooltip("頭ボーン。ローカルZ軸が顔の正面である前提。")]
        [SerializeField]
        private Transform headBone;

        [Header("顔向け設定")]
        [Tooltip(
            "体の正面から顔を向けられる最大角度。これを超える位置は限界角度の境界を狙う（首が不自然に回るのを防ぐ）。"
        )]
        [SerializeField]
        private float headMaxAngle = 70f;

        [Tooltip(
            "向きを変える量のうち首が負担する割合（0〜1）。残りを頭が負担して狙いを合わせきる。"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float neckShare = 0.4f;

        [Tooltip("狙い先の追従の滑らかさ。")]
        [SerializeField]
        private float targetSmoothSpeed = 8f;

        [Tooltip("顔向けの効きを 0⇔1 で切り替えるのにかける時間（秒）。0だと首がカクッと跳ねる。")]
        [SerializeField]
        private float weightBlendDuration = 0.25f;

        private float _weight;

        // 狙う向きを体基準のローカル方向で補間する。
        // ワールド座標の点で補間すると、体が向きを変えた時に狙いだけがその場に取り残され、首が体に遅れてついてくるため
        private Vector3 _smoothedLocalAimDir = Vector3.forward;

        private void Awake()
        {
            Assert.IsNotNull(boss, $"{name}: boss が未設定です");
            Assert.IsNotNull(neckBone, $"{name}: neckBone が未設定です");
            Assert.IsNotNull(headBone, $"{name}: headBone が未設定です");
        }

        private void LateUpdate()
        {
            UpdateWeight();

            if (_weight <= 0f)
            {
                // 効きが0の間も正面に置き直しておき、次に効かせた瞬間に古い向きへ首が振られるのを防ぐ
                _smoothedLocalAimDir = Vector3.forward;
                return;
            }

            Vector3 desiredDir = ClampToHeadRange(GetDesiredAimPoint()) - headBone.position;
            if (desiredDir.sqrMagnitude > 0.0001f)
            {
                _smoothedLocalAimDir = Vector3.Slerp(
                    _smoothedLocalAimDir,
                    boss.transform.InverseTransformDirection(desiredDir.normalized),
                    Time.deltaTime * targetSmoothSpeed
                );
            }

            Vector3 aimDir = boss.transform.TransformDirection(_smoothedLocalAimDir);

            // 首→頭の順に回す。どちらも「顔の正面（頭のZ軸）を狙う向きへ合わせる回転」を基準に、首はその一部だけを負担する
            RotateBoneTowardAim(neckBone, aimDir, _weight * neckShare);
            RotateBoneTowardAim(headBone, aimDir, _weight);
        }

        private void UpdateWeight()
        {
            float targetWeight = boss.IsHeadAimActive ? 1f : 0f;
            float maxDelta = weightBlendDuration > 0f ? Time.deltaTime / weightBlendDuration : 1f;
            _weight = Mathf.MoveTowards(_weight, targetWeight, maxDelta);
        }

        /// <summary>
        /// 顔の正面（頭のZ軸）が aimDir を向くための回転のうち、weight 分だけを bone に加える。
        /// 首を回すと頭の向きも変わるため、頭は首の後に現在の向きから計算し直して狙いを合わせきる。
        /// </summary>
        private void RotateBoneTowardAim(Transform bone, Vector3 aimDir, float weight)
        {
            if (weight <= 0f)
            {
                return;
            }

            Quaternion fullRotation = Quaternion.FromToRotation(headBone.forward, aimDir);
            bone.rotation =
                Quaternion.Slerp(Quaternion.identity, fullRotation, weight) * bone.rotation;
        }

        /// <summary>体の正面から可動域を超える位置は、限界角度の境界を狙う。</summary>
        private Vector3 ClampToHeadRange(Vector3 desired)
        {
            Vector3 toTarget = desired - headBone.position;
            Vector3 bodyForward = boss.transform.forward;
            if (Vector3.Angle(bodyForward, toTarget) <= headMaxAngle)
            {
                return desired;
            }

            Vector3 clampedDir = Vector3.RotateTowards(
                bodyForward,
                toTarget,
                headMaxAngle * Mathf.Deg2Rad,
                0f
            );
            return headBone.position + clampedDir.normalized * toTarget.magnitude;
        }

        /// <summary>
        /// 顔を向ける先。捕捉できている間はプレイヤーの胸、見失っている間は最後に把握した位置の胸の高さ。
        /// 見えていないのに今の位置を狙うと、物陰の移動を目で追う透視になってしまうため区別する。
        /// </summary>
        private Vector3 GetDesiredAimPoint()
        {
            if (boss.Player == null)
            {
                return GetForwardPoint();
            }

            Vector3 chestOffset = GetPlayerChestOffset();

            if (boss.CheckPlayerTracked())
            {
                return boss.Player.transform.position + chestOffset;
            }

            if (boss.HasKnownPlayerPosition)
            {
                return boss.LastKnownPlayerPosition + chestOffset;
            }

            return GetForwardPoint();
        }

        /// <summary>プレイヤーの足元から胸までのずれ。体格（スケール）が変わっても胸を狙えるようコライダーから求める。</summary>
        private Vector3 GetPlayerChestOffset()
        {
            Collider playerCollider = boss.PlayerCollider;
            if (playerCollider == null)
            {
                return Vector3.up * boss.Player.transform.lossyScale.y;
            }
            return playerCollider.bounds.center - boss.Player.transform.position;
        }

        private Vector3 GetForwardPoint()
        {
            // 正面に置く距離は意味を持たない（方向だけが効く）ため、体格に比例した適当な距離にしている
            return headBone.position + boss.transform.forward * boss.BodyScale * 2f;
        }
    }
}
