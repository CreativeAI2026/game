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
    /// イベント発火条件の1つ。progress(進行度 &gt;= 値) か flag(指定キーが指定値) のいずれか。
    /// スキーマは documents/CharactersAndEvents.md の events.json に対応。
    /// ファクトリ(Progress / Flag)はテストと将来の Importer が構築に使う。
    /// </summary>
    [Serializable]
    public sealed class EventCondition
    {
        [SerializeField]
        private ConditionType _type;

        [SerializeField]
        private int _progressValue; // type == Progress: 進行度がこの値以上で成立

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

        /// <summary>この条件を満たすか。進行度比較は「以上(&gt;=)」、フラグは完全一致。</summary>
        public bool IsMet(int progress, Func<string, string> getFlag) =>
            _type switch
            {
                ConditionType.Progress => progress >= _progressValue,
                ConditionType.Flag => string.Equals(
                    getFlag?.Invoke(_flagKey),
                    _flagValue,
                    StringComparison.Ordinal
                ),
                _ => false,
            };
    }
}
