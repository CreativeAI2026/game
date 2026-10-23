// TutorialBoss の目の光（追い込み開始の合図）を描く加算合成のシェーダー。TBossEyeGlint が板ポリゴンに使う。
// 画像は使わず、板の UV から形を計算する:
//   _Shape = 0 … 光の芯（中心が最も明るい丸い光）
//   _Shape = 1 … 光の筋（中心から左右へ細く伸び、端へ向かって細く消える光。鋭い芯の線＋淡いにじみ）
// シーンにブルームが無くても光って見えるよう、強度を 1 より大きくして中心を白っぽく飽和させる前提で作っている。
Shader "CreativeAI/TBossEyeGlint"
{
    Properties
    {
        [HDR] _Color ("色", Color) = (1, 0.35, 0.1, 1)
        _Intensity ("強度", Float) = 1
        _Shape ("形（0=芯 1=筋）", Range(0, 1)) = 0
        _CoreSharpness ("芯の絞り（大きいほど小さく鋭い）", Float) = 6
        _StreakSharpness ("筋の芯の線の絞り（大きいほど細く鋭い）", Float) = 100
        _StreakGlowSharpness ("筋のにじみの絞り（大きいほどにじみが狭い）", Float) = 4
        _StreakGlowAmount ("筋のにじみの強さ（芯の線に対する割合）", Float) = 0.3
        _StreakTaper ("筋の先細り（大きいほど先端が早く消える）", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "EyeGlint"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            // 頭の後ろ側にある時は頭に隠れるよう、深度の判定はふつうに行う
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Shape;
                half _CoreSharpness;
                half _StreakSharpness;
                half _StreakGlowSharpness;
                half _StreakGlowAmount;
                half _StreakTaper;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 板の中心を原点、端を ±1 とする
                float2 p = input.uv * 2.0 - 1.0;

                half core = exp(-dot(p, p) * _CoreSharpness);

                // 筋は「細く鋭い芯の線」と「その周りの淡いにじみ」の2重にする。
                // にじみだけだと太くぼやけた帯になり、ギラッとした鋭さが出ないため
                half along = pow(saturate(1.0 - abs(p.x)), _StreakTaper);
                float y2 = p.y * p.y;
                half across = exp(-y2 * _StreakSharpness) + _StreakGlowAmount * exp(-y2 * _StreakGlowSharpness);
                half streak = along * across;

                half shape = lerp(core, streak, _Shape);
                return half4(_Color.rgb * (_Intensity * shape), 0.0);
            }
            ENDHLSL
        }
    }
}
