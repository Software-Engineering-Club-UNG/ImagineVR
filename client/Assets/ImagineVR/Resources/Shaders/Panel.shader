// The terminal window: a rounded rectangle with a border, a title bar and faint scanlines.
// The quad's UVs run 0..1; _Size is its real size in metres so corners stay round.
Shader "ImagineVR/Panel"
{
    Properties
    {
        _Color ("Fill", Color) = (0.035,0.04,0.055,0.985)
        _BorderColor ("Border", Color) = (0.3,0.32,0.36,1)
        _TitleColor ("Title bar", Color) = (0.075,0.08,0.1,0.99)
        _Size ("Size (m)", Vector) = (1.62,1.06,0,0)
        _Radius ("Corner radius (m)", Float) = 0.03
        _Border ("Border width (m)", Float) = 0.003
        _TitleHeight ("Title bar height (m)", Float) = 0.055
        _Glow ("Border glow", Range(0,1)) = 0
        _GlowColor ("Glow colour", Color) = (1,0.55,0.3,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _BorderColor;
                half4 _TitleColor;
                half4 _GlowColor;
                float4 _Size;
                float _Radius;
                float _Border;
                float _TitleHeight;
                float _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * _Size.xy;
                float2 b = _Size.xy * 0.5 - _Radius;
                float d = length(max(abs(p) - b, 0.0)) - _Radius;   // negative inside
                float aa = max(fwidth(d), 0.00001);

                half inside = saturate(0.5 - d / aa);
                half border = saturate(1.0 - abs(d + _Border * 0.5) / (_Border * 0.5 + aa));

                half title = step(_Size.y * 0.5 - _TitleHeight, p.y);
                half4 col = lerp(_Color, _TitleColor, title);
                col.rgb *= 0.97 + 0.03 * sin(p.y * 1400.0);

                half3 edgeCol = lerp(_BorderColor.rgb, _GlowColor.rgb, _Glow);
                col.rgb = lerp(col.rgb, edgeCol, border);
                col.a = lerp(col.a, 1.0, border) * inside;

                // soft inner glow when the terminal is busy
                half inner = saturate(1.0 + d / 0.05);
                col.rgb += _GlowColor.rgb * (inner * inner * _Glow * 0.18);
                // depth is written so glows and particles behind the window stay behind it
                clip(col.a - 0.02);
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
