using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// プレイヤーのインベントリ（アイテム管理・装備スロット）
/// </summary>
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    [Header("Settings")]
    public int maxSlots = 30;

    // === アイテムスロット ===
    [Serializable]
    public class ItemSlot
    {
        public ItemData item;
        public int      count;

        public ItemSlot(ItemData item, int count = 1)
        {
            this.item  = item;
            this.count = count;
        }
    }

    // === 装備スロット ===
    [Serializable]
    public class EquipmentSlots
    {
        public ItemData weapon;
        public ItemData armor;
        public ItemData helmet;
        public ItemData boots;
        public ItemData accessory;
    }

    // ============================================================
    private List<ItemSlot>  _items     = new List<ItemSlot>();
    private EquipmentSlots  _equipment = new EquipmentSlots();
    private PlayerStats     _stats;

    public IReadOnlyList<ItemSlot> Items     => _items;
    public EquipmentSlots          Equipment => _equipment;
    public int                     Gold      { get; private set; }

    // --- Events ---
    public event Action OnInventoryChanged;
    public event Action OnEquipmentChanged;
    public event Action<int> OnGoldChanged;

    // ============================================================
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        _stats = GetComponent<PlayerStats>();
    }

    // ============================================================
    // Add / Remove
    // ============================================================
    public bool AddItem(ItemData item, int count = 1)
    {
        if (item == null) return false;

        // スタック可能なら既存スロットに追加
        if (item.maxStack > 1)
        {
            var existing = _items.Find(s => s.item == item && s.count < item.maxStack);
            if (existing != null)
            {
                int canAdd = Mathf.Min(count, item.maxStack - existing.count);
                existing.count += canAdd;
                count -= canAdd;
                if (count <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        // 新規スロット
        if (_items.Count >= maxSlots) return false;
        _items.Add(new ItemSlot(item, count));
        OnInventoryChanged?.Invoke();
        AudioManager.Instance?.PlayItemPickup();
        return true;
    }

    public bool RemoveItem(ItemData item, int count = 1)
    {
        var slot = _items.Find(s => s.item == item);
        if (slot == null) return false;

        slot.count -= count;
        if (slot.count <= 0) _items.Remove(slot);

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool HasItem(ItemData item, int count = 1)
    {
        var slot = _items.Find(s => s.item == item);
        return slot != null && slot.count >= count;
    }

    public int ItemCount(ItemData item)
    {
        var slot = _items.Find(s => s.item == item);
        return slot?.count ?? 0;
    }

    // ============================================================
    // Use
    // ============================================================
    public bool UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _items.Count) return false;
        var slot = _items[slotIndex];
        if (slot.item.type != ItemData.ItemType.Consumable) return false;

        bool used = slot.item.Use(_stats);
        if (used)
        {
            slot.count--;
            if (slot.count <= 0) _items.RemoveAt(slotIndex);
            OnInventoryChanged?.Invoke();
        }
        return used;
    }

    // ============================================================
    // Equipment
    // ============================================================
    public void Equip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _items.Count) return;
        var slot = _items[slotIndex];
        if (!IsEquippable(slot.item)) return;

        ItemData current = GetEquippedItem(slot.item.type);
        if (current != null)
        {
            AddItem(current); // 現在の装備をインベントリへ戻す
        }

        SetEquipSlot(slot.item.type, slot.item);
        RemoveItem(slot.item);
        OnEquipmentChanged?.Invoke();
    }

    public void Unequip(ItemData.ItemType type)
    {
        var current = GetEquippedItem(type);
        if (current == null) return;

        if (!AddItem(current)) return; // インベントリが満杯なら解除不可
        SetEquipSlot(type, null);
        OnEquipmentChanged?.Invoke();
    }

    private bool IsEquippable(ItemData item)
    {
        return item.type == ItemData.ItemType.Weapon  ||
               item.type == ItemData.ItemType.Armor   ||
               item.type == ItemData.ItemType.Helmet  ||
               item.type == ItemData.ItemType.Boots   ||
               item.type == ItemData.ItemType.Accessory;
    }

    private ItemData GetEquippedItem(ItemData.ItemType type)
    {
        return type switch
        {
            ItemData.ItemType.Weapon    => _equipment.weapon,
            ItemData.ItemType.Armor     => _equipment.armor,
            ItemData.ItemType.Helmet    => _equipment.helmet,
            ItemData.ItemType.Boots     => _equipment.boots,
            ItemData.ItemType.Accessory => _equipment.accessory,
            _                          => null,
        };
    }

    private void SetEquipSlot(ItemData.ItemType type, ItemData item)
    {
        switch (type)
        {
            case ItemData.ItemType.Weapon:    _equipment.weapon    = item; break;
            case ItemData.ItemType.Armor:     _equipment.armor     = item; break;
            case ItemData.ItemType.Helmet:    _equipment.helmet    = item; break;
            case ItemData.ItemType.Boots:     _equipment.boots     = item; break;
            case ItemData.ItemType.Accessory: _equipment.accessory = item; break;
        }
    }

    /// <summary>装備ボーナスの合計を計算</summary>
    public float TotalAttackBonus()
    {
        float bonus = 0f;
        if (_equipment.weapon    != null) bonus += _equipment.weapon.attackBonus;
        if (_equipment.accessory != null) bonus += _equipment.accessory.attackBonus;
        return bonus;
    }

    public float TotalDefenseBonus()
    {
        float bonus = 0f;
        if (_equipment.armor  != null) bonus += _equipment.armor.defenseBonus;
        if (_equipment.helmet != null) bonus += _equipment.helmet.defenseBonus;
        if (_equipment.boots  != null) bonus += _equipment.boots.defenseBonus;
        return bonus;
    }

    // ============================================================
    // Gold
    // ============================================================
    public void AddGold(int amount)
    {
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }
}
