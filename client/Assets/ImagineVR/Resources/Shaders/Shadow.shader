// The soft dark patch under anything standing on the ground: a flat quad, darkest in the
// middle. The quad's Y scale is not needed for its shape, so it carries how strong this one is.
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

                float4x4 m = GetObjectToWorldMatrix();
                float own = length(float3(m._m01, m._m11, m._m21));
                float3 ws = mul(m, float4(v.positionOS.x, 0.0, v.positionOS.z, 1.0)).xyz;
                // lean away from the light, a little: long at sunset, underfoot at noon
                float reach = min(length(float3(m._m00, m._m10, m._m20)), length(float3(m._m02, m._m12, m._m22)));
                ws.xz -= _ImSunDir.xz * (reach * 0.3);
                o.positionCS = TransformWorldToHClip(ws);
                float far = (1.0 - ImFog(ws, _WorldSpaceCameraPos)) * (1.0 - ImEdge(ws));
                o.uvFade = float3(v.positionOS.xz, saturate(own) * _Strength * _ImShadow * far);
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
