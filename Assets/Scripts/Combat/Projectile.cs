using UnityEngine;

/// <summary>
/// 飛翔弾（プレイヤーのスキル・ドラゴンの炎弾など）
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed      = 18f;
    public float lifetime   = 4f;

    [Header("VFX")]
    public GameObject hitVFXPrefab;

    private float     _damage;
    private float     _range;
    private GameObject _owner;
    private Rigidbody _rb;
    private bool      _hasHit;
    private float     _traveledDist;

    // ============================================================
    public void Init(float damage, float range, GameObject owner)
    {
        _damage = damage;
        _range  = range;
        _owner  = owner;
        _rb     = GetComponent<Rigidbody>();
        _rb.linearVelocity   = transform.forward * speed;
        _rb.useGravity = false;
        _rb.isKinematic = false;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        _traveledDist += speed * Time.deltaTime;
        if (_traveledDist >= _range) Impact(transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (_owner != null && other.gameObject == _owner) return;
        if (other.CompareTag("Projectile")) return;

        // プレイヤー → 敵へのダメージ
        var enemy = other.GetComponentInParent<EnemyBase>();
        if (enemy != null && !enemy.IsDead)
        {
            enemy.TakeDamage(_damage, transform.position);
            Impact(other.ClosestPoint(transform.position));
            return;
        }

        // ドラゴン → プレイヤーへのダメージ
        var playerStats = other.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            var ctrl   = other.GetComponent<PlayerController>();
            var combat = other.GetComponent<PlayerCombat>();

            if (ctrl != null && ctrl.IsInvincible) { Impact(transform.position); return; }

            if (combat != null && combat.IsBlocking)
            {
                combat.NotifyBlocked(_damage);
                playerStats.TakeDamage(_damage * 0.15f);
            }
            else
            {
                playerStats.TakeDamage(_damage);
                AudioManager.Instance?.PlayPlayerDamage();
            }
            Impact(other.ClosestPoint(transform.position));
            return;
        }

        // 壁など
        Impact(transform.position);
    }

    private void Impact(Vector3 pos)
    {
        if (_hasHit) return;
        _hasHit = true;

        if (hitVFXPrefab != null)
        {
            var vfx = Instantiate(hitVFXPrefab, pos, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        Destroy(gameObject);
    }
}
