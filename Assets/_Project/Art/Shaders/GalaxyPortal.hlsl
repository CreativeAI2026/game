#ifndef GALAXY_PORTAL_INCLUDED
#define GALAXY_PORTAL_INCLUDED

// 銀河のように渦を巻くポータル。円盤の UV(中心 0.5, 外周が半径 0.5)から描く。
// 腕は対数螺旋で、回すと中心へ吸い込まれて見える。星雲のむらと星も同じ渦の座標で取るので、一緒に巻き込まれる。
// ノイズとハッシュは Blender の移植(BlenderNoiseGradient.hlsl)を使い回す。
#include "Assets/_Project/Art/Shaders/BlenderNoiseGradient.hlsl"

// 星雲のむらに使う fBM のオクターブ数。4 より増やしても間近で見た差がほとんどなく、負荷だけ増えるため。
#define GALAXY_NEBULA_OCTAVES 4

float GalaxyNebula(float3 p)
{
    float sum = 0.0;
    float amp = 1.0;
    float maxamp = 0.0;
    [unroll]
    for (int i = 0; i < GALAXY_NEBULA_OCTAVES; i++)
    {
        sum += BlenderSignedNoise(p) * amp;
        maxamp += amp;
        amp *= 0.5;
        p *= 2.0;
    }
    return 0.5 * sum / maxamp + 0.5;
}

// 格子ごとに乱数で星を1つ置き、またたかせる。
float GalaxyStars(float2 p, float density, float time)
{
    float2 grid = p * density;
    float2 cell = floor(grid);
    uint hash = BlenderHashInt3((int)cell.x, (int)cell.y, 7);
    float presence = (hash & 0xFFFFu) / 65535.0;
    // 3 割ほどのマスにだけ星を置き、詰まりすぎないようにする。
    if (presence < 0.7)
        return 0.0;

    float2 offset = float2(((hash >> 16) & 0xFFu) / 255.0, ((hash >> 24) & 0xFFu) / 255.0);
    float2 local = grid - cell - (0.2 + 0.6 * offset);
    float size = lerp(0.04, 0.12, (presence - 0.7) / 0.3);
    float twinkle = 0.6 + 0.4 * sin(time * (2.0 + 4.0 * offset.x) + offset.y * 6.2832);
    return smoothstep(size, 0.0, length(local)) * twinkle;
}

/// ポータル1点の色と不透明度。色は HDR(1 を超えると Bloom で光る)。
/// Twist: 腕の巻きの強さ。RotationSpeed: 渦の回転(ラジアン/秒)。CoreSize: 中心の光の広がり(半径比)。
void GalaxyPortal_float(float2 UV, float Time,
    float ArmCount, float Twist, float RotationSpeed, float NebulaScale, float StarDensity,
    float CoreSize, float RimWidth, float EdgeSoftness,
    float4 SpaceColor, float4 NebulaColor, float4 ArmColor, float4 CoreColor, float4 StarColor, float4 RimColor,
    out float3 Color, out float Alpha)
{
    float2 d = UV * 2.0 - 1.0;
    float r = length(d);
    float rs = max(r, 1e-3);
    float theta = atan2(d.y, d.x);
    float spin = Time * RotationSpeed;

    // 対数螺旋の位相。腕はこの値が一定の線に沿って伸びる。
    float spiral = theta - Twist * log(rs) - spin;

    float arms = pow(0.5 + 0.5 * cos(ArmCount * spiral), 4.0);
    // 中心と外周では腕を消し、中心の光と外周の輪に任せる。
    arms *= smoothstep(0.05, 0.25, r) * smoothstep(1.0, 0.6, r);

    // 渦の座標を円周に戻してから 3D ノイズを取る(角度の継ぎ目が出ないように)。
    float swirlAngle = theta - Twist * 0.6 * log(rs) - spin;
    float2 swirl = float2(cos(swirlAngle), sin(swirlAngle)) * r;
    float nebula = GalaxyNebula(float3(swirl * NebulaScale, Time * 0.05));
    // 腕をちぎれた星の帯に見せるための細かいむら。
    float clumps = GalaxyNebula(float3(swirl * NebulaScale * 3.0, 11.0 + Time * 0.08));
    float dust = smoothstep(0.4, 0.8, nebula);
    arms *= lerp(0.15, 1.6, nebula) * (0.25 + 1.5 * smoothstep(0.3, 0.75, clumps));

    // 腕の内側に沿う暗い筋(銀河の暗黒帯)。腕より少し前の位相に置く。
    float lanes = pow(0.5 + 0.5 * cos(ArmCount * (spiral + 0.45)), 8.0) * smoothstep(0.15, 0.35, r);
    float laneShade = 1.0 - 0.75 * lanes * smoothstep(0.35, 0.65, clumps);

    // 星は巻きを弱めた座標で取る。腕と同じ強さで巻くと引き伸ばされて線に見えるため。
    float starAngle = theta - Twist * 0.15 * log(rs) - spin;
    float2 starCoord = float2(cos(starAngle), sin(starAngle)) * r;
    float stars = GalaxyStars(starCoord, StarDensity, Time) * smoothstep(0.1, 0.35, r);
    // 腕の上には細かい星を密に重ね、腕が星の集まりに見えるようにする。
    stars += GalaxyStars(starCoord + 3.7, StarDensity * 2.5, Time) * saturate(arms * 1.5);
    float core = exp(-r / max(CoreSize, 1e-3));

    // 縁を透明にしていく範囲(EdgeSoftness)の内側に置く。重なると輪が薄まって見えなくなるため。
    float rimCenter = 1.0 - EdgeSoftness - RimWidth;
    float rim = smoothstep(RimWidth, 0.0, abs(r - rimCenter)) * lerp(0.5, 1.2, nebula);

    float3 color = SpaceColor.rgb;
    color += NebulaColor.rgb * dust * (1.0 - r);
    color += ArmColor.rgb * arms;
    color *= laneShade;
    color += StarColor.rgb * stars;
    color += RimColor.rgb * rim;
    color += CoreColor.rgb * core;

    Color = color;
    Alpha = 1.0 - smoothstep(1.0 - EdgeSoftness, 1.0, r);
}

#endif
