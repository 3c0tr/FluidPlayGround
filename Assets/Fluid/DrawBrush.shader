Shader "Custom/DrawBrush"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BrushTex ("Brush Texture", 2D) = "white" {}
        _ExploreTex ("Explore Texture", 2D) = "white" {}
        _Color ("Draw Color", Color) = (1,1,1,1)
        _BrushPos ("Brush Position (UV)", Vector) = (0.5, 0.5, 0, 0)
        _BrushSize ("Brush Size", Float) = 10.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        // Pass 0: 颜色绘制Pass
        Pass
        {
            Name "ColorBatchDraw"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            
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
            sampler2D _BrushTex;
            float2 _BrushPos;
            StructuredBuffer<float2> _Positions;
            StructuredBuffer<float4> _Colors;
            StructuredBuffer<float> _ColorRadii;
            int _PointCount;
            float _BrushSize;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                
                for (int idx = 0; idx < _PointCount; idx++)
                {
                    float2 brushPos = _Positions[idx];
                    float radius = _ColorRadii[idx];
                    
                    if (radius <= 0.0001) continue;
                    
                    float2 brushDistance = i.uv - brushPos;
                    float2 textureScale = float2(1024.0/1280.0, 1024.0/720.0);
                    //float2 textureScale = float2(1024.0/1024.0, 1024.0/1024.0);
                    float2 brushUV = brushDistance * (1.0 / (_BrushSize * radius * 0.01 * textureScale)) + 0.5;
                    
                    if (brushUV.x >= 0.0 && brushUV.x <= 1.0 && brushUV.y >= 0.0 && brushUV.y <= 1.0)
                    {
                        float4 brushColor = tex2D(_BrushTex, brushUV);
                        
                        if (brushColor.a > 0.0)
                        {
                            float4 drawColor = _Colors[idx];
                            col.rgb = lerp(col.rgb, brushColor.rgb * drawColor.rgb, brushColor.a * drawColor.a);
                            col.a = lerp(col.a, drawColor.a, brushColor.a);
                        }
                    }
                }

                if (_PointCount == 0)
                {
                    float radius = 1;
                    float2 brushPos = _BrushPos;
                    float2 brushDistance = i.uv - brushPos;
                    //float2 textureScale = float2(1024.0/1024.0, 1024.0/1024.0);
                    float2 textureScale = float2(1024.0/1280.0, 1024.0/720.0);
                    float2 brushUV = brushDistance * (1.0 / (_BrushSize * radius * 0.01 * textureScale)) + 0.5;
                    //brushUV *= float2(0.8, 1.0);
                    if (brushUV.x >= 0.0 && brushUV.x <= 1.0 && brushUV.y >= 0.0 && brushUV.y <= 1.0)
                    {
                        float4 brushColor = tex2D(_BrushTex, brushUV);
                        
                        if (brushColor.a > 0.0)
                        {
                            col.rgb = lerp(col.rgb, brushColor.rgb, brushColor.a);
                            col.a = lerp(col.a, brushColor.a, brushColor.a);
                        }
                    }
                }
                
                return col;
            }
            ENDCG
        }
        
        // Pass 1: 速度绘制Pass
        Pass
        {
            Name "VelocityBatchDraw"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            
            #include "UnityCG.cginc"
            
            // 定义速度类型枚举
            #define VELOCITY_TYPE_DIRECT 0
            #define VELOCITY_TYPE_EXPLORE 1
            
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
            sampler2D _BrushTex;
            sampler2D _ExploreTex;
            StructuredBuffer<float2> _Positions;
            StructuredBuffer<float2> _Velocities;
            StructuredBuffer<float> _VelocityRadii;
            StructuredBuffer<int> _VelocityTypes; // 新增速度类型缓冲区
            int _PointCount;
            float _BrushSize;
            
            // 计算直接速度
            float4 CalculateDirectVelocity(float2 velocity)
            {
                return float4(0.5 + velocity.x * 0.05, 0.5 + velocity.y * 0.05, 0.5, 0.5);
            }

            // 柏林噪声辅助函数 - 平滑插值
            float fade(float t) {
                return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
            }
            
            // 梯度噪声函数
            float grad(float hash, float x) {
                float h = fmod(hash, 2.0);
                return (h < 1.0) ? x : -x;
            }
            
            // 1D柏林噪声
            float perlinNoise1D(float x) {
                float i = floor(x);
                float f = frac(x);
                
                // 生成伪随机梯度
                float a = grad(sin(i * 12.9898) * 43758.5453, f);
                float b = grad(sin((i + 1.0) * 12.9898) * 43758.5453, f - 1.0);
                
                // 平滑插值
                float u = fade(f);
                return lerp(a, b, u);
            }
            
            // 分形噪声 - 多个八度的柏林噪声叠加
            // 修改分形噪声函数，添加种子参数
            float fractalNoise(float x, float seed) {
                float value = 0.0;
                float amplitude = 1.0;
                float frequency = 1.0;
                
                // 使用种子偏移噪声
                x += seed * 123.456;
                
                // 3个八度
                for (int i = 0; i < 3; i++) {
                    value += amplitude * perlinNoise1D(x * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                return value;
            }

            float ExpoleAlphaMask(float2 brushUV, float seed)
            {
                // 以(0.5, 0.5)为中心计算极坐标
                float2 center = float2(0.5, 0.5);
                float2 offset = brushUV - center;
                
                // 计算角度 [0, 2π]
                float angle = atan2(offset.y, offset.x) + 3.14159; // 转换到 [0, 2π] 范围
                float radius = length(offset);
                
                // 基于角度的噪声 - 使用多个频率，每个频率使用不同的种子偏移
                float angleFreq1 = 2.0;  // 主要频率 - 控制主要的"射线"数量
                float angleFreq2 = 3.0;  // 细节频率 - 增加细节变化
                
                float noise1 = fractalNoise(angle * angleFreq1, seed);
                float noise2 = fractalNoise(angle * angleFreq2, seed * 1.7) * 0.3; // 使用不同的种子偏移
                float combinedNoise = noise1 + noise2;
                
                // 径向衰减 - 中心区域保持完整，边缘更容易被遮罩
                float radialFactor = smoothstep(0.1, 0.5, radius);
                
                // 噪声阈值 - 控制被遮罩的区域大小
                float threshold = 0.2; // 调整这个值来控制遮罩强度
                
                // 当噪声值低于阈值时，返回Alpha遮罩值
                float mask = step(threshold, combinedNoise + radialFactor * 0.3);
                
                // 返回需要从原Alpha中减去的值
                // mask为0时减去1.0（完全遮罩），mask为1时减去0.0（不遮罩）
                return (1.0 - mask) * smoothstep(0.05, 0.4, radius); // 中心小圆圈不受影响
            }
            
            // 简单的伪随机噪声函数
            float random(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // 计算爆炸速度
            float4 CalculateExploreVelocity(float2 brushUV, float2 velocity, float4 brushColor)
            {
                // 基础速度颜色
                float4 velocityColor = float4(0.5 + velocity.x * 0.05, 0.5 + velocity.y * 0.05, 0.5, 0.5);
                velocityColor += float4(brushColor.r - 0.5, brushColor.g - 0.5, brushColor.b * 3, 0);
                return velocityColor;
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                
                for (int idx = 0; idx < _PointCount; idx++)
                {
                    float2 brushPos = _Positions[idx];
                    float radius = _VelocityRadii[idx];
                    int velocityType = _VelocityTypes[idx];
                    
                    if (radius <= 0.0001) continue;
                    
                    float2 brushDistance = i.uv - brushPos;
                    float2 textureScale = float2(1024.0/1280.0, 1024.0/720.0);
                    //float2 textureScale = float2(1024.0/1024.0, 1024.0/1024.0);
                    float2 brushUV = brushDistance * (1.0 / (_BrushSize * radius * 0.01 * textureScale)) + 0.5;
                    
                    if (brushUV.x >= 0.0 && brushUV.x <= 1.0 && brushUV.y >= 0.0 && brushUV.y <= 1.0)
                    {
                        float4 velocityColor = tex2D(_MainTex, i.uv);
                        float4 brushColor;
                        // 根据速度类型选择不同的计算方式
                        if (velocityType == VELOCITY_TYPE_DIRECT)
                        {
                            brushColor = tex2D(_BrushTex, brushUV);
                            if (brushColor.a > 0.0)
                            {
                                float2 velocity = _Velocities[idx];
                                velocityColor = CalculateDirectVelocity(velocity);
                            }
                        }
                        else // VELOCITY_TYPE_EXPLORE
                        {
                            float seed = brushPos.x * 1000.0 + brushPos.y * 2000.0;
                            brushColor = tex2D(_ExploreTex, brushUV);
                            brushColor.a *= ExpoleAlphaMask(brushUV, seed);
                            if (brushColor.a > 0.01)
                            {
                                float2 velocity = _Velocities[idx];
                                velocityColor = CalculateExploreVelocity(brushUV, velocity, brushColor);
                            }
                        }
                            
                        col.rg = lerp(col.rg, velocityColor.rg, brushColor.a); 
                        // col = float4(ExpoleAlphaMask(brushUV), 0, 0, 1);
                    }
                }
                
                return col;
            }
            ENDCG
        }
    }
}