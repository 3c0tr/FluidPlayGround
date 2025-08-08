using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetShaderProp : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.material.SetVector("ObjectWorldPosition", new Vector2(transform.position.x, transform.position.y));
        }
    }
}
