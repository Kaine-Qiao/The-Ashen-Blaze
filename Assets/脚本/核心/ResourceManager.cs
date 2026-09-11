using System.Collections.Generic;
using UnityEngine;

// 资源管理器（挂在场景中的空物体上）：
// 管理两个阵营各自的四种资源存量（金币/矿石/木材/干粮）
public class ResourceManager : MonoBehaviour
{
    [Header("玩家初始资源")]
    [Tooltip("开局玩家拥有的金币")]
    public int playerStartGold = 50;
    [Tooltip("开局玩家拥有的矿石")]
    public int playerStartOre = 20;
    [Tooltip("开局玩家拥有的木材")]
    public int playerStartWood = 30;
    [Tooltip("开局玩家拥有的干粮")]
    public int playerStartFood = 20;

    [Header("敌方初始资源")]
    [Tooltip("开局敌方拥有的金币")]
    public int enemyStartGold = 50;
    [Tooltip("开局敌方拥有的矿石")]
    public int enemyStartOre = 20;
    [Tooltip("开局敌方拥有的木材")]
    public int enemyStartWood = 30;
    [Tooltip("开局敌方拥有的干粮")]
    public int enemyStartFood = 20;

    // 单例，方便其他脚本随时访问
    public static ResourceManager Instance { get; private set; }

    // 两个阵营各自的资源存量
    private readonly Dictionary<Faction, Dictionary<ResourceType, int>> resources =
        new Dictionary<Faction, Dictionary<ResourceType, int>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 初始化两个阵营的四种资源
        InitFaction(Faction.Player, playerStartGold, playerStartOre, playerStartWood, playerStartFood);
        InitFaction(Faction.Enemy, enemyStartGold, enemyStartOre, enemyStartWood, enemyStartFood);
    }

    private void InitFaction(Faction faction, int gold, int ore, int wood, int food)
    {
        resources[faction] = new Dictionary<ResourceType, int>
        {
            { ResourceType.Gold, gold },
            { ResourceType.Ore, ore },
            { ResourceType.Wood, wood },
            { ResourceType.Food, food }
        };
    }

    // 获取某个阵营某种资源的存量
    public int GetResource(Faction faction, ResourceType type)
    {
        if (resources.TryGetValue(faction, out var dict) &&
            dict.TryGetValue(type, out int value))
        {
            return value;
        }
        return 0;
    }

    // 增加资源（返回增加后的存量）
    public int AddResource(Faction faction, ResourceType type, int amount)
    {
        if (amount <= 0) return GetResource(faction, type);
        resources[faction][type] = GetResource(faction, type) + amount;
        return resources[faction][type];
    }

    // 判断是否够扣（不够扣返回 false，不扣任何东西）
    public bool CanAfford(Faction faction, ResourceType type, int amount)
    {
        return GetResource(faction, type) >= amount;
    }

    // 扣除资源：足够才扣并返回 true，不够返回 false
    public bool SpendResource(Faction faction, ResourceType type, int amount)
    {
        if (!CanAfford(faction, type, amount)) return false;
        resources[faction][type] = GetResource(faction, type) - amount;
        return true;
    }

    // 一次性检查一组花费是否都能付得起（用于建造，不实际扣钱）
    public bool CanAffordCosts(Faction faction, Dictionary<ResourceType, int> costs)
    {
        foreach (var pair in costs)
        {
            if (!CanAfford(faction, pair.Key, pair.Value)) return false;
        }
        return true;
    }

    // 一次性扣除一组花费（用于建造），全部够才扣，缺一不可
    public bool SpendCosts(Faction faction, Dictionary<ResourceType, int> costs)
    {
        if (!CanAffordCosts(faction, costs)) return false;
        foreach (var pair in costs)
        {
            SpendResource(faction, pair.Key, pair.Value);
        }
        return true;
    }
}
