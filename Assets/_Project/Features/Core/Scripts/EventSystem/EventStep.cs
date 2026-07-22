using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    public enum StepKind
    {
        Line,
        Choice,
        GiveItem,
        GiveWeapon,
        Battle,
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
    /// スキーマは documents/CharactersAndEvents.md の events.json steps に対応。
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
        private string _weaponKey; // giveWeapon(剣/弓/鎌 = sword/bow/scythe。ScenarioReference.md の武器カタログ)

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

        public static EventStep GiveItem(string itemKey) =>
            new() { _kind = StepKind.GiveItem, _itemKey = itemKey };

        /// <summary>
        /// 武器を渡すステップ。weaponKey は剣/弓/鎌(sword/bow/scythe)のいずれか。
        /// 実体はプレイヤーリグの WeaponManager(IWeaponGiver seam)で入手処理する
        /// (ScenarioReference.md「武器カタログ」, EventImplementation.md)。
        /// </summary>
        public static EventStep GiveWeapon(string weaponKey) =>
            new() { _kind = StepKind.GiveWeapon, _weaponKey = weaponKey };

        /// <summary>
        /// 戦闘ステップ。敵は JSON に書かず、シーンの EventTrigger の Enemy スロットに Prefab を配線する
        /// (documents/CharactersAndEvents.md「battle は { "kind": "battle" } のみ」, EventImplementation.md)。
        /// </summary>
        public static EventStep Battle() => new() { _kind = StepKind.Battle };

        public StepKind Kind => _kind;
        public string Speaker => _speaker;
        public string Portrait => _portrait;
        public string Text => _text;
        public string FlagKey => _flagKey;
        public IReadOnlyList<ChoiceOption> Options => _options;
        public string ItemKey => _itemKey;
        public string WeaponKey => _weaponKey;
    }
}
