Shader "Custom/ColorField"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _VelocityTex ("Velocity Texture", 2D) = "white" {}
        _ObstacleTex ("Obstacle Texture", 2D) = "black" {}
        _Velocity ("Velocity", Vector) = (0, 0, 0, 0)
        _FluidDomainOffset ("Fluid Domain Offset", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _VelocityTex;
            sampler2D _ObstacleTex;
            float4 _Velocity;
            float4 _FluidDomainOffset;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 hash2(float2 p, float t)
            {
                // 将输入和时间混合，生成伪随机数
                float3 x = float3(p, t);
                x = frac(x * float3(0.1031, 0.11369, 0.13787));
                x += dot(x, x.yzx + 19.19);
                return frac(float2(
                    (x.x + x.y) * x.z,
                    (x.x + x.z) * x.y
                ));
            }

            
            float4 frag (v2f i) : SV_Target
            {
                float2 velocity_sample = tex2D(_VelocityTex, i.uv + _Velocity.xy).xy - 0.5;
                float2 add_velocity = ((velocity_sample * -20) / float2(1280, 720)) * 0.45 + i.uv + _Velocity.xy;
                float obstacle = tex2D(_ObstacleTex, i.uv).a;

                if (obstacle > 0.0)
                {
                    return float4(0, 0, 0, -0.02); // it should be 0.0, but i use -0.02 for a special reason, which make sample colorField can get the info about obstical 
                }

                if (i.uv.x > 0.99 || i.uv.y > 0.99 || i.uv.x < 0.01 || i.uv.y < 0.01)
                {
                    return float4(0, 0, 0, 0);
                }
                
                if (add_velocity.x < 0.0 || add_velocity.x > 1.0 || add_velocity.y < 0.0 || add_velocity.y > 1.0)
                {
                    return float4(0, 0, 0, 0);
                }
                float4 color = tex2D(_MainTex, add_velocity);// + (hash2(i.uv, _Time.y) - float2(0.5, 0.5)) * 0.001);
                // color.rgb = lerp(color.rgb, float3(0,0,0), 0.02); // 变黑
                color.a = max(0.0, abs(color.a) - 1e-3);
                return color;
            }

            // fixed4 frag (v2f i) : SV_Target
            // {
            //     const float dt = 0.15;
            //     float2 velocity_sample = (tex2D(_VelocityTex, i.uv).xy - 0.5) * 20;
            //     float4 color = tex2D(_MainTex, dt * (-add_velocity / float2(1024, 1024)) + i.uv);
            //     return color;
            // }
            ENDCG
        }
    }
}