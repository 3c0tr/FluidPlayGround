using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MagicFlare : MonoBehaviour
{
    [SerializeField] private GameObject colorExplorion;
    [SerializeField] private UnityEngine.Rendering.Universal.Light2D light;


    private Rigidbody2D rb;
    public Color col;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mousePos - (Vector2)transform.position).normalized;
        rb.AddForce(direction * 50f);
        light.color = col;

        FluidController.Instance.QueueDrawAtPoint(
                this.transform.position,
                col,
                rb.velocity * 0.5f,
                0.3f, // 0.3
                0.3f,
                FluidController.VelocityType.Direct
            );
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        GameObject explorion = Instantiate(colorExplorion, this.transform.position, Quaternion.identity);
        explorion.GetComponent<ColorExplorion>().col = col;
        Destroy(gameObject);
    }
}
