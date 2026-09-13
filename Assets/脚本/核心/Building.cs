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

    [Header("3 路线升级状态（仅兵种塔/防御塔用）")]
    [Tooltip("主线路（-1=未选，0=A，1=B，2=C）")]
    public int mainPath = -1;
    [Tooltip("主线等级（0~5）")]
    public int mainPathLevel = 0;
    [Tooltip("分支路线（-1=未选，0=A，1=B，2=C）")]
    public int branchPath = -1;
    [Tooltip("分支等级（0~2）")]
    public int branchPathLevel = 0;
    [Tooltip("是否已合成 6 级（三主线都 5 级后合并）")]
    public bool isMerged6 = false;

    [Header("基地标记")]
    [Tooltip("是否基地（大本营）。基地被摧毁时触发胜负判定")]
    public bool isBase = false;

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

            // 自动缩放：让 Sprite 视觉大小 = 占格区域的世界大小（格子数 × cellSize）
            // 这样无论塔占几格，图片都能刚好填满对应区域，不同大小的塔共用 prefab 也没问题
            float targetSizeX = buildingData.gridWidth * GridManager.Instance.cellSize;
            float targetSizeY = buildingData.gridHeight * GridManager.Instance.cellSize;
            transform.localScale = new Vector3(targetSizeX, targetSizeY, 1f);

            // 给建筑加碰撞体（触发型），小兵才能"看见"并攻击它
            var col = GetComponent<CircleCollider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = Mathf.Max(buildingData.gridWidth, buildingData.gridHeight) * GridManager.Instance.cellSize * 0.5f;

            // 使用该塔数据资产里配的专属图片（没有配就保持 prefab 默认图）
            if (buildingData.icon != null)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = buildingData.icon;
            }

            // 根据塔类型自动挂载对应行为组件（资源塔→产出资源，兵种塔→出兵等）
            // 这样所有塔共用 prefab，不需要手动给每种塔挂不同脚本
            if (buildingData is ResourceTowerDataSO resourceData)
            {
                var rt = gameObject.GetComponent<ResourceTower>();
                if (rt == null) rt = gameObject.AddComponent<ResourceTower>();
                rt.Initialize(resourceData, owner);
            }
            else if (buildingData is BarracksDataSO barracksData)
            {
                var br = gameObject.GetComponent<Barracks>();
                if (br == null) br = gameObject.AddComponent<Barracks>();
                br.Initialize(barracksData, owner);
            }
            else if (buildingData is DefenseTowerDataSO defenseData)
            {
                var dt = gameObject.GetComponent<DefenseTower>();
                if (dt == null) dt = gameObject.AddComponent<DefenseTower>();
                dt.Initialize(defenseData, owner);
            }
            else if (buildingData is CultureTowerDataSO cultureData)
            {
                var ct = gameObject.GetComponent<CultureTower>();
                if (ct == null) ct = gameObject.AddComponent<CultureTower>();
                ct.Initialize(cultureData, owner);
            }
        }
    }

    // 受到伤害（由攻击系统调用）
    public void TakeDamage(float damage)
    {
        if (damage <= 0) return;
        currentHP -= damage;
        if (currentHP <= 0f) DestroyBuilding();
    }

    // ===== 3 路线升级加成 =====
    // 取该建筑配的 3 条路线（只有兵种塔有路线系统；其他类型返回 null）
    public UpgradePathSO[] GetPaths()
    {
        if (data == null) return null;
        if (data is BarracksDataSO b) return new UpgradePathSO[] { b.pathA, b.pathB, b.pathC };
        return null;
    }

    // 累计当前主线 + 分支的总加成（6 级时主线全按 5 级算并乘 mergeBoost）
    public PathLevelData GetTotalPathBonus()
    {
        var result = new PathLevelData();
        var paths = GetPaths();
        if (paths == null) return result;

        if (isMerged6)
        {
            // 6 级：三条主线都按 5 级加成，并乘 mergeBoost 强化
            for (int i = 0; i < 3; i++)
            {
                if (paths[i] == null || paths[i].levels.Length < 5) continue;
                AddLevel(paths[i].levels[4], ref result, paths[i].mergeBoost);
            }
            return result;
        }

        // 普通：主线按 mainPathLevel 加成（1~5 级对应 levels[0~4]）
        if (mainPath >= 0 && mainPath < 3 && paths[mainPath] != null)
        {
            int lv = Mathf.Clamp(mainPathLevel - 1, 0, 4);
            if (paths[mainPath].levels.Length > lv)
            {
                AddLevel(paths[mainPath].levels[lv], ref result, 1f);
            }
        }

        // 分支按 branchPathLevel 加成（分支 1~2 级对应 levels[0~1]，叠加生效）
        if (branchPath >= 0 && branchPath < 3 && paths[branchPath] != null)
        {
            int lv = Mathf.Clamp(branchPathLevel - 1, 0, 1);
            if (paths[branchPath].levels.Length > lv)
            {
                AddLevel(paths[branchPath].levels[lv], ref result, 1f);
            }
        }

        return result;
    }

    private static void AddLevel(PathLevelData level, ref PathLevelData acc, float boost)
    {
        acc.attackBonus += level.attackBonus * boost;
        acc.defenseBonus += level.defenseBonus * boost;
        acc.hpBonus += level.hpBonus * boost;
        acc.speedBonus += level.speedBonus * boost;
        acc.attackSpeedBonus += level.attackSpeedBonus * boost;
        acc.rangeBonus += level.rangeBonus * boost;
    }

    // 建筑被摧毁：释放网格占用并销毁物体
    // 如果是基地（大本营），同时触发胜负判定
    public void DestroyBuilding()
    {
        if (GridManager.Instance != null && data != null)
        {
            GridManager.Instance.Release(originCell, data.gridWidth, data.gridHeight);
        }

        // 基地被摧毁 → 游戏结束：玩家基地没了 = 失败，敌方基地没了 = 胜利
        if (isBase && GameManager.Instance != null)
        {
            GameState result = faction == Faction.Player ? GameState.Defeat : GameState.Victory;
            GameManager.Instance.EndGame(result);
            Debug.Log($"[基地] {name} 被摧毁！游戏结束：{result}");
        }

        Destroy(gameObject);
    }

#if UNITY_EDITOR
    // 编辑器辅助：在 Scene 视图画出建筑占用的格子范围（仅编辑器显示）
    private void OnDrawGizmos()
    {
        if (data == null || GridManager.Instance == null) return;

        // 用实际世界尺寸（格子数 × cellSize），而不是直接用格子数
        Vector3 size = new Vector3(
            data.gridWidth * GridManager.Instance.cellSize,
            data.gridHeight * GridManager.Instance.cellSize,
            0f);
        Gizmos.color = faction == Faction.Player
            ? new Color(0f, 0.8f, 1f, 0.3f)
            : new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawWireCube(transform.position, size);
    }
#endif
}
