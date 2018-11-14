using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.LightweightPipeline;
using UnityEngine.Rendering;

internal class ForwardOpaquesPass : ScriptableRenderPass
{
    FilteringSettings m_OpaqueFilterSettings;

    RenderTargetHandle colorAttachmentHandle { get; set; }
    RenderTargetHandle depthAttachmentHandle { get; set; }
    RenderTextureDescriptor descriptor { get; set; }
    SampleCount samples { get; set; }
    ClearFlag clearFlag { get; set; }
    Color clearColor { get; set; }

    PerObjectData rendererConfiguration;

    public ForwardOpaquesPass()
    {
        RegisterShaderPassName("LightweightForward");
        RegisterShaderPassName("SRPDefaultUnlit");

        m_OpaqueFilterSettings = new FilteringSettings(RenderQueueRange.opaque);
    }

    public void Setup(
        RenderTextureDescriptor baseDescriptor,
        RenderTargetHandle colorAttachmentHandle,
        RenderTargetHandle depthAttachmentHandle,
        SampleCount samples,
        ClearFlag clearFlag,
        Color clearColor,
        PerObjectData configuration)
    {
        descriptor = baseDescriptor;
        this.colorAttachmentHandle = colorAttachmentHandle;
        this.depthAttachmentHandle = depthAttachmentHandle;
        this.samples = samples;
        this.clearFlag = clearFlag;
        this.clearColor = CoreUtils.ConvertSRGBToActiveColorSpace(clearColor);
        this.rendererConfiguration = configuration;
    }

    /// <inheritdoc/>
    public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (renderer == null)
            throw new ArgumentNullException("renderer");

        context.SetupCameraProperties(renderingData.cameraData.camera, renderingData.cameraData.isStereoEnabled);

        CommandBuffer cmd = CommandBufferPool.Get("render opaques");
        if (colorAttachmentHandle != RenderTargetHandle.CameraTarget)
        {
            var colorDescriptor = descriptor;
            colorDescriptor.depthBufferBits = 0;
            colorDescriptor.sRGB = true;
            colorDescriptor.msaaSamples = (int)samples;
            cmd.GetTemporaryRT(colorAttachmentHandle.id, colorDescriptor, FilterMode.Bilinear);
        }

        if (depthAttachmentHandle != RenderTargetHandle.CameraTarget)
        {
            var depthDescriptor = descriptor;
            depthDescriptor.colorFormat = RenderTextureFormat.Depth;
            depthDescriptor.depthBufferBits = 32;
            depthDescriptor.msaaSamples = (int)samples;
            depthDescriptor.bindMS = (int)samples > 1 && !SystemInfo.supportsMultisampleAutoResolve && (SystemInfo.supportsMultisampledTextures != 0);
            cmd.GetTemporaryRT(depthAttachmentHandle.id, depthDescriptor, FilterMode.Point);
        }

        RenderBufferLoadAction loadOp = RenderBufferLoadAction.DontCare;
        RenderBufferStoreAction storeOp = RenderBufferStoreAction.Store;
        SetRenderTarget(cmd, colorAttachmentHandle.Identifier(), loadOp, storeOp,
        depthAttachmentHandle.Identifier(), loadOp, storeOp, clearFlag, clearColor, descriptor.dimension);

        Camera camera = renderingData.cameraData.camera;
        XRUtils.DrawOcclusionMesh(cmd, camera, renderingData.cameraData.isStereoEnabled);

        int mainLightIndex = renderingData.lightData.mainLightIndex;
        if (mainLightIndex != 1)
        {
            VisibleLight mainLight = renderingData.lightData.visibleLights[mainLightIndex];
            Vector4 lightDirection = -mainLight.localToWorldMatrix.GetColumn(2);
            cmd.SetGlobalVector("_MainLightPosition", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 1.0f));
            cmd.SetGlobalVector("_MainLightColor", mainLight.finalColor);
        }
        else
        {
            cmd.SetGlobalVector("_MainLightPosition", new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            cmd.SetGlobalVector("_MainLightColor", new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
        var drawSettings = CreateDrawingSettings(camera, sortFlags, rendererConfiguration, renderingData.supportsDynamicBatching);
        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_OpaqueFilterSettings);

        // Render objects that did not match any shader pass with error shader
        renderer.RenderObjectsWithError(context, ref renderingData.cullResults, camera, m_OpaqueFilterSettings, SortingCriteria.None);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (cmd == null)
            throw new ArgumentNullException("cmd");

        if (colorAttachmentHandle != RenderTargetHandle.CameraTarget)
        {
            cmd.ReleaseTemporaryRT(colorAttachmentHandle.id);
            colorAttachmentHandle = RenderTargetHandle.CameraTarget;
        }

        if (depthAttachmentHandle != RenderTargetHandle.CameraTarget)
        {
            cmd.ReleaseTemporaryRT(depthAttachmentHandle.id);
            depthAttachmentHandle = RenderTargetHandle.CameraTarget;
        }
    }
}

public class CustomLWPipe : MonoBehaviour, IRendererSetup
{
    private ForwardOpaquesPass m_RenderOpaqueForwardPass;

    [NonSerialized]
    private bool m_Initialized = false;

    private void Init()
    {
        if (m_Initialized)
            return;

        m_RenderOpaqueForwardPass = new ForwardOpaquesPass();

        m_Initialized = true;
    }

    public void Setup(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Init();

        renderer.SetupPerObjectLightIndices(ref renderingData.cullResults, ref renderingData.lightData);
        RenderTextureDescriptor baseDescriptor = ScriptableRenderer.CreateRenderTextureDescriptor(ref renderingData.cameraData);
        RenderTextureDescriptor shadowDescriptor = baseDescriptor;
        shadowDescriptor.dimension = TextureDimension.Tex2D;

        RenderTargetHandle colorHandle = RenderTargetHandle.CameraTarget;
        RenderTargetHandle depthHandle = RenderTargetHandle.CameraTarget;
        
        var sampleCount = (SampleCount)renderingData.cameraData.msaaSamples;
        Camera camera = renderingData.cameraData.camera;
        var rendererConfiguration = ScriptableRenderer.GetRendererConfiguration(renderingData.lightData.additionalLightsCount);

        m_RenderOpaqueForwardPass.Setup(baseDescriptor, colorHandle, depthHandle, sampleCount, ScriptableRenderer.GetCameraClearFlag(camera), camera.backgroundColor, rendererConfiguration);
        renderer.EnqueuePass(m_RenderOpaqueForwardPass);
    }
}
