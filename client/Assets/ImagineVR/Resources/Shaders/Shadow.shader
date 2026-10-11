// The soft dark patch under anything standing on the ground: a flat quad, darkest in the
// middle. Quads arrive already placed in world space, many to a mesh (see Shadows.cs):
// UV0 is the corner (-1..1), UV1 = (how strong this one is, how far it leans from the sun).
Shader "ImagineVR/Shadow"
{
    Properties
    {
        _Strength ("Strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="AlphaTest+60" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ImagineCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
            CBUFFER_END

            float _ImShadow;   // how dark shadows are right now: none in the dark room, deepest at noon

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uvFade : TEXCOORD0;   // xy: -1..1 across the patch, z: strength
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                // lean away from the light, a little: long at sunset, underfoot at noon
                ws.xz -= _ImSunDir.xz * (v.uv2.y * 0.3);
                o.positionCS = TransformWorldToHClip(ws);
                float far = (1.0 - ImFog(ws, _WorldSpaceCameraPos)) * (1.0 - ImEdge(ws));
                o.uvFade = float3(v.uv, saturate(v.uv2.x) * _Strength * _ImShadow * far);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half r = saturate(1.0 - dot(i.uvFade.xy, i.uvFade.xy));
                return half4(0.0, 0.0, 0.0, smoothstep(0.0, 0.7, r) * i.uvFade.z);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
