using UnityEngine;

// 科技效果类型：研究后对玩家阵营产生的全局效果
public enum TechEffectType
{
    [Tooltip("全局攻击加成（玩家所有兵/塔伤害提升）")]
    AttackBoost,

    [Tooltip("全局防御加成（玩家所有兵受到的伤害减少）")]
    DefenseBoost,

    [Tooltip("资源产出加成（玩家所有资源塔产出提升）")]
    ResourceBoost,

    [Tooltip("攻速加成（出兵间隔/攻击间隔缩短）")]
    AttackSpeedBoost,

    [Tooltip("解锁一个新建筑（建塔菜单里出现该塔）")]
    UnlockBuilding
}

// 科技研究数据（ScriptableObject 资产）：
// 在 Project 面板创建资产后填名称/成本/效果，把资产拖进 TechManager 的科技列表里
[CreateAssetMenu(fileName = "NewTech", menuName = "The Ashen Blaze/建筑数据/科技研究")]
public class TechDataSO : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("科技名称")]
    public string techName = "新科技";

    [Tooltip("科技说明（研究后获得什么）")]
    [TextArea(2, 4)]
    public string description = "研究后获得强化效果";

    [Header("研究成本")]
    [Tooltip("研究花费（金币/矿石/木材）")]
    public ResourceCost cost;

    [Header("效果")]
    [Tooltip("效果类型")]
    public TechEffectType effectType = TechEffectType.AttackBoost;

    [Tooltip("效果数值（加成比例，0.1 = +10%；解锁建筑时不用填）")]
    public float effectValue = 0.1f;

    [Tooltip("解锁建筑（仅 UnlockBuilding 类型用：研究后建塔菜单出现该塔）")]
    public BuildingDataSO unlockBuilding;

    [Header("前置科技")]
    [Tooltip("前置科技（可填多个，全部研究后才能研究本科技）")]
    public TechDataSO[] prerequisites;
}
