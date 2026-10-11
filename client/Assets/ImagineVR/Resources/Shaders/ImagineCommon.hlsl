#ifndef IMAGINE_COMMON_INCLUDED
#define IMAGINE_COMMON_INCLUDED

// World-wide values, set every frame by SkySystem. All ImagineVR shaders light and fog
// themselves from these, so nothing depends on URP's light loop.
float4 _ImSunDir;     // xyz: direction towards the light
float4 _ImSunColor;   // rgb: colour * intensity
float4 _ImAmbSky;     // rgb: ambient from above
float4 _ImAmbGround;  // rgb: ambient from below
float4 _ImFogColor;   // rgb
float4 _ImFogParams;  // x: density, y: world-edge fade start (m), z: world-edge fade end (m)
float4 _ImWind;       // xy: direction on the ground plane, z: strength
float _ImNight;       // 0 day .. 1 night, drives night-only emission
float _ImReal;        // 1 while the real room shows in place of the sky (headset passthrough), else 0

float ImHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float ImHash3(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float ImNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = ImHash(i);
    float b = ImHash(i + float2(1.0, 0.0));
    float c = ImHash(i + float2(0.0, 1.0));
    float d = ImHash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float ImFbm(float2 p)
{
    float v = 0.0;
    v += 0.5 * ImNoise(p);
    v += 0.25 * ImNoise(p * 2.03 + 17.1);
    v += 0.125 * ImNoise(p * 4.01 + 43.7);
    return v / 0.875;
}

// Overshooting ease used when things pop into the world.
float ImBackOut(float t)
{
    float c1 = 1.70158;
    float c3 = c1 + 1.0;
    float u = t - 1.0;
    return 1.0 + c3 * u * u * u + c1 * u * u;
}

void ImRotateY(inout float3 v, float angle)
{
    float s, c;
    sincos(angle, s, c);
    v.xz = float2(v.x * c + v.z * s, -v.x * s + v.z * c);
}

half3 ImLight(half3 albedo, float3 normalWS)
{
    half ndl = saturate(dot(normalWS, _ImSunDir.xyz));
    half3 ambient = lerp(_ImAmbGround.rgb, _ImAmbSky.rgb, normalWS.y * 0.5 + 0.5);
    return albedo * (ambient + _ImSunColor.rgb * ndl);
}

// One bounded interior: room membership excludes outdoor sun and prevents cross-wall light leaks.
int _ImRoomCount;
float4x4 _ImInteriorInverse;
float4 _ImInteriorCenter;
float4 _ImInteriorHalf;
float4 _ImRoomCenters[15];
float4 _ImRoomHalves[15];
float4 _ImRoomLights[30];
float4 _ImRoomLightColor;
half3 ImInteriorBase(half3 albedo, float3 normalWS, float3 worldPosition)
{
    if (_ImRoomCount == 0) return ImLight(albedo, normalWS);
    float3 p = mul(_ImInteriorInverse, float4(worldPosition, 1)).xyz;
    // Outdoor scenery takes one cheap bounds check, not a loop over every room.
    if (any(abs(p - _ImInteriorCenter.xyz) > _ImInteriorHalf.xyz)) return ImLight(albedo, normalWS);
    float3 n = normalize(mul((float3x3)_ImInteriorInverse, normalWS));
    [loop] for (int i = 0; i < _ImRoomCount; i++)
    {
        if (all(abs(p - _ImRoomCenters[i].xyz) <= _ImRoomHalves[i].xyz))
        {
            half3 illumination = _ImRoomCenters[i].www;
            [unroll] for (int j = 0; j < 2; j++)
            {
                float4 lamp = _ImRoomLights[i * 2 + j];
                float3 delta = lamp.xyz - p;
                float d = length(delta);
                float attenuation = saturate(1 - d / lamp.w);
                float flicker = lerp(1, 0.9 + 0.1 * sin(_Time.y * 8 + i * 7 + j), _ImRoomLightColor.w);
                illumination += _ImRoomLightColor.rgb * attenuation * attenuation * max(0.12, dot(n, delta / max(d, 0.001))) * flicker * 2;
            }
            return albedo * illumination;
        }
    }
    return ImLight(albedo, normalWS);
}

float4 _ImHeldLightPositions[4];
float4 _ImHeldLightDirections[4];
float4 _ImHeldLightColors[4];
half3 ImInteriorLight(half3 albedo, float3 normalWS, float3 worldPosition)
{
    half3 lit = ImInteriorBase(albedo, normalWS, worldPosition);
    [unroll] for (int i = 0; i < 4; i++)
    {
        float4 source = _ImHeldLightPositions[i];
        if (source.w <= 0) continue;
        float3 delta = source.xyz - worldPosition;
        float d = length(delta);
        float attenuation = saturate(1 - d / source.w);
        float3 towards = delta / max(d, 0.001);
        float cone = _ImHeldLightDirections[i].w < 0 ? 1 : smoothstep(_ImHeldLightDirections[i].w, 0.98, dot(-towards, _ImHeldLightDirections[i].xyz));
        lit += albedo * _ImHeldLightColors[i].rgb * _ImHeldLightColors[i].w * attenuation * attenuation * cone * max(0.08, dot(normalWS, towards));
    }
    return lit;
}

// 0 = no fog, 1 = fully the fog colour.
half ImFog(float3 positionWS, float3 cameraWS)
{
    float d = distance(positionWS, cameraWS) * _ImFogParams.x;
    return saturate(1.0 - exp(-d * d));
}

// The ground fades into the horizon at the edge of the world. Objects do not, so that
// things placed far away on purpose (a planet, a distant mountain) stay visible.
half ImEdge(float3 positionWS)
{
    return smoothstep(_ImFogParams.y, _ImFogParams.z, length(positionWS.xz));
}

#endif
