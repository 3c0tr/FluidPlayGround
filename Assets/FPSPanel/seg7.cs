using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class seg7 : MonoBehaviour
{
    [SerializeField] private GameObject[] seg;
    [SerializeField] private Color segColor;
    // Start is called before the first frame update
    bool[][] segState = new bool[10][];
    
    void Start()
    {
        InitializeSegmentStates();
    }
    
    void InitializeSegmentStates()
    {
        // 7段显示器布局：[0]顶部, [1]左上, [2]右上, [3]中间, [4]左下, [5]右下, [6]底部
        //   [0]
        // [1] [2]
        //   [3]
        // [4] [5]
        //   [6]
        
        segState[0] = new bool[] { true,  true,  true,  false, true,  true,  true  }; // 数字0: 显示0,1,2,4,5,6
        segState[1] = new bool[] { false, false, true,  false, false, true,  false }; // 数字1: 显示2,5
        segState[2] = new bool[] { true,  false, true,  true,  true,  false, true  }; // 数字2: 显示0,2,3,4,6
        segState[3] = new bool[] { true,  false, true,  true,  false, true,  true  }; // 数字3: 显示0,2,3,5,6
        segState[4] = new bool[] { false, true,  true,  true,  false, true,  false }; // 数字4: 显示1,2,3,5
        segState[5] = new bool[] { true,  true,  false, true,  false, true,  true  }; // 数字5: 显示0,1,3,5,6
        segState[6] = new bool[] { true,  true,  false, true,  true,  true,  true  }; // 数字6: 显示0,1,3,4,5,6
        segState[7] = new bool[] { true,  false, true,  false, false, true,  false }; // 数字7: 显示0,2,5
        segState[8] = new bool[] { true,  true,  true,  true,  true,  true,  true  }; // 数字8: 显示所有段
        segState[9] = new bool[] { true,  true,  true,  true,  false, true,  true  }; // 数字9: 显示0,1,2,3,5,6
    }

    public void UpdateNumber(int number, Color col)
    {
        // 确保数字在0-9范围内
        if (number < 0 || number > 9) return;
        
        // 根据数字更新各段的显示状态
        for (int i = 0; i < seg.Length && i < 7; i++)
        {
            SpriteRenderer sr = seg[i].GetComponent<SpriteRenderer>();
            Color segColorOff = col * 0.2f;
            segColorOff.a = 1.0f;
            sr.color = segState[number][i] ? col : segColorOff;
        }
    }
}
