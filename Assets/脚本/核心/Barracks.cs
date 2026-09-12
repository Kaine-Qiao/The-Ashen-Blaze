using UnityEngine;

// 兵种塔行为：定时出兵（消耗干粮），小兵朝敌方方向移动
// 挂在有 Building 组件的塔上，放置时自动添加
public class Barracks : MonoBehaviour
{
    private BarracksDataSO data;
    private Faction faction;
    private float timer;

    // 由 Building 挂载时调用，传入塔数据和所属阵营
    public void Initialize(BarracksDataSO barracksData, Faction owner)
    {
        data = barracksData;
        faction = owner;
        timer = barracksData.spawnInterval;
    }

    private void Update()
    {
        if (data == null) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = data.spawnInterval;
            TrySpawn();
        }
    }

    // 尝试出兵：干粮够才出，不够这轮跳过（下一轮再试）
    private void TrySpawn()
    {
        if (ResourceManager.Instance == null || UnitManager.Instance == null) return;

        // 1. 检查干粮
        int foodCost = data.foodCostPerSpawn;
        if (!ResourceManager.Instance.CanAfford(faction, ResourceType.Food, foodCost))
        {
            Debug.Log($"[兵种塔] {name} 干粮不足（需要 {foodCost}），跳过本轮");
            return;
        }

        // 2. 检查有没有配小兵数据
        if (data.unitData == null)
        {
            Debug.LogWarning($"[兵种塔] {name} 没有配 unitData，无法出兵");
            return;
        }

        // 3. 扣干粮
        ResourceManager.Instance.SpendResource(faction, ResourceType.Food, foodCost);

        // 4. 生成小兵：逐个错开位置（X 朝敌方方向推进、Y 上下散开），避免重叠
        int dir = faction == Faction.Player ? 1 : -1;
        for (int i = 0; i < data.spawnCount; i++)
        {
            float xOff = dir * (0.5f + i * 0.9f);
            float yOff = (i - (data.spawnCount - 1) * 0.5f) * 0.45f;
            Vector3 spawnPos = transform.position + new Vector3(xOff, yOff, 0f);
            UnitManager.Instance.SpawnUnit(data.unitData, faction, spawnPos);
        }
        Debug.Log($"[兵种塔] {name} 出兵 {data.spawnCount} 个，消耗干粮 {foodCost}");
    }
}
