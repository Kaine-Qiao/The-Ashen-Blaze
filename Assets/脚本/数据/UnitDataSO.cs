using UnityEngine;

// 小兵种类枚举：兵种塔产出的五种兵
public enum UnitType
{
    [Tooltip("步兵")]
    Infantry,

    [Tooltip("弓兵")]
    Archer,

    [Tooltip("盾兵")]
    Shield,

    [Tooltip("骑兵")]
    Cavalry,

    [Tooltip("刺客")]
    Assassin
}

// 小兵属性数据：某个兵种的基础数值
[CreateAssetMenu(fileName = "NewUnitData", menuName = "The Ashen Blaze/单位数据")]
public class UnitDataSO : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("小兵种类")]
    public UnitType unitType = UnitType.Infantry;

    [Tooltip("显示名称")]
    public string displayName = "步兵";

    [Header("基础属性")]
    [Tooltip("攻击力")]
    public float attack = 10f;

    [Tooltip("防御力（减免受到的伤害）")]
    public float defense = 0f;

    [Tooltip("移动速度")]
    public float moveSpeed = 3f;

    [Tooltip("攻击速度（每秒攻击次数）")]
    public float attackSpeed = 1f;

    [Tooltip("最大生命")]
    public float maxHP = 100f;

    [Tooltip("攻击范围（攻击距离）")]
    public float attackRange = 1.5f;

    [Header("经验")]
    [Tooltip("该兵被击杀后，提供给击杀者的经验值（以后经验系统用）")]
    public int expProvided = 10;
}
