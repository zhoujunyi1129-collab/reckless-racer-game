using UnityEngine;

/// <summary>
/// 玩家车辆控制器 - 处理车辆的输入、速度、转向和漂移逻辑
/// 核心职责：
/// - 监听输入信号（触控/键盘）
/// - 计算车辆速度和加速度
/// - 管理转向和漂移状态
/// - 与物理系统交互
/// </summary>
public class CarController : MonoBehaviour
{
    [Header("移动参数")]
    [SerializeField] private float maxSpeed = 50f;              // 最大速度 (单位/秒)
    [SerializeField] private float acceleration = 30f;          // 加速度
    [SerializeField] private float deceleration = 20f;          // 减速度
    [SerializeField] private float reverseSpeed = 20f;           // 倒车速度
    
    [Header("转向参数")]
    [SerializeField] private float maxSteerAngle = 45f;          // 最大转向角度
    [SerializeField] private float steerSensitivity = 3f;        // 转向灵敏度
    [SerializeField] private float steerRecoverySpeed = 5f;      // 转向恢复速度（中立时）
    
    [Header("漂移参数")]
    [SerializeField] private float driftThreshold = 0.7f;        // 漂移触发阈值（横向速度比例）
    [SerializeField] private float driftAngleMult = 1.5f;        // 漂移时的转向角度倍数
    [SerializeField] private float driftDrag = 0.95f;            // 漂移摩擦力（0-1）
    [SerializeField] private float driftSpeedBoost = 1.2f;       // 漂移加速倍数
    
    [Header("物理参数")]
    [SerializeField] private float mass = 1f;                    // 车辆质量
    [SerializeField] private float friction = 0.05f;             // 地面摩擦力
    [SerializeField] private float angularDrag = 2f;             // 角速度阻力
    
    // 内部状态变量
    private Rigidbody rb;
    private Vector3 currentVelocity = Vector3.zero;              // 当前速度向量
    private float currentSpeed = 0f;                             // 当前速度大小
    private float currentSteerAngle = 0f;                        // 当前转向角度
    private float horizontalInput = 0f;                          // 水平输入 (-1 to 1)
    private float verticalInput = 0f;                            // 竖直输入 (-1 to 1)
    private bool isDrifting = false;                             // 是否在漂移
    private float driftTime = 0f;                                // 漂移时长
    
    // 公共属性（用于其他系统查询）
    public float CurrentSpeed => currentSpeed;
    public float CurrentSteerAngle => currentSteerAngle;
    public bool IsDrifting => isDrifting;
    public float DriftTime => driftTime;
    public Vector3 VelocityDirection => rb.velocity.normalized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("CarController 需要 Rigidbody 组件！");
            enabled = false;
        }
    }

    private void Update()
    {
        // 收集输入（在 Update 中处理输入）
        GatherInput();
        
        // 更新转向角度
        UpdateSteerAngle();
    }

    private void FixedUpdate()
    {
        // 在 FixedUpdate 中处理物理计算
        UpdateMovement();
        UpdateRotation();
        ApplyPhysics();
    }

    /// <summary>
    /// 收集玩家输入（触控或键盘）
    /// </summary>
    private void GatherInput()
    {
        // 竖直输入：W/Up 加速，S/Down 减速/倒车
        verticalInput = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            verticalInput = 1f;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            verticalInput = -1f;

        // 水平输入：A/Left 左转，D/Right 右转
        horizontalInput = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            horizontalInput = -1f;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            horizontalInput = 1f;

        // 触控输入支持（可选）
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            HandleTouchInput(touch.position);
        }
    }

    /// <summary>
    /// 处理触控输入
    /// 屏幕被分为左右两半：左半部分控制前进/后退，右半部分控制转向
    /// </summary>
    private void HandleTouchInput(Vector2 touchPos)
    {
        float screenCenterX = Screen.width / 2f;
        
        // 左半屏：竖直输入
        if (touchPos.x < screenCenterX)
        {
            verticalInput = (touchPos.y > Screen.height / 2f) ? 1f : -1f;
        }
        
        // 右半屏：水平输入
        if (touchPos.x > screenCenterX)
        {
            horizontalInput = (touchPos.x > Screen.width * 0.75f) ? 1f : -1f;
        }
    }

    /// <summary>
    /// 更新转向角度
    /// 根据输入平滑地改变转向角度，并在无输入时逐渐恢复到中立位置
    /// </summary>
    private void UpdateSteerAngle()
    {
        if (currentSpeed < 1f)
        {
            // 停止状态下无法转向
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, 0f, steerRecoverySpeed * Time.deltaTime);
            return;
        }

        float targetSteerAngle = horizontalInput * maxSteerAngle;
        
        // 漂移时增加转向角度
        if (isDrifting)
            targetSteerAngle *= driftAngleMult;

        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, steerSensitivity * Time.deltaTime);
    }

    /// <summary>
    /// 更新车辆移动（速度计算）
    /// </summary>
    private void UpdateMovement()
    {
        if (verticalInput > 0)
        {
            // 加速
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.fixedDeltaTime, maxSpeed);
        }
        else if (verticalInput < 0)
        {
            // 倒车或减速
            if (currentSpeed > 0)
            {
                currentSpeed = Mathf.Max(currentSpeed - deceleration * Time.fixedDeltaTime, 0f);
            }
            else
            {
                currentSpeed = Mathf.Max(currentSpeed - reverseSpeed * Time.fixedDeltaTime, -reverseSpeed * 0.5f);
            }
        }
        else
        {
            // 无输入时自然减速
            float decelerationFactor = isDrifting ? driftDrag : friction;
            currentSpeed = Mathf.Lerp(currentSpeed, 0f, decelerationFactor * Time.fixedDeltaTime);
        }

        // 检测漂移状态
        UpdateDriftState();
    }

    /// <summary>
    /// 更新漂移状态
    /// 当侧向速度与前向速度的比例超过阈值时，触发漂移
    /// </summary>
    private void UpdateDriftState()
    {
        Vector3 localVelocity = transform.worldToLocalMatrix.MultiplyVector(rb.velocity);
        float sidewaysSpeed = Mathf.Abs(localVelocity.x);
        float forwardSpeed = Mathf.Abs(localVelocity.z);

        if (forwardSpeed > 0.1f)
        {
            float driftRatio = sidewaysSpeed / forwardSpeed;
            isDrifting = driftRatio > driftThreshold && Mathf.Abs(horizontalInput) > 0.5f;
        }
        else
        {
            isDrifting = false;
        }

        if (isDrifting)
        {
            driftTime += Time.fixedDeltaTime;
        }
        else
        {
            driftTime = 0f;
        }
    }

    /// <summary>
    /// 更新车辆旋转（转向）
    /// </summary>
    private void UpdateRotation()
    {
        if (currentSpeed < 0.5f)
            return;

        // 根据转向角度旋转车辆
        float rotationAmount = currentSteerAngle * currentSpeed * Time.fixedDeltaTime;
        transform.Rotate(0, rotationAmount, 0, Space.Self);
    }

    /// <summary>
    /// 应用物理力
    /// </summary>
    private void ApplyPhysics()
    {
        // 计算世界坐标系下的速度向量
        Vector3 forwardDir = transform.forward;
        Vector3 targetVelocity = forwardDir * currentSpeed;

        // 平滑应用速度
        rb.velocity = Vector3.Lerp(rb.velocity, targetVelocity, Time.fixedDeltaTime);

        // 应用角速度阻力
        rb.angularVelocity *= (1f - angularDrag * Time.fixedDeltaTime);
    }

    /// <summary>
    /// 外部碰撞时的减速方法
    /// </summary>
    public void OnCollision(float speedLossFactor = 0.5f)
    {
        currentSpeed *= speedLossFactor;
        isDrifting = false;
    }

    /// <summary>
    /// 获取漂移加速倍数
    /// 用于其他系统判断是否应用加速奖励
    /// </summary>
    public float GetDriftSpeedBoost()
    {
        return isDrifting ? driftSpeedBoost : 1f;
    }

    /// <summary>
    /// 完全停止车辆
    /// </summary>
    public void Stop()
    {
        currentSpeed = 0f;
        rb.velocity = Vector3.zero;
        isDrifting = false;
    }

    /// <summary>
    /// 应用外部冲击力（例如碰撞、爆炸）
    /// </summary>
    public void ApplyImpulse(Vector3 impulse)
    {
        rb.velocity += impulse;
    }
}
