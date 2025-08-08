using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AimToMouse : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // 获取鼠标在世界坐标中的位置
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0; // 确保z坐标为0
        
        // 计算物体指向鼠标的向量
        Vector2 direction = (mousePosition - transform.position).normalized;
        
        
        // 计算旋转角度（以度为单位）
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (direction.x < 0)
        {
            angle += 180;
        }
        
        // 应用旋转
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}
