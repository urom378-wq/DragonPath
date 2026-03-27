using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤーの移動・回避・ロックオン制御
/// New Input System (Keyboard/Mouse直接読み取り) 使用
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed      = 4.5f;
    public float runSpeed       = 7.5f;
    public float rotationSpeed  = 15f;
    public float gravity        = -22f;

    [Header("Dodge / Roll")]
    public float dodgeSpeed     = 14f;
    public float dodgeDuration  = 0.38f;
    public float iFrameDuration = 0.28f;  // 無敵時間
    public float dodgeStamina   = 25f;

    [Header("Lock-On")]
    public float lockOnRange    = 18f;
    public LayerMask enemyLayer;

    [Header("Camera Reference")]
    public ThirdPersonCamera camController;

    // --- Components ---
    private CharacterController _cc;
    private PlayerStats         _stats;
    private PlayerCombat        _combat;
    private Animator            _animator;

    // --- Movement State ---
    private Vector3 _velocity;
    private Vector3 _moveDir;
    private bool    _isGrounded;
    private bool    _isRunning;

    // --- Dodge State ---
    private bool    _isDodging;
    private float   _dodgeTimer;
    private Vector3 _dodgeDir;

    // --- I-Frame ---
    private bool  _isInvincible;
    private float _iFrameTimer;

    // --- Lock-On ---
    private Transform _lockTarget;
    private bool      _isLockedOn;

    // --- Animator Hashes ---
    private static readonly int HashSpeed      = Animator.StringToHash("Speed");
    private static readonly int HashGrounded   = Animator.StringToHash("IsGrounded");
    private static readonly int HashDodge      = Animator.StringToHash("Dodge");
    private static readonly int HashFall       = Animator.StringToHash("Fall");

    // --- Public ---
    public bool IsInvincible  => _isInvincible;
    public bool IsDodging     => _isDodging;
    public bool IsLockedOn    => _isLockedOn;
    public Transform LockTarget => _lockTarget;
    public Vector3 MoveDirection => _moveDir;

    // ============================================================
    private void Awake()
    {
        _cc      = GetComponent<CharacterController>();
        _stats   = GetComponent<PlayerStats>();
        _combat  = GetComponent<PlayerCombat>();
        _animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // カメラ参照がなければシーンから探す
        if (camController == null)
            camController = FindFirstObjectByType<ThirdPersonCamera>();
    }

    private void Update()
    {
        if (_stats.IsDead) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        HandleGroundCheck();
        ReadInputAndMove();
        HandleDodge();
        HandleIFrames();
        HandleLockOn();
        SyncAnimator();
    }

    // ============================================================
    // Ground
    // ============================================================
    private void HandleGroundCheck()
    {
        _isGrounded = _cc.isGrounded;
        if (_isGrounded && _velocity.y < 0f) _velocity.y = -3f;
        _velocity.y += gravity * Time.deltaTime;
    }

    // ============================================================
    // Input → Movement
    // ============================================================
    private void ReadInputAndMove()
    {
        if (_isDodging) return;
        if (_combat != null && _combat.IsAttacking) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    input.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  input.y -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  input.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;

        _isRunning = kb.leftShiftKey.isPressed && input.magnitude > 0.1f;
        float speed = input.magnitude < 0.1f ? 0f : (_isRunning ? runSpeed : walkSpeed);

        // カメラ基準の移動方向
        Vector3 camForward = camController != null ? camController.CameraForwardFlat : Vector3.forward;
        Vector3 camRight   = camController != null ? camController.CameraRightFlat   : Vector3.right;
        _moveDir = (camForward * input.y + camRight * input.x).normalized;

        // 移動方向へ回転（ロックオン中は敵の方向）
        if (_moveDir.magnitude > 0.1f)
        {
            Quaternion targetRot = _isLockedOn && _lockTarget != null
                ? Quaternion.LookRotation((_lockTarget.position - transform.position).WithY(0f).normalized)
                : Quaternion.LookRotation(_moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // 移動
        Vector3 move = _moveDir * speed + Vector3.up * _velocity.y;
        _cc.Move(move * Time.deltaTime);

        // Dodge 入力
        if (kb.leftShiftKey.wasPressedThisFrame && !_isRunning)
            TryDodge();
        if (kb.spaceKey.wasPressedThisFrame)
            TryDodge();

        // Pause
        if (kb.escapeKey.wasPressedThisFrame)
            GameManager.Instance?.TogglePause();

        // Inventory
        if (kb.tabKey.wasPressedThisFrame)
            GameManager.Instance?.ToggleInventory();
    }

    // ============================================================
    // Dodge / Roll
    // ============================================================
    private void TryDodge()
    {
        if (_isDodging) return;
        if (!_stats.TryUseStamina(dodgeStamina)) return;

        _isDodging    = true;
        _dodgeTimer   = dodgeDuration;
        _isInvincible = true;
        _iFrameTimer  = iFrameDuration;
        _dodgeDir     = _moveDir.magnitude > 0.1f ? _moveDir : -transform.forward;

        _animator?.SetTrigger(HashDodge);
        AudioManager.Instance?.PlayDodge();
    }

    private void HandleDodge()
    {
        if (!_isDodging) return;
        _dodgeTimer -= Time.deltaTime;
        _cc.Move((_dodgeDir * dodgeSpeed + Vector3.up * _velocity.y) * Time.deltaTime);
        if (_dodgeTimer <= 0f) _isDodging = false;
    }

    // ============================================================
    // I-Frames
    // ============================================================
    private void HandleIFrames()
    {
        if (!_isInvincible) return;
        _iFrameTimer -= Time.deltaTime;
        if (_iFrameTimer <= 0f) _isInvincible = false;
    }

    // ============================================================
    // Lock-On
    // ============================================================
    private void HandleLockOn()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.qKey.wasPressedThisFrame) ToggleLockOn();

        // 対象が死亡/範囲外ならロック解除
        if (_isLockedOn && _lockTarget != null)
        {
            var eb = _lockTarget.GetComponentInParent<EnemyBase>();
            if (eb != null && eb.IsDead)         ClearLockOn();
            else if (Vector3.Distance(transform.position, _lockTarget.position) > lockOnRange * 1.5f)
                ClearLockOn();
        }
    }

    private void ToggleLockOn()
    {
        if (_isLockedOn) { ClearLockOn(); return; }

        Collider[] hits = Physics.OverlapSphere(transform.position, lockOnRange, enemyLayer);
        if (hits.Length == 0) return;

        Transform nearest = null;
        float minDist = float.MaxValue;
        foreach (var h in hits)
        {
            // スクリーン中央に近い敵を優先
            float dist = Vector3.Distance(transform.position, h.transform.position);
            if (dist < minDist) { minDist = dist; nearest = h.transform; }
        }

        if (nearest != null)
        {
            _lockTarget  = nearest;
            _isLockedOn  = true;
            camController?.SetLockOn(_lockTarget);
        }
    }

    private void ClearLockOn()
    {
        _lockTarget  = null;
        _isLockedOn  = false;
        camController?.ClearLockOn();
    }

    // ============================================================
    // Animator Sync
    // ============================================================
    private void SyncAnimator()
    {
        if (_animator == null) return;
        float speed = _moveDir.magnitude * (_isRunning ? runSpeed : walkSpeed);
        _animator.SetFloat(HashSpeed,    speed,    0.1f, Time.deltaTime);
        _animator.SetBool (HashGrounded, _isGrounded);
        _animator.SetBool (HashFall,     !_isGrounded && _velocity.y < -3f);
    }
}

// --- Vector3 拡張 ---
public static class Vector3Extensions
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
