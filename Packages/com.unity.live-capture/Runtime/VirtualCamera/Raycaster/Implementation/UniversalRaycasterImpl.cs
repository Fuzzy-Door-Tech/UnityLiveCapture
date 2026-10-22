#if URP_14_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.LiveCapture.Rendering;

namespace Unity.LiveCapture.VirtualCamera.Raycasting
{
    class UniversalRaycasterImpl : BaseScriptableRenderPipelineRaycasterImpl
    {
        RenderTexture m_PlaceholderTarget;

        public override void Initialize()
        {
            base.Initialize();

            // We assign a target texture even though its content is not relevant to us to avoid
            // "Missing Vulkan framebuffer attachment image?" errors on Linux + Vulkan.
            // RenderGraph requires a depth attachment on a camera output texture.
            m_PlaceholderTarget = new RenderTexture(1, 1, 24);
            m_PlaceholderTarget.Create();
            m_Camera.targetTexture = m_PlaceholderTarget;

#if UNITY_6000_5_OR_NEWER
            // URP's RenderGraph path no longer calls the legacy injection pass. Render this
            // camera's picking targets after its camera graph has completed.
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#else
            RenderPipelineBridge.RequestRenderFeature<InjectionPointRenderFeature>();
            InjectionPointRenderPass.onExecute += OnExecute;
#endif
        }

        public override void Dispose()
        {
            m_PlaceholderTarget.Release();
#if UNITY_6000_5_OR_NEWER
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
#else
            InjectionPointRenderPass.onExecute -= OnExecute;
#endif
            base.Dispose();
        }

#if UNITY_6000_5_OR_NEWER
        void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            RenderForCamera(context, camera);
        }
#else
        void OnExecute(ScriptableRenderContext context, RenderingData renderingData)
        {
            RenderForCamera(context, renderingData.cameraData.camera);
        }
#endif

        void RenderForCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_Camera)
                return;

            context.SetupCameraProperties(camera);

            var cmd = CommandBufferPool.Get("Graphics Raycast");

            cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

            DoRender(cmd, context, m_Camera);

            CommandBufferPool.Release(cmd);
            context.Submit();
        }
    }
}
#endif
