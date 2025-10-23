Shader "URP/FlashParticle"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.8, 0.3, 1)
        _Size ("Particle Size", Float) = 0.1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct FlashParticle
            {
                float3 Position;
                float3 Velocity;
                float Lifetime;
                float Brightness;
            };
            
            StructuredBuffer<FlashParticle> flashBuffer;
            float4 _Color;
            float _Size;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float brightness : TEXCOORD1;
                float lifetime : TEXCOORD2;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                FlashParticle particle = flashBuffer[input.instanceID];
                
                // 생명주기가 끝난 파티클은 화면 밖으로
                if (particle.Lifetime <= 0.0)
                {
                    output.positionCS = float4(0, 0, 0, 0);
                    output.uv = input.uv;
                    output.brightness = 0;
                    output.lifetime = 0;
                    return output;
                }
                
                // Billboard: 카메라를 향하도록 회전
                float3 worldPos = particle.Position;
                
                // URP에서 카메라 방향 벡터 추출
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                
                float3 vertexOffset = (input.positionOS.x * cameraRight + input.positionOS.y * cameraUp) * _Size;
                float3 finalWorldPos = worldPos + vertexOffset;
                
                // World to Clip Space (URP 방식)
                output.positionCS = TransformWorldToHClip(finalWorldPos);
                output.uv = input.uv;
                output.brightness = particle.Brightness;
                output.lifetime = particle.Lifetime;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 중심에서 거리에 따른 알파 (원형 그라데이션)
                float dist = length(input.uv - 0.5) * 2.0;
                float alpha = 1.0 - saturate(dist);
                alpha = pow(alpha, 2.0);
                
                // Brightness 적용
                half4 col = _Color * input.brightness;
                col.a *= alpha;
                
                return col;
            }
            ENDHLSL
        }
    }
}