using UnityEngine;
using UnityEngine.Pool;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 矢（ArrowProjectile）のオブジェクトプールを管理するシングルトン。
    /// BowControllerなど射出側から ArrowPool.Instance.Get() を呼んで矢を借り受け、
    /// 使い終わったら ArrowProjectile.ReturnToPool() で返却する。
    /// </summary>
    public class ArrowPool : MonoBehaviour
    {
        public static ArrowPool Instance { get; private set; }

        [Tooltip("矢のPrefab（ArrowProjectileコンポーネントが必要）。")]
        [SerializeField]
        private ArrowProjectile _arrowPrefab;

        [Tooltip("プールの初期生成数。")]
        [SerializeField]
        private int _defaultCapacity = 5;

        [Tooltip("プールの最大サイズ。これを超えると余分な矢はDestroyされる。")]
        [SerializeField]
        private int _maxSize = 20;

        private IObjectPool<ArrowProjectile> _pool;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _pool = new ObjectPool<ArrowProjectile>(
                createFunc: CreateArrow,
                actionOnGet: OnGetArrow,
                actionOnRelease: OnReleaseArrow,
                actionOnDestroy: OnDestroyArrow,
                collectionCheck: true,
                defaultCapacity: _defaultCapacity,
                maxSize: _maxSize
            );
        }

        /// <summary>
        /// プールから矢を借り受ける。
        /// 空きがなければ新しく生成される。
        /// </summary>
        public ArrowProjectile Get()
        {
            return _pool.Get();
        }

        // ────────────────────────────────────────────
        //  プールコールバック
        // ────────────────────────────────────────────

        private ArrowProjectile CreateArrow()
        {
            ArrowProjectile arrow = Instantiate(_arrowPrefab);
            arrow.SetPool(_pool);
            return arrow;
        }

        private void OnGetArrow(ArrowProjectile arrow)
        {
            arrow.gameObject.SetActive(true);
        }

        private void OnReleaseArrow(ArrowProjectile arrow)
        {
            // 親子関係を解除してからプールに戻す
            arrow.transform.SetParent(null);
            arrow.gameObject.SetActive(false);
        }

        private void OnDestroyArrow(ArrowProjectile arrow)
        {
            Destroy(arrow.gameObject);
        }
    }
}
