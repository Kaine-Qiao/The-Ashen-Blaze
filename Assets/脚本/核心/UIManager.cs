using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 游戏内 UI 管理器（挂在场景中的空物体上）：
// 用代码动态创建全部界面，不需要手动搭 UI。
// 1. 顶部资源栏：金币/矿石/木材/干粮 实时刷新
// 2. 底部建造菜单：每个塔一个按钮（图标+名字），点击选塔进入建造
// 3. 选中信息：当前选中塔的名字和成本
// 4. 游戏结束画面：胜利/失败 大字
public class UIManager : MonoBehaviour
{
    private Font font;
    private BuildModeManager buildMode;
    private Transform canvasTransform; // 所有 UI 的根父物体（Canvas）

    // 资源栏文字
    private Text goldText, oreText, woodText, foodText;

    // 选中信息文字
    private Text selectInfoText;

    // 建造按钮（用于高亮当前选中的塔 + 资源不足时禁用/变红）
    private Image[] buildButtonImages;
    private Button[] buildButtons;
    private Text[] buildButtonNameTexts;
    private Text[] buildButtonCostTexts;

    // 结束画面
    private GameObject endPanel;
    private Text endTitleText;

    private void Start()
    {
        buildMode = FindObjectOfType<BuildModeManager>();
        if (buildMode == null)
        {
            Debug.LogError("[UIManager] 场景中找不到 BuildModeManager，UI 无法创建建造菜单");
        }

        // 加载中文字体（用户放在 Assets/Resources/Fonts/simhei.ttf）
        font = Resources.Load<Font>("Fonts/simhei");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        CreateCanvas();
        CreateResourceBar();
        CreateBuildMenu();
        CreateEndPanel();

        // 监听游戏结束（基地被摧毁时触发）
        if (GameManager.Instance != null)
            GameManager.Instance.onStateChanged.AddListener(OnGameEnded);

        // 延迟一帧打印诊断信息，确认字体是否缺字、资源栏是否创建成功
        Invoke(nameof(LogResourceBarDiagnostics), 0.3f);
    }

    // 诊断：打印字体包含哪些字 + 资源栏 4 段是否创建成功
    private void LogResourceBarDiagnostics()
    {
        if (font != null)
        {
            Debug.Log($"[UIManager] 当前字体: {font.name} | 金:{font.HasCharacter('金')} 矿:{font.HasCharacter('矿')} 木:{font.HasCharacter('木')} 粮:{font.HasCharacter('粮')} 数:{font.HasCharacter('5')}");
        }
        Debug.Log($"[UIManager] 资源栏: gold={(goldText != null ? goldText.text : "null")} ore={(oreText != null ? oreText.text : "null")} wood={(woodText != null ? woodText.text : "null")} food={(foodText != null ? foodText.text : "null")}");
    }

    private void Update()
    {
        RefreshResourceText();
        RefreshSelectInfo();
        RefreshAffordability();
    }

    // ==================== 创建 ====================

    // 创建 Canvas 和 EventSystem（UI 必需）
    private void CreateCanvas()
    {
        var canvasGO = new GameObject("UI_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; // 永远显示在最上层
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();
        canvasTransform = canvasGO.transform;

        // EventSystem（按钮点击必需），没有才创建
        if (FindObjectOfType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }
    }

    // 顶部资源栏：四个资源文字（宽度自适应内容，不超出屏幕）
    private void CreateResourceBar()
    {
        // 半透明底条，贴在左上角；宽度交给 ContentSizeFitter 自动收缩到内容大小
        // X 偏移两个块的距离（2×(130+6)=272），让金币/矿石进入屏幕
        var bar = CreatePanel("ResourceBar", null, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 44f), new Vector2(288f, -28f), new Color(0f, 0f, 0f, 0.55f));
        var fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 用 HorizontalLayoutGroup 自动横排，宽度随内容收缩
        var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(10, 10, 0, 0);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        goldText = CreateResourceLabel(bar.transform, "Gold", "金币 0");
        oreText = CreateResourceLabel(bar.transform, "Ore", "矿石 0");
        woodText = CreateResourceLabel(bar.transform, "Wood", "木材 0");
        foodText = CreateResourceLabel(bar.transform, "Food", "干粮 0");
    }

    // 每个资源一段：文字 + 固定宽度，由 LayoutGroup 排列
    private Text CreateResourceLabel(Transform parent, string name, string content)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(130f, 0f);

        var text = go.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 130f;
        return text;
    }

    // 底部建造菜单：用 HorizontalLayoutGroup 自动排列，避免手动定位错位
    private void CreateBuildMenu()
    {
        if (buildMode == null || buildMode.catalog == null) return;

        var towers = buildMode.catalog.towers;
        if (towers == null || towers.Count == 0)
        {
            Debug.LogWarning("[UIManager] 塔目录为空，建造菜单不显示");
            return;
        }

        float btnSize = 110f;

        // 底部面板：pivot 设为底边中心；Y 偏移 = 离屏幕底 12 像素
        var panel = CreatePanel("BuildMenu", null, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, btnSize + 12f), new Vector2(0f, 12f), new Color(0f, 0f, 0f, 0.6f));
        var panelRT = panel.rectTransform;
        panelRT.pivot = new Vector2(0.5f, 0f);
        // 宽度交给 ContentSizeFitter，让面板宽度自动包裹住所有按钮
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // HorizontalLayoutGroup：自动把按钮横排
        var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(12, 12, 6, 6);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // 塔按钮（每个固定尺寸 btnSize × btnSize，LayoutGroup 自动排）
        buildButtonImages = new Image[towers.Count];
        buildButtons = new Button[towers.Count];
        buildButtonNameTexts = new Text[towers.Count];
        buildButtonCostTexts = new Text[towers.Count];
        for (int i = 0; i < towers.Count; i++)
        {
            var btn = CreateBuildButton(panel.transform, towers[i], i, btnSize);
            buildButtonImages[i] = btn.GetComponent<Image>();
            buildButtons[i] = btn;
        }

        // 选中信息：放在建造面板正上方（面板高度 ≈ btnSize+12 = 122）
        // 用 Canvas 坐标系直接定位到面板上方约 10 像素处
        // 但因为面板宽度会自适应，选中信息直接用中心锚点 + 合适的 Y 即可
        selectInfoText = CreateText("SelectInfo", null, "未选择建筑（点下面的按钮选一个）", 18,
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-300f, 140f), new Vector2(300f, 170f));
    }

    // 创建一个塔按钮（固定尺寸，由 LayoutGroup 负责定位）
    private Button CreateBuildButton(Transform parent, BuildingDataSO tower, int index, float size)
    {
        var go = new GameObject(tower.displayName);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);

        // 给 LayoutGroup 用的尺寸元素
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f, 0.95f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        int capturedIndex = index;
        btn.onClick.AddListener(() =>
        {
            if (buildMode != null) buildMode.SelectBuilding(capturedIndex);
        });

        // 塔图标（上半部分）
        if (tower.icon != null)
        {
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = tower.icon;
            iconImg.preserveAspect = true;
            var iconRT = iconImg.rectTransform;
            iconRT.anchorMin = new Vector2(0.08f, 0.35f);
            iconRT.anchorMax = new Vector2(0.92f, 0.92f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
        }

        // 塔名字
        var nameText = CreateText("Name", go.transform, tower.displayName, 14, TextAnchor.MiddleCenter,
            new Vector2(0f, 0.18f), new Vector2(1f, 0.36f), Vector2.zero, Vector2.zero);
        buildButtonNameTexts[index] = nameText;

        // 成本（底部小字）
        string costStr = FormatCost(tower.buildCost);
        var costText = CreateText("Cost", go.transform, costStr, 12, TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);
        buildButtonCostTexts[index] = costText;

        return btn;
    }

    // 结束画面：半透明全屏 + 大字
    private void CreateEndPanel()
    {
        endPanel = CreatePanel("EndPanel", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(1920f, 1080f), Vector2.zero, new Color(0f, 0f, 0f, 0.65f)).gameObject;

        endTitleText = CreateText("EndTitle", endPanel.transform, "", 90, TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        endPanel.SetActive(false);
    }

    // ==================== 刷新 ====================

    // 每帧刷新资源数字
    private void RefreshResourceText()
    {
        if (ResourceManager.Instance == null) return;
        if (goldText != null)
            goldText.text = $"金币 {ResourceManager.Instance.GetResource(Faction.Player, ResourceType.Gold)}";
        if (oreText != null)
            oreText.text = $"矿石 {ResourceManager.Instance.GetResource(Faction.Player, ResourceType.Ore)}";
        if (woodText != null)
            woodText.text = $"木材 {ResourceManager.Instance.GetResource(Faction.Player, ResourceType.Wood)}";
        if (foodText != null)
            foodText.text = $"干粮 {ResourceManager.Instance.GetResource(Faction.Player, ResourceType.Food)}";
    }

    // 每帧检查玩家资源是否足够建塔：不足 → 按钮禁用（不可点）+ 名字/成本变红
    // 未研究科技解锁的塔：显示 🔒 并禁用
    private void RefreshAffordability()
    {
        if (buildMode == null || buildMode.catalog == null || buildButtons == null) return;
        if (ResourceManager.Instance == null) return;

        var towers = buildMode.catalog.towers;
        for (int i = 0; i < buildButtons.Length; i++)
        {
            if (i >= towers.Count) break;

            bool unlocked = TechManager.Instance == null || TechManager.Instance.IsBuildingUnlocked(towers[i]);
            bool afford = unlocked && ResourceManager.Instance.CanAffordCosts(Faction.Player, towers[i].buildCost.ToDictionary());

            var btn = buildButtons[i];
            if (btn != null) btn.interactable = afford;

            if (buildButtonNameTexts[i] != null)
            {
                buildButtonNameTexts[i].color = !unlocked ? new Color(0.5f, 0.5f, 0.5f) : (afford ? Color.white : Color.red);
            }
            if (buildButtonCostTexts[i] != null)
            {
                buildButtonCostTexts[i].text = unlocked ? FormatCost(towers[i].buildCost) : "需研究解锁";
                buildButtonCostTexts[i].color = afford ? new Color(1f, 0.85f, 0.5f) : Color.red;
            }
        }
    }

    // 刷新选中信息 + 按钮高亮
    private void RefreshSelectInfo()
    {
        if (buildMode == null || selectInfoText == null) return;

        var selected = buildMode.currentSelectedTower;
        if (selected == null)
        {
            selectInfoText.text = "未选择建筑（点下面的按钮选一个）";
            if (buildButtonImages != null)
                foreach (var b in buildButtonImages) b.color = new Color(0.25f, 0.25f, 0.25f, 0.95f);
        }
        else
        {
            selectInfoText.text = $"已选择：{selected.displayName}    成本：{FormatCost(selected.buildCost)}    左键放置 / 右键取消";
            if (buildButtonImages != null)
            {
                for (int i = 0; i < buildButtonImages.Length; i++)
                {
                    bool isSelected = buildMode.catalog != null &&
                                      i < buildMode.catalog.towers.Count &&
                                      buildMode.catalog.towers[i] == selected;
                    buildButtonImages[i].color = isSelected
                        ? new Color(0.2f, 0.6f, 0.9f, 1f)   // 选中：蓝色高亮
                        : new Color(0.25f, 0.25f, 0.25f, 0.95f);
                }
            }
        }
    }

    // 游戏结束：显示大字
    private void OnGameEnded(GameState state)
    {
        if (endPanel == null || endTitleText == null) return;
        endPanel.SetActive(true);
        if (state == GameState.Victory) endTitleText.text = "胜 利 ！";
        else if (state == GameState.Defeat) endTitleText.text = "失 败";
        else endTitleText.text = "";
    }

    // ==================== 辅助 ====================

    // 把成本格式化成 "金20 木30" 这样的字符串（只显示 >0 的资源）
    private string FormatCost(ResourceCost cost)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (cost.gold > 0) parts.Add($"金{cost.gold}");
        if (cost.ore > 0) parts.Add($"矿{cost.ore}");
        if (cost.wood > 0) parts.Add($"木{cost.wood}");
        return string.Join(" ", parts);
    }

    // 创建带 Image 的面板（没有指定父物体时挂到 Canvas 下）
    private Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 size, Vector2 anchoredPos, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : canvasTransform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // 创建 Text（没有指定父物体时挂到 Canvas 下）
    private Text CreateText(string name, Transform parent, string content, int fontSize,
        TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : canvasTransform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var text = go.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false; // 文字不挡按钮点击
        return text;
    }
}
