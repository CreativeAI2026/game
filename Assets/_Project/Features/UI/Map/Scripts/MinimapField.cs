using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// ミニマップの距離フィールドテクスチャの決まり事(エディタの焼き込みと描画のシェーダーで揃える値)。
    /// テクスチャは階ごとに 1 枚で、R = 床と階段の地、G = 壁の地、B = 線、A = 階段の地。
    /// R・G・A は図形の縁までの符号付き距離(内側が正)、B はそのテクセルをいちばん深く覆う線の芯までの距離(符号なし)。
    /// どれも 8 ビットに詰め、<see cref="Spread"/> を超える距離は端に丸める。
    /// 距離は「チェビシェフ距離」(縦横の大きい方)で測るので、四角形の角は丸まらない。
    /// </summary>
    public static class MinimapField
    {
        /// <summary>線の種類。ID 画像(線の種類を選ぶ画像)の値と同じ。</summary>
        public enum LineKind : byte
        {
            /// <summary>線が無い。</summary>
            None = 0,

            /// <summary>壁が床に面する縁。</summary>
            WallEdge = 1,

            /// <summary>柵。</summary>
            Fence = 2,

            /// <summary>ガラス・格子。</summary>
            Glass = 3,

            /// <summary>扉。</summary>
            Door = 4,
        }

        /// <summary>距離を保てる範囲(u)。これ以上遠い所は端に丸める。</summary>
        public const float Spread = 2f;

        /// <summary>焼き込みの解像度(1u あたりのテクセル数)。</summary>
        public const float TexelsPerUnit = 2.5f;

        /// <summary>線の太さ(u)。</summary>
        public static float LineWidth(LineKind kind) =>
            kind switch
            {
                LineKind.WallEdge => 0.6f, // 壁が床に面する縁
                LineKind.Fence => 0.5f,
                LineKind.Glass => 0.7f, // ガラス・格子
                LineKind.Door => 1.4f,
                _ => 0f,
            };

        /// <summary>図形のある範囲に、縁取りとなめらかな縁の分の余白を足した、テクスチャが覆う範囲。</summary>
        public static Rect FieldRect(Rect bounds) =>
            new(
                bounds.xMin - Spread,
                bounds.yMin - Spread,
                bounds.width + Spread * 2f,
                bounds.height + Spread * 2f
            );

        /// <summary>符号付き距離(u。内側が正)を 8 ビットにする。0 は 128 前後。</summary>
        public static byte EncodeSigned(float distance) =>
            (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f + distance / (2f * Spread)) * 255f);

        /// <summary>符号なしの距離(u)を 8 ビットにする。</summary>
        public static byte EncodeUnsigned(float distance) =>
            (byte)Mathf.RoundToInt(Mathf.Clamp01(distance / Spread) * 255f);

        /// <summary><see cref="EncodeSigned"/> の逆(シェーダーの読み方と同じ)。</summary>
        public static float DecodeSigned(byte value) => (value / 255f - 0.5f) * 2f * Spread;

        /// <summary><see cref="EncodeUnsigned"/> の逆(シェーダーの読み方と同じ)。</summary>
        public static float DecodeUnsigned(byte value) => value / 255f * Spread;
    }
}
