using System.Collections.Generic;
using UnityEngine;

// 敌方 AI v2：会算账、会看局势的对手
// 核心能力：
//   1. 计算场上己方资源塔的实际产出速率（金/矿/木/粮 每秒，含等级加成）
//   2. 计算兵种塔的干粮消耗速率，干粮供不上就补干粮塔
//   3. 金币产出率太低时，优先建"产金币"的塔（矿石塔），绝不先建干粮塔
//   4. 根据局势决策：补塔 / 升级 / 扩张，资源不浪费
public class EnemyAI : MonoBehaviour
{
    [Header("决策")]
    [Tooltip("每隔多少秒做一次决策（AI 每周期只能做一个动作，靠这个补偿鼠标速度）")]
    public float thinkInterval = 1.5f;

    [Header("目标数量")]
    public int targetBarracks = 4;
    public int targetDefenseTowers = 2;

    [Header("资源塔细分目标（学习玩家精简打法：2 矿石 + 1 木材就够）")]
    [Tooltip("产金币的矿石塔目标数量（2 座攒钱快一倍，玩家也是造 2-3 座）")]
    public int oreTowerTarget = 2;
    [Tooltip("木材塔目标数量")]
    public int woodTowerTarget = 2;

    [Header("升级策略")]
    public int maxUpgradeResourceLevel = 3;
    public int maxUpgradeBarracksLevel = 3;
    public int maxUpgradeHQLevel = 4;

    [Header("经济策略")]
    [Tooltip("金币低于这个值时只建资源塔（先发育）")]
    public int poorThreshold = 30;
    [Tooltip("干粮产出低于需求的比例，低于就补干粮塔")]
    public float foodSafetyMargin = 0.8f;

    [Header("升级成本系数（与玩家完全一致，不占 AI 便宜）")]
    public int upgradeGoldPerLevel = 50;
    public int upgradeOrePerLevel = 30;
    public int upgradeWoodPerLevel = 20;

    [Header("调试")]
    [Tooltip("每轮决策打印 AI 的思考过程（金/塔数/兵力/在攒什么钱），排查问题用，可关")]
    public bool verboseLog = true;

    private BuildingCatalogSO catalog;
    private float timer;

    // 玩家情报（战术针对用）
    private readonly List<string> playerBarracksNames = new List<string>(); // 玩家已造的兵种塔类型
    private int playerUnitCount = 0; // 玩家场上兵力
    private int enemyUnitCount = 0;  // 己方场上兵力

    // 场上敌方建筑的统计
    private List<Building> enemyBuildings = new List<Building>();
    private int resourceCount = 0;
    private int barracksCount = 0;
    private int defenseCount = 0;
    private int oreCount = 0;      // 产金币的矿石塔数量
    private int woodCount = 0;     // 木材塔数量
    private Building hq;

    // 产出速率（每秒）
    private float goldRate = 0f;
    private float oreRate = 0f;
    private float woodRate = 0f;
    private float foodRate = 0f;
    // 干粮消耗速率（每秒）
    private float foodNeed = 0f;

    private void Awake()
    {
        var bmm = FindObjectOfType<BuildModeManager>();
        if (bmm != null && bmm.catalog != null) catalog = bmm.catalog;

        if (catalog == null)
        {
            Debug.LogError("[EnemyAI] 找不到塔目录（catalog），请在 Inspector 手动拖入 BuildingCatalog 资产");
        }
    }

    private void Start()
    {
        // 版本标记：进 Play 后看 Console 有没有这行，确认跑的是新代码
        Debug.Log("[EnemyAI] v4 战术版启动（侦查玩家 + 针对性出兵 + 不造木材塔充数）");

        // 读取上一局玩家打法，调整本局策略（跨局学习）
        ApplyLearnedAdjustments();
    }

    // 跨局学习：根据 memory.json 里的玩家上一局打法，调整本局目标数量/决策频率
    // 规则（每局限定调整量，防止 AI 过强）：
    //   玩家上局赢了        → AI 加强经济（多造资源塔）
    //   玩家上局兵种塔≥4    → AI 加强防御（多造防御塔）
    //   玩家上局防御塔≥3    → AI 加强进攻（多造兵种塔）
    //   玩家上局资源塔≥5    → AI 决策更频繁（缩短决策间隔）
    private void ApplyLearnedAdjustments()
    {
        var mem = BattleLogRecorder.LoadMemory();
        if (mem == null)
        {
            Debug.Log("[EnemyAI] 没有学习记忆（第一局），使用默认策略");
            return;
        }

        if (mem.playerWon)
        {
            // 玩家上局赢了 → AI 加强经济（多造矿石塔）+ 资源塔能升更高
            oreTowerTarget = Mathf.Min(2, oreTowerTarget + 1);
            maxUpgradeResourceLevel = Mathf.Max(maxUpgradeResourceLevel, 4);
        }
        if (mem.playerBarracksCount >= 4) targetDefenseTowers = Mathf.Max(targetDefenseTowers, 3);
        if (mem.playerDefenseCount >= 3) targetBarracks = Mathf.Min(6, targetBarracks + 1);
        if (mem.playerResourceCount >= 5) thinkInterval = Mathf.Max(1.0f, thinkInterval - 0.5f);

        Debug.Log($"[EnemyAI] 已学习上局玩家打法（玩家{(mem.playerWon ? "赢了" : "输了")}，兵种塔{mem.playerBarracksCount} 防御塔{mem.playerDefenseCount} 资源塔{mem.playerResourceCount}）→ 本局：矿石塔目标{oreTowerTarget} 兵种塔目标{targetBarracks} 防御塔目标{targetDefenseTowers} 决策间隔{thinkInterval:F1}s");
    }

    private void Update()
    {
        if (catalog == null || catalog.towers.Count == 0) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = thinkInterval;
            Think();
        }
    }

    // ===== 主决策 =====

    private void Think()
    {
        RefreshStats();
        RefreshPlayerIntel();   // 侦查玩家建筑 + 双方兵力
        ComputeRates();

        int gold = ResourceManager.Instance.GetResource(Faction.Enemy, ResourceType.Gold);
        bool rich = gold >= poorThreshold;

        // 每轮打印状态：一眼看出 AI 在想什么、卡在哪
        if (verboseLog)
        {
            Debug.Log($"[AI-状态] 金{gold} 矿塔{oreCount}/{oreTowerTarget} 木塔{woodCount}/{woodTowerTarget} 粮速{foodRate:F1} 兵塔{barracksCount}/{targetBarracks} 防塔{defenseCount}/{targetDefenseTowers} | 我兵{enemyUnitCount} 敌兵{playerUnitCount} 富余:{rich}");
        }

        // ===== 阶段 1：基础发育（每一项都是必须品，买不起就攒钱，绝不拿便宜塔充数）=====
        if (oreCount < oreTowerTarget)
        {
            if (TryBuildResourceTower(true)) return;
            if (verboseLog) Debug.Log("[AI-决策] 矿石塔没买得起，攒钱");
            return; // 攒钱
        }
        if (woodCount < 1)
        {
            if (TryBuildResourceTower_Producing(ResourceType.Wood)) return;
            if (verboseLog) Debug.Log("[AI-决策] 木材塔没买得起，攒钱");
            return; // 攒钱
        }
        if (foodRate <= 0f)
        {
            if (TryBuildResourceTower_Producing(ResourceType.Food)) return;
            if (verboseLog) Debug.Log("[AI-决策] 干粮塔没买得起，攒钱");
            return; // 攒钱
        }
        if (barracksCount == 0)
        {
            if (TryBuildCounterBarracks()) return;
            if (verboseLog) Debug.Log("[AI-决策] 兵种塔没买得起，攒钱");
            return; // 攒钱
        }
        if (defenseCount == 0)
        {
            if (TryBuild(BuildingType.DefenseTower)) return;
            if (verboseLog) Debug.Log("[AI-决策] 防御塔没买得起，攒钱");
            return; // 攒钱
        }

        // ===== 阶段 2：战术应对（针对玩家局势）=====
        // 玩家兵力远多于己方 → 防守：补同类型兵种塔对打拖延 + 升级兵种塔 + 补防御
        bool underPressure = playerUnitCount > enemyUnitCount * 1.5f + 3;
        if (underPressure)
        {
            if (verboseLog) Debug.Log("[AI-决策] 玩家兵力占优！转入防守");
            if (TryBuildCounterBarracks()) return;                        // 造和玩家同类型的兵种塔，出兵拖延
            if (TryUpgradeRandom(ResourceUpgradeKind.Barracks)) return;   // 升级兵种塔，出兵更多更快
            if (TryBuild(BuildingType.DefenseTower)) return;              // 再补防御塔拦截
            if (rich) { if (TryUpgradeRandom(ResourceUpgradeKind.ResourceTower)) return; }
        }

        // 己方兵力占优或兵种塔没造满 → 扩兵，找反推机会
        bool winning = enemyUnitCount > playerUnitCount * 1.2f + 3;
        if (winning || barracksCount < targetBarracks)
        {
            if (TryBuildCounterBarracks()) return;
            if (TryUpgradeRandom(ResourceUpgradeKind.Barracks)) return;
        }

        // 升级矿石塔滚雪球（经济够就升）
        if (rich && oreCount > 0 && TryUpgradeRandom(ResourceUpgradeKind.ResourceTower)) return;

        // 防御塔补满
        if (defenseCount < targetDefenseTowers)
        {
            if (TryBuild(BuildingType.DefenseTower)) return;
        }

        // 干粮不够 → 补干粮塔
        if (foodRate < foodNeed * foodSafetyMargin)
        {
            if (TryBuildResourceTower_Producing(ResourceType.Food)) return;
        }

        // 只有富余时才补木材塔（防止没钱时拿木材塔泄洪）
        if (rich && woodCount < woodTowerTarget)
        {
            if (TryBuildResourceTower_Producing(ResourceType.Wood)) return;
        }

        // 有余钱就升级/扩张（按比例）
        int roll = Random.Range(0, 100);
        if (roll < 35) { if (TryUpgradeRandom(ResourceUpgradeKind.Barracks)) return; }
        else if (roll < 55) { if (TryUpgradeHQ()) return; }
        else
        {
            // 扩张：补最缺的（不造木材塔充数）
            if (TryBuildResourceTower(true)) return;
            if (TryBuildCounterBarracks()) return;
            if (TryBuild(BuildingType.DefenseTower)) return;
        }
    }

    private enum ResourceUpgradeKind { ResourceTower, Barracks }

    // ===== 统计 + 速率计算 =====

    private void RefreshStats()
    {
        enemyBuildings.Clear();
        resourceCount = 0;
        barracksCount = 0;
        defenseCount = 0;
        oreCount = 0;
        woodCount = 0;
        hq = null;

        foreach (var b in FindObjectsOfType<Building>())
        {
            if (b.faction != Faction.Enemy) continue;

            enemyBuildings.Add(b);
            if (b.isBase) { hq = b; continue; }
            if (b.data == null) continue;

            switch (b.data.buildingType)
            {
                case BuildingType.ResourceTower:
                    resourceCount++;
                    // 细分：产金币的算矿石塔，产木材的算木材塔
                    if (b.data is ResourceTowerDataSO rt)
                    {
                        bool gold = false, wood = false;
                        foreach (var o in rt.outputs)
                        {
                            if (o.type == ResourceType.Gold) gold = true;
                            if (o.type == ResourceType.Wood) wood = true;
                        }
                        if (gold) oreCount++;
                        else if (wood) woodCount++;
                    }
                    break;
                case BuildingType.Barracks: barracksCount++; break;
                case BuildingType.DefenseTower: defenseCount++; break;
            }
        }
    }

    // 侦查玩家：记下玩家造了哪些兵种塔（用于针对性出兵），统计双方场上兵力
    private void RefreshPlayerIntel()
    {
        playerBarracksNames.Clear();
        foreach (var b in FindObjectsOfType<Building>())
        {
            if (b.faction == Faction.Player && b.data != null && b.data.buildingType == BuildingType.Barracks)
            {
                if (!playerBarracksNames.Contains(b.data.displayName))
                    playerBarracksNames.Add(b.data.displayName);
            }
        }

        playerUnitCount = 0;
        enemyUnitCount = 0;
        foreach (var u in FindObjectsOfType<Unit>())
        {
            if (u.faction == Faction.Player) playerUnitCount++;
            else if (u.faction == Faction.Enemy) enemyUnitCount++;
        }
    }

    // 计算资源塔产出速率 + 兵种塔干粮消耗速率（含等级加成，和 ResourceTower/Barracks 的公式一致）
    private void ComputeRates()
    {
        goldRate = 0f; oreRate = 0f; woodRate = 0f; foodRate = 0f;
        foodNeed = 0f;

        foreach (var b in enemyBuildings)
        {
            if (b.isBase || b.data == null) continue;
            int lv = b.level;

            // 资源塔产出速率
            if (b.data is ResourceTowerDataSO rtData)
            {
                float interval = rtData.produceInterval * Mathf.Pow(0.95f, lv - 1);
                if (interval <= 0f) continue;
                foreach (var output in rtData.outputs)
                {
                    float amount = output.amount * (1f + 0.2f * (lv - 1));
                    switch (output.type)
                    {
                        case ResourceType.Gold: goldRate += amount / interval; break;
                        case ResourceType.Ore: oreRate += amount / interval; break;
                        case ResourceType.Wood: woodRate += amount / interval; break;
                        case ResourceType.Food: foodRate += amount / interval; break;
                    }
                }
            }
            // 兵种塔干粮消耗速率
            else if (b.data is BarracksDataSO barData)
            {
                float interval = barData.spawnInterval * Mathf.Pow(0.9f, lv - 1);
                if (interval <= 0f) continue;
                foodNeed += barData.foodCostPerSpawn / interval;
            }
        }
    }

    // ===== 建造 =====

    // 建资源塔，优先产金币的（preferGold=true 时只挑产出里有金币的塔）
    private bool TryBuildResourceTower(bool preferGold)
    {
        BuildingDataSO data = PickResourceTower(preferGold);
        if (data == null) return false;
        return TryPlace(data);
    }

    // 建一个产出指定资源的资源塔（比如缺干粮时用）
    private bool TryBuildResourceTower_Producing(ResourceType type)
    {
        BuildingDataSO data = PickResourceTower_Producing(type);
        if (data == null) return false;
        return TryPlace(data);
    }

    private bool TryBuild(BuildingType type)
    {
        BuildingDataSO data = PickAffordable(type);
        if (data == null) return false;
        return TryPlace(data);
    }

    // 针对性出兵：造一座买得起的兵种塔，优先和玩家已造的兵种塔同类型（镜像对抗，步兵对步兵）
    private bool TryBuildCounterBarracks()
    {
        BuildingDataSO data = PickCounterBarracks();
        if (data == null) return false;
        return TryPlace(data);
    }

    // 选兵种塔：优先挑与玩家相同的兵种塔；玩家没兵种塔或都不匹配就随机
    private BuildingDataSO PickCounterBarracks()
    {
        var affordable = new List<BuildingDataSO>();
        foreach (var t in catalog.towers)
        {
            if (t == null || t.buildingType != BuildingType.Barracks) continue;
            if (ResourceManager.Instance.CanAffordCosts(Faction.Enemy, t.buildCost.ToDictionary()))
                affordable.Add(t);
        }
        if (affordable.Count == 0) return null;

        if (playerBarracksNames.Count > 0)
        {
            foreach (var b in affordable)
            {
                if (playerBarracksNames.Contains(b.displayName)) return b;
            }
        }
        return affordable[Random.Range(0, affordable.Count)];
    }

    private bool TryPlace(BuildingDataSO data)
    {
        Vector3 spot = FindSpot(data);
        if (spot == Vector3.zero) return false;

        var building = BuildingManager.Instance.TryPlaceBuilding(data, spot, Faction.Enemy);
        if (building != null)
        {
            // 记录决策（写入对战日志，供分析优化）
            if (BattleLogRecorder.Instance != null)
                BattleLogRecorder.Instance.RecordDecision("build", data.displayName, 1);
            Debug.Log($"[EnemyAI] 建造了 {data.displayName} @ ({spot.x:F1},{spot.y:F1})");
            return true;
        }
        return false;
    }

    // 选一个买得起的资源塔（preferGold=true 优先含金币产出的塔，如矿石塔）
    private BuildingDataSO PickResourceTower(bool preferGold)
    {
        var withGold = new List<BuildingDataSO>();
        var others = new List<BuildingDataSO>();

        foreach (var t in catalog.towers)
        {
            if (t == null || t.buildingType != BuildingType.ResourceTower) continue;
            if (!ResourceManager.Instance.CanAffordCosts(Faction.Enemy, t.buildCost.ToDictionary())) continue;

            bool producesGold = false;
            if (t is ResourceTowerDataSO rtData)
            {
                foreach (var output in rtData.outputs)
                {
                    if (output.type == ResourceType.Gold) { producesGold = true; break; }
                }
            }
            if (producesGold) withGold.Add(t); else others.Add(t);
        }

        if (preferGold && withGold.Count > 0) return withGold[Random.Range(0, withGold.Count)];
        if (others.Count > 0) return others[Random.Range(0, others.Count)];
        if (withGold.Count > 0) return withGold[Random.Range(0, withGold.Count)];
        return null;
    }

    // 选一个产出指定资源的买得起的资源塔（缺干粮时补干粮塔用）
    private BuildingDataSO PickResourceTower_Producing(ResourceType type)
    {
        var matches = new List<BuildingDataSO>();
        foreach (var t in catalog.towers)
        {
            if (t == null || t.buildingType != BuildingType.ResourceTower) continue;
            if (!ResourceManager.Instance.CanAffordCosts(Faction.Enemy, t.buildCost.ToDictionary())) continue;

            bool produces = false;
            if (t is ResourceTowerDataSO rtData)
            {
                foreach (var output in rtData.outputs)
                {
                    if (output.type == type) { produces = true; break; }
                }
            }
            if (produces) matches.Add(t);
        }
        if (matches.Count == 0) return null;
        return matches[Random.Range(0, matches.Count)];
    }

    // 从目录里选一个"指定类型 + 买得起"的塔
    private BuildingDataSO PickAffordable(BuildingType type)
    {
        var affordable = new List<BuildingDataSO>();
        foreach (var t in catalog.towers)
        {
            if (t == null || t.buildingType != type) continue;
            if (ResourceManager.Instance.CanAffordCosts(Faction.Enemy, t.buildCost.ToDictionary()))
            {
                affordable.Add(t);
            }
        }
        if (affordable.Count == 0) return null;
        return affordable[Random.Range(0, affordable.Count)];
    }

    // 找放置位置：右半场从"中线到右边界"分成三段，塔按类型分区摆放，不乱放
    //   防御塔 → 第 1 段（最前线，玩家兵最先到达，拦截）
    //   兵种塔 → 第 2 段（中场，基地前）
    //   资源塔 → 第 3 段（大后方，最安全，避免被玩家兵秒拆）
    private Vector3 FindSpot(BuildingDataSO data)
    {
        var gm = GridManager.Instance;
        if (gm == null) return Vector3.zero;

        int gridW = gm.gridWidth;
        int midX = gridW / 2;
        int rightHalf = gridW - midX;

        int segStart, segEnd;
        switch (data.buildingType)
        {
            case BuildingType.DefenseTower:
                segStart = midX;
                segEnd = midX + rightHalf / 3;
                break;
            case BuildingType.ResourceTower:
                segStart = midX + rightHalf * 2 / 3;
                segEnd = gridW;
                break;
            default: // 兵种塔
                segStart = midX + rightHalf / 3;
                segEnd = midX + rightHalf * 2 / 3;
                break;
        }

        int maxX = gridW - data.gridWidth;
        if (segStart > maxX) segStart = maxX;
        if (segEnd > maxX) segEnd = maxX;

        // 先在分区内找；分区放不下（被占满）就退回全右半场
        int tries = 30;
        for (int i = 0; i < tries; i++)
        {
            int x = Random.Range(segStart, segEnd - data.gridWidth + 1);
            int y = Random.Range(0, gm.gridHeight - data.gridHeight + 1);
            Vector2Int cell = new Vector2Int(x, y);

            if (gm.CanPlace(cell, data.gridWidth, data.gridHeight, Faction.Enemy))
            {
                return CenterOf(cell, data, gm);
            }
        }

        for (int i = 0; i < tries; i++)
        {
            int x = Random.Range(midX, gridW - data.gridWidth + 1);
            int y = Random.Range(0, gm.gridHeight - data.gridHeight + 1);
            Vector2Int cell = new Vector2Int(x, y);

            if (gm.CanPlace(cell, data.gridWidth, data.gridHeight, Faction.Enemy))
            {
                return CenterOf(cell, data, gm);
            }
        }
        return Vector3.zero;
    }

    // 格子 → 建筑中心世界坐标（多格建筑取几何中心）
    private Vector3 CenterOf(Vector2Int cell, BuildingDataSO data, GridManager gm)
    {
        Vector3 center = gm.CellToWorld(cell);
        center.x += (data.gridWidth - 1) * 0.5f * gm.cellSize;
        center.y += (data.gridHeight - 1) * 0.5f * gm.cellSize;
        return center;
    }

    // ===== 升级 =====

    private bool TryUpgradeRandom(ResourceUpgradeKind kind)
    {
        var candidates = new List<Building>();
        int maxLevel = kind == ResourceUpgradeKind.ResourceTower
            ? maxUpgradeResourceLevel
            : maxUpgradeBarracksLevel;

        foreach (var b in enemyBuildings)
        {
            if (b.isBase || b.data == null) continue;
            bool match = kind == ResourceUpgradeKind.ResourceTower
                ? b.data.buildingType == BuildingType.ResourceTower
                : b.data.buildingType == BuildingType.Barracks;
            if (match && b.level < maxLevel) candidates.Add(b);
        }
        if (candidates.Count == 0) return false;

        var target = candidates[Random.Range(0, candidates.Count)];
        return UpgradeBuilding(target);
    }

    private bool TryUpgradeHQ()
    {
        if (hq == null) return false;
        if (hq.level >= maxUpgradeHQLevel) return false;
        return UpgradeBuilding(hq);
    }

    private bool UpgradeBuilding(Building building)
    {
        int costGold = upgradeGoldPerLevel * (building.level + 1);
        int costOre = upgradeOrePerLevel * (building.level + 1);
        int costWood = upgradeWoodPerLevel * (building.level + 1);

        var costs = new Dictionary<ResourceType, int>
        {
            { ResourceType.Gold, costGold },
            { ResourceType.Ore, costOre },
            { ResourceType.Wood, costWood }
        };

        if (!ResourceManager.Instance.CanAffordCosts(Faction.Enemy, costs)) return false;

        ResourceManager.Instance.SpendCosts(Faction.Enemy, costs);
        building.level++;

        int newLevel = building.level;
        var res = building.GetComponent<ResourceTower>();
        if (res != null) res.SetLevel(newLevel);
        var bar = building.GetComponent<Barracks>();
        if (bar != null) bar.SetLevel(newLevel);
        var dt = building.GetComponent<DefenseTower>();
        if (dt != null) dt.SetLevel(newLevel);

        Debug.Log($"[EnemyAI] 升级 {(building.isBase ? "大本营" : building.data.displayName)} 到 {newLevel} 级");

        // 记录决策（写入对战日志，供分析优化）
        if (BattleLogRecorder.Instance != null)
            BattleLogRecorder.Instance.RecordDecision("upgrade", building.isBase ? "大本营" : building.data.displayName, newLevel);

        return true;
    }
}
