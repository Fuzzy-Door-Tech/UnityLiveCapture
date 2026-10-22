#if URP_14_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_6000_6_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine.Rendering.Universal;

namespace Unity.LiveCapture.VirtualCamera
{
    internal class UrpFrameLinesPass : ScriptableRenderPass
    {
#if UNITY_6000_6_OR_NEWER
        class PassData
        {
            internal FrameLines FrameLines;
            internal TextureHandle Color;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>().camera;
            if (camera.cameraType == CameraType.SceneView ||
                !FrameLinesMap.Instance.TryGetInstance(camera, out var frameLines))
                return;

            var color = frameData.Get<UniversalResourceData>().activeColorTexture;
            using (var builder = renderGraph.AddUnsafePass<PassData>(FrameLines.k_ProfilingSamplerLabel, out var data))
            {
                data.FrameLines = frameLines;
                data.Color = color;
                builder.UseTexture(color, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData pass, UnsafeGraphContext context) =>
                {
                    context.cmd.SetRenderTarget(pass.Color);
                    pass.FrameLines.Render(CommandBufferHelpers.GetNativeCommandBuffer(context.cmd));
                });
            }
        }
#else
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (camera.cameraType == CameraType.SceneView)
                return;

            if (FrameLinesMap.Instance.TryGetInstance(camera, out var frameLines))
            {
                CommandBuffer cmd = CommandBufferPool.Get(FrameLines.k_ProfilingSamplerLabel);
                frameLines.Render(cmd);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
#endif
    }
}
#endif
