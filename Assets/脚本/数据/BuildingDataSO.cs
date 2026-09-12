using UnityEngine;

// 建筑类型枚举：区分所有塔的种类
public enum BuildingType
{
    [Tooltip("资源塔：产出四种资源")]
    ResourceTower,

    [Tooltip("兵种塔：定时产出小兵")]
    Barracks,

    [Tooltip("防御塔：箭塔/炮塔/城墙/陷阱")]
    DefenseTower,

    [Tooltip("文化塔：增益塔/箭阵塔")]
    CultureTower,

    [Tooltip("科技塔：研究解锁更高级建筑")]
    TechTower
}

// 建筑通用数据（ScriptableObject 资产）：
// 所有塔共有的基础信息。具体的塔（资源塔/兵种塔等）继承这个类并扩展自己的字段。
// 在 Project 面板创建资产后，数值随时可改，不需要改代码。
[CreateAssetMenu(fileName = "NewBuildingData", menuName = "The Ashen Blaze/建筑数据/通用建筑")]
public class BuildingDataSO : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("显示名称")]
    public string displayName = "建筑";

    [Tooltip("建筑类型")]
    public BuildingType buildingType = BuildingType.ResourceTower;

    [Tooltip("建筑显示图片（每个塔配自己的图）")]
    public Sprite icon;

    [Header("占格大小")]
    [Tooltip("占几格宽（红警式，建筑有自己的大小）")]
    public int gridWidth = 1;

    [Tooltip("占几格高")]
    public int gridHeight = 1;

    [Header("建造成本")]
    [Tooltip("建造所需资源")]
    public ResourceCost buildCost;

    [Header("属性")]
    [Tooltip("建筑血量")]
    public float maxHP = 200f;
}
