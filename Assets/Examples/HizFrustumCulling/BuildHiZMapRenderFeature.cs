using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[System.Serializable]
public class BuildHiZMapSetting
{
    public ComputeShader HizComputeShader; 
}

public class BuildHiZMapRenderFeature : ScriptableRendererFeature
{
    public BuildHiZMapSetting Setting = new BuildHiZMapSetting();
    BuildHizMapRenderPass m_buildhizPass;

    /// <inheritdoc/>
    public override void Create()
    {
        if (m_buildhizPass == null)
        {
            if (!Setting.HizComputeShader)
            {
                Debug.LogError("missing Hiz compute shader");
                return;
            }
            m_buildhizPass = new BuildHizMapRenderPass(Setting.HizComputeShader);
        }
        

        // Configures where the render pass should be injected.
        m_buildhizPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera)
        {
            return;
        }

        if(renderingData.cameraData.camera.name != "Main Camera")
        {
            return;
        }
        renderer.EnqueuePass(m_buildhizPass);
    }
}

public class BuildHizMapRenderPass  : ScriptableRenderPass
{
    private HizMap m_Hizmap;
    public BuildHizMapRenderPass(ComputeShader computeShader)
    {
        this.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        m_Hizmap = new HizMap(computeShader);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        m_Hizmap.Update(context, renderingData.cameraData.camera,renderingData.cameraData.renderer.cameraDepthTargetHandle);
    }

}

