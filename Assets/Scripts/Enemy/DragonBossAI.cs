using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// ドラゴンボスAI - 3フェーズ制
/// フェーズ1 (100-66%): 地上戦 - 爪斬り/噛みつき/尻尾払い/炎ブレス
/// フェーズ2 (66-33%): 空中戦 - ダイブ/空中炎ブレス/羽風
/// フェーズ3 (33-0%):  狂乱   - 全攻撃が高速化/炎オーラ/絶望攻撃
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DragonBossAI : EnemyBase
{
    // ============================================================
    // Inspector
    // ============================================================
    [Header("Boss Stats")]
    public string bossName          = "古龍ヴァルドラス";
    public float  phase2Threshold   = 0.66f;
    public float  phase3Threshold   = 0.33f;

    [Header("Attack Ranges")]
    public float clawRange          = 3.5f;
    public float biteRange          = 3.0f;
    public float tailRange          = 4.0f;
    public float fireBreathRange    = 12f;
    public float diveRange          = 20f;

    [Header("Attack Damage")]
    public float clawDamage         = 25f;
    public float biteDamage         = 40f;
    public float tailDamage         = 20f;
    public float fireBreathDamage   = 8f;   // per tick
    public float diveDamage         = 60f;
    public float enrageDamageBonus  = 1.5f;

    [Header("Cooldowns")]
    public float clawCooldown       = 2.0f;
    public float biteCooldown       = 3.5f;
    public float tailCooldown       = 4.0f;
    public float fireBreathCooldown = 6.0f;
    public float diveCooldown       = 8.0f;

    [Header("Movement")]
    public float groundChaseSpeed   = 5.0f;
    public float phase3SpeedBonus   = 2.0f;

    [Header("VFX")]
    public GameObject fireBreathVFX;
    public GameObject diveImpactVFX;
    public GameObject phaseTransitionVFX;
    public GameObject enrageAuraVFX;
    public ParticleSystem fireAura;

    [Header("References")]
    public BossHealthBar bossHealthBar;
    public Transform     flyTarget;     // 空中移動先

    // ============================================================
    // State Machine
    // ============================================================
    private enum BossState { Idle, Chase, Phase1, Phase2, Phase3, Transitioning, Dead }
    private BossState _state = BossState.Idle;
    private int       _phase = 1;

    private NavMeshAgent _agent;
    private Animator     _anim;
    private Transform    _player;
    private PlayerStats  _playerStats;
    private PlayerController _playerCtrl;

    // Attack cooldown timers
    private float _clawTimer, _biteTimer, _tailTimer, _fireTimer, _diveTimer;

    // Flying state
    private bool    _isFlying   = false;
    private Vector3 _flyPos;

    // Phase transitions
    private bool _phase2Triggered = false;
    private bool _phase3Triggered = false;
    private bool _isRoaring       = false;

    // --- Animator Hashes ---
    private static readonly int HashSpeed       = Animator.StringToHash("Speed");
    private static readonly int HashClaw        = Animator.StringToHash("ClawAttack");
    private static readonly int HashBite        = Animator.StringToHash("BiteAttack");
    private static readonly int HashTail        = Animator.StringToHash("TailSweep");
    private static readonly int HashFireBreath  = Animator.StringToHash("FireBreath");
    private static readonly int HashFly         = Animator.StringToHash("IsFlying");
    private static readonly int HashDive        = Animator.StringToHash("Dive");
    private static readonly int HashRoar        = Animator.StringToHash("Roar");
    private static readonly int HashDead        = Animator.StringToHash("Dead");
    private static readonly int HashPhase       = Animator.StringToHash("Phase");

    // ============================================================
    protected override void Awake()
    {
        base.Awake();
        _agent = GetComponent<NavMeshAgent>();
        _anim  = GetComponentInChildren<Animator>();
        _agent.stoppingDistance = clawRange * 0.7f;
        _agent.angularSpeed     = 120f;
        _agent.acceleration     = 8f;
    }

    private void Start()
    {
        var p = FindFirstObjectByType<PlayerController>();
        if (p != null)
        {
            _player      = p.transform;
            _playerStats = p.GetComponent<PlayerStats>();
            _playerCtrl  = p;
        }

        // ボスHPバー表示
        if (bossHealthBar != null)
            bossHealthBar.Init(bossName, maxHP);

        OnHPChanged += (cur, max) => bossHealthBar?.UpdateHP(cur, max);

        _state = BossState.Chase;
        AudioManager.Instance?.PlayDragonRoar();
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead || _isRoaring) return;

        CheckPhaseTransitions();
        TickCooldowns();

        switch (_state)
        {
            case BossState.Chase:
                if (_phase == 2 || _phase == 3) UpdatePhase2Chase();
                else UpdatePhase1Chase();
                break;
            case BossState.Phase1: UpdatePhase1Combat(); break;
            case BossState.Phase2: UpdatePhase2Combat(); break;
            case BossState.Phase3: UpdatePhase3Combat(); break;
        }

        SyncAnimator();
    }

    // ============================================================
    // Phase Transitions
    // ============================================================
    private void CheckPhaseTransitions()
    {
        float ratio = CurrentHP / maxHP;

        if (!_phase2Triggered && ratio <= phase2Threshold)
        {
            _phase2Triggered = true;
            StartCoroutine(TransitionToPhase2());
        }
        else if (!_phase3Triggered && ratio <= phase3Threshold)
        {
            _phase3Triggered = true;
            StartCoroutine(TransitionToPhase3());
        }
    }

    private IEnumerator TransitionToPhase2()
    {
        _state    = BossState.Transitioning;
        _isRoaring = true;
        _agent.isStopped = true;

        _anim?.SetTrigger(HashRoar);
        AudioManager.Instance?.PlayDragonRoar();

        if (phaseTransitionVFX != null)
        {
            var vfx = Instantiate(phaseTransitionVFX, transform.position, Quaternion.identity);
            Destroy(vfx, 4f);
        }

        // カメラシェイク（UI経由）
        UIManager.Instance?.TriggerScreenShake(0.8f, 0.5f);

        yield return new WaitForSeconds(3f);

        _phase     = 2;
        _isFlying  = true;
        _state     = BossState.Phase2;
        _isRoaring = false;
        _agent.speed = groundChaseSpeed * 1.3f;
        _anim?.SetInteger(HashPhase, 2);
        _anim?.SetBool(HashFly, true);
    }

    private IEnumerator TransitionToPhase3()
    {
        _state     = BossState.Transitioning;
        _isRoaring = true;
        _agent.isStopped = true;

        _anim?.SetTrigger(HashRoar);
        AudioManager.Instance?.PlayDragonRoar();

        UIManager.Instance?.TriggerScreenShake(1.2f, 0.8f);

        if (enrageAuraVFX != null)
        {
            Instantiate(enrageAuraVFX, transform);
        }
        if (fireAura != null) fireAura.Play();

        yield return new WaitForSeconds(2.5f);

        _phase     = 3;
        _isFlying  = false;
        _state     = BossState.Phase3;
        _isRoaring = false;
        _agent.speed = groundChaseSpeed + phase3SpeedBonus;
        _anim?.SetInteger(HashPhase, 3);
        _anim?.SetBool(HashFly, false);
    }

    // ============================================================
    // Phase 1 - 地上戦
    // ============================================================
    private void UpdatePhase1Chase()
    {
        if (_player == null) return;
        _agent.SetDestination(_player.position);

        float dist = DistToPlayer();
        if (dist <= clawRange) _state = BossState.Phase1;
    }

    private void UpdatePhase1Combat()
    {
        if (_player == null) return;
        float dist = DistToPlayer();
        FacePlayer();

        if (dist > clawRange * 1.5f) { _state = BossState.Chase; return; }

        // 攻撃選択
        if (_clawTimer <= 0f && dist <= clawRange)
        {
            StartCoroutine(ClawAttack());
        }
        else if (_biteTimer <= 0f && dist <= biteRange)
        {
            StartCoroutine(BiteAttack());
        }
        else if (_tailTimer <= 0f && dist <= tailRange)
        {
            StartCoroutine(TailSweep());
        }
        else if (_fireTimer <= 0f && dist <= fireBreathRange && HasLineOfSight())
        {
            StartCoroutine(FireBreath());
        }
    }

    // ============================================================
    // Phase 2 - 空中戦
    // ============================================================
    private void UpdatePhase2Chase()
    {
        // 空中を漂いながら追跡
        if (_player == null) return;
        float dist = DistToPlayer();

        if (dist <= diveRange && _diveTimer <= 0f)
        {
            StartCoroutine(DiveAttack());
        }
        else if (dist <= fireBreathRange && _fireTimer <= 0f)
        {
            StartCoroutine(AerialFireBreath());
        }
    }

    private void UpdatePhase2Combat()
    {
        UpdatePhase2Chase(); // Phase2はChaseとCombatが一体
    }

    // ============================================================
    // Phase 3 - 狂乱
    // ============================================================
    private void UpdatePhase3Combat()
    {
        if (_player == null) return;
        float dist = DistToPlayer();
        FacePlayer();
        _agent.SetDestination(_player.position);

        // 全攻撃が並列発動（乱打）
        if (dist <= clawRange && _clawTimer <= 0f)
            StartCoroutine(ClawAttack());
        if (dist <= tailRange && _tailTimer <= 0f)
            StartCoroutine(TailSweep());
        if (dist <= fireBreathRange && _fireTimer <= 0f)
            StartCoroutine(FireBreath());
    }

    // ============================================================
    // Attack Coroutines
    // ============================================================
    private IEnumerator ClawAttack()
    {
        _clawTimer = clawCooldown * (_phase == 3 ? 0.6f : 1f);
        _agent.isStopped = true;
        _anim?.SetTrigger(HashClaw);
        yield return new WaitForSeconds(0.3f);
        HitInRadius(clawRange, clawDamage * (_phase == 3 ? enrageDamageBonus : 1f));
        yield return new WaitForSeconds(0.5f);
        _agent.isStopped = false;
    }

    private IEnumerator BiteAttack()
    {
        _biteTimer = biteCooldown;
        _agent.isStopped = true;
        _anim?.SetTrigger(HashBite);

        // プレイヤーへ素早く飛びかかる
        yield return StartCoroutine(LungeToward(_player.position, 0.25f));
        HitInRadius(biteRange * 1.1f, biteDamage);
        yield return new WaitForSeconds(0.6f);
        _agent.isStopped = false;
    }

    private IEnumerator TailSweep()
    {
        _tailTimer = tailCooldown * (_phase == 3 ? 0.7f : 1f);
        _anim?.SetTrigger(HashTail);
        yield return new WaitForSeconds(0.4f);
        // 後方・側方を広域ヒット
        HitInRadius(tailRange, tailDamage, true);
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator FireBreath()
    {
        _fireTimer = fireBreathCooldown * (_phase == 3 ? 0.7f : 1f);
        _agent.isStopped = true;
        _anim?.SetTrigger(HashFireBreath);

        GameObject vfxInst = null;
        if (fireBreathVFX != null)
        {
            vfxInst = Instantiate(fireBreathVFX,
                transform.position + transform.forward * 2f + Vector3.up * 1.5f,
                transform.rotation);
        }

        float dur = _phase == 3 ? 3f : 2f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            // コーン状に継続ダメージ
            if (_player != null && IsInFireCone())
            {
                bool invincible = _playerCtrl != null && _playerCtrl.IsInvincible;
                if (!invincible) _playerStats?.TakeDamage(fireBreathDamage * Time.deltaTime * 10f);
            }
            AudioManager.Instance?.PlayFireBreath(transform.position);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (vfxInst != null) Destroy(vfxInst);
        _agent.isStopped = false;
    }

    private IEnumerator DiveAttack()
    {
        _diveTimer = diveCooldown;
        _anim?.SetTrigger(HashDive);

        // 急降下
        Vector3 start = transform.position + Vector3.up * 5f;
        Vector3 end   = _player != null ? _player.position : transform.position;
        float dur = 0.5f, t = 0f;

        while (t < dur)
        {
            transform.position = Vector3.Lerp(start, end, t / dur);
            t += Time.deltaTime;
            yield return null;
        }

        // 着地インパクト
        if (diveImpactVFX != null) Instantiate(diveImpactVFX, transform.position, Quaternion.identity);
        UIManager.Instance?.TriggerScreenShake(1.0f, 0.4f);
        HitInRadius(5f, diveDamage * (_phase == 3 ? enrageDamageBonus : 1f));
        AudioManager.Instance?.PlayDragonRoar();

        yield return new WaitForSeconds(1f);
    }

    private IEnumerator AerialFireBreath()
    {
        _fireTimer = fireBreathCooldown;
        yield return StartCoroutine(FireBreath());
    }

    // ============================================================
    // Helpers
    // ============================================================
    private void HitInRadius(float radius, float damage, bool isTail = false)
    {
        if (_playerStats == null || _playerCtrl == null) return;
        if (_playerCtrl.IsInvincible) return;

        float dist = DistToPlayer();
        if (dist > radius) return;

        var combat = _player.GetComponent<PlayerCombat>();
        if (combat != null && combat.IsBlocking)
        {
            combat.NotifyBlocked(damage);
            _playerStats.TakeDamage(damage * 0.15f);
        }
        else
        {
            _playerStats.TakeDamage(damage);
            AudioManager.Instance?.PlayPlayerDamage();
        }
    }

    private bool IsInFireCone()
    {
        if (_player == null) return false;
        float dist = DistToPlayer();
        if (dist > fireBreathRange) return false;
        float angle = Vector3.Angle(transform.forward, (_player.position - transform.position).normalized);
        return angle < 35f;
    }

    private bool HasLineOfSight()
    {
        if (_player == null) return false;
        Vector3 dir = (_player.position - transform.position).normalized;
        return !Physics.Raycast(transform.position + Vector3.up, dir, DistToPlayer(), LayerMask.GetMask("Default"));
    }

    private IEnumerator LungeToward(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        float t = 0f;
        while (t < duration)
        {
            transform.position = Vector3.Lerp(start, target, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void FacePlayer()
    {
        if (_player == null) return;
        Vector3 dir = (_player.position - transform.position).WithY(0f);
        if (dir == Vector3.zero) return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
    }

    private float DistToPlayer() =>
        _player != null ? Vector3.Distance(transform.position, _player.position) : float.MaxValue;

    private void TickCooldowns()
    {
        float dt = Time.deltaTime;
        _clawTimer  = Mathf.Max(0f, _clawTimer  - dt);
        _biteTimer  = Mathf.Max(0f, _biteTimer  - dt);
        _tailTimer  = Mathf.Max(0f, _tailTimer  - dt);
        _fireTimer  = Mathf.Max(0f, _fireTimer  - dt);
        _diveTimer  = Mathf.Max(0f, _diveTimer  - dt);
    }

    private void SyncAnimator()
    {
        if (_anim == null) return;
        float spd = _agent.enabled ? _agent.velocity.magnitude : 0f;
        _anim.SetFloat(HashSpeed, spd, 0.1f, Time.deltaTime);
        _anim.SetBool(HashFly, _isFlying);
    }

    // ============================================================
    // Death
    // ============================================================
    protected override void OnDeath()
    {
        _state = BossState.Dead;
        _agent.isStopped = true;
        _agent.enabled   = false;
        if (fireAura != null) fireAura.Stop();

        _anim?.SetTrigger(HashDead);
        bossHealthBar?.Hide();

        AudioManager.Instance?.PlayDragonRoar();
        UIManager.Instance?.TriggerScreenShake(2f, 1f);

        GameManager.Instance?.TriggerVictory();
        Destroy(gameObject, 6f);
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, clawRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fireBreathRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, diveRange);
    }
#endif
}
