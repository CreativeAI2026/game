using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>コードから色を付ける UI で共通に使う色。</summary>
    public static class UIVisualTheme
    {
        /// <summary>画面の背景。</summary>
        public static readonly Color Background = new Color32(48, 67, 85, 255);

        /// <summary>パネルの地。</summary>
        public static readonly Color Panel = new Color32(27, 43, 56, 255);

        /// <summary>ふつうの文字。</summary>
        public static readonly Color Ink = new Color32(219, 225, 244, 255);

        /// <summary>控えめな文字(押せないボタンの文字など)。</summary>
        public static readonly Color Muted = new Color32(178, 187, 225, 255);

        /// <summary>数値。</summary>
        public static readonly Color Value = new Color32(37, 222, 233, 255);

        /// <summary>強調(金色)。</summary>
        public static readonly Color Gold = new Color32(255, 195, 70, 255);

        /// <summary>金色のボタンの上の文字。</summary>
        public static readonly Color ActionInk = new Color32(48, 34, 13, 255);

        /// <summary>選んでいないタブ。</summary>
        public static readonly Color Tab = new Color32(20, 36, 47, 255);

        /// <summary>選んでいるタブ。</summary>
        public static readonly Color SelectedTab = new Color32(79, 59, 31, 230);
    }
}
