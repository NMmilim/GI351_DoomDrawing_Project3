using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Heavy Penetration Weapon projectile (Railgun).
/// High-velocity beam/slug with Pierce = 1 (strikes up to 2 targets in a line).
/// Directly penetrates and destroys Tanky and Magnet meteors (isExplosionOrPiercing = true).
/// Cost: 25 resources per shot.
/// </summary>
public class RailgunBullet : Bullet
{
    [Header("Railgun Penetration Settings")]
    [Tooltip("Number of additional enemies this projectile can pierce through before destroying itself (Pierce = 1 means 2 targets hit total).")]
    [SerializeField] private int maxPierceCount = 1;
    [Tooltip("Heavy physical impulse knockback applied to struck targets.")]
    [SerializeField] private float knockbackForce = 18f;

    private int remainingPierces;
    private readonly HashSet<Collider2D> hitColliders = new HashSet<Collider2D>();
    private TrailRenderer trail;

    protected override void Start()
    {
        // Railgun defaults: high speed and high damage
        if (speed <= 15f) speed = 36f;
        if (damage <= 1) damage = 3;

        remainingPierces = maxPierceCount;

        base.Start();

        // Setup sleek electric cyan trail if not present
        trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
            Shader s = Shader.Find("Sprites/Default");
            if (s != null) trail.material = new Material(s);
            trail.startColor = new Color(0.15f, 0.95f, 1f, 0.95f);
            trail.endColor = new Color(0.1f, 0.4f, 1f, 0f);
            trail.startWidth = 0.22f;
            trail.endWidth = 0.02f;
            trail.time = 0.25f;
        }

        // Tint sprite electric cyan if using a standard sprite
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(0.3f, 0.95f, 1f, 1f);
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (CheckAmmoDrop(other.gameObject)) return;
        if (CheckDisposableMine(other.gameObject)) return;

        if (hitColliders.Contains(other)) return;
        hitColliders.Add(other);

        Rock rock = other.gameObject.GetComponent<Rock>();
        if (rock != null)
        {
            // Armor-bypassing trait: passes isExplosionOrPiercing = true!
            // Tanky and Magnet meteors CANNOT resist the Railgun!
            ComboManager.RegisterHit();
            rock.TakeDamage(damage, isExplosionOrPiercing: true);

            // Apply heavy physical knockback
            rock.Push(transform.right * knockbackForce);

            HandlePierce();
        }
        else if (IsEnemy(other.gameObject))
        {
            HandlePierce();
        }
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (CheckAmmoDrop(collision.gameObject)) return;
        if (CheckDisposableMine(collision.gameObject)) return;

        if (hitColliders.Contains(collision.collider)) return;
        hitColliders.Add(collision.collider);

        Rock rock = collision.gameObject.GetComponent<Rock>();
        if (rock != null)
        {
            ComboManager.RegisterHit();
            rock.TakeDamage(damage, isExplosionOrPiercing: true);
            rock.Push(transform.right * knockbackForce);

            HandlePierce();
        }
        else if (IsEnemy(collision.gameObject))
        {
            HandlePierce();
        }
    }

    private void HandlePierce()
    {
        remainingPierces--;
        if (remainingPierces < 0)
        {
            Destroy(gameObject);
        }
    }

    protected override bool CheckDisposableMine(GameObject target)
    {
        DisposableMine mine = target.GetComponent<DisposableMine>();
        if (mine != null && !mine.IsDetonated)
        {
            mine.Detonate();
            HandlePierce();
            return true;
        }
        return false;
    }
}
