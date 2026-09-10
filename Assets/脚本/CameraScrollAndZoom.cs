using UnityEngine;

// 相机控制脚本（关卡地图用）：
// 1. 鼠标移到屏幕边缘时，视角向该方向平移
// 2. 键盘 WASD / 方向键 也可以移动视角
// 3. 鼠标滚轮缩放（正交相机，改变 orthographic size）
// 4. 移动有边界、缩放有范围，都在 Inspector 里调
public class CameraScrollAndZoom : MonoBehaviour
{
    [Header("边缘滑屏")]
    [Tooltip("屏幕边缘触发宽度（像素），鼠标离边缘小于这个值就开始移动")]
    public float edgeSize = 20f;
    [Tooltip("边缘移动速度")]
    public float edgeMoveSpeed = 10f;

    [Header("键盘移动")]
    [Tooltip("WASD / 方向键 移动速度")]
    public float keyMoveSpeed = 12f;

    [Header("滚轮缩放")]
    [Tooltip("滚轮一格改变的 orthographic size 大小")]
    public float zoomSpeed = 1f;
    [Tooltip("缩放范围：最小 size（拉最近，画面放最大）")]
    public float minSize = 3f;
    [Tooltip("缩放范围：最大 size（拉最远，画面缩最小）")]
    public float maxSize = 15f;

    [Header("移动边界（相机中心 X 坐标范围）")]
    public float minX = -20f;
    public float maxX = 20f;

    [Header("移动边界（相机中心 Y 坐标范围）")]
    public float minY = -15f;
    public float maxY = 15f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        // 2D 关卡地图用正交相机更合适，这里自动切换，不用手动改
        if (cam != null)
        {
            cam.orthographic = true;
        }
    }

    private void Update()
    {
        if (cam == null) return;

        // 先根据鼠标边缘 + 键盘算出移动方向
        Vector2 moveDir = GetMoveDirection();
        // 再应用移动（边缘和键盘分开调速）
        ApplyMove(moveDir);

        // 滚轮缩放
        ZoomByWheel();

        // 最后把相机限制在边界内
        ClampPosition();
    }

    // 计算移动方向：鼠标边缘 + 键盘，两个来源可以叠加
    private Vector2 GetMoveDirection()
    {
        Vector2 dir = Vector2.zero;

        // 鼠标移到屏幕边缘
        if (Input.mousePosition.x <= edgeSize) dir.x -= 1;
        if (Input.mousePosition.x >= Screen.width - edgeSize) dir.x += 1;
        if (Input.mousePosition.y <= edgeSize) dir.y -= 1;
        if (Input.mousePosition.y >= Screen.height - edgeSize) dir.y += 1;

        // 键盘 WASD / 方向键
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dir.y += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dir.y -= 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dir.x -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir.x += 1;

        return dir;
    }

    // 应用移动：键盘有输入就用键盘速度，否则用边缘速度
    private void ApplyMove(Vector2 dir)
    {
        if (dir == Vector2.zero) return;

        bool hasKeyInput =
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
            Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);

        float speed = hasKeyInput ? keyMoveSpeed : edgeMoveSpeed;

        // 只移动 X 和 Y，保持相机的 Z 不变
        Vector3 pos = transform.position;
        pos.x += dir.x * speed * Time.deltaTime;
        pos.y += dir.y * speed * Time.deltaTime;
        transform.position = pos;
    }

    // 鼠标滚轮缩放：改变正交相机的 size
    private void ZoomByWheel()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minSize, maxSize);
        }
    }

    // 把相机限制在边界内：限制的是"视野边缘"，而不是相机中心
    // 这样不管怎么缩放，视野都不会看到边界外的场景
    private void ClampPosition()
    {
        // 正交相机的视野范围：
        // 垂直半高 = orthographicSize
        // 水平半宽 = orthographicSize × 屏幕宽高比
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        // 相机中心的活动范围 = 边界往里缩进半个视野
        float centerMinX = minX + halfWidth;
        float centerMaxX = maxX - halfWidth;
        float centerMinY = minY + halfHeight;
        float centerMaxY = maxY - halfHeight;

        Vector3 pos = transform.position;

        // 如果缩放太大，视野比边界还宽，就保持在正中间（防止取反导致跳边）
        if (centerMaxX < centerMinX)
        {
            pos.x = (minX + maxX) * 0.5f;
        }
        else
        {
            pos.x = Mathf.Clamp(pos.x, centerMinX, centerMaxX);
        }

        if (centerMaxY < centerMinY)
        {
            pos.y = (minY + maxY) * 0.5f;
        }
        else
        {
            pos.y = Mathf.Clamp(pos.y, centerMinY, centerMaxY);
        }

        transform.position = pos;
    }
}
