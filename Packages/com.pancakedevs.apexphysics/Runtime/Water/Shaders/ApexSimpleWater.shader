Shader "Apex Physics Engine/Water/Simple"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.25, 0.65, 0.6, 0.65)
        _DeepColor ("Deep Color", Color) = (0.02, 0.15, 0.25, 0.95)
        _FresnelColor ("Fresnel Color", Color) = (1,1,1,1)
        _Transparency ("Transparency", Range(0,1)) = 0.85
        _FresnelPower ("Fresnel Power", Range(0.1,8)) = 3
        _WaveHeight ("Wave Height", Float) = 0.25
        _WaveSpeed ("Wave Speed", Float) = 1
        _WaveScale ("Wave Scale", Float) = 6
        _WindDirX ("Wind X", Float) = 1
        _WindDirZ ("Wind Z", Float) = 0.3
        _DepthMaxDistance ("Depth Max Distance", Float) = 6
        _FoamColor ("Foam Color", Color) = (1,1,1,1)
        _FoamDistance ("Foam Distance", Float) = 0.4
        _CausticsTex ("Caustics", 2D) = "white" {}
        _EnableCaustics ("Enable Caustics", Float) = 0
        _CausticsStrength ("Caustics Strength", Float) = 0.5
        _EnableDistortion ("Enable Distortion", Float) = 0
        _DistortionStrength ("Distortion Strength", Float) = 0.03
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            fixed4 _FresnelColor;
            float _Transparency;
            float _FresnelPower;
            float _WaveHeight;
            float _WaveSpeed;
            float _WaveScale;
            float _WindDirX;
            float _WindDirZ;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            float SampleWaveHeight(float3 worldPosition, float time)
            {
                float2 wind = normalize(float2(_WindDirX, _WindDirZ) + float2(0.0001, 0.0001));
                float height = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float angle = radians(i * 27.0 - 40.0);
                    float cosine = cos(angle);
                    float sine = sin(angle);
                    float2 direction = normalize(float2(
                        wind.x * cosine - wind.y * sine,
                        wind.x * sine + wind.y * cosine));
                    float frequencyMultiplier = 1.0 + i * 0.63;
                    float amplitude = _WaveHeight * (1.0 / (i + 1.0)) * 0.6;
                    float wavelength = max(0.01, _WaveScale / frequencyMultiplier);
                    float speed = _WaveSpeed * (0.7 + i * 0.15);
                    float waveNumber = 6.28318530718 / wavelength;
                    float phase = dot(direction, worldPosition.xz) * waveNumber + time * speed;
                    height += amplitude * sin(phase);
                }
                return height;
            }

            v2f vert(appdata input)
            {
                v2f output;
                float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                worldPosition.y += SampleWaveHeight(worldPosition, _Time.y);
                output.positionWS = worldPosition;
                output.positionCS = mul(UNITY_MATRIX_VP, float4(worldPosition, 1));
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 derivativeX = ddx(input.positionWS);
                float3 derivativeY = ddy(input.positionWS);
                float3 normal = normalize(cross(derivativeY, derivativeX));
                if (normal.y < 0) normal = -normal;
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDirection)), _FresnelPower);
                float waveTint = saturate(normal.y * 0.5 + 0.5);
                fixed4 color = lerp(_DeepColor, _ShallowColor, waveTint);
                color.rgb = lerp(color.rgb, _FresnelColor.rgb, fresnel * 0.65);
                color.a = saturate(color.a * _Transparency + fresnel * 0.15);
                return color;
            }
            ENDCG
        }
    }

    FallBack Off
}
