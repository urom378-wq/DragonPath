using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// インタラクト可能オブジェクトの基底クラス
/// 範囲内に入ると「F キー」プロンプトを表示
/// </summary>
public abstract class InteractableBase : MonoBehaviour
{
    [Header("Interaction")]
    public float interactRange  = 2.5f;
    public string promptMessage = "F: インタラクト";

    [Header("VFX")]
    public GameObject outlineEffect;   // オプション: アウトライン表示

    private Transform  _player;
    private bool       _playerInRange = false;
    private bool       _interacted    = false;

    // ============================================================
    protected virtual void Start()
    {
        var p = FindFirstObjectByType<PlayerController>();
        if (p != null) _player = p.transform;
    }

    protected virtual void Update()
    {
        if (_player == null || _interacted) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        float dist = Vector3.Distance(transform.position, _player.position);
        bool inRange = dist <= interactRange;

        if (inRange != _playerInRange)
        {
            _playerInRange = inRange;
            OnRangeChanged(inRange);
        }

        if (inRange && Keyboard.current?.fKey.wasPressedThisFrame == true)
        {
            OnInteract(_player.GetComponent<PlayerStats>());
        }
    }

    protected virtual void OnRangeChanged(bool entered)
    {
        if (outlineEffect != null) outlineEffect.SetActive(entered);

        if (entered)
            UIManager.Instance?.ShowNotification(promptMessage, UIManager.NotificationType.Info);
    }

    protected abstract void OnInteract(PlayerStats player);

    protected void MarkInteracted()
    {
        _interacted = true;
        if (outlineEffect != null) outlineEffect.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
#endif
}
