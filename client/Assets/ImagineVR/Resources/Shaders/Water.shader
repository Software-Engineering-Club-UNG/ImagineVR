// A flat water surface: deep colour below, sky colour at grazing angles, moving glints.
Shader "ImagineVR/Water"
{
    Properties
    {
        _Color ("Deep colour", Color) = (0.05,0.3,0.45,1)
        _Reveal ("Reveal radius (m)", Float) = 1000
        _RevealMode ("0 = show inside, 1 = show outside", Float) = 0
        _Opacity ("Opacity", Range(0,1)) = 0.88
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ImagineCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Reveal;
                float _RevealMode;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.positionWS = ws;
                o.viewDir = _WorldSpaceCameraPos - ws;
                o.fog = max(ImFog(ws, _WorldSpaceCameraPos), ImEdge(ws));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float dist = length(i.positionWS.xz);
                float edge = _Reveal - dist;
                clip(lerp(edge, -edge, step(0.5, _RevealMode)));

                float t = _Time.y;
                float2 p = i.positionWS.xz;
                float2 slope = float2(
                    sin(p.x * 0.9 + t * 1.1) * 0.5 + sin(p.y * 0.53 - t * 0.8) * 0.5 + sin((p.x + p.y) * 2.1 + t * 1.7) * 0.25,
                    sin(p.y * 1.1 - t * 0.9) * 0.5 + sin(p.x * 0.47 + t * 0.7) * 0.5 + sin((p.x - p.y) * 1.9 - t * 1.4) * 0.25);
                float3 n = normalize(float3(slope.x * 0.09, 1.0, slope.y * 0.09));

                float3 vdir = normalize(i.viewDir);
                float fresnel = pow(1.0 - saturate(dot(n, vdir)), 4.0);
                half3 col = lerp(_Color.rgb * (_ImAmbSky.rgb + _ImSunColor.rgb * 0.6), _ImFogColor.rgb, fresnel * 0.85);

                float3 halfDir = normalize(_ImSunDir.xyz + vdir);
                float glint = pow(saturate(dot(n, halfDir)), 140.0);
                col += _ImSunColor.rgb * glint * 1.5;

                col = lerp(col, _ImFogColor.rgb, i.fog);
                return half4(col, lerp(_Opacity, 1.0, saturate(fresnel + i.fog)));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
