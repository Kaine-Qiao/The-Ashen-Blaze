using UnityEngine;

// 文化塔（箭阵塔）行为：对攻击范围内的所有敌方小兵齐射箭雨（一次打多个）
// 每升一级：伤害 +20%、攻击间隔 -10%
public class CultureTower : MonoBehaviour
{
    private CultureTowerDataSO baseData;
    private Faction faction;
    private float attackTimer;

    // 运行时当前等级（从 Building.level 读取）
    public int currentLevel = 1;

    public void Initialize(CultureTowerDataSO towerData, Faction owner)
    {
        baseData = towerData;
        faction = owner;
        currentLevel = 1;
        attackTimer = GetAttackInterval();
    }

    private void Update()
    {
        if (baseData == null) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = GetAttackInterval();
            Fire();
        }
    }

    // ===== 运行时数值计算（按当前等级加成 + 科技加成） =====

    public float GetAttackDamage()
    {
        // 每级 +20%，再乘玩家科技攻击加成
        float dmg = baseData.attackDamage * (1f + 0.2f * (currentLevel - 1));
        if (faction == Faction.Player && TechManager.Instance != null)
            dmg *= 1f + TechManager.Instance.GetAttackBonus();
        return dmg;
    }

    public float GetAttackRange()
    {
        return baseData.attackRange;
    }

    public float GetAttackInterval()
    {
        // 每级攻速 +10% → 间隔缩短 10%，再乘科技出兵/攻速加成（只对玩家）
        float interval = baseData.attackInterval * Mathf.Pow(0.9f, currentLevel - 1);
        if (faction == Faction.Player && TechManager.Instance != null)
            interval *= 1f - TechManager.Instance.GetAttackSpeedBonus();
        return Mathf.Max(0.1f, interval);
    }

    public void SetLevel(int level)
    {
        currentLevel = level;
    }

    // ===== 箭雨攻击：范围内最多 maxTargets 个敌方目标 =====

    private void Fire()
    {
        float range = GetAttackRange();
        float damage = GetAttackDamage();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
        int count = 0;
        foreach (var hit in hits)
        {
            var unit = hit.GetComponent<Unit>();
            if (unit == null || unit.faction == faction) continue;
            if (count >= baseData.maxTargets) break;

            Vector2 hitDir = ((Vector2)(unit.transform.position - transform.position)).normalized;
            unit.TakeDamage(damage, hitDir);
            count++;
        }

        if (count > 0)
            Debug.Log($"[箭阵塔] {name} 箭雨命中 {count} 个目标（每目标 {damage:F0} 伤害）");
    }
}
