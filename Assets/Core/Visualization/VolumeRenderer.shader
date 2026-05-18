// Single-pass front-to-back ray-marching volume renderer for URP.
// Renders the inside of a unit cube (Cull Front) as a translucent volume sampled
// from a Texture3D. Each fragment walks N steps from camera→cube-exit, accumulates
// color * alpha based on the intensity threshold + density scale uniforms.
//
// Quality knobs:
//   _StepCount      higher = smoother density gradients, higher GPU cost
//   _ThresholdLow   intensities below this contribute nothing (cull noise / air)
//   _ThresholdHigh  intensities above this saturate to full opacity (highlight bone / contrast)
//   _DensityScale   global multiplier on per-sample alpha (compensates for slice spacing)
//   _Tint           RGB tint applied to all visible samples
//
// Quest 3 budget: ~10 ms target for 128 steps at 2048×2048 eye render. Reduce
// _StepCount to 64 for headset; 128 is fine for flat-mode preview.

Shader "Host/Visualization/Volume"
{
    Properties
    {
        _Volume("Volume", 3D) = "" {}
        _StepCount("Step Count", Range(16, 512)) = 128
        _ThresholdLow("Threshold Low", Range(0, 1)) = 0.05
        _ThresholdHigh("Threshold High", Range(0, 1)) = 1.0
        _DensityScale("Density Scale", Range(0, 8)) = 1.5
        _Tint("Tint", Color) = (1, 0.95, 0.9, 1)
        _SegmentEnabled("Segment Enabled", Float) = 0
        _SegmentLow("Segment Low", Range(0, 1)) = 0.4
        _SegmentHigh("Segment High", Range(0, 1)) = 0.6
        _SegmentColor("Segment Color", Color) = (1, 0.4, 0.3, 1)
        _SegmentBoost("Segment Boost", Range(0, 5)) = 2.5
        _Mask("Mask", 3D) = "" {}
        _MaskColor("Mask Color", Color) = (0.3, 0.8, 1.0, 1.0)
        _ShowVolume("Show Volume", Float) = 1
        _ShowSegmentation("Show Segmentation", Float) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE3D(_Volume);
            SAMPLER(sampler_Volume);
            TEXTURE3D(_Mask);
            SAMPLER(sampler_Mask);
            float4 _MaskColor;
            int _StepCount;
            float _ThresholdLow;
            float _ThresholdHigh;
            float _DensityScale;
            float4 _Tint;
            float _SegmentEnabled;
            float _SegmentLow;
            float _SegmentHigh;
            float4 _SegmentColor;
            float _SegmentBoost;
            // Per-segment colour palette indexed by the byte value in _Mask.
            // Index 0 is reserved for "empty"; indices 1..31 hold actual segment
            // colours assigned by the workstation. An entry of (0,0,0,0) means
            // "use the legacy _MaskColor instead".
            float4 _SegmentPalette[32];
            // 1 = render the underlying volume intensity (threshold/density/tint).
            // 0 = skip it. Same idea for the segmentation mask overlay. Workstation
            // toggles these so the user can pick volume-only, segmentation-only, or both.
            float _ShowVolume;
            float _ShowSegmentation;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            // Intersect a ray with the unit cube centred at origin extending [-0.5, 0.5].
            // Returns (tNear, tFar). Used to derive a robust march length.
            float2 RayCubeIntersect(float3 rayOrigin, float3 rayDir)
            {
                float3 invDir = 1.0 / rayDir;
                float3 t1 = (-0.5 - rayOrigin) * invDir;
                float3 t2 = ( 0.5 - rayOrigin) * invDir;
                float3 tMin = min(t1, t2);
                float3 tMax = max(t1, t2);
                float tNear = max(max(tMin.x, tMin.y), tMin.z);
                float tFar  = min(min(tMax.x, tMax.y), tMax.z);
                return float2(tNear, tFar);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Camera position in object space.
                float3 camOS = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 rayDirOS = normalize(IN.positionOS - camOS);

                float2 t = RayCubeIntersect(camOS, rayDirOS);
                float tEntry = max(0.0, t.x);
                float tExit  = t.y;
                if (tExit <= tEntry) return half4(0,0,0,0);

                float marchLen = tExit - tEntry;
                int steps = max(8, _StepCount);
                float stepLen = marchLen / steps;
                float3 step = rayDirOS * stepLen;
                float3 samplePos = camOS + rayDirOS * tEntry;

                // Texture3D coords are [0,1] over [-0.5, 0.5] object space.
                float4 accum = float4(0, 0, 0, 0);

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float3 uvw = samplePos + 0.5;
                    float intensity = SAMPLE_TEXTURE3D(_Volume, sampler_Volume, uvw).r;
                    float3 color = float3(0, 0, 0);
                    float  alpha = 0.0;
                    bool   contributes = false;

                    // ── Volume intensity contribution (optional) ────────────
                    if (_ShowVolume > 0.5)
                    {
                        float window = max(0.001, _ThresholdHigh - _ThresholdLow);
                        float normalized = saturate((intensity - _ThresholdLow) / window);
                        if (normalized > 0)
                        {
                            alpha = saturate(normalized * _DensityScale * stepLen);
                            color = _Tint.rgb * normalized;
                            // Threshold-band highlight (active segment).
                            if (_SegmentEnabled > 0.5 &&
                                intensity >= _SegmentLow && intensity <= _SegmentHigh)
                            {
                                color = lerp(color, _SegmentColor.rgb, _SegmentColor.a);
                                alpha = saturate(alpha * _SegmentBoost);
                            }
                            contributes = true;
                        }
                    }

                    // ── Segmentation mask overlay (optional) ────────────────
                    if (_ShowSegmentation > 0.5)
                    {
                        float maskV = SAMPLE_TEXTURE3D(_Mask, sampler_Mask, uvw).r;
                        if (maskV > 0.001)
                        {
                            int idx = (int)(maskV * 255.0 + 0.5);
                            idx = clamp(idx, 0, 31);
                            float4 segCol = _SegmentPalette[idx];
                            float anyRgb = max(segCol.r, max(segCol.g, segCol.b));
                            if (anyRgb > 0.0)
                            {
                                if (segCol.a > 0.001)
                                {
                                    color = lerp(color, segCol.rgb, segCol.a);
                                    alpha = saturate(alpha + segCol.a * 0.5);
                                    contributes = true;
                                }
                            }
                            else
                            {
                                color = lerp(color, _MaskColor.rgb, _MaskColor.a * maskV);
                                alpha = saturate(alpha + maskV * 0.5);
                                contributes = true;
                            }
                        }
                    }

                    if (contributes)
                    {
                        accum.rgb += (1.0 - accum.a) * color * alpha;
                        accum.a   += (1.0 - accum.a) * alpha;
                        if (accum.a > 0.99) break;
                    }

                    samplePos += step;
                }

                return half4(accum.rgb, accum.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
