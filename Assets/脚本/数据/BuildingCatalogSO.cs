using System.Collections.Generic;
using UnityEngine;

// 塔目录：集中引用所有塔的数据资产，供 BuildModeManager 按顺序选塔
// 在 Project 面板创建一个资产，把所有塔数据拖进列表里即可
[CreateAssetMenu(fileName = "BuildingCatalog", menuName = "The Ashen Blaze/数据/塔目录")]
public class BuildingCatalogSO : ScriptableObject
{
    [Tooltip("所有可建造的塔（顺序即快捷键数字：1=第一个，2=第二个...）")]
    public List<BuildingDataSO> towers = new List<BuildingDataSO>();
}
