// クラゲボスの「神々しい光」を描く加算合成のパーティクル用シェーダー。突きのエフェクト(KurageStabEffectBuilder)が使う。
// 画像は使わず、板の UV から形を計算する:
//   Orb    … 光の粒（小さく鋭い芯＋淡いにじみ）
//   Ring   … 細い光の輪（天使のわっかに合わせた形。内側にごく淡いもやを持つ）
//   Streak … 光の筋（U 方向へ細く伸び、両端へ向かって消える。鋭い芯の線＋淡いにじみ）
// 既定の Volume はブルーム無し・トーンマッピング無しなので、HDR で光らせる前提にしない。
// 明るさは「頂点カラー × 頂点アルファ × _Intensity」で決まり、淡く重ねて主張しすぎない光にする。
Shader "CreativeAI/KurageHolyGlow"
{
    Properties
    {
        _Color ("色", Color) = (1, 1, 1, 1)
        _Intensity ("強度", Float) = 1
        [KeywordEnum(Orb, Ring, Streak)] _Shape ("形", Float) = 0

        [Header(Orb)]
        _OrbCoreSharpness ("芯の絞り（大きいほど小さく鋭い）", Float) = 9
        _OrbHaloAmount ("にじみの強さ（芯に対する割合）", Float) = 0.3

        [Header(Ring)]
        _RingRadius ("輪の半径（板の端が 1）", Range(0.1, 0.95)) = 0.8
        _RingThickness ("輪の太さ（板の端が 1）", Float) = 0.035
        [Toggle] _RingThicknessFromParticle ("太さにパーティクルの Custom1.x を掛ける", Float) = 0
        _RingHaze ("輪の内側のもやの強さ", Float) = 0.1

        [Header(Streak)]
        _StreakSharpness ("芯の線の絞り（大きいほど細く鋭い）", Float) = 80
        _StreakGlowSharpness ("にじみの絞り（大きいほどにじみが狭い）", Float) = 5
        _StreakGlowAmount ("にじみの強さ（芯の線に対する割合）", Float) = 0.25
        _StreakTaper ("先細り（大きいほど先端が早く消える）", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "HolyGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _SHAPE_ORB _SHAPE_RING _SHAPE_STREAK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _OrbCoreSharpness;
                half _OrbHaloAmount;
                half _RingRadius;
                half _RingThickness;
                half _RingThicknessFromParticle;
                half _RingHaze;
                half _StreakSharpness;
                half _StreakGlowSharpness;
                half _StreakGlowAmount;
                half _StreakTaper;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                // xy = UV、z = パーティクルの Custom1.x（輪の太さの倍率。頂点ストリームに Custom1.x を足した時だけ入る）
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 板の中心を原点、端を ±1 とする
                float2 p = input.uv.xy * 2.0 - 1.0;
                float r = length(p);
                half shape = 0;

            #if defined(_SHAPE_RING)
                half thickness = _RingThickness * lerp(1.0, input.uv.z, _RingThicknessFromParticle);
                float d = (r - _RingRadius) / max(thickness, 1e-4);
                half ringLine = exp(-d * d);
                // 輪のすぐ内側だけに淡いもやを置く（輪の存在感を少し足す。中心は空ける）
                half haze = _RingHaze * pow(saturate(r / _RingRadius), 6.0) * step(r, _RingRadius);
                // にじみが板の縁で切れて四角く見えないよう、縁へ向けて消す
                shape = (ringLine + haze) * saturate((1.0 - r) * 8.0);
            #elif defined(_SHAPE_STREAK)
                half along = pow(saturate(1.0 - abs(p.x)), _StreakTaper);
                float y2 = p.y * p.y;
                half across = exp(-y2 * _StreakSharpness) + _StreakGlowAmount * exp(-y2 * _StreakGlowSharpness);
                shape = along * across * saturate((1.0 - abs(p.y)) * 4.0);
            #else
                half edge = saturate(1.0 - r);
                shape = exp(-r * r * _OrbCoreSharpness) + _OrbHaloAmount * edge * edge;
            #endif

                half3 rgb = _Color.rgb * input.color.rgb * (_Intensity * input.color.a * shape);
                return half4(rgb, 0.0);
            }
            ENDHLSL
        }
    }
}
