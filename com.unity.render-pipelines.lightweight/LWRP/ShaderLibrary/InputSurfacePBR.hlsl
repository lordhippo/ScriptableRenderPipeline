#ifndef LIGHTWEIGHT_INPUT_SURFACE_PBR_INCLUDED
#define LIGHTWEIGHT_INPUT_SURFACE_PBR_INCLUDED

#include "Core.hlsl"
#include "CoreRP/ShaderLibrary/CommonMaterial.hlsl"
#include "InputSurfaceCommon.hlsl"
#if defined(UNITY_COLORSPACE_GAMMA)
#include "CoreRP/ShaderLibrary/Color.hlsl"
#endif

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;

#ifdef _DIFFUSEMODEL_CLOTH
half _ClothRimScale;
half _ClothRimExp;
half _ClothInnerScale;
half _ClothInnerExp;
half _ClothLambertScale;
#elif _DIFFUSEMODEL_SKIN
half _SkinCurvature;
#endif

half4 _Color;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Glossiness;
half _GlossMapScale;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
CBUFFER_END

TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_SkinCurvatureMap);   SAMPLER(sampler_SkinCurvatureMap);

#ifdef _SPECULAR_SETUP
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D_COLOR(_SpecGlossMap, sampler_SpecGlossMap, uv)
#else
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D_COLOR(_MetallicGlossMap, sampler_MetallicGlossMap, uv)
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;

#ifdef _METALLICSPECGLOSSMAP
    specGloss = SAMPLE_METALLICSPECULAR(uv);
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _GlossMapScale;
    #else
        specGloss.a *= _GlossMapScale;
    #endif
#else // _METALLICSPECGLOSSMAP
    #if _SPECULAR_SETUP
        #if defined(UNITY_COLORSPACE_GAMMA)
            specGloss.rgb = FastSRGBToLinear(_SpecColor.rgb);
        #else
            specGloss.rgb = _SpecColor.rgb;
        #endif
    #else
        #if defined(UNITY_COLORSPACE_GAMMA)
            specGloss.rgb = Gamma22ToLinear(_Metallic).rrr;
        #else
            specGloss.rgb = _Metallic.rrr;
        #endif
    #endif

    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _GlossMapScale;
    #else
        specGloss.a = _Glossiness;
    #endif
#endif

    return specGloss;
}

half SampleOcclusion(float2 uv)
{
#ifdef _OCCLUSIONMAP
// TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
#if defined(SHADER_API_GLES)
    return SAMPLE_TEXTURE2D_COLOR(_OcclusionMap, sampler_OcclusionMap, uv).g;
#else
    half occ = SAMPLE_TEXTURE2D_COLOR(_OcclusionMap, sampler_OcclusionMap, uv).g;
    return LerpWhiteTo(occ, _OcclusionStrength);
#endif
#else
    return 1.0;
#endif
}

half SampleCurvature(float2 uv)
{
#if _DIFFUSEMODEL_SKIN
#ifdef _CURVATUREMAP
    return SAMPLE_TEXTURE2D_COLOR(_SkinCurvatureMap, sampler_SkinCurvatureMap, uv).g * _SkinCurvature;
#else
    return _SkinCurvature;
#endif
#else
    return 0;
#endif
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    _Color = FastSRGBToLinear(_Color);
#endif

    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_PARAM(_MainTex, sampler_MainTex));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _Color, _Cutoff);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _Color.rgb;

#if _SPECULAR_SETUP
    outSurfaceData.metallic = 1.0h;
    outSurfaceData.specular = specGloss.rgb;
#else
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
#endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_PARAM(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_PARAM(_EmissionMap, sampler_EmissionMap));

    outSurfaceData.curvature = SampleCurvature(uv);
}

#endif // LIGHTWEIGHT_INPUT_SURFACE_PBR_INCLUDED
