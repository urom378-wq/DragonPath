using UnityEngine;
using System;

/// <summary>
/// プレイヤーのステータス管理（HP/MP/スタミナ/レベル/経験値）
/// </summary>
public class PlayerStats : MonoBehaviour
{
    // === ベースステータス（インスペクター設定） ===
    [Header("Base Stats")]
    [SerializeField] private int _vigor        = 10; // HP上限
    [SerializeField] private int _mind         = 10; // MP上限
    [SerializeField] private int _endurance    = 10; // スタミナ上限
    [SerializeField] private int _strength     = 10; // 物理攻撃
    [SerializeField] private int _dexterity    = 10; // 攻撃速度補正
    [SerializeField] private int _intelligence = 10; // 魔法攻撃

    [Header("Level")]
    [SerializeField] private int _level              = 1;
    [SerializeField] private int _experience         = 0;
    [SerializeField] private int _experienceToNext   = 200;

    // === 派生ステータス ===
    public float MaxHP       => 300f + _vigor        * 25f;
    public float MaxMP       =>  80f + _mind         * 12f;
    public float MaxStamina  => 100f + _endurance    * 10f;
    public float PhysAttack  =>  15f + _strength     *  4f + _dexterity;
    public float MagAttack   =>   8f + _intelligence *  5f;
    public float Defense     =>   5f + _endurance    *  0.8f;

    // === 現在値（プロパティ） ===
    public float CurrentHP      { get; private set; }
    public float CurrentMP      { get; private set; }
    public float CurrentStamina { get; private set; }

    // === パブリック読み取り ===
    public int Level            => _level;
    public int Experience       => _experience;
    public int ExperienceToNext => _experienceToNext;
    public int Vigor            => _vigor;
    public int Mind             => _mind;
    public int Endurance        => _endurance;
    public int Strength         => _strength;
    public int Dexterity        => _dexterity;
    public int Intelligence     => _intelligence;
    public bool IsDead          { get; private set; }

    // === スタミナ再生設定 ===
    private const float StaminaRegenRate  = 30f;
    private const float StaminaRegenDelay = 1.0f;
    private float _staminaRegenTimer = 0f;

    // === イベント ===
    public event Action<float, float> OnHPChanged;       // current, max
    public event Action<float, float> OnMPChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<int, int>     OnXPChanged;       // current, toNext
    public event Action<int>          OnLevelUp;
    public event Action<float>        OnDamageReceived;  // damage amount
    public event Action               OnDeath;

    private void Awake()
    {
        CurrentHP      = MaxHP;
        CurrentMP      = MaxMP;
        CurrentStamina = MaxStamina;
    }

    private void Update()
    {
        if (IsDead) return;
        RegenStamina();
        RegenMP();
    }

    // ============================================================
    // Stamina & MP Regen
    // ============================================================
    private void RegenStamina()
    {
        if (CurrentStamina >= MaxStamina) return;

        if (_staminaRegenTimer > 0f)
        {
            _staminaRegenTimer -= Time.deltaTime;
            return;
        }
        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + StaminaRegenRate * Time.deltaTime);
        OnStaminaChanged?.Invoke(CurrentStamina, MaxStamina);
    }

    private void RegenMP()
    {
        if (CurrentMP >= MaxMP) return;
        CurrentMP = Mathf.Min(MaxMP, CurrentMP + 4f * Time.deltaTime);
        OnMPChanged?.Invoke(CurrentMP, MaxMP);
    }

    // ============================================================
    // Resource Consumption
    // ============================================================
    public bool TryUseStamina(float amount)
    {
        if (CurrentStamina < amount) return false;
        CurrentStamina     -= amount;
        _staminaRegenTimer  = StaminaRegenDelay;
        OnStaminaChanged?.Invoke(CurrentStamina, MaxStamina);
        return true;
    }

    public bool TryUseMP(float amount)
    {
        if (CurrentMP < amount) return false;
        CurrentMP -= amount;
        OnMPChanged?.Invoke(CurrentMP, MaxMP);
        return true;
    }

    // ============================================================
    // Damage & Heal
    // ============================================================
    public void TakeDamage(float damage, bool ignoreDefense = false)
    {
        if (IsDead) return;
        float final = ignoreDefense ? damage : Mathf.Max(1f, damage - Defense);
        CurrentHP = Mathf.Max(0f, CurrentHP - final);
        OnDamageReceived?.Invoke(final);
        OnHPChanged?.Invoke(CurrentHP, MaxHP);
        if (CurrentHP <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        OnHPChanged?.Invoke(CurrentHP, MaxHP);
    }

    public void RestoreMP(float amount)
    {
        CurrentMP = Mathf.Min(MaxMP, CurrentMP + amount);
        OnMPChanged?.Invoke(CurrentMP, MaxMP);
    }

    public void RestoreStamina(float amount)
    {
        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + amount);
        OnStaminaChanged?.Invoke(CurrentStamina, MaxStamina);
    }

    // ============================================================
    // Experience & Level
    // ============================================================
    public void AddExperience(int amount)
    {
        _experience += amount;
        while (_experience >= _experienceToNext)
        {
            _experience      -= _experienceToNext;
            LevelUp();
        }
        OnXPChanged?.Invoke(_experience, _experienceToNext);
    }

    private void LevelUp()
    {
        _level++;
        _vigor++;
        _mind++;
        _endurance++;
        _strength++;
        _dexterity++;
        _intelligence++;
        _experienceToNext = Mathf.RoundToInt(_experienceToNext * 1.55f);

        // レベルアップ時HP/MP全回復
        CurrentHP      = MaxHP;
        CurrentMP      = MaxMP;
        CurrentStamina = MaxStamina;

        OnLevelUp?.Invoke(_level);
        OnHPChanged?.Invoke(CurrentHP, MaxHP);
        OnMPChanged?.Invoke(CurrentMP, MaxMP);
        AudioManager.Instance?.PlayLevelUp();
    }

    // ============================================================
    // Death
    // ============================================================
    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        OnDeath?.Invoke();
        AudioManager.Instance?.PlayPlayerDeath();
        GameManager.Instance?.TriggerGameOver();
    }

    // ============================================================
    // Save/Load
    // ============================================================
    public void ApplySaveData(SaveSystem.SaveData data)
    {
        _level            = data.level;
        _experience       = data.experience;
        _experienceToNext = Mathf.RoundToInt(200 * Mathf.Pow(1.55f, _level - 1));
        _vigor            = data.vigor;
        _mind             = data.mind;
        _endurance        = data.endurance;
        _strength         = data.strength;
        _dexterity        = data.dexterity;
        _intelligence     = data.intelligence;

        CurrentHP         = Mathf.Min(data.currentHP, MaxHP);
        CurrentMP         = Mathf.Min(data.currentMP, MaxMP);
        CurrentStamina    = MaxStamina;

        OnHPChanged?.Invoke(CurrentHP, MaxHP);
        OnMPChanged?.Invoke(CurrentMP, MaxMP);
        OnXPChanged?.Invoke(_experience, _experienceToNext);
    }
}
