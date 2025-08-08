using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
VibeCoding Advice:
我希望通过NumberScroll实现类似老式机械钟表的数字显示功能。
在这个脚本的物体下，存在3个子物体，分别命名为“1” “10” “100”, 
分别代表着个十百位，每个子物体上使用SpriteRenderer，
使用相同但独立复制的材质，材质中存在一个名为“_Offset”的Float输入，
可以用来操控Sprite图片的UV，其中，_Offset = 0.0时，
材质上的数字指向9，当_Offset = 0.9时，材质上的数字指向0，0~9之间线性变化，
而当_Offset = 1.0时，材质上的数字重回9，在UV上是循环的。
我需要你实现类似老式机械钟表的那种滚动的数字变化，并且这套系统能够显示剧烈变化的数据。
*/

public class NumberScroll : MonoBehaviour
{
    [Header("数字显示设置")]
    [SerializeField] private float animationSpeed = 2f;  // 动画速度
    [SerializeField] private float overshootAmount = 0.1f;  // 过冲量，模拟机械感
    [SerializeField] private bool useOvershoot = true;  // 是否使用过冲效果
    [SerializeField] private bool enableDebugLog = false;  // 启用调试日志
    
    [Header("FPS显示设置")]
    [SerializeField] private float fpsUpdateInterval = 0.5f;  // FPS更新间隔（秒）
    [SerializeField] private bool enableFPSDisplay = true;  // 启用自动FPS显示
    [SerializeField] private bool useSmoothFPS = true;  // 使用平滑FPS计算
    [SerializeField] private float fpsSmoothFactor = 0.8f;  // FPS平滑系数 (0-1)
    
    [Header("目标数值")]
    [SerializeField] private int targetNumber = 0;  // 目标数字（0-999）
    
    [Header("调试信息")]
    [SerializeField] private int currentDisplayNumber = 0;  // 当前显示的数字
    
    // 子物体引用
    private Transform onesDigit;      // "1" - 个位
    private Transform tensDigit;      // "10" - 十位  
    private Transform hundredsDigit;  // "100" - 百位
    
    // 材质引用
    private Material onesMaterial;
    private Material tensMaterial;
    private Material hundredsMaterial;
    
    // 当前偏移值
    private float currentOnesOffset = 0f;
    private float currentTensOffset = 0f;
    private float currentHundredsOffset = 0f;
    
    // 目标偏移值
    private float targetOnesOffset = 0f;
    private float targetTensOffset = 0f;
    private float targetHundredsOffset = 0f;
    
    // 动画状态
    private bool isAnimating = false;
    private Coroutine animationCoroutine;
    
    // FPS更新计时器
    private float fpsUpdateTimer = 0f;
    private float smoothedFPS = 60f;  // 平滑后的FPS值
    void Start()
    {
        InitializeComponents();
        SetNumber(0);  // 初始化为0
    }

    void Update()
    {
        // 只有启用FPS显示时才自动更新
        if (!enableFPSDisplay) return;
        
        // 累加计时器
        fpsUpdateTimer += Time.deltaTime;
        
        // 计算当前帧的FPS
        float currentFrameFPS = 1.0f / Time.deltaTime;
        
        // 平滑处理FPS值
        if (useSmoothFPS)
        {
            smoothedFPS = Mathf.Lerp(currentFrameFPS, smoothedFPS, fpsSmoothFactor);
        }
        else
        {
            smoothedFPS = currentFrameFPS;
        }
        
        // 根据设定的间隔更新FPS显示
        if (fpsUpdateTimer >= fpsUpdateInterval)
        {
            int displayFPS = Mathf.RoundToInt(smoothedFPS);
            SetNumber(displayFPS);
            
            // 重置计时器
            fpsUpdateTimer = 0f;
        }
    }
    
    /// <summary>
    /// 初始化组件引用
    /// </summary>
    private void InitializeComponents()
    {
        // 查找子物体
        onesDigit = transform.Find("1");
        tensDigit = transform.Find("10");
        hundredsDigit = transform.Find("100");
        
        // 检查子物体是否存在
        if (onesDigit == null || tensDigit == null || hundredsDigit == null)
        {
            Debug.LogError($"NumberScroll ({gameObject.name}): 缺少必要的子物体。请确保存在名为 '1', '10', '100' 的子物体。" +
                          $"找到的子物体: 1={onesDigit != null}, 10={tensDigit != null}, 100={hundredsDigit != null}");
            return;
        }
        
        // 获取材质引用
        onesMaterial = onesDigit.GetComponent<SpriteRenderer>()?.material;
        tensMaterial = tensDigit.GetComponent<SpriteRenderer>()?.material;
        hundredsMaterial = hundredsDigit.GetComponent<SpriteRenderer>()?.material;
        
        // 检查材质是否存在
        if (onesMaterial == null || tensMaterial == null || hundredsMaterial == null)
        {
            Debug.LogError($"NumberScroll ({gameObject.name}): 子物体缺少 SpriteRenderer 或材质。" +
                          $"材质状态: 1={onesMaterial != null}, 10={tensMaterial != null}, 100={hundredsMaterial != null}");
            return;
        }
        
        if (enableDebugLog)
        {
            Debug.Log($"NumberScroll ({gameObject.name}): 初始化完成，材质引用已获取");
        }
        
        // 初始化偏移值
        currentOnesOffset = 0f;  // 显示9
        currentTensOffset = 0f;  // 显示9
        currentHundredsOffset = 0f;  // 显示9
        
        UpdateMaterialOffsets();
    }
    
    /// <summary>
    /// 设置要显示的数字
    /// </summary>
    /// <param name="number">要显示的数字（0-999）</param>
    public void SetNumber(int number)
    {
        // 如果组件还没初始化，先初始化
        if (onesMaterial == null || tensMaterial == null || hundredsMaterial == null)
        {
            InitializeComponents();
        }
        
        // 限制范围
        number = Mathf.Clamp(number, 0, 999);
        targetNumber = number;
        currentDisplayNumber = number;  // 立即更新当前显示数字
        
        if (enableDebugLog)
        {
            Debug.Log($"NumberScroll: 设置数字 {number}, 偏移值: 个位={CalculateOffset(number % 10):F2}, 十位={CalculateOffset((number / 10) % 10):F2}, 百位={CalculateOffset((number / 100) % 10):F2}");
        }
        
        // 分解数字
        int ones = number % 10;
        int tens = (number / 10) % 10;
        int hundreds = (number / 100) % 10;
        
        // 计算目标偏移值
        targetOnesOffset = CalculateOffset(ones);
        targetTensOffset = CalculateOffset(tens);
        targetHundredsOffset = CalculateOffset(hundreds);
        
        // 开始动画
        StartNumberAnimation();
    }
    
    /// <summary>
    /// 根据数字计算偏移值
    /// </summary>
    /// <param name="digit">数字（0-9）</param>
    /// <returns>对应的偏移值</returns>
    private float CalculateOffset(int digit)
    {
        // 根据材质逻辑：9对应0.0，0对应0.9
        return (9 - digit) * 0.1f;
    }
    
    /// <summary>
    /// 开始数字动画
    /// </summary>
    private void StartNumberAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        
        animationCoroutine = StartCoroutine(AnimateToTarget());
    }
    
    /// <summary>
    /// 动画协程
    /// </summary>
    private IEnumerator AnimateToTarget()
    {
        isAnimating = true;
        
        float startOnesOffset = currentOnesOffset;
        float startTensOffset = currentTensOffset;
        float startHundredsOffset = currentHundredsOffset;
        
        // 处理循环边界（例如从9到0的过渡）
        float onesDistance = CalculateShortestDistance(startOnesOffset, targetOnesOffset);
        float tensDistance = CalculateShortestDistance(startTensOffset, targetTensOffset);
        float hundredsDistance = CalculateShortestDistance(startHundredsOffset, targetHundredsOffset);
        
        float finalOnesOffset = startOnesOffset + onesDistance;
        float finalTensOffset = startTensOffset + tensDistance;
        float finalHundredsOffset = startHundredsOffset + hundredsDistance;
        
        float elapsedTime = 0f;
        float duration = 1f / animationSpeed;
        
        while (elapsedTime < duration)
        {
            float progress = elapsedTime / duration;
            
            // 使用缓动函数创建机械感
            float easedProgress = useOvershoot ? EaseOutBack(progress) : EaseOutQuart(progress);
            
            // 插值计算当前偏移值
            currentOnesOffset = Mathf.Lerp(startOnesOffset, finalOnesOffset, easedProgress);
            currentTensOffset = Mathf.Lerp(startTensOffset, finalTensOffset, easedProgress);
            currentHundredsOffset = Mathf.Lerp(startHundredsOffset, finalHundredsOffset, easedProgress);
            
            // 规范化偏移值到0-1范围
            currentOnesOffset = NormalizeOffset(currentOnesOffset);
            currentTensOffset = NormalizeOffset(currentTensOffset);
            currentHundredsOffset = NormalizeOffset(currentHundredsOffset);
            
            UpdateMaterialOffsets();
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 确保最终值准确
        currentOnesOffset = targetOnesOffset;
        currentTensOffset = targetTensOffset;
        currentHundredsOffset = targetHundredsOffset;
        
        UpdateMaterialOffsets();
        isAnimating = false;
    }
    
    /// <summary>
    /// 计算最短旋转距离（处理循环）
    /// </summary>
    private float CalculateShortestDistance(float from, float to)
    {
        float directDistance = to - from;
        float wrapDistance = directDistance > 0 ? directDistance - 1f : directDistance + 1f;
        
        // 选择最短路径
        return Mathf.Abs(directDistance) <= Mathf.Abs(wrapDistance) ? directDistance : wrapDistance;
    }
    
    /// <summary>
    /// 规范化偏移值到0-1范围
    /// </summary>
    private float NormalizeOffset(float offset)
    {
        while (offset < 0f) offset += 1f;
        while (offset >= 1f) offset -= 1f;
        return offset;
    }
    
    /// <summary>
    /// 更新材质偏移值
    /// </summary>
    private void UpdateMaterialOffsets()
    {
        if (onesMaterial != null)
            onesMaterial.SetFloat("_Offset", currentOnesOffset);
        
        if (tensMaterial != null)
            tensMaterial.SetFloat("_Offset", currentTensOffset);
        
        if (hundredsMaterial != null)
            hundredsMaterial.SetFloat("_Offset", currentHundredsOffset);
    }
    
    /// <summary>
    /// 带回弹的缓动函数
    /// </summary>
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f * overshootAmount;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    
    /// <summary>
    /// 四次方缓出函数
    /// </summary>
    private float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }
    
    /// <summary>
    /// 获取当前显示的数字
    /// </summary>
    public int GetCurrentNumber()
    {
        return currentDisplayNumber;
    }
    
    /// <summary>
    /// 检查是否正在动画中
    /// </summary>
    public bool IsAnimating()
    {
        return isAnimating;
    }
    
    /// <summary>
    /// 立即设置数字（无动画）
    /// </summary>
    public void SetNumberImmediate(int number)
    {
        // 如果组件还没初始化，先初始化
        if (onesMaterial == null || tensMaterial == null || hundredsMaterial == null)
        {
            InitializeComponents();
        }
        
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            isAnimating = false;
        }
        
        number = Mathf.Clamp(number, 0, 999);
        targetNumber = number;
        currentDisplayNumber = number;
        
        // 分解数字并直接设置偏移值
        int ones = number % 10;
        int tens = (number / 10) % 10;
        int hundreds = (number / 100) % 10;
        
        currentOnesOffset = targetOnesOffset = CalculateOffset(ones);
        currentTensOffset = targetTensOffset = CalculateOffset(tens);
        currentHundredsOffset = targetHundredsOffset = CalculateOffset(hundreds);
        
        UpdateMaterialOffsets();
    }
}
