// Every solid object in the world: library meshes, kits, props.
// Vertex colours carry the albedo. UV0 = (always-on emission, night-only emission).
// UV1.x = wind sway weight. Lighting and fog are done per vertex.
Shader "ImagineVR/Lit"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Appear ("Appear", Range(0,1)) = 1
        _Motion ("Motion (orbit, bob height, bob speed, spin)", Vector) = (0,0,0,0)
        _Sway ("Sway", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ImagineCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float4 _Motion;
                float _Appear;
                float _Sway;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;   // rgb: lit colour, a: fog amount
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4x4 m = GetObjectToWorldMatrix();
                float3 origin = float3(m._m03, m._m13, m._m23);
                float h = ImHash(origin.xz);

                // Pop in: near things first, a wave spreading outward.
                float stagger = saturate(length(origin.xz) / 100.0) * 0.75 + h * 0.25;
                float a = saturate(_Appear * 3.0 - stagger * 2.0);
                float grow = ImBackOut(a) * step(0.0001, a);

                float3 pos = v.positionOS.xyz * grow;
                float3 nrm = v.normalOS;

                float spin = _Motion.w * (_Time.y + h * 10.0);
                ImRotateY(pos, spin);
                ImRotateY(nrm, spin);

                float3 ws = mul(m, float4(pos, 1.0)).xyz;
                float3 nws = normalize(mul((float3x3)m, nrm));

                // Wind: the top of a tree moves, its base does not.
                float sway = v.uv2.x * _Sway * _ImWind.z;
                float phase = _Time.y * 1.6 + origin.x * 0.35 + origin.z * 0.27;
                ws.xz += _ImWind.xy * ((sin(phase) * 0.6 + sin(phase * 2.3 + 1.7) * 0.25) * sway);

                ws.y += sin(_Time.y * _Motion.z + h * 6.2831) * _Motion.y;

                float orbit = _Motion.x * _Time.y * (0.6 + 0.8 * h);
                ImRotateY(ws, orbit);
                ImRotateY(nws, orbit);

                o.positionCS = TransformWorldToHClip(ws);

                half3 albedo = v.color.rgb * _Tint.rgb * (0.93 + 0.14 * ImHash(origin.zx + 3.1));
                // uv.x: always-on glow. uv.y: glow at night only; a value of 2 means "window light",
                // which glows warm regardless of the glass colour.
                half night = saturate(v.uv.y) * _ImNight;
                half emit = saturate(v.uv.x + night);
                half3 lit = ImLight(albedo, nws);
                // window light is warm; brighter glass makes a brighter window, so rooms differ
                half3 warm = half3(1.25, 0.95, 0.5) * (0.35 + v.color.g * 1.1);
                half3 glowCol = lerp(albedo * 1.5, warm, step(1.5, v.uv.y) * night);
                half3 col = lerp(lit, glowCol, emit);
                half fog = ImFog(ws, _WorldSpaceCameraPos) * (1.0 - 0.6 * emit);
                o.color = half4(col, fog);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(lerp(i.color.rgb, _ImFogColor.rgb, i.color.a), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
