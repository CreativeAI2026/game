using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    public enum DialoguePortraitSide
    {
        Left,
        Right,
    }

    /// <summary>
    /// 会話キャラクターの表示名・立ち位置・表情スプライトを一元管理する定義。
    /// シナリオ側は portrait キーだけを保持し、表示上の情報はこのアセットから解決する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "DialogueCharacter",
        menuName = "CreativeAI/Conversation/Character Definition"
    )]
    public sealed class DialogueCharacterDefinition : ScriptableObject
    {
        [Serializable]
        public struct Expression
        {
            public string PortraitKey;
            public Sprite Sprite;
            public Sprite Icon;
        }

        [SerializeField]
        private string _id;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private DialoguePortraitSide _side;

        [SerializeField]
        private Expression[] _expressions = Array.Empty<Expression>();

        public string Id => _id;
        public string DisplayName => _displayName;
        public DialoguePortraitSide Side => _side;
        public IReadOnlyList<Expression> Expressions => _expressions;

        public bool TryResolvePortrait(string portraitKey, out Sprite sprite)
        {
            return TryResolveVisual(portraitKey, out sprite, out _);
        }

        public bool TryResolveVisual(string portraitKey, out Sprite sprite, out Sprite icon)
        {
            sprite = null;
            icon = null;
            if (string.IsNullOrEmpty(portraitKey) || _expressions == null)
                return false;

            foreach (var expression in _expressions)
            {
                if (expression.PortraitKey != portraitKey || expression.Sprite == null)
                    continue;

                sprite = expression.Sprite;
                icon = expression.Icon;
                return true;
            }

            return false;
        }
    }
}
