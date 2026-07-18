namespace CreativeAI.Core.SceneManagement
{
    public static class SceneNames
    {
        public const string Title = "01_Title";

        /// <summary>生成器(Setup Initial Scenes)が作るスカフォールド用フィールド。手作りの本番フィールドと衝突させない。</summary>
        public const string FieldArea00 = "Field_Area00";

        // UI 確認/開発用シーン(Scenes/UI 配下・Editor で直接 Play する)。実行時に名前ロードはしない。
        /// <summary>会話UIの確認用シーン(旧 Field_Area05)。</summary>
        public const string UiConversationPreview = "UI_ConversationPreview";

        /// <summary>調合UIの確認用シーン(旧 Field_Area06)。</summary>
        public const string UiCraftingPreview = "UI_CraftingPreview";

        /// <summary>各UIパネルを素組みした総合UI開発シーン(旧 Field_Area01)。</summary>
        public const string UiSandbox = "UI_Sandbox";
    }
}
