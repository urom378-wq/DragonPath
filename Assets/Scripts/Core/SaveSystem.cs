using UnityEngine;
using System;
using System.IO;

/// <summary>
/// JSON ベースのセーブ/ロードシステム。最大3スロット対応。
/// </summary>
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    private const string SavePrefix = "DragonPath_Save_";
    private const int MaxSlots = 3;

    [Serializable]
    public class SaveData
    {
        public int    slot;
        public string saveDate;

        // Player Stats
        public int   level;
        public int   experience;
        public float currentHP;
        public float currentMP;
        public int   vigor;
        public int   mind;
        public int   endurance;
        public int   strength;
        public int   dexterity;
        public int   intelligence;

        // Progress
        public int   enemyKillCount;
        public bool  isBossDefeated;
        public float playTimeSeconds;

        // Position
        public float posX;
        public float posY;
        public float posZ;
    }

    private float _sessionTimer = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
            _sessionTimer += Time.deltaTime;
    }

    // ============================================================
    // Save
    // ============================================================
    public void Save(int slot, PlayerStats stats, Transform playerTransform, bool bossDefeated)
    {
        if (slot < 0 || slot >= MaxSlots) return;

        var data = new SaveData
        {
            slot              = slot,
            saveDate          = DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
            level             = stats.Level,
            experience        = stats.Experience,
            currentHP         = stats.CurrentHP,
            currentMP         = stats.CurrentMP,
            vigor             = stats.Vigor,
            mind              = stats.Mind,
            endurance         = stats.Endurance,
            strength          = stats.Strength,
            dexterity         = stats.Dexterity,
            intelligence      = stats.Intelligence,
            enemyKillCount    = GameManager.Instance?.EnemyKillCount ?? 0,
            isBossDefeated    = bossDefeated,
            playTimeSeconds   = _sessionTimer,
            posX              = playerTransform.position.x,
            posY              = playerTransform.position.y,
            posZ              = playerTransform.position.z,
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSavePath(slot), json);
        Debug.Log($"[SaveSystem] Slot {slot} saved.");
    }

    // ============================================================
    // Load
    // ============================================================
    public SaveData Load(int slot)
    {
        string path = GetSavePath(slot);
        if (!File.Exists(path)) return null;

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);
    }

    public bool HasSave(int slot) => File.Exists(GetSavePath(slot));

    public void DeleteSave(int slot)
    {
        string path = GetSavePath(slot);
        if (File.Exists(path)) File.Delete(path);
    }

    public void ApplyToPlayer(SaveData data, PlayerStats stats, Transform playerTransform)
    {
        if (data == null || stats == null) return;

        stats.ApplySaveData(data);
        playerTransform.position = new Vector3(data.posX, data.posY, data.posZ);
        _sessionTimer = data.playTimeSeconds;
    }

    private string GetSavePath(int slot) =>
        Path.Combine(Application.persistentDataPath, $"{SavePrefix}{slot}.json");

    public string GetFormattedPlayTime()
    {
        int hours   = (int)(_sessionTimer / 3600);
        int minutes = (int)((_sessionTimer % 3600) / 60);
        int seconds = (int)(_sessionTimer % 60);
        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }
}
