namespace CreativeAI.UI.CharacterUI
{
    /// <summary>
    /// CharacterUI のタブに乗る View の契約。<see cref="CharacterUIController"/> がタブ選択に応じて
    /// これらを呼び分ける(表示に入る View は <see cref="OnEnter"/>、外れる View は <see cref="OnExit"/>)。
    /// 装備品タブ = <see cref="EquipmentViewController"/> / 即時使用食材タブ = <see cref="QuickFoodViewController"/>
    /// が実装する。CharacterUIController は具体型ではなくこの契約で View を収集する。
    /// </summary>
    public interface ICharacterTabView
    {
        /// <summary>初回表示前の初期化(冪等)。</summary>
        void EnsureInitialized();

        /// <summary>このタブが選択されて表示に入るとき。</summary>
        void OnEnter();

        /// <summary>他タブが選択されてこの View が隠れるとき。</summary>
        void OnExit();

        /// <summary>パネルを開き直したときの表示状態リセット。</summary>
        void ResetViewState();
    }
}
