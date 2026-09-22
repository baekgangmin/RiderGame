Shader "Custom/VertexColorUnlit"
{
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 프로젝트가 Linear 컬러 스페이스라 정점 색(의도한 sRGB 값)을 그대로 출력하면
                // 최종 화면에 더 밝게(색 바랜 듯) 보임 - sRGB -> Linear로 변환해서 의도한 색 그대로 보이게 함
                half3 linearColor = pow(IN.color.rgb, 2.2);
                return half4(linearColor, IN.color.a);
            }
            ENDHLSL
        }
    }
}
