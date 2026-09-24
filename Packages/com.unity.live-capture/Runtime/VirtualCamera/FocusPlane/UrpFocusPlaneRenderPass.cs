#if URP_14_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_6000_6_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine.Rendering.Universal;

namespace Unity.LiveCapture.VirtualCamera
{
    /// <summary>
    /// Pass that renders the focus plane to an intermediary render target.
    /// </summary>
    /// We find ourselves supporting both RenderTexture and RTHandle,
    /// as URP migrated to RTHandle at v13.1.2
    /// </remarks>
    class UrpFocusPlaneRenderPass : ScriptableRenderPass
    {
#if UNITY_6000_6_OR_NEWER
        class PassData
        {
            internal TextureHandle Source;
            internal TextureHandle Target;
            internal Material Material;
        }

        public UrpFocusPlaneRenderPass()
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>().camera;
            if (camera.cameraType == CameraType.SceneView ||
                !FocusPlaneMap.Instance.TryGetInstance(camera, out var focusPlane) ||
                !focusPlane.isActiveAndEnabled ||
                !focusPlane.TryGetRenderTarget(out RTHandle target))
                return;

            if (!(Mathf.Approximately(target.scaleFactor.x, 1) && Mathf.Approximately(target.scaleFactor.y, 1)))
                throw new InvalidOperationException("Scaling of renderTarget not supported yet.");

            var resources = frameData.Get<UniversalResourceData>();
            using (var builder = renderGraph.AddUnsafePass<PassData>(FocusPlaneConsts.RenderProfilingSamplerLabel, out var data))
            {
                data.Source = resources.activeColorTexture;
                data.Target = renderGraph.ImportTexture(target);
                data.Material = focusPlane.RenderMaterial;
                builder.UseTexture(data.Source, AccessFlags.Read);
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.UseTexture(data.Target, AccessFlags.WriteAll);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderFunc(static (PassData pass, UnsafeGraphContext context) =>
                {
                    context.cmd.SetRenderTarget(pass.Target);
                    Blitter.BlitTexture(CommandBufferHelpers.GetNativeCommandBuffer(context.cmd),
                        pass.Source, new Vector4(1, 1, 0, 0), pass.Material, 0);
                });
            }
        }
#else
        public RTHandle Source;

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (camera.cameraType == CameraType.SceneView)
                return;

            if (FocusPlaneMap.Instance.TryGetInstance(camera, out var focusPlane))
            {
                if (focusPlane.isActiveAndEnabled && focusPlane.TryGetRenderTarget(out RTHandle target))
                {
                    // URP does not yet use scaling but it is upcoming so this will prevent us from missing the landing.
                    // We should try and lean on URP's built-in blitting utilities if possible.
                    if (!(Mathf.Approximately(target.scaleFactor.x, 1) && Mathf.Approximately(target.scaleFactor.y, 1)))
                    {
                        throw new InvalidOperationException("Scaling of renderTarget not supported yet.");
                    }

                    CommandBuffer cmd = CommandBufferPool.Get(FocusPlaneConsts.RenderProfilingSamplerLabel);
                    Blit(cmd, Source, target, focusPlane.RenderMaterial);
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }
        }
#endif
    }
}
#endif
