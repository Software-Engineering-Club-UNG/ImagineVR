// Comfort: while the user is being moved, the edge of their view darkens, which is where
// motion is felt most. Measured as an angle from the centre of each eye's view, so both eyes
// see the same opening and it sits at no particular distance.
Shader "ImagineVR/Vignette"
{
    Properties
    {
        _Amount ("Amount", Range(0,1)) = 0
        _Inner ("Inner edge (tan of angle)", Float) = 1
        _Feather ("Feather", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Amount;
                float _Inner;
                float _Feather;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 view : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.view = TransformWorldToView(ws);
                o.positionCS = TransformWorldToHClip(ws);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.view.xy) / max(0.0001, -i.view.z);
                half a = smoothstep(_Inner, _Inner + _Feather, r) * _Amount;
                return half4(0.0, 0.0, 0.0, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
