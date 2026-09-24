#if URP_14_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_6000_6_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine.Rendering.Universal;

namespace Unity.LiveCapture.VirtualCamera
{
    /// <summary>
    /// Pass that blends the render target in which the focus plane was rendered with the final frame.
    /// </summary>
    class UrpFocusPlaneComposePass : ScriptableRenderPass
    {
#if UNITY_6000_6_OR_NEWER
        class PassData
        {
            internal TextureHandle Color;
            internal Material Material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>().camera;
            if (camera.cameraType == CameraType.SceneView ||
                !FocusPlaneMap.Instance.TryGetInstance(camera, out var focusPlane) ||
                !focusPlane.isActiveAndEnabled ||
                !focusPlane.TryGetRenderTarget(out RTHandle target))
                return;

            var color = frameData.Get<UniversalResourceData>().activeColorTexture;
            using (var builder = renderGraph.AddUnsafePass<PassData>(FocusPlaneConsts.ComposePlaneProfilingSamplerLabel, out var data))
            {
                data.Color = color;
                data.Material = focusPlane.ComposeMaterial;
                builder.UseTexture(renderGraph.ImportTexture(target), AccessFlags.Read);
                builder.UseTexture(color, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData pass, UnsafeGraphContext context) =>
                {
                    context.cmd.SetRenderTarget(pass.Color);
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, pass.Material);
                });
            }
        }
#else
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (camera.cameraType == CameraType.SceneView)
                return;

            if (FocusPlaneMap.Instance.TryGetInstance(camera, out var focusPlane))
            {
                // Compositing is done by drawing a fullscreen quad as opposed to using Blit,
                // since it saves us the need to explicitly access the right destination target,
                // which turns out to be buggy in case of passes executed after post processes.
                var cmd = CommandBufferPool.Get(FocusPlaneConsts.ComposePlaneProfilingSamplerLabel);
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, focusPlane.ComposeMaterial);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
#endif
    }
}
#endif
