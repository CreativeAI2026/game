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

    public enum DialoguePortraitIdleMotion
    {
        None,
        Float,
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
        private Color _themeColor = new(0.75f, 0.9f, 1f, 1f);

        [SerializeField]
        [Tooltip("立ち絵スロット内でのキャラクター固有の表示位置補正")]
        private Vector2 _portraitOffset;

        [SerializeField]
        [Tooltip("会話中に常時再生する、そのキャラクター固有の立ち絵モーション")]
        private DialoguePortraitIdleMotion _idleMotion;

        [SerializeField]
        [Range(0f, 100f)]
        [Tooltip("Float モーションで基準位置から上下する最大距離（px）")]
        private float _idleMotionAmplitude = 14f;

        [SerializeField]
        [Range(0.2f, 10f)]
        [Tooltip("Float モーションが一往復する時間（秒）")]
        private float _idleMotionCycleDuration = 1.8f;

        [SerializeField]
        private AudioClip _typingSound;

        [SerializeField]
        private Expression[] _expressions = Array.Empty<Expression>();

        public string Id => _id;
        public string DisplayName => _displayName;
        public DialoguePortraitSide Side => _side;
        public Color ThemeColor => _themeColor;
        public Vector2 PortraitOffset => _portraitOffset;
        public DialoguePortraitIdleMotion IdleMotion => _idleMotion;
        public float IdleMotionAmplitude => _idleMotionAmplitude;
        public float IdleMotionCycleDuration => _idleMotionCycleDuration;
        public AudioClip TypingSound => _typingSound;
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
