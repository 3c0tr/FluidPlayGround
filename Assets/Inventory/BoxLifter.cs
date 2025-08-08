using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxLifter : MonoBehaviour
{
    // 力的系数
    [SerializeField] private float forceMultiplier = 50f;
    [SerializeField] private float dragMultiplier = 10f;
    private GameObject box;

    void Update()
    {
        // 当按下鼠标右键时
        if (Input.GetMouseButtonDown(1))
        {
            // 从摄像机发射射线到鼠标位置
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePosition, Vector2.zero);

            // 如果射线击中了物体
            if (hit.collider != null)
            {
                // 检查物体名称是否包含"Box"
                if (hit.collider.gameObject.name.Contains("Box"))
                {
                    box = hit.collider.gameObject;
                    Rigidbody2D rb = box.GetComponent<Rigidbody2D>();
                    rb.gravityScale = 0;
                }
            }
        }
        else if (Input.GetMouseButtonUp(1))
        {
            if (box != null)
            {
                Rigidbody2D rb = box.GetComponent<Rigidbody2D>();
                rb.gravityScale = 1;
            }
            box = null;
        }

        if (box != null)
        {
            // 获取物体的Rigidbody2D组件
            Rigidbody2D rb = box.GetComponent<Rigidbody2D>();
            
            if (rb != null)
            {
                Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector2 direction = (mousePosition - (Vector2)box.transform.position).normalized;
                
                // 计算距离
                float distance = Mathf.Clamp01(Vector2.Distance(mousePosition, box.transform.position));
                
                // 施加力使物体移动
                rb.AddForce(direction * forceMultiplier * distance, ForceMode2D.Force);
                rb.AddForce((1.0f - distance) * rb.velocity * -dragMultiplier * Time.deltaTime);

                if (Vector2.Dot(rb.velocity, direction) < 0)
                {
                    rb.velocity = rb.velocity * (1 - 10f * Time.deltaTime);
                }
            }
        }
    }
}