using System.Collections.Generic;
using UnityEngine;

// 建筑管理器（挂在场景中的空物体上）：
// 建造/拆除的统一入口，负责：
// 1. 检测位置能否放置（网格占用 + 资源是否够）
// 2. 扣除资源
// 3. 生成建筑物体并初始化
public class BuildingManager : MonoBehaviour
{
    [Header("建筑预制体")]
    [Tooltip("建筑物体的预制体（上面挂着 Building 脚本），所有塔共用这一个")]
    public GameObject buildingPrefab;

    // 单例
    public static BuildingManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 尝试建造一个建筑：位置可用 + 资源够 才成功
    // 返回生成出来的建筑组件；失败返回 null
    public Building TryPlaceBuilding(BuildingDataSO data, Vector3 worldPos, Faction faction)
    {
        if (data == null)
        {
            Debug.LogWarning("[BuildingManager] 没有指定建筑数据");
            return null;
        }

        if (buildingPrefab == null)
        {
            Debug.LogError("[BuildingManager] 未指定 buildingPrefab，无法生成建筑");
            return null;
        }

        // 1. 世界坐标 -> 网格坐标（建筑左下角格子）
        Vector2Int originCell = GridManager.Instance.WorldToCell(worldPos);

        // 2. 检查位置能否放置（界内/未占用/正确半场）
        if (!GridManager.Instance.CanPlace(originCell, data.gridWidth, data.gridHeight, faction))
        {
            Debug.LogWarning("[BuildingManager] 该位置无法放置");
            return null;
        }

        // 3. 检查资源是否够
        Dictionary<ResourceType, int> costs = data.buildCost.ToDictionary();
        if (!ResourceManager.Instance.CanAffordCosts(faction, costs))
        {
            Debug.LogWarning("[BuildingManager] 资源不足");
            return null;
        }

        // 4. 扣资源、占用格子
        ResourceManager.Instance.SpendCosts(faction, costs);
        GridManager.Instance.Occupy(originCell, data.gridWidth, data.gridHeight);

        // 5. 生成建筑物体
        GameObject go = Instantiate(buildingPrefab);
        Building building = go.GetComponent<Building>();
        if (building == null)
        {
            Debug.LogError("[BuildingManager] buildingPrefab 上没有 Building 脚本");
            Destroy(go);
            return null;
        }

        building.Initialize(data, faction, originCell);
        return building;
    }

    // 拆除建筑（会释放占用的格子）
    public void RemoveBuilding(Building building)
    {
        if (building == null) return;
        building.DestroyBuilding();
    }
}
