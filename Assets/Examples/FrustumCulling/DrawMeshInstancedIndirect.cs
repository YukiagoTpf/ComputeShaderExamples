using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Copy from https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstancedIndirect.html
public class DrawMeshInstancedIndirect : MonoBehaviour
{
    
    public int instanceCount = 100000;
    public Mesh instanceMesh;
    public Material instanceMaterial;
    public int subMeshIndex = 0;
    public Vector4 boundSize;
    private int cachedInstanceCount = -1;
    private int cachedSubMeshIndex = -1;
    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    
    public ComputeShader computeShader;
    Camera mainCamera;
    List<Matrix4x4> LToWMatrixCollection = new List<Matrix4x4>();
    ComputeBuffer LToWMatrixBuffer;
    ComputeBuffer FrustumCullResult;
    int kernelIndex;
    //Get Frustum Plane
    
    #region GetFrustumPlane
    //如何获取相机视锥体6个面的信息？
    //平面方程：Ax+By+Cz+D=0
    //在Unity中，1个Vector4表示一个平面的Plane结构。Vector4的xyz分别对应法向量的x、y、z分量，和一个浮点数，数学意义代表平面上任意一点在法向量方向上的投影长度。
    //So,一个点+一个法向量确定一个Plane
    //这个方法足以确定近裁面和远裁面的信息，其他四个面可以通过三点来求平面（相机点，远平面两个点or近平面两个点）
    public static Vector4 GetPlane(Vector3 normal, Vector3 point)
    {
        return new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
    }
    public static Vector4 GetPlanebyThreePoint(Vector3 point1, Vector3 point2, Vector3 point3)
    {
        // 计算两个向量
        Vector3 vector1 = point2 - point1;
        Vector3 vector2 = point3 - point1;
        // 计算法向量（叉乘）
        Vector3 normal = Vector3.Normalize(Vector3.Cross(vector1, vector2));
        return GetPlane(normal,point1);
    }
    //接下来来正式获取视锥体
    //首先获取远裁面四个点信息
    public static Vector3[] GetFarClipPlanePoint(Camera camera)
    {
        float zFar = camera.farClipPlane;
        float halfFov = Mathf.Deg2Rad * camera.fieldOfView * 0.5f;
        float halfHigh = zFar * Mathf.Tan(halfFov) ;
        float halfWidth = halfHigh * camera.aspect;
        Vector3 CenterPoint = camera.transform.position + zFar * camera.transform.forward;
        Vector3 RightVector = camera.transform.right * halfWidth;
        Vector3 UpVector = camera.transform.up * halfHigh;
        Vector3[] points = new Vector3[4];
        points[0] = CenterPoint - UpVector - RightVector;//left-bottom
        points[1] = CenterPoint - UpVector + RightVector;//right-bottom
        points[2] = CenterPoint + UpVector - RightVector;//left-up
        points[3] = CenterPoint + UpVector + RightVector;//right-up
        return points;
    }
    
    public static Vector4[] GetFrustumPlane(Camera camera)
    {
        Vector4[] planes = new Vector4[6];
        Transform camTransform = camera.transform;
        Vector3[] farPlanPoints = GetFarClipPlanePoint(camera);
        planes[0] = GetPlanebyThreePoint(camTransform.position,farPlanPoints[0],farPlanPoints[2]);//Left
        planes[1] = GetPlanebyThreePoint(camTransform.position,farPlanPoints[3],farPlanPoints[1]);//Right
        planes[2] = GetPlanebyThreePoint(camTransform.position,farPlanPoints[1],farPlanPoints[0]);//Bottom
        planes[3] = GetPlanebyThreePoint(camTransform.position,farPlanPoints[2],farPlanPoints[3]);//Top
        planes[4] = GetPlane(-camTransform.forward,camTransform.position + camTransform.forward * camera.nearClipPlane);//nearClipPlane
        planes[5] = GetPlane(camTransform.forward,camTransform.position + camTransform.forward * camera.farClipPlane);//farClipPlane
        return planes;
    }

    #endregion
    

    void Start() 
    {
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        mainCamera = Camera.main;
        kernelIndex = computeShader.FindKernel("FrustumCulling");
        FrustumCullResult = new ComputeBuffer(instanceCount, sizeof(float)*16, ComputeBufferType.Append);
        computeShader.SetVector("boundSizeInput",boundSize);
        UpdateBuffers();
    }

    void Update() {
        // Update starting position buffer
        if (cachedInstanceCount != instanceCount || cachedSubMeshIndex != subMeshIndex)
            UpdateBuffers();
        Vector4[] FrustumPlane = GetFrustumPlane(mainCamera);
       
        computeShader.SetBuffer(kernelIndex,"LocalToWorldinput",LToWMatrixBuffer);
        FrustumCullResult.SetCounterValue(0);
        computeShader.SetBuffer(kernelIndex,"result",FrustumCullResult);
        computeShader.SetInt("instanceCount",instanceCount);
        computeShader.SetVectorArray("FrustumPlane",FrustumPlane);
        computeShader.Dispatch(kernelIndex,1 + instanceCount/640,1,1);
        
        instanceMaterial.SetBuffer("PerInstandedLtoW", FrustumCullResult);
        //把Culling后的数量位移一个字节Copy到argsBuffer的第二个参数
        ComputeBuffer.CopyCount(FrustumCullResult,argsBuffer,sizeof(uint));
        // Render
        Graphics.DrawMeshInstancedIndirect(instanceMesh, subMeshIndex, instanceMaterial, new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), argsBuffer);
    }
    

    void UpdateBuffers() {
        // Ensure submesh index is in range
        if (instanceMesh != null)
            subMeshIndex = Mathf.Clamp(subMeshIndex, 0, instanceMesh.subMeshCount - 1);


        Vector4[] positions = new Vector4[instanceCount];
        
        if(LToWMatrixBuffer != null)
            LToWMatrixBuffer.Release();
        LToWMatrixBuffer = new ComputeBuffer(instanceCount, sizeof(float)* 16);
        LToWMatrixCollection.Clear();
        for (int i = 0; i < instanceCount; i++) {
            float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
            float distance = Random.Range(20.0f, 100.0f);
            float height = Random.Range(-2.0f, 2.0f);
            float size = Random.Range(0.05f, 0.25f);
            positions[i] = new Vector4(Mathf.Sin(angle) * distance, height, Mathf.Cos(angle) * distance, size);
            LToWMatrixCollection.Add(Matrix4x4.TRS(positions[i], Quaternion.identity, new Vector3(size,size,size)));
        }
        LToWMatrixBuffer.SetData(LToWMatrixCollection);


        // Indirect args
        if (instanceMesh != null) {
            args[0] = (uint)instanceMesh.GetIndexCount(subMeshIndex);
            args[1] = (uint)instanceCount;
            args[2] = (uint)instanceMesh.GetIndexStart(subMeshIndex);
            args[3] = (uint)instanceMesh.GetBaseVertex(subMeshIndex);
        }
        else
        {
            args[0] = args[1] = args[2] = args[3] = 0;
        }
        argsBuffer.SetData(args);

        cachedInstanceCount = instanceCount;
        cachedSubMeshIndex = subMeshIndex;
    }

    void OnDisable() {

        if (argsBuffer != null)
            argsBuffer.Release();
        argsBuffer = null;
        
        if (FrustumCullResult != null)
            FrustumCullResult.Release();
        FrustumCullResult = null;
        
        if (LToWMatrixBuffer != null)
            LToWMatrixBuffer.Release();
        LToWMatrixBuffer = null;
    }

}