Shader "Custom/StylizedYarn"
{
    Properties
    {
        [MainColor]
        _BaseColor(
            "Base Color",
            Color
        ) = (1, 0.2, 0.4, 1)

        [MainTexture]
        _BaseMap(
            "Base Map",
            2D
        ) = "white" {}

        [Normal]
        _NormalMap(
            "Yarn Normal Map",
            2D
        ) = "bump" {}

        _NormalStrength(
            "Normal Strength",
            Range(0, 2)
        ) = 0.7

        _NormalTiling(
            "Normal Tiling",
            Vector
        ) = (2, 1, 0, 0)

        _FlowOffset(
            "Flow Offset",
            Float
        ) = 0

        _FlowScale(
            "Flow Scale",
            Float
        ) = 1

        _AmbientStrength(
            "Ambient Strength",
            Range(0, 1)
        ) = 0.35

        _WrapLighting(
            "Wrap Lighting",
            Range(0, 1)
        ) = 0.35

        _RimStrength(
            "Rim Strength",
            Range(0, 1)
        ) = 0.1

        _RimPower(
            "Rim Power",
            Range(0.5, 8)
        ) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 tangentWS : TEXCOORD2;
                half3 bitangentWS : TEXCOORD3;

                float2 uv : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            CBUFFER_START(UnityPerMaterial)

                half4 _BaseColor;
                float4 _BaseMap_ST;

                float4 _NormalTiling;

                half _NormalStrength;
                half _AmbientStrength;
                half _WrapLighting;
                half _RimStrength;
                half _RimPower;

                float _FlowOffset;
                float _FlowScale;

            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        input.normalOS,
                        input.tangentOS
                    );

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;

                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;

                output.uv = input.uv;

                output.fogFactor = ComputeFogFactor(
                    positionInputs.positionCS.z
                );

                return output;
            }

            half3 UnpackNormalStrength(
                half4 packedNormal,
                half strength
            )
            {
                half3 normalTS =
                    UnpackNormal(packedNormal);

                normalTS.xy *= strength;
                normalTS.z = sqrt(
                    saturate(
                        1.0h -
                        dot(normalTS.xy, normalTS.xy)
                    )
                );

                return normalTS;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 baseUV =
                    input.uv * _BaseMap_ST.xy +
                    _BaseMap_ST.zw;

                half4 baseSample = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    baseUV
                );

                half3 baseColor =
                    baseSample.rgb * _BaseColor.rgb;

                float2 normalUV =
                    input.uv * _NormalTiling.xy;

                // UV.y is the length of the thread.
                normalUV.y -=
                    _FlowOffset * _FlowScale;

                half4 packedNormal = SAMPLE_TEXTURE2D(
                    _NormalMap,
                    sampler_NormalMap,
                    normalUV
                );

                half3 normalTS = UnpackNormalStrength(
                    packedNormal,
                    _NormalStrength
                );

                half3 tangentWS =
                    normalize(input.tangentWS);

                half3 bitangentWS =
                    normalize(input.bitangentWS);

                half3 geometricNormalWS =
                    normalize(input.normalWS);

                half3x3 tangentToWorld = half3x3(
                    tangentWS,
                    bitangentWS,
                    geometricNormalWS
                );

                half3 normalWS = normalize(
                    TransformTangentToWorld(
                        normalTS,
                        tangentToWorld
                    )
                );

                Light mainLight = GetMainLight(
                    TransformWorldToShadowCoord(
                        input.positionWS
                    )
                );

                half rawNdotL = dot(
                    normalWS,
                    mainLight.direction
                );

                half wrappedNdotL = saturate(
                    (rawNdotL + _WrapLighting) /
                    (1.0h + _WrapLighting)
                );

                half shadow =
                    mainLight.shadowAttenuation *
                    mainLight.distanceAttenuation;

                half3 directLight =
                    mainLight.color *
                    wrappedNdotL *
                    shadow;

                half3 ambient =
                    SampleSH(normalWS) *
                    _AmbientStrength;

                half3 viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(
                        input.positionWS
                    );

                half rim = pow(
                    1.0h -
                    saturate(
                        dot(normalWS, viewDirectionWS)
                    ),
                    _RimPower
                );

                half3 finalColor =
                    baseColor *
                    (directLight + ambient);

                finalColor +=
                    baseColor *
                    rim *
                    _RimStrength;

                finalColor = MixFog(
                    finalColor,
                    input.fogFactor
                );

                return half4(
                    finalColor,
                    _BaseColor.a * baseSample.a
                );
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz
                    );

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }
}