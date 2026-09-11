using System;
using System.Collections.Generic;
using UnityEngine;

// 资源塔的一次产出配置（可配多个，矿石塔就产金币+矿石两种）
[System.Serializable]
public struct ResourceOutput
{
    [Tooltip("产出哪种资源")]
    public ResourceType type;

    [Tooltip("每次产出的数量")]
    public int amount;
}

// 资源塔数据：定时产出资源，可以同时产出多种
[CreateAssetMenu(fileName = "NewResourceTowerData", menuName = "The Ashen Blaze/建筑数据/资源塔")]
public class ResourceTowerDataSO : BuildingDataSO
{
    [Header("产出设置")]
    [Tooltip("产出列表（矿石塔配金币+矿石两种，木材塔/干粮塔各配一种）")]
    public List<ResourceOutput> outputs = new List<ResourceOutput>();

    [Tooltip("产出间隔（秒）")]
    public float produceInterval = 5f;
}
