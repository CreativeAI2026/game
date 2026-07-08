using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// 1イベントの定義(条件 + 会話ステップ + 終了時進行度)。
    /// startBgm は音響班待ちで未追加(documents/StoryProgressionSystem.md, CharactersAndEvents.md)。
    /// </summary>
    [CreateAssetMenu(menuName = "CreativeAI/Event Definition", fileName = "Event")]
    public sealed class EventDefinition : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private EventCondition[] _conditions = Array.Empty<EventCondition>();

        [SerializeField]
        private EventStep[] _steps = Array.Empty<EventStep>();

        [SerializeField]
        private bool _hasNextProgress; // nextProgress を JSON に書いたか(省略時は進めない)

        [SerializeField]
        private int _nextProgress;

        public string Id => _id;
        public IReadOnlyList<EventCondition> Conditions => _conditions;
        public IReadOnlyList<EventStep> Steps => _steps;

        /// <summary>終了時に進行度を進めるか(nextProgress 省略時は false)。</summary>
        public bool HasNextProgress => _hasNextProgress;
        public int NextProgress => _nextProgress;

        /// <summary>全条件を満たす(AND)と発火可。条件が空なら常に満たす。</summary>
        public bool ConditionsMet(int progress, Func<string, string> getFlag)
        {
            if (_conditions == null)
                return true;
            foreach (var condition in _conditions)
            {
                if (condition != null && !condition.IsMet(progress, getFlag))
                    return false;
            }
            return true;
        }

        /// <summary>条件のみの構築用(テスト・Importer)。</summary>
        public static EventDefinition Create(string id, params EventCondition[] conditions)
        {
            var def = CreateInstance<EventDefinition>();
            def._id = id;
            def._conditions = conditions ?? Array.Empty<EventCondition>();
            return def;
        }

        /// <summary>ステップ・進行度まで含めた構築用(テスト・Importer)。</summary>
        public static EventDefinition Create(
            string id,
            EventCondition[] conditions,
            EventStep[] steps,
            int? nextProgress
        )
        {
            var def = CreateInstance<EventDefinition>();
            def._id = id;
            def._conditions = conditions ?? Array.Empty<EventCondition>();
            def._steps = steps ?? Array.Empty<EventStep>();
            def._hasNextProgress = nextProgress.HasValue;
            def._nextProgress = nextProgress ?? 0;
            return def;
        }
    }
}
