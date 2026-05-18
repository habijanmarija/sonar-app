// Two-sided lit shader for marching-cubes segment meshes.
// URP/Lit can't shade both sides correctly when culling is off — back faces
// get wrong-normal lighting and look washed/dim. URP/Unlit avoids that but
// also drops all depth shading, so vertebrae look like flat coloured blobs.
//
// This shader splits the difference: render both sides (Cull Off), and in
// the fragment stage flip the world-normal for back-facing fragments so
// each side gets its correct N·L lighting. Ambient term keeps the
// dark side visible. Output is a diffuse shade tinted by _BaseColor.
//
// Transparency: Queue=Transparent + SrcAlpha OneMinusSrcAlpha lets the
// segmentation panel's per-segment + global opacity sliders dim the
// vertebra meshes. ZWrite stays on so closer meshes occlude farther ones,
// which is fine because anatomical segments rarely overlap spatially.
Shader "Host/Visualization/SegmentMeshLit"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Ambient("Ambient", Range(0, 1)) = 0.45
        _Wrap("Light Wrap", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType"   = "Transparent"
            "Queue"        = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Cull  Off
            ZTest LEqual
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Ambient;
                float  _Wrap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = pos.positionCS;
                OUT.normalWS = nrm.normalWS;
                return OUT;
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                if (!IS_FRONT_VFACE(isFrontFace, true, false)) n = -n;

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(n, mainLight.direction) * (1.0 - _Wrap) + _Wrap);
                float shade = ndotl * (1.0 - _Ambient) + _Ambient;
                float3 col = _BaseColor.rgb * mainLight.color * shade;
                return half4(col, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
