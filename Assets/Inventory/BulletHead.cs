using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletHead : MonoBehaviour
{
    [Header("子弹设置")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private LayerMask collisionLayers = -1;
    
    [Header("效果设置")]
    [SerializeField] private bool destroyOnHit = true;
    
    private Rigidbody2D rb;
    public float timeAlive;
    public bool hasHit = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        timeAlive = 0f;
        
        // 设置子弹向前移动
        if (rb != null)
        {
            rb.velocity = transform.right * speed;
        }
    }

    void Update()
    {
        // 生命周期检查
        timeAlive += Time.deltaTime;
        if (timeAlive >= lifeTime)
        {
            DestroyBullet();
        }
        
        // 如果没有Rigidbody2D，使用Transform移动
        if (rb == null && !hasHit)
        {
            transform.Translate(Vector3.right * speed * Time.deltaTime);
        }

        FluidController.Instance.QueueDrawAtPoint(
                        this.transform.position,
                        Color.white,
                        rb.velocity,
                        0.07f,
                        0.07f,
                        FluidController.VelocityType.Direct
                    );
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 检查碰撞层级
        if (((1 << other.gameObject.layer) & collisionLayers) != 0 && !hasHit)
        {
            OnHit(other);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 检查碰撞层级
        if (((1 << collision.gameObject.layer) & collisionLayers) != 0 && !hasHit)
        {
            OnHit(collision.collider);
        }
    }

    private void OnHit(Collider2D hitCollider)
    {
        if (hasHit) return;
        
        hasHit = true;
        
        // 停止移动
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        
        
        // 销毁子弹或回收到对象池
        if (destroyOnHit)
        {
            DestroyBullet();
        }
    }

    private void DestroyBullet()
    {
        FluidController.Instance.QueueDrawAtPoint(
                this.transform.position,
                Color.white,
                Vector2.zero,
                0.6f,
                0.6f,
                FluidController.VelocityType.Explore
            );
        EntityPool.Instance.ReturnBullet(gameObject);
    }

    // 公共方法：设置子弹速度
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
        if (rb != null)
        {
            rb.velocity = transform.right * speed;
        }
    }

    // 公共方法：设置子弹方向
    public void SetDirection(Vector2 direction)
    {
        if (rb != null)
        {
            rb.velocity = direction.normalized * speed;
        }
        
        // 旋转子弹朝向移动方向
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    // 离开屏幕时自动销毁
    void OnBecameInvisible()
    {
        if (timeAlive > 1f) // 避免刚生成就销毁
        {
            DestroyBullet();
        }
    }
}
