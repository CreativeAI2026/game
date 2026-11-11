using UnityEngine;
using UnityEngine.Pool;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// EffectPool から借りたエフェクト1つ分の再生と、再生が終わったらプールへ返す処理を受け持つ。
    /// EffectPool が生成時に付けるので、プレハブには付けない。
    /// </summary>
    public sealed class PooledEffect : MonoBehaviour
    {
        private IObjectPool<PooledEffect> _pool;
        private ParticleSystem[] _particleSystems;

        /// <summary>
        /// 返却先のプールを受け取り、使い回せる状態に整える。生成直後に一度だけ呼ぶ。
        /// </summary>
        public void Initialize(IObjectPool<PooledEffect> pool)
        {
            _pool = pool;
            // 非表示の子もいずれ使われることがあるので含めて集める
            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in _particleSystems)
            {
                // Destroy / Disable のままだと使い回す前にオブジェクトが消えたり子が非表示のまま戻らなくなるので、
                // 片付けはプールに任せる
                ParticleSystem.MainModule main = particle.main;
                main.stopAction = ParticleSystemStopAction.None;
            }
        }

        /// <summary>
        /// 指定の位置と向きで最初から再生する。
        /// 非アクティブから戻すことで Play On Awake が働き、Instantiate したときと同じ再生になる。
        /// </summary>
        public void PlayAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (IsAnyParticleAlive())
                return;
            _pool.Release(this);
        }

        private bool IsAnyParticleAlive()
        {
            foreach (ParticleSystem particle in _particleSystems)
            {
                if (particle.IsAlive(false))
                    return true;
            }
            return false;
        }
    }
}
