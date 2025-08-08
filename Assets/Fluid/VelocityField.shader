Shader "Custom/VelocityField"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ObstacleTex ("Obstacle Texture", 2D) = "black" {}
        _ObstacleTexPre ("Previous Obstacle Texture", 2D) = "black" {}
        _Velocity ("Velocity", Vector) = (0, 0, 0, 0)
        _ObstacleForceStrength ("Obstacle Force Strength", Float) = 5.0
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
            sampler2D _ObstacleTex;
            sampler2D _ObstacleTexPre;
            float4 _Velocity;
            float _ObstacleForceStrength;
            float4 _FluidDomainOffset;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float test(float2 uv, float2 fluidDomainOffset)
            {
                float a = tex2D(_ObstacleTexPre, uv + fluidDomainOffset).a;
                float b = tex2D(_ObstacleTex, uv).a;
                
                // 更精细的差异检测和可视化
                float diff = abs(a - b);
                
                // 返回差异的可视化：
                // 白色(1.0) = 有差异 (可能是移动的物体)
                // 灰色(0.5) = 小差异 (可能是采样误差/时间不同步)  
                // 黑色(0.0) = 无差异 (静止物体)
                
                if (diff > 0.5)
                    return 1.0; // 大差异 - 白色
                else if (diff > 0.1)
                    return 0.5; // 中等差异 - 灰色
                else
                    return 0.0; // 小差异 - 黑色
            }
            
            // 计算障碍物运动产生的推力
            float2 CalculateObstacleForce(float2 uv, float wx, float wy, float2 fluidDomainOffset)
            {
                // 采样当前帧和前一帧的障碍物
                float obstacleCurrent = tex2D(_ObstacleTex, uv).a;
                
                // 如果当前位置没有障碍物，检查周围是否有障碍物运动
                if (obstacleCurrent < 0.1)
                {
                    float2 force = float2(0, 0);
                    
                    // 检查四个方向的障碍物运动
                    float2 directions[4] = {
                        float2(wx, 0),    // 右
                        float2(-wx, 0),   // 左
                        float2(0, wy),    // 上
                        float2(0, -wy)    // 下
                    };
                    
                    for (int i = 0; i < 4; i++)
                    {
                        float2 samplePos = uv + directions[i];
                        
                        // 确保在纹理范围内
                        if (samplePos.x >= 0.0 && samplePos.x <= 1.0 && 
                            samplePos.y >= 0.0 && samplePos.y <= 1.0)
                        {
                            float obstCur = tex2D(_ObstacleTex, samplePos).a;
                            // 补偿摄像机移动：加上摄像机位移来采样前一帧对应的世界位置
                            float obstPre = tex2D(_ObstacleTexPre, samplePos + fluidDomainOffset).a;
                            
                            // 如果障碍物从这个位置消失了，说明障碍物向当前位置移动
                            if (obstPre > 0.1 && obstCur < 0.1)
                            {
                                // 障碍物从该方向移向当前位置，产生推力
                                force += normalize(directions[i]) * obstPre * _ObstacleForceStrength;
                            }
                            // 如果障碍物出现在这个位置，说明有推挤效果
                            else if (obstCur > 0.1 && obstPre < 0.1)
                            {
                                // 障碍物移动到该方向，推动流体远离
                                force -= normalize(directions[i]) * obstCur * _ObstacleForceStrength;
                            }
                        }
                    }
                    
                    return force;
                }
                
                return float2(0, 0);
            }



            #if 0


            fixed4 frag (v2f i) : SV_Target
            {
                const float wx = 0.00078125; // 1 / 1280 = 0.00078125
                const float wy = 0.00138888; // 1 / 720 = 0.00138888
                const float dt = 0.15;
                const float CScale = 0.5;
                const float v = 0.55;
                const float K = 0.3;
                const float VORTICITY_AMOUNT = 0.11;

                float4 mid = (tex2D(_MainTex, i.uv) - 0.5) * 20;
                float4 up = (tex2D(_MainTex, i.uv + float2(0, wy)) - 0.5) * 20;
                float4 down = (tex2D(_MainTex, i.uv - float2(0, wy)) - 0.5) * 20;
                float4 left = (tex2D(_MainTex, i.uv - float2(wx, 0)) - 0.5) * 20;
                float4 right = (tex2D(_MainTex, i.uv + float2(wx, 0)) - 0.5) * 20;

                // Gradient
                float3 dx = (right.xyz - left.xyz) * CScale;
                float3 dy = (up.xyz - down.xyz) * CScale;
                float2 densDiff = float2(dx.z, dy.z);
                
                // Solve for density
                mid.z -= dt * dot(float3(densDiff, dx.x + dy.y), mid.xyz);

                // Solve for velocity
                float2 laplacian = up.xy + down.xy + left.xy + right.xy - 4 * mid.xy;
                float2 viscForce = v * laplacian;
                
                // ADVECTION
                mid.xyw = (tex2D(_MainTex, i.uv - mid.xy * dt * float2(wx, wy)).xyw - 0.5) * 20;

                mid.xy += dt * (viscForce.xy - K / dt * densDiff);

                // Vort
                mid.w = right.y - left.y - up.x + down.x;
                float2 vort = float2(abs(up.w) - abs(down.w), abs(left.w) - abs(right.w));
                vort *= VORTICITY_AMOUNT / length(vort + 1e-9) * mid.w;
                mid.xy += vort;

                return (mid * 0.05) + float4(0.5, 0.5, 0.5, 0.5);
            }


            // Vort
            // mid.w = right.y - left.y - up.x + down.x;
            // float2 vort = float2(abs(up.w) - abs(down.w), abs(left.w) - abs(right.w));
            // vort *= VORTICITY_AMOUNT / length(vort + 1e-9) * mid.w;
            // mid.xy += vort;



            #else
            
            fixed4 frag (v2f i) : SV_Target
            {
                const float wx = 0.00078125; // 1 / 1280 = 0.00078125
                const float wy = 0.00138888; // 1 / 720 = 0.00138888
                const float K = 0.3;
                const float dt = 0.15;
                const float VORTICITY_AMOUNT = 0.11;

                // 采样当前位置的障碍物
                float4 obstacle_color = tex2D(_ObstacleTex, i.uv);
                float obstacle = obstacle_color.a;
                float4 obstacle_function = float4(0, 0, 0, 0);
                
                // 如果当前位置是障碍物，直接返回零速度
                if (obstacle > 0.75)
                {
                    return float4(0.5, 0.5, 0.515, 0.5); // 零速度（编码为0.5）
                }
                else if (obstacle == 0.5)
                {
                    //obstacle_function = (obstacle_color - 0.5) * 20;
                }

                float4 mid;
                float4 up;
                float4 down;
                float4 left;
                float4 right;
                
                // 采样相邻位置的障碍物信息
                float obstacleUp = tex2D(_ObstacleTex, i.uv + float2(0, wy)).a;
                float obstacleDown = tex2D(_ObstacleTex, i.uv - float2(0, wy)).a;
                float obstacleLeft = tex2D(_ObstacleTex, i.uv - float2(wx, 0)).a;
                float obstacleRight = tex2D(_ObstacleTex, i.uv + float2(wx, 0)).a;
                
                if (i.uv.x < 0.99 && i.uv.y < 0.99 && i.uv.x > 0.01 && i.uv.y > 0.01)
                {
                    mid = (tex2D(_MainTex, i.uv + _Velocity.xy) - 0.5) * 20;
                    
                    // 对于障碍物位置，使用边界条件
                    if (obstacleUp > 0.0)
                        up = float4(-mid.x, -mid.y, mid.z, mid.w); // 反射边界条件
                    else
                        up = (tex2D(_MainTex, i.uv + float2(0, wy) + _Velocity.xy) - 0.5) * 20;
                        
                    if (obstacleDown > 0.0)
                        down = float4(-mid.x, -mid.y, mid.z, mid.w); // 反射边界条件
                    else
                        down = (tex2D(_MainTex, i.uv - float2(0, wy) + _Velocity.xy) - 0.5) * 20;
                        
                    if (obstacleLeft > 0.0)
                        left = float4(-mid.x, -mid.y, mid.z, mid.w); // 反射边界条件
                    else
                        left = (tex2D(_MainTex, i.uv - float2(wx, 0) + _Velocity.xy) - 0.5) * 20;
                        
                    if (obstacleRight > 0.0)
                        right = float4(-mid.x, -mid.y, mid.z, mid.w); // 反射边界条件
                    else
                        right = (tex2D(_MainTex, i.uv + float2(wx, 0) + _Velocity.xy) - 0.5) * 20;
                }
                else
                {
                    return float4(0.5, 0.5, 0.515, 0.5);
                }

                float3 dx = (right.xyz - left.xyz) * 0.5;
                float3 dy = (up.xyz - down.xyz) * 0.5;
                float2 densDiff = float2(dx.z, dy.z);

                mid.z -= dt * dot(float3(densDiff, dx.x + dy.y), mid.xyz); // 密度扩散

                float2 laplacian = up.xy + down.xy + left.xy + right.xy - 4 * mid.xy;
                float2 viscForce = 0.55 * laplacian; // 粘性力

                mid.xyw = (tex2D(_MainTex, i.uv - mid.xy * dt * float2(wx, wy) + _Velocity.xy).xyw - 0.5) * 20; // 速度反向采样
                
                mid.xy += dt * (viscForce.xy - K / dt * densDiff); // 速度更新
                mid.xy = max(0.0, abs(mid.xy) - 1e-4) * sign(mid.xy); // 速度衰减

                // 涡流部分
                mid.w = right.y - left.y - up.x + down.x;

                float2 vort = float2(abs(up.w) - abs(down.w), abs(left.w) - abs(right.w));
                vort *= VORTICITY_AMOUNT / length(vort + 1e-9) * mid.w;
                mid.xy += vort;

                // 计算障碍物运动产生的推力
                float2 obstacleForce = CalculateObstacleForce(i.uv, wx * 3, wy * 3, _FluidDomainOffset.xy);
                mid.xy += obstacleForce * dt;

                mid = clamp(mid, float4(-10, -10, 0.5, -10), float4(10, 10, 3, 10));
                // return float4(0.5, 0.5, 0.5, 0.5);
                return (mid * 0.05) + float4(0.5, 0.5, 0.5, 0.5);
            }
            #endif
            ENDCG
        }
    }
}