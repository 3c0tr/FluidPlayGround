using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicDrawBrush : MonoBehaviour
{
    [Header("绘制设置")]
    public float velocityMultiplier = 5f; // 速度倍数
    public float minVelocityThreshold = 0.1f; // 最小速度阈值
    [SerializeField] Color drawColor = Color.white;

    
    private Vector2? lastMouseWorldPoint; // 上一帧的鼠标世界坐标
    
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Vector2? mouseWorldPoint = GetMouseWorldPoint();
        if (mouseWorldPoint.HasValue && Input.GetMouseButton(0))
        {
            Vector2 mouseVelocity = CalculateMouseVelocity(mouseWorldPoint.Value);
            Color col = new Color(1f, 1f, 1f, 1.5f);
            
            for (int i = 0; i < 1; i++)
            {
                for (int j = 0; j < 1; j++)
                {
                    if (mouseVelocity.magnitude > 0.1f || true)
                    {
                        FluidController.Instance.QueueDrawAtPoint(
                                            mouseWorldPoint.Value + new Vector2(i * 0.05f, j * 0.05f),
                                            col,
                                            mouseVelocity,
                                            1f,
                                            1f,
                                            FluidController.VelocityType.Direct
                                        );
                    }
                }
            }
        }
        if (mouseWorldPoint.HasValue && Input.GetMouseButton(1))
        {
            Vector2 mouseVelocity = CalculateMouseVelocity(mouseWorldPoint.Value);
            if (mouseVelocity.magnitude > 0.1f || true)
            {
                FluidController.Instance.QueueDrawAtPoint(
                                    mouseWorldPoint.Value,
                                    drawColor,
                                    mouseVelocity,
                                    0f,
                                    1f,
                                    FluidController.VelocityType.Direct
                                );
            }
        }
        
        // 更新上一帧位置
        lastMouseWorldPoint = mouseWorldPoint;
    }
    
    private Vector2 CalculateMouseVelocity(Vector2 currentMousePos)
    {
        if (!lastMouseWorldPoint.HasValue)
        {
            // 第一帧或者鼠标刚开始点击，返回零向量
            return Vector2.zero;
        }
        
        // 计算鼠标移动向量
        Vector2 deltaPos = currentMousePos - lastMouseWorldPoint.Value;
        
        // 计算速度（考虑帧率）
        Vector2 velocity = deltaPos / Time.deltaTime;
        
        // 应用速度倍数
        velocity *= velocityMultiplier;
        
        // 如果速度太小，可以选择不施加速度或使用最小速度
        if (velocity.magnitude < minVelocityThreshold)
        {
            return Vector2.zero;
        }
        
        // 可以选择限制最大速度
        float maxVelocity = 10f;
        if (velocity.magnitude > maxVelocity)
        {
            velocity = velocity.normalized * maxVelocity;
        }
        
        return velocity;
    }

    private Vector2? GetMouseWorldPoint()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -Camera.main.transform.position.z; // 设置z深度为摄像机到z=0平面的距离
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
        return new Vector2(worldPos.x, worldPos.y);
    }
}
