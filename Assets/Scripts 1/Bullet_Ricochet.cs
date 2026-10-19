using System.Collections.Generic;
using UnityEngine;

// Bullet that jumps from enemy to enemy. Each enemy hit costs 1 hp; the bullet dies at 0 hp.
// If no other enemy is in range, it bounces off the hit enemy like a real bullet (angle in = angle out).
public class Bullet_Ricochet : Bullet
{
    [Header("Ricochet")]
    [SerializeField] private int maxhp = 3;
    [Tooltip("Only objects on these layers can be chosen as the next ricochet target. Defaults to the 'Enemy' layer if left as Nothing.")]
    [SerializeField] private LayerMask target_layers;
    [Tooltip("How far (in units) from the HIT ENEMY the bullet looks for the next enemy.")]
    [SerializeField] private float search_radius = 10f;
    [Tooltip("If off, the bullet never hits the same enemy twice (chain-lightning style).\nIf on, it can bounce back and forth between two enemies.")]
    [SerializeField] private bool can_hit_same_enemy_twice = false;

    [Header("Rotation")]
    [Tooltip("Z rotation at which the sprite faces right (+X). Your sprite faces forward at -90.")]
    [SerializeField] private float sprite_angle_offset = -90f;
    [Tooltip("How fast (degrees per second) the bullet turns to face its new direction. 0 = snap instantly.")]
    [SerializeField] private float turn_speed = 900f;

    public int hp;

    private readonly HashSet<GameObject> hit_enemies = new HashSet<GameObject>();
    private GameObject last_hit;
    private Vector2 last_velocity;   // velocity BEFORE the physics engine changes it on impact

    protected override void Start()
    {
        base.Start();   // sets velocity + lifetime from Bullet
        hp = maxhp;

        if (target_layers.value == 0) target_layers = LayerMask.GetMask("Enemy");
        if (target_layers.value == 0) Debug.LogWarning("No 'Enemy' layer found. Create it in Tags & Layers or set Target Layers.", this);

        rb.rotation = VelocityAngle(rb.linearVelocity);   // face travel direction right away on spawn
    }

    private void FixedUpdate()
    {
        last_velocity = rb.linearVelocity;
        RotateTowardsVelocity();
    }

    // Angle the sprite should have to face along 'velocity'
    private float VelocityAngle(Vector2 velocity)
    {
        return Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + sprite_angle_offset;
    }

    private void RotateTowardsVelocity()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.0001f) return;

        float target = VelocityAngle(rb.linearVelocity);
        float next = turn_speed <= 0f
            ? target
            : Mathf.MoveTowardsAngle(rb.rotation, target, turn_speed * Time.fixedDeltaTime);

        rb.angularVelocity = 0f;   // stop collisions from adding random spin
        rb.MoveRotation(next);     // smooth turn (works with Rigidbody2D interpolation)
    }

    // Solid collider: real contact normal available
    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject, collision.GetContact(0).normal);
    }

    // Trigger collider: no contact info, so estimate the surface normal
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        Vector2 pos = transform.position;
        Vector2 normal = pos - other.ClosestPoint(pos);
        if (normal.sqrMagnitude < 0.0001f) normal = -last_velocity;   // bullet center already inside: bounce straight back
        HandleHit(other.gameObject, normal.normalized);
    }

    private void HandleHit(GameObject target, Vector2 surface_normal)
    {
        AmmoDrop drop = target.GetComponent<AmmoDrop>();
        if (drop != null)
        {
            drop.Collect();
            return;
        }

        if (!IsEnemy(target)) return;      // walls etc. are left to normal physics
        if (target == last_hit) return;    // ignore touching the enemy we just ricocheted off

        Rock rock = target.GetComponent<Rock>();
        if (rock != null)
        {
            rock.TakeDamage(damage);
            ComboManager.RegisterHit();
        }

        last_hit = target;
        hit_enemies.Add(target);

        hp--;
        if (hp <= 0)
        {
            Destroy(gameObject);
            return;
        }

        // Search from the HIT ENEMY's position, not the bullet's
        Transform next = FindNextTarget(target.transform.position);
        if (next != null)
            RicochetTowards(next.position);
        else
            BounceOff(surface_normal);     // no other enemy: physical ricochet
    }

    private Transform FindNextTarget(Vector2 from)
    {
        Transform best = null;
        float best_dist = float.MaxValue;

        // Layer mask filter = only Enemy-layer colliders are considered
        foreach (Collider2D col in Physics2D.OverlapCircleAll(from, search_radius, target_layers))
        {
            GameObject candidate = col.gameObject;
            if (candidate == last_hit) continue;
            if (!can_hit_same_enemy_twice && hit_enemies.Contains(candidate)) continue;

            float dist = ((Vector2)candidate.transform.position - from).sqrMagnitude;
            if (dist < best_dist)
            {
                best_dist = dist;
                best = candidate.transform;
            }
        }
        return best;
    }

    private void RicochetTowards(Vector2 target_pos)
    {
        SetDirection((target_pos - (Vector2)transform.position).normalized);
    }

    private void BounceOff(Vector2 surface_normal)
    {
        Vector2 incoming = last_velocity.sqrMagnitude > 0.0001f ? last_velocity : -surface_normal;
        // Law of reflection: angle of incidence = angle of reflection
        SetDirection(Vector2.Reflect(incoming, surface_normal).normalized);
    }

    private void SetDirection(Vector2 dir)
    {
        rb.linearVelocity = dir * speed;
        last_velocity = rb.linearVelocity;
        // rotation is handled smoothly in RotateTowardsVelocity()
        life_timer = lifetime;       // refresh lifetime so long chains don't expire mid-flight
    }

    // Shows the search radius in the Scene view when the bullet is selected
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, search_radius);
    }
}
