using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// インゲームHUD（HP/MP/スタミナ/XPバー・コンボ表示・スキルクールダウン）
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("HP")]
    public Slider   hpSlider;
    public Slider   hpDelayedSlider;
    public TMP_Text hpText;
    public Image    hpFill;

    [Header("MP")]
    public Slider   mpSlider;
    public TMP_Text mpText;

    [Header("Stamina")]
    public Slider   staminaSlider;
    public TMP_Text staminaText;

    [Header("XP / Level")]
    public Slider   xpSlider;
    public TMP_Text levelText;
    public TMP_Text xpText;

    [Header("Combo")]
    public TMP_Text comboText;
    public CanvasGroup comboGroup;

    [Header("Skill Icons")]
    public Image[]    skillIcons      = new Image[4];
    public Image[]    skillCooldowns  = new Image[4];
    public TMP_Text[] skillCDTexts    = new TMP_Text[4];

    [Header("Crosshair")]
    public GameObject crosshair;
    public GameObject lockOnIndicator;

    [Header("Colors")]
    public Color hpColorHigh   = new Color(0.2f, 0.85f, 0.2f);
    public Color hpColorMid    = new Color(0.9f, 0.7f, 0.1f);
    public Color hpColorLow    = new Color(0.9f, 0.15f, 0.1f);

    private PlayerStats      _stats;
    private PlayerCombat     _combat;
    private PlayerController _ctrl;
    private Inventory        _inventory;

    private float _hpDelayedTarget;
    private float _comboFadeTimer;

    // ============================================================
    private void Start()
    {
        _stats     = FindFirstObjectByType<PlayerStats>();
        _combat    = FindFirstObjectByType<PlayerCombat>();
        _ctrl      = FindFirstObjectByType<PlayerController>();
        _inventory = FindFirstObjectByType<Inventory>();

        if (_stats != null)
        {
            _stats.OnHPChanged      += UpdateHP;
            _stats.OnMPChanged      += UpdateMP;
            _stats.OnStaminaChanged += UpdateStamina;
            _stats.OnXPChanged      += UpdateXP;
            _stats.OnLevelUp        += OnLevelUp;

            // 初期値設定
            UpdateHP(_stats.CurrentHP, _stats.MaxHP);
            UpdateMP(_stats.CurrentMP, _stats.MaxMP);
            UpdateStamina(_stats.CurrentStamina, _stats.MaxStamina);
            UpdateXP(_stats.Experience, _stats.ExperienceToNext);
            UpdateLevel(_stats.Level);
        }

        if (_combat != null)
        {
            _combat.OnComboHit   += ShowCombo;
            _combat.OnComboReset += HideCombo;
        }

        if (comboGroup != null) comboGroup.alpha = 0f;
    }

    // ============================================================
    private void Update()
    {
        UpdateHPDelayed();
        UpdateSkillCooldowns();
        UpdateComboFade();
        UpdateLockOnIndicator();
    }

    // ============================================================
    // HP
    // ============================================================
    private void UpdateHP(float current, float max)
    {
        float ratio = max > 0 ? current / max : 0f;

        if (hpSlider        != null) hpSlider.value = ratio;
        if (hpText          != null) hpText.text    = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        _hpDelayedTarget = ratio;

        // 色変化
        if (hpFill != null)
        {
            hpFill.color = ratio > 0.6f ? hpColorHigh :
                           ratio > 0.3f ? hpColorMid  : hpColorLow;
        }

        // 低HP警告点滅
        if (ratio < 0.25f) StartCoroutine(FlashHP());
    }

    private void UpdateHPDelayed()
    {
        if (hpDelayedSlider == null) return;
        hpDelayedSlider.value = Mathf.Lerp(hpDelayedSlider.value, _hpDelayedTarget, Time.deltaTime * 1.5f);
    }

    private IEnumerator FlashHP()
    {
        if (hpFill == null) yield break;
        Color orig = hpFill.color;
        hpFill.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        hpFill.color = orig;
    }

    // ============================================================
    // MP / Stamina
    // ============================================================
    private void UpdateMP(float current, float max)
    {
        if (mpSlider != null) mpSlider.value = max > 0 ? current / max : 0f;
        if (mpText   != null) mpText.text    = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void UpdateStamina(float current, float max)
    {
        if (staminaSlider != null) staminaSlider.value = max > 0 ? current / max : 0f;
        if (staminaText   != null) staminaText.text    = $"{Mathf.CeilToInt(current)}";
    }

    // ============================================================
    // XP / Level
    // ============================================================
    private void UpdateXP(int current, int toNext)
    {
        if (xpSlider != null) xpSlider.value = toNext > 0 ? (float)current / toNext : 0f;
        if (xpText   != null) xpText.text    = $"{current} / {toNext}";
    }

    private void OnLevelUp(int newLevel)
    {
        UpdateLevel(newLevel);
        StartCoroutine(LevelUpFlash());
    }

    private void UpdateLevel(int level)
    {
        if (levelText != null) levelText.text = $"Lv. {level}";
    }

    private IEnumerator LevelUpFlash()
    {
        if (levelText == null) yield break;
        Color orig = levelText.color;
        for (int i = 0; i < 4; i++)
        {
            levelText.color = Color.yellow;
            yield return new WaitForSeconds(0.15f);
            levelText.color = orig;
            yield return new WaitForSeconds(0.15f);
        }
    }

    // ============================================================
    // Combo Display
    // ============================================================
    private void ShowCombo(int count)
    {
        _comboFadeTimer = 1.5f;
        if (comboText  != null) comboText.text = $"{count} HIT";
        if (comboGroup != null) comboGroup.alpha = 1f;
    }

    private void HideCombo()
    {
        _comboFadeTimer = 0f;
    }

    private void UpdateComboFade()
    {
        if (comboGroup == null) return;
        if (_comboFadeTimer > 0f)
        {
            _comboFadeTimer -= Time.deltaTime;
            if (_comboFadeTimer <= 0f)
                comboGroup.alpha = Mathf.Lerp(comboGroup.alpha, 0f, Time.deltaTime * 3f);
        }
        else
        {
            comboGroup.alpha = Mathf.Lerp(comboGroup.alpha, 0f, Time.deltaTime * 3f);
        }
    }

    // ============================================================
    // Skill Cooldowns
    // ============================================================
    private void UpdateSkillCooldowns()
    {
        if (_combat == null) return;
        for (int i = 0; i < _combat.SkillCooldowns.Length && i < skillCooldowns.Length; i++)
        {
            float cd  = _combat.SkillCooldowns[i];
            float max = _combat.skills[i]?.cooldown ?? 1f;
            float ratio = max > 0 ? Mathf.Clamp01(cd / max) : 0f;

            if (skillCooldowns[i] != null) skillCooldowns[i].fillAmount = ratio;
            if (skillCDTexts[i]   != null)
                skillCDTexts[i].text = cd > 0f ? $"{cd:F1}" : "";
        }
    }

    // ============================================================
    // Lock-On Indicator
    // ============================================================
    private void UpdateLockOnIndicator()
    {
        if (lockOnIndicator == null || _ctrl == null) return;
        lockOnIndicator.SetActive(_ctrl.IsLockedOn);

        if (_ctrl.IsLockedOn && _ctrl.LockTarget != null)
        {
            Vector3 screenPos = Camera.main?.WorldToScreenPoint(_ctrl.LockTarget.position + Vector3.up * 1.5f) ?? Vector3.zero;
            if (screenPos.z > 0)
                lockOnIndicator.transform.position = screenPos;
        }
    }
}
