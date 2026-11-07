using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスのHP割合から今の段階を判定して保持する。段階ごとの値は使う側が必要な時に読む。
    /// </summary>
    [RequireComponent(typeof(EnemyStatus))]
    public class KuragePhaseTracker : MonoBehaviour
    {
        [Tooltip("段階ごとの設定（HPが高い順）")]
        [SerializeField]
        private PhaseSettings[] _phases = new PhaseSettings[]
        {
            // 第1段階
            new PhaseSettings(
                hpRatio: 1.0f,
                orbitDurationRange: new Vector2(2f, 4f),
                speedUpProbability: 0f,
                turnProbability: 0f,
                doubleAttackProbability: 0f
            ),
            // 第2段階
            new PhaseSettings(
                hpRatio: 0.66f,
                orbitDurationRange: new Vector2(1.5f, 3f),
                speedUpProbability: 0.35f,
                turnProbability: 0.25f,
                doubleAttackProbability: 0.25f
            ),
            // 第3段階
            new PhaseSettings(
                hpRatio: 0.33f,
                orbitDurationRange: new Vector2(1f, 2.2f),
                speedUpProbability: 0.5f,
                turnProbability: 0.5f,
                doubleAttackProbability: 0.5f
            ),
        };

        [Serializable]
        public struct PhaseSettings
        {
            [Tooltip("この段階に入るHP割合")]
            [SerializeField, Range(0f, 1f)]
            private float _hpRatio;

            [Tooltip("周回時間の範囲(x:最小時間、y:最大時間)")]
            [SerializeField]
            private Vector2 _orbitDurationRange;

            [Tooltip("1回の周回中にスピードアップする確率（0〜1）")]
            [SerializeField, Range(0f, 1f)]
            private float _speedUpProbability;

            [Tooltip("1回の周回中に切り返す確率（0〜1）")]
            [SerializeField, Range(0f, 1f)]
            private float _turnProbability;

            [Tooltip("2連続攻撃の確率（0〜1）")]
            [SerializeField, Range(0f, 1f)]
            private float _doubleAttackProbability;

            public float HpRatio => _hpRatio;
            public Vector2 OrbitDurationRange => _orbitDurationRange;
            public float SpeedUpProbability => _speedUpProbability;
            public float TurnProbability => _turnProbability;
            public float DoubleAttackProbability => _doubleAttackProbability;

            public PhaseSettings(
                float hpRatio,
                Vector2 orbitDurationRange,
                float speedUpProbability,
                float turnProbability,
                float doubleAttackProbability
            )
            {
                _hpRatio = hpRatio;
                _orbitDurationRange = orbitDurationRange;
                _speedUpProbability = speedUpProbability;
                _turnProbability = turnProbability;
                _doubleAttackProbability = doubleAttackProbability;
            }
        }

        private EnemyStatus _status;
        private int _currentPhaseIndex;

        public int CurrentPhaseIndex => _currentPhaseIndex;

        void Awake()
        {
            Assert.IsTrue(
                _phases != null && _phases.Length > 0,
                $"{name}: 段階の設定が1つもありません"
            );
            _status = GetComponent<EnemyStatus>();
        }

        void OnEnable()
        {
            _status.HpChanged += OnHpChanged;
        }

        void OnDisable()
        {
            _status.HpChanged -= OnHpChanged;
        }

        private void OnHpChanged(float currentHp, float maxHp)
        {
            float hpRatio = currentHp / maxHp;
            // 大ダメージで複数段階をまたぐ場合に備え、条件を満たす最も後ろの段階まで進める
            for (int i = _currentPhaseIndex + 1; i < _phases.Length; i++)
            {
                if (hpRatio <= _phases[i].HpRatio)
                    _currentPhaseIndex = i;
            }
        }

        /// <summary>
        /// 現在の段階の設定。使う側は必要な時にこれを読む。
        /// </summary>
        public PhaseSettings CurrentPhase => _phases[_currentPhaseIndex];
    }
}
