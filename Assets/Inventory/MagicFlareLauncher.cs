using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MagicFlareLauncher : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sprites;
    [SerializeField] private float FireRate = 60.0f;
    [SerializeField] private GameObject MagicFlarePrefab;
    [SerializeField] private Transform part1;

    private float timeCounter;
    private Color col;

    // Start is called before the first frame update
    void Start()
    {
        col = GetRandomHSVColor(0.9f, 0.9f);
        sprites.color = col;
        timeCounter = 0.0f;
    }

    Color GetRandomHSVColor(float saturation, float value)
    {
        float hue = Random.Range(0f, 1f); // H随机 [0,1]
        return Color.HSVToRGB(hue, saturation, value);
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
            GameObject MagicFlare = Instantiate(MagicFlarePrefab, part1.position, part1.rotation);
            MagicFlare.GetComponent<Rigidbody2D>().AddForce(direction * 5.0f, ForceMode2D.Impulse);
            MagicFlare.GetComponent<MagicFlare>().col = col;

            col = GetRandomHSVColor(0.9f, 0.9f);
            sprites.color = col;
        }
    }
}
