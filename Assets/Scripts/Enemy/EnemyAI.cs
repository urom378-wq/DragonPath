using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// 汎用敵AI。Idle → Patrol → Chase → Attack → Stagger → Dead
/// NavMesh Agent + 視野角 + 複数攻撃パターン
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(CharacterController))]
public class EnemyAI : EnemyBase
{
    // ============================================================
    // Inspector
    // ============================================================
    [Header("Detection")]
    public float detectionRange = 12f;
    public float fieldOfView    = 120f;   // 視野角（度）
    public float loseRange      = 20f;    // 追跡を諦める距離

    [Header("Patrol")]
    public Transform[] patrolPoints;
    public float patrolWaitTime = 2f;

    [Header("Combat")]
    public float attackRange    = 2.0f;
    public float attackDamage   = 12f;
    public float attackCooldown = 1.8f;
    public float chaseSpeed     = 4.5f;
    public float patrolSpeed    = 2.0f;

    [Header("Stagger")]
    public float staggerThreshold = 30f;  // この量のダメージで怯む
    public float staggerDuration  = 0.8f;

    // ============================================================
    // State Machine
    // ============================================================
    private enum State { Idle, Patrol, Chase, Attack, Stagger, Dead }
    private State _state = State.Idle;

    private NavMeshAgent   _agent;
    private Animator       _anim;
    private Transform      _player;
    private PlayerController _playerCtrl;

    private int   _patrolIndex  = 0;
    private float _patrolTimer  = 0f;
    private float _atkTimer     = 0f;
    private float _staggerTimer = 0f;
    private float _damageSinceLastStagger = 0f;

    // --- Animator Hashes ---
    private static readonly int HashSpeed    = Animator.StringToHash("Speed");
    private static readonly int HashAttack   = Animator.StringToHash("Attack");
    private static readonly int HashStagger  = Animator.StringToHash("Stagger");
    private static readonly int HashDead     = Animator.StringToHash("Dead");

    // ============================================================
    protected override void Awake()
    {
        base.Awake();
        _agent = GetComponent<NavMeshAgent>();
        _anim  = GetComponentInChildren<Animator>();

        _agent.speed        = patrolSpeed;
        _agent.stoppingDistance = attackRange * 0.8f;
        _agent.angularSpeed = 200f;
        _agent.acceleration = 12f;
        _agent.autoBraking  = true;
    }

    private void Start()
    {
        var p = FindFirstObjectByType<PlayerController>();
        if (p != null)
        {
            _player     = p.transform;
            _playerCtrl = p;
        }

        if (patrolPoints != null && patrolPoints.Length > 0)
            SetState(State.Patrol);
        else
            SetState(State.Idle);
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead) return;

        _atkTimer     = Mathf.Max(0f, _atkTimer     - Time.deltaTime);
        _staggerTimer = Mathf.Max(0f, _staggerTimer - Time.deltaTime);

        switch (_state)
        {
            case State.Idle:    UpdateIdle();    break;
            case State.Patrol:  UpdatePatrol();  break;
            case State.Chase:   UpdateChase();   break;
            case State.Attack:  UpdateAttack();  break;
            case State.Stagger: UpdateStagger(); break;
        }

        SyncAnimator();
    }

    // ============================================================
    // State Updates
    // ============================================================
    private void UpdateIdle()
    {
        _patrolTimer += Time.deltaTime;
        if (_patrolTimer >= patrolWaitTime)
        {
            _patrolTimer = 0f;
            if (patrolPoints != null && patrolPoints.Length > 0)
                SetState(State.Patrol);
        }
        if (CanSeePlayer()) SetState(State.Chase);
    }

    private void UpdatePatrol()
    {
        if (CanSeePlayer()) { SetState(State.Chase); return; }
        if (patrolPoints == null || patrolPoints.Length == 0) { SetState(State.Idle); return; }

        _agent.SetDestination(patrolPoints[_patrolIndex].position);

        if (!_agent.pathPending && _agent.remainingDistance < 0.5f)
        {
            _patrolTimer += Time.deltaTime;
            if (_patrolTimer >= patrolWaitTime)
            {
                _patrolTimer = 0f;
                _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
            }
        }
    }

    private void UpdateChase()
    {
        if (_player == null) { SetState(State.Idle); return; }

        float dist = Vector3.Distance(transform.position, _player.position);

        if (dist > loseRange)                  { SetState(State.Patrol); return; }
        if (dist <= attackRange)               { SetState(State.Attack); return; }

        _agent.speed = chaseSpeed;
        _agent.SetDestination(_player.position);
    }

    private void UpdateAttack()
    {
        if (_player == null) { SetState(State.Idle); return; }

        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist > attackRange * 1.3f) { SetState(State.Chase); return; }

        _agent.SetDestination(transform.position); // 停止
        FacePlayer();

        if (_atkTimer <= 0f)
        {
            PerformAttack();
        }
    }

    private void UpdateStagger()
    {
        if (_staggerTimer <= 0f)
            SetState(State.Chase);
    }

    // ============================================================
    // Attack
    // ============================================================
    private void PerformAttack()
    {
        _atkTimer = attackCooldown;
        _anim?.SetTrigger(HashAttack);
        StartCoroutine(AttackHitDelay());
    }

    private IEnumerator AttackHitDelay()
    {
        yield return new WaitForSeconds(0.35f);

        if (_player == null) yield break;
        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist > attackRange + 0.5f) yield break;

        var stats = _player.GetComponent<PlayerStats>();
        var ctrl  = _player.GetComponent<PlayerController>();
        var combat= _player.GetComponent<PlayerCombat>();

        // 無敵・ブロック確認
        if (ctrl != null && ctrl.IsInvincible) yield break;

        if (combat != null && combat.IsBlocking)
        {
            combat.NotifyBlocked(attackDamage);
            // ブロック成功時もわずかにダメージ
            stats?.TakeDamage(attackDamage * 0.2f);
        }
        else
        {
            stats?.TakeDamage(attackDamage);
            AudioManager.Instance?.PlayPlayerDamage();
        }
    }

    // ============================================================
    // Damage Override
    // ============================================================
    public override void TakeDamage(float damage, Vector3 attackerPos)
    {
        base.TakeDamage(damage, attackerPos);
        if (IsDead) return;

        _damageSinceLastStagger += damage;
        if (_damageSinceLastStagger >= staggerThreshold)
        {
            _damageSinceLastStagger = 0f;
            TriggerStagger();
        }

        // 気づき
        if (_state == State.Idle || _state == State.Patrol)
            SetState(State.Chase);
    }

    private void TriggerStagger()
    {
        SetState(State.Stagger);
        _staggerTimer = staggerDuration;
        _anim?.SetTrigger(HashStagger);
        _agent.ResetPath();
    }

    // ============================================================
    // Detection
    // ============================================================
    private bool CanSeePlayer()
    {
        if (_player == null) return false;
        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist > detectionRange) return false;

        Vector3 dir = (_player.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, dir) > fieldOfView * 0.5f) return false;

        // 視線チェック（壁越し見えない）
        if (Physics.Raycast(transform.position + Vector3.up, dir, dist, LayerMask.GetMask("Default")))
            return false;

        return true;
    }

    // ============================================================
    // State Transition
    // ============================================================
    private void SetState(State next)
    {
        _state = next;
        switch (next)
        {
            case State.Patrol:
                _agent.speed = patrolSpeed;
                _agent.isStopped = false;
                break;
            case State.Chase:
                _agent.speed = chaseSpeed;
                _agent.isStopped = false;
                break;
            case State.Attack:
            case State.Stagger:
                _agent.isStopped = true;
                _agent.ResetPath();
                break;
        }
    }

    // ============================================================
    // Death
    // ============================================================
    protected override void OnDeath()
    {
        _state = State.Dead;
        _agent.isStopped = true;
        _agent.enabled   = false;

        _anim?.SetTrigger(HashDead);

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Destroy(gameObject, 4f);
    }

    // ============================================================
    // Helpers
    // ============================================================
    private void FacePlayer()
    {
        if (_player == null) return;
        Vector3 dir = (_player.position - transform.position).WithY(0f);
        if (dir == Vector3.zero) return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
    }

    private void SyncAnimator()
    {
        if (_anim == null) return;
        float spd = _agent.enabled ? _agent.velocity.magnitude : 0f;
        _anim.SetFloat(HashSpeed, spd, 0.1f, Time.deltaTime);
    }

    // ============================================================
    // Gizmos
    // ============================================================
#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}
