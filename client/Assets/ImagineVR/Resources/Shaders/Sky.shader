// Procedural sky: gradient, sun, moon, stars, clouds, aurora, nebula.
// Every parameter is animatable; with everything at zero it is pure black (the dark room).
Shader "ImagineVR/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0,0,0,1)
        _Horizon ("Horizon", Color) = (0,0,0,1)
        _Bottom ("Bottom", Color) = (0,0,0,1)
        _SunColor ("Sun colour", Color) = (0,0,0,1)
        _SunDir ("Sun direction", Vector) = (0,1,0,0)
        _SunSize ("Sun size", Range(0,1)) = 0.2
        _MoonDir ("Moon direction", Vector) = (0,1,0,0)
        _Moon ("Moon", Range(0,1)) = 0
        _Stars ("Stars", Range(0,1)) = 0
        _Clouds ("Clouds", Range(0,1)) = 0
        _CloudColor ("Cloud colour", Color) = (1,1,1,1)
        _Aurora ("Aurora", Range(0,1)) = 0
        _Nebula ("Nebula", Range(0,1)) = 0
        _Haze ("Horizon haze", Range(0,1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ImagineCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top;
                half4 _Horizon;
                half4 _Bottom;
                half4 _SunColor;
                half4 _CloudColor;
                float4 _SunDir;
                float4 _MoonDir;
                float _SunSize;
                float _Moon;
                float _Stars;
                float _Clouds;
                float _Aurora;
                float _Nebula;
                float _Haze;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;

                half3 col = y >= 0.0
                    ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(y), 0.5))
                    : lerp(_Horizon.rgb, _Bottom.rgb, pow(saturate(-y * 1.5), 0.6));

                float above = saturate(y * 6.0 + 0.15);

                // Nebula: soft coloured clouds of gas.
                if (_Nebula > 0.001)
                {
                    float2 nuv = float2(atan2(d.x, d.z) * 1.2, y * 2.4);
                    float n1 = ImFbm(nuv * 1.6 + 3.7);
                    float n2 = ImFbm(nuv * 2.7 + 11.3);
                    half3 gas = lerp(half3(0.45, 0.12, 0.65), half3(0.08, 0.5, 0.7), n2);
                    col += gas * (smoothstep(0.45, 0.85, n1) * _Nebula * 0.6);
                }

                // Stars: one possible star per cell of a 3D grid on the sky sphere.
                if (_Stars > 0.001)
                {
                    float3 p = d * 190.0;
                    float3 cell = floor(p);
                    float h = ImHash3(cell);
                    float3 f = frac(p) - 0.5;
                    float star = step(0.9935, h) * smoothstep(0.38, 0.0, length(f));
                    float twinkle = 0.7 + 0.3 * sin(_Time.y * 2.5 + h * 420.0);
                    half3 starCol = lerp(half3(0.8, 0.88, 1.0), half3(1.0, 0.9, 0.75), frac(h * 53.0));
                    col += starCol * (star * twinkle * _Stars * 1.6 * above);
                }

                // Aurora: slow curtains in the upper sky.
                if (_Aurora > 0.001)
                {
                    float az = atan2(d.x, d.z);
                    float wave = sin(az * 5.0 + sin(az * 2.0 + _Time.y * 0.15) * 2.5 + _Time.y * 0.1);
                    float curtain = smoothstep(0.25, 1.0, wave * 0.5 + 0.5);
                    float band = smoothstep(0.08, 0.3, y) * smoothstep(0.85, 0.4, y);
                    float rays = 0.6 + 0.4 * sin(az * 60.0 + _Time.y * 0.6);
                    half3 aur = lerp(half3(0.15, 1.0, 0.55), half3(0.55, 0.3, 1.0), saturate(y * 1.8));
                    col += aur * (curtain * band * rays * _Aurora * 0.8);
                }

                // Moon.
                if (_Moon > 0.001)
                {
                    float md = dot(d, _MoonDir.xyz);
                    float disc = smoothstep(0.9990, 0.9993, md);
                    float halo = pow(saturate(md), 180.0) * 0.35 + pow(saturate(md), 24.0) * 0.08;
                    col += half3(0.92, 0.94, 1.0) * ((disc + halo) * _Moon);
                }

                // Sun: disc plus two glows. _SunColor.a scales the glow.
                float sd = dot(d, _SunDir.xyz);
                float sunEdge = lerp(0.99985, 0.985, _SunSize);
                float sunDisc = smoothstep(sunEdge, sunEdge + 0.0006, sd);
                float sunGlow = pow(saturate(sd), 10.0) * 0.22 + pow(saturate(sd), 90.0) * 0.5;
                col += _SunColor.rgb * (sunDisc * 1.6 + sunGlow * _SunColor.a);

                // Clouds on a flat layer overhead, drifting slowly.
                if (_Clouds > 0.001)
                {
                    float2 cuv = d.xz / (abs(y) + 0.22) * 1.1 + _Time.y * float2(0.012, 0.006);
                    float n = ImFbm(cuv);
                    float cover = smoothstep(1.0 - _Clouds * 0.75, 1.15 - _Clouds * 0.75, n + 0.25);
                    half3 cloudLit = _CloudColor.rgb + _SunColor.rgb * (pow(saturate(sd), 4.0) * 0.25);
                    col = lerp(col, cloudLit, cover * saturate(y * 7.0) * 0.92);
                }

                // The horizon always meets the fog colour, so the far ground blends into the sky.
                float haze = pow(1.0 - saturate(abs(y)), lerp(48.0, 3.0, _Haze));
                col = lerp(col, _ImFogColor.rgb, haze);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
