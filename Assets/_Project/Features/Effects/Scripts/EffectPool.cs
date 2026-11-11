using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// その場で一度だけ再生するエフェクト（ヒット・ガード・着弾など）を、プレハブごとのプールで使い回す。
    /// 呼び出し側は Instantiate の代わりに EffectPool.Play を呼ぶ。
    /// シーンに置く必要はなく、初めて使われたときに今のシーンへ作られ、シーンと一緒に破棄される。
    /// </summary>
    public sealed class EffectPool : MonoBehaviour
    {
        // 1種類のエフェクトが同時に出る数の目安。多めにヒットしても足りるように取っておく
        private const int DefaultCapacityPerPrefab = 5;

        // 返却されたエフェクトを残しておく上限。これを超えて返ってきた分は破棄して、溜まり続けないようにする
        private const int MaxPooledPerPrefab = 20;

        /// <summary>今のシーンにあるプール。まだ一度も使われていなければ null。</summary>
        public static EffectPool Instance { get; private set; }

        private readonly Dictionary<GameObject, ObjectPool<PooledEffect>> _poolsByPrefab = new();

        // 生成した瞬間に Play On Awake で再生が始まらないよう、非アクティブの親の下で Instantiate する
        private Transform _inactiveSpawnRoot;

        /// <summary>
        /// prefab のエフェクトを指定の位置と向きで再生する。再生が終わると自動でプールへ戻る。
        /// </summary>
        public static void Play(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GetOrCreateInstance().PlayFromPool(prefab, position, rotation);
        }

        private static EffectPool GetOrCreateInstance()
        {
            if (Instance == null)
                // AddComponent の中で Awake が呼ばれ、Instance が設定される
                new GameObject(nameof(EffectPool)).AddComponent<EffectPool>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            var inactiveSpawnRoot = new GameObject("InactiveSpawnRoot");
            inactiveSpawnRoot.SetActive(false);
            _inactiveSpawnRoot = inactiveSpawnRoot.transform;
            _inactiveSpawnRoot.SetParent(transform, false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void PlayFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!_poolsByPrefab.TryGetValue(prefab, out ObjectPool<PooledEffect> pool))
            {
                pool = CreatePool(prefab);
                _poolsByPrefab.Add(prefab, pool);
            }
            pool.Get().PlayAt(position, rotation);
        }

        private ObjectPool<PooledEffect> CreatePool(GameObject prefab)
        {
            ObjectPool<PooledEffect> pool = null;
            pool = new ObjectPool<PooledEffect>(
                createFunc: () => CreateEffect(prefab, pool),
                actionOnRelease: effect => effect.gameObject.SetActive(false),
                actionOnDestroy: DestroyEffect,
                collectionCheck: true,
                defaultCapacity: DefaultCapacityPerPrefab,
                maxSize: MaxPooledPerPrefab
            );
            return pool;
        }

        private PooledEffect CreateEffect(GameObject prefab, IObjectPool<PooledEffect> pool)
        {
            GameObject instance = Instantiate(prefab, _inactiveSpawnRoot);
            instance.SetActive(false);
            instance.transform.SetParent(transform, false);

            PooledEffect effect = instance.AddComponent<PooledEffect>();
            effect.Initialize(pool);
            return effect;
        }

        private static void DestroyEffect(PooledEffect effect)
        {
            // 再生を止めたときにエディタが全 ObjectPool を Clear するが、
            // その時点でエフェクトはシーンと一緒に破棄済みなので、残っているものだけ消す
            if (effect != null)
                Destroy(effect.gameObject);
        }
    }
}
