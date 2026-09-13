using UnityEngine;

// 文化塔（箭阵塔）数据：对攻击范围内"多个"敌方目标齐射箭雨
// 与防御塔的区别：一次打多个目标（箭雨），单发伤害较低
[CreateAssetMenu(fileName = "NewCultureTowerData", menuName = "The Ashen Blaze/建筑数据/文化塔")]
public class CultureTowerDataSO : BuildingDataSO
{
    [Header("箭阵塔属性")]
    [Tooltip("每次齐射对每个目标造成的伤害")]
    public float attackDamage = 8f;

    [Tooltip("攻击范围（能覆盖多远的小兵）")]
    public float attackRange = 5f;

    [Tooltip("攻击间隔（秒）")]
    public float attackInterval = 2f;

    [Tooltip("一次最多打多少个目标")]
    public int maxTargets = 6;
}
