using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 关卡选择按钮：鼠标悬停时放大并显示白色边框，移开时恢复，点击进入对应关卡场景
public class LevelSelectButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("点击后要进入的场景名称（在 Build Settings 里注册的名字）")]
    public string sceneName = "Level 1";

    [Header("悬停时放大的倍数")]
    public float hoverScale = 1.15f;

    [Header("白色边框的粗细")]
    public float outlineWidth = 2f;

    // 记录原始大小，用于移开鼠标时恢复
    private Vector3 originalScale;
    private Outline outline;

    private void Awake()
    {
        originalScale = transform.localScale;

        // 获取或添加白色边框组件（Outline 是 UI 的描边组件）
        outline = GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
        }
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(outlineWidth, -outlineWidth);
        outline.enabled = false; // 默认不显示边框
    }

    // 鼠标移入按钮时调用：放大 + 显示白色边框
    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * hoverScale;
        if (outline != null)
        {
            outline.enabled = true;
        }
    }

    // 鼠标移出按钮时调用：恢复原始大小 + 隐藏边框
    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    // 点击按钮时调用：进入对应关卡场景
    public void OnPointerClick(PointerEventData eventData)
    {
        // 如果场景名字没填，给出提示而不跳转
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("LevelSelectButton：还没有填 sceneName，无法进入关卡");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
