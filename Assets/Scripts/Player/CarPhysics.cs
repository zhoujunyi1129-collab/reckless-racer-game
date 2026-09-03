using UnityEngine;

/// <summary>
/// 车辆物理系统 - 处理更复杂的物理模拟
/// 核心职责：
/// - 轮胎抓地力计算
/// - 车身重心和翻车检测
/// - 空气阻力和速度衰减
/// - 地形相互作用（可扩展）
/// - 碰撞响应和反弹
/// </summary>
public class CarPhysics : MonoBehaviour
{
    [Header("轮胎抓地力")]
    [SerializeField] private float tireGrip = 1.0f;              // 轮胎抓地力系数 (0-2)
    [SerializeField] private float maxLateralForce = 15f;        // 最大侧向力
    [SerializeField] private float slipThreshold = 0.3f;         // 轮胎打滑阈值
    
    [Header("空气阻力")]
    [SerializeField] private float airResistance = 0.05f;        // 空气阻力系数
    [SerializeField] private float rollingResistance = 0.02f;    // 滚动阻力系数
    
    [Header("车身稳定性")]
    [SerializeField] private float centerOfMassY = 0.1f;         // 重心高度
    [SerializeField] private float maxTiltAngle = 25f;           // 最大倾斜角度
    [SerializeField] private float rollRecoverySpeed = 5f;       // 横滚恢复速度
    [SerializeField] private float flipDetectionThreshold = 70f; // 翻车检测角度阈值
    
    [Header("碰撞响应")]
    [SerializeField] private float collisionDamping = 0.3f;      // 碰撞阻尼
    [SerializeField] private float collisionBounceFactor = 0.2f; // 碰撞反弹系数
    [SerializeField] private float minCollisionForce = 5f;       // 最小碰撞力（触发物理反应）
    
    [Header("地形影响")]
    [SerializeField] private float offRoadDragMultiplier = 1.5f; // 越野阻力倍数
    [SerializeField] private float mudSpeedPenalty = 0.6f;       // 泥地速度惩罚
    
    // 内部引用
    private Rigidbody rb;
    private CarController carController;
    private Vector3 lastFrameVelocity;
    private float currentTireSlip = 0f;                          // 当前轮胎打滑量
    private float currentTilt = 0f;                              // 当前车身倾斜角度
    private bool isFlipped = false;                              // 是否翻车
    private float speedDamping = 1.0f;                           // 速度衰减系数
    
    // 公共属性
    public float CurrentTireSlip => currentTireSlip;
    public float CurrentTilt => currentTilt;
    public bool IsFlipped => isFlipped;
    public float SpeedDamping => speedDamping;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carController = GetComponent<CarController>();
        
        if (rb == null || carController == null)
        {
            Debug.LogError("CarPhysics 需要 Rigidbody 和 CarController 组件！");
            enabled = false;
        }

        // 设置重心
        rb.centerOfMass = new Vector3(0, centerOfMassY, 0);
    }

    private void FixedUpdate()
    {
        CalculateTireSlip();
        ApplyAirResistance();
        UpdateBodyTilt();
        CheckFlipped();
        ApplyTerrainEffects();
        
        lastFrameVelocity = rb.velocity;
    }

    /// <summary>
    /// 计算轮胎打滑量
    /// 基于车身方向与实际速度方向的偏差
    /// </summary>
    private void CalculateTireSlip()
    {
        if (rb.velocity.magnitude < 0.1f)
        {
            currentTireSlip = 0f;
            return;
        }

        // 获取本地坐标系下的速度
        Vector3 localVelocity = transform.worldToLocalMatrix.MultiplyVector(rb.velocity);
        float sidewaysVelocity = Mathf.Abs(localVelocity.x);
        float forwardVelocity = Mathf.Abs(localVelocity.z);

        // 计算打滑比例
        if (forwardVelocity > 0.1f)
        {
            currentTireSlip = Mathf.Clamp01(sidewaysVelocity / (forwardVelocity + 0.1f));
        }

        // 根据打滑状态应用侧向力
        if (currentTireSlip > slipThreshold)
        {
            ApplyLateralForce();
        }
    }

    /// <summary>
    /// 应用侧向力来纠正打滑
    /// 模拟轮胎抓地力恢复速度
    /// </summary>
    private void ApplyLateralForce()
    {
        Vector3 lateralForce = Vector3.zero;
        Vector3 localVelocity = transform.worldToLocalMatrix.MultiplyVector(rb.velocity);

        // 如果车辆向左打滑，施加向右的力
        if (localVelocity.x > 0)
        {
            lateralForce = -transform.right * maxLateralForce * tireGrip * Time.fixedDeltaTime;
        }
        // 如果车辆向右打滑，施加向左的力
        else if (localVelocity.x < 0)
        {
            lateralForce = transform.right * maxLateralForce * tireGrip * Time.fixedDeltaTime;
        }

        rb.velocity += lateralForce;
    }

    /// <summary>
    /// 应用空气阻力和滚动阻力
    /// 速度越高，阻力越大
    /// </summary>
    private void ApplyAirResistance()
    {
        if (rb.velocity.magnitude < 0.01f)
            return;

        // 空气阻力（与速度平方成正比）
        Vector3 airDrag = -rb.velocity.normalized * airResistance * rb.velocity.sqrMagnitude;
        
        // 滚动阻力（与速度成正比）
        Vector3 rollingDrag = -rb.velocity * rollingResistance;
        
        // 应用总阻力
        rb.velocity += (airDrag + rollingDrag) * Time.fixedDeltaTime;
    }

    /// <summary>
    /// 更新车身倾斜角度
    /// 模拟加速/减速时车身的俯仰和侧倾
    /// </summary>
    private void UpdateBodyTilt()
    {
        Vector3 localVelocity = transform.worldToLocalMatrix.MultiplyVector(rb.velocity);
        
        // 根据加速度计算俯仰倾斜
        Vector3 acceleration = (rb.velocity - lastFrameVelocity) / Time.fixedDeltaTime;
        float pitchTilt = -Mathf.Clamp(acceleration.z * 0.05f, -maxTiltAngle, maxTiltAngle);
        
        // 根据侧向速度计算侧倾
        float sidewaysVelocity = localVelocity.x;
        float rollTilt = Mathf.Clamp(sidewaysVelocity * 0.5f, -maxTiltAngle, maxTiltAngle);
        
        currentTilt = Mathf.Max(Mathf.Abs(pitchTilt), Mathf.Abs(rollTilt));
        
        // 平滑应用倾斜（可选：添加动画效果）
        // Vector3 currentEuler = transform.eulerAngles;
        // transform.eulerAngles = Vector3.Lerp(
        //     currentEuler,
        //     new Vector3(pitchTilt, currentEuler.y, rollTilt),
        //     rollRecoverySpeed * Time.fixedDeltaTime
        // );
    }

    /// <summary>
    /// 检测车辆是否翻车
    /// 如果车身倾斜超过阈值，标记为翻车
    /// </summary>
    private void CheckFlipped()
    {
        float xRotation = Mathf.DeltaAngle(0, transform.eulerAngles.x);
        float zRotation = Mathf.DeltaAngle(0, transform.eulerAngles.z);
        
        if (Mathf.Abs(xRotation) > flipDetectionThreshold || Mathf.Abs(zRotation) > flipDetectionThreshold)
        {
            if (!isFlipped)
            {
                isFlipped = true;
                OnFlipped();
            }
        }
        else
        {
            isFlipped = false;
        }
    }

    /// <summary>
    /// 翻车事件处理
    /// </summary>
    private void OnFlipped()
    {
        Debug.Log("车辆翻车！");
        // 可以在这里添加音效、动画、伤害等逻辑
        carController.Stop();
    }

    /// <summary>
    /// 应用地形效果
    /// 模拟在不同地形上的驾驶体验（可扩展）
    /// </summary>
    private void ApplyTerrainEffects()
    {
        // TODO: 使用 Raycast 检测地面类型
        // 根据地形类型调整 speedDamping 和阻力
        
        // 示例：越野阻力
        // if (IsOnOffRoad())
        // {
        //     speedDamping = Mathf.Lerp(speedDamping, mudSpeedPenalty, Time.fixedDeltaTime * 2f);
        //     airResistance *= offRoadDragMultiplier;
        // }
    }

    /// <summary>
    /// 处理碰撞
    /// 从外部（如 CollisionHandler）调用
    /// </summary>
    public void OnCollisionImpact(Vector3 collisionNormal, float collisionForce)
    {
        if (collisionForce < minCollisionForce)
            return;

        // 计算反弹速度
        Vector3 bounceVelocity = collisionNormal * collisionForce * collisionBounceFactor;
        
        // 应用碰撞阻尼
        rb.velocity *= (1f - collisionDamping);
        rb.velocity += bounceVelocity;

        Debug.Log($"碰撞检测 - 力度: {collisionForce}, 方向: {collisionNormal}");
    }

    /// <summary>
    /// 获取当前有效速度（考虑所有阻力因素）
    /// </summary>
    public float GetEffectiveSpeed()
    {
        return rb.velocity.magnitude * speedDamping;
    }

    /// <summary>
    /// 计算轮胎抓地力系数
    /// 用于其他系统查询当前的物理状态
    /// </summary>
    public float GetTireGripCoefficient()
    {
        // 打滑时抓地力降低
        return tireGrip * (1f - currentTireSlip * 0.5f);
    }

    /// <summary>
    /// 设置地形类型
    /// 用于动态调整物理参数
    /// </summary>
    public void SetTerrainType(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Road:
                speedDamping = 1.0f;
                airResistance = 0.05f;
                break;
            case TerrainType.Grass:
                speedDamping = 0.85f;
                airResistance = 0.08f;
                break;
            case TerrainType.Mud:
                speedDamping = 0.6f;
                airResistance = 0.12f;
                break;
            case TerrainType.Water:
                speedDamping = 0.4f;
                airResistance = 0.2f;
                break;
        }
    }

    /// <summary>
    /// 获取当前地面法向量
    /// 用于计算摩擦力和法向力
    /// </summary>
    public Vector3 GetGroundNormal()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
        {
            return hit.normal;
        }
        return Vector3.up;
    }

    /// <summary>
    /// 重置物理状态
    /// 用于游戏重置或重生
    /// </summary>
    public void ResetPhysics()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentTireSlip = 0f;
        currentTilt = 0f;
        isFlipped = false;
        speedDamping = 1.0f;
    }
}

/// <summary>
/// 地形类型枚举
/// </summary>
public enum TerrainType
{
    Road,   // 路面 - 无阻力
    Grass,  // 草地 - 轻微阻力
    Mud,    // 泥地 - 中等阻力
    Water   // 水面 - 重阻力
}
