Shader "AsciiArena/TerminalGlyph"
{
    Properties { _MainTex ("ASCII SDF atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            struct Input {float4 position:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Output {float4 position:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            Output Vert(Input i)
            {
                Output o;o.position=TransformObjectToHClip(i.position.xyz);o.uv=i.uv;o.color=i.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                    o.color.rgb=SRGBToLinear(o.color.rgb);
                #endif
                return o;
            }
            half4 Frag(Output i):SV_Target
            {
                if(i.uv.x<0)return i.color;
                half distance=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
                half edge=max(fwidth(distance)*.6h,.001h);
                return half4(i.color.rgb,i.color.a*smoothstep(.5h-edge,.5h+edge,distance));
            }
            ENDHLSL
        }
    }
}
