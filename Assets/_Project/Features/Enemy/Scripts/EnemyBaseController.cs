using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    public class EnemyBaseController : MonoBehaviour
    {
        // 行動に必要なコンポーネント群
        public NavMeshAgent Agent { get; private set; }
        public GameObject Player { get; private set; }
        public Animator Animator { get; private set; }
        public EnemyStatus Status { get; private set; }

        // 現在の状態を保持する変数
        private IEnemyState currentState;

        // 発見済みかどうかのフラグ
        public bool IsAlerted { get; set; }

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Player = GameObject.FindGameObjectWithTag("Player");
            Animator = GetComponent<Animator>();
            Status = GetComponent<EnemyStatus>();
        }

        protected virtual void OnEnable()
        {
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
            // 現在のステートがセットされていれば、そのUpdateを実行し続ける
            if (currentState != null)
            {
                currentState.Update();
            }
        }

        // ステートを切り替える最重要関数
        public void ChangeState(IEnemyState newState)
        {
            Debug.Log(currentState + " → " + newState);
            // 今の状態を終わらせる
            if (currentState != null)
            {
                currentState.Exit();
            }

            // 新しい状態に書き換える
            currentState = newState;

            // 新しい状態を開始する
            if (currentState != null)
            {
                currentState.Enter();
            }
        }

        // 怯み
        public virtual void ForceFlinch()
        {
            // 子クラスでオーバーライドして、固有の怯みステートに遷移させる。
        }

        // 不意打ち発見
        public virtual void ForceAlert()
        {
            // 子クラスでオーバーライドして、固有の発見（追跡）ステートに遷移させる。
        }
    }
}
