using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketLauncher : MonoBehaviour
{
    [SerializeField] Transform part1;
    [SerializeField] Transform part2;
    [SerializeField] float FireRate = 600;
    [SerializeField] UnityEngine.Rendering.Universal.Light2D flash;
    [SerializeField] GameObject RocketPrefab;
    private float timeCounter;
    private float AfterShooting;
    // Start is called before the first frame update
    void Start()
    {
        timeCounter = 0.0f;
    }

    // Update is called once per frame
    void Update()
    {
        timeCounter -= Time.deltaTime;
        AfterShooting -= Time.deltaTime;

        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;
        Vector2 direction = (mousePosition - transform.position).normalized;

        if (Input.GetMouseButtonDown(0) && timeCounter <= 0.0f)
        {
            AfterShooting = 60.0f / FireRate;
            timeCounter = 60.0f / FireRate;

            float isLeft = direction.x < 0f ? -1f : 1f;
            GameObject Rocket = Instantiate(RocketPrefab, part2.position, part2.rotation);
            Rocket.GetComponent<Rocket>().SetSpeed(15.0f);
            Rocket.GetComponent<Rocket>().SetDirection(part2.right * isLeft);
        }

        float AfterShooting01 = AfterShooting * (FireRate / 60.0f);
        float temp = Mathf.Max((AfterShooting01 * 5.0f - 4.0f), 0.0f);
        flash.intensity = 3.0f * temp;
        float temp2 = Mathf.Max((AfterShooting01 * 3.0f - 2.0f), 0.0f);

        FluidController.Instance.QueueDrawAtPoint(
                        part1.position,
                        Color.white,// * Mathf.Max(temp, 0.5f),
                        part1.up * 10f,
                        1.0f * temp2,
                        1.2f * temp2,
                        FluidController.VelocityType.Direct
                    );
    }
}
