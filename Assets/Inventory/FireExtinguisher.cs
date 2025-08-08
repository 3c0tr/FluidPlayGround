using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireExtinguisher : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePosition.z = 0;
            Vector2 direction = (mousePosition - transform.position).normalized;
            Vector2 velocity = transform.right * (direction.x > 0f ? 5f : -5f);

            FluidController.Instance.QueueDrawAtPoint(
                                            transform.position,
                                            Color.white,
                                            velocity, // 修改为物体的right方向
                                            1f,
                                            1.3f,
                                            FluidController.VelocityType.Direct
                                        );
        }
    }
}
