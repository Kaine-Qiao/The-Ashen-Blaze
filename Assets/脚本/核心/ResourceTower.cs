using UnityEngine;

// 资源塔行为：定时产出资源（挂在有 Building 组件的塔上，放置时自动添加）
// 每隔 produceInterval 秒，把 outputs 列表里的每种资源加到对应阵营
public class ResourceTower : MonoBehaviour
{
    private ResourceTowerDataSO data;
    private Faction faction;
    private float timer;

    // 由 Building 挂载时调用，传入塔数据和所属阵营
    public void Initialize(ResourceTowerDataSO towerData, Faction owner)
    {
        data = towerData;
        faction = owner;
        timer = towerData.produceInterval;
    }

    private void Update()
    {
        if (data == null) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = data.produceInterval;
            Produce();
        }
    }

    // 产出一次：遍历产出列表，给所属阵营加上对应资源
    private void Produce()
    {
        if (ResourceManager.Instance == null) return;

        foreach (var output in data.outputs)
        {
            int newTotal = ResourceManager.Instance.AddResource(faction, output.type, output.amount);
            Debug.Log($"[资源塔] {name} 产出 {output.amount} {output.type}，当前总量：{newTotal}");
        }
    }
}
