using System.Collections.Generic;
using UnityEngine;

// 科技管理器（挂在场景中的空物体上）：
// 管理玩家已研究的科技，提供全局加成查询（只对玩家阵营生效）
// 所有科技资产要拖进 allTechs 列表里；科技在大本营升级面板中研究
public class TechManager : MonoBehaviour
{
    [Header("科技列表")]
    [Tooltip("所有科技资产（按顺序拖进来，大本营研究面板会按此顺序显示）")]
    public List<TechDataSO> allTechs = new List<TechDataSO>();

    // 单例
    public static TechManager Instance { get; private set; }

    // 已研究的科技集合
    private readonly HashSet<TechDataSO> researched = new HashSet<TechDataSO>();

    // 加成累计值
    private float attackBonus = 0f;
    private float defenseBonus = 0f;
    private float resourceBonus = 0f;
    private float attackSpeedBonus = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ===== 查询 =====

    public bool IsResearched(TechDataSO tech)
    {
        return tech != null && researched.Contains(tech);
    }

    // 是否满足研究条件：前置科技都已研究（资源够不够在这里不查，由 UI 层查）
    public bool CanResearch(TechDataSO tech)
    {
        if (tech == null || IsResearched(tech)) return false;
        if (tech.prerequisites != null)
        {
            foreach (var pre in tech.prerequisites)
            {
                if (!IsResearched(pre)) return false;
            }
        }
        return true;
    }

    // 某建筑是否已解锁（科技解锁类型；没有科技列表时默认全部解锁）
    public bool IsBuildingUnlocked(BuildingDataSO building)
    {
        if (building == null) return true;
        // 检查是否存在"解锁该建筑"的科技未研究
        foreach (var tech in allTechs)
        {
            if (tech == null || tech.effectType != TechEffectType.UnlockBuilding) continue;
            if (tech.unlockBuilding == building && !IsResearched(tech)) return false;
        }
        return true;
    }

    // ===== 全局加成（只对玩家） =====

    public float GetAttackBonus() { return attackBonus; }
    public float GetDefenseBonus() { return defenseBonus; }
    public float GetResourceBonus() { return resourceBonus; }
    public float GetAttackSpeedBonus() { return attackSpeedBonus; }

    // ===== 研究 =====

    // 尝试研究一个科技：前置满足 + 资源够 → 扣资源、记录、累计加成
    public bool Research(TechDataSO tech)
    {
        if (tech == null || !CanResearch(tech)) return false;
        if (ResourceManager.Instance == null) return false;

        var costs = tech.cost.ToDictionary();
        if (!ResourceManager.Instance.CanAffordCosts(Faction.Player, costs)) return false;

        ResourceManager.Instance.SpendCosts(Faction.Player, costs);
        researched.Add(tech);

        switch (tech.effectType)
        {
            case TechEffectType.AttackBoost: attackBonus += tech.effectValue; break;
            case TechEffectType.DefenseBoost: defenseBonus += tech.effectValue; break;
            case TechEffectType.ResourceBoost: resourceBonus += tech.effectValue; break;
            case TechEffectType.AttackSpeedBoost: attackSpeedBonus += tech.effectValue; break;
            case TechEffectType.UnlockBuilding:
                string unlockedName = tech.unlockBuilding != null ? tech.unlockBuilding.displayName : "(未指定)";
                Debug.Log($"[科技] 解锁建筑：{unlockedName}");
                break;
        }

        Debug.Log($"[科技] 已研究：{tech.techName}");
        return true;
    }
}
