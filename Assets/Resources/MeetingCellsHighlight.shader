Shader "SPARC/Interaction Highlight"
{
    Properties { _OutlineColor("Highlight",Color)=(0.98,0.88,0.50,1) _Width("Pixel width",Float)=3 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Name "InteractionOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor; float _Width;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                float3 normalVS=TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS),true);
                float2 direction=normalVS.xy/max(length(normalVS.xy),0.001);
                output.positionCS.xy+=direction*(_Width*2/_ScreenParams.xy)*output.positionCS.w;
                return output;
            }
            half4 Frag(Varyings input):SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
