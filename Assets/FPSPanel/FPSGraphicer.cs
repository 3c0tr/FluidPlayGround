using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSGraphicer : MonoBehaviour
{
    [Header("图表设置")]
    [SerializeField] private float timeWindow = 5f;  // 时间窗口（秒）
    [SerializeField] private float maxFPS = 240f;    // 最大帧率
    [SerializeField] private float graphWidth = 10f; // 图表宽度
    [SerializeField] private float graphHeight = 5f; // 图表高度
    [SerializeField] private int maxPoints = 100;    // 最大点数
    [SerializeField] private float updateInterval = 0.5f; // 每0.1秒更新一次图表
    
    private LineRenderer lineRenderer;
    private List<float> fpsHistory = new List<float>();
    private List<float> timeHistory = new List<float>();
    private float currentTime = 0f;
    
    // FPS计算相关变量（和FPSPanel相同的方式）
    private float fps;
    private float fpsTimer;
    private int frameCount;
    
    void Start()
    {
        // 获取LineRenderer组件
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            Debug.LogError("未找到LineRenderer组件！");
            return;
        }
        
        // 设置LineRenderer基本属性
        // lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 0;
    }
    
    void Update()
    {
        currentTime += Time.deltaTime;
        
        // 使用和FPSPanel相同的FPS计算方式
        CalculateFPS();
    }
    
    private void CalculateFPS()
    {
        fpsTimer += Time.deltaTime;
        frameCount++;
        
        if (fpsTimer >= updateInterval)
        {
            fps = frameCount / fpsTimer;
            fpsTimer = 0f;
            frameCount = 0;
            
            // 添加新的数据点（只在更新时添加）
            AddDataPoint(fps, currentTime);
            
            // 更新图表显示
            UpdateGraph();
        }
    }
    
    private void AddDataPoint(float fps, float time)
    {
        fpsHistory.Add(fps);
        timeHistory.Add(time);
        
        // 移除超出时间窗口的旧数据
        while (timeHistory.Count > 0 && (currentTime - timeHistory[0]) > timeWindow)
        {
            fpsHistory.RemoveAt(0);
            timeHistory.RemoveAt(0);
        }
        
        // 限制最大点数以优化性能
        while (fpsHistory.Count > maxPoints)
        {
            fpsHistory.RemoveAt(0);
            timeHistory.RemoveAt(0);
        }
    }
    
    private void UpdateGraph()
    {
        if (fpsHistory.Count < 2) return;
        
        // 设置LineRenderer点数
        lineRenderer.positionCount = fpsHistory.Count;
        
        // 计算图表点位置
        Vector3[] positions = new Vector3[fpsHistory.Count];
        
        for (int i = 0; i < fpsHistory.Count; i++)
        {
            // 时间轴映射（0到timeWindow映射到0到graphWidth）
            float normalizedTime = (timeHistory[i] - (currentTime - timeWindow)) / timeWindow;
            float x = normalizedTime * graphWidth;
            
            // 帧率轴映射（0到maxFPS映射到0到graphHeight）
            float normalizedFPS = Mathf.Clamp01(fpsHistory[i] / maxFPS);
            float y = normalizedFPS * graphHeight;
            
            positions[i] = new Vector3(x, y, 0);
        }
        
        // 更新LineRenderer位置
        lineRenderer.SetPositions(positions);
    }
}
