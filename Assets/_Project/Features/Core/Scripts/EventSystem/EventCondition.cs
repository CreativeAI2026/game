using System;
using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    public enum ConditionType
    {
        Progress,
        Flag,
    }

    /// <summary>
    /// イベント発火条件の1つ。progress(進行度が値にちょうど一致) か flag(指定キーが指定値) のいずれか。
    /// スキーマは documents/CharactersAndEvents.md の events.json に対応。
    /// ファクトリ(Progress / Flag)はテストと将来の Importer が構築に使う。
    /// </summary>
    [Serializable]
    public sealed class EventCondition
    {
        [SerializeField]
        private ConditionType _type;

        [SerializeField]
        private int _progressValue; // type == Progress: 進行度がこの値にちょうど一致で成立

        [SerializeField]
        private string _flagKey; // type == Flag: 対象フラグのキー

        [SerializeField]
        private string _flagValue; // type == Flag: 一致すべき値

        public EventCondition() { } // Unity シリアライズ用

        public static EventCondition Progress(int value) =>
            new() { _type = ConditionType.Progress, _progressValue = value };

        public static EventCondition Flag(string key, string value) =>
            new()
            {
                _type = ConditionType.Flag,
                _flagKey = key,
                _flagValue = value,
            };

        public ConditionType Type => _type;
        public int ProgressValue => _progressValue;
        public string FlagKey => _flagKey;
        public string FlagValue => _flagValue;

        /// <summary>
        /// この条件を満たすか。進行度比較は「ちょうど一致(==)」、フラグは完全一致。
        /// == なので終了時に AdvanceTo で進行度が進むと二度と一致せず、各イベントは1回だけ発火する
        /// (documents/CharactersAndEvents.md, Specification.md §4)。
        /// </summary>
        public bool IsMet(int progress, Func<string, string> getFlag) =>
            _type switch
            {
                ConditionType.Progress => progress == _progressValue,
                ConditionType.Flag => string.Equals(
                    getFlag?.Invoke(_flagKey),
                    _flagValue,
                    StringComparison.Ordinal
                ),
                _ => false,
            };
    }
}
