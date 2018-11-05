using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    partial class HDLightEditor
    {
        sealed class SerializedLightData
        {
            public SerializedProperty intensity;
            public SerializedProperty enableSpotReflector;
            public SerializedProperty spotInnerPercent;
            public SerializedProperty lightDimmer;
            public SerializedProperty fadeDistance;
            public SerializedProperty affectDiffuse;
            public SerializedProperty affectSpecular;
            public SerializedProperty nonLightmappedOnly;
            public SerializedProperty lightTypeExtent;
            public SerializedProperty spotLightShape;
            public SerializedProperty shapeWidth;
            public SerializedProperty shapeHeight;
            public SerializedProperty aspectRatio;
            public SerializedProperty shapeRadius;
            public SerializedProperty maxSmoothness;
            public SerializedProperty applyRangeAttenuation;
            public SerializedProperty volumetricDimmer;
            public SerializedProperty lightUnit;
            public SerializedProperty displayAreaLightEmissiveMesh;
            public SerializedProperty lightLayers;
            public SerializedProperty shadowNearPlane;
            public SerializedProperty shadowSoftness;
            public SerializedProperty blockerSampleCount;
            public SerializedProperty filterSampleCount;
            public SerializedProperty sunDiskSize;
            public SerializedProperty sunHaloSize;

            // Editor stuff
            public SerializedProperty useOldInspector;
            public SerializedProperty showFeatures;
            public SerializedProperty showAdditionalSettings;
        }

        sealed class SerializedShadowData
        {
            public SerializedProperty shadowDimmer;
            public SerializedProperty volumetricShadowDimmer;
            public SerializedProperty fadeDistance;
            public SerializedProperty resolution;
            public SerializedProperty contactShadows;

            // Bias control
            public SerializedProperty viewBiasMin;
            public SerializedProperty viewBiasMax;
            public SerializedProperty viewBiasScale;
            public SerializedProperty normalBiasMin;
            public SerializedProperty normalBiasMax;
            public SerializedProperty normalBiasScale;
            public SerializedProperty sampleBiasScale;
            public SerializedProperty edgeLeakFixup;
            public SerializedProperty edgeToleranceNormal;
            public SerializedProperty edgeTolerance;
        }

        SerializedObject m_SerializedAdditionalLightData;
        SerializedObject m_SerializedAdditionalShadowData;

        SerializedLightData m_AdditionalLightData;
        SerializedShadowData m_AdditionalShadowData;

        void InitSerializedData()
        {
            // Get & automatically add additional HD data if not present
            m_AdditionalLightDatas = CoreEditorUtils.GetAdditionalData<HDAdditionalLightData>(targets, HDAdditionalLightData.InitDefaultHDAdditionalLightData);
            m_AdditionalShadowDatas = CoreEditorUtils.GetAdditionalData<AdditionalShadowData>(targets, HDAdditionalShadowData.InitDefaultHDAdditionalShadowData);
            m_SerializedAdditionalLightData = new SerializedObject(m_AdditionalLightDatas);
            m_SerializedAdditionalShadowData = new SerializedObject(m_AdditionalShadowDatas);

            using (var o = new PropertyFetcher<HDAdditionalLightData>(m_SerializedAdditionalLightData))
                m_AdditionalLightData = new SerializedLightData
                {
                    intensity = o.Find(x => x.displayLightIntensity),
                    enableSpotReflector = o.Find(x => x.enableSpotReflector),
                    spotInnerPercent = o.Find(x => x.m_InnerSpotPercent),
                    lightDimmer = o.Find(x => x.lightDimmer),
                    volumetricDimmer = o.Find(x => x.volumetricDimmer),
                    lightUnit = o.Find(x => x.lightUnit),
                    displayAreaLightEmissiveMesh = o.Find(x => x.displayAreaLightEmissiveMesh),
                    lightLayers = o.Find(x => x.lightLayers),
                    fadeDistance = o.Find(x => x.fadeDistance),
                    affectDiffuse = o.Find(x => x.affectDiffuse),
                    affectSpecular = o.Find(x => x.affectSpecular),
                    nonLightmappedOnly = o.Find(x => x.nonLightmappedOnly),
                    lightTypeExtent = o.Find(x => x.lightTypeExtent),
                    spotLightShape = o.Find(x => x.spotLightShape),
                    shapeWidth = o.Find(x => x.shapeWidth),
                    shapeHeight = o.Find(x => x.shapeHeight),
                    aspectRatio = o.Find(x => x.aspectRatio),
                    shapeRadius = o.Find(x => x.shapeRadius),
                    maxSmoothness = o.Find(x => x.maxSmoothness),
                    applyRangeAttenuation = o.Find(x => x.applyRangeAttenuation),
                    shadowNearPlane = o.Find(x => x.shadowNearPlane),
                    shadowSoftness = o.Find(x => x.shadowSoftness),
                    blockerSampleCount = o.Find(x => x.blockerSampleCount),
                    filterSampleCount = o.Find(x => x.filterSampleCount),
                    sunDiskSize = o.Find(x => x.sunDiskSize),
                    sunHaloSize = o.Find(x => x.sunHaloSize),

                    // Editor stuff
                    useOldInspector = o.Find(x => x.useOldInspector),
                    showFeatures = o.Find(x => x.featuresFoldout),
                    showAdditionalSettings = o.Find(x => x.showAdditionalSettings)
                };

            // TODO: Review this once AdditionalShadowData is refactored
            using (var o = new PropertyFetcher<AdditionalShadowData>(m_SerializedAdditionalShadowData))
                m_AdditionalShadowData = new SerializedShadowData
                {
                    shadowDimmer = o.Find(x => x.shadowDimmer),
                    volumetricShadowDimmer = o.Find(x => x.volumetricShadowDimmer),
                    fadeDistance = o.Find(x => x.shadowFadeDistance),
                    resolution = o.Find(x => x.shadowResolution),
                    contactShadows = o.Find(x => x.contactShadows),

                    viewBiasMin = o.Find(x => x.viewBiasMin),
                    viewBiasMax = o.Find(x => x.viewBiasMax),
                    viewBiasScale = o.Find(x => x.viewBiasScale),
                    normalBiasMin = o.Find(x => x.normalBiasMin),
                    normalBiasMax = o.Find(x => x.normalBiasMax),
                    normalBiasScale = o.Find(x => x.normalBiasScale),
                    sampleBiasScale = o.Find(x => x.sampleBiasScale),
                    edgeLeakFixup = o.Find(x => x.edgeLeakFixup),
                    edgeToleranceNormal = o.Find(x => x.edgeToleranceNormal),
                    edgeTolerance = o.Find(x => x.edgeTolerance)
                };
        }
    }
}
