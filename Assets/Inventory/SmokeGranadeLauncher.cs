using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmokeGranadeLauncher : MonoBehaviour
{
    [SerializeField] Transform part1;
    [SerializeField] float FireRate = 120;
    [SerializeField] GameObject SmokeGranadePrefab;
    private float timeCounter;

    // Start is called before the first frame update
    void Start()
    {
        timeCounter = 0.0f;
    }

    // Update is called once per frame
    void Update()
    {
        timeCounter -= Time.deltaTime;

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;
        Vector2 direction = (mousePosition - transform.position).normalized;

        if (Input.GetMouseButtonDown(0) && timeCounter <= 0.0f)
        {
            timeCounter = 60.0f / FireRate;

            float isLeft = direction.x < 0f ? -1f : 1f;
            GameObject SmokeGranade = Instantiate(SmokeGranadePrefab, part1.position, part1.rotation);
            SmokeGranade.GetComponent<Rigidbody2D>().AddForce(direction * 10.0f, ForceMode2D.Impulse);
        }
    }
}

