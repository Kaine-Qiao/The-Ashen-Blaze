using UnityEngine;

// 兵种塔行为：定时出兵（消耗干粮），小兵朝敌方方向移动
// 每升一级：出兵数 +1、出兵间隔 -10%
// 3 路线系统：塔上挂的 Building 记录主线/分支选择，产出的兵继承路线属性加成
// 经验等级：塔自己会积累经验（它产出的兵击杀敌人时塔获得一半经验），
//           经验等级越高，产出的兵出生等级越高（越强）
public class Barracks : MonoBehaviour
{
    private BarracksDataSO baseData;
    private Faction faction;
    private float timer;

    public int currentLevel = 1;

    [Header("经验等级（兵种塔）")]
    [Tooltip("塔的经验等级（决定产出小兵的出生等级，初始 = 数据资产里的 startLevel）")]
    public int expLevel = 1;
    [Tooltip("塔当前经验")]
    public int exp = 0;
    [Tooltip("塔升级所需基础经验（实际 = 基础 + 等级 × 增量）")]
    public int expBase = 40;
    [Tooltip("每级经验增量")]
    public int expPerLevelInc = 30;
    [Tooltip("经验等级上限")]
    public int maxExpLevel = 10;

    public void Initialize(BarracksDataSO barracksData, Faction owner)
    {
        baseData = barracksData;
        faction = owner;
        currentLevel = 1;
        expLevel = Mathf.Max(1, barracksData != null ? barracksData.startLevel : 1);
        timer = GetSpawnInterval();
    }

    private void Update()
    {
        if (baseData == null) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = GetSpawnInterval();
            TrySpawn();
        }
    }

    // 出兵间隔：每级 -10%，再乘玩家科技攻速加成
    private float GetSpawnInterval()
    {
        float interval = baseData.spawnInterval * Mathf.Pow(0.9f, currentLevel - 1);
        if (faction == Faction.Player && TechManager.Instance != null)
            interval *= 1f - TechManager.Instance.GetAttackSpeedBonus();
        return Mathf.Max(0.2f, interval);
    }

    // 出兵数：基础 + 每级 +1
    private int GetSpawnCount()
    {
        return baseData.spawnCount + (currentLevel - 1);
    }

    public void SetLevel(int level)
    {
        currentLevel = level;
    }

    // 塔获得经验（它产出的兵击杀敌人时调用），升级提升出生等级
    public void AddExp(int amount)
    {
        if (amount <= 0 || expLevel >= maxExpLevel) return;
        exp += amount;

        int need = expBase + expPerLevelInc * (expLevel - 1);
        while (exp >= need && expLevel < maxExpLevel)
        {
            exp -= need;
            expLevel++;
            need = expBase + expPerLevelInc * (expLevel - 1);
            Debug.Log($"[兵种塔] {name} 经验等级升到 {expLevel}（以后出兵都是 {expLevel} 级兵）");
        }
    }

    private void TrySpawn()
    {
        if (ResourceManager.Instance == null || UnitManager.Instance == null) return;

        int foodCost = baseData.foodCostPerSpawn;
        if (!ResourceManager.Instance.CanAfford(faction, ResourceType.Food, foodCost))
        {
            Debug.Log($"[兵种塔] {name} 干粮不足（需要 {foodCost}），跳过本轮");
            return;
        }

        if (baseData.unitData == null)
        {
            Debug.LogWarning($"[兵种塔] {name} 没有配 unitData，无法出兵");
            return;
        }

        ResourceManager.Instance.SpendResource(faction, ResourceType.Food, foodCost);

        // 3 路线加成：从同物体的 Building 上取当前主线/分支累计加成，传给出生的兵
        PathLevelData pathBonus = default;
        var building = GetComponent<Building>();
        if (building != null) pathBonus = building.GetTotalPathBonus();

        int count = GetSpawnCount();
        int dir = faction == Faction.Player ? 1 : -1;
        for (int i = 0; i < count; i++)
        {
            float xOff = dir * (0.5f + i * 0.9f);
            float yOff = (i - (count - 1) * 0.5f) * 0.45f;
            Vector3 spawnPos = transform.position + new Vector3(xOff, yOff, 0f);
            UnitManager.Instance.SpawnUnit(baseData.unitData, faction, spawnPos, expLevel, pathBonus, this);
        }
    }
}
