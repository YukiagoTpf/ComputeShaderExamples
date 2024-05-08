using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Screen = UnityEngine.Device.Screen;

public class BuildHizMapRenderPass  : ScriptableRenderPass
{
    int mipCount = 11;
    CommandBuffer _command;
    public ComputeShader CS_BuildHizMap;
    public int KN_BuildHizMap;

    //RenderTexture inputDepthMap0;
    RenderTexture inputDepthMap1;
    RenderTexture inputDepthMap2;
    ComputeBuffer mDispatchArgsBuffer;

    //Material MT_CopyDepthTexture;

    Vector4 InputDepthMapSize0 = new Vector4();
    Vector4 InputDepthMapSize1 = new Vector4();
    Vector4 InputDepthMapSize2 = new Vector4();
    uint[] BuildHizMapArgs0 = new uint[3];
    uint[] BuildHizMapArgs1 = new uint[3];
    uint[] BuildHizMapArgs2 = new uint[3];
    
    

    private BuildHiZMapSetting Setting;
    public BuildHizMapRenderPass(BuildHiZMapSetting setting)
    {
        this.Setting = setting;
        InitCallback();
    }
    private void InitCallback()
    {
        Debug.Log("InitCallback");

        CS_BuildHizMap = Setting.HizComputeShader;

        if (SystemInfo.usesReversedZBuffer)
        {
            CS_BuildHizMap.EnableKeyword("_REVERSE_Z");
        }
        else
        {
            CS_BuildHizMap.DisableKeyword("_REVERSE_Z");
        }
        
        _command = new CommandBuffer();
        _command.name = "BuildHizMap";
        KN_BuildHizMap = CS_BuildHizMap.FindKernel("BuildHizMap");

        mDispatchArgsBuffer = new ComputeBuffer(3, 4, ComputeBufferType.IndirectArguments);

        //2488-1080 -> 2048x1024 1024x512 512x256 256x128
        InputDepthMapSize0.x = Screen.width;
        InputDepthMapSize0.y = Screen.height;
        InputDepthMapSize0.z = 4096;//4096
        InputDepthMapSize0.w = 2048;//2048

        BuildHizMapArgs0[0] = (uint)4096 / 32;//128
        BuildHizMapArgs0[1] = (uint)2048 / 16;//128
        BuildHizMapArgs0[2] = 1;

        //256x128 -> 128x64 64x32 32x16 16x8
        InputDepthMapSize1.x = 256;//256
        InputDepthMapSize1.y = 128;//128
        InputDepthMapSize1.z = 256;
        InputDepthMapSize1.w = 128;

        BuildHizMapArgs1[0] = (uint)256 / 32;
        BuildHizMapArgs1[1] = (uint)128 / 16;
        BuildHizMapArgs1[2] = 1;

        //16x8 -> 8x4 4x2 2x1 1x1
        InputDepthMapSize2.x = 32;//16
        InputDepthMapSize2.y = 16;//8
        InputDepthMapSize2.z = 32;
        InputDepthMapSize2.w = 16;

        BuildHizMapArgs2[0] = (uint)1;//1
        BuildHizMapArgs2[1] = (uint)1;//1
        BuildHizMapArgs2[2] = 1;

        //RenderTextureDescriptor inputDepthMapDesc0 = new RenderTextureDescriptor((int)InputDepthMapSize0.x, (int)InputDepthMapSize0.y, RenderTextureFormat.RFloat, 0, 1);
        //inputDepthMap0 = RenderTexture.GetTemporary(inputDepthMapDesc0);
        //inputDepthMap0.filterMode = FilterMode.Point;

        RenderTextureDescriptor inputDepthMapDesc1 = new RenderTextureDescriptor((int)InputDepthMapSize1.x, (int)InputDepthMapSize1.y, RenderTextureFormat.RFloat, 0, 1);
        inputDepthMap1 = RenderTexture.GetTemporary(inputDepthMapDesc1);
        inputDepthMap1.filterMode = FilterMode.Point;
        inputDepthMap1.Create();

        RenderTextureDescriptor inputDepthMapDesc2 = new RenderTextureDescriptor((int)InputDepthMapSize2.x, (int)InputDepthMapSize2.y, RenderTextureFormat.RFloat, 0, 1);
        inputDepthMap2 = RenderTexture.GetTemporary(inputDepthMapDesc2);
        inputDepthMap2.filterMode = FilterMode.Point;
        inputDepthMap2.Create();

        RenderTextureDescriptor HizMapDesc = new RenderTextureDescriptor(2048, 1024, RenderTextureFormat.RFloat, 0, mipCount);
        HizMapDesc.useMipMap = true;
        HizMapDesc.autoGenerateMips = false;
        HizMapDesc.enableRandomWrite = true;
        HiZData.GetInstance().HIZ_MAP = RenderTexture.GetTemporary(HizMapDesc);
        HiZData.GetInstance().HIZ_MAP.filterMode = FilterMode.Point;
        HiZData.GetInstance().HIZ_MAP.Create();
    }
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        
    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if(CS_BuildHizMap == null)
        {
            return;
        }
        _command.Clear();
        RTHandle depthRTHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;
        BuildHizMap(_command, depthRTHandle);

        context.ExecuteCommandBuffer(_command);
    }

    // Cleanup any allocated resources that were created during the execution of this render pass.
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
    }

    /// <summary>
    /// 根据深度图，生成Hizmap
    /// </summary>
    public void BuildHizMap(CommandBuffer command, RTHandle depthRTHandle)
    {
        if (CS_BuildHizMap == null)
        {
            return;
        }

        //////////////////////////
        command.SetComputeVectorParam(CS_BuildHizMap, "inputDepthMapSize", InputDepthMapSize0);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "inputDepthMap", depthRTHandle);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip0", HiZData.GetInstance().HIZ_MAP, 0);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip1", HiZData.GetInstance().HIZ_MAP, 1);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip2", HiZData.GetInstance().HIZ_MAP, 2);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip3", HiZData.GetInstance().HIZ_MAP, 3);

        command.SetBufferData(mDispatchArgsBuffer, BuildHizMapArgs0);
        command.DispatchCompute(CS_BuildHizMap, KN_BuildHizMap, mDispatchArgsBuffer, 0);



        ////////////////////////
        command.SetComputeVectorParam(CS_BuildHizMap, "inputDepthMapSize", InputDepthMapSize1);
        command.CopyTexture(HiZData.GetInstance().HIZ_MAP, 0, 3,inputDepthMap1,0, 0);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "inputDepthMap", inputDepthMap1);

        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip0", HiZData.GetInstance().HIZ_MAP, 4);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip1", HiZData.GetInstance().HIZ_MAP, 5);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip2", HiZData.GetInstance().HIZ_MAP, 6);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip3", HiZData.GetInstance().HIZ_MAP, 7);//16x8

        command.SetBufferData(mDispatchArgsBuffer, BuildHizMapArgs1);
        command.DispatchCompute(CS_BuildHizMap, KN_BuildHizMap, mDispatchArgsBuffer, 0);



        ///////////////////////////
        command.SetComputeVectorParam(CS_BuildHizMap, "inputDepthMapSize", InputDepthMapSize2);
        command.CopyTexture(HiZData.GetInstance().HIZ_MAP, 0, 6, inputDepthMap2, 0, 0);//thread num in group is large than texture, so this mip is 6 not 7
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "inputDepthMap", inputDepthMap2);

        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip0", HiZData.GetInstance().HIZ_MAP, 7);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip1", HiZData.GetInstance().HIZ_MAP, 8);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip2", HiZData.GetInstance().HIZ_MAP, 9);
        command.SetComputeTextureParam(CS_BuildHizMap, KN_BuildHizMap, "HIZ_MAP_Mip3", HiZData.GetInstance().HIZ_MAP, 10);

        command.SetBufferData(mDispatchArgsBuffer, BuildHizMapArgs2);
        command.DispatchCompute(CS_BuildHizMap, KN_BuildHizMap, mDispatchArgsBuffer, 0);
        //此时Hizmap已经写入
    }

    public void Dispose()
    {
        if (CS_BuildHizMap == null)
        {
            return;
        }
        _command.Dispose();
        //RenderTexture.ReleaseTemporary(inputDepthMap0);
        RenderTexture.ReleaseTemporary(inputDepthMap1);
        RenderTexture.ReleaseTemporary(inputDepthMap2);
        mDispatchArgsBuffer.Dispose();
    }

}


