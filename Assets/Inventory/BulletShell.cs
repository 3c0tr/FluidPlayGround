using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletShell : MonoBehaviour
{
    public float timeSpan = 0.0f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        timeSpan += Time.deltaTime;
        if (timeSpan > 2.0f)
        {
            EntityPool.Instance.ReturnShell(gameObject);
        }
        this.transform.position = new Vector3(  this.transform.position.x, 
                                                this.transform.position.y,
                                                Mathf.Min(timeSpan * 10, 1.0f) * -0.33f);   
    }
}
