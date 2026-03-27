using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// プレイヤー戦闘システム
/// コンボ攻撃・強攻撃・スキル・ガード・ヒットストップ
/// </summary>
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    // ============================================================
    // Inspector
    // ============================================================
    [Header("Light Attack")]
    public float lightStamina  = 18f;
    public float lightMult     = 1.0f;
    public float lightRange    = 2.0f;
    public int   maxCombo      = 4;
    public float comboWindow   = 0.75f;

    [Header("Heavy Attack")]
    public float heavyStamina  = 40f;
    public float heavyMult     = 2.8f;
    public float heavyRange    = 2.5f;

    [Header("Block")]
    public float blockDmgReduce = 0.8f;   // ブロック時のダメージ軽減率

    [Header("Hit Stop")]
    public float hitStopDuration    = 0.07f;
    public float hitStopTimeScale   = 0.05f;

    [Header("Skills")]
    public SkillData[] skills = new SkillData[4];  // 1,2,3,4 キーに対応

    [Header("Hit Detection")]
    public Transform    weaponTip;     // 武器先端（オプション）
    public LayerMask    enemyLayer;

    [Header("VFX")]
    public GameObject slashVFX;
    public GameObject heavySlashVFX;
    public GameObject blockVFX;
    public GameObject criticalVFX;

    // ============================================================
    // State
    // ============================================================
    private PlayerStats      _stats;
    private PlayerController _ctrl;
    private Animator         _anim;

    private int   _combo        = 0;
    private float _comboTimer   = 0f;
    private bool  _isAttacking  = false;
    private bool  _canChain     = false;
    private bool  _isBlocking   = false;
    private bool  _hitStopping  = false;

    // Skill cooldowns
    private float[] _skillCooldowns = new float[4];

    // --- Animator hashes ---
    private static readonly int HashAttack      = Animator.StringToHash("Attack");
    private static readonly int HashCombo       = Animator.StringToHash("ComboIndex");
    private static readonly int HashHeavy       = Animator.StringToHash("HeavyAttack");
    private static readonly int HashBlock       = Animator.StringToHash("IsBlocking");
    private static readonly int HashSkill       = Animator.StringToHash("Skill");

    // --- Events ---
    public System.Action<int>   OnComboHit;
    public System.Action        OnComboReset;
    public System.Action<float> OnEnemyHit;

    // --- Public ---
    public bool IsAttacking => _isAttacking;
    public bool IsBlocking  => _isBlocking;
    public int  ComboIndex  => _combo;
    public float[] SkillCooldowns => _skillCooldowns;

    // ============================================================
    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        _ctrl  = GetComponent<PlayerController>();
        _anim  = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (_stats.IsDead) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        if (_hitStopping) return;

        UpdateComboTimer();
        UpdateSkillCooldowns();
        HandleAttackInput();
        HandleBlockInput();
        HandleSkillInput();
    }

    // ============================================================
    // Timers
    // ============================================================
    private void UpdateComboTimer()
    {
        if (_comboTimer > 0f)
        {
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f) ResetCombo();
        }
    }

    private void UpdateSkillCooldowns()
    {
        for (int i = 0; i < _skillCooldowns.Length; i++)
            if (_skillCooldowns[i] > 0f)
                _skillCooldowns[i] -= Time.deltaTime;
    }

    // ============================================================
    // Input
    // ============================================================
    private void HandleAttackInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)  TryLightAttack();
        if (mouse.rightButton.wasPressedThisFrame) TryHeavyAttack();
    }

    private void HandleBlockInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        _isBlocking = kb.eKey.isPressed && !_isAttacking && !_ctrl.IsDodging;
        _anim?.SetBool(HashBlock, _isBlocking);
    }

    private void HandleSkillInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) TrySkill(0);
        if (kb.digit2Key.wasPressedThisFrame) TrySkill(1);
        if (kb.digit3Key.wasPressedThisFrame) TrySkill(2);
        if (kb.digit4Key.wasPressedThisFrame) TrySkill(3);
    }

    // ============================================================
    // Light Attack (Combo)
    // ============================================================
    private void TryLightAttack()
    {
        if (_isAttacking && !_canChain) return;
        if (!_stats.TryUseStamina(lightStamina)) return;

        _combo = (_combo >= maxCombo) ? 1 : _combo + 1;
        _comboTimer = comboWindow;
        _isAttacking = true;
        _canChain    = false;

        _anim?.SetInteger(HashCombo, _combo);
        _anim?.SetTrigger(HashAttack);

        StartCoroutine(LightAttackRoutine(_combo));
        OnComboHit?.Invoke(_combo);
        AudioManager.Instance?.PlayLightAttack();
    }

    private IEnumerator LightAttackRoutine(int combo)
    {
        // 前方へ僅かに踏み込む
        yield return StartCoroutine(ForwardDash(0.12f, 2.5f));

        // ヒット判定（コンボ数に応じたタイミング）
        float delay = combo == 3 ? 0.22f : 0.18f;
        yield return new WaitForSeconds(delay);

        SpawnVFX(slashVFX, 1.2f);
        float dmg   = _stats.PhysAttack * lightMult * (combo == maxCombo ? 1.6f : 1f);
        bool  hit   = DoHit(lightRange, dmg, combo == maxCombo);
        if (hit) StartCoroutine(HitStop());

        // チェーン入力を受け付ける
        yield return new WaitForSeconds(0.12f);
        _canChain = true;

        yield return new WaitForSeconds(0.25f);
        if (_comboTimer <= 0f) ResetCombo();
        else _isAttacking = false;
    }

    // ============================================================
    // Heavy Attack
    // ============================================================
    private void TryHeavyAttack()
    {
        if (_isAttacking) return;
        if (!_stats.TryUseStamina(heavyStamina)) return;

        _isAttacking = true;
        ResetComboInternal();
        _anim?.SetTrigger(HashHeavy);
        StartCoroutine(HeavyAttackRoutine());
        AudioManager.Instance?.PlayHeavyAttack();
    }

    private IEnumerator HeavyAttackRoutine()
    {
        yield return new WaitForSeconds(0.45f); // 溜め演出
        SpawnVFX(heavySlashVFX, 1.8f);
        float dmg = _stats.PhysAttack * heavyMult;
        bool hit  = DoHit(heavyRange, dmg, true);
        if (hit) StartCoroutine(HitStop());

        yield return new WaitForSeconds(0.55f);
        _isAttacking = false;
    }

    // ============================================================
    // Hit Detection
    // ============================================================
    private bool DoHit(float range, float damage, bool canCrit)
    {
        Vector3 origin = transform.position + transform.forward * (range * 0.5f) + Vector3.up;
        Collider[] cols = Physics.OverlapSphere(origin, range * 0.55f, enemyLayer);

        bool anyHit = false;
        foreach (var col in cols)
        {
            var enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy.IsDead) continue;

            bool isCrit = canCrit && Random.value < 0.2f;
            float finalDmg = isCrit ? damage * 1.8f : damage;
            enemy.TakeDamage(finalDmg, transform.position);

            if (isCrit) SpawnVFX(criticalVFX, 1.5f);

            OnEnemyHit?.Invoke(finalDmg);
            anyHit = true;
        }
        return anyHit;
    }

    // ============================================================
    // Skill
    // ============================================================
    private void TrySkill(int idx)
    {
        if (idx >= skills.Length || skills[idx] == null) return;
        var sk = skills[idx];
        if (_skillCooldowns[idx] > 0f) return;
        if (!_stats.TryUseMP(sk.mpCost)) return;
        if (!_stats.TryUseStamina(sk.staminaCost)) return;

        _skillCooldowns[idx] = sk.cooldown;
        _anim?.SetTrigger(HashSkill);
        StartCoroutine(SkillRoutine(sk));
        AudioManager.Instance?.PlaySkill();
    }

    private IEnumerator SkillRoutine(SkillData sk)
    {
        _isAttacking = true;
        yield return new WaitForSeconds(sk.castTime);

        if (sk.vfxPrefab != null)
            Instantiate(sk.vfxPrefab, transform.position + Vector3.up, transform.rotation);

        switch (sk.skillType)
        {
            case SkillData.SkillType.MeleeAOE:
                Collider[] aoeHits = Physics.OverlapSphere(transform.position, sk.range, enemyLayer);
                foreach (var h in aoeHits)
                    h.GetComponentInParent<EnemyBase>()?.TakeDamage(sk.damage, transform.position);
                break;

            case SkillData.SkillType.DashSlash:
                yield return StartCoroutine(DashSlashRoutine(sk));
                break;

            case SkillData.SkillType.Projectile:
                if (sk.projectilePrefab != null)
                {
                    var pos  = transform.position + transform.forward + Vector3.up * 1.2f;
                    var proj = Instantiate(sk.projectilePrefab, pos, transform.rotation);
                    proj.GetComponent<Projectile>()?.Init(sk.damage, sk.range, gameObject);
                }
                break;

            case SkillData.SkillType.Buff:
                StartCoroutine(BuffRoutine(sk));
                break;
        }

        yield return new WaitForSeconds(0.5f);
        _isAttacking = false;
    }

    private IEnumerator DashSlashRoutine(SkillData sk)
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, sk.range, enemyLayer);
        if (cols.Length == 0) yield break;

        Transform tgt = cols[0].transform;
        float minD = float.MaxValue;
        foreach (var c in cols)
        {
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minD) { minD = d; tgt = c.transform; }
        }

        Vector3 start = transform.position;
        Vector3 end   = tgt.position - (tgt.position - start).normalized * 1.4f;
        float   t     = 0f, dur = 0.18f;
        while (t < dur)
        {
            transform.position = Vector3.Lerp(start, end, t / dur);
            t += Time.deltaTime;
            yield return null;
        }
        tgt.GetComponentInParent<EnemyBase>()?.TakeDamage(sk.damage, transform.position);
        StartCoroutine(HitStop());
    }

    private IEnumerator BuffRoutine(SkillData sk)
    {
        // バフ: 一定時間攻撃力上昇などはここで実装
        float buffMult = 1.5f;
        // PlayerStats に直接干渉せず、乗算バフとして扱う（拡張余地あり）
        yield return new WaitForSeconds(sk.duration);
    }

    // ============================================================
    // Block
    // ============================================================
    public void NotifyBlocked(float incomingDmg)
    {
        if (!_isBlocking) return;
        float stamCost = incomingDmg * (1f - blockDmgReduce) * 0.5f;
        _stats.TryUseStamina(stamCost);
        SpawnVFX(blockVFX, 1f);
    }

    // ============================================================
    // Utilities
    // ============================================================
    private IEnumerator ForwardDash(float dur, float speed)
    {
        float t = 0f;
        var cc = GetComponent<CharacterController>();
        while (t < dur)
        {
            cc.Move(transform.forward * speed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator HitStop()
    {
        if (_hitStopping) yield break;
        _hitStopping       = true;
        Time.timeScale     = hitStopTimeScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale     = 1f;
        _hitStopping       = false;
    }

    private void SpawnVFX(GameObject prefab, float lifetime)
    {
        if (prefab == null) return;
        var pos = weaponTip != null ? weaponTip.position : transform.position + transform.forward + Vector3.up;
        var go  = Instantiate(prefab, pos, transform.rotation);
        Destroy(go, lifetime);
    }

    private void ResetCombo()
    {
        ResetComboInternal();
        OnComboReset?.Invoke();
    }

    private void ResetComboInternal()
    {
        _combo       = 0;
        _comboTimer  = 0f;
        _isAttacking = false;
        _canChain    = false;
    }
}
