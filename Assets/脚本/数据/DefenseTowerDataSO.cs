using UnityEngine;

// 防御塔数据（ScriptableObject 资产）：箭塔/炮塔等攻击型防御塔的属性
// 防御塔会攻击进入攻击范围的敌方小兵
[CreateAssetMenu(fileName = "NewDefenseTowerData", menuName = "The Ashen Blaze/建筑数据/防御塔")]
public class DefenseTowerDataSO : BuildingDataSO
{
    [Header("防御塔属性")]
    [Tooltip("攻击伤害")]
    public float attackDamage = 15f;

    [Tooltip("攻击范围（能打多远的小兵）")]
    public float attackRange = 4f;

    [Tooltip("攻击间隔（秒）")]
    public float attackInterval = 1.5f;
}
