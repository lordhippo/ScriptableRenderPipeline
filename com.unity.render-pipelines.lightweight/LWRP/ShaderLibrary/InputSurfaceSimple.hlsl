#ifndef LIGHTWEIGHT_INPUT_SURFACE_SIMPLE_INCLUDED
#define LIGHTWEIGHT_INPUT_SURFACE_SIMPLE_INCLUDED

#include "Core.hlsl"
#include "InputSurfaceCommon.hlsl"
#if defined(UNITY_COLORSPACE_GAMMA)
#include "CoreRP/ShaderLibrary/Color.hlsl"
#endif

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
half4 _Color;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Shininess;
CBUFFER_END

TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);

half4 SampleSpecularGloss(half2 uv, half alpha, half4 specColor, TEXTURE2D_ARGS(specGlossMap, sampler_specGlossMap))
{
    half4 specularGloss = half4(0.0h, 0.0h, 0.0h, 1.0h);
#ifdef _SPECGLOSSMAP
    specularGloss = SAMPLE_TEXTURE2D_COLOR(specGlossMap, sampler_specGlossMap, uv);
#elif defined(_SPECULAR_COLOR)
    #if defined(UNITY_COLORSPACE_GAMMA)
        specularGloss = FastSRGBToLinear(specColor);
    #else
        specularGloss = specColor;
    #endif
#endif

#ifdef _GLOSSINESS_FROM_BASE_ALPHA
    specularGloss.a = alpha;
#endif
    return specularGloss;
}

#endif // LIGHTWEIGHT_INPUT_SURFACE_SIMPLE_INCLUDED
