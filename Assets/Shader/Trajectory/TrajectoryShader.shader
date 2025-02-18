Shader "Custom/TrajectoryLine"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _LineWidth ("Line Width", Range(0.001, 0.1)) = 0.02
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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

            float4 _Color;
            float _LineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 線の中心からの距離を計算
                float2 center = float2(0.5, 0.5);
                float dist = length(i.uv - center);
                
                // スムーズなアンチエイリアス処理付きの線を描画
                float alpha = 1.0 - smoothstep(_LineWidth - 0.01, _LineWidth, dist);
                
                return float4(_Color.rgb, _Color.a * alpha);
            }
            ENDCG
        }
    }
}