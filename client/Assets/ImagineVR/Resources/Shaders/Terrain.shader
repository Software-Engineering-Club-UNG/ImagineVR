// The ground. Vertex colours carry the albedo, lighting is per vertex.
// A terrain change is a ring that spreads outward from the user: inside the ring the new
// ground has risen into place, outside it the old ground is still there.
Shader "ImagineVR/Terrain"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Reveal ("Reveal radius (m)", Float) = 1000
        _RevealMode ("0 = show inside, 1 = show outside", Float) = 0
        _EdgeColor ("Reveal edge colour (a = strength)", Color) = (1,0.6,0.3,1)
        _GridColor ("Grid colour", Color) = (0,1,1,1)
        _GridAmount ("Grid amount", Range(0,1)) = 0
        _GridSize ("Grid cell (m)", Float) = 4
        _Detail ("Ground detail (0 = a clean sheet of colour)", Range(0,1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-10" "RenderPipeline"="UniversalPipeline" }

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
                half4 _EdgeColor;
                half4 _GridColor;
                float _Reveal;
                float _RevealMode;
                float _GridAmount;
                float _GridSize;
                float _Detail;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;        // rgb: lit colour, a: fog amount
                float3 ground : TEXCOORD0;  // xy: world xz, z: distance from the user
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float dist = length(ws.xz);

                // New ground rises from below just behind the spreading ring.
                float rise = smoothstep(0.0, 1.0, (_Reveal - dist) / 14.0);
                float risen = lerp(min(ws.y, 0.0) - 5.0, ws.y, rise);
                ws.y = lerp(risen, ws.y, step(0.5, _RevealMode));

                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ws);
                o.color = half4(ImInteriorLight(v.color.rgb * _Tint.rgb, nws, ws), max(ImFog(ws, _WorldSpaceCameraPos), ImEdge(ws)));
                o.ground = float3(ws.xz, dist);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float edge = _Reveal - i.ground.z;   // positive inside the ring
                clip(lerp(edge, -edge, step(0.5, _RevealMode)));

                half3 col = i.color.rgb;

                // The mesh has a vertex every two metres, so on its own the ground is one smooth sheet
                // of colour: it looks unfinished exactly where the user stands. Break it up at the
                // scale of a footstep, a stride and a field, each fading out before it could shimmer.
                float2 g0 = i.ground.xy;
                float away = length(g0 - _WorldSpaceCameraPos.xz);
                float nearBy = saturate(1.0 - away / 26.0), mid = saturate(1.0 - away / 90.0);
                float fine = ImNoise(g0 * 5.3) - 0.5;
                float tread = ImNoise(g0 * 1.15 + 31.7) - 0.5;
                float field = ImNoise(g0 * 0.17 + 7.3) - 0.5;
                // flecks: the odd stone, tuft or scuff, a little darker or lighter than what is around it
                float fleck = smoothstep(0.80, 0.93, ImNoise(g0 * 9.0 + 91.0)) - smoothstep(0.80, 0.93, ImNoise(g0 * 7.0 + 13.0));
                float detail = (fine * 0.55 * nearBy + tread * 0.75 * mid + field * 0.9 + fleck * 0.5 * nearBy) * _Detail;
                col *= 1.0 + detail;
                // the hollows run a touch cooler and the swells warmer, as ground does
                col = lerp(col, col * half3(1.05, 1.0, 0.9), saturate(field * 2.0 + 0.5) * _Detail * 1.2);

                // Optional glowing grid lines.
                float2 cell = i.ground.xy / _GridSize;
                float2 g = abs(frac(cell - 0.5) - 0.5) / max(fwidth(cell), 0.0001);
                half gridLine = (1.0 - saturate(min(g.x, g.y))) * _GridAmount * saturate(1.0 - i.ground.z / 110.0);
                col += _GridColor.rgb * gridLine;

                col = lerp(col, _ImFogColor.rgb, i.color.a * (1.0 - 0.5 * gridLine));

                half glow = saturate(1.0 - abs(edge) / 3.0);
                col += _EdgeColor.rgb * (glow * glow * _EdgeColor.a);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
