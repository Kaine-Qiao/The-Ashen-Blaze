using UnityEngine;

// 游戏中的四种资源类型
public enum ResourceType
{
    [Tooltip("金币：通用货币，建造/升级各种塔都要")]
    Gold,

    [Tooltip("矿石：建造防御塔、科技塔等重建筑")]
    Ore,

    [Tooltip("木材：建造兵种塔、文化塔等轻型建筑")]
    Wood,

    [Tooltip("干粮：文化塔、资源塔需要")]
    Food
}
