using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 敵のHP割合から今の段階（0始まり）を判定して保持する。
    /// 段階ごとの値（周回時間・防御の確率など）は、使う側が段階番号で引ける形で自分で持つ。
    /// </summary>
    [RequireComponent(typeof(EnemyStatus))]
    public sealed class EnemyHpPhaseTracker : MonoBehaviour
    {
        [Tooltip("各段階に入るHP割合（HPが高い順）。0番目は開始時の段階なので 1 にする")]
        [SerializeField, Range(0f, 1f)]
        private float[] _phaseHpRatios = { 1f, 0.66f, 0.33f };

        private EnemyStatus _status;

        /// <summary>今の段階の番号（0始まり）。HPが減るほど大きくなり、戻らない。</summary>
        public int CurrentPhaseIndex { get; private set; }

        /// <summary>段階の数。使う側は段階ごとの値をこの数だけ持つ。</summary>
        public int PhaseCount => _phaseHpRatios.Length;

        private void Awake()
        {
            Assert.IsTrue(
                _phaseHpRatios != null && _phaseHpRatios.Length > 0,
                $"{name}: 段階のHP割合が1つもありません"
            );
            _status = GetComponent<EnemyStatus>();
        }

        private void OnEnable()
        {
            _status.HpChanged += OnHpChanged;
        }

        private void OnDisable()
        {
            _status.HpChanged -= OnHpChanged;
        }

        private void OnHpChanged(float currentHp, float maxHp)
        {
            CurrentPhaseIndex = ResolvePhaseIndex(
                CurrentPhaseIndex,
                currentHp / maxHp,
                _phaseHpRatios
            );
        }

        /// <summary>
        /// 今の段階とHP割合から、次の段階番号を求める。段階は戻らない。
        /// 大ダメージで複数の段階をまたぐ場合に備え、条件を満たす最も後ろの段階まで進める。
        /// </summary>
        public static int ResolvePhaseIndex(int currentIndex, float hpRatio, float[] phaseHpRatios)
        {
            int index = currentIndex;
            for (int i = currentIndex + 1; i < phaseHpRatios.Length; i++)
            {
                if (hpRatio <= phaseHpRatios[i])
                    index = i;
            }
            return index;
        }
    }
}
