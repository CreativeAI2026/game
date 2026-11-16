#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core;
using CreativeAI.UI;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// イベント定義から、そのイベントが発火する直前の <see cref="DevState"/> を組み立てる。
    /// 発火条件(進行度・フラグ・大事なもの)を満たす値を入れ、それ以前のイベントで渡される
    /// 武器・アイテムも持たせられる。選択肢のフラグは前のイベントの選び方で変わるため推測せず、
    /// 対象イベントの条件に書かれたものだけを入れる。
    /// </summary>
    public static class DevStateBuilder
    {
        public const float DefaultHp = 1000f;

        /// <summary>イベントの発火に必要な進行度。progress 条件が無ければ -1。</summary>
        public static int StartProgress(EventDefinition ev)
        {
            if (ev == null)
                return -1;
            foreach (var condition in ev.Conditions)
                if (
                    condition != null
                    && condition.Type == ConditionType.Progress
                    && !condition.Negated
                )
                    return condition.ProgressValue;
            return -1;
        }

        /// <summary>発火に必要な進行度の昇順(同じなら id 順)に並べる。</summary>
        public static List<EventDefinition> SortByProgress(IEnumerable<EventDefinition> events) =>
            events
                .Where(ev => ev != null)
                .OrderBy(StartProgress)
                .ThenBy(ev => ev.Id, StringComparer.Ordinal)
                .ToList();

        /// <param name="target">この発火直前の状態にする</param>
        /// <param name="all">全イベント(それ以前のイベントの判定に使う)</param>
        /// <param name="includeEarlierRewards">それ以前のイベントで渡される武器・アイテムも持たせる</param>
        /// <param name="basis">HP・選択武器を引き継ぐ元(無ければ既定値)</param>
        public static DevState ForEvent(
            EventDefinition target,
            IEnumerable<EventDefinition> all,
            bool includeEarlierRewards,
            DevState basis = null
        )
        {
            int targetProgress = StartProgress(target);
            var state = new DevState
            {
                progress = Math.Max(0, targetProgress),
                hp = basis != null && basis.HasHp ? basis.hp : DefaultHp,
            };

            // 開始位置は人ごとの設定でイベントの条件とは関係ないので、そのまま引き継ぐ。
            if (basis != null)
            {
                state.useSpawn = basis.useSpawn;
                state.spawnPosition = basis.spawnPosition;
                state.spawnYaw = basis.spawnYaw;
            }

            var flags = new List<DevState.FlagEntry>();
            var items = new List<string>();
            var weapons = new List<string>();

            foreach (var condition in target.Conditions)
            {
                // 「〜でないとき」の条件は、既定の状態(未設定・未所持)でそのまま満たすので入れない。
                if (condition == null || condition.Negated)
                    continue;
                if (condition.Type == ConditionType.Flag)
                    flags.Add(
                        new DevState.FlagEntry
                        {
                            key = condition.FlagKey,
                            value = condition.FlagValue,
                        }
                    );
                else if (condition.Type == ConditionType.HasItem)
                    items.Add(condition.ItemKey);
            }

            if (includeEarlierRewards)
            {
                foreach (var earlier in SortByProgress(all))
                {
                    if (
                        ReferenceEquals(earlier, target)
                        || StartProgress(earlier) >= targetProgress
                    )
                        continue;
                    foreach (var step in earlier.Steps)
                    {
                        if (step == null)
                            continue;
                        if (step.Kind == StepKind.GiveItem)
                            items.Add(step.ItemKey);
                        else if (step.Kind == StepKind.GiveWeapon)
                            weapons.Add(step.WeaponKey);
                    }
                }
            }

            state.flags = flags.ToArray();
            state.importantItems = Distinct(items);
            state.weapons = Distinct(weapons);
            state.selectedWeapon =
                basis != null && Array.IndexOf(state.weapons, basis.selectedWeapon) >= 0
                    ? basis.selectedWeapon
                    : string.Empty;
            return state;
        }

        private static string[] Distinct(IEnumerable<string> keys) =>
            keys.Where(key => !string.IsNullOrEmpty(key))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }
}
#endif
