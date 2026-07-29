Shader "Apex Physics Engine/Water/URP Low Poly"
{
    Properties
    {
        [Header(Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.25, 0.65, 0.6, 0.65)
        _DeepColor ("Deep Color", Color) = (0.02, 0.15, 0.25, 0.95)
        _DepthMaxDistance ("Depth Max Distance", Float) = 6
        _Transparency ("Transparency", Range(0,1)) = 0.85

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0,8)) = 3
        _FresnelColor ("Fresnel Color", Color) = (1,1,1,1)

        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (1,1,1,1)
        _FoamDistance ("Foam Distance", Float) = 0.4

        [Header(Waves)]
        _WaveHeight ("Wave Height", Range(0,5)) = 0.25
        _WaveSpeed ("Wave Speed", Range(0,5)) = 1
        _WaveScale ("Wave Scale", Range(0.1,50)) = 6
        _WindDirX ("Wind Direction X", Float) = 1
        _WindDirZ ("Wind Direction Z", Float) = 0.3

        [Header(Lighting)]
        _Smoothness ("Smoothness", Range(0,1)) = 0.7

        [Header(Optional Effects)]
        [Toggle] _EnableCaustics ("Enable Caustics", Float) = 0
        _CausticsTex ("Caustics Texture", 2D) = "black" {}
        _CausticsStrength ("Caustics Strength", Range(0,2)) = 0.5
        [Toggle] _EnableDistortion ("Enable Underwater Distortion", Float) = 1
        _DistortionStrength ("Distortion Strength", Range(0,0.2)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthMaxDistance;
                float _Transparency;
                float _FresnelPower;
                half4 _FresnelColor;
                half4 _FoamColor;
                float _FoamDistance;
                float _WaveHeight;
                float _WaveSpeed;
                float _WaveScale;
                float _WindDirX;
                float _WindDirZ;
                float _Smoothness;
                float _EnableCaustics;
                float _CausticsStrength;
                float _EnableDistortion;
                float _DistortionStrength;
            CBUFFER_END

            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float3 viewDirWS  : TEXCOORD3;
            };

            // Mirrors ApexWaterMath on the CPU side so the visual surface matches
            // the physics/height queries used by ApexWaterVolume, ApexBuoyantBody,
            // and ApexSwimmer.
            float SampleWaveHeight(float3 worldPos, float time)
            {
                float2 wind = normalize(float2(_WindDirX, _WindDirZ) + 1e-5);
                float h = 0;

                UNITY_UNROLL
                for (int i = 0; i < 4; i++)
                {
                    float angleOffset = radians(i * 27.0 - 40.0);
                    float cs = cos(angleOffset);
                    float sn = sin(angleOffset);
                    float2 dir = normalize(float2(wind.x * cs - wind.y * sn, wind.x * sn + wind.y * cs));

                    float freqMul = 1.0 + i * 0.63;
                    float amplitude = _WaveHeight * (1.0 / (i + 1.0)) * 0.6;
                    float wavelength = max(0.01, _WaveScale / freqMul);
                    float speed = _WaveSpeed * (0.7 + i * 0.15);

                    float k = 2.0 * PI / wavelength;
                    float phase = dot(dir, worldPos.xz) * k + time * speed;
                    h += amplitude * sin(phase);
                }
                return h;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS);
                float waveOffset = SampleWaveHeight(positionWS, _Time.y);
                positionWS.y += waveOffset;

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dx = ddx(IN.positionWS);
                float3 dy = ddy(IN.positionWS);
                float3 normalWS = normalize(cross(dy, dx));
                float3 viewDirWS = normalize(IN.viewDirWS);

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float surfaceEyeDepth = IN.screenPos.w;
                float depthDifference = max(0, sceneEyeDepth - surfaceEyeDepth);

                float depthFactor = saturate(depthDifference / max(0.001, _DepthMaxDistance));
                half4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);

                float foamFactor = 1.0 - saturate(depthDifference / max(0.001, _FoamDistance));
                foamFactor = smoothstep(0.0, 1.0, foamFactor);
                waterColor.rgb = lerp(waterColor.rgb, _FoamColor.rgb, foamFactor * _FoamColor.a);

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                waterColor.rgb = lerp(waterColor.rgb, _FresnelColor.rgb, fresnel);

                if (_EnableCaustics > 0.5)
                {
                    float2 causticUV = IN.positionWS.xz * 0.1 + _Time.y * 0.05;
                    half3 caustics = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, causticUV).rgb;
                    waterColor.rgb += caustics * _CausticsStrength * (1.0 - depthFactor);
                }

                half3 sceneColor;
                if (_EnableDistortion > 0.5)
                {
                    float2 distortedUV = screenUV + normalWS.xz * _DistortionStrength;
                    sceneColor = SampleSceneColor(distortedUV);
                }
                else
                {
                    sceneColor = SampleSceneColor(screenUV);
                }

                half3 finalColor = lerp(sceneColor, waterColor.rgb, saturate(waterColor.a * _Transparency + fresnel * 0.3));

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float3 halfVec = normalize(mainLight.direction + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfVec)), lerp(8.0, 128.0, _Smoothness));
                finalColor += mainLight.color * spec * 0.5;
                finalColor *= lerp(0.6, 1.0, ndotl);

                return half4(finalColor, saturate(_Transparency + foamFactor * 0.5));
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
