using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// インベントリUI - グリッド表示・アイテム詳細・装備スロット
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Grid")]
    public Transform    itemGridParent;
    public GameObject   itemSlotPrefab;

    [Header("Equipment Slots")]
    public Image weaponSlot;
    public Image armorSlot;
    public Image helmetSlot;
    public Image bootsSlot;
    public Image accessorySlot;

    [Header("Item Detail")]
    public CanvasGroup  detailPanel;
    public TMP_Text     itemNameText;
    public TMP_Text     itemDescText;
    public TMP_Text     itemStatsText;
    public Image        itemIconDisplay;
    public Button       useEquipBtn;
    public Button       dropBtn;
    public TMP_Text     useEquipBtnText;

    [Header("Gold Display")]
    public TMP_Text goldText;

    private Inventory  _inventory;
    private int        _selectedIndex = -1;

    // ============================================================
    private void Start()
    {
        _inventory = FindFirstObjectByType<Inventory>();
        if (_inventory != null)
        {
            _inventory.OnInventoryChanged += Refresh;
            _inventory.OnEquipmentChanged += Refresh;
            _inventory.OnGoldChanged      += g => UpdateGold(g);
        }

        useEquipBtn?.onClick.AddListener(OnUseEquipClicked);
        dropBtn    ?.onClick.AddListener(OnDropClicked);

        if (detailPanel != null)
        {
            detailPanel.alpha = 0f;
            detailPanel.interactable = false;
        }
    }

    // ============================================================
    public void Refresh()
    {
        if (_inventory == null) return;

        // グリッドクリア
        foreach (Transform child in itemGridParent)
            Destroy(child.gameObject);

        // アイテムスロット生成
        for (int i = 0; i < _inventory.Items.Count; i++)
        {
            int idx  = i;
            var slot = _inventory.Items[i];

            var go   = Instantiate(itemSlotPrefab, itemGridParent);
            var icon = go.transform.Find("Icon")?.GetComponent<Image>();
            var cnt  = go.transform.Find("Count")?.GetComponent<TMP_Text>();
            var bg   = go.GetComponent<Image>();

            if (icon != null && slot.item.icon != null) icon.sprite = slot.item.icon;
            if (cnt  != null) cnt.text = slot.count > 1 ? slot.count.ToString() : "";
            if (bg   != null) bg.color = slot.item.GetRarityColor() * 0.3f + Color.black * 0.7f;

            go.GetComponent<Button>()?.onClick.AddListener(() => SelectItem(idx));
        }

        // 空スロット埋め
        int empties = _inventory.maxSlots - _inventory.Items.Count;
        for (int i = 0; i < empties; i++)
        {
            var go = Instantiate(itemSlotPrefab, itemGridParent);
            go.GetComponent<Button>()?.onClick.RemoveAllListeners();
        }

        UpdateEquipmentSlots();
        UpdateGold(_inventory.Gold);
    }

    // ============================================================
    // Equipment Slots
    // ============================================================
    private void UpdateEquipmentSlots()
    {
        if (_inventory == null) return;
        var eq = _inventory.Equipment;
        SetEquipSlotIcon(weaponSlot,    eq.weapon);
        SetEquipSlotIcon(armorSlot,     eq.armor);
        SetEquipSlotIcon(helmetSlot,    eq.helmet);
        SetEquipSlotIcon(bootsSlot,     eq.boots);
        SetEquipSlotIcon(accessorySlot, eq.accessory);
    }

    private void SetEquipSlotIcon(Image slot, ItemData item)
    {
        if (slot == null) return;
        slot.sprite = item?.icon;
        slot.color  = item != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);
    }

    // ============================================================
    // Selection
    // ============================================================
    private void SelectItem(int index)
    {
        if (_inventory == null || index < 0 || index >= _inventory.Items.Count) return;
        _selectedIndex = index;
        var slot = _inventory.Items[index];

        AudioManager.Instance?.PlayUIClick();
        ShowDetail(slot);
    }

    private void ShowDetail(Inventory.ItemSlot slot)
    {
        if (detailPanel == null) return;
        detailPanel.alpha        = 1f;
        detailPanel.interactable = true;

        itemNameText?.SetText(slot.item.itemName);
        itemDescText?.SetText(slot.item.description);

        // 装備スタット表示
        var sb = new System.Text.StringBuilder();
        if (slot.item.attackBonus   > 0) sb.AppendLine($"攻撃力 +{slot.item.attackBonus}");
        if (slot.item.defenseBonus  > 0) sb.AppendLine($"防御力 +{slot.item.defenseBonus}");
        if (slot.item.hpBonus       > 0) sb.AppendLine($"HP +{slot.item.hpBonus}");
        if (slot.item.healAmount    > 0) sb.AppendLine($"回復量: {slot.item.healAmount}");
        if (itemStatsText != null) itemStatsText.text = sb.ToString();

        if (itemIconDisplay != null) itemIconDisplay.sprite = slot.item.icon;

        // ボタンラベル
        bool isEquip = slot.item.type == ItemData.ItemType.Weapon ||
                       slot.item.type == ItemData.ItemType.Armor  ||
                       slot.item.type == ItemData.ItemType.Helmet ||
                       slot.item.type == ItemData.ItemType.Boots  ||
                       slot.item.type == ItemData.ItemType.Accessory;

        if (useEquipBtnText != null)
            useEquipBtnText.text = isEquip ? "装備" : "使用";
    }

    // ============================================================
    // Buttons
    // ============================================================
    private void OnUseEquipClicked()
    {
        if (_selectedIndex < 0 || _inventory == null) return;
        var item = _inventory.Items[_selectedIndex].item;

        if (item.type == ItemData.ItemType.Consumable)
        {
            _inventory.UseItem(_selectedIndex);
        }
        else
        {
            _inventory.Equip(_selectedIndex);
        }

        _selectedIndex = -1;
        if (detailPanel != null) detailPanel.alpha = 0f;
        Refresh();
    }

    private void OnDropClicked()
    {
        if (_selectedIndex < 0 || _inventory == null) return;
        var item = _inventory.Items[_selectedIndex].item;
        _inventory.RemoveItem(item);
        _selectedIndex = -1;
        if (detailPanel != null) detailPanel.alpha = 0f;
        Refresh();
    }

    // ============================================================
    private void UpdateGold(int gold)
    {
        if (goldText != null) goldText.text = $"G: {gold}";
    }
}
