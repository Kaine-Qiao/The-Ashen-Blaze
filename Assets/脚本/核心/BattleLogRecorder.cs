using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ===== 对战记录数据结构 =====

// 一局的完整记录（写入 battle_log.jsonl，每行一局）
[Serializable]
public class BattleLogEntry
{
    public int version = 1;
    public string result;            // Victory / Defeat
    public float durationSeconds;    // 本局时长
    public float playerBaseHP;       // 结束时双方基地血量
    public float enemyBaseHP;
    public int playerUnits;          // 结束时双方场上小兵数
    public int enemyUnits;
    public int playerGold;           // 结束时双方资源
    public int enemyGold;
    public List<AIDecisionEntry> aiDecisions = new List<AIDecisionEntry>();   // AI 每步决策时间线
    public List<BuildingSnapshot> playerBuildings = new List<BuildingSnapshot>(); // 双方最终建筑清单
    public List<BuildingSnapshot> enemyBuildings = new List<BuildingSnapshot>();
}

// AI 的一步决策（建塔/升级）
[Serializable]
public class AIDecisionEntry
{
    public float t;          // 游戏时间（秒）
    public string action;    // build / upgrade
    public string type;      // 建筑类型名
    public int level;        // 升级到几级（build 时为 1）
}

// 一方的建筑统计（按建筑类型聚合）
[Serializable]
public class BuildingSnapshot
{
    public string type;      // 建筑类型名
    public int count;        // 数量
    public int maxLevel;     // 最高等级
}

// 玩家上一局打法摘要（写入 memory.json，AI 下局读取并调整策略）
[Serializable]
public class MemoryProfile
{
    public bool playerWon;          // 玩家上一局是否赢了
    public int playerBarracksCount; // 玩家兵种塔数量
    public int playerDefenseCount;  // 玩家防御塔数量
    public int playerResourceCount; // 玩家资源塔数量
    public int playerAvgLevel;      // 玩家建筑平均等级
}

// ===== 对战记录器 =====

// 挂到场景中的空物体上（和 EnemyAI 同场景）：
// 1. 每局游戏结束时，把本局数据追加写入 battle_log.jsonl（项目根目录/BattleLogs/）
// 2. 把玩家上一局打法摘要写入 memory.json，供 EnemyAI 下一局读取学习
public class BattleLogRecorder : MonoBehaviour
{
    public static BattleLogRecorder Instance { get; private set; }

    // 记录目录：项目根目录（Assets 的上一级）/BattleLogs
    private static string LogDir
    {
        get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BattleLogs"); }
    }
    private static string LogFile => Path.Combine(LogDir, "battle_log.jsonl");
    private static string MemoryFile => Path.Combine(LogDir, "memory.json");

    // 本局收集中的 AI 决策
    private readonly List<AIDecisionEntry> decisions = new List<AIDecisionEntry>();
    private float startTime;
    private bool logged;  // 本局是否已记录（防止重复）

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        startTime = Time.time;
        logged = false;
    }

    private void Start()
    {
        // 延迟订阅游戏结束事件（等 GameManager 初始化完成）
        Invoke(nameof(Subscribe), 0.2f);
    }

    private void Subscribe()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onStateChanged.AddListener(OnGameEnded);
    }

    // 供 EnemyAI 在每次建塔/升级时调用，记录决策时间线
    public void RecordDecision(string action, string typeName, int level)
    {
        decisions.Add(new AIDecisionEntry
        {
            t = Mathf.Round(Time.time - startTime),
            action = action,
            type = typeName,
            level = level
        });
    }

    // 游戏结束：保存本局日志 + 玩家打法摘要
    private void OnGameEnded(GameState state)
    {
        if (logged) return;
        logged = true;

        SaveBattleLog(state);
        SaveMemoryProfile(state);
    }

    // 追加写入一局记录
    private void SaveBattleLog(GameState state)
    {
        try
        {
            var entry = new BattleLogEntry();
            entry.result = state.ToString();
            entry.durationSeconds = Mathf.Round(Time.time - startTime);
            entry.aiDecisions = decisions;
            entry.playerBuildings = CollectBuildings(Faction.Player);
            entry.enemyBuildings = CollectBuildings(Faction.Enemy);
            entry.playerUnits = CountUnits(Faction.Player);
            entry.enemyUnits = CountUnits(Faction.Enemy);
            entry.playerBaseHP = GetBaseHP(Faction.Player);
            entry.enemyBaseHP = GetBaseHP(Faction.Enemy);
            entry.playerGold = ResourceManager.Instance.GetResource(Faction.Player, ResourceType.Gold);
            entry.enemyGold = ResourceManager.Instance.GetResource(Faction.Enemy, ResourceType.Gold);

            Directory.CreateDirectory(LogDir);
            File.AppendAllText(LogFile, JsonUtility.ToJson(entry) + Environment.NewLine);
            Debug.Log($"[对战记录] 本局已保存到 {LogFile}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[对战记录] 保存失败：" + e.Message);
        }
    }

    // 写入玩家打法摘要（AI 学习用）
    private void SaveMemoryProfile(GameState state)
    {
        try
        {
            var mem = new MemoryProfile();
            mem.playerWon = state == GameState.Victory;

            foreach (var snap in CollectBuildings(Faction.Player))
            {
                if (snap.type.Contains("兵种塔") || snap.type.Contains("兵")) mem.playerBarracksCount += snap.count;
                else if (snap.type.Contains("防御") || snap.type.Contains("箭塔") || snap.type.Contains("炮塔")) mem.playerDefenseCount += snap.count;
                else if (snap.type.Contains("资源") || snap.type.Contains("矿石") || snap.type.Contains("木材") || snap.type.Contains("干粮")) mem.playerResourceCount += snap.count;
            }

            // 玩家建筑平均等级（基地除外）
            int totalLevel = 0, count = 0;
            foreach (var b in FindObjectsOfType<Building>())
            {
                if (b.faction != Faction.Player || b.isBase) continue;
                totalLevel += b.level;
                count++;
            }
            mem.playerAvgLevel = count > 0 ? totalLevel / count : 1;

            Directory.CreateDirectory(LogDir);
            File.WriteAllText(MemoryFile, JsonUtility.ToJson(mem));
            Debug.Log($"[对战记录] 玩家打法已写入学习记忆 {MemoryFile}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[对战记录] 记忆保存失败：" + e.Message);
        }
    }

    // 读取玩家上一局打法摘要（EnemyAI 启动时调用）；文件不存在返回 null
    public static MemoryProfile LoadMemory()
    {
        try
        {
            if (!File.Exists(MemoryFile)) return null;
            return JsonUtility.FromJson<MemoryProfile>(File.ReadAllText(MemoryFile));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[对战记录] 读取记忆失败：" + e.Message);
            return null;
        }
    }

    // ===== 统计辅助 =====

    private List<BuildingSnapshot> CollectBuildings(Faction faction)
    {
        var dict = new Dictionary<string, BuildingSnapshot>();
        foreach (var b in FindObjectsOfType<Building>())
        {
            if (b.faction != faction || b.isBase) continue;
            string type = b.data != null ? b.data.displayName : "未知建筑";
            if (!dict.TryGetValue(type, out var snap))
            {
                snap = new BuildingSnapshot { type = type, count = 0, maxLevel = 1 };
                dict[type] = snap;
            }
            snap.count++;
            if (b.level > snap.maxLevel) snap.maxLevel = b.level;
        }
        return new List<BuildingSnapshot>(dict.Values);
    }

    private int CountUnits(Faction faction)
    {
        int n = 0;
        foreach (var u in FindObjectsOfType<Unit>())
        {
            if (u.faction == faction) n++;
        }
        return n;
    }

    private float GetBaseHP(Faction faction)
    {
        foreach (var b in FindObjectsOfType<Building>())
        {
            if (b.isBase && b.faction == faction) return Mathf.Round(b.currentHP);
        }
        return 0f;
    }
}
