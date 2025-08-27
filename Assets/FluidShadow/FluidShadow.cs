using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Reflection;
using System.Linq;

public class FluidShadow : MonoBehaviour
{
    [SerializeField] private GameObject followObject;
    [SerializeField] private RenderTexture rt;
    [SerializeField] private ComputeShader cs;
    [SerializeField] private RenderTexture visualizeResult;
    
    // 对象池管理字段
    [Header("Shadow Caster Settings")]
    [SerializeField] private bool castsShadows = true;
    [SerializeField] private bool selfShadows = true;
    [SerializeField] private bool simulation = true;
    [SerializeField] private int frequency = 30;
    [SerializeField] private int MaxExistShadowEntity = 8;
    
    // 对象池相关字段
    private List<GameObject> contourObjects = new List<GameObject>();
    private Queue<GameObject> contourPool = new Queue<GameObject>();
    
    // 反射字段缓存（用于设置ShadowCaster2D的私有字段）
    private static FieldInfo layerField;
    private static FieldInfo shapePathField;
    private static FieldInfo shapePathHashField;
    private static FieldInfo previousShapePathHashField;
    private static FieldInfo meshField;
    private static FieldInfo localBoundsField;
    // private static MethodInfo generateShadowMeshMethod;
    
    bool hasReq = false;
    ComputeShaderRequset req;
    ShadowMeshJobHelper.JobData[] jobDatas;
    List<List<Vector3>> lastFrameContours;
    bool hasRequest = false;
    float lastTime = 0;
    // private ComputeBuffer BoolBuffer;

    // Start is called before the first frame update
    void Start()
    {
        InitializeReflectionFields();
    }

    private void InitializeReflectionFields()
    {
        System.Type shadowCasterType = typeof(ShadowCaster2D);
        
        layerField = shadowCasterType.GetField("m_ApplyToSortingLayers", BindingFlags.NonPublic | BindingFlags.Instance);
        shapePathField = shadowCasterType.GetField("m_ShapePath", BindingFlags.NonPublic | BindingFlags.Instance);
        shapePathHashField = shadowCasterType.GetField("m_ShapePathHash", BindingFlags.NonPublic | BindingFlags.Instance);
        previousShapePathHashField = typeof(ShadowCaster2D).GetField("m_PreviousPathHash", BindingFlags.NonPublic | BindingFlags.Instance);
        meshField = shadowCasterType.GetField("m_Mesh", BindingFlags.NonPublic | BindingFlags.Instance);
        localBoundsField = typeof(ShadowCaster2D).GetField("m_LocalBounds", BindingFlags.NonPublic | BindingFlags.Instance);
        // generateShadowMeshMethod = typeof(ShadowCaster2D).Assembly.GetType("UnityEngine.Rendering.Universal.ShadowUtility").GetMethod("GenerateShadowMesh", BindingFlags.Public | BindingFlags.Static);
    }

    void Update()
    {
        Mesh[] meshs = null;
        Bounds[] bounds = null;
        //var sw = System.Diagnostics.Stopwatch.StartNew();
        if (jobDatas != null && jobDatas.Length > 0)
        {
            meshs = new Mesh[jobDatas.Length];
            bounds = new Bounds[jobDatas.Length];
            for (int i = 0; i < jobDatas.Length; i++)
            {
                jobDatas[i].handle.Complete();
                ShadowMeshJobHelper.MeshData meshdata = jobDatas[i].GetResult();  
                meshs[i] = new Mesh();
                meshdata.ApplyToMesh(meshs[i]);
                bounds[i] = meshdata.bounds;
            }
            jobDatas = null;
        }

        if (meshs != null && meshs.Length > 0 && lastFrameContours != null && lastFrameContours.Count > 0 && bounds != null && bounds.Length > 0)
        {
            CreateShadowCaster2D(lastFrameContours, meshs, bounds);
        }

        lastFrameContours = null;

        int[] boolData;

        if (hasReq)
        {
            boolData = Stage1(req);
            hasReq = false;
            List<List<Vector3>> contours = Stage2(boolData, (rt.width / 4) - 1 , (rt.height / 4) - 1);
            List<List<Vector3>> filtedContours = contours.Where(c => c.Count <= 1000).OrderByDescending(c => c.Count).Take(MaxExistShadowEntity).ToList();
            List<List<Vector3>> simplifiedContours = SimplifyContours(filtedContours, 0.1f);

            if (simplifiedContours.Count > 0)
            {
                jobDatas = new ShadowMeshJobHelper.JobData[simplifiedContours.Count];
                for (int i = 0; i < simplifiedContours.Count; i++)
                {
                    jobDatas[i] = ShadowMeshJobHelper.AsyncMeshGen(simplifiedContours[i].ToArray());
                }
                lastFrameContours = simplifiedContours;
            }
            else
            {
                Mesh[] temp1 = null;
                Bounds[] temp2 = null;
                CreateShadowCaster2D(new List<List<Vector3>>(), temp1, temp2);
            }
        }

        if (!hasReq && simulation && lastTime >= 1.0f /  (float)frequency)
        {
            this.transform.position = followObject.transform.position;
            req = Stage0(rt);
            hasReq = true;
        }

        if (lastTime >= (1.0f / (float)frequency))
        {
            lastTime = 0;
        }

        lastTime += Time.deltaTime;
    }


    void OnDestroy()
    {
        if (req.buffer != null)
        {
            req.buffer.Release();
        }

        if (jobDatas != null)
        {
            for (int i = 0; i < jobDatas.Length; i++)
            {
                jobDatas[i].handle.Complete();
                ShadowMeshJobHelper.MeshData meshdata = jobDatas[i].GetResult();  
            }
        }
    
        // 清理对象池
        CleanupObjectPool();
    }

    void VisualizeBool(int[] boolData, int width, int height)
    {
        if (visualizeResult == null || visualizeResult.width != width || visualizeResult.height != height)
        {
            visualizeResult = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        }

        Texture2D temp = new Texture2D(width, height, TextureFormat.RGB24, false);
        Color[] colors = new Color[width * height];
        
        for (int i = 0; i < boolData.Length; i++)
        {
            if (boolData[i] == 0)
            {
                colors[i] = Color.black;
            }
            else if (boolData[i] <= 14)
            {
                colors[i] = new Color(boolData[i] * 0.07f, 0.0f, 0.0f, 1.0f);
            }
            else
            {
                colors[i] = Color.white;
            }
        }
        
        temp.SetPixels(colors);
        temp.Apply();
        
        Graphics.Blit(temp, visualizeResult);
        DestroyImmediate(temp);
    }
    
    private void CleanupObjectPool()
    {
        // 销毁所有活动的轮廓对象
        foreach (GameObject obj in contourObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        contourObjects.Clear();
        
        // 销毁对象池中的对象
        while (contourPool.Count > 0)
        {
            GameObject obj = contourPool.Dequeue();
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
    }

    public void CreateShadowCaster2D(List<List<Vector3>> contours, Mesh[] meshes, Bounds[] bounds)
    {
        if (contours == null) return;
        
        // 确保mesh数组长度与contours匹配
        int validContourCount = 0;
        for (int i = 0; i < contours.Count && i < meshes.Length; i++)
        {
            if (contours[i] != null && contours[i].Count >= 3)
            {
                validContourCount++;
            }
        }
        
        // 1. 激活并更新所需数量的轮廓对象
        int contourIndex = 0;
        for (int i = 0; i < contours.Count && i < meshes.Length; i++)
        {
            List<Vector3> contour = contours[i];
            if (contour == null || contour.Count < 3) continue; // Shadow Caster 2D需要至少3个点

            GameObject contourObj;
            if (contourIndex < contourObjects.Count)
            {
                // 复用现有的活动对象
                contourObj = contourObjects[contourIndex];
                contourObj.SetActive(true);
            }
            else
            {
                // 如果现有活动对象不够，从对象池取或创建新的
                if (contourPool.Count > 0)
                {
                    contourObj = contourPool.Dequeue();
                    contourObj.SetActive(true);
                }
                else
                {
                    contourObj = new GameObject();
                    contourObj.AddComponent<ShadowCaster2D>();
                }
                contourObjects.Add(contourObj); // 将新对象添加到活动列表
            }

            // --- 更新 ShadowCaster2D 属性 ---
            contourObj.name = $"MooreShadowContour_{contourIndex}";
            contourObj.transform.SetParent(transform);
            contourObj.transform.localPosition = Vector3.zero;

            // // 转换坐标系（根据你的需求调整缩放和偏移）
            Vector3[] shapePath = new Vector3[contour.Count];
            for (int j = 0; j < contour.Count; j++)
            {
                shapePath[j] = new Vector3(contour[j].x, contour[j].y, 0f);
            }

            // 设置ShadowCaster2D组件
            ShadowCaster2D shadowCaster = contourObj.GetComponent<ShadowCaster2D>();
            shadowCaster.castsShadows = castsShadows;
            shadowCaster.selfShadows = selfShadows;

            // 使用反射设置私有字段
            if (layerField != null) layerField.SetValue(shadowCaster, new int[] { 0 });
            if (shapePathField != null) shapePathField.SetValue(shadowCaster, shapePath);
            int rand = 69;//Random.Range(int.MinValue, int.MaxValue);
            if (shapePathHashField != null) shapePathHashField.SetValue(shadowCaster, rand);
            if (previousShapePathHashField != null) previousShapePathHashField.SetValue(shadowCaster, rand);
            
            // 设置网格
            if (meshField != null && meshes[i] != null)
            {
                meshField.SetValue(shadowCaster, meshes[i]);
            }
            if (localBoundsField != null && bounds[i] != null)
            {
                localBoundsField.SetValue(shadowCaster, bounds[i]);
            }

            contourIndex++;
        }

        // 2. 将当前帧不再需要的、多余的活动对象禁用并回收到对象池
        int requiredCount = validContourCount;
        int currentActiveCount = contourObjects.Count;
        if (currentActiveCount > requiredCount)
        {
            for (int i = currentActiveCount - 1; i >= requiredCount; i--)
            {
                GameObject unusedObj = contourObjects[i];
                unusedObj.SetActive(false);
                contourPool.Enqueue(unusedObj);
                contourObjects.RemoveAt(i);
            }
        }
    }

    public struct ComputeShaderRequset
    {
        public UnityEngine.Rendering.AsyncGPUReadbackRequest request;
        public ComputeBuffer buffer;
    }

    ComputeShaderRequset Stage0(RenderTexture rt)
    {
        int targetWidth = (rt.width / 4) - 1;
        int targetHeight = (rt.height / 4) - 1;
        int totalPixels = targetWidth * targetHeight;

        ComputeBuffer BoolBuffer = new ComputeBuffer(totalPixels, sizeof(int));
        int kernelHandle = cs.FindKernel("MarchingSquareExtract");
        cs.SetBuffer(kernelHandle, "BoolOutput", BoolBuffer);
        cs.SetTexture(kernelHandle, "InputTexture", rt);
        cs.SetInts("TargetSize", targetWidth, targetHeight);
        cs.SetInts("InputSize", rt.width, rt.height);
        cs.SetFloat("threshold", 1.01f);
        
        cs.Dispatch(kernelHandle, targetWidth, targetHeight, 1);
        var request = UnityEngine.Rendering.AsyncGPUReadback.Request(BoolBuffer);

        ComputeShaderRequset req = new ComputeShaderRequset();
        req.request = request;
        req.buffer = BoolBuffer;
        return req;
    }

    int[] Stage1(ComputeShaderRequset req)
    {
        int targetWidth = (rt.width / 4) - 1;
        int targetHeight = (rt.height / 4) - 1;
        int totalPixels = targetWidth * targetHeight;

        req.request.WaitForCompletion();
        int[] boolData = new int[totalPixels];
        if (!req.request.hasError)
        {
            boolData = req.request.GetData<int>().ToArray();
        }   
        req.buffer.Release();
        return boolData;
    }

    List<List<Vector3>> Stage2(int[] BoolData, int width, int height)
    {
        List<List<Vector3>> contours = new List<List<Vector3>>();
        bool[,] visited = new bool[width, height];
        
        int prevValue = 0;
        
        // 跳点扫描：每3个像素扫描一次
        for (int y = 0; y < height; y += 3)
        {
            prevValue = 0; // 每行开始重置
            
            for (int x = 0; x < width; x += 3)
            {
                int index = y * width + x;
                int currentValue = BoolData[index];
                
                // 检测数值变化
                if (currentValue != prevValue)
                {
                    if (prevValue == 0 && currentValue >= 1 && currentValue <= 14)
                    {
                        // 0->1~14：直接找到边缘，开始追踪
                        if (!visited[x, y])
                        {
                            //Debug.LogError("0->1~14");
                            List<Vector3> contour = TraceMarchingSquareContour(BoolData, width, height, x, y, visited);
                            if (contour.Count >= 3)
                            {
                                contours.Add(contour);
                            }
                        }
                    }
                    else if (prevValue == 0 && currentValue == 15)
                    {
                        // 0->15：跨过了边缘，回溯查找
                        for (int backX = x - 3; backX < x; backX++)
                        {
                            if (backX >= 0 && backX < width)
                            {
                                int backIndex = y * width + backX;
                                if (BoolData[backIndex] == 2 && !visited[backX, y])
                                {
                                    //Debug.LogError("0->15");
                                    List<Vector3> contour = TraceMarchingSquareContour(BoolData, width, height, backX, y, visited);
                                    if (contour.Count >= 3)
                                    {
                                        contours.Add(contour);
                                    }
                                    break; // 找到一个边缘点就够了
                                }
                            }
                        }
                    }
                }
                
                prevValue = currentValue;
            }
        }

        // 替换第435-438行的代码
        for (int i = 0; i < contours.Count; i++)
        {
            var contour = contours[i];
            for (int j = 0; j < contour.Count; j++)
            {
                Vector3 p = contour[j];
                contour[j] = new Vector3(p.x * 0.1f, p.y * 0.1f, 0f) - new Vector3(16f, 9f, 0f);
            }
        }
        
        //Debug.LogError(contours.Count);
        return contours;
    }

    static readonly int[,] marchingSquareTable = new int[16, 4]
    {
        {-1, -1, -1, -1}, // case0
        { 1,  2, -1, -1}, // case1
        { 0,  1, -1, -1}, // case2
        { 0,  2, -1, -1}, // case3
        { 0,  3, -1, -1}, // case4
        { 2,  3,  0,  1}, // case5
        { 1,  3, -1, -1}, // case6
        { 2,  3, -1, -1}, // case7
        { 2,  3, -1, -1}, // case8
        { 1,  3, -1, -1}, // case9
        { 1,  2,  0,  3}, // case10
        { 0,  3, -1, -1}, // case11
        { 0,  2, -1, -1}, // case12
        { 0,  1, -1, -1}, // case13
        { 1,  2, -1, -1}, // case14
        {-1, -1, -1, -1}, // case15
    };

    List<Vector3> TraceMarchingSquareContour(int[] IntData, int width, int height, int startX, int startY, bool[,] visited)
    {
        List<Vector3> contour = new List<Vector3>();

        int currentX = startX;
        int currentY = startY;
        int direction = 0;

        Vector2Int[] dxdy = new Vector2Int[4]
        {
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
        };

        Vector3 startPoint = new Vector3(startX, startY, 0);
        contour.Add(startPoint);

        int StartIndex = startX + startY * width;
        int StartConfig = IntData[StartIndex];

        direction = marchingSquareTable[StartConfig, 0];

        do
        {
            int nextIndex = currentX + dxdy[direction].x + (currentY + dxdy[direction].y) * width;
            int nextConfig = IntData[nextIndex];
            int lastDirection = direction;
            direction = FindNextDirection(nextConfig, (direction + 2) % 4);

            currentX += dxdy[lastDirection].x;
            currentY += dxdy[lastDirection].y;
            contour.Add(new Vector3(currentX, currentY, 0));

            if (nextConfig != 5 && nextConfig != 10)
            {
                visited[currentX, currentY] = true;
            }

        } while(!(currentX == startX && currentY == startY) && contour.Count < 10000);

        return contour;
    }

    int FindNextDirection(int Config, int InDir)
    {
        for(int i = 0; i < 4; i++)
        {
            if(marchingSquareTable[Config, i] == InDir)
            {
                switch(i)
                {
                    case 0:
                        return marchingSquareTable[Config, 1];
                    case 1:
                        return marchingSquareTable[Config, 0];
                    case 2:
                        return marchingSquareTable[Config, 3];
                    case 3:
                        return marchingSquareTable[Config, 2];
                    default:
                        return -1;
                }
            }
        }
        return -2;
    }

    List<List<Vector3>> SimplifyContours(List<List<Vector3>> contours, float epsilon)
    {
        List<List<Vector3>> simplifiedContours = new List<List<Vector3>>();

        foreach (var contour in contours)
        {
            if (contour.Count <= 2)
            {
                // 点数太少，直接保留
                simplifiedContours.Add(new List<Vector3>(contour));
                continue;
            }

            // 使用 Douglas-Peucker 算法简化轮廓
            List<Vector3> simplified = DouglasPeucker(contour, epsilon);
            
            // 确保简化后至少有3个点（形成有效多边形）
            if (simplified.Count >= 3)
            {
                simplifiedContours.Add(simplified);
            }
        }

        return simplifiedContours;
    }

    // Douglas-Peucker 算法实现
    List<Vector3> DouglasPeucker(List<Vector3> points, float epsilon)
    {
        if (points.Count <= 2)
            return new List<Vector3>(points);

        // 找到距离起点和终点连线最远的点
        float maxDistance = 0;
        int maxIndex = 0;
        Vector3 start = points[0];
        Vector3 end = points[points.Count - 1];

        for (int i = 1; i < points.Count - 1; i++)
        {
            float distance = PointToLineDistance(points[i], start, end);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }

        List<Vector3> result = new List<Vector3>();

        // 如果最大距离大于阈值，递归处理
        if (maxDistance > epsilon)
        {
            // 递归处理前半段
            List<Vector3> firstHalf = new List<Vector3>();
            for (int i = 0; i <= maxIndex; i++)
            {
                firstHalf.Add(points[i]);
            }
            List<Vector3> rec1 = DouglasPeucker(firstHalf, epsilon);

            // 递归处理后半段
            List<Vector3> secondHalf = new List<Vector3>();
            for (int i = maxIndex; i < points.Count; i++)
            {
                secondHalf.Add(points[i]);
            }
            List<Vector3> rec2 = DouglasPeucker(secondHalf, epsilon);

            // 合并结果（避免重复中间点）
            result.AddRange(rec1);
            for (int i = 1; i < rec2.Count; i++)
            {
                result.Add(rec2[i]);
            }
        }
        else
        {
            // 距离都小于阈值，只保留起点和终点
            result.Add(start);
            result.Add(end);
        }

        return result;
    }

    // 计算点到直线的距离
    float PointToLineDistance(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        // 使用向量叉积计算点到直线距离
        Vector3 lineVec = lineEnd - lineStart;
        Vector3 pointVec = point - lineStart;
        
        // 2D 叉积的模长就是平行四边形面积
        float crossProduct = Mathf.Abs(lineVec.x * pointVec.y - lineVec.y * pointVec.x);
        float lineLength = lineVec.magnitude;
        
        if (lineLength < 0.0001f)
            return Vector3.Distance(point, lineStart);
        
        return crossProduct / lineLength;
    }
}
