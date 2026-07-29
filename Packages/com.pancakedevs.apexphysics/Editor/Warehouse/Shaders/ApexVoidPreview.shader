Shader "Hidden/Apex Physics Engine/Void Preview"
{
    Properties
    {
        _VoidColor ("Void Color", Color) = (0.01, 0.015, 0.03, 1)
        _EdgeColor ("Edge Color", Color) = (0.16, 0.75, 1.0, 1)
        _GridColor ("Grid Color", Color) = (0.38, 0.12, 0.7, 1)
        _Opacity ("Opacity", Range(0.05, 1)) = 0.55
        _GridScale ("Grid Scale", Range(1, 40)) = 12
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 1.6
        _FresnelPower ("Fresnel Power", Range(0.25, 8)) = 2.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ApexVoidPreviewURP"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _VoidColor;
                half4 _EdgeColor;
                half4 _GridColor;
                half _Opacity;
                half _GridScale;
                half _PulseSpeed;
                half _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirectionWS : TEXCOORD2;
            };

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirectionWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(input.viewDirectionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), _FresnelPower);

                float3 gridCoordinates = abs(frac(input.positionOS * _GridScale) - 0.5);
                float closestLine = min(gridCoordinates.x, min(gridCoordinates.y, gridCoordinates.z));
                float grid = 1.0 - smoothstep(0.035, 0.085, closestLine);

                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + input.positionOS.y * 4.0);
                float noise = Hash31(floor(input.positionOS * _GridScale * 0.5) + _Time.y * 0.15);

                float edgeAmount = saturate(fresnel * 1.25 + grid * 0.35 + pulse * 0.08);
                half3 color = lerp(_VoidColor.rgb, _EdgeColor.rgb, edgeAmount);
                color = lerp(color, _GridColor.rgb, grid * (0.25 + noise * 0.25));

                half alpha = saturate(_Opacity * (0.35 + fresnel * 0.75 + grid * 0.25));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            fixed4 _VoidColor;
            fixed4 _EdgeColor;
            fixed4 _GridColor;
            float _Opacity;
            float _GridScale;
            float _PulseSpeed;
            float _FresnelPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirectionWS : TEXCOORD2;
            };

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            v2f Vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.positionOS = input.vertex.xyz;
                output.normalWS = UnityObjectToWorldNormal(input.normal);
                float3 positionWS = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.viewDirectionWS = _WorldSpaceCameraPos.xyz - positionWS;
                return output;
            }

            fixed4 Frag(v2f input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(input.viewDirectionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), _FresnelPower);

                float3 gridCoordinates = abs(frac(input.positionOS * _GridScale) - 0.5);
                float closestLine = min(gridCoordinates.x, min(gridCoordinates.y, gridCoordinates.z));
                float grid = 1.0 - smoothstep(0.035, 0.085, closestLine);

                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + input.positionOS.y * 4.0);
                float noise = Hash31(floor(input.positionOS * _GridScale * 0.5) + _Time.y * 0.15);

                float edgeAmount = saturate(fresnel * 1.25 + grid * 0.35 + pulse * 0.08);
                fixed3 color = lerp(_VoidColor.rgb, _EdgeColor.rgb, edgeAmount);
                color = lerp(color, _GridColor.rgb, grid * (0.25 + noise * 0.25));

                float alpha = saturate(_Opacity * (0.35 + fresnel * 0.75 + grid * 0.25));
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
