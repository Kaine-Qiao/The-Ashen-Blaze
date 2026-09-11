using System.Collections.Generic;
using UnityEngine;

// 建造成本：建塔只消耗金币/矿石/木材（干粮不参与建塔，干粮是出兵消耗）
[System.Serializable]
public struct ResourceCost
{
    [Tooltip("金币")]
    public int gold;
    [Tooltip("矿石")]
    public int ore;
    [Tooltip("木材")]
    public int wood;

    // 转换成字典形式，方便直接交给 ResourceManager.SpendCosts 使用
    public Dictionary<ResourceType, int> ToDictionary()
    {
        var dict = new Dictionary<ResourceType, int>
        {
            { ResourceType.Gold, gold },
            { ResourceType.Ore, ore },
            { ResourceType.Wood, wood }
        };
        return dict;
    }
}
