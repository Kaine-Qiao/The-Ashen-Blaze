using UnityEngine;
using UnityEngine.Events;

// 游戏状态枚举：整局游戏的进行状态
public enum GameState
{
    [Tooltip("游戏进行中")]
    Playing,

    [Tooltip("玩家获胜")]
    Victory,

    [Tooltip("玩家失败")]
    Defeat
}

// 游戏管理器（挂在场景中的空物体上）：
// 管理整局游戏的状态（进行中/胜利/失败），提供单例入口
public class GameManager : MonoBehaviour
{
    // 当前游戏状态
    public GameState CurrentState { get; private set; } = GameState.Playing;

    // 单例，方便其他脚本随时访问
    public static GameManager Instance { get; private set; }

    // 事件：游戏状态改变时触发（UI 可以监听它来显示胜负画面）
    [Header("事件")]
    [Tooltip("游戏状态改变时触发")]
    public UnityEvent<GameState> onStateChanged = new UnityEvent<GameState>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 外部调用：宣布游戏结束（胜利或失败）
    // 例如基地被摧毁时调用 GameManager.Instance.EndGame(GameState.Defeat)
    public void EndGame(GameState result)
    {
        if (CurrentState != GameState.Playing) return; // 游戏已经结束了就不再重复触发
        CurrentState = result;
        onStateChanged.Invoke(result);
        Debug.Log($"[GameManager] 游戏结束：{result}");
    }
}
