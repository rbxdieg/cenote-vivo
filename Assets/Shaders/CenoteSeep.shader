// Filtración de lixiviados sobre las paredes de roca.
// La mancha "escurre" de arriba hacia abajo según el valor global _CenotePollution
// (escrito por PollutionSimulator), así que todas las filtraciones comparten un solo material.
Shader "CenoteVivo/CenoteSeep"
{
    Properties
    {
        _SeepColor ("Seep Color", Color) = (0.13, 0.09, 0.03, 0.88)
        _EdgeColor ("Edge Color", Color) = (0.26, 0.22, 0.07, 1)
        _Streaks ("Streaks", Float) = 7
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SeepColor;
                half4 _EdgeColor;
                float _Streaks;
                float _Seed;
            CBUFFER_END

            half _CenotePollution;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; float seed : TEXCOORD2; };

            float Hash(float x) { return frac(sin(x * 127.1) * 43758.5453); }

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = v.uv;
                o.fog = ComputeFogFactor(p.positionCS.z);
                // Semilla por objeto a partir de su posición en el mundo: variación sin material extra.
                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = _Seed + dot(originWS, float3(1.3, 2.1, 0.7));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Cada columna (streak) tiene su propio largo y retraso.
                float column = floor(i.uv.x * _Streaks);
                float len = lerp(0.45, 1.05, Hash(column + i.seed));
                float delay = Hash(column * 3.7 + i.seed) * 0.35;
                float reach = saturate((_CenotePollution - delay) / (1.0 - delay)) * len;

                float fromTop = 1.0 - i.uv.y;
                half body = 1.0h - smoothstep(reach - 0.12, reach, fromTop);
                float cx = frac(i.uv.x * _Streaks);
                half width = smoothstep(0.0, 0.25, cx) * smoothstep(1.0, 0.75, cx);
                half sides = sin(i.uv.x * PI);

                half a = body * lerp(0.35h, 1.0h, width) * sides * step(0.001, _CenotePollution);
                half3 col = lerp(_EdgeColor.rgb, _SeepColor.rgb, saturate(fromTop / max(reach, 1e-3)));
                col = MixFog(col, i.fog);
                return half4(col, a * _SeepColor.a);
            }
            ENDHLSL
        }
    }
}
