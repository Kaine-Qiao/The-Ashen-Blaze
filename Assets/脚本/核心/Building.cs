using UnityEngine;

// 建筑对象组件：挂在每一个建筑物体上（塔、基地等）
// 保存该建筑的数据、阵营、血量、占用的格子，并处理受伤/摧毁
public class Building : MonoBehaviour
{
    [Header("数据")]
    [Tooltip("该建筑的配置数据（ScriptableObject）")]
    public BuildingDataSO data;

    [Tooltip("所属阵营")]
    public Faction faction = Faction.Player;

    [Header("当前状态")]
    [Tooltip("当前血量")]
    public float currentHP = 100f;

    [Tooltip("当前等级（升级系统用，初始 1）")]
    public int level = 1;

    // 占用的左下角格子（放置时由 BuildingManager 写入）
    [HideInInspector] public Vector2Int originCell;

    // 建筑数据初始化：设置数据、阵营、血量，并放到占格区域的正中心
    public void Initialize(BuildingDataSO buildingData, Faction owner, Vector2Int cell)
    {
        data = buildingData;
        faction = owner;
        originCell = cell;
        currentHP = buildingData != null ? buildingData.maxHP : 100f;
        name = buildingData != null ? buildingData.displayName : "Building";

        if (buildingData != null && GridManager.Instance != null)
        {
            // 占 W×H 格时，物体中心放在整个区域的中心
            Vector3 center = GridManager.Instance.CellToWorld(cell);
            center.x += (buildingData.gridWidth - 1) * 0.5f * GridManager.Instance.cellSize;
            center.y += (buildingData.gridHeight - 1) * 0.5f * GridManager.Instance.cellSize;
            transform.position = center;
        }
    }

    // 受到伤害（由攻击系统调用）
    public void TakeDamage(float damage)
    {
        if (damage <= 0) return;
        currentHP -= damage;
        if (currentHP <= 0f) DestroyBuilding();
    }

    // 建筑被摧毁：释放网格占用并销毁物体
    public void DestroyBuilding()
    {
        if (GridManager.Instance != null && data != null)
        {
            GridManager.Instance.Release(originCell, data.gridWidth, data.gridHeight);
        }
        // TODO: 以后在这里接上基地被摧毁 = 游戏失败的判定
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    // 编辑器辅助：在 Scene 视图画出建筑占用的格子范围（仅编辑器显示）
    private void OnDrawGizmos()
    {
        if (data == null || GridManager.Instance == null) return;

        Vector3 size = new Vector3(data.gridWidth, data.gridHeight, 0f);
        Gizmos.color = faction == Faction.Player
            ? new Color(0f, 0.8f, 1f, 0.5f)
            : new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawWireCube(transform.position, size);
    }
#endif
}
