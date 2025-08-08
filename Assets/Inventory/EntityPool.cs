using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityPool : MonoBehaviour
{
    [System.Serializable]
    public class PoolItem
    {
        public GameObject prefab;
        public int poolSize = 10;
        [HideInInspector] public Queue<GameObject> pool = new Queue<GameObject>();
    }

    [SerializeField] private PoolItem bulletPool;
    [SerializeField] private PoolItem shellPool;

    public static EntityPool Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        InitializePools();
    }

    void InitializePools()
    {
        InitializePool(bulletPool);
        InitializePool(shellPool);
    }

    void InitializePool(PoolItem poolItem)
    {
        for (int i = 0; i < poolItem.poolSize; i++)
        {
            GameObject obj = Instantiate(poolItem.prefab, transform);
            obj.SetActive(false);
            poolItem.pool.Enqueue(obj);
        }
    }

    public GameObject GetBullet()
    {
        return GetFromPool(bulletPool);
    }

    public GameObject GetShell()
    {
        return GetFromPool(shellPool);
    }

    public void ReturnBullet(GameObject bullet)
    {
        ReturnToPool(bullet, bulletPool);
    }

    public void ReturnShell(GameObject shell)
    {
        ReturnToPool(shell, shellPool);
    }

    GameObject GetFromPool(PoolItem poolItem)
    {
        if (poolItem.pool.Count > 0)
        {
            GameObject obj = poolItem.pool.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            GameObject obj = Instantiate(poolItem.prefab, transform);
            obj.SetActive(true);
            return obj;
        }
    }

    void ReturnToPool(GameObject obj, PoolItem poolItem)
    {
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        poolItem.pool.Enqueue(obj);
    }
}
