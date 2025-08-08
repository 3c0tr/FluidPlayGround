using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSPanel : MonoBehaviour
{
    private float fps;
    private float fpsTimer;
    private int frameCount;
    private float updateInterval = 0.5f; // 每0.1秒更新一次帧率显示
    [SerializeField] private seg7[] numbers;
    
    [Header("FPS颜色渐变设置")]
    [SerializeField] private Gradient fpsGradient;
    [SerializeField] private int testInt = 0;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
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
            
            // 根据FPS计算渐变位置 (0.0 - 1.0)
            float gradientPosition = Mathf.Clamp01(fps / 200.0f);
            // 从渐变中采样颜色
            Color fpsColor = fpsGradient.Evaluate(gradientPosition);
            
            // 更新数字显示，显示FPS的整数部分
            int fpsInteger = Mathf.RoundToInt(fps);
            DisplayFPS(fpsInteger, fpsColor);
            
            // 在控制台打印帧率和颜色信息
            // Debug.Log($"FPS: {fps:F1}, 渐变位置: {gradientPosition:F2}, 颜色: {fpsColor}");
        }
    }
    
    private void DisplayFPS(int fpsValue, Color color)
    {
        // 将FPS数字分解为各个位数
        if (numbers.Length >= 3) // 假设我们有3位数显示（百位、十位、个位）
        {
            int hundreds = fpsValue / 100;
            int tens = (fpsValue % 100) / 10;
            int units = fpsValue % 10;
            
            numbers[0].UpdateNumber(hundreds, color);  // 百位
            numbers[1].UpdateNumber(tens, color);      // 十位
            numbers[2].UpdateNumber(units, color);     // 个位
        }
        else if (numbers.Length >= 2) // 如果只有2位数显示
        {
            int tens = fpsValue / 10;
            int units = fpsValue % 10;
            
            numbers[0].UpdateNumber(tens, color);   // 十位
            numbers[1].UpdateNumber(units, color);  // 个位
        }
        else if (numbers.Length >= 1) // 如果只有1位数显示
        {
            numbers[0].UpdateNumber(fpsValue % 10, color); // 个位
        }
    }
}
