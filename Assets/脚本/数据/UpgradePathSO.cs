using UnityEngine;

// 兵种塔升级路线的每一级加成（索引 0 = 第 1 级，4 = 第 5 级）
[System.Serializable]
public struct PathLevelData
{
    [Tooltip("攻击力 +%")]
    public float attackBonus;

    [Tooltip("防御力 +（固定值）")]
    public float defenseBonus;

    [Tooltip("生命值 +%")]
    public float hpBonus;

    [Tooltip("移速 +%")]
    public float speedBonus;

    [Tooltip("攻速 +%")]
    public float attackSpeedBonus;

    [Tooltip("攻击范围 +%")]
    public float rangeBonus;

    [Tooltip("本级的文字描述（显示在升级面板）")]
    public string description;
}

// 兵种塔的一条升级路线（比如"攻击路线""防御路线""速度路线"）
// 每个兵种塔数据资产配 3 条路线，每条路线最多 5 级
[CreateAssetMenu(fileName = "NewUpgradePath", menuName = "The Ashen Blaze/建筑数据/升级路线")]
public class UpgradePathSO : ScriptableObject
{
    [Tooltip("路线名称（如：攻击路线）")]
    public string pathName = "升级路线";

    [Tooltip("5 级加成（索引 0 = 1 级 ... 4 = 5 级）")]
    public PathLevelData[] levels = new PathLevelData[5];

    // 合并后的路线加成（用于合成 6 级塔时把 3 条路线加成叠加强化）
    [Header("6 级强化系数")]
    [Tooltip("合成 6 级时，本路线加成额外乘的系数")]
    public float mergeBoost = 1.5f;
}
