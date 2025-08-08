using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorExplorion : MonoBehaviour
{
    public Color col;
    private float timeSpan = 0.2f;
    [SerializeField] UnityEngine.Rendering.Universal.Light2D flash;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        FluidController.Instance.QueueDrawAtPoint(
                this.transform.position,
                new Color(col.r * timeSpan * 5f, col.g * timeSpan * 5f, col.b * timeSpan * 5f, 1.2f),// * Mathf.Max(temp, 0.5f),
                Vector2.zero,
                2.0f * timeSpan * 5f,
                3.2f * timeSpan * 5f,
                FluidController.VelocityType.Explore
            );
        flash.color = col;
        flash.intensity = 3.0f * timeSpan * 5f;
        timeSpan -= Time.deltaTime;
        if (timeSpan <= 0)
        {
            Destroy(gameObject);
        }
    }
}
