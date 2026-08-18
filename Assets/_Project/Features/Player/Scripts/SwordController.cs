using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 近接武器の統括コンポーネント。ステートマシン（SwordStates.cs）と連携し、
    /// 攻撃・ガード・パリィ・ダッシュの状態管理と、
    /// 敵からの攻撃をガード/パリィで受け流す判定を担当する。
    /// </summary>
    public class SwordController : MonoBehaviour
    {
        [Header("武器管理")]
        [Tooltip("WeaponManagerで剣が割り当てられているインデックス (通常は 0)")]
        public int weaponIndex = 0;

        [Header("設定")]
        public float searchRadius = 8f;
        public float attackRange = 3f;
        public float dashSpeed = 20f;
        public LayerMask enemyLayer;
        public float comboResetTime = 1f;

        [SerializeField]
        public Vector3 attackSwordRotation = new Vector3(90f, -100f, 90f);

        [Header("ガード・パリィ設定")]
        [Tooltip("ジャストパリィの受付時間（秒）")]
        public float parryWindowDuration = 0.2f;

        [Header("ガード上限数設定")]
        [Tooltip("ガードの回数がこの数値以上になったら攻撃を受ける。")]
        public int guardMaxCount = 3;

        [Tooltip("ガード時に剣の角度を変えるためのメッシュの親オブジェクト")]
        public Transform weaponMeshRoot;

        [Tooltip("通常時の剣のローカル角度。今は専用アニメーションが無いので、テスト用のみ使用。")]
        public Vector3 normalSwordRotation = Vector3.zero;

        [Tooltip(
            "ガード時の剣のローカル角度。今は専用アニメーションが無いので、テスト用のみ使用。（横に寝かせるなど）"
        )]
        public Vector3 guardSwordRotation = new Vector3(0f, 90f, 45f);

        [Header("エフェクト")]
        [Tooltip("ガード成功時に出すVFXプレハブ")]
        public GameObject guardEffectPrefab;

        [Tooltip("パリィ成功時に出すVFXプレハブ")]
        public GameObject parryEffectPrefab;

        [Header("参照")]
        public CharacterController characterController;

        [HideInInspector]
        public PlayerInputHandler input;

        [HideInInspector]
        public Animator animator;

        [HideInInspector]
        public PlayerController playerController;

        [HideInInspector]
        public Transform playerTransform;
        private WeaponManager _weaponManager;

        private SwordState _currentState;

        [HideInInspector]
        public Transform targetEnemy;

        [HideInInspector]
        public int comboStep = 0;

        [HideInInspector]
        public float lastAttackTime = 0f;

        [HideInInspector]
        public float parryTimer = 0f;

        [HideInInspector]
        public int guardHitCount = 0;

        private void Awake()
        {
            var root = GetComponentInParent<PlayerController>();
            if (root != null)
            {
                playerTransform = root.transform;
                input = root.GetComponent<PlayerInputHandler>();
                animator = root.GetComponent<Animator>();
                characterController = root.GetComponent<CharacterController>();
                playerController = root;
                _weaponManager = root.GetComponent<WeaponManager>();
            }
        }

        private void OnEnable()
        {
            ChangeState(new SwordStateFree(this));
        }

        private void OnDisable()
        {
            // 他の武器に切り替わった際に攻撃トリガーが残ると誤発動するためリセット
            if (animator != null)
            {
                animator.ResetTrigger("Slash1");
                animator.ResetTrigger("Slash2");
                animator.ResetTrigger("Slash3");
                animator.ResetTrigger("DashTrigger");
            }

            _currentState?.Exit();

            if (playerController != null)
            {
                playerController.CanMove = true;
                playerController.CanChangeWeapon = true;
            }
        }

        private bool _prevSubAction = false;

        private void Update()
        {
            if (input == null || _weaponManager == null)
                return;

            // 怯み中、または掴まれ中は武器ステートマシンを完全停止し、入力を一切処理しない
            if (playerController.IsFlinching || playerController.IsGrabbed)
            {
                if (_currentState is not SwordStateFree)
                    ChangeState(new SwordStateFree(this));
                return;
            }

            // 非装備時に裏で入力を消費するのを防ぐ
            if (_weaponManager.CurrentWeaponIndex != weaponIndex)
            {
                if (_currentState is not SwordStateFree)
                    ChangeState(new SwordStateFree(this));
                return;
            }

            // パリィタイマーの更新（ステートに依存せず、ボタンの「押し始め」で受付開始）
            if (input.subAction && !_prevSubAction)
            {
                parryTimer = parryWindowDuration;
            }
            else if (parryTimer > 0f)
            {
                parryTimer -= Time.deltaTime;
            }
            _prevSubAction = input.subAction;

            _currentState?.Update();
        }

        public void ChangeState(SwordState newState)
        {
            _currentState?.Exit();
            _currentState = newState;
            _currentState?.Enter();
        }

        /// <summary>
        /// 敵の攻撃判定（Hitbox等）から呼ばれ、ガード/パリィ判定を行う。
        /// ガードまたはパリィが成立した場合はtrueを返し、呼び出し元はダメージを与えない。
        /// falseの場合は無防備であり、呼び出し元がTakeDamageを行う。
        /// </summary>
        public bool ReceiveAttack(
            float damage,
            bool isMeleeAttack,
            Transform attacker,
            Vector3 hitPoint
        )
        {
            if (_currentState is SwordStateGuard or SwordStateParry)
            {
                // ガード入力がされている、またはパリィ受付時間中のみ防御成立
                if (parryTimer > 0f || input.subAction)
                {
                    if (parryTimer > 0f)
                    {
                        SpawnParryEffect(hitPoint);

                        CameraShakeManager.Instance?.Shake(0.4f);

                        if (isMeleeAttack && attacker != null)
                        {
                            var enemyCon = attacker.GetComponentInParent<TestEnemyController>();
                            if (enemyCon != null)
                            {
                                enemyCon.ChangeState(new TestEnemyParriedState(enemyCon));
                            }
                        }

                        ChangeState(new SwordStateParry(this));

                        // 一回のガード入力で一回のパリィのみ受け付けるため、タイマーを0にする
                        parryTimer = 0f;
                        return true;
                    }

                    SpawnGuardEffect(hitPoint);

                    CameraShakeManager.Instance?.Shake(0.2f);

                    guardHitCount++;

                    // ガード耐久上限を超えたら強制的にガードを崩し、次の攻撃をダメージとして受ける
                    if (guardHitCount >= guardMaxCount)
                    {
                        Debug.Log("ガード上限に達した");
                        // TODO : 専用アニメーションを追加する。
                        ChangeState(new SwordStateFree(this));
                    }

                    return true;
                }
            }

            return false;
        }

        private void SpawnGuardEffect(Vector3 hitPosition)
        {
            if (guardEffectPrefab != null)
            {
                // TODO : ObjectPool導入後、Instantiateをやめる
                Instantiate(guardEffectPrefab, hitPosition, playerTransform.rotation);
            }
        }

        private void SpawnParryEffect(Vector3 hitPosition)
        {
            if (parryEffectPrefab != null)
            {
                // TODO : ObjectPool導入後、Instantiateをやめる
                Instantiate(parryEffectPrefab, hitPosition, playerTransform.rotation);
            }
        }

        public Transform FindNearestEnemy()
        {
            Collider[] hitColliders = Physics.OverlapSphere(
                playerTransform.position,
                searchRadius,
                enemyLayer
            );
            Transform nearest = null;
            float minDistance = float.MaxValue;

            foreach (var hitCollider in hitColliders)
            {
                float distance = Vector3.Distance(
                    playerTransform.position,
                    hitCollider.transform.position
                );
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = hitCollider.transform;
                }
            }
            return nearest;
        }

        /// <summary>
        /// PlayerFlinchHandler から呼ばれ、現在のステートを安全に終了して初期状態にリセットする。
        /// Enter() は呼ばない。IsFlinching中はUpdateが動かないため、
        /// Enter内のCanMove等の設定は怯み終了後にFlinchHandlerが行う。
        /// </summary>
        public void ForceReset()
        {
            _currentState?.Exit();
            comboStep = 0;
            guardHitCount = 0;
            parryTimer = 0f;
            if (weaponMeshRoot != null)
                weaponMeshRoot.localRotation = Quaternion.Euler(normalSwordRotation);
            _currentState = new SwordStateFree(this);
        }
    }
}
