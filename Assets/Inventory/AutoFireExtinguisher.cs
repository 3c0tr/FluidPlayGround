using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoFireExtinguisher : MonoBehaviour
{
    [SerializeField] private float firerate = 1f;

    // Update is called once per frame
    void Update()
    {
        FluidController.Instance.QueueDrawAtPoint(
                                        transform.position,
                                        Color.white,
                                        transform.right * 8f, // 修改为物体的right方向
                                        0.7f * firerate,
                                        1f * firerate,
                                        FluidController.VelocityType.Direct
                                    );
        
    }
}
