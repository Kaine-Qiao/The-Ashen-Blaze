using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 升级面板管理器：
// 1. 点击玩家大本营 → 弹出大本营升级面板（最高 6 级）+ 科技研究入口
// 2. 点击玩家兵种塔 → 弹出升级面板 + 3 路线选择（主线 5 级 + 分支 2 级 + 三主线合成 6 级）
// 3. 点击其他塔 → 弹出普通升级面板（受大本营等级限制，最高 5 级）
public class HQUpgradeUI : MonoBehaviour
{
    [Header("大本营升级成本系数（最终成本 = 系数 × 当前等级）")]
    public int hqGoldPerLevel = 200;
    public int hqOrePerLevel = 100;
    public int hqWoodPerLevel = 80;

    [Header("防御塔升级成本系数")]
    public int towerGoldPerLevel = 50;
    public int towerOrePerLevel = 30;
    public int towerWoodPerLevel = 20;

    [Header("3 路线升级成本系数（主线/分支共用）")]
    public int pathGoldPerLevel = 80;
    public int pathOrePerLevel = 50;
    public int pathWoodPerLevel = 30;

    [Header("合成 6 级成本")]
    public int mergeGoldCost = 600;
    public int mergeOreCost = 400;
    public int mergeWoodCost = 300;

    [Header("等级上限")]
    public int maxHQLevel = 6;
    public int maxTowerLevel = 5;
    public int maxMainPathLevel = 5;
    public int maxBranchPathLevel = 2;

    [Header("拆除")]
    [Tooltip("拆除建筑返还建造成本的比例（0.5 = 返还一半）")]
    [Range(0f, 1f)]
    public float demolishRefundRatio = 0.5f;

    // 三条路线的名字
    private static readonly string[] pathNames = { "路线A", "路线B", "路线C" };

    // UI 引用
    private Transform canvasTransform;
    private GameObject panel;
    private Text titleText;
    private Text levelText;
    private Text costText;
    private Text effectText;  // 升级效果说明
    private Button upgradeBtn;
    private Button closeBtn;
    private Button demolishBtn;  // 拆除按钮
    private Button researchBtn;  // 大本营研究按钮
    private Text tipText;  // 额外提示（如"需要大本营 3 级"）

    // 3 路线区域
    private GameObject pathArea;
    private Button[] mainPathBtns;      // 3 个主线选择按钮
    private Text mainPathStatusText;
    private Button upgradeMainPathBtn;
    private Button[] branchPathBtns;    // 2 个分支选择按钮（标签动态）
    private int[] branchBtnPathIds;     // 分支按钮当前对应的路线 id
    private Text branchPathStatusText;
    private Button upgradeBranchPathBtn;
    private Button mergeBtn;
    private Text pathInfoText;

    // 科技研究面板
    private GameObject researchPanel;
    private Transform researchContent;
    private Text researchTipText;

    // 当前正在操作的建筑
    private Building currentBuilding;
    private bool isHQMode;

    private Font font;

    private void Start()
    {
        font = Resources.Load<Font>("Fonts/simhei");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    // 确保 Canvas 和面板已创建（懒加载）
    private void EnsurePanelReady()
    {
        if (panel != null) return;

        var canvasGO = GameObject.Find("UI_Canvas");
        if (canvasGO == null)
        {
            Invoke(nameof(EnsurePanelReady), 0.1f);
            return;
        }
        canvasTransform = canvasGO.transform;
        CreatePanel();
        CreatePathArea();
        panel.SetActive(false);
    }

    private void EnsureResearchPanelReady()
    {
        EnsurePanelReady();
        if (researchPanel != null || canvasTransform == null) return;
        CreateResearchPanel();
    }

    private void Update()
    {
        // ESC 关闭面板
        if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePanel();
            return;
        }
        // ESC 也关研究面板
        if (researchPanel != null && researchPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseResearchPanel();
            return;
        }

        // 面板打开时刷新
        if (panel != null && panel.activeSelf)
        {
            RefreshPanel();
        }

        // 左键点击场景建筑（面板没打开时）
        if (panel == null || !panel.activeSelf)
        {
            if (researchPanel == null || !researchPanel.activeSelf)
            {
                if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
                {
                    TryClickBuilding();
                }
            }
        }
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // 检测点击到的 Building：玩家的任何塔都能弹升级面板
    private void TryClickBuilding()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screen = Input.mousePosition;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

        Collider2D col = Physics2D.OverlapPoint(world);
        if (col == null) return;

        var building = col.GetComponent<Building>();
        if (building == null) return;
        if (building.faction != Faction.Player) return;

        OpenUpgradePanel(building);
    }

    // 根据塔类型决定最高等级
    private int GetMaxLevel(Building building)
    {
        if (building.isBase) return maxHQLevel;
        if (building.data == null) return maxTowerLevel;
        // 资源塔/兵种塔/防御塔/文化塔统一最高 5 级（大本营除外）
        return maxTowerLevel;
    }

    // 查询玩家大本营的当前等级（找场上所有 Building 里 isBase==true && faction==Player 的那一个）
    private int GetPlayerHQLevel()
    {
        var all = FindObjectsOfType<Building>();
        foreach (var b in all)
        {
            if (b.isBase && b.faction == Faction.Player) return b.level;
        }
        return 1;
    }

    // ===== 面板打开 / 刷新 =====

    private void OpenUpgradePanel(Building building)
    {
        EnsurePanelReady();
        if (panel == null) return;

        currentBuilding = building;
        isHQMode = building.isBase;
        panel.SetActive(true);
        RefreshPanel();
    }

    private void ClosePanel()
    {
        if (panel == null) return;
        panel.SetActive(false);
        currentBuilding = null;
    }

    private void RefreshPanel()
    {
        EnsurePanelReady();
        if (panel == null || currentBuilding == null) return;

        int curLevel = currentBuilding.level;
        int maxLvl = GetMaxLevel(currentBuilding);

        // === 标题 ===
        string bName = currentBuilding.data != null ? currentBuilding.data.displayName : "建筑";
        string bType = currentBuilding.isBase ? "大本营" : bName;

        if (curLevel >= maxLvl)
            titleText.text = $"{bType}（已满级）";
        else
            titleText.text = $"{bType} 升级";

        // === 等级显示 ===
        levelText.text = curLevel >= maxLvl
            ? $"当前等级：{curLevel} / {maxLvl}"
            : $"当前等级：{curLevel}  →  升级后 {curLevel + 1}";

        // === 升级效果说明 ===
        effectText.text = curLevel >= maxLvl
            ? "已达到最高等级"
            : GetUpgradeEffectDescription(currentBuilding, curLevel + 1);

        // === 拆除按钮：大本营不可拆（大本营显示研究按钮） ===
        bool canDemolish = !currentBuilding.isBase;
        if (demolishBtn != null)
        {
            demolishBtn.gameObject.SetActive(canDemolish);
            if (canDemolish && currentBuilding.data != null)
            {
                var refundText = demolishBtn.GetComponentInChildren<Text>();
                if (refundText != null)
                {
                    var rc = currentBuilding.data.buildCost;
                    int rGold = Mathf.RoundToInt(rc.gold * demolishRefundRatio);
                    int rOre = Mathf.RoundToInt(rc.ore * demolishRefundRatio);
                    int rWood = Mathf.RoundToInt(rc.wood * demolishRefundRatio);
                    refundText.text = $"拆除 (+{rGold}金 +{rOre}矿 +{rWood}木)";
                }
            }
        }

        // === 大本营显示"研究"按钮 ===
        if (researchBtn != null)
        {
            bool showResearch = isHQMode
                && TechManager.Instance != null
                && TechManager.Instance.allTechs.Count > 0;
            researchBtn.gameObject.SetActive(showResearch);
        }

        // === 3 路线区（只有兵种塔） ===
        RefreshPathArea();

        // === 满级 ===
        if (curLevel >= maxLvl)
        {
            costText.text = "已满级";
            tipText.text = "";
            upgradeBtn.interactable = false;
            return;
        }

        // === 防御塔需要检查大本营等级（其他塔无此限制）===
        bool isDefense = !currentBuilding.isBase && currentBuilding.data != null
            && currentBuilding.data.buildingType == BuildingType.DefenseTower;

        if (isDefense && curLevel + 1 > GetPlayerHQLevel())
        {
            var nextCost = GetUpgradeCost(currentBuilding, curLevel);
            int gold = nextCost.GetValueOrDefault(ResourceType.Gold, 0);
            int ore = nextCost.GetValueOrDefault(ResourceType.Ore, 0);
            int wood = nextCost.GetValueOrDefault(ResourceType.Wood, 0);
            costText.text = $"金币 {gold} 矿石 {ore} 木材 {wood}";
            tipText.text = $"⚠ 需要大本营达到 {curLevel + 1} 级（当前 {GetPlayerHQLevel()}）";
            tipText.color = new Color(1f, 0.4f, 0.4f);
            upgradeBtn.interactable = false;
            return;
        }

        // === 成本显示 + 是否够钱 ===
        var costs = GetUpgradeCost(currentBuilding, curLevel);
        int g = costs.GetValueOrDefault(ResourceType.Gold, 0);
        int o = costs.GetValueOrDefault(ResourceType.Ore, 0);
        int w = costs.GetValueOrDefault(ResourceType.Wood, 0);
        costText.text = $"金币 {g} 矿石 {o} 木材 {w}";
        tipText.text = "";

        bool afford = ResourceManager.Instance != null
            && ResourceManager.Instance.CanAffordCosts(Faction.Player, costs);
        upgradeBtn.interactable = afford;
    }

    // ===== 3 路线区域 =====

    private void RefreshPathArea()
    {
        if (pathArea == null) return;

        bool isBarracks = currentBuilding != null
            && currentBuilding.data is BarracksDataSO
            && !currentBuilding.isMerged6;
        pathArea.SetActive(isBarracks);
        if (!isBarracks) return;

        var b = currentBuilding;

        // === 主线 ===
        if (b.mainPath < 0)
        {
            // 未选：显示 3 个选择按钮
            for (int i = 0; i < 3; i++) if (mainPathBtns[i] != null) mainPathBtns[i].gameObject.SetActive(true);
            if (upgradeMainPathBtn != null) upgradeMainPathBtn.gameObject.SetActive(false);
            if (mainPathStatusText != null) mainPathStatusText.text = "主线：未选择";
        }
        else
        {
            for (int i = 0; i < 3; i++) if (mainPathBtns[i] != null) mainPathBtns[i].gameObject.SetActive(false);
            if (mainPathStatusText != null)
                mainPathStatusText.text = $"主线：{pathNames[b.mainPath]}  Lv.{b.mainPathLevel}/{maxMainPathLevel}";

            if (upgradeMainPathBtn != null)
            {
                bool canUp = b.mainPathLevel < maxMainPathLevel;
                upgradeMainPathBtn.gameObject.SetActive(canUp);
                if (canUp)
                {
                    SetButtonText(upgradeMainPathBtn, $"升主线 {FormatCostText(GetPathCost(b.mainPathLevel))}");
                    upgradeMainPathBtn.interactable = ResourceManager.Instance.CanAffordCosts(Faction.Player, GetPathCost(b.mainPathLevel));
                }
            }
        }

        // === 分支 ===
        if (b.branchPath < 0)
        {
            // 未选：显示除主线外的两个按钮
            if (branchPathBtns != null)
            {
                int idx = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (i == b.mainPath) continue;
                    if (idx < 2)
                    {
                        branchPathBtns[idx].gameObject.SetActive(true);
                        SetButtonText(branchPathBtns[idx], pathNames[i]);
                        branchBtnPathIds[idx] = i;
                        idx++;
                    }
                }
                for (; idx < 2; idx++) branchPathBtns[idx].gameObject.SetActive(false);
            }
            if (upgradeBranchPathBtn != null) upgradeBranchPathBtn.gameObject.SetActive(false);
            if (branchPathStatusText != null) branchPathStatusText.text = "分支：未选择（可叠加第二路线）";
        }
        else
        {
            for (int i = 0; i < 2; i++) if (branchPathBtns[i] != null) branchPathBtns[i].gameObject.SetActive(false);
            if (branchPathStatusText != null)
                branchPathStatusText.text = $"分支：{pathNames[b.branchPath]}  Lv.{b.branchPathLevel}/{maxBranchPathLevel}";

            if (upgradeBranchPathBtn != null)
            {
                bool canUp = b.branchPathLevel < maxBranchPathLevel;
                upgradeBranchPathBtn.gameObject.SetActive(canUp);
                if (canUp)
                {
                    SetButtonText(upgradeBranchPathBtn, $"升分支 {FormatCostText(GetPathCost(b.branchPathLevel))}");
                    upgradeBranchPathBtn.interactable = ResourceManager.Instance.CanAffordCosts(Faction.Player, GetPathCost(b.branchPathLevel));
                }
            }
        }

        // === 合成按钮（三主线都 5 级时出现） ===
        if (mergeBtn != null)
        {
            bool canMerge = CheckMergePossible(b);
            mergeBtn.gameObject.SetActive(canMerge);
            if (canMerge) SetButtonText(mergeBtn, $"合成 6 级（{FormatCostText(GetMergeCost())}）");
        }

        // === 当前加成汇总 ===
        if (pathInfoText != null)
            pathInfoText.text = FormatPathBonus(b.GetTotalPathBonus());
    }

    // 检查三主线是否都各有 1 座达到 5 级（合成 6 级条件）
    private bool CheckMergePossible(Building current)
    {
        if (current == null || !(current.data is BarracksDataSO)) return false;

        bool has0 = false, has1 = false, has2 = false;
        foreach (var b in FindObjectsOfType<Building>())
        {
            if (b.faction != Faction.Player || !(b.data is BarracksDataSO)) continue;
            if (b.isMerged6) continue;
            if (b.mainPath == 0 && b.mainPathLevel >= maxMainPathLevel) has0 = true;
            else if (b.mainPath == 1 && b.mainPathLevel >= maxMainPathLevel) has1 = true;
            else if (b.mainPath == 2 && b.mainPathLevel >= maxMainPathLevel) has2 = true;
        }
        return has0 && has1 && has2;
    }

    // 选择主线（同主线限 1 座）
    private void OnSelectMainPath(int path)
    {
        if (currentBuilding == null || !(currentBuilding.data is BarracksDataSO)) return;

        // 同主线限 1 座：场上其他玩家兵种塔占用同主线则禁止
        foreach (var b in FindObjectsOfType<Building>())
        {
            if (b == currentBuilding || b.faction != Faction.Player) continue;
            if (b.data is BarracksDataSO && b.mainPath == path)
            {
                if (tipText != null)
                {
                    tipText.text = $"⚠ 主线{pathNames[path]}已被其他兵种塔占用（每条主线只能一座塔）";
                    tipText.color = new Color(1f, 0.4f, 0.4f);
                }
                return;
            }
        }

        currentBuilding.mainPath = path;
        currentBuilding.mainPathLevel = 1; // 选中即 1 级
        Debug.Log($"[路线] {currentBuilding.name} 选择主线 {pathNames[path]}");
        RefreshPanel();
    }

    private void OnUpgradeMainPath()
    {
        if (currentBuilding == null || currentBuilding.mainPath < 0) return;
        if (currentBuilding.mainPathLevel >= maxMainPathLevel) return;

        var costs = GetPathCost(currentBuilding.mainPathLevel);
        if (!ResourceManager.Instance.SpendCosts(Faction.Player, costs)) return;

        currentBuilding.mainPathLevel++;
        Debug.Log($"[路线] {currentBuilding.name} 主线升级到 {currentBuilding.mainPathLevel} 级");
        RefreshPanel();
    }

    // 选择分支（只能选和主线不同的路线）
    private void OnSelectBranchPath(int btnIndex)
    {
        if (currentBuilding == null || !(currentBuilding.data is BarracksDataSO)) return;
        if (btnIndex < 0 || btnIndex >= branchBtnPathIds.Length) return;

        int path = branchBtnPathIds[btnIndex];
        if (path == currentBuilding.mainPath) return; // 分支不能和主线相同

        currentBuilding.branchPath = path;
        currentBuilding.branchPathLevel = 1; // 选中即 1 级
        Debug.Log($"[路线] {currentBuilding.name} 选择分支 {pathNames[path]}");
        RefreshPanel();
    }

    private void OnUpgradeBranchPath()
    {
        if (currentBuilding == null || currentBuilding.branchPath < 0) return;
        if (currentBuilding.branchPathLevel >= maxBranchPathLevel) return;

        var costs = GetPathCost(currentBuilding.branchPathLevel);
        if (!ResourceManager.Instance.SpendCosts(Faction.Player, costs)) return;

        currentBuilding.branchPathLevel++;
        Debug.Log($"[路线] {currentBuilding.name} 分支升级到 {currentBuilding.branchPathLevel} 级");
        RefreshPanel();
    }

    // 合成 6 级：扣费 → 本塔三路线全强化 → 拆除另外两座主线 5 级塔（三合一）
    private void OnMergeClick()
    {
        if (currentBuilding == null) return;
        if (!CheckMergePossible(currentBuilding)) return;

        var costs = GetMergeCost();
        if (!ResourceManager.Instance.SpendCosts(Faction.Player, costs)) return;

        currentBuilding.isMerged6 = true;
        currentBuilding.branchPath = -1;
        currentBuilding.branchPathLevel = 0;

        // 拆除其他两座主线 5 级塔（合成投入）
        foreach (var b in FindObjectsOfType<Building>())
        {
            if (b == currentBuilding || b.faction != Faction.Player) continue;
            if (b.data is BarracksDataSO && b.mainPath != currentBuilding.mainPath && b.mainPathLevel >= maxMainPathLevel)
            {
                b.DestroyBuilding();
            }
        }

        Debug.Log($"[合成] {currentBuilding.name} 合成 6 级！获得三条主线全部强化");
        RefreshPanel();
    }

    // 点击升级按钮（普通等级升级）
    private void OnUpgradeClick()
    {
        if (currentBuilding == null) return;
        int curLevel = currentBuilding.level;
        int maxLvl = GetMaxLevel(currentBuilding);
        if (curLevel >= maxLvl) return;

        // 防御塔再检查一次大本营等级
        bool isDefense = !currentBuilding.isBase && currentBuilding.data != null
            && currentBuilding.data.buildingType == BuildingType.DefenseTower;
        if (isDefense && curLevel + 1 > GetPlayerHQLevel()) return;

        var costs = GetUpgradeCost(currentBuilding, curLevel);
        if (!ResourceManager.Instance.SpendCosts(Faction.Player, costs)) return;

        currentBuilding.level++;

        // 同步更新各 Behavior 组件的 currentLevel
        int newLevel = currentBuilding.level;
        var res = currentBuilding.GetComponent<ResourceTower>();
        if (res != null) res.SetLevel(newLevel);
        var bar = currentBuilding.GetComponent<Barracks>();
        if (bar != null) bar.SetLevel(newLevel);
        var dt = currentBuilding.GetComponent<DefenseTower>();
        if (dt != null) dt.SetLevel(newLevel);
        var ct = currentBuilding.GetComponent<CultureTower>();
        if (ct != null) ct.SetLevel(newLevel);

        Debug.Log($"[升级] {(currentBuilding.isBase ? "大本营" : currentBuilding.data.displayName)} 升级到 {newLevel} 级");
        RefreshPanel();
    }

    // 点击拆除按钮：返还部分资源并销毁建筑
    private void OnDemolishClick()
    {
        if (currentBuilding == null) return;
        if (currentBuilding.isBase) return; // 大本营不能拆

        // 返还部分建造成本
        if (currentBuilding.data != null && ResourceManager.Instance != null)
        {
            var rc = currentBuilding.data.buildCost;
            int rGold = Mathf.RoundToInt(rc.gold * demolishRefundRatio);
            int rOre = Mathf.RoundToInt(rc.ore * demolishRefundRatio);
            int rWood = Mathf.RoundToInt(rc.wood * demolishRefundRatio);
            if (rGold > 0) ResourceManager.Instance.AddResource(Faction.Player, ResourceType.Gold, rGold);
            if (rOre > 0) ResourceManager.Instance.AddResource(Faction.Player, ResourceType.Ore, rOre);
            if (rWood > 0) ResourceManager.Instance.AddResource(Faction.Player, ResourceType.Wood, rWood);
            Debug.Log($"[拆除] {currentBuilding.data.displayName} 已拆除，返还 金{rGold} 矿{rOre} 木{rWood}");
        }

        // 销毁建筑（会释放格子）
        BuildingManager.Instance.RemoveBuilding(currentBuilding);
        ClosePanel();
    }

    // ===== 科技研究 =====

    private void OpenResearchPanel()
    {
        EnsureResearchPanelReady();
        if (researchPanel == null) return;
        RefreshResearchList();
        researchPanel.SetActive(true);
    }

    private void CloseResearchPanel()
    {
        if (researchPanel == null) return;
        researchPanel.SetActive(false);
    }

    private void OnResearchClick()
    {
        OpenResearchPanel();
    }

    private void OnResearchTechClick(TechDataSO tech)
    {
        if (TechManager.Instance == null) return;
        if (TechManager.Instance.Research(tech))
        {
            RefreshResearchList();
            RefreshPanel();
        }
    }

    private void RefreshResearchList()
    {
        if (researchContent == null) return;

        // 清空旧的科技按钮
        foreach (Transform child in researchContent) Destroy(child.gameObject);

        var tm = TechManager.Instance;
        if (tm == null || tm.allTechs == null || tm.allTechs.Count == 0)
        {
            if (researchTipText != null)
                researchTipText.text = "没有科技可研究（在场景中 TechManager 的科技列表里添加科技资产）";
            return;
        }
        if (researchTipText != null) researchTipText.text = "";

        foreach (var tech in tm.allTechs)
        {
            if (tech == null) continue;
            bool researched = tm.IsResearched(tech);
            bool afford = ResourceManager.Instance.CanAffordCosts(Faction.Player, tech.cost.ToDictionary());
            bool canResearch = tm.CanResearch(tech);
            CreateTechButton(tech, researched, canResearch && afford);
        }
    }

    private void CreateTechButton(TechDataSO tech, bool researched, bool interactable)
    {
        var go = new GameObject(tech.techName);
        go.transform.SetParent(researchContent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 66f;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.3f, 0.95f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = interactable && !researched;
        var captured = tech;
        btn.onClick.AddListener(() => OnResearchTechClick(captured));

        // 名称（左对齐）
        var nameText = CreateText(go.transform, tech.techName, 17, -215f, 16f, 210f, 26f);
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = researched ? new Color(0.4f, 1f, 0.5f) : (interactable ? Color.white : new Color(0.6f, 0.6f, 0.6f));

        // 成本
        var costText = CreateText(go.transform, FormatCostText(tech.cost.ToDictionary()), 13, -215f, -14f, 210f, 22f);
        costText.alignment = TextAnchor.MiddleLeft;
        costText.color = researched ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.85f, 0.5f);

        // 描述（右半部分）
        var descText = CreateText(go.transform, (researched ? "已研究：" : "") + tech.description, 13, 110f, 16f, 300f, 26f);
        descText.alignment = TextAnchor.MiddleLeft;
        descText.color = new Color(0.85f, 0.85f, 0.85f);
    }

    // ===== 升级效果说明 =====

    private string GetUpgradeEffectDescription(Building building, int targetLevel)
    {
        if (building.isBase)
        {
            return $"升级效果：基地血量 +20%，可解锁更高等级的防御塔";
        }

        if (building.data == null) return "";

        switch (building.data.buildingType)
        {
            case BuildingType.ResourceTower:
                return $"升级效果：产出量 +20%，产出间隔 -5%";
            case BuildingType.Barracks:
                return $"升级效果：每次出兵 +1 个，出兵间隔 -10%   （下面路线区可强化小兵）";
            case BuildingType.DefenseTower:
                return $"升级效果：伤害 +20%，攻速 +10%，攻击范围 +5%";
            case BuildingType.CultureTower:
                return $"升级效果：箭雨伤害 +20%，攻击间隔 -10%";
            default:
                return "";
        }
    }

    // ===== 成本计算 =====

    private Dictionary<ResourceType, int> GetUpgradeCost(Building building, int currentLevel)
    {
        int lv = currentLevel + 1;

        // 大本营
        if (building.isBase)
        {
            return new Dictionary<ResourceType, int>
            {
                { ResourceType.Gold, hqGoldPerLevel * lv },
                { ResourceType.Ore, hqOrePerLevel * lv },
                { ResourceType.Wood, hqWoodPerLevel * lv }
            };
        }

        // 其他所有塔（资源/兵种/防御/文化）统一用 tower 系数
        return new Dictionary<ResourceType, int>
        {
            { ResourceType.Gold, towerGoldPerLevel * lv },
            { ResourceType.Ore, towerOrePerLevel * lv },
            { ResourceType.Wood, towerWoodPerLevel * lv }
        };
    }

    // 路线升级成本：升到下一级 = 系数 × 下一级
    private Dictionary<ResourceType, int> GetPathCost(int currentLevel)
    {
        int lv = currentLevel + 1;
        return new Dictionary<ResourceType, int>
        {
            { ResourceType.Gold, pathGoldPerLevel * lv },
            { ResourceType.Ore, pathOrePerLevel * lv },
            { ResourceType.Wood, pathWoodPerLevel * lv }
        };
    }

    private Dictionary<ResourceType, int> GetMergeCost()
    {
        return new Dictionary<ResourceType, int>
        {
            { ResourceType.Gold, mergeGoldCost },
            { ResourceType.Ore, mergeOreCost },
            { ResourceType.Wood, mergeWoodCost }
        };
    }

    // ===== 创建面板 =====

    private void CreatePanel()
    {
        if (canvasTransform == null) return;
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 半透明全屏背景
        var bgGO = new GameObject("UpgradeBG");
        bgGO.transform.SetParent(canvasTransform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.4f);
        panel = bgGO;

        // 主面板（居中，宽 460 高 620，给路线区留空间）
        var mainGO = new GameObject("UpgradePanel");
        mainGO.transform.SetParent(bgGO.transform, false);
        var mainRT = mainGO.AddComponent<RectTransform>();
        mainRT.anchorMin = mainRT.anchorMax = new Vector2(0.5f, 0.5f);
        mainRT.pivot = new Vector2(0.5f, 0.5f);
        mainRT.sizeDelta = new Vector2(460f, 620f);
        mainRT.anchoredPosition = Vector2.zero;
        var mainImg = mainGO.AddComponent<Image>();
        mainImg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // 标题
        titleText = CreateText(mainGO.transform, "升级", 26, 0f, 260f, 420f, 50f);
        titleText.color = new Color(1f, 0.85f, 0.3f);

        // 等级文字
        levelText = CreateText(mainGO.transform, "", 20, 0f, 200f, 420f, 40f);
        levelText.color = Color.white;

        // 成本文字
        costText = CreateText(mainGO.transform, "", 18, 0f, 150f, 400f, 35f);
        costText.color = new Color(1f, 0.85f, 0.5f);

        // 升级效果说明（绿色）
        effectText = CreateText(mainGO.transform, "", 16, 0f, 105f, 420f, 35f);
        effectText.color = new Color(0.4f, 1f, 0.5f);

        // 额外提示（大本营等级不够时显示红色警告）
        tipText = CreateText(mainGO.transform, "", 16, 0f, 55f, 420f, 30f);
        tipText.color = new Color(0.7f, 0.7f, 0.7f);

        // 升级按钮
        upgradeBtn = CreateButton(mainGO.transform, "升级", 22, -80f, 5f, 150f, 55f);
        upgradeBtn.onClick.AddListener(OnUpgradeClick);

        // 拆除按钮（红色，返还部分资源）
        demolishBtn = CreateButton(mainGO.transform, "拆除", 22, 85f, 5f, 150f, 55f);
        demolishBtn.onClick.AddListener(OnDemolishClick);
        var demolishImg = demolishBtn.GetComponent<Image>();
        if (demolishImg != null) demolishImg.color = new Color(0.8f, 0.3f, 0.3f, 0.95f);

        // 大本营研究按钮（和拆除按钮同位置，只显示其一）
        researchBtn = CreateButton(mainGO.transform, "科技研究", 20, 85f, 5f, 150f, 55f);
        researchBtn.onClick.AddListener(OnResearchClick);
        researchBtn.gameObject.SetActive(false);

        // 关闭按钮 X
        closeBtn = CreateButton(mainGO.transform, "X", 24, 195f, 260f, 40f, 40f);
        closeBtn.onClick.AddListener(ClosePanel);
    }

    // 3 路线区域（挂在主面板内，兵种塔时显示）
    private void CreatePathArea()
    {
        if (panel == null) return;

        var parent = panel.transform.GetChild(0); // 主面板 UpgradePanel
        if (parent == null) return;

        pathArea = new GameObject("PathArea");
        pathArea.transform.SetParent(parent, false);
        var rt = pathArea.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(440f, 280f);
        rt.anchoredPosition = new Vector2(0f, -190f);

        // 分区标题
        var title = CreateText(pathArea.transform, "—— 3 路线升级（小兵强化）——", 16, 0f, 130f, 420f, 25f);
        title.color = new Color(1f, 0.85f, 0.3f);

        // 主线：3 个选择按钮（x: -120, -20, 80）
        mainPathBtns = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int captured = i;
            mainPathBtns[i] = CreateButton(pathArea.transform, pathNames[i], 14, -120f + i * 100f, 80f, 90f, 32f);
            mainPathBtns[i].onClick.AddListener(() => OnSelectMainPath(captured));
        }
        mainPathStatusText = CreateText(pathArea.transform, "", 14, -45f, 80f, 190f, 32f);
        mainPathStatusText.color = Color.white;
        upgradeMainPathBtn = CreateButton(pathArea.transform, "升主线", 14, 115f, 80f, 110f, 32f);
        upgradeMainPathBtn.onClick.AddListener(OnUpgradeMainPath);

        // 分支：2 个选择按钮（标签刷新时动态改）
        branchPathBtns = new Button[2];
        branchBtnPathIds = new int[2];
        for (int i = 0; i < 2; i++)
        {
            int captured = i;
            branchPathBtns[i] = CreateButton(pathArea.transform, "分支", 14, -70f + i * 140f, 25f, 120f, 32f);
            branchPathBtns[i].onClick.AddListener(() => OnSelectBranchPath(captured));
        }
        branchPathStatusText = CreateText(pathArea.transform, "", 14, -45f, 25f, 190f, 32f);
        branchPathStatusText.color = Color.white;
        upgradeBranchPathBtn = CreateButton(pathArea.transform, "升分支", 14, 115f, 25f, 110f, 32f);
        upgradeBranchPathBtn.onClick.AddListener(OnUpgradeBranchPath);

        // 合成 6 级按钮
        mergeBtn = CreateButton(pathArea.transform, "合成 6 级", 15, 0f, -35f, 210f, 40f);
        mergeBtn.onClick.AddListener(OnMergeClick);
        var mergeImg = mergeBtn.GetComponent<Image>();
        if (mergeImg != null) mergeImg.color = new Color(0.7f, 0.45f, 0.1f, 0.95f);

        // 当前加成汇总
        pathInfoText = CreateText(pathArea.transform, "", 14, 0f, -95f, 420f, 60f);
        pathInfoText.color = new Color(0.4f, 1f, 0.5f);

        pathArea.SetActive(false);
    }

    // 科技研究面板（全屏遮罩 + 中央列表）
    private void CreateResearchPanel()
    {
        if (canvasTransform == null) return;
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var bgGO = new GameObject("ResearchBG");
        bgGO.transform.SetParent(canvasTransform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.5f);
        researchPanel = bgGO;
        researchPanel.SetActive(false);

        var mainGO = new GameObject("ResearchPanel");
        mainGO.transform.SetParent(bgGO.transform, false);
        var mainRT = mainGO.AddComponent<RectTransform>();
        mainRT.anchorMin = mainRT.anchorMax = new Vector2(0.5f, 0.5f);
        mainRT.pivot = new Vector2(0.5f, 0.5f);
        mainRT.sizeDelta = new Vector2(640f, 540f);
        mainRT.anchoredPosition = Vector2.zero;
        var mainImg = mainGO.AddComponent<Image>();
        mainImg.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);

        var title = CreateText(mainGO.transform, "科技研究（大本营）", 24, 0f, 245f, 500f, 40f);
        title.color = new Color(1f, 0.85f, 0.3f);

        // 科技列表（VerticalLayoutGroup 自动排列）
        var contentGO = new GameObject("ResearchList");
        contentGO.transform.SetParent(mainGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 0f);
        contentRT.anchorMax = new Vector2(0.5f, 1f);
        contentRT.pivot = new Vector2(0.5f, 0.5f);
        contentRT.sizeDelta = new Vector2(580f, 420f);
        contentRT.anchoredPosition = new Vector2(0f, -20f);
        var layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        researchContent = contentGO.transform;

        researchTipText = CreateText(mainGO.transform, "", 15, 0f, -225f, 560f, 30f);
        researchTipText.color = new Color(1f, 0.6f, 0.4f);

        var closeBtn2 = CreateButton(mainGO.transform, "关闭", 18, 0f, -252f, 140f, 42f);
        closeBtn2.onClick.AddListener(CloseResearchPanel);
    }

    // ===== 辅助 =====

    private void SetButtonText(Button btn, string text)
    {
        if (btn == null) return;
        var t = btn.GetComponentInChildren<Text>();
        if (t != null) t.text = text;
    }

    private string FormatCostText(Dictionary<ResourceType, int> costs)
    {
        int g = costs.GetValueOrDefault(ResourceType.Gold, 0);
        int o = costs.GetValueOrDefault(ResourceType.Ore, 0);
        int w = costs.GetValueOrDefault(ResourceType.Wood, 0);
        return $"金{g} 矿{o} 木{w}";
    }

    private string FormatPathBonus(PathLevelData pb)
    {
        var parts = new List<string>();
        if (pb.attackBonus > 0f) parts.Add($"攻击+{pb.attackBonus * 100f:F0}%");
        if (pb.defenseBonus > 0f) parts.Add($"防御+{pb.defenseBonus * 100f:F0}%");
        if (pb.hpBonus > 0f) parts.Add($"血量+{pb.hpBonus * 100f:F0}%");
        if (pb.speedBonus > 0f) parts.Add($"移速+{pb.speedBonus * 100f:F0}%");
        if (pb.attackSpeedBonus > 0f) parts.Add($"攻速+{pb.attackSpeedBonus * 100f:F0}%");
        if (pb.rangeBonus > 0f) parts.Add($"范围+{pb.rangeBonus * 100f:F0}%");
        return parts.Count > 0 ? "当前加成：" + string.Join("  ", parts) : "当前加成：无";
    }

    private Text CreateText(Transform parent, string content, int fontSize,
        float centerX, float centerY, float width, float height)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(centerX, centerY);

        var text = go.AddComponent<Text>();
        text.text = content;
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(Transform parent, string content, int fontSize,
        float centerX, float centerY, float width, float height)
    {
        var go = new GameObject("Btn_" + content);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(centerX, centerY);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.5f, 0.9f, 0.95f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var text = CreateText(go.transform, content, fontSize, 0, 0, width, height);
        text.color = Color.white;
        text.raycastTarget = false;

        return btn;
    }
}
