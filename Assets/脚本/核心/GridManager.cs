using UnityEngine;

// 网格管理器（挂在场景中的空物体上）：
// 负责地图的隐藏网格，供建筑放置使用。
// 1. 世界坐标 <-> 网格坐标换算
// 2. 格子占用检测（建筑占 W×H 格）
// 3. 阵营半场判定（X=0 为中线，玩家在左半场，敌方在右半场）
public class GridManager : MonoBehaviour
{
    [Header("网格参数")]
    [Tooltip("每格的世界尺寸（单位）")]
    public float cellSize = 1f;
    [Tooltip("网格宽度（多少格）")]
    public int gridWidth = 120;
    [Tooltip("网格高度（多少格）")]
    public int gridHeight = 60;
    [Tooltip("网格中心点的世界坐标（地图中心，一般保持 0,0）")]
    public Vector2 gridCenter = Vector2.zero;

    [Header("调试显示")]
    [Tooltip("在 Scene 视图里显示网格线（仅编辑器辅助，不影响游戏）")]
    public bool showGridGizmos = true;

    // 单例
    public static GridManager Instance { get; private set; }

    // 占用表：true 表示该格已被建筑占用
    private bool[,] occupied;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitGrid();
    }

    private void InitGrid()
    {
        occupied = new bool[gridWidth, gridHeight];
    }

    // ---------- 坐标换算 ----------

    // 世界坐标 -> 网格坐标（返回格子的 X/Y 索引）
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - gridCenter.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.y - gridCenter.y) / cellSize);
        return new Vector2Int(x, y);
    }

    // 网格坐标 -> 世界坐标（格子的中心点）
    public Vector3 CellToWorld(Vector2Int cell)
    {
        float x = gridCenter.x + (cell.x + 0.5f) * cellSize;
        float y = gridCenter.y + (cell.y + 0.5f) * cellSize;
        return new Vector3(x, y, 0f);
    }

    // 网格坐标是否在网格范围内
    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridWidth &&
               cell.y >= 0 && cell.y < gridHeight;
    }

    // ---------- 占用检测 ----------

    // 单个格子是否被占用
    public bool IsOccupied(Vector2Int cell)
    {
        if (!IsInBounds(cell)) return true; // 出界视为占用，防止放出去
        return occupied[cell.x, cell.y];
    }

    // 判断一个 W×H 的建筑能否放在这里（originCell 是建筑左下角格子）
    // 条件：全部格子在界内、未占用、且位于正确阵营的半场
    public bool CanPlace(Vector2Int originCell, int width, int height, Faction faction)
    {
        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                Vector2Int c = new Vector2Int(originCell.x + dx, originCell.y + dy);
                if (!IsInBounds(c)) return false;
                if (IsOccupied(c)) return false;
                if (!IsOnFactionSide(c, faction)) return false;
            }
        }
        return true;
    }

    // 占用一个 W×H 区域（建筑放置成功后调用）
    public void Occupy(Vector2Int originCell, int width, int height)
    {
        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                Vector2Int c = new Vector2Int(originCell.x + dx, originCell.y + dy);
                if (IsInBounds(c)) occupied[c.x, c.y] = true;
            }
        }
    }

    // 释放一个 W×H 区域（建筑被摧毁后调用）
    public void Release(Vector2Int originCell, int width, int height)
    {
        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                Vector2Int c = new Vector2Int(originCell.x + dx, originCell.y + dy);
                if (IsInBounds(c)) occupied[c.x, c.y] = false;
            }
        }
    }

    // ---------- 阵营半场判定 ----------

    // 判断某格属于哪一方半场（中线固定为世界坐标 X=0）
    // 玩家：格子中心 X < 0 → 左半场
    // 敌方：格子中心 X >= 0 → 右半场
    // 这样不受 gridCenter 参数影响，地图中线始终是世界坐标原点
    public bool IsOnFactionSide(Vector2Int cell, Faction faction)
    {
        Vector3 center = CellToWorld(cell);
        if (faction == Faction.Player)
        {
            return center.x < 0f;
        }
        else
        {
            return center.x >= 0f;
        }
    }

    // ---------- 编辑器辅助 ----------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGridGizmos) return;

        // 画整个网格的边框
        Vector3 origin = new Vector3(gridCenter.x, gridCenter.y, 0f);
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawWireCube(
            origin + new Vector3(gridWidth * cellSize * 0.5f, gridHeight * cellSize * 0.5f, 0f),
            new Vector3(gridWidth * cellSize, gridHeight * cellSize, 0f));

        // 画中线（阵营分界）
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Vector3 midStart = origin + new Vector3(0f, 0f, 0f);
        Vector3 midEnd = origin + new Vector3(0f, gridHeight * cellSize, 0f);
        Gizmos.DrawLine(midStart, midEnd);
    }
#endif
}
