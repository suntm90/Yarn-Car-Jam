Shader "YarnCarJam/Sphere Energy Rim"
{
    Properties
    {
        [HDR] _RimColor ("Rim Color", Color) = (0.0, 0.55, 1.0, 1.0)
        [HDR] _HighlightColor ("Moving Highlight Color", Color) = (0.35, 1.5, 3.0, 1.0)
        _CenterAlpha ("Center Alpha", Range(0, 1)) = 0.02
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.75
        _RimPower ("Rim Width", Range(0.5, 8)) = 3.0
        _HighlightSharpness ("Highlight Sharpness", Range(1, 32)) = 12.0
        _HighlightIntensity ("Highlight Intensity", Range(0, 5)) = 2.0
        _HighlightSpeed ("Highlight Speed", Range(-10, 10)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half4 _HighlightColor;
                half _CenterAlpha;
                half _RimAlpha;
                half _RimPower;
                half _HighlightSharpness;
                half _HighlightIntensity;
                half _HighlightSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                // Transparent at the view-facing center, colored toward the silhouette.
                half facing = saturate(dot(normalWS, viewDirectionWS));
                half rim = pow(saturate(1.0h - facing), _RimPower);

                // Camera basis makes the highlights orbit around the visible screen-space rim.
                half aroundX = dot(normalWS, UNITY_MATRIX_I_V[0].xyz);
                half aroundY = dot(normalWS, UNITY_MATRIX_I_V[1].xyz);
                half angle = atan2(aroundY, aroundX);
                half phase = angle - _Time.y * _HighlightSpeed;

                // abs(cos) creates exactly two opposing highlights, 180 degrees apart.
                half twoHighlights = pow(saturate(abs(cos(phase))), _HighlightSharpness);
                twoHighlights *= rim;

                half3 color = _RimColor.rgb * rim
                    + _HighlightColor.rgb * twoHighlights * _HighlightIntensity;
                half alpha = lerp(_CenterAlpha, _RimAlpha, rim);
                alpha = saturate(alpha + twoHighlights * _HighlightColor.a * 0.2h);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
