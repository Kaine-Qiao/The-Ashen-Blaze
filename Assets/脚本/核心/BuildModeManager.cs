using UnityEngine;
using UnityEngine.EventSystems;

// 建造模式管理器（挂在场景中的空物体上）：
// 1. 用数字键选塔（1=目录里第1种塔，2=第2种...）
// 2. 按 B 进入/退出建造模式
// 3. 鼠标跟随预览（绿色=可放，红色=不可放）—— 同时在 Scene 和 Game 视图显示
// 4. 左键放置，右键/ESC 取消
public class BuildModeManager : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("塔目录（把所有塔数据按顺序拖进去）")]
    public BuildingCatalogSO catalog;

    [Header("控制")]
    [Tooltip("玩家建造时用哪个阵型")]
    public Faction playerFaction = Faction.Player;

    [Header("预览")]
    [Tooltip("预览框颜色（可放）")]
    public Color validColor = new Color(0f, 1f, 0f, 0.5f);
    [Tooltip("预览框颜色（不可放）")]
    public Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

    // 当前选中的塔数据（null 表示不在建造模式）
    public BuildingDataSO currentSelectedTower;

    // 运行时预览物体（用 SpriteRenderer，Game 视图也能看见）
    private SpriteRenderer previewRenderer;
    private Transform previewTransform;

    // 当前鼠标悬停的格子（调试用）
    private Vector2Int hoverCell;
    private bool isHoveringValid;

    // UI 用：通过目录索引选中要建造的塔（和数字键 1/2/3... 一样的效果）
    public void SelectBuilding(int index)
    {
        if (catalog == null || index < 0 || index >= catalog.towers.Count) return;
        currentSelectedTower = catalog.towers[index];
        Debug.Log($"[建造] 选中: {currentSelectedTower.displayName}");
    }

    // UI 用：取消建造（和 ESC 一样）
    public void CancelBuild()
    {
        currentSelectedTower = null;
        Debug.Log("[建造] 取消选中");
    }

    private void Awake()
    {
        // 创建一个运行时预览物体——一个白色半透明方块
        var previewGO = new GameObject("BuildPreview");
        previewTransform = previewGO.transform;
        previewTransform.SetParent(transform);

        previewRenderer = previewGO.AddComponent<SpriteRenderer>();
        // Unity 自带的白色 Sprite
        // pixelsPerUnit = 纹理宽度 → 保证 sprite 原始世界尺寸 = 1×1 单位
        // 后面用 localScale 精确控制预览框大小 = 格子数 × cellSize
        int texW = Texture2D.whiteTexture.width;
        int texH = Texture2D.whiteTexture.height;
        previewRenderer.sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0, 0, texW, texH),
            new Vector2(0.5f, 0.5f),
            (float)texW); // 让 sprite 世界尺寸 = 1×1 单位
        previewRenderer.sortingOrder = 100; // 让它在塔上面
        previewRenderer.enabled = false;
    }

    private void Update()
    {
        // ==== 快捷键选塔 ====
        if (catalog != null)
        {
            for (int i = 0; i < catalog.towers.Count && i < 9; i++)
            {
                KeyCode key = KeyCode.Alpha1 + i;
                if (Input.GetKeyDown(key))
                {
                    currentSelectedTower = catalog.towers[i];
                    Debug.Log($"[建造] 选中: {currentSelectedTower.displayName}");
                }
            }
        }

        // ==== B 键切换建造模式 ====
        if (Input.GetKeyDown(KeyCode.B))
        {
            currentSelectedTower = null;
            Debug.Log("[建造] 退出建造模式");
        }

        // ==== F 键切换建造阵营（测试用：方便建敌方塔来测试战斗） ====
        if (Input.GetKeyDown(KeyCode.F))
        {
            playerFaction = playerFaction == Faction.Player ? Faction.Enemy : Faction.Player;
            currentSelectedTower = null;
            Debug.Log($"[建造] 切换建造阵营为: {(playerFaction == Faction.Player ? "玩家(左)" : "敌方(右)")}");
        }

        // ==== 不在建造模式：隐藏预览并返回 ====
        if (currentSelectedTower == null)
        {
            if (previewRenderer != null) previewRenderer.enabled = false;
            return;
        }

        // ==== 获取鼠标世界坐标 ====
        Vector3 worldPos = GetMouseWorldPosition();
        hoverCell = GridManager.Instance.WorldToCell(worldPos);

        // ==== 逐步验证并打印失败原因（调试用） ====
        bool inBounds = true;
        bool occupied = false;
        bool onCorrectSide = true;
        int failingCell = -1;

        for (int dx = 0; dx < currentSelectedTower.gridWidth; dx++)
        {
            for (int dy = 0; dy < currentSelectedTower.gridHeight; dy++)
            {
                Vector2Int c = new Vector2Int(hoverCell.x + dx, hoverCell.y + dy);
                if (!GridManager.Instance.IsInBounds(c)) { inBounds = false; failingCell = dx + dy * 10; break; }
                if (GridManager.Instance.IsOccupied(c)) { occupied = true; failingCell = dx + dy * 10; break; }
                if (!GridManager.Instance.IsOnFactionSide(c, playerFaction)) { onCorrectSide = false; failingCell = dx + dy * 10; break; }
            }
            if (!inBounds || occupied || !onCorrectSide) break;
        }

        isHoveringValid = inBounds && !occupied && onCorrectSide;

        // ==== 画预览框 ====
        UpdatePreview(worldPos, isHoveringValid);

        // ==== 左键放置 ====
        if (Input.GetMouseButtonDown(0))
        {
            // 鼠标点在 UI 元素上（按钮/面板/文字）时忽略，避免点 UI 时误触场景建造
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Building result = BuildingManager.Instance.TryPlaceBuilding(
                currentSelectedTower, worldPos, playerFaction);

            if (result != null)
            {
                Debug.Log($"[建造] ✓ 放置成功: {currentSelectedTower.displayName}");
            }
            else
            {
                // 打印详细原因，方便调试
                string reason = !inBounds ? "出界" : occupied ? "位置已占用" : !onCorrectSide ? "不在玩家半场" : "未知";
                Debug.LogWarning($"[建造] ✗ 放置失败: {reason}  格子={hoverCell}  世界坐标={worldPos}");
            }
        }

        // ==== 右键 / ESC 取消 ====
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            currentSelectedTower = null;
            Debug.Log("[建造] 取消选中");
        }
    }

    // 把鼠标屏幕坐标转换成 2D 世界坐标
    private Vector3 GetMouseWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[BuildModeManager] 找不到 Main Camera！请给相机 tag 设为 MainCamera");
            return Vector3.zero;
        }

        Vector3 screen = Input.mousePosition;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        world.z = 0f;
        return world;
    }

    // 更新运行时预览物体的位置/大小/颜色
    private void UpdatePreview(Vector3 worldPos, bool valid)
    {
        if (previewRenderer == null || currentSelectedTower == null) return;

        previewRenderer.enabled = true;

        // 大小 = 塔占的世界尺寸
        Vector3 size = new Vector3(
            currentSelectedTower.gridWidth * GridManager.Instance.cellSize,
            currentSelectedTower.gridHeight * GridManager.Instance.cellSize,
            1f);

        previewTransform.localScale = new Vector3(size.x, size.y, 1f);

        // 位置 = 占格区域中心
        Vector3 center = GridManager.Instance.CellToWorld(hoverCell);
        center.x += (currentSelectedTower.gridWidth - 1) * 0.5f * GridManager.Instance.cellSize;
        center.y += (currentSelectedTower.gridHeight - 1) * 0.5f * GridManager.Instance.cellSize;
        previewTransform.position = new Vector3(center.x, center.y, 0f);

        // 颜色
        previewRenderer.color = valid ? validColor : invalidColor;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (currentSelectedTower == null || GridManager.Instance == null) return;

        Vector3 size = new Vector3(
            currentSelectedTower.gridWidth * GridManager.Instance.cellSize,
            currentSelectedTower.gridHeight * GridManager.Instance.cellSize,
            0f);

        Vector3 center = GridManager.Instance.CellToWorld(hoverCell);
        center.x += (currentSelectedTower.gridWidth - 1) * 0.5f * GridManager.Instance.cellSize;
        center.y += (currentSelectedTower.gridHeight - 1) * 0.5f * GridManager.Instance.cellSize;

        Gizmos.color = isHoveringValid ? new Color(0f, 1f, 0f, 0.6f)
                                        : new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
