using UnityEngine;
using System;

/// <summary>
/// 统一输入管理器 - 集中处理所有输入类型
/// 核心职责：
/// - 规范化触控、键盘、手柄输入
/// - 提供事件系统供其他脚本订阅
/// - 处理输入的生命周期（按下、保持、释放）
/// - 支持输入重映射
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("输入配置")]
    [SerializeField] private float touchDeadzone = 0.1f;         // 触控死区
    [SerializeField] private float joystickDeadzone = 0.2f;      // 摇杆死区
    [SerializeField] private bool enableKeyboardInput = true;    // 启用键盘调试
    [SerializeField] private bool enableTouchInput = true;       // 启用触控输入
    [SerializeField] private bool enableGamepadInput = true;     // 启用手柄输入
    
    // 输入状态
    private Vector2 movementInput = Vector2.zero;               // 移动输入 (-1 to 1)
    private Vector2 lookInput = Vector2.zero;                   // 观看输入（未来功能）
    private bool acceleratePressed = false;
    private bool brakePressed = false;
    private bool driftPressed = false;
    private bool boostPressed = false;
    private bool pausePressed = false;
    
    // 单例
    private static InputManager instance;
    public static InputManager Instance => instance;
    
    // 事件系统
    [System.Serializable]
    public class InputEvent : UnityEngine.Events.UnityEvent<Vector2> { }
    
    public event Action OnDriftStart;
    public event Action OnDriftEnd;
    public event Action OnBoostActivate;
    public event Action OnPauseToggle;
    public InputEvent OnMovementChanged;
    
    // 公共属性
    public Vector2 MovementInput => movementInput;
    public Vector2 LookInput => lookInput;
    public bool AcceleratePressed => acceleratePressed;
    public bool BrakePressed => brakePressed;
    public bool DriftPressed => driftPressed;
    public bool BoostPressed => boostPressed;
    public bool PausePressed => pausePressed;
    
    private bool wasDriftingLastFrame = false;

    private void Awake()
    {
        // 单例模式
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // 初始化事件
        if (OnMovementChanged == null)
            OnMovementChanged = new InputEvent();
    }

    private void Update()
    {
        if (!enabled)
            return;

        // 收集所有输入类型
        Vector2 keyboardInput = GetKeyboardInput();
        Vector2 touchInput = GetTouchInput();
        Vector2 gamepadInput = GetGamepadInput();
        
        // 优先级：手柄 > 触控 > 键盘
        if (enableGamepadInput && gamepadInput.magnitude > joystickDeadzone)
        {
            movementInput = gamepadInput;
        }
        else if (enableTouchInput && touchInput.magnitude > touchDeadzone)
        {
            movementInput = touchInput;
        }
        else if (enableKeyboardInput)
        {
            movementInput = keyboardInput;
        }
        else
        {
            movementInput = Vector2.zero;
        }

        // 触发移动事件
        OnMovementChanged?.Invoke(movementInput);
        
        // 收集其他输入
        GetActionInputs();
        
        // 处理漂移事件
        HandleDriftEvents();
    }

    /// <summary>
    /// 获取键盘输入
    /// WASD 或方向键控制移动
    /// </summary>
    private Vector2 GetKeyboardInput()
    {
        if (!enableKeyboardInput)
            return Vector2.zero;

        Vector2 input = Vector2.zero;
        
        // 上下移动
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            input.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            input.y -= 1f;
        
        // 左右移动
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            input.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            input.x += 1f;
        
        // 规范化
        return Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>
    /// 获取触控输入
    /// 屏幕分为 4 个象限控制方向
    /// </summary>
    private Vector2 GetTouchInput()
    {
        if (!enableTouchInput || Input.touchCount == 0)
            return Vector2.zero;

        Touch touch = Input.GetTouch(0);
        
        // 转换为屏幕中心相对坐标 (-1 to 1)
        Vector2 screenPos = touch.position;
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 touchRelative = (screenPos - screenCenter) / screenCenter;
        
        // 应用死区
        if (touchRelative.magnitude < touchDeadzone)
            return Vector2.zero;
        
        return Vector2.ClampMagnitude(touchRelative, 1f);
    }

    /// <summary>
    /// 获取手柄摇杆输入
    /// </summary>
    private Vector2 GetGamepadInput()
    {
        if (!enableGamepadInput)
            return Vector2.zero;

        Vector2 input = new Vector2(
            Input.GetAxis("Horizontal"),
            Input.GetAxis("Vertical")
        );
        
        // 应用死区
        if (input.magnitude < joystickDeadzone)
            return Vector2.zero;
        
        return Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>
    /// 获取动作输入（加速、刹车、漂移等）
    /// </summary>
    private void GetActionInputs()
    {
        // 加速
        acceleratePressed = Input.GetKey(KeyCode.W) || 
                           Input.GetKey(KeyCode.UpArrow) || 
                           Input.GetButton("Fire1");
        
        // 刹车/倒车
        brakePressed = Input.GetKey(KeyCode.S) || 
                      Input.GetKey(KeyCode.DownArrow) || 
                      Input.GetButton("Fire2");
        
        // 漂移
        driftPressed = Input.GetKey(KeyCode.Space) || 
                      Input.GetButton("Jump");
        
        // 加速冲刺
        boostPressed = Input.GetKeyDown(KeyCode.E) || 
                      Input.GetKeyDown(KeyCode.LeftShift);
        
        if (boostPressed)
            OnBoostActivate?.Invoke();
        
        // 暂停
        pausePressed = Input.GetKeyDown(KeyCode.Escape) || 
                      Input.GetKeyDown(KeyCode.P);
        
        if (pausePressed)
            OnPauseToggle?.Invoke();
    }

    /// <summary>
    /// 处理漂移状态变化事件
    /// </summary>
    private void HandleDriftEvents()
    {
        if (driftPressed && !wasDriftingLastFrame)
        {
            OnDriftStart?.Invoke();
        }
        else if (!driftPressed && wasDriftingLastFrame)
        {
            OnDriftEnd?.Invoke();
        }
        
        wasDriftingLastFrame = driftPressed;
    }

    /// <summary>
    /// 检查特定按键是否按下（单帧）
    /// </summary>
    public bool IsKeyPressedThisFrame(KeyCode key)
    {
        return Input.GetKeyDown(key);
    }

    /// <summary>
    /// 检查特定按键是否持续按下
    /// </summary>
    public bool IsKeyHeld(KeyCode key)
    {
        return Input.GetKey(key);
    }

    /// <summary>
    /// 重新映射输入（支持自定义控制方案）
    /// </summary>
    public void RemapInput(InputAction action, KeyCode newKey)
    {
        // TODO: 实现输入重映射系统
        Debug.Log($"输入重映射: {action} -> {newKey}");
    }

    /// <summary>
    /// 临时禁用输入（菜单、对话等场景）
    /// </summary>
    public void EnableInput(bool enable)
    {
        enabled = enable;
        if (!enable)
            movementInput = Vector2.zero;
    }

    /// <summary>
    /// 获取输入强度（0-1，用于动画混合等）
    /// </summary>
    public float GetInputMagnitude()
    {
        return movementInput.magnitude;
    }

    /// <summary>
    /// 获取输入方向（用于旋转摄像机等）
    /// </summary>
    public float GetHorizontalInput()
    {
        return movementInput.x;
    }

    public float GetVerticalInput()
    {
        return movementInput.y;
    }

    /// <summary>
    /// 清除所有输入状态
    /// 用于场景切换或游戏暂停时
    /// </summary>
    public void ClearAllInput()
    {
        movementInput = Vector2.zero;
        lookInput = Vector2.zero;
        acceleratePressed = false;
        brakePressed = false;
        driftPressed = false;
        boostPressed = false;
        pausePressed = false;
        wasDriftingLastFrame = false;
    }
}

/// <summary>
/// 输入动作枚举
/// </summary>
public enum InputAction
{
    Accelerate,
    Brake,
    Drift,
    Boost,
    Pause,
    Interact
}
