Shader "AsciiArena/StudyCRT"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline"}
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _StudyVariant, _StudyCrtBypass;
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=i.texcoord;
                if(_StudyCrtBypass>.5)return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv,0);
                float2 p=uv*2-1;float bend=lerp(.012,.006,_StudyVariant);
                uv=(p*(1+bend*dot(p,p)))*.5+.5;
                if(any(uv<0)||any(uv>1))return half4(0,0,0,1);
                half3 color=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv,0).rgb;
                float scan=.5+.5*cos((i.positionCS.y-.5)*3.14159265);
                color*=1-scan*lerp(.17,.10,_StudyVariant);
                color*=1-.3*smoothstep(.3,1.8,dot(p,p));
                return half4(color*1.06,1);
            }
            ENDHLSL
        }
    }
}
