using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchWeaponInHand : MonoBehaviour
{
    [SerializeField] GameObject[] WeaponInHands;
    int curIndex = 0;
    
    void Start()
    {
        UpdateWeapons();
    }

    void Update()
    {
        // Q键 - 下一个
        if (Input.GetKeyDown(KeyCode.Q))
        {
            curIndex = (curIndex + 1) % WeaponInHands.Length;
            UpdateWeapons();
        }
        
        // E键 - 上一个
        if (Input.GetKeyDown(KeyCode.E))
        {
            curIndex = (curIndex - 1 + WeaponInHands.Length) % WeaponInHands.Length;
            UpdateWeapons();
        }
        
        // 鼠标滚轮
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f)
        {
            curIndex = (curIndex + 1) % WeaponInHands.Length;
            UpdateWeapons();
        }
        else if (scroll < 0f)
        {
            curIndex = (curIndex - 1 + WeaponInHands.Length) % WeaponInHands.Length;
            UpdateWeapons();
        }
    }
    
    void UpdateWeapons()
    {
        for (int i = 0; i < WeaponInHands.Length; i++)
        {
            WeaponInHands[i].SetActive(i == curIndex);
        }
    }
}
