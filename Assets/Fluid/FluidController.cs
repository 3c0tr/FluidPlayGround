using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class FluidController : MonoBehaviour
{
    private static FluidController instance;  
    public static FluidController Instance
    {
        get
        {
            return instance;
        }
    }
    public RenderTexture colorTexture;
    public RenderTexture velocityTexture;
    public RenderTexture obstacleTexture;
    public RenderTexture obstacleTexturePre;
    public Camera obstacleCamera;
    public Color drawColor = Color.black;
    public float brushSize = 5f;
    public float obstacleForceStrength = 5.0f; // 障碍物推力强度
    public Shader drawShader; // 用于绘制的着色器
    public Shader colorFieldShader;
    public Shader velocityFieldShader;
    public Shader offsetTextureShader;
    public bool simulation = true;
    private Vector2 FluidDomainOffset = new Vector2(0.0f, 0.0f);
    private Material drawMaterial;
    private Material colorFieldMaterial;
    private Material velocityFieldMaterial;
    private Material offsetTextureMaterial;
    private Texture2D brushTexture;
    [SerializeField] private Texture2D CustomTexture;
    private Renderer rend;
    private Bounds objectBounds;
    [SerializeField] private GameObject followObject;
    private Vector3 previousPosition;
    
    // 批处理绘制请求的数据结构
    private class DrawRequest
    {
        public Vector2 worldPos;
        public Color color;
        public Vector2 initialVelocity;
        public float colorRadius;    // 颜色场的绘制半径
        public float velocityRadius; // 速度场的绘制半径
        public VelocityType velocityType; // 新增速度类型字段
    }

    // 绘制请求队列
    private List<DrawRequest> drawRequests = new List<DrawRequest>(1000);

    // 缓存的临时RenderTexture
    private RenderTexture tempColorRT;
    private RenderTexture tempVelocityRT;

    // 添加到类的成员变量部分
    private bool useCommandBuffer = true; // 控制是否使用CommandBuffer优化
    private ComputeBuffer positionsBuffer; // 存储绘制位置的缓冲区
    private ComputeBuffer velocitiesBuffer; // 存储速度的缓冲区
    private ComputeBuffer radiiBuffer; // 存储颜色半径的缓冲区
    private ComputeBuffer velocityRadiiBuffer; // 存储速度半径的缓冲区
    private ComputeBuffer colorsBuffer; // 存储颜色的缓冲区
    private Material batchDrawMaterial; // 用于批量绘制的材质

    private Texture2D exploreTexture; // 新增爆炸纹理

    private ComputeBuffer velocityTypesBuffer; // 新增缓冲区

    // 在FluidController类的开始处添加枚举定义
    public enum VelocityType
    {
        Direct,     // 直接使用传入的速度
        Explore     // 使用爆炸纹理的速度场
    }

    // 在类中添加一个字段保存上一帧的 worldPoint
    private Vector2? lastWorldPoint = null;

    // Start is called before the first frame update
    void Start()
    {
        // 初始化绘制纹理
        if (colorTexture == null)
        {
            colorTexture = new RenderTexture(1280, 720, 24);
            colorTexture.enableRandomWrite = true;
            colorTexture.Create();
        }
        
        // 初始化障碍纹理
        if (obstacleTexture == null)
        {
            obstacleTexture = new RenderTexture(1280, 720, 24);
            obstacleTexture.enableRandomWrite = true;
            obstacleTexture.Create();
        }
        
        // 初始化前一帧障碍纹理
        if (obstacleTexturePre == null)
        {
            obstacleTexturePre = new RenderTexture(1280, 720, 24);
            obstacleTexturePre.enableRandomWrite = true;
            obstacleTexturePre.Create();
        }
        
        // 获取渲染器并设置纹理
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.enabled = true;
            // 创建材质的实例而不是使用共享材质
            Material materialInstance = new Material(rend.sharedMaterial);
            rend.material = materialInstance;
            
            if (materialInstance != null)
            {
                materialInstance.SetTexture("_ColorField", colorTexture);
                materialInstance.SetTexture("_VelocityField", velocityTexture);
                objectBounds = rend.bounds;
            }
        }
        
        // 初始化画笔纹理 - 创建一个渐变的圆形笔刷
        int brushTextureSize = 128;
        if (CustomTexture != null)
        {
            brushTexture = CustomTexture;
        }
        else
        {
            brushTexture = new Texture2D(brushTextureSize, brushTextureSize, TextureFormat.RGBA32, false);
            
            // 填充画笔纹理为渐变圆形
            for (int y = 0; y < brushTextureSize; y++)
            {
                for (int x = 0; x < brushTextureSize; x++)
                {
                    float distanceFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(brushTextureSize/2, brushTextureSize/2));
                    float normalizedDistance = distanceFromCenter / (brushTextureSize/2);
                    
                    // 创建从中心到边缘的渐变效果
                    float alpha = Mathf.Clamp01(1.0f - normalizedDistance);
                    alpha = Mathf.Pow(alpha, 2.0f); // 让渐变更加平滑
                    // float alpha = 0.0f;
                    // if (normalizedDistance < 1f)
                    // {
                    //     alpha = 1.0f;
                    // }
                    
                    brushTexture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            brushTexture.Apply();
        }

        // 初始化爆炸纹理
        int exploreTextureSize = 128;
        exploreTexture = new Texture2D(exploreTextureSize, exploreTextureSize, TextureFormat.RGBA32, false);
        
        Vector2 center = new Vector2(exploreTextureSize/2, exploreTextureSize/2);
        
        // 填充爆炸纹理
        for (int y = 0; y < exploreTextureSize; y++)
        {
            for (int x = 0; x < exploreTextureSize; x++)
            {
                Vector2 currentPos = new Vector2(x, y);
                Vector2 directionFromCenter = (currentPos - center).normalized;
                
                // 计算到中心的距离
                float distanceFromCenter = Vector2.Distance(currentPos, center);
                float normalizedDistance = distanceFromCenter / (exploreTextureSize/2);
                
                // 创建衰减效果
                float intensity = Mathf.Clamp01(1.0f - normalizedDistance);
                intensity = Mathf.Pow(intensity, 0.5f); // 调整衰减曲线
                intensity = 5f; // temp
                
                // 方向信息存储在RG通道（将[-1,1]映射到[0,1]范围）
                // R通道存储X方向，G通道存储Y方向
                float r = directionFromCenter.x * intensity * 0.5f + 0.5f;
                float g = directionFromCenter.y * intensity * 0.5f + 0.5f;
                
                // B通道可以用来存储强度，A通道设为1
                float b = intensity;
                float a = Mathf.Clamp01(1 - normalizedDistance);
                
                exploreTexture.SetPixel(x, y, new Color(r, g, b, a));
            }
        }
        
        exploreTexture.Apply();
        
        // 设置过滤模式为双线性过滤，使采样更平滑
        exploreTexture.filterMode = FilterMode.Bilinear;
        exploreTexture.wrapMode = TextureWrapMode.Clamp;

        
        if (drawShader == null)
        {
            Debug.LogError("未找到Custom/DrawBrush着色器");
        }
        if (colorFieldShader == null)
        {
            Debug.LogError("未找到Custom/ColorField着色器");
        }
        if (velocityFieldShader == null)
        {
            Debug.LogError("未找到Custom/VelocityField着色器");
        }
        if (offsetTextureShader == null)
        {
            Debug.LogError("未找到Custom/OffsetTexture着色器");
        }
        // 创建绘制材质
        drawMaterial = new Material(drawShader);

        // 创建颜色场材质
        colorFieldMaterial = new Material(colorFieldShader);

        // 创建速度场材质
        velocityFieldMaterial = new Material(velocityFieldShader);

        // 创建偏移场材质
        offsetTextureMaterial = new Material(offsetTextureShader);

        // 清除初始纹理为透明
        RenderTexture.active = colorTexture;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = null;

        // 清除初始速度场纹理为透明
        RenderTexture.active = velocityTexture;
        GL.Clear(true, true, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        RenderTexture.active = null;

        // 清除初始障碍纹理为透明（Alpha=0表示无障碍物）
        RenderTexture.active = obstacleTexture;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = null;

        // 清除前一帧障碍纹理为透明
        RenderTexture.active = obstacleTexturePre;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = null;

        // 初始化CommandBuffer资源
        InitCommandBufferResources();
        
        // 禁用障碍物摄像机的自动渲染，确保完全手动控制
        if (obstacleCamera != null)
        {
            obstacleCamera.enabled = false;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("场景中已存在FluidController实例，销毁当前实例");
            Destroy(this.gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        // return;
        if (followObject == null)
        {
            followObject = GameObject.Find("Main Camera");
            return;
        }

        rend = GetComponent<Renderer>();
        if (rend != null && rend.material != null)
        {
            objectBounds = rend.bounds;
        }

        Vector3 followPosition = followObject.transform.position;
        this.transform.position = new Vector3(followPosition.x, followPosition.y, this.transform.position.z);

        // 获取鼠标世界坐标
        // Vector2? worldPoint = GetMouseWorldPoint();
        // worldPoint = new Vector2(0.5f, 0.5f);
        // // return;
        // // 检查是否有有效的鼠标位置且在物体范围内
        // if (worldPoint.HasValue && IsPointInsideObject(worldPoint.Value))
        // {
        //     if (Input.GetMouseButton(0) && false) // 左键按下时绘制
        //     {
        //         float startTime = Time.realtimeSinceStartup;
        //         // DrawAtPoint(worldPoint.Value, Color.white, Vector2.zero, 1f);
                
        //         for (int i = 0; i < 1; i++) // 1024个绘制点每帧耗时0.5ms, 感觉很可以
        //         {
        //             for (int j = 0; j < 1; j++)
        //             {
        //                 // 使用不同的半径
        //                 if (worldPoint.HasValue && lastWorldPoint.HasValue)
        //                 {
        //                     Vector2 direction = (worldPoint.Value - lastWorldPoint.Value).normalized;
        //                     direction = new Vector2(-1f, 0.0f);
        //                     QueueDrawAtPoint(
        //                         worldPoint.Value + new Vector2((float)i * 0.1f, (float)j * 0.1f),
        //                         Color.white,
        //                         direction * 5f, // 方向向量，长度可调
        //                         0.7f,  // 颜色场半径
        //                         1f,  // 速度场半径
        //                         VelocityType.Direct
        //                     );
        //                 }
        //             }
        //         }
                
        //         float endTime = Time.realtimeSinceStartup;
        //         Debug.Log($"绘制耗时: {(endTime - startTime) * 1000:F2}毫秒");
        //     }

        //     // if (Input.GetMouseButton(1))
        //     // {
        //     //     DrawAtPoint(new Vector2(0.5f, 0.5f), true);
        //     // }
        // }

        // if (Input.GetKey(KeyCode.Q))
        // {
        //     DrawAtPoint(worldPoint.Value, Color.white, Vector2.zero, 1f);
        // }
        
        if (Input.GetKey(KeyCode.R))
        {
            ClearTexture();
        }

        FluidDomainOffset = (this.transform.position - previousPosition);// 0.03333f;
        FluidDomainOffset = new Vector2(
            FluidDomainOffset.x * 0.03125f, 
            FluidDomainOffset.y * 0.05555f
        );
        previousPosition = this.transform.position;

        UpdateObstacleTexture();

        UpdateOffsetField(colorTexture, FluidDomainOffset);
        UpdateOffsetField(velocityTexture, FluidDomainOffset);

        if (simulation)
        {
            UpdateColorField();
            UpdateVelocityField();
            UpdateVelocityField();
            UpdateVelocityField();
        }

        if (simulation)
        {
            Graphics.Blit(obstacleTexture, obstacleTexturePre);
        }
    }
    
    // 获取鼠标在世界空间中的XY坐标
    private Vector2? GetMouseWorldPoint()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -Camera.main.transform.position.z; // 设置z深度为摄像机到z=0平面的距离
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        return new Vector2(worldPos.x, worldPos.y);
    }
    
    // 判断点是否在物体范围内
    private bool IsPointInsideObject(Vector2 point)
    {
        // 检查点是否在物体的边界框内
        return objectBounds.Contains(new Vector3(point.x, point.y, transform.position.z));
    }
    
    // 将世界坐标转换为UV坐标
    private Vector2 WorldToUV(Vector2 worldPos)
    {
        // 计算相对于物体边界的位置
        float u = (worldPos.x - objectBounds.min.x) / objectBounds.size.x;
        float v = (worldPos.y - objectBounds.min.y) / objectBounds.size.y;
        
        return new Vector2(u, v);
    }
    

    public void QueueDrawAtPoint(Vector2 worldPos, Color color, Vector2 initialVelocity, float colorRadius, float velocityRadius, VelocityType velocityType = VelocityType.Direct)
    {
        drawRequests.Add(new DrawRequest
        {
            worldPos = worldPos,
            color = color,
            initialVelocity = initialVelocity,
            colorRadius = colorRadius,
            velocityRadius = velocityRadius,
            velocityType = velocityType
        });
    }

    // 在每帧末尾批处理所有绘制请求
    private void LateUpdate()
    {
        if (drawRequests.Count > 0)
        {
            float startTime = Time.realtimeSinceStartup;
            
            if (useCommandBuffer)
            {
                ProcessDrawRequestsWithCommandBuffer();
            }
            else
            {
                InitTemporaryTextures();
                ProcessDrawRequestsBatch();
            }
            
            drawRequests.Clear();
            
            float endTime = Time.realtimeSinceStartup;
            //Debug.Log($"绘制处理耗时: {(endTime - startTime) * 1000:F2}ms, 使用CommandBuffer: {useCommandBuffer}");
        }
    }

    // 批量处理所有绘制请求
    private void ProcessDrawRequestsBatch()
    {
        // 复制当前内容到临时RT
        Graphics.Blit(colorTexture, tempColorRT);
        Graphics.Blit(velocityTexture, tempVelocityRT);
        
        // 处理所有颜色绘制请求
        foreach (var request in drawRequests)
        {
            Vector2 uv = WorldToUV(request.worldPos) - FluidDomainOffset;
            Debug.LogError("FluidDomainOffset: " + FluidDomainOffset);
            
            // 设置颜色绘制参数，使用colorRadius
            drawMaterial.SetColor("_Color", request.color);
            drawMaterial.SetFloat("_BrushSize", brushSize * request.colorRadius); // 使用颜色半径
            drawMaterial.SetVector("_BrushPos", new Vector4(uv.x, uv.y, 0, 0));
            drawMaterial.SetFloat("_NOT_DRAW_ALPHA", 0.0f);
            drawMaterial.SetTexture("_MainTex", tempColorRT);
        
            // 绘制到颜色纹理
            Graphics.Blit(tempColorRT, colorTexture, drawMaterial);
            Graphics.Blit(colorTexture, tempColorRT); // 更新临时RT为最新结果
        }
        
        // 再处理所有速度绘制请求
        foreach (var request in drawRequests)
        {
            Vector2 uv = WorldToUV(request.worldPos) - FluidDomainOffset;
            
            Color velocityColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            velocityColor.r += request.initialVelocity.x * 0.05f;
            velocityColor.g += request.initialVelocity.y * 0.05f;
            
            // 设置速度绘制参数，使用velocityRadius
            drawMaterial.SetColor("_Color", velocityColor);
            drawMaterial.SetFloat("_BrushSize", brushSize * request.velocityRadius); // 使用速度半径
            drawMaterial.SetVector("_BrushPos", new Vector4(uv.x, uv.y, 0, 0));
            drawMaterial.SetFloat("_NOT_DRAW_ALPHA", 1.0f);
            drawMaterial.SetTexture("_MainTex", tempVelocityRT);
        
            // 绘制到速度纹理
            Graphics.Blit(tempVelocityRT, velocityTexture, drawMaterial);
            Graphics.Blit(velocityTexture, tempVelocityRT); // 更新临时RT为最新结果
        }
    }

    // 在引擎启动时初始化临时纹理
    private void InitTemporaryTextures()
    {
        if (tempColorRT == null)
            tempColorRT = RenderTexture.GetTemporary(colorTexture.width, colorTexture.height, 0, colorTexture.format);
        if (tempVelocityRT == null)
            tempVelocityRT = RenderTexture.GetTemporary(velocityTexture.width, velocityTexture.height, 0, velocityTexture.format);
    }

    // 释放临时纹理
    private void ReleaseTemporaryTextures()
    {
        if (tempColorRT != null)
        {
            RenderTexture.ReleaseTemporary(tempColorRT);
            tempColorRT = null;
        }
        if (tempVelocityRT != null)
        {
            RenderTexture.ReleaseTemporary(tempVelocityRT);
            tempVelocityRT = null;
        }
    }

    // 在组件被销毁时释放资源
    private void OnDestroy()
    {
        ReleaseTemporaryTextures();
        ReleaseCommandBufferResources();
    }

    // 原始的DrawAtPoint方法保持不变
    public void DrawAtPoint(Vector2 worldPos, Color color, Vector2 initialVelocity, float radius)
    {
        // 现有实现保持不变
        Vector2 uv = WorldToUV(worldPos);
        Debug.LogError("uv: " + uv);
        
        // 创建临时渲染纹理绘制颜色图层
        RenderTexture tempRT = RenderTexture.GetTemporary(colorTexture.width, colorTexture.height, 0, colorTexture.format);
        
        // 先将当前内容复制到临时RT
        Graphics.Blit(colorTexture, tempRT);

        // float hue = Mathf.Sin(Time.time * 0.3f) * 0.5f + 0.5f; // 将sin值从[-1,1]映射到[0,1]
        // Color color = Color.HSVToRGB(hue, 1f, 1f);

        Vector2 offset = (this.transform.position - previousPosition) * 0.1f;
        
        // 设置绘制参数
        drawMaterial.SetColor("_Color", color);
        drawMaterial.SetTexture("_BrushTex", CustomTexture);
        drawMaterial.SetFloat("_BrushSize", brushSize * radius); 
        drawMaterial.SetVector("_BrushPos", new Vector4(uv.x, uv.y, 0, 0));
        drawMaterial.SetFloat("_NOT_DRAW_ALPHA", 0.0f);
        drawMaterial.SetTexture("_MainTex", tempRT);
        
        // 将结果绘制回目标纹理
        Graphics.Blit(tempRT, colorTexture, drawMaterial, 0);
        
        // 先将当前内容复制到临时RT
        // Graphics.Blit(velocityTexture, tempRT);

        // color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        // color.r += initialVelocity.x * 0.05f;
        // color.g += initialVelocity.y * 0.05f;

        // drawMaterial.SetColor("_Color", color);
        // drawMaterial.SetTexture("_BrushTex", brushTexture);
        // drawMaterial.SetFloat("_BrushSize", brushSize); 
        // drawMaterial.SetVector("_BrushPos", new Vector4(uv.x, uv.y, 0, 0));
        // drawMaterial.SetFloat("_NOT_DRAW_ALPHA", 1.0f);
        // drawMaterial.SetTexture("_MainTex", tempRT);

        // Graphics.Blit(tempRT, velocityTexture, drawMaterial);
        
        // 释放临时RT
        RenderTexture.ReleaseTemporary(tempRT);
    }

    private void UpdateColorField()
    {
        // 创建临时渲染纹理
        RenderTexture tempRT = RenderTexture.GetTemporary(colorTexture.width, colorTexture.height, 0, colorTexture.format);

        // 先将当前内容复制到临时RT
        Graphics.Blit(colorTexture, tempRT);

        Vector2 offset = (this.transform.position - previousPosition) * 0.0f;

        // 设置颜色场材质
        colorFieldMaterial.SetTexture("_MainTex", tempRT);
        colorFieldMaterial.SetTexture("_VelocityTex", velocityTexture);
        colorFieldMaterial.SetTexture("_ObstacleTex", obstacleTexture);
        colorFieldMaterial.SetColor("_Velocity", new Vector4(offset.x, offset.y, 0, 0));
        colorFieldMaterial.SetVector("_FluidDomainOffset", new Vector4(FluidDomainOffset.x, FluidDomainOffset.y, 0, 0));

        // 将结果绘制回目标纹理
        Graphics.Blit(tempRT, colorTexture, colorFieldMaterial);
        
        // 释放临时RT
        RenderTexture.ReleaseTemporary(tempRT);
    }

    private void UpdateVelocityField()
    {
        float dt = 0;
        float startTime = Time.realtimeSinceStartup;

        Vector2 offset = (this.transform.position - previousPosition) * dt;
        
        // 创建临时渲染纹理
        RenderTexture tempRT = RenderTexture.GetTemporary(velocityTexture.width, velocityTexture.height, 0, velocityTexture.format);
        
        // 先将当前内容复制到临时RT
        Graphics.Blit(velocityTexture, tempRT);

        // 设置速度场材质
        velocityFieldMaterial.SetTexture("_MainTex", tempRT);
        velocityFieldMaterial.SetTexture("_ObstacleTex", obstacleTexture);
        velocityFieldMaterial.SetTexture("_ObstacleTexPre", obstacleTexturePre);
        velocityFieldMaterial.SetFloat("_ObstacleForceStrength", obstacleForceStrength);
        velocityFieldMaterial.SetVector("_Velocity", new Vector4(offset.x, offset.y, 0, 0));
        velocityFieldMaterial.SetVector("_FluidDomainOffset", new Vector4(FluidDomainOffset.x, FluidDomainOffset.y, 0, 0));

        Graphics.Blit(tempRT, velocityTexture, velocityFieldMaterial);

        // 释放临时RT
        RenderTexture.ReleaseTemporary(tempRT);

        float endTime = Time.realtimeSinceStartup;
        //Debug.Log("速度场更新耗时: " + ((endTime - startTime) * 1000f) + "ms");
    }

    private void UpdateOffsetField(RenderTexture Texture, Vector2 offset)
    {
        // 创建临时渲染纹理
        RenderTexture tempRT = RenderTexture.GetTemporary(Texture.width, Texture.height, 0, Texture.format);
        
        // 先将当前内容复制到临时RT
        Graphics.Blit(Texture, tempRT);

        // 设置偏移场材质
        offsetTextureMaterial.SetTexture("_MainTex", tempRT);
        offsetTextureMaterial.SetVector("_Offset", new Vector4(offset.x, offset.y, 0, 0));
        
        Graphics.Blit(tempRT, Texture, offsetTextureMaterial);

        // 释放临时RT
        RenderTexture.ReleaseTemporary(tempRT);
    }   

    private void UpdateObstacleTexture()
    {
        if (obstacleCamera == null)
        {
            return;
        }

        obstacleCamera.enabled = false;
        obstacleCamera.targetTexture = obstacleTexture;
        obstacleCamera.Render();
        
        RenderTexture.active = obstacleTexture;
        RenderTexture.active = null;
    }

    
    // 清除纹理
    public void ClearTexture()
    {
        RenderTexture rt1 = RenderTexture.active;
        RenderTexture.active = colorTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = rt1;

        RenderTexture rt2 = RenderTexture.active;
        RenderTexture.active = velocityTexture;
        GL.Clear(true, true, new Color(0.5f, 0.5f, 0.515f, 0.5f));
        RenderTexture.active = rt2;
    }

    // 初始化CommandBuffer相关资源
    private void InitCommandBufferResources()
    {
        if (drawShader == null) return;
        
        // 创建或获取用于批处理的材质
        if (batchDrawMaterial == null)
            batchDrawMaterial = new Material(drawShader);
        
        // 释放旧的缓冲区（如果存在）
        ReleaseCommandBufferResources();
    }

    // 释放CommandBuffer相关资源
    private void ReleaseCommandBufferResources()
    {
        if (positionsBuffer != null)
        {
            positionsBuffer.Release();
            positionsBuffer = null;
        }
        
        if (velocitiesBuffer != null)
        {
            velocitiesBuffer.Release();
            velocitiesBuffer = null;
        }
        
        if (radiiBuffer != null)
        {
            radiiBuffer.Release();
            radiiBuffer = null;
        }

        if (velocityRadiiBuffer != null)
        {
            velocityRadiiBuffer.Release();
            velocityRadiiBuffer = null;
        }
        
        if (colorsBuffer != null)
        {
            colorsBuffer.Release();
            colorsBuffer = null;
        }

        if (velocityTypesBuffer != null)
        {
            velocityTypesBuffer.Release();
            velocityTypesBuffer = null;
        }
    }

    // 使用CommandBuffer处理绘制请求
    private void ProcessDrawRequestsWithCommandBuffer()
    {
        if (drawRequests.Count == 0) return;
        
        // 准备数据缓冲区
        if (positionsBuffer == null || positionsBuffer.count < drawRequests.Count)
        {
            if (positionsBuffer != null) positionsBuffer.Release();
            positionsBuffer = new ComputeBuffer(Mathf.Max(1000, drawRequests.Count), sizeof(float) * 2);
        }
        
        if (velocitiesBuffer == null || velocitiesBuffer.count < drawRequests.Count)
        {
            if (velocitiesBuffer != null) velocitiesBuffer.Release();
            velocitiesBuffer = new ComputeBuffer(Mathf.Max(1000, drawRequests.Count), sizeof(float) * 2);
        }
        
        if (radiiBuffer == null || radiiBuffer.count < drawRequests.Count)
        {
            if (radiiBuffer != null) radiiBuffer.Release();
            radiiBuffer = new ComputeBuffer(Mathf.Max(1000, drawRequests.Count), sizeof(float));
        }

        if (velocityRadiiBuffer == null || velocityRadiiBuffer.count < drawRequests.Count)
        {
            if (velocityRadiiBuffer != null) velocityRadiiBuffer.Release();
            velocityRadiiBuffer = new ComputeBuffer(Mathf.Max(1000, drawRequests.Count), sizeof(float));
        }
        
        if (colorsBuffer == null || colorsBuffer.count < drawRequests.Count)
        {
            if (colorsBuffer != null) colorsBuffer.Release();
            colorsBuffer = new ComputeBuffer(Mathf.Max(1000, drawRequests.Count), sizeof(float) * 4);
        }
        
        if (velocityTypesBuffer == null || velocityTypesBuffer.count < drawRequests.Count)
        {
            if (velocityTypesBuffer != null) velocityTypesBuffer.Release();
            velocityTypesBuffer = new ComputeBuffer(Mathf.Max(1000, drawRequests.Count), sizeof(int));
        }
        
        // 填充位置数据
        Vector2[] positions = new Vector2[drawRequests.Count];
        Vector2[] velocities = new Vector2[drawRequests.Count];
        float[] colorRadii = new float[drawRequests.Count];   // 颜色半径数组
        float[] velocityRadii = new float[drawRequests.Count]; // 速度半径数组
        Vector4[] colors = new Vector4[drawRequests.Count];
        int[] velocityTypes = new int[drawRequests.Count];
        
        for (int i = 0; i < drawRequests.Count; i++)
        {
            positions[i] = WorldToUV(drawRequests[i].worldPos) - FluidDomainOffset;
            velocities[i] = drawRequests[i].initialVelocity;
            colorRadii[i] = drawRequests[i].colorRadius;      // 设置颜色半径
            velocityRadii[i] = drawRequests[i].velocityRadius; // 设置速度半径
            colors[i] = drawRequests[i].color;
            velocityTypes[i] = (int)drawRequests[i].velocityType;
        }
        
        // 更新缓冲区
        positionsBuffer.SetData(positions);
        velocitiesBuffer.SetData(velocities);
        radiiBuffer.SetData(colorRadii);  // 设置颜色半径
        velocityRadiiBuffer.SetData(velocityRadii);  // 设置速度半径
        colorsBuffer.SetData(colors);
        velocityTypesBuffer.SetData(velocityTypes);
        
        // 创建命令缓冲区
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "Batch Draw Processing";
        
        // 处理颜色纹理绘制
        InitTemporaryTextures();
        cmd.Blit(colorTexture, tempColorRT);
        
        // 设置着色器参数 - 颜色绘制
        batchDrawMaterial.SetBuffer("_Positions", positionsBuffer);
        batchDrawMaterial.SetBuffer("_Colors", colorsBuffer);
        batchDrawMaterial.SetBuffer("_ColorRadii", radiiBuffer); // 改为_ColorRadii
        batchDrawMaterial.SetInt("_PointCount", drawRequests.Count);
        batchDrawMaterial.SetTexture("_BrushTex", brushTexture);
        batchDrawMaterial.SetTexture("_ExploreTex", exploreTexture);
        batchDrawMaterial.SetFloat("_BrushSize", brushSize);
        
        // 一次性绘制所有点到颜色纹理
        cmd.Blit(tempColorRT, colorTexture, batchDrawMaterial, 0);
        
        // 处理速度纹理绘制
        cmd.Blit(velocityTexture, tempVelocityRT);
        
        // 设置速度绘制参数
        batchDrawMaterial.SetBuffer("_Velocities", velocitiesBuffer);
        batchDrawMaterial.SetBuffer("_VelocityRadii", velocityRadiiBuffer); // 改为_VelocityRadii
        batchDrawMaterial.SetBuffer("_VelocityTypes", velocityTypesBuffer);
        
        // 一次性绘制所有点到速度纹理
        cmd.Blit(tempVelocityRT, velocityTexture, batchDrawMaterial, 1);
        
        // 设置速度类型
        batchDrawMaterial.SetBuffer("_VelocityTypes", velocityTypesBuffer);
        
        // 执行命令
        Graphics.ExecuteCommandBuffer(cmd);
        
        // 释放命令缓冲区
        cmd.Release();
    }
}

