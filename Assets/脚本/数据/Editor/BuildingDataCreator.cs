using UnityEngine;
using UnityEditor;

// 编辑器工具：在菜单里一键创建各类建筑/单位的数据资产
// 使用方式：顶部菜单栏 -> The Ashen Blaze -> 创建xxx数据
public static class BuildingDataCreator
{
    // 在 Project 面板当前选中文件夹下创建一个资源塔数据资产
    [MenuItem("The Ashen Blaze/创建数据资产/资源塔")]
    public static void CreateResourceTowerData()
    {
        var asset = ScriptableObject.CreateInstance<ResourceTowerDataSO>();
        asset.displayName = "新资源塔";
        asset.buildingType = BuildingType.ResourceTower;
        asset.gridWidth = 1;
        asset.gridHeight = 1;
        asset.buildCost = new ResourceCost { gold = 20, wood = 10 };
        asset.outputs = new System.Collections.Generic.List<ResourceOutput>
        {
            new ResourceOutput { type = ResourceType.Gold, amount = 8 }
        };

        ProjectWindowUtil.CreateAsset(asset, "NewResourceTower.asset");
    }

    // 创建兵种塔数据资产
    [MenuItem("The Ashen Blaze/创建数据资产/兵种塔")]
    public static void CreateBarracksData()
    {
        var asset = ScriptableObject.CreateInstance<BarracksDataSO>();
        asset.displayName = "新兵种塔";
        asset.buildingType = BuildingType.Barracks;
        asset.gridWidth = 1;
        asset.gridHeight = 1;
        asset.buildCost = new ResourceCost { gold = 50, wood = 30 };
        asset.unitType = UnitType.Infantry;
        asset.foodCostPerSpawn = 1;

        ProjectWindowUtil.CreateAsset(asset, "NewBarracks.asset");
    }

    // 创建小兵单位数据资产
    [MenuItem("The Ashen Blaze/创建数据资产/小兵单位")]
    public static void CreateUnitData()
    {
        var asset = ScriptableObject.CreateInstance<UnitDataSO>();
        asset.displayName = "新兵种";

        ProjectWindowUtil.CreateAsset(asset, "NewUnitData.asset");
    }
}
