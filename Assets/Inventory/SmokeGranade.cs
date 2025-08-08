using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmokeGranade : MonoBehaviour
{
    private float timeSpan = 0.0f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (timeSpan > 5.0f)
        {
            FluidController.Instance.QueueDrawAtPoint(
                this.transform.position,
                new Color(1.0f, 1.0f, 1.0f, 1.2f),// * Mathf.Max(temp, 0.5f),
                Vector2.zero,
                0.3f * Mathf.Clamp01(-timeSpan * 0.25f + 6.0f),
                0.4f * Mathf.Clamp01(-timeSpan * 0.25f + 6.0f),
                FluidController.VelocityType.Explore
            );
        }
        if (timeSpan > 25.0f)
        {
            Destroy(this.gameObject);
        }
        timeSpan += Time.deltaTime;
    }
}
