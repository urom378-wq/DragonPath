using UnityEngine;
using System;

/// <summary>
/// 全敵の基底クラス。ダメージ処理・死亡・ドロップ・ノックバックを共通化。
/// </summary>
public abstract class EnemyBase : MonoBehaviour
{
    // ============================================================
    // Inspector
    // ============================================================
    [Header("Stats")]
    public float maxHP          = 100f;
    public float defense        = 5f;
    public int   xpReward       = 50;
    public int   goldMin        = 5;
    public int   goldMax        = 20;

    [Header("Drop")]
    public ItemData[] dropItems;
    [Range(0f, 1f)]
    public float dropChance     = 0.3f;

    [Header("Knockback")]
    public float knockbackForce = 3f;
    public float knockbackDecay = 8f;

    [Header("VFX")]
    public GameObject hitVFXPrefab;
    public GameObject deathVFXPrefab;
    public GameObject dropItemPrefab;  // ワールドドロップ用プレハブ

    // ============================================================
    // State
    // ============================================================
    public float CurrentHP { get; protected set; }
    public bool  IsDead    { get; protected set; }

    protected Vector3 _knockbackVelocity;
    protected CharacterController _cc;

    // ============================================================
    // Events
    // ============================================================
    public event Action<float, float> OnHPChanged;   // current, max
    public event Action               OnDied;

    // ============================================================
    protected virtual void Awake()
    {
        _cc        = GetComponent<CharacterController>();
        CurrentHP  = maxHP;
    }

    protected virtual void Update()
    {
        ApplyKnockback();
    }

    // ============================================================
    // Damage
    // ============================================================
    public virtual void TakeDamage(float damage, Vector3 attackerPosition)
    {
        if (IsDead) return;

        float finalDmg = Mathf.Max(1f, damage - defense);
        CurrentHP      = Mathf.Max(0f, CurrentHP - finalDmg);

        OnHPChanged?.Invoke(CurrentHP, maxHP);

        // ダメージ数値の表示
        DamageNumber.Spawn(transform.position + Vector3.up * 2f, finalDmg, false);

        // ヒット VFX
        if (hitVFXPrefab != null)
        {
            var vfx = Instantiate(hitVFXPrefab, transform.position + Vector3.up, Quaternion.identity);
            Destroy(vfx, 1.5f);
        }

        // ノックバック
        Vector3 dir = (transform.position - attackerPosition).WithY(0f).normalized;
        _knockbackVelocity = dir * knockbackForce;

        AudioManager.Instance?.PlayHit();

        if (CurrentHP <= 0f) Die();
        else OnDamageTaken(finalDmg, attackerPosition);
    }

    // ============================================================
    // Death
    // ============================================================
    protected virtual void Die()
    {
        if (IsDead) return;
        IsDead = true;

        // 経験値・リワード
        var player = FindAnyObjectByType<PlayerStats>();
        if (player != null)
        {
            player.AddExperience(xpReward);
        }

        // アイテムドロップ
        if (UnityEngine.Random.value < dropChance && dropItemPrefab != null && dropItems != null && dropItems.Length > 0)
        {
            var item = dropItems[UnityEngine.Random.Range(0, dropItems.Length)];
            var go   = Instantiate(dropItemPrefab,
                transform.position + Vector3.up * 0.5f + UnityEngine.Random.insideUnitSphere * 0.5f,
                Quaternion.identity);
            go.GetComponent<ItemPickup>()?.Init(item);
        }

        // 死亡 VFX
        if (deathVFXPrefab != null)
        {
            var vfx = Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        GameManager.Instance?.NotifyEnemyKilled();
        OnDied?.Invoke();
        OnDeath();
    }

    // ============================================================
    // Knockback
    // ============================================================
    private void ApplyKnockback()
    {
        if (_knockbackVelocity.magnitude < 0.05f) { _knockbackVelocity = Vector3.zero; return; }
        if (_cc != null)
            _cc.Move(_knockbackVelocity * Time.deltaTime);
        _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);
    }

    // ============================================================
    // Virtual Methods (サブクラスで実装)
    // ============================================================
    protected virtual void OnDamageTaken(float damage, Vector3 attackerPos) { }
    protected abstract void OnDeath();

    // ============================================================
    // Gizmos
    // ============================================================
#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
#endif
}
