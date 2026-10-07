using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// 1イベントの定義(条件 + 会話ステップ + 終了時進行度)。
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

        /// <summary>
        /// 全条件を満たす(AND)と発火可。条件が空なら常に満たす。
        /// <paramref name="hasItem"/> は hasItem 条件用(itemKey→大事なもの所持か)。未指定なら
        /// hasItem 条件は不成立扱い(progress/flag のみのイベントは従来どおり)。
        /// </summary>
        public bool ConditionsMet(
            int progress,
            Func<string, string> getFlag,
            Func<string, bool> hasItem = null
        )
        {
            if (_conditions == null)
                return true;
            foreach (var condition in _conditions)
            {
                if (condition != null && !condition.IsMet(progress, getFlag, hasItem))
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

    public enum StepKind
    {
        Line,
        Choice,
        GiveItem,
        GiveWeapon,
        Battle,
        Command,
    }

    /// <summary>choice ステップの選択肢1つ(表示文 + 書き込む値)。</summary>
    [Serializable]
    public sealed class ChoiceOption
    {
        [SerializeField]
        private string _text;

        [SerializeField]
        private string _value;

        public ChoiceOption() { }

        public ChoiceOption(string text, string value)
        {
            _text = text;
            _value = value;
        }

        public string Text => _text;
        public string Value => _value;
    }

    /// <summary>
    /// 会話の1ステップ。kind に応じて使うフィールドが変わる(union 的)。
    /// ファクトリはテスト・将来の Importer が構築に使う。
    /// </summary>
    [Serializable]
    public sealed class EventStep
    {
        [SerializeField]
        private StepKind _kind;

        [SerializeField]
        private string _speaker; // line

        [SerializeField]
        private string _portrait; // line

        [SerializeField]
        private string _text; // line

        [SerializeField]
        private string _flagKey; // choice: 書き込むフラグ

        [SerializeField]
        private ChoiceOption[] _options = Array.Empty<ChoiceOption>(); // choice

        [SerializeField]
        private string _itemKey; // giveItem

        [SerializeField]
        private string _weaponKey; // giveWeapon(剣/弓/鎌 = sword/bow/scythe)

        [SerializeField]
        private string _message; // giveItem / giveWeapon: 入手演出に出す文。省略時は UI 側が既定文を作る

        [SerializeField]
        private string _command; // command: 演出コマンド名(window.hide 等)

        [SerializeField]
        private string _arg; // command: コマンド引数(wait の秒数など。不要なら空)

        public EventStep() { }

        public static EventStep Line(string speaker, string portrait, string text) =>
            new()
            {
                _kind = StepKind.Line,
                _speaker = speaker,
                _portrait = portrait,
                _text = text,
            };

        public static EventStep Choice(string flagKey, params ChoiceOption[] options) =>
            new()
            {
                _kind = StepKind.Choice,
                _flagKey = flagKey,
                _options = options ?? Array.Empty<ChoiceOption>(),
            };

        public static EventStep GiveItem(string itemKey, string message = null) =>
            new()
            {
                _kind = StepKind.GiveItem,
                _itemKey = itemKey,
                _message = message,
            };

        /// <summary>
        /// 武器を渡すステップ。weaponKey は剣/弓/鎌(sword/bow/scythe)のいずれか。
        /// 実体はプレイヤーリグの WeaponManager(IWeaponGiver seam)で入手処理する。
        /// </summary>
        public static EventStep GiveWeapon(string weaponKey, string message = null) =>
            new()
            {
                _kind = StepKind.GiveWeapon,
                _weaponKey = weaponKey,
                _message = message,
            };

        /// <summary>
        /// 戦闘ステップ。敵は JSON に書かず、シーンの EventTrigger の Enemy スロットに Prefab を配線する。
        /// </summary>
        public static EventStep Battle() => new() { _kind = StepKind.Battle };

        /// <summary>
        /// 会話UIの演出コマンド1つ(window.hide / portrait.left.shake / wait など)。
        /// 実行は IDialogueView.RunCommand → ConversationView の演出コマンドルータ。
        /// </summary>
        public static EventStep Command(string command, string arg = null) =>
            new()
            {
                _kind = StepKind.Command,
                _command = command,
                _arg = arg,
            };

        public StepKind Kind => _kind;
        public string Speaker => _speaker;
        public string Portrait => _portrait;
        public string Text => _text;
        public string FlagKey => _flagKey;
        public IReadOnlyList<ChoiceOption> Options => _options;
        public string ItemKey => _itemKey;
        public string WeaponKey => _weaponKey;

        /// <summary>giveItem / giveWeapon の入手演出に出す文(省略可)。</summary>
        public string Message => _message;

        /// <summary>command ステップの演出コマンド名。</summary>
        public string CommandName => _command;

        /// <summary>command ステップの引数(不要なら null)。</summary>
        public string Arg => _arg;
    }

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
