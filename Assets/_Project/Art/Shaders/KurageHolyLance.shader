// クラゲボスの突きの「槍の穂先」を描く加算合成のシェーダー。細い円錐のメッシュ(KurageStabLance)に使う。
// 円錐の UV は u=周方向(0〜1で一周)、v=軸方向(0=開いた根元側、1=尖った先端)。
// 光は2つを重ねる:
//   縁の光   … 視線に対して斜めの面ほど明るい。横から見ると円錐の輪郭が光り、中は透けて「殻」に見える
//   縦の筋   … 根元から先端へ収束する細い線。先端へ向かって流れる明暗を乗せて、突き進む勢いを出す
// 根元側は透明から始めて、いつの間にか槍に包まれているように見せる(円錐の口が輪として見えないように)。
// 全体の濃さ(_Strength)は KurageStabLance が出現・消える時に動かす。
Shader "CreativeAI/KurageHolyLance"
{
    Properties
    {
        _Color ("色", Color) = (1, 0.9, 0.85, 1)
        _Intensity ("強度", Float) = 0.6
        _Strength ("濃さ(スクリプトが動かす 0〜1)", Range(0, 1)) = 1

        [Header(Rim)]
        _RimPower ("縁の絞り（大きいほど輪郭だけが光る）", Float) = 2.5
        _RimAmount ("縁の光の強さ", Float) = 0.7

        [Header(Lines)]
        _LineCount ("縦の筋の本数", Float) = 14
        _LineSharpness ("縦の筋の絞り（大きいほど細い）", Float) = 24
        _LineAmount ("縦の筋の強さ", Float) = 0.8
        _LineTwist ("縦の筋のねじれ（先端までに回る回数）", Float) = 0.35

        [Header(Flow)]
        _FlowBands ("流れる明暗の数", Float) = 3
        _FlowSpeed ("流れる速さ（1秒に進む明暗の数）", Float) = 6
        _FlowAmount ("流れる明暗の強さ(0〜1)", Range(0, 1)) = 0.5

        [Header(Profile)]
        _BaseFade ("根元側が透明から濃くなり切る位置(0〜1)", Range(0.01, 1)) = 0.45
        _TipBoost ("先端の明るさの倍率", Float) = 2
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
            Name "HolyLance"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            // 円錐の手前と奥の両方の面を描く(殻として見せるため)
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Strength;
                half _RimPower;
                half _RimAmount;
                half _LineCount;
                half _LineSharpness;
                half _LineAmount;
                half _LineTwist;
                half _FlowBands;
                half _FlowSpeed;
                half _FlowAmount;
                half _BaseFade;
                half _TipBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;

                half ndotv = abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half rim = pow(1.0 - ndotv, _RimPower) * _RimAmount;

                float lineWave = 0.5 + 0.5 * cos((u * _LineCount + v * _LineTwist) * TWO_PI);
                half lines = pow(lineWave, _LineSharpness) * _LineAmount;

                half flowWave = 0.5 + 0.5 * sin((v * _FlowBands - _Time.y * _FlowSpeed) * TWO_PI);
                half flow = lerp(1.0, flowWave, _FlowAmount);

                half along = smoothstep(0.0, _BaseFade, v) * lerp(1.0, _TipBoost, v * v);

                half brightness = (rim + lines * flow) * along * _Intensity * _Strength;
                return half4(_Color.rgb * brightness, 0.0);
            }
            ENDHLSL
        }
    }
}
