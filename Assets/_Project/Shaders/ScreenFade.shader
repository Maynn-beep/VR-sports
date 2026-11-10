// Полноэкранное затемнение для VR.
// Квадрат висит прямо перед камерой; ZTest Always — рисуется поверх всего.
// Макросы UNITY_VERTEX_OUTPUT_STEREO нужны для режима Single Pass Instanced
// (оба глаза рисуются за один проход — так работает рендер на Quest).
Shader "VRSports/ScreenFade"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Overlay+100" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
