#if UNITY_6000_5_OR_NEWER
using NUnit.Framework;
using Unity.LiveCapture.VirtualCamera.Raycasting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.LiveCapture.Tests.Editor.Pipelines.Urp
{
    public class UrpGraphicsRaycasterTests
    {
        [Test]
        public void Raycast_VisibleCube_ReturnsObject()
        {
            var priorPipeline = GraphicsSettings.defaultRenderPipeline;
            var priorQualityPipeline = QualitySettings.renderPipeline;
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(0, 0, 3);
            cube.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            try
            {
                using (var raycaster = new GraphicsRaycaster())
                {
                    var found = raycaster.Raycast(Vector3.zero, Vector3.forward, out _, out var hitObject, 0.1f, 10f);
                    Assert.That(found, Is.True);
                    Assert.That(hitObject, Is.SameAs(cube));
                }
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = priorPipeline;
                QualitySettings.renderPipeline = priorQualityPipeline;
                Object.DestroyImmediate(cube.GetComponent<Renderer>().sharedMaterial);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(pipeline);
                Object.DestroyImmediate(rendererData);
            }
        }
    }
}
#endif
