using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{ 
    partial class HDLightEditor
    {
        protected override void OnSceneGUI()
        {
            m_SerializedAdditionalLightData.Update();

            HDAdditionalLightData src = (HDAdditionalLightData)m_SerializedAdditionalLightData.targetObject;
            Light light = (Light)target;

            Color wireframeColorAbove = light.enabled ? LightEditor.kGizmoLight : LightEditor.kGizmoDisabledLight;
            Color handleColorAbove = CoreLightEditorUtilities.GetLightHandleColor(wireframeColorAbove);
            Color wireframeColorBehind = CoreLightEditorUtilities.GetLightBehindObjectWireframeColor(wireframeColorAbove);
            Color handleColorBehind = CoreLightEditorUtilities.GetLightHandleColor(wireframeColorBehind);

            switch (src.lightTypeExtent)
            {
                case LightTypeExtent.Punctual:
                    switch (light.type)
                    {
                        case LightType.Directional:
                        case LightType.Point:
                            base.OnSceneGUI();  //use legacy handles
                            break;
                        case LightType.Spot:
                            switch (src.spotLightShape)
                            {
                                case SpotLightShape.Cone:
                                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                                    {
                                        Vector3 outterAngleInnerAngleRange = new Vector3(light.spotAngle, light.spotAngle * src.GetInnerSpotPercent01(), light.range);
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
                                        Handles.color = wireframeColorBehind;
                                        CoreLightEditorUtilities.DrawSpotlightWireframe(outterAngleInnerAngleRange, src.shadowNearPlane);
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                                        Handles.color = wireframeColorAbove;
                                        CoreLightEditorUtilities.DrawSpotlightWireframe(outterAngleInnerAngleRange, src.shadowNearPlane);
                                        EditorGUI.BeginChangeCheck();
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
                                        Handles.color = handleColorBehind;
                                        outterAngleInnerAngleRange = CoreLightEditorUtilities.DrawSpotlightHandle(outterAngleInnerAngleRange);
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                                        Handles.color = handleColorAbove;
                                        outterAngleInnerAngleRange = CoreLightEditorUtilities.DrawSpotlightHandle(outterAngleInnerAngleRange);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            Undo.RecordObjects(new UnityEngine.Object[] { target, src }, "Adjust Cone Spot Light");
                                            src.m_InnerSpotPercent = 100f * outterAngleInnerAngleRange.y / outterAngleInnerAngleRange.x;
                                            light.spotAngle = outterAngleInnerAngleRange.x;
                                            light.range = outterAngleInnerAngleRange.z;
                                        }

                                        // Handles.color reseted at end of scope
                                    }
                                    break;
                                case SpotLightShape.Pyramid:
                                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                                    {
                                        Vector4 aspectFovMaxRangeMinRange = new Vector4(src.aspectRatio, light.spotAngle, light.range);
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
                                        Handles.color = wireframeColorBehind;
                                        CoreLightEditorUtilities.DrawPyramidFrustumWireframe(aspectFovMaxRangeMinRange);
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                                        Handles.color = wireframeColorAbove;
                                        CoreLightEditorUtilities.DrawPyramidFrustumWireframe(aspectFovMaxRangeMinRange);
                                        EditorGUI.BeginChangeCheck();
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
                                        Handles.color = handleColorBehind;
                                        aspectFovMaxRangeMinRange = CoreLightEditorUtilities.DrawPyramidFrustumHandle(aspectFovMaxRangeMinRange, false);
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                                        Handles.color = handleColorAbove;
                                        aspectFovMaxRangeMinRange = CoreLightEditorUtilities.DrawPyramidFrustumHandle(aspectFovMaxRangeMinRange, false);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            Undo.RecordObjects(new UnityEngine.Object[] { target, src }, "Adjust Pyramid Spot Light");
                                            src.aspectRatio = aspectFovMaxRangeMinRange.x;
                                            light.spotAngle = aspectFovMaxRangeMinRange.y;
                                            light.range = aspectFovMaxRangeMinRange.z;
                                        }

                                        // Handles.color reseted at end of scope
                                    }
                                    break;
                                case SpotLightShape.Box:
                                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                                    {
                                        Vector4 widthHeightMaxRangeMinRange = new Vector4(src.shapeWidth, src.shapeHeight, light.range);
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
                                        Handles.color = wireframeColorBehind;
                                        CoreLightEditorUtilities.DrawOrthoFrustumWireframe(widthHeightMaxRangeMinRange);
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                                        Handles.color = wireframeColorAbove;
                                        CoreLightEditorUtilities.DrawOrthoFrustumWireframe(widthHeightMaxRangeMinRange);
                                        EditorGUI.BeginChangeCheck();
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
                                        Handles.color = handleColorBehind;
                                        widthHeightMaxRangeMinRange = CoreLightEditorUtilities.DrawOrthoFrustumHandle(widthHeightMaxRangeMinRange, false);
                                        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                                        Handles.color = handleColorAbove;
                                        widthHeightMaxRangeMinRange = CoreLightEditorUtilities.DrawOrthoFrustumHandle(widthHeightMaxRangeMinRange, false);
                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            Undo.RecordObjects(new UnityEngine.Object[] { target, src }, "Adjust Box Spot Light");
                                            src.shapeWidth = widthHeightMaxRangeMinRange.x;
                                            src.shapeHeight = widthHeightMaxRangeMinRange.y;
                                            light.range = widthHeightMaxRangeMinRange.z;
                                        }

                                        // Handles.color reseted at end of scope
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case LightTypeExtent.Rectangle:
                case LightTypeExtent.Tube:
                    bool withYAxis = src.lightTypeExtent == LightTypeExtent.Rectangle;
                    using (new Handles.DrawingScope(Matrix4x4.TRS(light.transform.position, light.transform.rotation, Vector3.one)))
                    {
                        Vector2 widthHeight = new Vector4(light.areaSize.x, withYAxis ? light.areaSize.y : 0f);
                        float range = light.range;
                        EditorGUI.BeginChangeCheck();
                        Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
                        Handles.color = wireframeColorBehind;
                        CoreLightEditorUtilities.DrawAreaLightWireframe(widthHeight);
                        range = Handles.RadiusHandle(Quaternion.identity, Vector3.zero, range); //also draw handles
                        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                        Handles.color = wireframeColorAbove;
                        CoreLightEditorUtilities.DrawAreaLightWireframe(widthHeight);
                        range = Handles.RadiusHandle(Quaternion.identity, Vector3.zero, range); //also draw handles
                        Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
                        Handles.color = handleColorBehind;
                        widthHeight = CoreLightEditorUtilities.DrawAreaLightHandle(widthHeight, withYAxis);
                        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                        Handles.color = handleColorAbove;
                        widthHeight = CoreLightEditorUtilities.DrawAreaLightHandle(widthHeight, withYAxis);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObjects(new UnityEngine.Object[] { target, src }, withYAxis ? "Adjust Area Rectangle Light" : "Adjust Area Tube Light");
                            light.areaSize = withYAxis ? widthHeight : new Vector2(widthHeight.x, light.areaSize.y);
                            light.range = range;
                        }

                        // Handles.color reseted at end of scope
                    }
                    break;
            }
        }


        [DrawGizmo(GizmoType.Selected)]
        static void DrawGizmoForHDAdditionalLightData(HDAdditionalLightData src, GizmoType gizmoType)
        {
            var light = src.gameObject.GetComponent<Light>();
            Color previousColor = Gizmos.color;
            Gizmos.color = light.enabled ? LightEditor.kGizmoLight : LightEditor.kGizmoDisabledLight;

            if (light.type != LightType.Directional)
            {
                // Trace a ray down to better locate the light location
                Ray ray = new Ray(src.gameObject.transform.position, Vector3.down);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                    using (new Handles.DrawingScope(Color.green))
                    {
                        Handles.DrawLine(src.gameObject.transform.position, hit.point);
                        Handles.DrawWireDisc(hit.point, hit.normal, 0.5f);
                    }

                    Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
                    using (new Handles.DrawingScope(Color.red))
                    {
                        Handles.DrawLine(src.gameObject.transform.position, hit.point);
                        Handles.DrawWireDisc(hit.point, hit.normal, 0.5f);
                    }
                }
            }

            Gizmos.color = previousColor;
        }
    }
}
