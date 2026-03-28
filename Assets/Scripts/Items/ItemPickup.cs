using UnityEngine;

/// <summary>
/// ワールドに配置されたアイテムのピックアップ処理
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [Header("Item")]
    public ItemData item;
    public int      count = 1;

    [Header("Motion")]
    public float bobHeight  = 0.2f;
    public float bobSpeed   = 2.0f;
    public float rotSpeed   = 80f;

    [Header("Pickup Range")]
    public float autoPickupRange = 1.5f;

    private float   _bobTimer;
    private Vector3 _startPos;
    private bool    _initialized;
    private Inventory _inventory;

    // ============================================================
    private void Start()
    {
        _startPos    = transform.position;
        _bobTimer    = Random.Range(0f, Mathf.PI * 2f);
        _inventory   = FindAnyObjectByType<Inventory>();
        _initialized = item != null;
    }

    public void Init(ItemData itemData, int itemCount = 1)
    {
        item  = itemData;
        count = itemCount;
        _inventory   = FindAnyObjectByType<Inventory>();
        _startPos    = transform.position;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        // 浮遊アニメーション
        _bobTimer += Time.deltaTime * bobSpeed;
        float bobY = Mathf.Sin(_bobTimer) * bobHeight;
        transform.position = _startPos + new Vector3(0f, bobY, 0f);
        transform.Rotate(0f, rotSpeed * Time.deltaTime, 0f);

        // 自動拾得チェック
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null && Vector3.Distance(transform.position, player.transform.position) <= autoPickupRange)
        {
            Pickup();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) Pickup();
    }

    private void Pickup()
    {
        if (_inventory == null || item == null) return;

        if (_inventory.AddItem(item, count))
        {
            UIManager.Instance?.ShowNotification($"{item.itemName} を入手！", UIManager.NotificationType.Item);
            Destroy(gameObject);
        }
    }
}
