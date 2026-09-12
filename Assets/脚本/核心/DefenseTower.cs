using UnityEngine;

// 防御塔行为：攻击进入攻击范围的敌方小兵（挂在有 Building 组件的塔上，放置时自动添加）
// 按 attackInterval 秒攻击一次攻击范围内最近的敌方小兵
public class DefenseTower : MonoBehaviour
{
    private DefenseTowerDataSO data;
    private Faction faction;
    private float attackTimer;

    // 由 Building 挂载时调用，传入塔数据和所属阵营
    public void Initialize(DefenseTowerDataSO towerData, Faction owner)
    {
        data = towerData;
        faction = owner;
        attackTimer = towerData.attackInterval;
    }

    private void Update()
    {
        if (data == null) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = data.attackInterval;
            Attack();
        }
    }

    // 在攻击范围内找最近的敌方小兵并攻击
    private void Attack()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, data.attackRange);
        Transform target = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var unit = hit.GetComponent<Unit>();
            if (unit == null || unit.faction == faction) continue; // 只打敌方小兵

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                target = hit.transform;
            }
        }

        if (target == null) return;

        // 攻击：受击方向从塔指向小兵
        Vector2 hitDir = ((Vector2)(target.position - transform.position)).normalized;
        var targetUnit = target.GetComponent<Unit>();
        targetUnit.TakeDamage(data.attackDamage, hitDir);
        Debug.Log($"[防御塔] {name} 攻击 {target.name}，造成 {data.attackDamage} 伤害");
    }
}
