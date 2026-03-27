using UnityEngine;
using System.Collections;

/// <summary>
/// 宝箱 - 開封アニメーション・アイテムドロップ・一度限り
/// </summary>
public class TreasureChest : InteractableBase
{
    [Header("Loot")]
    public ItemData[] possibleItems;
    [Range(1, 5)]
    public int        dropCount     = 2;
    public int        goldAmount    = 50;

    [Header("Animation")]
    public Transform  lidTransform;
    public float      openAngle     = -90f;
    public float      openSpeed     = 3f;

    [Header("VFX")]
    public GameObject openVFXPrefab;
    public GameObject itemDropPrefab;

    private bool _isOpened = false;

    // ============================================================
    protected override void Start()
    {
        base.Start();
        promptMessage = "F: 宝箱を開ける";
    }

    protected override void OnInteract(PlayerStats player)
    {
        if (_isOpened) return;
        _isOpened = true;
        MarkInteracted();
        StartCoroutine(OpenChest(player));
    }

    private IEnumerator OpenChest(PlayerStats player)
    {
        // 蓋を開くアニメーション
        if (lidTransform != null)
        {
            float elapsed = 0f;
            Quaternion startRot = lidTransform.localRotation;
            Quaternion endRot   = Quaternion.Euler(openAngle, 0f, 0f);
            while (elapsed < 1f / openSpeed)
            {
                elapsed += Time.deltaTime;
                lidTransform.localRotation = Quaternion.Slerp(startRot, endRot, elapsed * openSpeed);
                yield return null;
            }
            lidTransform.localRotation = endRot;
        }

        AudioManager.Instance?.PlayChestOpen();

        // VFX
        if (openVFXPrefab != null)
        {
            var vfx = Instantiate(openVFXPrefab, transform.position + Vector3.up, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // ゴールド付与
        if (goldAmount > 0)
        {
            Inventory.Instance?.AddGold(goldAmount);
            UIManager.Instance?.ShowNotification($"Gold +{goldAmount}", UIManager.NotificationType.Item);
        }

        // アイテムドロップ
        if (possibleItems != null && possibleItems.Length > 0 && itemDropPrefab != null)
        {
            for (int i = 0; i < Mathf.Min(dropCount, possibleItems.Length); i++)
            {
                var item = possibleItems[Random.Range(0, possibleItems.Length)];
                Vector3 dropPos = transform.position + Vector3.up + Random.insideUnitSphere * 0.8f;
                dropPos.y = transform.position.y + 0.5f;
                var go = Instantiate(itemDropPrefab, dropPos, Quaternion.identity);
                go.GetComponent<ItemPickup>()?.Init(item);
            }
        }
    }
}
