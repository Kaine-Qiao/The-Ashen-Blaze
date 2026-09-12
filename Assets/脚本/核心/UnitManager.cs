using UnityEngine;

// 单位管理器：生成小兵的统一入口（兵种塔、以后的敌方 AI 都走这里）
public class UnitManager : MonoBehaviour
{
    [Header("小兵预制体")]
    [Tooltip("小兵预制体（上面挂着 SpriteRenderer + Unit 脚本），所有兵种共用一个")]
    public GameObject unitPrefab;

    [Header("全局缩放（视觉 + 数值）")]
    [Tooltip("小兵视觉大小倍率（含碰撞体、脚下圈、红罩）")]
    public float unitScale = 0.5f;
    [Tooltip("小兵数值倍率（攻击范围/索敌/移速/击退一起缩放）")]
    public float statsScale = 0.5f;

    // 单例
    public static UnitManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 生成一个小兵并初始化，返回生成的 Unit；失败返回 null
    public Unit SpawnUnit(UnitDataSO data, Faction faction, Vector3 position)
    {
        if (data == null)
        {
            Debug.LogWarning("[UnitManager] 没有小兵数据");
            return null;
        }
        if (unitPrefab == null)
        {
            Debug.LogError("[UnitManager] 未指定 unitPrefab，无法生成小兵");
            return null;
        }

        GameObject go = Instantiate(unitPrefab, position, Quaternion.identity);
        if (go.GetComponent<SpriteRenderer>() == null) go.AddComponent<SpriteRenderer>();

        // 视觉缩放（子物体：圈、红罩会自动跟着缩）
        go.transform.localScale = Vector3.one * unitScale;

        // 给小兵加刚体（重力0、锁定旋转），配合非 trigger 碰撞体 → 小兵之间会物理推开不重叠
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 碰撞体（非 trigger，物理阻挡），半径跟着视觉缩放
        var col = go.GetComponent<CircleCollider2D>();
        if (col == null) col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = false;
        col.radius = 0.4f * unitScale;

        Unit unit = go.GetComponent<Unit>();
        if (unit == null) unit = go.AddComponent<Unit>();

        // 玩家兵往右（敌方半场），敌方兵往左（玩家半场）
        int dir = faction == Faction.Player ? 1 : -1;
        unit.Initialize(data, faction, dir, statsScale);
        return unit;
    }
}
