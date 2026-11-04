// Partícula circular suave calculada en el shader (sin textura): ahorra memoria y ancho de banda.
// _SrcBlend/_DstBlend permiten usarla en modo alfa (lixiviados, residuos) o aditivo (bioluminiscencia).
Shader "CenoteVivo/ParticleSoft"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Softness", Range(0.5, 4)) = 1.6
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color * _Tint;
                o.uv = v.uv * 2.0 - 1.0;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half mask = pow(saturate(1.0h - dot(i.uv, i.uv)), _Softness);
                half4 c = i.color;
                c.a *= mask;
                // Alfa premultiplicado: con Blend One OneMinusSrcAlpha es mezcla normal y con One One es aditivo.
                c.rgb = MixFog(c.rgb, i.fog) * c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
