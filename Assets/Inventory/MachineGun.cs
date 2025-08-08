using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MachineGun : MonoBehaviour
{
    [SerializeField] Transform GunPoint;
    [SerializeField] float FireRate = 600;
    [SerializeField] UnityEngine.Rendering.Universal.Light2D muzzleFlash;
    [SerializeField] Transform part1;
    [SerializeField] Transform ShellPoint;
    private float timeCounter;
    private float AfterShooting;
    private float recoilValue;
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
        Vector2 velocity = transform.right * (direction.x > 0f ? 10f : -10f);

        if (Input.GetMouseButton(0) && timeCounter <= 0.0f)
        {
            timeCounter = 60.0f / FireRate;
            muzzleFlash.intensity = 3.0f;
            AfterShooting = 60.0f / FireRate;
            GameObject shell = EntityPool.Instance.GetShell();
            shell.GetComponent<BulletShell>().timeSpan = 0.0f;
            shell.transform.position = ShellPoint.position;
            shell.transform.rotation = ShellPoint.rotation;

            float isLeft = direction.x < 0f ? -1f : 1f;
            Rigidbody2D shellRb = shell.GetComponent<Rigidbody2D>();
            Vector2 localVelocity = new Vector2(Random.Range(-0.7f, -0.05f) * isLeft, Random.Range(2.0f, 2.5f));
            shellRb.velocity += (Vector2)shell.transform.TransformDirection(localVelocity);
            shellRb.angularVelocity = Random.Range(180f, 720f) * isLeft;

            GameObject head = EntityPool.Instance.GetBullet();
            head.GetComponent<BulletHead>().timeAlive = 0.0f;
            head.GetComponent<BulletHead>().hasHit = false;
            head.transform.position = GunPoint.position;
            head.transform.rotation = GunPoint.rotation;
            head.GetComponent<BulletHead>().SetSpeed(20.0f);
            head.GetComponent<BulletHead>().SetDirection(GunPoint.right * isLeft);
            AddRecoil();
        }
        muzzleFlash.intensity = 3.0f * Mathf.Max(AfterShooting * 10f, 0.0f);

        FluidController.Instance.QueueDrawAtPoint(
                                GunPoint.position,
                                Color.white,
                                velocity,
                                0.7f * Mathf.Max(AfterShooting * 10f, 0.0f),
                                1.2f * Mathf.Max(AfterShooting * 10f, 0.0f),
                                FluidController.VelocityType.Direct
                            );

        float AftShooting01 = (FireRate / 60.0f) * Mathf.Max(AfterShooting, 0.0f);

        //this.transform.localPosition = new Vector3(Mathf.Sin(Mathf.PI * 0.5f * AftShooting01) * -0.15f, 0, 0);
        this.transform.localRotation = Quaternion.Euler(0, 0, recoilValue);
        ReduceRecoil(10.0f * (1.0f - AftShooting01));
        float temp = AftShooting01 > 0.5f ? 2 * (1 - AftShooting01) : 2 * AftShooting01;
        part1.localPosition = new Vector3(1.132f + temp * -0.241f, part1.localPosition.y, part1.localPosition.z);
    }

    void AddRecoil()
    {
        recoilValue += (5.0f + Random.Range(0.0f, 1.0f) * 4f) * 0.05f *(20 - recoilValue);
    }

    void ReduceRecoil(float scale)
    {
        recoilValue *= (1.0f - scale * Time.deltaTime);
    }
}
