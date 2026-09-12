using UnityEngine;

// 基地（大本营）管理器：开局自动创建双方基地
// 玩家基地在左半场最深处，敌方基地在右半场最深处
// 基地被摧毁时由 Building 触发胜负判定
// 以后大本营兼任科技塔（升级/科技研究功能后面实现）
public class BaseManager : MonoBehaviour
{
    [Header("基地配置")]
    [Tooltip("基地血量")]
    public float baseMaxHP = 500f;

    [Tooltip("基地占格宽")]
    public int baseWidth = 2;

    [Tooltip("基地占格高")]
    public int baseHeight = 2;

    [Tooltip("玩家基地图片（不填就用蓝色方块占位）")]
    public Sprite playerBaseIcon;

    [Tooltip("敌方基地图片（不填就用红色方块占位）")]
    public Sprite enemyBaseIcon;

    private void Start()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("[BaseManager] 场景中缺少 GridManager，无法生成基地");
            return;
        }

        CreateBase(Faction.Player, playerBaseIcon);
        CreateBase(Faction.Enemy, enemyBaseIcon);
    }

    // 创建一座基地：玩家放左端，敌方放右端
    private void CreateBase(Faction faction, Sprite icon)
    {
        var gm = GridManager.Instance;

        // 基地左下角格子：玩家 = 左端（x=0），敌方 = 右端（x=最右 - 宽度）
        int x = faction == Faction.Player ? 0 : gm.gridWidth - baseWidth;
        int y = (gm.gridHeight - baseHeight) / 2;
        Vector2Int originCell = new Vector2Int(x, y);

        // 万一该位置被占用/不可放，向中线的方向逐格找可放的位置
        while (!gm.CanPlace(originCell, baseWidth, baseHeight, faction))
        {
            x += (faction == Faction.Player) ? 1 : -1;
            if (x < 0 || x + baseWidth > gm.gridWidth)
            {
                Debug.LogError("[BaseManager] 找不到可放置基地的位置");
                return;
            }
            originCell = new Vector2Int(x, y);
        }
        gm.Occupy(originCell, baseWidth, baseHeight);

        // 创建基地物体：SpriteRenderer + Building 组件
        string baseName = faction == Faction.Player ? "玩家基地" : "敌方基地";
        var go = new GameObject(baseName);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = icon != null ? icon : CreateColorSquare(faction);
        sr.sortingOrder = 0;
        // 没配图片时用颜色区分阵营（玩家蓝、敌方红）
        if (icon == null)
        {
            sr.color = faction == Faction.Player
                ? new Color(0.2f, 0.5f, 1f, 1f)
                : new Color(1f, 0.3f, 0.3f, 1f);
        }

        var building = go.AddComponent<Building>();
        building.isBase = true;

        // 基地数据：直接用通用建筑数据（大本营 = 科技塔类型，升级/研究功能以后接）
        var data = ScriptableObject.CreateInstance<BuildingDataSO>();
        data.displayName = baseName;
        data.buildingType = BuildingType.TechTower;
        data.gridWidth = baseWidth;
        data.gridHeight = baseHeight;
        data.maxHP = baseMaxHP;
        data.icon = icon;

        building.Initialize(data, faction, originCell);
        Debug.Log($"[BaseManager] 已生成 {baseName}，位置格 ({originCell.x},{originCell.y})，血量 {baseMaxHP}");
    }

    // 生成一个带颜色的方块 Sprite（玩家蓝、敌方红），用于没配基地图片时占位
    private static Sprite CreateColorSquare(Faction faction)
    {
        var tex = Texture2D.whiteTexture;
        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.width),
            new Vector2(0.5f, 0.5f),
            tex.width);

        // 颜色在生成后通过 SpriteRenderer.color 设置，这里先给一个默认色
        return sprite;
    }
}
