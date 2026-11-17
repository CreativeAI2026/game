namespace CreativeAI.UI
{
    /// <summary>
    /// タイトルの「はじめる / 続きから」で、常駐を作ってフィールドへ入る処理。
    /// 実体は UI より上の Session 層にあり、起動時に <see cref="GameStartService.Current"/> へ登録される。
    /// </summary>
    public interface IGameStarter
    {
        /// <summary>はじめから遊ぶ。始められなければ false。</summary>
        bool StartNewGame(string sceneName);

        /// <summary>セーブから続ける。保存シーンが読めなければ <paramref name="fallbackSceneName"/> から。始められなければ false。</summary>
        bool ContinueGame(string fallbackSceneName);
    }

    /// <summary>実行時の <see cref="IGameStarter"/> を登録する窓口。UI は Session を参照できないためこれを挟む。</summary>
    public static class GameStartService
    {
        /// <summary>登録済みの実体。</summary>
        public static IGameStarter Current { get; set; }
    }
}
