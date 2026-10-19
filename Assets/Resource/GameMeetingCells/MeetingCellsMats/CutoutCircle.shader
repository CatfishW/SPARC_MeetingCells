Shader "Custom/CircleCutoutTransition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _CutoutSize ("Cutout Size", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
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
            float _CutoutSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 center = float2(0.5, 0.5);
                float dist = distance(i.uv, center);

                if (dist < _CutoutSize)
                {
                    return fixed4(0, 0, 0, 0); // Transparent
                }
                else
                {
                    return fixed4(0, 0, 0, 1); // Black background
                }
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
