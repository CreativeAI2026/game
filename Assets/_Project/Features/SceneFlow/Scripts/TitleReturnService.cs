namespace CreativeAI.Gameplay
{
    /// <summary>プレイを終えてタイトルへ戻る処理。実体は Session 層が持つ。</summary>
    public interface ITitleReturner
    {
        /// <summary>今のプレイを打ち切ってタイトルへ戻る。</summary>
        void ReturnToTitle();
    }

    /// <summary>実行時の <see cref="ITitleReturner"/> を登録する窓口。Gameplay は Session を参照できないためこれを挟む。</summary>
    public static class TitleReturnService
    {
        /// <summary>登録済みの実体。</summary>
        public static ITitleReturner Current { get; set; }
    }
}
