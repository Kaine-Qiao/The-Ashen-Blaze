using UnityEngine;

// 小兵组件：挂在每个小兵物体上（由兵种塔生成）
// 行为：索敌范围内发现敌方目标→追向它，进入攻击范围→停下攻击；没有目标→朝敌方方向走
// 视觉：脚下阵营圈（玩家蓝/敌方红），受击时盖一层红色光罩
public class Unit : MonoBehaviour
{
    [Header("数据")]
    [Tooltip("兵种数据（哪个兵种的属性）")]
    public UnitDataSO data;

    [Tooltip("所属阵营")]
    public Faction faction;

    [Header("索敌")]
    [Tooltip("发现敌人的范围（比攻击范围大，让兵提前转向）")]
    public float searchRange = 4f;

    [Header("受击反馈")]
    [Tooltip("受击后向后退的距离")]
    public float knockbackDistance = 0.15f;

    [Header("当前状态")]
    [Tooltip("当前生命")]
    public float currentHP;

    // 移动方向：+1 = 向右（玩家兵往敌方右半场走），-1 = 向左（敌方兵往玩家左半场走）
    [HideInInspector] public int moveDir = 1;

    // 实际生效的移动速度 / 攻击范围（= 数据值 × statsScale，由 UnitManager 传入）
    private float moveSpeed;
    private float attackRange;

    // 当前攻击目标（没有目标则为 null，继续移动）
    private Transform target;
    private float attackTimer;

    // 受击红罩计时器（>0 时显示红色光罩）
    private float flashTimer;

    // 组件缓存
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer flashRenderer;  // 受击红色光罩

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // 由生成方（兵种塔）调用，初始化数据、阵营、方向
    // statsScale：数值倍率（攻击范围/索敌/移速/击退一起缩放，与视觉缩小平match）
    public void Initialize(UnitDataSO unitData, Faction owner, int direction, float statsScale = 1f)
    {
        data = unitData;
        faction = owner;
        moveDir = direction;
        currentHP = unitData != null ? unitData.maxHP : 100f;
        name = unitData != null ? unitData.displayName : "Unit";

        // 应用数值倍率（索敌范围 searchRange 不缩放，保持 Inspector 里的原值）
        moveSpeed = (unitData != null ? unitData.moveSpeed : 3f) * statsScale;
        attackRange = (unitData != null ? unitData.attackRange : 1.5f) * statsScale;
        knockbackDistance *= statsScale;

        // 用该兵种配的专属图片（没有配就保持 prefab 默认图）
        if (unitData != null && unitData.icon != null)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.sprite = unitData.icon;
        }

        CreateVisuals();
    }

    // 创建脚下的阵营圈（前后两半，做出 3D 遮挡效果）+ 受击红色光罩
    private void CreateVisuals()
    {
        // 统一渲染层级：本体 1，脚后半圈 0（被身体盖住），脚前圈 2（露出来），红罩 3
        if (spriteRenderer != null) spriteRenderer.sortingOrder = 1;

        Color ringColor = faction == Faction.Player
            ? new Color(0.15f, 0.5f, 1f, 0.85f)   // 玩家：蓝
            : new Color(1f, 0.2f, 0.2f, 0.85f);   // 敌方：红

        // --- 阵营圈后半（椭圆的上半，在小兵脚后）→ 排序低于本体，被脚挡住 ---
        var backGO = new GameObject("RingBack");
        backGO.transform.SetParent(transform, false);
        var backR = backGO.AddComponent<SpriteRenderer>();
        backR.sprite = CreateHalfEllipseSprite(topHalf: true);
        backR.color = ringColor;
        backR.sortingOrder = 0;
        backGO.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        backGO.transform.localScale = new Vector3(1.5f, 0.5f, 1f); // 压扁成椭圆

        // --- 阵营圈前半（椭圆的下半，在小兵脚前）→ 排序高于本体，正常显示 ---
        var frontGO = new GameObject("RingFront");
        frontGO.transform.SetParent(transform, false);
        var frontR = frontGO.AddComponent<SpriteRenderer>();
        frontR.sprite = CreateHalfEllipseSprite(topHalf: false);
        frontR.color = ringColor;
        frontR.sortingOrder = 2;
        frontGO.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        frontGO.transform.localScale = new Vector3(1.5f, 0.5f, 1f);

        // --- 受击红色光罩：直接用和小兵相同的图片，形状大小自动匹配 ---
        var flashGO = new GameObject("HitFlash");
        flashGO.transform.SetParent(transform, false);
        flashRenderer = flashGO.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = (data != null && data.icon != null)
            ? data.icon
            : (spriteRenderer != null ? spriteRenderer.sprite : null);
        flashRenderer.color = new Color(1f, 0.15f, 0.15f, 0.45f);
        flashRenderer.sortingOrder = 3;
        flashGO.transform.localScale = Vector3.one;
        flashRenderer.enabled = false;
    }

    // 生成半椭圆 Sprite：topHalf=true 返回椭圆上半，false 返回下半（32x32，世界尺寸 1x1）
    // 两个半圆拼起来是完整圆，通过 localScale 压扁变成椭圆
    private static Sprite CreateHalfEllipseSprite(bool topHalf)
    {
        int size = 32;
        var tex = new Texture2D(size, size);
        float r = size / 2f - 1f;
        Vector2 center = new Vector2(size / 2f, size / 2f);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                // 只保留一半（上半或下半），另一半透明
                bool inHalf = topHalf ? y >= size / 2f : y < size / 2f;
                if (!inHalf)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }
                float dist = Vector2.Distance(center, new Vector2(x, y));
                tex.SetPixel(x, y, dist <= r ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void Update()
    {
        if (data == null) return;

        // 受击红罩计时：时间到就隐藏
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && flashRenderer != null) flashRenderer.enabled = false;
        }

        // 1. 索敌（用 searchRange，比攻击范围大）
        target = FindTarget(searchRange);

        if (target != null)
        {
            float dist = Vector2.Distance(transform.position, target.position);

            if (dist > attackRange)
            {
                // 2a. 目标在攻击范围外：追向目标
                MoveToward(target.position);
            }
            else
            {
                // 2b. 目标在攻击范围内：停下攻击
                if (rb != null) rb.velocity = Vector2.zero;

                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    attackTimer = 1f / Mathf.Max(0.01f, data.attackSpeed);
                    AttackTarget(target);
                }
            }
        }
        else
        {
            // 3. 没目标：朝敌方方向移动
            if (rb != null)
            {
                rb.velocity = new Vector2(moveDir * moveSpeed, 0f);
            }
            else
            {
                transform.position += new Vector3(moveDir * moveSpeed * Time.deltaTime, 0f, 0f);
            }
        }
    }

    // 朝某个位置移动
    private void MoveToward(Vector2 pos)
    {
        Vector2 dir = ((Vector2)pos - (Vector2)transform.position).normalized;
        if (rb != null)
        {
            rb.velocity = dir * moveSpeed;
        }
        else
        {
            transform.position += (Vector3)dir * moveSpeed * Time.deltaTime;
        }
    }

    // 在指定范围内找最近的敌方目标（小兵或建筑）
    private Transform FindTarget(float range)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range);
        float closestDist = float.MaxValue;
        Transform result = null;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            // 判断目标属于哪一方（小兵或建筑）
            Faction? targetFaction = GetTargetFaction(hit);
            if (targetFaction == null || targetFaction.Value == faction) continue; // 同阵营跳过

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                result = hit.transform;
            }
        }
        return result;
    }

    // 攻击目标：小兵按防御减免伤害并带受击反馈，建筑直接扣攻击
    private void AttackTarget(Transform target)
    {
        var unit = target.GetComponent<Unit>();
        if (unit != null)
        {
            int dmg = Mathf.Max(1, (int)(data.attack - unit.data.defense));
            // 受击方向：从攻击者指向目标
            Vector2 hitDir = ((Vector2)(unit.transform.position - transform.position)).normalized;
            unit.TakeDamage(dmg, hitDir);
            return;
        }

        var building = target.GetComponent<Building>();
        if (building != null)
        {
            building.TakeDamage(data.attack);
        }
    }

    // 取一个碰撞体上的目标阵营（小兵或建筑），都不是则返回 null
    private Faction? GetTargetFaction(Collider2D hit)
    {
        var u = hit.GetComponent<Unit>();
        if (u != null) return u.faction;

        var b = hit.GetComponent<Building>();
        if (b != null) return b.faction;

        return null;
    }

    // 受到伤害（由攻击系统调用）
    // hitDir：攻击方向（从攻击者指向自己），用于击退
    public void TakeDamage(float damage, Vector2 hitDir)
    {
        if (damage <= 0) return;
        currentHP -= damage;

        // 受击反馈：显示红色光罩 + 按受击方向后退一小步
        flashTimer = 0.12f;
        if (flashRenderer != null) flashRenderer.enabled = true;
        transform.position += (Vector3)hitDir.normalized * knockbackDistance;

        if (currentHP <= 0f)
        {
            // TODO: 以后在这里加死亡经验分配
            Destroy(gameObject);
        }
    }
}
