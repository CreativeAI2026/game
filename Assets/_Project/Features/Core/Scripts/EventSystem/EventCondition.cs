using System;
using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    public enum ConditionType
    {
        Progress,
        Flag,
        HasItem,
    }

    /// <summary>
    /// イベント発火条件の1つ。progress(進行度が値にちょうど一致) / flag(指定キーが指定値) /
    /// hasItem(指定 itemKey の「大事なもの」を所持) のいずれか。
    /// ファクトリ(Progress / Flag / HasItem)はテストと Importer が構築に使う。
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

        [SerializeField]
        private string _itemKey; // type == HasItem: 所持を問う「大事なもの」の itemKey

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

        public static EventCondition HasItem(string itemKey) =>
            new() { _type = ConditionType.HasItem, _itemKey = itemKey };

        public ConditionType Type => _type;
        public int ProgressValue => _progressValue;
        public string FlagKey => _flagKey;
        public string FlagValue => _flagValue;
        public string ItemKey => _itemKey;

        /// <summary>
        /// この条件を満たすか。進行度は == 比較なので AdvanceTo 後は一致せず、各イベントは1回だけ発火する。
        /// hasItem は <paramref name="hasItem"/> に委譲し、null なら不成立扱い。
        /// </summary>
        public bool IsMet(
            int progress,
            Func<string, string> getFlag,
            Func<string, bool> hasItem = null
        ) =>
            _type switch
            {
                ConditionType.Progress => progress == _progressValue,
                ConditionType.Flag => string.Equals(
                    getFlag?.Invoke(_flagKey),
                    _flagValue,
                    StringComparison.Ordinal
                ),
                ConditionType.HasItem => hasItem?.Invoke(_itemKey) ?? false,
                _ => false,
            };
    }
}
