#ifndef LIGHTWEIGHT_LIGHTING_INCLUDED
#define LIGHTWEIGHT_LIGHTING_INCLUDED

#include "CoreRP/ShaderLibrary/Common.hlsl"
#include "CoreRP/ShaderLibrary/EntityLighting.hlsl"
#include "CoreRP/ShaderLibrary/ImageBasedLighting.hlsl"
#include "Core.hlsl"
#include "Shadows.hlsl"
#if defined(UNITY_COLORSPACE_GAMMA)
#include "CoreRP/ShaderLibrary/Color.hlsl"
#endif

// If lightmap is not defined than we evaluate GI (ambient + probes) from SH
// We might do it fully or partially in vertex to save shader ALU
#if !defined(LIGHTMAP_ON)
// TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
    #if defined(SHADER_API_GLES) || !defined(_NORMALMAP)
        // Evaluates SH fully in vertex
        #define EVALUATE_SH_VERTEX
    #elif !SHADER_HINT_NICE_QUALITY
        // Evaluates L2 SH in vertex and L0L1 in pixel
        #define EVALUATE_SH_MIXED
    #endif
        // Otherwise evaluate SH fully per-pixel
#endif


#ifdef LIGHTMAP_ON
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) float2 lmName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT) OUT.xy = lightmapUV.xy * lightmapScaleOffset.xy + lightmapScaleOffset.zw;
    #define OUTPUT_SH(normalWS, OUT)
#else
    #define DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) half3 shName : TEXCOORD##index
    #define OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT)
    #define OUTPUT_SH(normalWS, OUT) OUT.xyz = SampleSHVertex(normalWS)
#endif

///////////////////////////////////////////////////////////////////////////////
//                          Light Helpers                                    //
///////////////////////////////////////////////////////////////////////////////

// Abstraction over Light input constants
struct LightInput
{
    float4  position;
    half3   color;
    half4   distanceAttenuation;
    half4   spotDirection;
    half4   spotAttenuation;
};

// Abstraction over Light shading data.
struct Light
{
    int     index;
    half3   direction;
    half3   color;
    half    attenuation;
    half    subtractiveModeAttenuation;
};

///////////////////////////////////////////////////////////////////////////////
//                        Attenuation Functions                               /
///////////////////////////////////////////////////////////////////////////////
half CookieAttenuation(float3 worldPos)
{
#ifdef _MAIN_LIGHT_COOKIE
#ifdef _MAIN_LIGHT_DIRECTIONAL
    float2 cookieUV = mul(_WorldToLight, float4(worldPos, 1.0)).xy;
    return SAMPLE_TEXTURE2D(_MainLightCookie, sampler_MainLightCookie, cookieUV).a;
#elif defined(_MAIN_LIGHT_SPOT)
    float4 projPos = mul(_WorldToLight, float4(worldPos, 1.0));
    float2 cookieUV = projPos.xy / projPos.w + 0.5;
    return SAMPLE_TEXTURE2D(_MainLightCookie, sampler_MainLightCookie, cookieUV).a;
#endif // POINT LIGHT cookie not supported
#endif

    return 1;
}

// Matches Unity Vanila attenuation
// Attenuation smoothly decreases to light range.
half DistanceAttenuation(half distanceSqr, half3 distanceAttenuation)
{
    // We use a shared distance attenuation for additional directional and puctual lights
    // for directional lights attenuation will be 1
    half quadFalloff = distanceAttenuation.x;
    half denom = distanceSqr * quadFalloff + 1.0h;
    half lightAtten = 1.0h / denom;

    // We need to smoothly fade attenuation to light range. We start fading linearly at 80% of light range
    // Therefore:
    // fadeDistance = (0.8 * 0.8 * lightRangeSq)
    // smoothFactor = (lightRangeSqr - distanceSqr) / (lightRangeSqr - fadeDistance)
    // We can rewrite that to fit a MAD by doing
    // distanceSqr * (1.0 / (fadeDistanceSqr - lightRangeSqr)) + (-lightRangeSqr / (fadeDistanceSqr - lightRangeSqr)
    // distanceSqr *        distanceAttenuation.y            +             distanceAttenuation.z
    half smoothFactor = saturate(distanceSqr * distanceAttenuation.y + distanceAttenuation.z);
    return lightAtten * smoothFactor;
}

half SpotAttenuation(half3 spotDirection, half3 lightDirection, half4 spotAttenuation)
{
    // Spot Attenuation with a linear falloff can be defined as
    // (SdotL - cosOuterAngle) / (cosInnerAngle - cosOuterAngle)
    // This can be rewritten as
    // invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle)
    // SdotL * invAngleRange + (-cosOuterAngle * invAngleRange)
    // SdotL * spotAttenuation.x + spotAttenuation.y

    // If we precompute the terms in a MAD instruction
    half SdotL = dot(spotDirection, lightDirection);
    half atten = saturate(SdotL * spotAttenuation.x + spotAttenuation.y);
    return atten * atten;
}

half4 GetLightDirectionAndAttenuation(LightInput lightInput, float3 positionWS)
{
    half4 directionAndAttenuation;
    float3 posToLightVec = lightInput.position.xyz - positionWS * lightInput.position.w;
    float distanceSqr = max(dot(posToLightVec, posToLightVec), FLT_MIN);

    directionAndAttenuation.xyz = half3(posToLightVec * rsqrt(distanceSqr));
    directionAndAttenuation.w = DistanceAttenuation(distanceSqr, lightInput.distanceAttenuation.xyz);
    directionAndAttenuation.w *= SpotAttenuation(lightInput.spotDirection.xyz, directionAndAttenuation.xyz, lightInput.spotAttenuation);
    return directionAndAttenuation;
}

half4 GetMainLightDirectionAndAttenuation(LightInput lightInput, float3 positionWS)
{
    half4 directionAndAttenuation = GetLightDirectionAndAttenuation(lightInput, positionWS);

    // Cookies disabled for now due to amount of shader variants
    //directionAndAttenuation.w *= CookieAttenuation(positionWS);

    return directionAndAttenuation;
}

///////////////////////////////////////////////////////////////////////////////
//                      Light Abstraction                                    //
///////////////////////////////////////////////////////////////////////////////

Light GetMainLight()
{
    Light light;
    light.index = 0;
    light.direction = _MainLightPosition.xyz;
    light.attenuation = 1.0;
    light.subtractiveModeAttenuation = _MainLightPosition.w;
#if defined(UNITY_COLORSPACE_GAMMA)
    light.color = FastSRGBToLinear(_MainLightColor.rgb);
#else
    light.color = _MainLightColor.rgb;
#endif

    return light;
}

Light GetLight(half i, float3 positionWS)
{
    LightInput lightInput;

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    int lightIndex = _LightIndexBuffer[unity_LightIndicesOffsetAndCount.x + i];
#else
    // The following code is more optimal than indexing unity_4LightIndices0.
    // Conditional moves are branch free even on mali-400
    half i_rem = (i < 2.0h) ? i : i - 2.0h;
    half2 lightIndex2 = (i < 2.0h) ? unity_4LightIndices0.xy : unity_4LightIndices0.zw;
    int lightIndex = (i_rem < 1.0h) ? lightIndex2.x : lightIndex2.y;
#endif

    // The following code will turn into a branching madhouse on platforms that don't support
    // dynamic indexing. Ideally we need to configure light data at a cluster of
    // objects granularity level. We will only be able to do that when scriptable culling kicks in.
    // TODO: Use StructuredBuffer on PC/Console and profile access speed on mobile that support it.
    lightInput.position = _AdditionalLightPosition[lightIndex];
#if defined(UNITY_COLORSPACE_GAMMA)
    lightInput.color = FastSRGBToLinear(_AdditionalLightColor[lightIndex].rgb);
#else
    lightInput.color = _AdditionalLightColor[lightIndex].rgb;
#endif
    lightInput.distanceAttenuation = _AdditionalLightDistanceAttenuation[lightIndex];
    lightInput.spotDirection = _AdditionalLightSpotDir[lightIndex];
    lightInput.spotAttenuation = _AdditionalLightSpotAttenuation[lightIndex];

    half4 directionAndRealtimeAttenuation = GetLightDirectionAndAttenuation(lightInput, positionWS);

    Light light;
    light.index = lightIndex;
    light.direction = directionAndRealtimeAttenuation.xyz;
    light.attenuation = directionAndRealtimeAttenuation.w;
    light.subtractiveModeAttenuation = lightInput.distanceAttenuation.w;
    light.color = lightInput.color;

    return light;
}

half GetPixelLightCount()
{
    // TODO: we need to expose in SRP api an ability for the pipeline cap the amount of lights
    // in the culling. This way we could do the loop branch with an uniform
    // This would be helpful to support baking exceeding lights in SH as well
    return min(_AdditionalLightCount.x, unity_LightIndicesOffsetAndCount.y);
}

///////////////////////////////////////////////////////////////////////////////
//                         BRDF Functions                                    //
///////////////////////////////////////////////////////////////////////////////

#define kDieletricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)

struct BRDFData
{
    half3 diffuse;
    half3 specular;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half grazingTerm;
    half occlusion;
    half curvature;

    // We save some light invariant BRDF terms so we don't have to recompute
    // them in the light loop. Take a look at DirectBRDF function for detailed explaination.
    half normalizationTerm;     // roughness * 4.0 + 2.0
    half roughness2MinusOne;    // roughness² - 1.0
};

half ReflectivitySpecular(half3 specular)
{
#if defined(SHADER_API_GLES)
    return specular.r; // Red channel - because most metals are either monocrhome or with redish/yellowish tint
#else
    return max(max(specular.r, specular.g), specular.b);
#endif
}

half OneMinusReflectivityMetallic(half metallic)
{
    // We'll need oneMinusReflectivity, so
    //   1-reflectivity = 1-lerp(dielectricSpec, 1, metallic) = lerp(1-dielectricSpec, 0, metallic)
    // store (1-dielectricSpec) in kDieletricSpec.a, then
    //   1-reflectivity = lerp(alpha, 0, metallic) = alpha + metallic*(0 - alpha) =
    //                  = alpha - metallic * alpha
    half oneMinusDielectricSpec = kDieletricSpec.a;
    return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

inline void InitializeBRDFData(half3 albedo, half metallic, half3 specular, half smoothness, half alpha, half occlusion, half curvature, out BRDFData outBRDFData)
{
#ifdef _SPECULAR_SETUP
    half reflectivity = ReflectivitySpecular(specular);
    half oneMinusReflectivity = 1.0 - reflectivity;

    outBRDFData.diffuse = albedo * (half3(1.0h, 1.0h, 1.0h) - specular);
    outBRDFData.specular = specular;
#else

    half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
    half reflectivity = 1.0 - oneMinusReflectivity;

    outBRDFData.diffuse = albedo * oneMinusReflectivity;
    outBRDFData.specular = lerp(kDieletricSpec.rgb, albedo, metallic);
#endif

    outBRDFData.grazingTerm = saturate(smoothness + reflectivity);
    outBRDFData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    outBRDFData.roughness = PerceptualRoughnessToRoughness(outBRDFData.perceptualRoughness);
    outBRDFData.roughness2 = outBRDFData.roughness * outBRDFData.roughness;

    outBRDFData.normalizationTerm = outBRDFData.roughness * 4.0h + 2.0h;
    outBRDFData.roughness2MinusOne = outBRDFData.roughness2 - 1.0h;

#ifdef _ALPHAPREMULTIPLY_ON
    outBRDFData.diffuse *= alpha;
    alpha = alpha * oneMinusReflectivity + reflectivity;
#endif

    outBRDFData.occlusion = occlusion;
    outBRDFData.curvature = curvature;
}

#if _DIFFUSEMODEL_SKIN
half3 SkinTerm(half NdotL, half curvature)
{
    NdotL = mad(NdotL, 0.5, 0.5); // map to 0 to 1 range
    float curva = (1.0/mad(curvature, 0.5 - 0.0625, 0.0625) - 2.0) / (16.0 - 2.0); // curvature is within [0, 1] remap to normalized r from 2 to 16
    float oneMinusCurva = 1.0 - curva;
    float3 curve0;
    {
        float3 rangeMin = float3(0.0, 0.3, 0.3);
        float3 rangeMax = float3(1.0, 0.7, 0.7);
        float3 offset = float3(0.0, 0.06, 0.06);
        float3 t = saturate( mad(NdotL, 1.0 / (rangeMax - rangeMin), (offset + rangeMin) / (rangeMin - rangeMax)  ) );
        float3 lowerLine = (t * t) * float3(0.65, 0.5, 0.9);
        lowerLine.r += 0.045;
        lowerLine.b *= t.b;
        float3 m = float3(1.75, 2.0, 1.97);
        float3 upperLine = mad(NdotL, m, float3(0.99, 0.99, 0.99) -m );
        upperLine = saturate(upperLine);
        float3 lerpMin = float3(0.0, 0.35, 0.35);
        float3 lerpMax = float3(1.0, 0.7 , 0.6 );
        float3 lerpT = saturate( mad(NdotL, 1.0/(lerpMax-lerpMin), lerpMin/ (lerpMin - lerpMax) ));
        curve0 = lerp(lowerLine, upperLine, lerpT * lerpT);
    }
    float3 curve1;
    {
        float3 m = float3(1.95, 2.0, 2.0);
        float3 upperLine = mad( NdotL, m, float3(0.99, 0.99, 1.0) - m);
        curve1 = saturate(upperLine);
    }
    float oneMinusCurva2 = oneMinusCurva * oneMinusCurva;
    return lerp(curve0, curve1, mad(oneMinusCurva2, -1.0 * oneMinusCurva2, 1.0) );
}

half3 SkinTerm(half3 normalWS, half3 lightDirectionWS, half curvature, half occlusion)
{
    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    return SkinTerm(NdotL * occlusion, curvature);
}

half3 SkinTermIndirect(half3 indirect, half curvature)
{
    half3 lumaVec = half3(0.299, 0.587, 0.114);

    half shL0 = SHEvalLinearL0(unity_SHAr, unity_SHAg, unity_SHAb);
    half indirectLuma = dot (lumaVec, indirect) / dot(lumaVec, shL0);

    half3 skinTerm = SkinTerm(indirectLuma, curvature);
    half skinLuma = dot (lumaVec, skinTerm);
    return indirect * skinTerm / skinLuma;

    /*float curva = (1.0/mad(curvature, 0.5 - 0.0625, 0.0625) - 2.0) / (16.0 - 2.0); // curvature is within [0, 1] remap to r distance 2 to 16
    float oneMinusCurva = 1.0 - curva;

    half zh0;
    // ZH0
    {
        float2 remappedCurva = 1.0 - saturate(curva * float2(3.0, 2.7) );
        remappedCurva *= remappedCurva;
        remappedCurva *= remappedCurva;
        float3 multiplier = float3(1.0/mad(curva, 3.2, 0.4), remappedCurva.x, remappedCurva.y);
        zh0 = mad(multiplier, float3( 0.061659, 0.00991683, 0.003783), float3(0.868938, 0.885506, 0.885400));
    }
    half zh1;
    // ZH1
    {
        float remappedCurva = 1.0 - saturate(curva * 2.7);
        float3 lowerLine = mad(float3(0.197573092, 0.0117447875, 0.0040980375), (1.0f - remappedCurva * remappedCurva * remappedCurva), float3(0.7672169, 1.009236, 1.017741));
        float3 upperLine = float3(1.018366, 1.022107, 1.022232);
        zh1 = lerp(upperLine, lowerLine, oneMinusCurva * oneMinusCurva);
    }

    //return indirect;
    return indirect * (zh0 + zh1) * 0.5;*/
}

#endif

#ifdef _DIFFUSEMODEL_CLOTH
half3 ClothTerm(half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS, half occlusion)
{
    half vDotN = saturate(dot(normalWS, viewDirectionWS));
    float NdotL = saturate(dot(normalWS, lightDirectionWS));

    half rim = _ClothRimExp * _ClothRimScale * pow(1.f - vDotN, _ClothRimExp) * occlusion;
    half inner = _ClothInnerExp * _ClothInnerScale * pow(vDotN, _ClothInnerExp);
    half lambert = _ClothLambertScale * NdotL * occlusion;

    half clothTerm = rim + inner + lambert;
    //clothTerm /= _ClothRimScale + _ClothInnerScale + _ClothLambertScale;

    return clothTerm;
}
#endif

half3 EnvironmentBRDF(BRDFData brdfData, half3 indirectDiffuse, half3 indirectSpecular, half fresnelTerm)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    indirectSpecular = FastSRGBToLinear(indirectSpecular);
#endif

#ifdef _DIFFUSEMODEL_SKIN
    indirectDiffuse = SkinTermIndirect(indirectDiffuse, brdfData.curvature);
#endif

    half3 c = indirectDiffuse * brdfData.diffuse;
    float surfaceReduction = 1.0 / (brdfData.roughness2 + 1.0);
    c += surfaceReduction * indirectSpecular * lerp(brdfData.specular, brdfData.grazingTerm, fresnelTerm);
    return c;
}

// Based on Minimalist CookTorrance BRDF
// Implementation is slightly different from original derivation: http://www.thetenthplanet.de/archives/255
//
// * NDF [Modified] GGX
// * Modified Kelemen and Szirmay-​Kalos for Visibility term
// * Fresnel approximated with 1/LdotH
half3 DirectBDRFSpec(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
#ifndef _SPECULARHIGHLIGHTS_OFF
    half3 halfDir = SafeNormalize(lightDirectionWS + viewDirectionWS);

    half NoH = saturate(dot(normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // BRDFspec = (D * V * F) / 4.0
    // D = roughness² / ( NoH² * (roughness² - 1) + 1 )²
    // V * F = 1.0 / ( LoH² * (roughness + 0.5) )
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155

    // Final BRDFspec = roughness² / ( NoH² * (roughness² - 1) + 1 )² * (LoH² * (roughness + 0.5) * 4.0)
    // We further optimize a few light invariant terms
    // brdfData.normalizationTerm = (roughness + 0.5) * 4.0 rewritten as roughness * 4.0 + 2.0 to a fit a MAD.
    half d = NoH * NoH * brdfData.roughness2MinusOne + 1.00001h;

    half LoH2 = LoH * LoH;
    half specularTerm = brdfData.roughness2 / ((d * d) * max(0.1h, LoH2) * brdfData.normalizationTerm);

    // on mobiles (where half actually means something) denominator have risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif

    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    half3 color = specularTerm * brdfData.specular * NdotL * brdfData.occlusion;
    return color;
#else
    return half3(0, 0, 0);
#endif
}

half3 DirectBDRFDiffuse(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
    half3 diffuseTerm;
#ifdef _DIFFUSEMODEL_CLOTH
    diffuseTerm = ClothTerm(normalWS, lightDirectionWS, viewDirectionWS, brdfData.occlusion);
#elif _DIFFUSEMODEL_SKIN
    diffuseTerm = SkinTerm(normalWS, lightDirectionWS, brdfData.curvature, brdfData.occlusion);
#else
    diffuseTerm = saturate(dot(normalWS, lightDirectionWS)) * brdfData.occlusion;
#endif

    return diffuseTerm * brdfData.diffuse;
}

///////////////////////////////////////////////////////////////////////////////
//                      Global Illumination                                  //
///////////////////////////////////////////////////////////////////////////////

// Samples SH L0, L1 and L2 terms
half3 SampleSH(half3 normalWS)
{
    // LPPV is not supported in Ligthweight Pipeline
    real4 SHCoefficients[7];
    SHCoefficients[0] = unity_SHAr;
    SHCoefficients[1] = unity_SHAg;
    SHCoefficients[2] = unity_SHAb;
    SHCoefficients[3] = unity_SHBr;
    SHCoefficients[4] = unity_SHBg;
    SHCoefficients[5] = unity_SHBb;
    SHCoefficients[6] = unity_SHC;

    return max(half3(0, 0, 0), SampleSH9(SHCoefficients, normalWS));
}

// SH Vertex Evaluation. Depending on target SH sampling might be
// done completely per vertex or mixed with L2 term per vertex and L0, L1
// per pixel. See SampleSHPixel
half3 SampleSHVertex(half3 normalWS)
{
#if defined(EVALUATE_SH_VERTEX)
    return max(half3(0, 0, 0), SampleSH(normalWS));
#elif defined(EVALUATE_SH_MIXED)
    // no max since this is only L2 contribution
    return SHEvalLinearL2(normalWS, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);
#endif

    // Fully per-pixel. Nothing to compute.
    return half3(0.0, 0.0, 0.0);
}

// SH Pixel Evaluation. Depending on target SH sampling might be done
// mixed or fully in pixel. See SampleSHVertex
half3 SampleSHPixel(half3 L2Term, half3 normalWS)
{
#if defined(EVALUATE_SH_VERTEX)
    return L2Term;
#elif defined(EVALUATE_SH_MIXED)
    half3 L0L1Term = SHEvalLinearL0L1(normalWS, unity_SHAr, unity_SHAg, unity_SHAb);
    return max(half3(0, 0, 0), L2Term + L0L1Term);
#endif

    // Default: Evaluate SH fully per-pixel
    return SampleSH(normalWS);
}

// Sample baked lightmap. Non-Direction and Directional if available.
// Realtime GI is not supported.
half3 SampleLightmap(float2 lightmapUV, half3 normalWS)
{
#ifdef UNITY_LIGHTMAP_FULL_HDR
    bool encodedLightmap = false;
#else
    bool encodedLightmap = true;
#endif

    // The shader library sample lightmap functions transform the lightmap uv coords to apply bias and scale.
    // However, lightweight pipeline already transformed those coords in vertex. We pass half4(1, 1, 0, 0) and
    // the compiler will optimize the transform away.
    half4 transformCoords = half4(1, 1, 0, 0);

#ifdef DIRLIGHTMAP_COMBINED
    return SampleDirectionalLightmap(TEXTURE2D_PARAM(unity_Lightmap, samplerunity_Lightmap),
        TEXTURE2D_PARAM(unity_LightmapInd, samplerunity_Lightmap),
        lightmapUV, transformCoords, normalWS, encodedLightmap, unity_Lightmap_HDR);
#else
    return SampleSingleLightmap(TEXTURE2D_PARAM(unity_Lightmap, samplerunity_Lightmap), lightmapUV, transformCoords, encodedLightmap, unity_Lightmap_HDR);
#endif
}

// We either sample GI from baked lightmap or from probes.
// If lightmap: sampleData.xy = lightmapUV
// If probe: sampleData.xyz = L2 SH terms
#ifdef LIGHTMAP_ON
#define SAMPLE_GI(lmName, shName, normalWSName) SampleLightmap(lmName, normalWSName)
#else
#define SAMPLE_GI(lmName, shName, normalWSName) SampleSHPixel(shName, normalWSName)
#endif

half3 GlossyEnvironmentReflection(half3 reflectVector, half perceptualRoughness, half occlusion)
{
#if !defined(_GLOSSYREFLECTIONS_OFF)
    half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip);

#if !defined(UNITY_USE_NATIVE_HDR)
    half3 irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
#else
    half3 irradiance = encodedIrradiance.rbg;
#endif

    return irradiance * occlusion;
#endif // GLOSSY_REFLECTIONS

    return _GlossyEnvironmentColor.rgb * occlusion;
}

half3 SubtractDirectMainLightFromLightmap(Light mainLight, half3 normalWS, half3 bakedGI)
{
    // Let's try to make realtime shadows work on a surface, which already contains
    // baked lighting and shadowing from the main sun light.
    // Summary:
    // 1) Calculate possible value in the shadow by subtracting estimated light contribution from the places occluded by realtime shadow:
    //      a) preserves other baked lights and light bounces
    //      b) eliminates shadows on the geometry facing away from the light
    // 2) Clamp against user defined ShadowColor.
    // 3) Pick original lightmap value, if it is the darkest one.


    // 1) Gives good estimate of illumination as if light would've been shadowed during the bake.
    // We only subtract the main direction light. This is accounted in the contribution term below.
    half shadowStrength = _ShadowData.x;
    half contributionTerm = saturate(dot(mainLight.direction, normalWS));
    half3 lambert = mainLight.color * contributionTerm;
    half3 estimatedLightContributionMaskedByInverseOfShadow = lambert * (1.0 - mainLight.attenuation);
    half3 subtractedLightmap = bakedGI - estimatedLightContributionMaskedByInverseOfShadow;

    // 2) Allows user to define overall ambient of the scene and control situation when realtime shadow becomes too dark.
    half3 realtimeShadow = max(subtractedLightmap, _SubtractiveShadowColor.xyz);
    realtimeShadow = lerp(bakedGI, realtimeShadow, shadowStrength);

    // 3) Pick darkest color
    return min(bakedGI, realtimeShadow);
}

half3 GlobalIllumination(BRDFData brdfData, half3 bakedGI, half occlusion, half3 normalWS, half3 viewDirectionWS)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half fresnelTerm = Pow4(1.0 - saturate(dot(normalWS, viewDirectionWS)));

    half3 indirectDiffuse = bakedGI * occlusion;
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, brdfData.perceptualRoughness, occlusion);

    return EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);
}

void MixRealtimeAndBakedGI(inout Light light, half3 normalWS, inout half3 bakedGI, half4 shadowMask)
{
#if defined(_MIXED_LIGHTING_SUBTRACTIVE) && defined(LIGHTMAP_ON)
    bakedGI = SubtractDirectMainLightFromLightmap(light, normalWS, bakedGI);
#endif

#if defined(LIGHTMAP_ON)
    #if defined(_MIXED_LIGHTING_SHADOWMASK)
        // TODO:
    #elif defined(_MIXED_LIGHTING_SUBTRACTIVE)
        // Subtractive Light mode has direct light contribution baked into lightmap for mixed lights.
        // We need to remove direct realtime contribution from mixed lights
        // subtractiveModeBakedOcclusion is set 0.0 if this light occlusion was baked in the lightmap, 1.0 otherwise.
        light.attenuation *= light.subtractiveModeAttenuation;
    #endif
#endif
}

///////////////////////////////////////////////////////////////////////////////
//                      Lighting Functions                                   //
///////////////////////////////////////////////////////////////////////////////
half3 LightingDiffuse(half3 lightColor, half3 lightDir, half3 normal, half3 viewDir)
{
    half3 lighting;
#ifdef _DIFFUSEMODEL_CLOTH
    lighting = ClothTerm(normal, lightDir, viewDir, 1);
#elif _DIFFUSEMODEL_SKIN
    lighting = SkinTerm(normal, lightDir, _SkinCurvature, 1);
#else
    lighting = saturate(dot(normal, lightDir));
#endif

    return lightColor * lighting;
}

half3 LightingSpecular(half3 lightColor, half3 lightDir, half3 normal, half3 viewDir, half4 specularGloss, half shininess)
{
    half3 halfVec = SafeNormalize(lightDir + viewDir);
    half NdotH = saturate(dot(normal, halfVec));
    half modifier = pow(NdotH, shininess) * specularGloss.a;
    half3 specularReflection = specularGloss.rgb * modifier;
    return lightColor * specularReflection;
}

half3 LightingPhysicallyBased(BRDFData brdfData, half3 lightColor, half3 lightDirectionWS, half lightAttenuation, half3 normalWS, half3 viewDirectionWS)
{
    half3 diffBrdf = DirectBDRFDiffuse(brdfData, normalWS, lightDirectionWS, viewDirectionWS);
    half3 specBrdf = DirectBDRFSpec(brdfData, normalWS, lightDirectionWS, viewDirectionWS);

    return (diffBrdf + specBrdf) * lightColor * lightAttenuation;
}

half3 LightingPhysicallyBased(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirectionWS)
{
    return LightingPhysicallyBased(brdfData, light.color, light.direction, light.attenuation, normalWS, viewDirectionWS);
}

half3 VertexLighting(float3 positionWS, half3 normalWS, half3 viewWS)
{
    half3 vertexLightColor = half3(0.0, 0.0, 0.0);

#if defined(_VERTEX_LIGHTS)
    int vertexLightStart = _AdditionalLightCount.x;
    int vertexLightEnd = min(_AdditionalLightCount.y, unity_LightIndicesOffsetAndCount.y);
    for (int lightIter = vertexLightStart; lightIter < vertexLightEnd; ++lightIter)
    {
        Light light = GetLight(lightIter, positionWS);

        half3 lightColor = light.color * light.attenuation;
        vertexLightColor += LightingDiffuse(lightColor, light.direction, normalWS, viewWS);
    }
#endif

    return vertexLightColor;
}

///////////////////////////////////////////////////////////////////////////////
//                      Fragment Functions                                   //
//       Used by ShaderGraph and others builtin renderers                    //
///////////////////////////////////////////////////////////////////////////////
half4 LightweightFragmentPBR(InputData inputData, half3 albedo, half metallic, half3 specular,
    half smoothness, half occlusion, half3 emission, half alpha, half curvature)
{
    BRDFData brdfData;
    InitializeBRDFData(albedo, metallic, specular, smoothness, alpha, occlusion, curvature, brdfData);

    Light mainLight = GetMainLight();
    mainLight.attenuation = MainLightRealtimeShadowAttenuation(inputData.shadowCoord);

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, half4(0, 0, 0, 0));
    half3 color = GlobalIllumination(brdfData, inputData.bakedGI, occlusion, inputData.normalWS, inputData.viewDirectionWS);

    color += LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);

#ifdef _ADDITIONAL_LIGHTS
    int pixelLightCount = GetPixelLightCount();
    for (int i = 0; i < pixelLightCount; ++i)
    {
        Light light = GetLight(i, inputData.positionWS);
        light.attenuation *= LocalLightRealtimeShadowAttenuation(light.index, inputData.positionWS);
        color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS);
    }
#endif

    color += inputData.vertexLighting * brdfData.diffuse;
    color += emission;
    return half4(color, alpha);
}

half4 LightweightFragmentBlinnPhong(InputData inputData, half3 diffuse, half4 specularGloss, half shininess, half3 emission, half alpha)
{
    Light mainLight = GetMainLight();
    mainLight.attenuation = MainLightRealtimeShadowAttenuation(inputData.shadowCoord);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, half4(0, 0, 0, 0));

    half3 attenuatedLightColor = mainLight.color * mainLight.attenuation;
    half3 diffuseColor = inputData.bakedGI + LightingDiffuse(attenuatedLightColor, mainLight.direction, inputData.normalWS, inputData.viewDirectionWS);
    half3 specularColor = LightingSpecular(attenuatedLightColor, mainLight.direction, inputData.normalWS, inputData.viewDirectionWS, specularGloss, shininess);

#ifdef _ADDITIONAL_LIGHTS
    int pixelLightCount = GetPixelLightCount();
    for (int i = 0; i < pixelLightCount; ++i)
    {
        Light light = GetLight(i, inputData.positionWS);
        light.attenuation *= LocalLightRealtimeShadowAttenuation(light.index, inputData.positionWS);
        half3 attenuatedLightColor = light.color * light.attenuation;
        diffuseColor += LightingDiffuse(attenuatedLightColor, light.direction, inputData.normalWS, inputData.viewDirectionWS);
        specularColor += LightingSpecular(attenuatedLightColor, light.direction, inputData.normalWS, inputData.viewDirectionWS, specularGloss, shininess);
    }
#endif

    half3 fullDiffuse = diffuseColor + inputData.vertexLighting;
    half3 finalColor = fullDiffuse * diffuse + emission;

#if defined(_SPECGLOSSMAP) || defined(_SPECULAR_COLOR)
    finalColor += specularColor;
#endif

    return half4(finalColor, alpha);
}
#endif
