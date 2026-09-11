using UnityEngine;

// 兵种塔数据：定时产出某一种小兵
[CreateAssetMenu(fileName = "NewBarracksData", menuName = "The Ashen Blaze/建筑数据/兵种塔")]
public class BarracksDataSO : BuildingDataSO
{
    [Header("出兵设置")]
    [Tooltip("产出哪种小兵")]
    public UnitType unitType = UnitType.Infantry;

    [Tooltip("出兵间隔（秒）")]
    public float spawnInterval = 5f;

    [Tooltip("每次出兵数量")]
    public int spawnCount = 1;

    [Header("出兵消耗")]
    [Tooltip("每次出兵消耗的干粮数量（粮食不够就不出兵）")]
    public int foodCostPerSpawn = 1;

    [Header("经验等级（初始）")]
    [Tooltip("兵种塔初始的经验等级，决定产出小兵的初始等级（以后经验系统会升级它）")]
    public int startLevel = 1;
}
