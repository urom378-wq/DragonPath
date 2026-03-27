using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// サードパーソン カメラ。マウス操作・ロックオン・壁貫通防止に対応。
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;          // プレイヤーのPivot(頭部付近)
    public float     targetHeightOffset = 1.5f;

    [Header("Distance")]
    public float defaultDistance = 5f;
    public float minDistance     = 1.5f;
    public float maxDistance     = 10f;
    public float scrollSpeed     = 3f;

    [Header("Sensitivity")]
    public float horizontalSensitivity = 180f;
    public float verticalSensitivity   = 120f;
    public float minVerticalAngle      = -20f;
    public float maxVerticalAngle      =  60f;

    [Header("Smoothing")]
    public float positionLerpSpeed = 12f;
    public float rotationLerpSpeed = 15f;

    [Header("Camera Collision")]
    public float   collisionRadius = 0.3f;
    public LayerMask collisionMask;

    [Header("Lock-On")]
    public float lockOnFOV        = 65f;
    public float defaultFOV       = 75f;
    public float fovTransitionSpeed = 5f;

    // --- Private State ---
    private float  _yaw;
    private float  _pitch;
    private float  _currentDistance;
    private Camera _cam;

    private Transform _lockOnTarget;
    private bool      _isLockedOn;

    private void Awake()
    {
        _cam             = GetComponent<Camera>();
        _currentDistance = defaultDistance;
        if (_cam != null) _cam.fieldOfView = defaultFOV;
    }

    private void Start()
    {
        // 初期角度をプレイヤーの向きに合わせる
        if (target != null)
        {
            _yaw   = target.eulerAngles.y;
            _pitch = 20f;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.GameOver) return;
        if (GameManager.Instance != null && GameManager.Instance.State == GameManager.GameState.Victory)  return;

        bool canControl = GameManager.Instance == null || GameManager.Instance.IsPlaying;

        if (_isLockedOn && _lockOnTarget != null)
            UpdateLockOnCamera(canControl);
        else
            UpdateFreeCamera(canControl);

        UpdateFOV();
        HandleScrollZoom();
    }

    // ============================================================
    // Free Camera
    // ============================================================
    private void UpdateFreeCamera(bool canControl)
    {
        if (canControl)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw   += delta.x * horizontalSensitivity * Time.deltaTime;
                _pitch -= delta.y * verticalSensitivity   * Time.deltaTime;
                _pitch  = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);
            }
        }

        Quaternion desiredRot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    pivot      = target.position + Vector3.up * targetHeightOffset;
        float      dist       = GetCollisionCorrectedDistance(pivot, desiredRot);

        Vector3 desiredPos = pivot - desiredRot * Vector3.forward * dist;
        transform.position = Vector3.Lerp(transform.position, desiredPos, positionLerpSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationLerpSpeed * Time.deltaTime);
    }

    // ============================================================
    // Lock-On Camera
    // ============================================================
    private void UpdateLockOnCamera(bool canControl)
    {
        if (_lockOnTarget == null) { _isLockedOn = false; return; }

        Vector3 pivot      = target.position + Vector3.up * targetHeightOffset;
        Vector3 toEnemy    = _lockOnTarget.position - pivot;
        Vector3 midpoint   = pivot + toEnemy * 0.3f;

        Quaternion lookRot = Quaternion.LookRotation((midpoint - transform.position).normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationLerpSpeed * Time.deltaTime);

        _yaw   = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;

        float dist       = GetCollisionCorrectedDistance(pivot, transform.rotation);
        Vector3 desiredPos = pivot - transform.rotation * Vector3.forward * dist;
        transform.position = Vector3.Lerp(transform.position, desiredPos, positionLerpSpeed * Time.deltaTime);
    }

    // ============================================================
    // Collision Detection
    // ============================================================
    private float GetCollisionCorrectedDistance(Vector3 pivot, Quaternion rotation)
    {
        Vector3 direction = rotation * (-Vector3.forward);
        if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit, _currentDistance, collisionMask))
            return Mathf.Max(minDistance, hit.distance - 0.1f);
        return _currentDistance;
    }

    // ============================================================
    // Scroll Zoom
    // ============================================================
    private void HandleScrollZoom()
    {
        if (!GameManager.Instance?.IsPlaying ?? false) return;
        var mouse = Mouse.current;
        if (mouse == null) return;
        float scroll    = mouse.scroll.ReadValue().y;
        _currentDistance = Mathf.Clamp(_currentDistance - scroll * scrollSpeed * Time.deltaTime, minDistance, maxDistance);
    }

    // ============================================================
    // FOV Transition
    // ============================================================
    private void UpdateFOV()
    {
        if (_cam == null) return;
        float targetFOV  = _isLockedOn ? lockOnFOV : defaultFOV;
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, fovTransitionSpeed * Time.deltaTime);
    }

    // ============================================================
    // Lock-On API (PlayerController から呼ぶ)
    // ============================================================
    public void SetLockOn(Transform target)
    {
        _lockOnTarget = target;
        _isLockedOn   = target != null;
    }

    public void ClearLockOn()
    {
        _lockOnTarget = null;
        _isLockedOn   = false;
    }

    /// <summary>カメラの水平向き（プレイヤーの移動方向計算用）</summary>
    public Vector3 CameraForwardFlat => new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
    public Vector3 CameraRightFlat   => new Vector3(transform.right.x,   0f, transform.right.z).normalized;
    public float   Yaw               => _yaw;
}
