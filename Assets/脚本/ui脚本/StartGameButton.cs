using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGameButton : MonoBehaviour
{
    // 点击"开始游戏"按钮时调用，跳转到游戏地图场景
    public void OnClickStart()
    {
        SceneManager.LoadScene("Map Scenes");
    }
}
