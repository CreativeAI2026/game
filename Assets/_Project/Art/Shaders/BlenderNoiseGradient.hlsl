#ifndef BLENDER_NOISE_GRADIENT_INCLUDED
#define BLENDER_NOISE_GRADIENT_INCLUDED

// Blender の「ノイズテクスチャ(3D / fBM)」と「カラーランプ(リニア)」を ShaderGraph の Custom Function 用に移植したもの。
// 同じ座標と値を入れれば Blender と同じ模様になるよう、ハッシュ・補間・係数まで Blender の GPU シェーダーに合わせている。
// 参照: Blender source/blender/gpu/shaders/common/gpu_shader_common_hash.glsl, material/gpu_shader_material_noise.glsl

// --- ハッシュ(Bob Jenkins lookup3 の final) ---
#define BLENDER_HASH_ROTATE(x, k) (((x) << (k)) | ((x) >> (32u - (k))))

uint BlenderHashUint3(uint kx, uint ky, uint kz)
{
    uint a, b, c;
    a = b = c = 0xdeadbeefu + (3u << 2u) + 13u;
    c += kz;
    b += ky;
    a += kx;

    c ^= b; c -= BLENDER_HASH_ROTATE(b, 14u);
    a ^= c; a -= BLENDER_HASH_ROTATE(c, 11u);
    b ^= a; b -= BLENDER_HASH_ROTATE(a, 25u);
    c ^= b; c -= BLENDER_HASH_ROTATE(b, 16u);
    a ^= c; a -= BLENDER_HASH_ROTATE(c, 4u);
    b ^= a; b -= BLENDER_HASH_ROTATE(a, 14u);
    c ^= b; c -= BLENDER_HASH_ROTATE(b, 24u);
    return c;
}

uint BlenderHashInt3(int x, int y, int z)
{
    return BlenderHashUint3(asuint(x), asuint(y), asuint(z));
}

// --- Perlin ノイズ ---
float BlenderFade(float t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

float BlenderNegateIf(float value, uint condition)
{
    return condition != 0u ? -value : value;
}

float BlenderNoiseGrad(uint hash, float x, float y, float z)
{
    uint h = hash & 15u;
    float u = h < 8u ? x : y;
    float vt = (h == 12u || h == 14u) ? x : z;
    float v = h < 4u ? y : vt;
    return BlenderNegateIf(u, h & 1u) + BlenderNegateIf(v, h & 2u);
}

float BlenderPerlin(float3 p)
{
    float3 cell = floor(p);
    int X = (int)cell.x;
    int Y = (int)cell.y;
    int Z = (int)cell.z;
    float3 f = p - cell;

    float u = BlenderFade(f.x);
    float v = BlenderFade(f.y);
    float w = BlenderFade(f.z);

    float v0 = BlenderNoiseGrad(BlenderHashInt3(X,     Y,     Z    ), f.x,       f.y,       f.z);
    float v1 = BlenderNoiseGrad(BlenderHashInt3(X + 1, Y,     Z    ), f.x - 1.0, f.y,       f.z);
    float v2 = BlenderNoiseGrad(BlenderHashInt3(X,     Y + 1, Z    ), f.x,       f.y - 1.0, f.z);
    float v3 = BlenderNoiseGrad(BlenderHashInt3(X + 1, Y + 1, Z    ), f.x - 1.0, f.y - 1.0, f.z);
    float v4 = BlenderNoiseGrad(BlenderHashInt3(X,     Y,     Z + 1), f.x,       f.y,       f.z - 1.0);
    float v5 = BlenderNoiseGrad(BlenderHashInt3(X + 1, Y,     Z + 1), f.x - 1.0, f.y,       f.z - 1.0);
    float v6 = BlenderNoiseGrad(BlenderHashInt3(X,     Y + 1, Z + 1), f.x,       f.y - 1.0, f.z - 1.0);
    float v7 = BlenderNoiseGrad(BlenderHashInt3(X + 1, Y + 1, Z + 1), f.x - 1.0, f.y - 1.0, f.z - 1.0);

    float x1 = 1.0 - u;
    float y1 = 1.0 - v;
    float z1 = 1.0 - w;
    return z1 * (y1 * (v0 * x1 + v1 * u) + v * (v2 * x1 + v3 * u))
         + w  * (y1 * (v4 * x1 + v5 * u) + v * (v6 * x1 + v7 * u));
}

// Blender は 3D Perlin の値域を -1〜1 に近づけるため 0.982 を掛けている。
float BlenderSignedNoise(float3 p)
{
    return 0.9820 * BlenderPerlin(p);
}

/// Blender のノイズテクスチャ(3D / fBM / 正規化あり / 歪み 0)の「係数」出力。
/// Coord: 生成座標(0〜1)。Detail は Blender と同じく 0〜15 に制限し、端数は次のオクターブとの補間で表す。
void BlenderNoiseFbm_float(float3 Coord, float Scale, float Detail, float Roughness, float Lacunarity, out float Factor)
{
    float3 p = Coord * Scale;
    float detail = clamp(Detail, 0.0, 15.0);
    float roughness = max(Roughness, 0.0);

    float fscale = 1.0;
    float amp = 1.0;
    float maxamp = 0.0;
    float sum = 0.0;
    int octaves = (int)detail;
    [loop]
    for (int i = 0; i <= octaves; i++)
    {
        sum += BlenderSignedNoise(fscale * p) * amp;
        maxamp += amp;
        amp *= roughness;
        fscale *= Lacunarity;
    }

    float normalized = 0.5 * sum / maxamp + 0.5;
    float remainder = detail - floor(detail);
    if (remainder != 0.0)
    {
        float sum2 = sum + BlenderSignedNoise(fscale * p) * amp;
        float normalized2 = 0.5 * sum2 / (maxamp + amp) + 0.5;
        normalized = lerp(normalized, normalized2, remainder);
    }
    Factor = normalized;
}

/// Blender のカラーランプ(RGB / リニア)。最大 5 ストップで、StopCount 個目までを使う。
/// 最初のストップより前は最初の色、最後のストップより後は最後の色になる(Blender と同じ)。
void BlenderColorRamp5_float(float Factor, float StopCount,
    float4 Color0, float Position0, float4 Color1, float Position1, float4 Color2, float Position2,
    float4 Color3, float Position3, float4 Color4, float Position4, out float4 Color)
{
    float4 colors[5] = { Color0, Color1, Color2, Color3, Color4 };
    float positions[5] = { Position0, Position1, Position2, Position3, Position4 };
    int count = clamp((int)StopCount, 1, 5);

    Color = colors[count - 1];
    if (Factor <= positions[0])
    {
        Color = colors[0];
        return;
    }

    [unroll]
    for (int i = 1; i < 5; i++)
    {
        if (i < count && Factor < positions[i])
        {
            float t = (Factor - positions[i - 1]) / max(positions[i] - positions[i - 1], 1e-5);
            Color = lerp(colors[i - 1], colors[i], t);
            return;
        }
    }
}

#endif
