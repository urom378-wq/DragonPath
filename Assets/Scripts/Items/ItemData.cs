using UnityEngine;

/// <summary>
/// アイテムデータ ScriptableObject
/// Assets > Create > DragonPath > Item Data で作成
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "DragonPath/Item Data")]
public class ItemData : ScriptableObject
{
    public enum ItemType   { Weapon, Armor, Helmet, Boots, Accessory, Consumable, Material, Quest }
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    [Header("Basic Info")]
    public string    itemName        = "Item Name";
    [TextArea]
    public string    description     = "Item description.";
    public Sprite    icon;
    public ItemType  type            = ItemType.Consumable;
    public ItemRarity rarity         = ItemRarity.Common;
    public int       maxStack        = 1;
    public int       price           = 100;

    [Header("Equipment Stats (装備品のみ)")]
    public float attackBonus    = 0f;
    public float defenseBonus   = 0f;
    public float hpBonus        = 0f;
    public float mpBonus        = 0f;
    public float staminaBonus   = 0f;
    public float critChance     = 0f;   // 0~1

    [Header("Consumable Effect (消耗品のみ)")]
    public float healAmount     = 0f;
    public float mpRestoreAmount = 0f;
    public float staminaRestoreAmount = 0f;
    public float buffDuration   = 0f;
    public float attackBuff     = 0f;   // 一時的な攻撃力上昇

    [Header("VFX")]
    public GameObject useVFXPrefab;

    // ============================================================
    // 使用処理（コンシューマブルのみ）
    // ============================================================
    public bool Use(PlayerStats stats)
    {
        if (type != ItemType.Consumable) return false;

        if (healAmount         > 0f) stats.Heal(healAmount);
        if (mpRestoreAmount    > 0f) stats.RestoreMP(mpRestoreAmount);
        if (staminaRestoreAmount > 0f) stats.RestoreStamina(staminaRestoreAmount);

        if (useVFXPrefab != null)
        {
            var go = Object.Instantiate(useVFXPrefab,
                stats.transform.position + Vector3.up, Quaternion.identity);
            Object.Destroy(go, 2f);
        }

        AudioManager.Instance?.PlayItemPickup();
        return true;
    }

    public Color GetRarityColor()
    {
        return rarity switch
        {
            ItemRarity.Common    => Color.white,
            ItemRarity.Uncommon  => Color.green,
            ItemRarity.Rare      => Color.blue,
            ItemRarity.Epic      => new Color(0.5f, 0f, 1f),
            ItemRarity.Legendary => Color.yellow,
            _                    => Color.white,
        };
    }
}
