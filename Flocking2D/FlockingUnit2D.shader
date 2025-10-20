// FlockingUnit.shader
Shader "Custom/FlockingUnit2D"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 4.5

            #include "UnityCG.cginc"

            struct UnitData
            {
                float2 position;
                float2 velocity;
            };

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            StructuredBuffer<UnitData> _UnitsBuffer;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _UnitScale;

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                // GPU 버퍼에서 유닛 데이터 읽기 (CPU 복사 없음)
                UnitData unit = _UnitsBuffer[instanceID];
                
                //// 회전 계산 (velocity 방향)
                //float angle = atan2(unit.velocity.y, unit.velocity.x) - 1.5708; // -90도
                //float cosA = cos(angle);
                //float sinA = sin(angle);
                
                //// 2D 회전 매트릭스
                //float2x2 rotMatrix = float2x2(cosA, -sinA, sinA, cosA);

                // 🔥 개선: 정규화된 벡터로 직접 계산 (빠름)
                float2 forward = normalize(unit.velocity);
                float2 right = float2(-forward.y, forward.x);

                // 회전 매트릭스 (삼각함수 없음!)
                float2x2 rotMatrix = float2x2(
                    forward.x, -forward.y,
                    forward.y, forward.x
                );
                
                // 로컬 정점을 회전
                float2 rotatedPos = mul(rotMatrix, v.vertex.xy * _UnitScale);
                
                // 월드 위치에 추가
                float3 worldPos = float3(
                    unit.position.x + rotatedPos.x,
                    unit.position.y + rotatedPos.y,
                    0
                );
                
                // VP 매트릭스 적용
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // 속도 기반 색상 (옵션)
                float speed = length(unit.velocity);
                o.color = lerp(float4(0.2, 0.2, 1, 1), float4(1, 0.2, 0.2, 1), speed / 10.0);
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color * i.color;
                return col;
            }
            ENDCG
        }
    }
}