Shader "Custom/DownSample"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DownSampleFactor ("DownSample Factor", Float) = 10.0
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
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _DownSampleFactor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 方法1: 简单采样 - 直接采样对应位置
                // return tex2D(_MainTex, i.uv);
                
                // 方法2: 多重采样平均 - 更好的质量
                float2 texelSize = _MainTex_TexelSize.xy * _DownSampleFactor;
                
                fixed4 color = fixed4(0, 0, 0, 0);
                
                // 在一个区域内进行多次采样并平均
                for(int x = 0; x < 3; x++)
                {
                    for(int y = 0; y < 3; y++)
                    {
                        float2 offset = float2(x - 1, y - 1) * texelSize * 0.5;
                        color += tex2D(_MainTex, i.uv + offset);
                    }
                }
                
                return color / 9.0; // 平均9个采样点
            }
            ENDCG
        }
    }
}