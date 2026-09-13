using UnityEngine;

// 防御塔行为：攻击进入攻击范围的敌方小兵（挂在有 Building 组件的塔上，放置时自动添加）
// 每升一级：伤害 +20%、攻速 +10%、范围 +5%
public class DefenseTower : MonoBehaviour
{
    private DefenseTowerDataSO baseData;    // 原始数据（ScriptableObject 里的初始值）
    private Faction faction;
    private float attackTimer;

    // 运行时当前等级（从 Building.level 读取，升级时由 Building 改 level）
    public int currentLevel = 1;

    // 由 Building 挂载时调用，传入塔数据和所属阵营
    public void Initialize(DefenseTowerDataSO towerData, Faction owner)
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
            Attack();
        }
    }

    // ===== 运行时数值计算（按当前等级加成） =====

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
        // 每级 +5%
        return baseData.attackRange * (1f + 0.05f * (currentLevel - 1));
    }

    public float GetAttackInterval()
    {
        // 每级攻速 +10% → 间隔缩短 10%
        return baseData.attackInterval * Mathf.Pow(0.9f, currentLevel - 1);
    }

    // 更新等级（由 Building 升级时调用）
    public void SetLevel(int level)
    {
        currentLevel = level;
    }

    // ===== 攻击逻辑 =====

    private void Attack()
    {
        float range = GetAttackRange();
        float damage = GetAttackDamage();

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
        Transform target = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var unit = hit.GetComponent<Unit>();
            if (unit == null || unit.faction == faction) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                target = hit.transform;
            }
        }

        if (target == null) return;

        Vector2 hitDir = ((Vector2)(target.position - transform.position)).normalized;
        var targetUnit = target.GetComponent<Unit>();
        targetUnit.TakeDamage(damage, hitDir);
    }
}
