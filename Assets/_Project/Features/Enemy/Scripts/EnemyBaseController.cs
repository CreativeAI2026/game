using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 全敵キャラクターの共通基盤。ステートパターンによる行動制御と、
    /// 共有コンポーネントへの参照を提供する。
    /// 敵ごとの固有ロジックは子クラスで実装する。
    /// </summary>
    public class EnemyBaseController : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }
        public GameObject Player { get; private set; }
        public Animator Animator { get; private set; }
        public EnemyStatus Status { get; private set; }
        private IEnemyState currentState;
        public bool IsAlerted { get; set; }

        protected virtual void Awake()
        {
            // Prefab構成によってはコンポーネントが子オブジェクトに配置されるため、
            // 自身で見つからない場合は子階層からも検索する
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
        }

        protected virtual void OnEnable()
        {
            // Awakeより先にOnEnableが呼ばれるケースがあるため、Statusの取得をここでも行う
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
            }
        }

        protected virtual void OnDisable()
        {
            if (Status != null)
            {
                Status.OnFlinchTriggered -= ForceFlinch;
                Status.OnAlertTriggered -= ForceAlert;
            }
        }

        protected virtual void Start()
        {
            // 子クラス側で最初に遷移するステートを定義する
        }

        protected virtual void Update()
        {
            if (currentState != null)
            {
                currentState.Update();
            }
        }

        /// <summary>
        /// ステート遷移を一元管理し、Exit→Enter の呼び出し順序を保証する。
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
        /// 子クラスでオーバーライドして、固有の怯みステートに遷移させる。
        /// </summary>
        public virtual void ForceFlinch()
        {
        }

        /// <summary>
        /// 子クラスでオーバーライドして、被弾時に未発見状態から追跡ステートへ遷移させる。
        /// </summary>
        public virtual void ForceAlert()
        {
        }
    }
}
