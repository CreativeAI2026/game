// ミニマップの図(MinimapMap)用の UI シェーダー。階ごとの距離フィールドのテクスチャから、縁をなめらかに描く。
// 縁の太さは画面上の 1px(隣のピクセルとの距離)を基準にするので、拡大・縮小・回転してもジャギーが出ない。
// 2 種類の四角形を 1 つのメッシュで描く(uv0.z が種類)。
//   0 = 階の図。uv0.xy = テクスチャの uv、uv1.xy = 図の大きさ(u)。
//   1 = 階段の段。uv0.xy = 階段の左下からの位置(u)、uv0.w = 上る向き(±1)、uv1 = (階段の幅, 高さ, 段の数, 向きが X か)。
// テクスチャの決まり(R/G/A = 符号付き距離、B = 線の芯までの距離)は MinimapField.cs と同じ。
// UI の Mask(ステンシル)と RectMask2D の切り抜きに対応する(UI/Default と同じ作り)。
Shader "CreativeAI/UI/MinimapShape"
{
    Properties
    {
        [PerRendererData] _MainTex ("Distance Field", 2D) = "gray" {}
        _IdTex ("Line Kind Ids", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Spread ("Distance Spread (u)", Float) = 2
        _MinLinePixels ("Min Line Pixels", Float) = 1
        _EdgeWidth ("Floor Edge Width (u)", Float) = 0.5
        _StepWidth ("Stair Step Width (u)", Float) = 0.25
        _LineHalf ("Line Half Width (u): WallEdge, Fence, Glass, Door", Vector) = (0.3, 0.25, 0.35, 0.7)

        _FloorColor ("Floor", Color) = (0.27, 0.32, 0.39, 1)
        _EdgeColor ("Floor Edge", Color) = (0.5, 0.57, 0.66, 1)
        _WallColor ("Wall", Color) = (0.13, 0.16, 0.2, 1)
        _WallEdgeColor ("Wall Edge", Color) = (0.84, 0.88, 0.93, 1)
        _GlassColor ("Glass", Color) = (0.55, 0.8, 1, 1)
        _FenceColor ("Fence", Color) = (0.6, 0.65, 0.72, 1)
        _DoorColor ("Door", Color) = (0.42, 0.86, 0.62, 1)
        _StairsColor ("Stairs", Color) = (0.32, 0.37, 0.45, 1)
        _StairsStepColor ("Stairs Step", Color) = (0.7, 0.76, 0.84, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _IdTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _Spread;
            float _MinLinePixels;
            float _EdgeWidth;
            float _StepWidth;
            float4 _LineHalf;
            fixed4 _FloorColor, _EdgeColor, _WallColor, _WallEdgeColor, _GlassColor, _FenceColor, _DoorColor;
            fixed4 _StairsColor, _StairsStepColor;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.uv0 = v.uv0;
                OUT.uv1 = v.uv1;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 縁までの距離 d(u。内側が正)から、そのピクセルの塗りの割合。縁の上でちょうど半分になる。
            // pixelUnits は 1px が何 u か。
            float Coverage(float d, float pixelUnits)
            {
                return saturate(d / pixelUnits + 0.5);
            }

            // color の上に、透明度 a の c を重ねる(ストレートアルファのまま)。
            float4 Over(float4 color, float3 c, float a)
            {
                float outA = a + color.a * (1.0 - a);
                float3 rgb = outA > 0.0001 ? (c * a + color.rgb * color.a * (1.0 - a)) / outA : c;
                return float4(rgb, outA);
            }

            float LineHalfWidth(int kind)
            {
                return kind == 1 ? _LineHalf.x : kind == 2 ? _LineHalf.y : kind == 3 ? _LineHalf.z : _LineHalf.w;
            }

            float3 LineColor(int kind)
            {
                return kind == 1 ? _WallEdgeColor.rgb : kind == 2 ? _FenceColor.rgb : kind == 3 ? _GlassColor.rgb : _DoorColor.rgb;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                bool isSteps = IN.uv0.z > 0.5;

                // 1px が何 u か(拡大率・回転に関わらず、隣のピクセルとの距離)。微分は分岐の外で取る
                float2 position = isSteps ? IN.uv0.xy : IN.uv0.xy * IN.uv1.xy;
                float pixelUnits = max(max(length(ddx(position)), length(ddy(position))), 0.0001);
                // 線の太さの下限(半幅 u)
                float minHalf = 0.5 * _MinLinePixels * pixelUnits;

                float4 color = float4(0, 0, 0, 0);
                if (!isSteps)
                {
                    float4 field = tex2Dlod(_MainTex, float4(IN.uv0.xy, 0, 0));
                    float ground = (field.r - 0.5) * 2.0 * _Spread;
                    float wall = (field.g - 0.5) * 2.0 * _Spread;
                    float stairs = (field.a - 0.5) * 2.0 * _Spread;
                    float lineDistance = field.b * _Spread;
                    int kind = (int)(tex2Dlod(_IdTex, float4(IN.uv0.xy, 0, 0)).r * 255.0 + 0.5);

                    // 床の外周の縁取り(床の外側の帯)。床の上から床の色を重ねると、外周にだけ残る
                    float edge = max(_EdgeWidth, minHalf * 2.0);
                    color = Over(color, _EdgeColor.rgb, Coverage(ground + edge, pixelUnits));
                    color = Over(color, _FloorColor.rgb, Coverage(ground, pixelUnits));
                    color = Over(color, _StairsColor.rgb, Coverage(stairs, pixelUnits));
                    color = Over(color, _WallColor.rgb, Coverage(wall, pixelUnits));
                    if (kind > 0)
                    {
                        float halfWidth = max(LineHalfWidth(kind), minHalf);
                        color = Over(color, LineColor(kind), Coverage(halfWidth - lineDistance, pixelUnits));
                    }
                }
                else
                {
                    float2 size = IN.uv1.xy;
                    float steps = IN.uv1.z;
                    bool alongX = IN.uv1.w > 0.5;
                    float span = alongX ? size.x : size.y;
                    float t = alongX ? IN.uv0.x : IN.uv0.y;

                    float spacing = span / (steps + 1.0);
                    float index = clamp(round(t / spacing), 1.0, steps);
                    float halfWidth = max(_StepWidth * 0.5, minHalf);
                    float a = Coverage(halfWidth - abs(t - spacing * index), pixelUnits);
                    // 階段の端でも縁をなめらかにする(四角形は、端より外へ余白を持たせてある)
                    float2 inner = min(IN.uv0.xy, size - IN.uv0.xy);
                    a *= Coverage(min(inner.x, inner.y), pixelUnits);

                    // 上り切る側ほど明るくして、どちらへ上るかが分かるようにする
                    float progress = index / (steps + 1.0);
                    float brightness = IN.uv0.w > 0.0 ? progress : 1.0 - progress;
                    color = float4(lerp(_StairsColor.rgb, _StairsStepColor.rgb, 0.35 + 0.65 * brightness), a);
                }

                // 頂点の色(Graphic の色・CanvasGroup のフェード)を掛ける
                color *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
