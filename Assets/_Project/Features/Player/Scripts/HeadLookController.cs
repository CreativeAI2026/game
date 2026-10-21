using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 近くの敵がいれば視線を向け、いなければ正面を見るようにする。
    /// 可動域を制限することで、首が不自然に回りすぎるのを防ぐ。
    ///
    /// Animation Rigging の Multi-Aim は、ターゲットがキャラクター自身の Animator 配下にあると
    /// 首の位置を実際とずれた位置で計算して下を向いてしまった（TutorialBoss でも同じ症状を確認）ため使わない。
    /// Animator がポーズを書き込んだ後（LateUpdate）に、実際の骨の位置から首・頭の回転を求めて上書きする。
    /// Animator は毎フレームポーズを書き直すため、上書きが積み重なることはない。
    ///
    /// 頭・首のボーンはモデル層(<see cref="PlayerModel"/>)から取るので、モデルを差し替えても設定し直さなくてよい。
    ///
    /// 弓を構えている間は追従を弱めて止め、構えモーションの頭の向き(照準の方向)に任せる。
    /// 敵を追い続けると、狙っている方向と顔の向きが食い違うため。
    /// </summary>
    [RequireComponent(typeof(PlayerTargetManager))]
    [RequireComponent(typeof(PlayerController))]
    public class HeadLookController : MonoBehaviour
    {
        // 可動域外の敵を見るとき、方向だけを表す仮想ポイントを置く距離。方向のみ参照するため値自体は重要ではない
        private const float VirtualTargetDistance = 5f;

        [Header("可動域設定")]
        [Tooltip("正面から首を向ける最大角度（左右・上下）")]
        public float maxAngle = 60f;

        [Tooltip("視線移動の滑らかさ")]
        public float smoothSpeed = 5f;

        [Tooltip("向きを変える量のうち首が負担する割合（0〜1）。残りを頭が負担して視線を合わせきる")]
        [Range(0f, 1f)]
        public float neckShare = 0.4f;

        [Tooltip("弓を構えた・解いた時に、追従の強さを切り替える速さ（1秒あたりの変化量）")]
        public float aimBlendSpeed = 8f;

        private Transform _headBone;
        private Transform _neckBone;
        private PlayerTargetManager _targetManager;
        private PlayerController _playerController;

        // 追従の強さ(0〜1)。構え中は 0 へ下げる
        private float _weight = 1f;

        // 頭ボーンのローカル空間での「顔の正面」の向き。ボーンの軸の取り方はモデルごとに違うため、開始時の姿勢から求める
        private Vector3 _faceLocalAxis;
        private bool _isFaceAxisInitialized;

        // 視線の向きを体（ルート）基準のローカル方向で補間する。
        // ワールド座標の点で補間すると、体が素早く向きを変えた時に視線だけがその場に取り残され、
        // 首が体に遅れてついてくる（首が座っていない）見た目になるため
        private Vector3 _smoothedLocalLookDir = Vector3.forward;

        // ボーンの取得は Animator の初期化後(Start)に行う
        private void Start()
        {
            PlayerModel model = PlayerModel.Of(this);
            _headBone = model.GetBone(HumanBodyBones.Head);
            _neckBone = model.GetBone(HumanBodyBones.Neck);
            _targetManager = GetComponent<PlayerTargetManager>();
            _playerController = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            // 顔の軸は Start ではなく、Animator がポーズを書き込んだ後の最初の LateUpdate で求める。
            // Start 時点の骨の姿勢は、WeaponManager.Start の Animator.Rebind() との実行順(エディタとビルド・
            // シーン読み込みで変わる)に左右され、誤った軸を求めると首が一方向を向き続けるため
            if (!_isFaceAxisInitialized)
            {
                _faceLocalAxis = _headBone.InverseTransformDirection(transform.forward).normalized;
                _isFaceAxisInitialized = true;
            }

            // 補間することで、ターゲットが切り替わった瞬間に首がカクつくのを防ぐ。
            // 体基準の向きで補間するので、体の回転には遅れずに追従し、遅れるのは見る対象が変わった時だけになる
            Vector3 desiredDir = GetDesiredLookPoint() - _headBone.position;
            if (desiredDir.sqrMagnitude > 0.0001f)
            {
                _smoothedLocalLookDir = Vector3.Slerp(
                    _smoothedLocalLookDir,
                    transform.InverseTransformDirection(desiredDir.normalized),
                    Time.deltaTime * smoothSpeed
                );
            }

            Vector3 lookDir = transform.TransformDirection(_smoothedLocalLookDir);

            float targetWeight = _playerController.IsAiming ? 0f : 1f;
            _weight = Mathf.MoveTowards(_weight, targetWeight, aimBlendSpeed * Time.deltaTime);

            // 首→頭の順に回す。首はその一部だけを負担し、頭は首を回した後の向きから計算し直して合わせきる
            RotateBoneTowardLook(_neckBone, lookDir, neckShare * _weight);
            RotateBoneTowardLook(_headBone, lookDir, _weight);
        }

        private Vector3 GetDesiredLookPoint()
        {
            // 敵の足元ではなく顔のあたりを見る(高さは PlayerTargetManager.CurrentTargetPoint が決める)
            Vector3? enemyPoint = _targetManager.CurrentTargetPoint;
            if (!enemyPoint.HasValue)
            {
                return GetForwardPoint();
            }

            Vector3 dirToEnemy = enemyPoint.Value - _headBone.position;
            if (Vector3.Angle(transform.forward, dirToEnemy) <= maxAngle)
            {
                return enemyPoint.Value;
            }

            // 可動域を超えた敵に対しては、限界角度の境界にターゲットを留める。
            // こうしないと首が180度回転する等の不自然な見た目になる
            Vector3 clampedDir = Vector3.RotateTowards(
                transform.forward,
                dirToEnemy,
                maxAngle * Mathf.Deg2Rad,
                0f
            );
            return _headBone.position + clampedDir * VirtualTargetDistance;
        }

        /// <summary>顔の正面が lookDir を向くための回転のうち、weight 分だけを bone に加える。</summary>
        private void RotateBoneTowardLook(Transform bone, Vector3 lookDir, float weight)
        {
            if (weight <= 0f)
            {
                return;
            }

            Vector3 faceDir = _headBone.TransformDirection(_faceLocalAxis);
            Quaternion fullRotation = Quaternion.FromToRotation(faceDir, lookDir);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, fullRotation, weight) * bone.rotation;
        }

        private Vector3 GetForwardPoint()
        {
            return _headBone.position + transform.forward * VirtualTargetDistance;
        }
    }
}
