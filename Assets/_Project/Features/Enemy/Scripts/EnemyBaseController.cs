using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 敵キャラクターの行動制御基盤を標準化し、コードの重複を防ぎつつAIの拡張性を確保するための抽象基底クラス。
    /// </summary>
    public class EnemyBaseController : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }
        public GameObject Player { get; private set; }
        public Animator Animator { get; private set; }
        public EnemyStatus Status { get; private set; }

        public Collider EnemyCollider { get; private set; }
        public Collider PlayerCollider { get; private set; }

        private IEnemyState currentState;
        public bool IsAlerted { get; set; }

        protected virtual void Awake()
        {
            // モデルの差し替え等でPrefabの階層構造が変化しても、参照の欠損でスクリプトが破損するのを防ぐためのフォールバック
            Agent = GetComponent<NavMeshAgent>();
            if (Agent == null)
            {
                Agent = GetComponentInChildren<NavMeshAgent>();
            }

            Player = GameObject.FindGameObjectWithTag("Player");

            Animator = GetComponent<Animator>();
            if (Animator == null)
            {
                Animator = GetComponentInChildren<Animator>();
            }

            Status = GetComponent<EnemyStatus>();
            if (Status == null)
            {
                Status = GetComponentInChildren<EnemyStatus>();
            }

            EnemyCollider = GetComponent<Collider>();
            if (Player != null)
            {
                PlayerCollider = Player.GetComponent<Collider>();
            }
        }

        protected virtual void OnEnable()
        {
            // Unityのライフサイクルの不定性（非アクティブ状態での生成など）に起因するNullReferenceExceptionを防止するため
            if (Status == null)
            {
                Status = GetComponent<EnemyStatus>();
                if (Status == null)
                {
                    Status = GetComponentInChildren<EnemyStatus>();
                }
            }

            if (Status != null)
            {
                Status.OnFlinchTriggered += ForceFlinch;
                Status.OnAlertTriggered += ForceAlert;
                Status.OnDeathTriggered += ForceDeath;
            }
        }

        protected virtual void OnDisable()
        {
            if (Status != null)
            {
                Status.OnFlinchTriggered -= ForceFlinch;
                Status.OnAlertTriggered -= ForceAlert;
                Status.OnDeathTriggered -= ForceDeath;
            }
        }

        protected virtual void Start()
        {
            // 初期状態（待機や巡回など）は敵の種別や配置状況に依存するため、具象クラスに委譲する
        }

        protected virtual void Update()
        {
            if (currentState != null)
            {
                currentState.Update();
            }
        }

        /// <summary>
        /// 状態が重複して実行されたり、終了処理が漏れてAIの挙動が破綻するのを防ぐため、遷移処理を一元化する。
        /// </summary>
        public void ChangeState(IEnemyState newState)
        {
            Debug.Log(currentState + " → " + newState);
            if (currentState != null)
            {
                currentState.Exit();
            }

            currentState = newState;

            if (currentState != null)
            {
                currentState.Enter();
            }
        }

        /// <summary>
        /// 敵のサイズや耐性によって怯みの表現（専用モーションの再生や無視など）が異なるため、派生クラスでの具象化を要求する。
        /// </summary>
        public virtual void ForceFlinch() { }

        /// <summary>
        /// ステルス状態からの強制発覚等、感知手段や警戒への移行プロセスは敵ごとに固有となるため拡張ポイントとして開放する。
        /// </summary>
        public virtual void ForceAlert() { }

        /// <summary>
        /// 死亡時の演出（ラグドール化、爆発、分裂など）のバリエーションを吸収するためのフックメソッド。
        /// </summary>
        public virtual void ForceDeath() { }
    }
}
