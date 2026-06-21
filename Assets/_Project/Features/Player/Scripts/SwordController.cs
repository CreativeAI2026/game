using UnityEngine;

namespace CreativeAI.Gameplay
{
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

        [Tooltip("ガード時に剣の角度を変えるためのメッシュの親オブジェクト")]
        public Transform weaponMeshRoot;

        [Tooltip("通常時の剣のローカル角度")]
        public Vector3 normalSwordRotation = Vector3.zero;

        [Tooltip("ガード時の剣のローカル角度（横に寝かせるなど）")]
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

        // ステートマシンの中央管理
        private SwordState _currentState;

        // 状態間で共有するデータ
        [HideInInspector]
        public Transform targetEnemy;

        [HideInInspector]
        public int comboStep = 0;

        [HideInInspector]
        public float lastAttackTime = 0f;

        // パリィ用の内部変数
        [HideInInspector]
        public float parryTimer = 0f;

        [HideInInspector]
        public int guardHitCount = 0; // 連続ガード回数

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
            // 他の武器に切り替わった際に剣の攻撃トリガーが残らないように全てリセットする
            if (animator != null)
            {
                animator.ResetTrigger("Slash1");
                animator.ResetTrigger("Slash2");
                animator.ResetTrigger("Slash3");
                animator.ResetTrigger("DashTrigger");
            }

            // 強制終了時のステートクリーンアップ
            _currentState?.Exit();

            // プレイヤーの移動・武器切り替えロックを確実に解除
            if (playerController != null)
            {
                playerController.CanMove = true;
                playerController.CanChangeWeapon = true;
            }
        }

        private void Update()
        {
            if (input == null || _weaponManager == null)
                return;

            // 怯み中は武器ステートマシンを完全停止する
            if (playerController.IsFlinching)
                return;

            // 裏で入力を奪うのを防ぐ
            if (_weaponManager.CurrentWeaponIndex != weaponIndex)
            {
                // 自分が非装備状態なら強制的にFreeに戻して一切何もしない
                if (!(_currentState is SwordStateFree))
                    ChangeState(new SwordStateFree(this));
                return;
            }

            _currentState?.Update();
        }

        // ステートを切り替える関数
        public void ChangeState(SwordState newState)
        {
            _currentState?.Exit();
            _currentState = newState;
            _currentState?.Enter();
        }

        // ==========================================================
        // 敵の攻撃スクリプト（Hitbox等）から呼ばれるダメージ受け取り関数
        // ==========================================================
        public bool ReceiveAttack(
            float damage,
            bool isMeleeAttack,
            Transform attacker,
            Vector3 hitPoint
        )
        {
            // 現在のステートがガード中かどうか
            if (_currentState is SwordStateGuard || _currentState is SwordStateParry)
            {
                // ① ジャストパリィ判定
                if (parryTimer > 0f)
                {
                    Debug.Log("🎯 ジャストパリィ成功！ダメージ完全無効！");
                    // Hitboxが計算した正確なヒット位置にVFXを生成
                    SpawnParryEffect(hitPoint);

                    // パリィ成功時のカメラシェイク
                    CameraShakeManager.Instance?.Shake(0.4f);

                    if (isMeleeAttack && attacker != null)
                    {
                        var enemyCon = attacker.GetComponentInParent<TestEnemyController>();
                        if (enemyCon != null)
                        {
                            // ★ここを FlinchState から ParriedState に変更する
                            enemyCon.ChangeState(new TestEnemyParriedState(enemyCon));
                        }
                    }

                    // プレイヤーをパリィ成功（弾き）ステートへ移行
                    ChangeState(new SwordStateParry(this));

                    // パリィ成功時はタイマーをリセットし、連続パリィを可能にする
                    parryTimer = parryWindowDuration;
                    return true; // 攻撃を防いだので true
                }

                // ② 通常ガード判定
                Debug.Log("🛡️ 通常ガード！");
                // Hitboxが計算した正確なヒット位置にVFXを生成
                SpawnGuardEffect(hitPoint);

                // ガード成功時のカメラシェイク
                CameraShakeManager.Instance?.Shake(0.2f);

                guardHitCount++;

                if (guardHitCount >= 3)
                {
                    Debug.Log("💥 ガードブレイク！");
                    if (animator != null)
                        animator.SetTrigger("GuardBreak");
                    ChangeState(new SwordStateFree(this));
                }

                return true;
            }

            // ③ 無防備（ダメージを受ける。TakeDamage は呼び出し元が行う）
            return false;
        }

        private void SpawnGuardEffect(Vector3 hitPosition)
        {
            if (guardEffectPrefab != null)
            {
                Instantiate(guardEffectPrefab, hitPosition, playerTransform.rotation);
            }
        }

        private void SpawnParryEffect(Vector3 hitPosition)
        {
            if (parryEffectPrefab != null)
            {
                Instantiate(parryEffectPrefab, hitPosition, playerTransform.rotation);
            }
        }

        // 索敵メソッド
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
        /// PlayerFlinchHandler から呼ばれる。現在のステートを安全に終了し、
        /// SwordStateFree に強制リセットする（Enter() は呼ばない）。
        /// CanMove / CanChangeWeapon の管理は FlinchHandler 側で行う。
        /// </summary>
        public void ForceReset()
        {
            _currentState?.Exit();
            // コンボ・ガード状態を手動リセット
            comboStep = 0;
            guardHitCount = 0;
            parryTimer = 0f;
            // 剣の角度を元に戻す
            if (weaponMeshRoot != null)
                weaponMeshRoot.localRotation = Quaternion.Euler(normalSwordRotation);
            // Enter() は呼ばない（IsFlinching 中は Update も動かないため問題なし）
            _currentState = new SwordStateFree(this);
        }
    }
}
