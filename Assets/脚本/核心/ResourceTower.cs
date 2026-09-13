using UnityEngine;

// 资源塔行为：定时产出资源（挂在有 Building 组件的塔上，放置时自动添加）
// 每升一级：产出量 +20%、产出间隔 -5%
public class ResourceTower : MonoBehaviour
{
    private ResourceTowerDataSO baseData;
    private Faction faction;
    private float timer;

    // 运行时等级
    public int currentLevel = 1;

    public void Initialize(ResourceTowerDataSO towerData, Faction owner)
    {
        baseData = towerData;
        faction = owner;
        currentLevel = 1;
        timer = GetProduceInterval();
    }

    private void Update()
    {
        if (baseData == null) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = GetProduceInterval();
            Produce();
        }
    }

    // 产出量按等级加成（每级 +20%），再乘玩家科技资源产出加成
    private int GetOutputAmount(int baseAmount)
    {
        float amount = baseAmount * (1f + 0.2f * (currentLevel - 1));
        if (faction == Faction.Player && TechManager.Instance != null)
            amount *= 1f + TechManager.Instance.GetResourceBonus();
        return Mathf.RoundToInt(amount);
    }

    // 产出间隔按等级缩短（每级 -5%）
    private float GetProduceInterval()
    {
        return baseData.produceInterval * Mathf.Pow(0.95f, currentLevel - 1);
    }

    public void SetLevel(int level)
    {
        currentLevel = level;
    }

    private void Produce()
    {
        if (ResourceManager.Instance == null) return;

        foreach (var output in baseData.outputs)
        {
            int amount = GetOutputAmount(output.amount);
            ResourceManager.Instance.AddResource(faction, output.type, amount);
        }
    }
}
