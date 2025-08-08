using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCharacter : MonoBehaviour
{
    [SerializeField] private GameObject MainCamera;
    [SerializeField] private GameObject SubCamera;
    [SerializeField] private GameObject FluidDomain;
    [SerializeField] private float moveSpeed = 5f; // 移动速度
    [SerializeField] private GameObject[] Eyes;
    [SerializeField] private float eyeTrackingRange = 0.5f; // 眼睛追踪范围
    [SerializeField] private float eyeTrackingSpeed = 5f; // 眼睛追踪速度
    [SerializeField] private float blinkInterval = 10f; // 眨眼间隔时间
    [SerializeField] private float blinkDuration = 0.1f; // 眨眼持续时间
    
    private Rigidbody2D rb; // Rigidbody2D 组件引用
    private bool facingRight = true; // 记录当前朝向，初始朝向右
    private Vector3[] eyesInitialPositions; // 存储眼睛的初始位置
    private Vector3[] eyesInitialScales; // 存储眼睛的初始缩放
    
    // 眨眼相关变量
    private float lastInputTime; // 上次有输入的时间
    private float lastBlinkTime; // 上次眨眼的时间
    private bool isBlinking = false; // 是否正在眨眼
    private float blinkStartTime; // 眨眼开始时间
    private Vector3 lastMousePosition; // 上次鼠标位置
    
    // Start is called before the first frame update
    void Start()
    {
        // 获取 Rigidbody2D 组件
        rb = GetComponent<Rigidbody2D>();
        
        // 存储眼睛的初始位置和缩放
        if (Eyes != null && Eyes.Length > 0)
        {
            eyesInitialPositions = new Vector3[Eyes.Length];
            eyesInitialScales = new Vector3[Eyes.Length];
            for (int i = 0; i < Eyes.Length; i++)
            {
                if (Eyes[i] != null)
                {
                    eyesInitialPositions[i] = Eyes[i].transform.localPosition;
                    eyesInitialScales[i] = Eyes[i].transform.localScale;
                }
            }
        }
        
        // 初始化时间变量
        lastInputTime = Time.time;
        lastBlinkTime = Time.time;
        lastMousePosition = Input.mousePosition;
    }

    // Update is called once per frame
    void Update()
    {
        bool hasInput = CheckForInput(); // 检查是否有输入
        
        Move(); // 调用移动函数
        FlipCharacterTowardsMouse(); // 调用翻转函数
        MoveEyesTowardsMouse(); // 调用眼睛追踪函数
        HandleBlinking(hasInput); // 处理眨眼逻辑
        
        Vector3 cameraPosition = this.transform.position;
        cameraPosition.z = -10f;
        if (MainCamera != null)
        {
            MainCamera.transform.position = cameraPosition;
        }
        if (SubCamera != null)
        {
            SubCamera.transform.position = cameraPosition;
        }

        Vector3 fluidDomainPosition = FluidDomain.transform.position;
        fluidDomainPosition.z = 1f;
        FluidDomain.transform.position = fluidDomainPosition;
    }

    bool CheckForInput()
    {
        bool hasInput = false;
        
        // 检查键盘输入
        if (Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0)
        {
            hasInput = true;
        }
        
        // 检查鼠标位移
        Vector3 currentMousePosition = Input.mousePosition;
        if (Vector3.Distance(currentMousePosition, lastMousePosition) > 1f) // 鼠标移动超过1像素
        {
            hasInput = true;
            lastMousePosition = currentMousePosition;
        }
        
        // 如果有输入，更新最后输入时间
        if (hasInput)
        {
            lastInputTime = Time.time;
        }
        
        return hasInput;
    }

    void HandleBlinking(bool hasInput)
    {
        if (Eyes == null || Eyes.Length == 0)
            return;
            
        // 如果正在眨眼
        if (isBlinking)
        {
            float blinkProgress = (Time.time - blinkStartTime) / blinkDuration;
            
            if (blinkProgress >= 1f)
            {
                // 眨眼结束，恢复正常缩放
                isBlinking = false;
                for (int i = 0; i < Eyes.Length; i++)
                {
                    if (Eyes[i] != null && i < eyesInitialScales.Length)
                    {
                        Eyes[i].transform.localScale = eyesInitialScales[i];
                    }
                }
            }
            else
            {
                // 眨眼进行中，设置Y轴缩放为0.1
                for (int i = 0; i < Eyes.Length; i++)
                {
                    if (Eyes[i] != null && i < eyesInitialScales.Length)
                    {
                        Vector3 blinkScale = eyesInitialScales[i];
                        blinkScale.y = 0.1f;
                        Eyes[i].transform.localScale = blinkScale;
                    }
                }
            }
        }
        else
        {
            // 检查是否应该开始眨眼
            // 条件：没有输入且距离上次眨眼超过指定间隔
            float timeSinceLastInput = Time.time - lastInputTime;
            float timeSinceLastBlink = Time.time - lastBlinkTime;
            
            if (!hasInput && timeSinceLastInput >= 1f && timeSinceLastBlink >= blinkInterval)
            {
                StartBlink();
            }
        }
    }
    
    void StartBlink()
    {
        isBlinking = true;
        blinkStartTime = Time.time;
        lastBlinkTime = Time.time;
    }

    void Move()
    {
        float horizontalInput = Input.GetAxis("Horizontal"); // A/D 或 ←/→
        float verticalInput = Input.GetAxis("Vertical");     // W/S 或 ↑/↓
        
        // 创建移动向量
        Vector2 moveDirection = new Vector2(horizontalInput, verticalInput);
        
        // 应用移动速度到rigidbody
        rb.velocity = moveDirection * moveSpeed;
    }
    
    void FlipCharacterTowardsMouse()
    {
        // 获取鼠标在世界空间中的位置
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -Camera.main.transform.position.z; // 设置z深度为摄像机到z=0平面的距离
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mousePos);
        
        // 计算鼠标相对于主角的X轴位置
        float mouseRelativeX = mouseWorldPos.x - transform.position.x;
        
        // 根据鼠标位置决定朝向
        if (mouseRelativeX >= 0.0f && !facingRight)
        {
            // 鼠标在右侧，且当前朝向左，需要翻转到右
            Flip();
        }
        else if (mouseRelativeX < 0.0f && facingRight)
        {
            // 鼠标在左侧，且当前朝向右，需要翻转到左
            Flip();
        }
    }
    
    void MoveEyesTowardsMouse()
    {
        if (Eyes == null || Eyes.Length == 0 || eyesInitialPositions == null)
            return;
            
        // 获取鼠标在世界空间中的位置
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -Camera.main.transform.position.z;
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mousePos);
        
        // 计算鼠标相对于主角的Y轴位置差
        float mouseRelativeY = mouseWorldPos.y - transform.position.y;
        
        // 限制眼睛移动范围，并计算目标Y偏移
        float targetYOffset = Mathf.Clamp(mouseRelativeY * 0.1f, -eyeTrackingRange, eyeTrackingRange);
        
        // 为每只眼睛设置目标位置
        for (int i = 0; i < Eyes.Length; i++)
        {
            if (Eyes[i] != null && i < eyesInitialPositions.Length)
            {
                Vector3 targetPosition = eyesInitialPositions[i];
                targetPosition.y += targetYOffset;
                
                // 平滑移动到目标位置
                Eyes[i].transform.localPosition = Vector3.Lerp(
                    Eyes[i].transform.localPosition,
                    targetPosition,
                    eyeTrackingSpeed * Time.deltaTime
                );
            }
        }
    }
    
    void Flip()
    {
        // 切换朝向状态
        facingRight = !facingRight;
        
        // 翻转角色的X轴缩放
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }
}
