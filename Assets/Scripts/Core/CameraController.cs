using UnityEngine;

/// <summary>
/// 摄像机控制器 - 管理俯视角摄像机跟随和缩放
/// 核心职责：
/// - 平滑跟随玩家车辆
/// - 根据速度动态调整视角
/// - 防止摄像机穿过障碍物
/// - 支持边界限制
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("跟随设置")]
    [SerializeField] private Transform target;                   // 目标（玩家车辆）
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 10, -7); // 摄像机偏移
    [SerializeField] private float followSmoothness = 5f;        // 跟随平滑度
    [SerializeField] private float rotationSmoothness = 3f;      // 旋转平滑度
    
    [Header("动态视角")]
    [SerializeField] private bool enableDynamicZoom = true;      // 启用动态缩放
    [SerializeField] private float minZoomDistance = 8f;         // 最小缩放距离
    [SerializeField] private float maxZoomDistance = 15f;        // 最大缩放距离
    [SerializeField] private float speedZoomFactor = 0.1f;       // 速度缩放因子
    
    [Header("边界限制")]
    [SerializeField] private bool enableBoundaries = true;       // 启用边界限制
    [SerializeField] private Vector2 mapBoundsMin = new Vector2(-100, -100);
    [SerializeField] private Vector2 mapBoundsMax = new Vector2(100, 100);
    
    [Header("碰撞检测")]
    [SerializeField] private bool enableCollisionAvoidance = true; // 启用碰撞回避
    [SerializeField] private float collisionCheckDistance = 15f;   // 碰撞检测距离
    
    private Camera cam;
    private CarController carController;
    private Vector3 currentVelocity = Vector3.zero;
    private float targetZoomDistance;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (target == null)
        {
            target = FindObjectOfType<CarController>()?.transform;
        }
        carController = target?.GetComponent<CarController>();
        
        if (target == null)
        {
            Debug.LogError("CameraController: 未找到目标对象！");
            enabled = false;
        }
        
        targetZoomDistance = Vector3.Distance(transform.position, target.position);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        // 计算目标位置
        Vector3 targetPosition = CalculateTargetPosition();
        
        // 平滑移动摄像机
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            1f / followSmoothness
        );

        // 始终看向目标
        LookAtTarget();
    }

    /// <summary>
    /// 计算摄像机的目标位置
    /// 根据玩家速度和方向调整
    /// </summary>
    private Vector3 CalculateTargetPosition()
    {
        Vector3 baseOffset = cameraOffset;
        
        // 根据车辆速度动态调整摄像机距离
        if (enableDynamicZoom && carController != null)
        {
            float speedRatio = Mathf.Clamp01(carController.CurrentSpeed / 50f);
            targetZoomDistance = Mathf.Lerp(minZoomDistance, maxZoomDistance, speedRatio * speedZoomFactor);
            baseOffset.y = Mathf.Lerp(baseOffset.y, targetZoomDistance, Time.deltaTime * 2f);
        }

        // 根据车辆方向旋转偏移
        Vector3 desiredPosition = target.position + target.TransformDirection(baseOffset);

        // 应用边界限制
        if (enableBoundaries)
        {
            desiredPosition = ClampPositionToBounds(desiredPosition);
        }

        // 碰撞检测（防止摄像机穿过建筑）
        if (enableCollisionAvoidance)
        {
            desiredPosition = AvoidCollisions(desiredPosition);
        }

        return desiredPosition;
    }

    /// <summary>
    /// 限制摄像机在地图边界内
    /// </summary>
    private Vector3 ClampPositionToBounds(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, mapBoundsMin.x, mapBoundsMax.x);
        position.z = Mathf.Clamp(position.z, mapBoundsMin.y, mapBoundsMax.y);
        return position;
    }

    /// <summary>
    /// 碰撞回避 - 防止摄像机穿过障碍物
    /// </summary>
    private Vector3 AvoidCollisions(Vector3 desiredPosition)
    {
        Vector3 directionToCamera = (desiredPosition - target.position).normalized;
        float distanceToCamera = Vector3.Distance(desiredPosition, target.position);

        if (Physics.Raycast(target.position, directionToCamera, out RaycastHit hit, distanceToCamera))
        {
            // 如果有碰撞，将摄像机移到碰撞点之前
            desiredPosition = target.position + directionToCamera * (hit.distance - 0.5f);
        }

        return desiredPosition;
    }

    /// <summary>
    /// 摄像机注视目标
    /// </summary>
    private void LookAtTarget()
    {
        Vector3 lookDirection = target.position - transform.position;
        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSmoothness
            );
        }
    }

    /// <summary>
    /// 设置新的摄像机目标
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        carController = target?.GetComponent<CarController>();
    }

    /// <summary>
    /// 设置地图边界
    /// </summary>
    public void SetMapBounds(Vector2 min, Vector2 max)
    {
        mapBoundsMin = min;
        mapBoundsMax = max;
    }

    /// <summary>
    /// 启用/禁用碰撞回避
    /// </summary>
    public void SetCollisionAvoidance(bool enable)
    {
        enableCollisionAvoidance = enable;
    }

    /// <summary>
    /// 立即跳转到目标位置（用于初始化或场景切换）
    /// </summary>
    public void JumpToTarget()
    {
        if (target != null)
        {
            transform.position = CalculateTargetPosition();
            LookAtTarget();
            currentVelocity = Vector3.zero;
        }
    }
}
