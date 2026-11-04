// Agua dinámica para el cenote (URP, optimizado para móviles).
// - Refracción ligera: muestrea _CameraOpaqueTexture desplazada por el mapa de normales.
// - Absorción por profundidad (Depth Fade) con _CameraDepthTexture.
// - Fresnel + reflexión del probe de entorno + especular Blinn-Phong del sol.
// - _PollutionLevel interpola de agua cristalina turquesa a agua turbia y opaca.
// - Bioluminiscencia nocturna (_CenoteNight global) que se apaga con la contaminación.
Shader "CenoteVivo/CenoteWater"
{
    Properties
    {
        [Header(Agua limpia)]
        _WaterColor ("Water Color (somero)", Color) = (0.10, 0.78, 0.80, 1)
        _AbsorptionColor ("Absorption Color (profundo)", Color) = (0.01, 0.20, 0.32, 1)
        _Turbidity ("Turbidity", Range(0, 1)) = 0.05
        _DepthDistance ("Depth Distance (m)", Float) = 6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92

        [Header(Contaminacion)]
        _PollutionLevel ("Pollution Level", Range(0, 1)) = 0
        _PollutedColor ("Polluted Color (somero)", Color) = (0.27, 0.25, 0.12, 1)
        _PollutedDeepColor ("Polluted Color (profundo)", Color) = (0.06, 0.06, 0.03, 1)
        _ScumColor ("Scum Color (pelicula)", Color) = (0.20, 0.22, 0.10, 1)

        [Header(Superficie)]
        [NoScaleOffset][Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalTiling ("Normal Tiling", Float) = 0.18
        _NormalStrength ("Normal Strength", Range(0, 1.5)) = 0.55
        _WaveSpeed ("Wave Speed", Float) = 0.035
        _RefractionStrength ("Refraction Strength", Range(0, 0.1)) = 0.025
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4

        [Header(Noche)]
        _BioColor ("Bioluminescence Color", Color) = (0.15, 1.0, 0.85, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "CenoteWater"
            Tags { "LightMode" = "UniversalForward" }

            // El color ya compone la escena refractada: no hace falta mezclar alfa.
            Blend One Zero
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor;
                half4 _AbsorptionColor;
                half _Turbidity;
                float _DepthDistance;
                half _Smoothness;
                half _PollutionLevel;
                half4 _PollutedColor;
                half4 _PollutedDeepColor;
                half4 _ScumColor;
                float _NormalTiling;
                half _NormalStrength;
                float _WaveSpeed;
                half _RefractionStrength;
                half _FresnelPower;
                half4 _BioColor;
            CBUFFER_END

            // Valor global escrito por DayNightController (0 = día, 1 = noche).
            half _CenoteNight;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float4 uvWaves    : TEXCOORD2; // dos juegos de UV desplazándose en sentidos opuestos
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                // Las UV y el desplazamiento se calculan por vértice para ahorrar ALU en el fragmento.
                float2 uv = p.positionWS.xz * _NormalTiling;
                float t = _Time.y * _WaveSpeed;
                o.uvWaves = float4(uv + float2(t, t * 0.6), uv * 1.37 - float2(t * 0.8, -t * 0.45));
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half P = _PollutionLevel;

                // Normales: dos muestras del mismo mapa, promediadas (empaquetado: 1 textura, 2 lecturas).
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uvWaves.xy));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uvWaves.zw));
                half2 ripple = (n1.xy + n2.xy) * (_NormalStrength * (1.0h - 0.6h * P)); // el agua sucia se mueve menos
                half3 N = normalize(half3(ripple.x, 1.0h, ripple.y));
                N = isFrontFace ? N : -N;

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Profundidad de la columna de agua detrás de la superficie.
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thickness = max(0.0, sceneEye - i.screenPos.w);

                half turbidity = lerp(_Turbidity, 1.0h, P);
                float distanceScale = _DepthDistance * lerp(1.0, 0.06, turbidity);
                half depthFade = (half)(1.0 - exp(-thickness / distanceScale));

                half3 shallow = lerp(_WaterColor.rgb, _PollutedColor.rgb, P);
                half3 deep = lerp(_AbsorptionColor.rgb, _PollutedDeepColor.rgb, P);
                half3 waterCol = lerp(shallow, deep, depthFade);

                // Refracción ligera: se atenúa con la profundidad y desaparece al contaminarse.
                float2 refrUV = screenUV + ripple * _RefractionStrength * (1.0h - P) * saturate(thickness);
                half3 behind = SampleSceneColor(refrUV) * shallow * 1.6h;
                half opacity = saturate(depthFade + turbidity * 0.9h);
                half3 col = lerp(behind, waterCol, opacity);

                // Iluminación del sol / luna.
                Light mainLight = GetMainLight();
                half smooth = _Smoothness * (1.0h - 0.65h * P);
                half3 H = normalize(mainLight.direction + V);
                half specPow = exp2(10.0h * smooth + 1.0h);
                half3 spec = mainLight.color * pow(saturate(dot(N, H)), specPow) * smooth;
                col *= 0.55h + 0.45h * (SampleSH(N) + mainLight.color * saturate(mainLight.direction.y));

                // Fresnel + reflexión del entorno.
                half fresnel = pow(1.0h - saturate(dot(N, V)), _FresnelPower);
                half3 refl = GlossyEnvironmentReflection(reflect(-V, N), 1.0h - smooth, 1.0h);
                col += (refl * fresnel * (1.0h - 0.6h * P) + spec) * (isFrontFace ? 1.0h : 0.25h);

                // Película de residuos flotantes en el agua contaminada.
                half scum = saturate((n1.z * n2.z - 0.92h) * 14.0h) * smoothstep(0.35h, 1.0h, P);
                col = lerp(col, _ScumColor.rgb, scum);

                // Bioluminiscencia nocturna: chispas cian que solo existen en agua limpia.
                half sparkle = saturate((abs(n1.x - n2.y) - 0.55h) * 6.0h);
                col += _BioColor.rgb * sparkle * _CenoteNight * (1.0h - P) * 0.8h;

                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
