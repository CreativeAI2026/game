namespace CreativeAI.UI.CharacterUI
{
    /// <summary>
    /// CharacterUI のタブに乗る View の契約。<see cref="CharacterUIController"/> がタブ選択に応じて
    /// <see cref="OnEnter"/> / <see cref="OnExit"/> を呼び分ける。実装は両タブともモード設定した <see cref="EquipmentViewController"/>。
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
