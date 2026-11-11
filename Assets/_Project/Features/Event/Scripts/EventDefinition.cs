using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// 1イベントの定義(条件 + 会話ステップ + 終了時進行度)。
    /// </summary>
    public sealed class EventDefinition : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private EventCondition[] _conditions = Array.Empty<EventCondition>();

        [SerializeField]
        private EventStep[] _steps = Array.Empty<EventStep>();

        [SerializeField]
        private int _nextProgress;

        // 既定値(Progress / false)が従来のイベントの挙動と同じになるよう、足したフィールドは 0 / false を従来側にしている。
        [SerializeField]
        private EventRepeat _repeat;

        [SerializeField]
        private bool _keepsProgress;

        public string Id => _id;
        public IReadOnlyList<EventCondition> Conditions => _conditions;
        public IReadOnlyList<EventStep> Steps => _steps;

        /// <summary>何回発火するか。</summary>
        public EventRepeat Repeat => _repeat;

        /// <summary>終了時に進行度を進めるか。進めないイベントは <see cref="NextProgress"/> を使わない。</summary>
        public bool AdvancesProgress => !_keepsProgress;

        /// <summary>終了時に進める進行度(<see cref="AdvancesProgress"/> のとき Importer が検証済み)。</summary>
        public int NextProgress => _nextProgress;

        /// <summary>
        /// 全条件を満たす(AND)と発火可。条件が空なら常に満たす。
        /// <see cref="EventRepeat.Once"/> のイベントは、見終えた印(<see cref="ProgressFlags.Seen"/>)があると満たさない。
        /// <paramref name="hasItem"/> は hasItem 条件用(itemKey→大事なもの所持か)。未指定なら
        /// hasItem 条件は不成立扱い(progress/flag のみのイベントは従来どおり)。
        /// </summary>
        public bool ConditionsMet(
            int progress,
            Func<string, string> getFlag,
            Func<string, bool> hasItem = null
        )
        {
            if (
                _repeat == EventRepeat.Once
                && !string.IsNullOrEmpty(getFlag?.Invoke(ProgressFlags.Seen(_id)))
            )
                return false;
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

        /// <summary>
        /// ステップ・進行度まで含めた構築用(テスト・Importer)。
        /// <paramref name="advancesProgress"/> が false なら終了時に進行度を進めず、<paramref name="nextProgress"/> は使わない。
        /// </summary>
        public static EventDefinition Create(
            string id,
            EventCondition[] conditions,
            EventStep[] steps,
            int nextProgress,
            EventRepeat repeat = EventRepeat.Progress,
            bool advancesProgress = true
        )
        {
            var def = CreateInstance<EventDefinition>();
            def._id = id;
            def._conditions = conditions ?? Array.Empty<EventCondition>();
            def._steps = steps ?? Array.Empty<EventStep>();
            def._nextProgress = nextProgress;
            def._repeat = repeat;
            def._keepsProgress = !advancesProgress;
            return def;
        }
    }

    /// <summary>イベントが何回発火するか。</summary>
    public enum EventRepeat
    {
        /// <summary>進行度で1回きり(従来どおり。終了で進行度が進み、条件を外れる)。</summary>
        Progress,

        /// <summary>1回だけ。見終えた印をセーブされるフラグに残し、二度と発火しない。進行度は任意。</summary>
        Once,

        /// <summary>条件を満たす限り何度でも。進行度は進めない。</summary>
        Always,
    }

    public enum StepKind
    {
        Line,
        Choice,
        GiveItem,
        GiveWeapon,
        Battle,
        Command,
        Branch,
        Jump,
        SetFlag,
    }

    [Serializable]
    public sealed class EventBranchCase
    {
        [SerializeField]
        private string _value;

        [SerializeField]
        private int _target;
        public string Value => _value;
        public int Target => _target;

        public EventBranchCase(string value, int target)
        {
            _value = value;
            _target = target;
        }
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
    /// ファクトリはテスト・Importer が構築に使う。
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
        private string _flagKey; // choice / setFlag: 書き込むフラグ

        [SerializeField]
        private string _flagValue; // setFlag: 書き込む値(空文字ならフラグを空に戻す)

        [SerializeField]
        private ChoiceOption[] _options = Array.Empty<ChoiceOption>(); // choice

        [SerializeField]
        private string _itemKey; // giveItem

        [SerializeField]
        private string _weaponKey; // giveWeapon(剣/弓/鎌/ハンマー = sword/bow/scythe/hammer)

        [SerializeField]
        private string _message; // giveItem / giveWeapon: 入手演出に出す文。省略時は UI 側が既定文を作る

        [SerializeField]
        private string _command; // command: 演出コマンド名(window.hide 等)

        [SerializeField]
        private string _arg; // command: コマンド引数(wait の秒数など。不要なら空)

        [SerializeField]
        private EventBranchCase[] _cases = Array.Empty<EventBranchCase>();

        [SerializeField]
        private int _thenTarget;

        [SerializeField]
        private int _elseTarget;

        [SerializeField]
        private int _joinTarget;
        public IReadOnlyList<EventBranchCase> Cases => _cases;
        public int JoinTarget => _joinTarget;

        public static EventStep BranchFlag(string flag, EventBranchCase[] cases, int join) =>
            new()
            {
                _kind = StepKind.Branch,
                _flagKey = flag,
                _cases = cases,
                _joinTarget = join,
            };

        public static EventStep BranchItem(string key, int thenTarget, int elseTarget, int join) =>
            new()
            {
                _kind = StepKind.Branch,
                _itemKey = key,
                _thenTarget = thenTarget,
                _elseTarget = elseTarget,
                _joinTarget = join,
            };

        public static EventStep Jump(int target) =>
            new() { _kind = StepKind.Jump, _joinTarget = target };

        public int ResolveTarget(Func<string, string> getFlag, Func<string, bool> hasItem)
        {
            if (_kind == StepKind.Jump)
                return _joinTarget;
            if (!string.IsNullOrEmpty(_itemKey))
                return hasItem?.Invoke(_itemKey) == true ? _thenTarget : _elseTarget;
            string value = getFlag?.Invoke(_flagKey);
            foreach (var option in _cases ?? Array.Empty<EventBranchCase>())
                if (string.Equals(option.Value, value, StringComparison.Ordinal))
                    return option.Target;
            return _joinTarget;
        }

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

        /// <summary>
        /// フラグを書くステップ。value が空文字ならフラグを空に戻す(「合成した」を書き直す、など)。
        /// </summary>
        public static EventStep SetFlag(string flagKey, string value) =>
            new()
            {
                _kind = StepKind.SetFlag,
                _flagKey = flagKey,
                _flagValue = value,
            };

        public static EventStep GiveItem(string itemKey, string message = null) =>
            new()
            {
                _kind = StepKind.GiveItem,
                _itemKey = itemKey,
                _message = message,
            };

        /// <summary>
        /// 武器を渡すステップ。weaponKey は剣/弓/鎌/ハンマー(sword/bow/scythe/hammer)のいずれか。
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

        /// <summary>setFlag ステップが書く値。</summary>
        public string FlagValue => _flagValue;
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

        [SerializeField]
        private bool _negate; // true なら「満たさないとき」成立(not)

        public EventCondition() { } // Unity シリアライズ用

        public static EventCondition Progress(int value, bool not = false) =>
            new()
            {
                _type = ConditionType.Progress,
                _progressValue = value,
                _negate = not,
            };

        public static EventCondition Flag(string key, string value, bool not = false) =>
            new()
            {
                _type = ConditionType.Flag,
                _flagKey = key,
                _flagValue = value,
                _negate = not,
            };

        public static EventCondition HasItem(string itemKey, bool not = false) =>
            new()
            {
                _type = ConditionType.HasItem,
                _itemKey = itemKey,
                _negate = not,
            };

        public ConditionType Type => _type;
        public int ProgressValue => _progressValue;
        public string FlagKey => _flagKey;
        public string FlagValue => _flagValue;
        public string ItemKey => _itemKey;

        /// <summary>「満たさないとき」成立する条件か(events.json の <c>"not": true</c>)。</summary>
        public bool Negated => _negate;

        /// <summary>
        /// この条件を満たすか。進行度は == 比較なので AdvanceTo 後は一致せず、各イベントは1回だけ発火する。
        /// hasItem は <paramref name="hasItem"/> に委譲し、null なら不成立扱い。
        /// <see cref="Negated"/> なら、その判定結果を反転する(hasItem 未供給の「不成立」も反転して成立になる)。
        /// </summary>
        public bool IsMet(
            int progress,
            Func<string, string> getFlag,
            Func<string, bool> hasItem = null
        ) => Matches(progress, getFlag, hasItem) != _negate;

        private bool Matches(
            int progress,
            Func<string, string> getFlag,
            Func<string, bool> hasItem
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
