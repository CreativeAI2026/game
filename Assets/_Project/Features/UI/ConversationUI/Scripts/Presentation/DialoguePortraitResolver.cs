using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>キャラクター定義と旧形式の立ち絵一覧から表示情報を解決する。</summary>
    internal static class DialoguePortraitResolver
    {
        public static DialoguePortraitPresenter.ResolvedPortrait Resolve(
            string key,
            DialogueCharacterDefinition[] characters,
            ConversationView.PortraitEntry[] portraits,
            Sprite defaultPortrait,
            DialoguePortraitSide defaultSide
        )
        {
            if (!string.IsNullOrEmpty(key) && characters != null)
            {
                foreach (var character in characters)
                    if (
                        character != null
                        && character.TryResolveVisual(key, out var portrait, out var icon)
                    )
                        return new DialoguePortraitPresenter.ResolvedPortrait(
                            portrait,
                            icon,
                            character.Side,
                            character.DisplayName,
                            character.ThemeColor,
                            character.TypingSound,
                            character.PortraitOffset
                        );
            }
            if (!string.IsNullOrEmpty(key) && portraits != null)
            {
                foreach (var entry in portraits)
                    if (entry.Key == key && entry.Sprite != null)
                        return new DialoguePortraitPresenter.ResolvedPortrait(
                            entry.Sprite,
                            entry.Sprite,
                            entry.Side,
                            string.Empty,
                            new Color(0.75f, 0.9f, 1f, 1f),
                            null,
                            Vector2.zero
                        );
            }
            return new DialoguePortraitPresenter.ResolvedPortrait(
                defaultPortrait,
                defaultPortrait,
                defaultSide,
                string.Empty,
                new Color(0.75f, 0.9f, 1f, 1f),
                null,
                Vector2.zero
            );
        }
    }
}
