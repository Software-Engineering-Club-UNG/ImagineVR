// Soft additive halos (lamps, windows, fireflies, embers). Each halo is a quad whose four
// vertices share one position; UV0 holds the corner (-1..1) and UV1 = (radius, night-only).
// The quad is turned to face the camera here.
Shader "ImagineVR/Glow"
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
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

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
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
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

                float stagger = saturate(length(origin.xz) / 100.0) * 0.75 + h * 0.25;
                float a = saturate(_Appear * 3.0 - stagger * 2.0);

                float3 pos = v.positionOS.xyz * a;
                ImRotateY(pos, _Motion.w * (_Time.y + h * 10.0));
                float3 center = mul(m, float4(pos, 1.0)).xyz;
                center.y += sin(_Time.y * _Motion.z + h * 6.2831) * _Motion.y;
                ImRotateY(center, _Motion.x * _Time.y * (0.6 + 0.8 * h));

                float scale = length(float3(m._m00, m._m10, m._m20));
                float radius = v.uv2.x * scale * a;
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 ws = center + (right * v.uv.x + up * v.uv.y) * radius;

                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                half strength = lerp(1.0, _ImNight, v.uv2.y);
                half fog = ImFog(center, _WorldSpaceCameraPos);
                o.color = half4(v.color.rgb * _Tint.rgb * strength * (1.0 - fog * 0.8), 1.0);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half falloff = saturate(1.0 - dot(i.uv, i.uv));
                falloff *= falloff;
                return half4(i.color.rgb * falloff, 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
