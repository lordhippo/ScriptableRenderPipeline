using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.HDPipeline;
using Object = UnityEngine.Object;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    using _ = CoreEditorUtils;

    [CustomEditorForRenderPipeline(typeof(PlanarReflectionProbe), typeof(HDRenderPipelineAsset))]
    [CanEditMultipleObjects]
    sealed class PlanarReflectionProbeEditor : HDProbeEditor<PlanarReflectionProbeUISettingsProvider, PlanarReflectionProbeUI, SerializedPlanarReflectionProbe>
    {
        const float k_PreviewHeight = 128;

        List<Texture> m_PreviewedTextures = new List<Texture>();

        public override bool HasPreviewGUI()
        {
            foreach (PlanarReflectionProbe p in m_TypedTargets)
            {
                if (p.texture != null)
                    return true;
            }
            return false;
        }

        public override GUIContent GetPreviewTitle() => _.GetContent("Planar Reflection");

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            m_PreviewedTextures.Clear();
            foreach (PlanarReflectionProbe p in m_TypedTargets)
            {
                m_PreviewedTextures.Add(p.texture);
            }

            var space = Vector2.one;
            var rowSize = Mathf.CeilToInt(Mathf.Sqrt(m_PreviewedTextures.Count));
            var size = r.size / rowSize - space * (rowSize - 1);

            for (var i = 0; i < m_PreviewedTextures.Count; i++)
            {
                var row = i / rowSize;
                var col = i % rowSize;
                var itemRect = new Rect(
                        r.x + size.x * row + ((row > 0) ? (row - 1) * space.x : 0),
                        r.y + size.y * col + ((col > 0) ? (col - 1) * space.y : 0),
                        size.x,
                        size.y);

                if (m_PreviewedTextures[i] != null)
                    EditorGUI.DrawPreviewTexture(itemRect, m_PreviewedTextures[i], CameraEditorUtils.GUITextureBlit2SRGBMaterial, ScaleMode.ScaleToFit, 0, 1);
                else
                    EditorGUI.LabelField(itemRect, _.GetContent("Not Available"));
            }
        }

        protected override SerializedPlanarReflectionProbe NewSerializedObject(SerializedObject so)
            => new SerializedPlanarReflectionProbe(so);
        internal override HDProbe GetTarget(Object editorTarget) => editorTarget as HDProbe;

        protected override void DrawAdditionalCaptureSettings(
            PlanarReflectionProbeUI s, SerializedPlanarReflectionProbe d, Editor o
        )
        {
            ++EditorGUI.indentLevel;
            GUI.enabled = d.probeSettings.mode.intValue != (int)ProbeSettings.Mode.Realtime;
            EditorGUILayout.PropertyField(d.localReferencePosition, _.GetContent("Reference Local Position"));
            GUI.enabled = true;
            --EditorGUI.indentLevel;
        }

        protected override void DrawHandles(
            PlanarReflectionProbeUI s,
            SerializedPlanarReflectionProbe d,
            Editor o
        )
        {
            base.DrawHandles(s, d, o);

            SceneViewOverlay_Window(_.GetContent("Planar Probe"), OnOverlayGUI, -100, target);

            using (new Handles.DrawingScope(Matrix4x4.TRS(d.target.transform.position, d.target.transform.rotation, Vector3.one)))
            {
                var referencePosition = d.localReferencePosition.vector3Value;
                EditorGUI.BeginChangeCheck();
                referencePosition = Handles.PositionHandle(referencePosition, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                    d.localReferencePosition.vector3Value = referencePosition;
            }
        }

        void OnOverlayGUI(Object target, SceneView sceneView)
        {
            var previewSize = new Rect();
            foreach(PlanarReflectionProbe p in m_TypedTargets)
            {
                if (p.texture == null)
                    continue;

                var factor = k_PreviewHeight / p.texture.height;

                previewSize.x += p.texture.width * factor;
                previewSize.y = k_PreviewHeight;
            }

            // Get and reserve rect
            Rect cameraRect = GUILayoutUtility.GetRect(previewSize.x, previewSize.y);

            if (Event.current.type == EventType.Repaint)
            {
                var c = new Rect(cameraRect);
                foreach(PlanarReflectionProbe p in m_TypedTargets)
                {
                    if (p.texture == null)
                        continue;

                    var factor = k_PreviewHeight / p.texture.height;

                    c.width = p.texture.width * factor;
                    c.height = k_PreviewHeight;
                    Graphics.DrawTexture(c, p.texture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, GUI.color, CameraEditorUtils.GUITextureBlit2SRGBMaterial);

                    c.x += c.width;
                }
            }
        }

        static Type k_SceneViewOverlay_WindowFunction = Type.GetType("UnityEditor.SceneViewOverlay+WindowFunction,UnityEditor");
        static Type k_SceneViewOverlay_WindowDisplayOption = Type.GetType("UnityEditor.SceneViewOverlay+WindowDisplayOption,UnityEditor");
        static MethodInfo k_SceneViewOverlay_Window = Type.GetType("UnityEditor.SceneViewOverlay,UnityEditor")
            .GetMethod(
                "Window",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                CallingConventions.Any,
                new[] { typeof(GUIContent), k_SceneViewOverlay_WindowFunction, typeof(int), typeof(Object), k_SceneViewOverlay_WindowDisplayOption },
                null);
        static void SceneViewOverlay_Window(GUIContent title, Action<Object, SceneView> sceneViewFunc, int order, Object target)
        {
            k_SceneViewOverlay_Window.Invoke(null, new[]
            {
                title, DelegateUtility.Cast(sceneViewFunc, k_SceneViewOverlay_WindowFunction),
                order,
                target,
                Enum.ToObject(k_SceneViewOverlay_WindowDisplayOption, 1)
            });
        }

        static Mesh k_QuadMesh;
        static Material k_PreviewMaterial;
        [DrawGizmo(GizmoType.Selected)]
        static void DrawSelectedGizmo(PlanarReflectionProbe probe, GizmoType gizmoType)
        {
            var e = (PlanarReflectionProbeEditor)GetEditorFor(probe);
            if (e == null)
                return;

            var mat = Matrix4x4.TRS(probe.transform.position, probe.transform.rotation, Vector3.one);
            InfluenceVolumeUI.DrawGizmos(
                e.m_UIState.probeSettings.influence,
                probe.influenceVolume,
                mat,
                InfluenceVolumeUI.HandleType.None,
                InfluenceVolumeUI.HandleType.Base | InfluenceVolumeUI.HandleType.Influence
            );

            DrawCapturePositionGizmo(probe);
        }

        static void DrawCapturePositionGizmo(PlanarReflectionProbe probe)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            // Capture gizmo
            if (k_QuadMesh == null)
                k_QuadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (k_PreviewMaterial == null)
                k_PreviewMaterial = new Material(Shader.Find("Debug/PlanarReflectionProbePreview"));

            var proxyToWorld = probe.proxyToWorld;
            var settings = probe.settings;
            var mirrorPosition = proxyToWorld.MultiplyPoint(settings.proxySettings.mirrorPositionProxySpace);
            var mirrorRotation = proxyToWorld.rotation * settings.proxySettings.mirrorRotationProxySpace * Quaternion.Euler(0, 180, 0);
            var renderData = probe.renderData;

            var gpuProj = GL.GetGPUProjectionMatrix(renderData.projectionMatrix, true);
            var gpuView = renderData.worldToCameraRHS;
            var vp = gpuProj * gpuView;

            var cameraPositionWS = Vector3.zero;
            var capturePositionWS = renderData.capturePosition;
            if (SceneView.currentDrawingSceneView?.camera != null)
            {
                cameraPositionWS = SceneView.currentDrawingSceneView.camera.transform.position;
                // For Camera relative rendering, we need to translate with the position of the currently rendering camera
                capturePositionWS -= cameraPositionWS;
            }

            k_PreviewMaterial.SetTexture("_MainTex", probe.texture);
            k_PreviewMaterial.SetMatrix("_CaptureVPMatrix", vp);
            k_PreviewMaterial.SetVector("_CameraPositionWS", new Vector4(0, 0, 0, 0));
            k_PreviewMaterial.SetVector("_CapturePositionWS", new Vector4(capturePositionWS.x, capturePositionWS.y, -capturePositionWS.z, 0));
            k_PreviewMaterial.SetPass(0);
            Graphics.DrawMeshNow(k_QuadMesh, Matrix4x4.TRS(mirrorPosition, mirrorRotation, Vector3.one * capturePointPreviewSize * 2));
        }
    }

    struct PlanarReflectionProbeUISettingsProvider : HDProbeUI.IProbeUISettingsProvider, InfluenceVolumeUI.IInfluenceUISettingsProvider
    {
        bool InfluenceVolumeUI.IInfluenceUISettingsProvider.drawOffset => false;
        bool InfluenceVolumeUI.IInfluenceUISettingsProvider.drawNormal => false;
        bool InfluenceVolumeUI.IInfluenceUISettingsProvider.drawFace => false;


        ProbeSettingsOverride HDProbeUI.IProbeUISettingsProvider.displayedCaptureSettings => new ProbeSettingsOverride
        {
            probe = ProbeSettingsFields.proxyMirrorPositionProxySpace
               | ProbeSettingsFields.proxyMirrorRotationProxySpace,
            camera = new CameraSettingsOverride
            {
                camera = (CameraSettingsFields)(-1) & ~(
                   CameraSettingsFields.flipYMode
                   | CameraSettingsFields.frustumAspect
                   | CameraSettingsFields.cullingInvertCulling
                   | CameraSettingsFields.frustumMode
                   | CameraSettingsFields.frustumProjectionMatrix
               )
            }
        };
        ProbeSettingsOverride HDProbeUI.IProbeUISettingsProvider.overrideableCaptureSettings => new ProbeSettingsOverride
        {
            probe = ProbeSettingsFields.none,
            camera = new CameraSettingsOverride
            {
                camera = CameraSettingsFields.frustumFieldOfView
            }
        };
        ProbeSettingsOverride HDProbeUI.IProbeUISettingsProvider.displayedAdvancedSettings => new ProbeSettingsOverride
        {
            probe = ProbeSettingsFields.lightingLightLayer
                | ProbeSettingsFields.lightingMultiplier
                | ProbeSettingsFields.lightingWeight,
            camera = new CameraSettingsOverride
            {
                camera = CameraSettingsFields.none
            }
        };
        ProbeSettingsOverride HDProbeUI.IProbeUISettingsProvider.overrideableAdvancedSettings => new ProbeSettingsOverride();
        Type HDProbeUI.IProbeUISettingsProvider.customTextureType => typeof(Texture2D);
        static readonly HDProbeUI.ToolBar[] k_Toolbars = { HDProbeUI.ToolBar.InfluenceShape | HDProbeUI.ToolBar.Blend };
        HDProbeUI.ToolBar[] HDProbeUI.IProbeUISettingsProvider.toolbars => k_Toolbars;
    }
}
