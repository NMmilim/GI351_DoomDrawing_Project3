using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Rock : MonoBehaviour
{
    [Header("Enemy Collision & Health")]
    [SerializeField] private string enemy_tag = "Enemy";
    [SerializeField] private LayerMask enemy_layers;
    public int damage = 1;
    public int hp;
    public int maxhp = 1;

    [Header("Earth & Gravity Pull")]
    [Tooltip("Reference to Earth. If empty, automatically finds a GameObject named 'Earth' or defaults to (0,0).")]
    [SerializeField] private Transform earthTransform;
    [Tooltip("Radius of Earth where impact occurs.")]
    [SerializeField] private float earthRadius = 2.35f;
    [Tooltip("Gravitational pull strength towards Earth.")]
    [SerializeField] private float gravityStrength = 30f;
    [Tooltip("Distance threshold to prevent excessive gravity force right at Earth's center.")]
    [SerializeField] private float minGravityDistance = 1.5f;
    [Tooltip("Maximum fall speed toward Earth.")]
    [SerializeField] private float terminalVelocity = 7f;

    [Header("Movement & Tumble")]
    [Tooltip("Initial inward push speed toward Earth when the rock spawns.")]
    [SerializeField] private float initialInwardSpeed = 1.5f;
    [Tooltip("Random initial rotational tumble speed in degrees per second.")]
    [SerializeField] private float randomTumbleSpeed = 35f;
    [Tooltip("Damping on sideways (tangential) velocity over time so knocked asteroids always fall back toward Earth.")]
    [SerializeField] private float tangentialDamping = 0.6f;

    [Header("Ammo Drops")]
    [Tooltip("Chance (0 to 1) to drop regular ammo upon destruction.")]
    [Range(0f, 1f)] [SerializeField] private float regularDropChance = 0.4f;
    [Tooltip("Fixed regular ammo amount granted per drop.")]
    [SerializeField] private int regularDropAmount = 30;
    [Tooltip("Optional custom prefab for regular ammo drop.")]
    [SerializeField] private GameObject regularDropPrefab;

    [Tooltip("Chance (0 to 1) to drop ricochet ammo upon destruction.")]
    [Range(0f, 1f)] [SerializeField] private float ricochetDropChance = 0.2f;
    [Tooltip("Fixed ricochet ammo amount granted per drop.")]
    [SerializeField] private int ricochetDropAmount = 5;
    [Tooltip("Optional custom prefab for ricochet ammo drop.")]
    [SerializeField] private GameObject ricochetDropPrefab;

    [Header("Formation Separation & Anti-Collision")]
    [Tooltip("Distance threshold within which nearby asteroids exert a gentle mutual repulsion force to maintain formation spacing.")]
    [SerializeField] private float separationRadius = 2.0f;
    [Tooltip("Strength of the gentle repulsion force between nearby asteroids.")]
    [SerializeField] private float separationForce = 15f;
    [Tooltip("Time in seconds after spawning during which this rock cannot damage other rocks.")]
    [SerializeField] private float spawnDamageGracePeriod = 1.5f;
    [Tooltip("Minimum relative collision speed required to deal asteroid-to-asteroid impact damage. Prevents gentle formation bumping from destroying rocks.")]
    [SerializeField] private float minCollisionDamageSpeed = 2.5f;

    [Header("Death Explosion & Chain Reaction")]
    [Tooltip("Whether this rock explodes when destroyed, damaging and pushing nearby asteroids.")]
    [SerializeField] private bool explodeOnDeath = true;
    [Tooltip("Radius of the death explosion blast.")]
    [SerializeField] private float explosionRadius = 3.0f;
    [Tooltip("Fraction of max health dealt as explosion damage (0.5 = 50% of max health).")]
    [SerializeField] private float explosionDamageFraction = 0.5f;
    [Tooltip("Physical impulse force pushing surrounding asteroids away from the blast.")]
    [SerializeField] private float explosionForce = 12f;
    [Tooltip("Whether explosion hits count toward the player combo counter.")]
    [SerializeField] private bool explosionCountsForCombo = true;
    [Tooltip("Optional visual effect prefab to instantiate at explosion position.")]
    [SerializeField] private GameObject explosionVFXPrefab;

    private Rigidbody2D rb;
    private bool isDead = false;
    private float spawnTime = 0f;
    private static readonly Collider2D[] nearbyColliders = new Collider2D[16];

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        hp = maxhp; // Start at full health
        spawnTime = Time.time;

        if (earthTransform == null)
        {
            GameObject earthObj = GameObject.Find("Earth");
            if (earthObj != null) earthTransform = earthObj.transform;
        }

        Vector2 earthCenter = GetEarthCenter();
        Vector2 toEarth = earthCenter - (Vector2)transform.position;

        // Apply initial inward velocity toward Earth
        if (initialInwardSpeed > 0f && toEarth.sqrMagnitude > 0.01f)
        {
            rb.linearVelocity = toEarth.normalized * initialInwardSpeed;
        }

        // Apply slight random space tumble
        if (randomTumbleSpeed > 0f)
        {
            rb.angularVelocity = Random.Range(-randomTumbleSpeed, randomTumbleSpeed);
        }
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        Vector2 earthCenter = GetEarthCenter();
        Vector2 toEarth = earthCenter - (Vector2)transform.position;
        float dist = toEarth.magnitude;

        // Check if the asteroid has reached Earth's surface
        if (dist <= earthRadius)
        {
            HitEarth();
            return;
        }

        Vector2 dirToEarth = toEarth / dist;

        // Realistic inverse-square gravitational force: F = (G * mass) / r^2
        float effectiveDist = Mathf.Max(dist, minGravityDistance);
        float forceMagnitude = (gravityStrength * rb.mass) / (effectiveDist * effectiveDist);
        Vector2 gravityForce = dirToEarth * forceMagnitude;

        rb.AddForce(gravityForce);

        // Gentle mutual repulsion between asteroids in formation to prevent bunching up
        if (separationForce > 0f && separationRadius > 0f)
        {
            ApplyFlockingSeparation();
        }

        // Decompose velocity into radial (toward/away from Earth) and tangential (sideways orbit)
        Vector2 currentVel = rb.linearVelocity;
        float radialSpeed = Vector2.Dot(currentVel, dirToEarth);
        Vector2 radialVel = dirToEarth * radialSpeed;
        Vector2 tangentialVel = currentVel - radialVel;

        // Damp sideways velocity so knockback doesn't cause eternal elliptical orbits
        if (tangentialDamping > 0f)
        {
            tangentialVel = Vector2.Lerp(tangentialVel, Vector2.zero, tangentialDamping * Time.fixedDeltaTime);
        }

        // Enforce terminal velocity cap when falling toward Earth
        if (radialSpeed > terminalVelocity)
        {
            radialVel = dirToEarth * terminalVelocity;
        }

        rb.linearVelocity = radialVel + tangentialVel;
    }

    private void ApplyFlockingSeparation()
    {
        LayerMask searchMask = enemy_layers.value != 0 ? enemy_layers : (1 << gameObject.layer);
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, separationRadius, nearbyColliders, searchMask);
        Vector2 separationPush = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = nearbyColliders[i];
            if (col == null || col.gameObject == gameObject) continue;

            Vector2 away = (Vector2)transform.position - (Vector2)col.transform.position;
            float dist = away.magnitude;
            if (dist > 0.001f && dist < separationRadius)
            {
                // Push stronger the closer they get to each other
                float factor = 1f - (dist / separationRadius);
                separationPush += (away / dist) * (factor * separationForce);
            }
        }

        if (separationPush.sqrMagnitude > 0.001f)
        {
            rb.AddForce(separationPush);
        }
    }

    // Public so OTHER scripts (bullets, other rocks) can damage this rock
    public void TakeDamage(int amount)
    {
        if (isDead) return;

        hp -= amount;
        Debug.Log($"{name} took {amount} damage, hp now {hp}");

        if (hp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (explodeOnDeath)
        {
            Explode();
        }

        RollAndSpawnDrops();
        Destroy(gameObject);
    }

    private void Explode()
    {
        Vector2 explosionCenter = transform.position;

        // Calculate explosion damage: half of max health (minimum 1 so Weak asteroids can trigger chain reactions)
        int expDamage = Mathf.Max(1, Mathf.RoundToInt(maxhp * explosionDamageFraction));

        // Spawn visual effect if prefab is assigned
        if (explosionVFXPrefab != null)
        {
            Instantiate(explosionVFXPrefab, explosionCenter, Quaternion.identity);
        }

        // Query nearby colliders on enemy layer
        LayerMask searchMask = enemy_layers.value != 0 ? enemy_layers : (1 << gameObject.layer);
        Collider2D[] hits = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius, searchMask);

        // Gather distinct alive target rocks first to prevent multi-collider duplication
        List<Rock> targets = new List<Rock>();
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            Rock targetRock = hit.GetComponent<Rock>();
            if (targetRock != null && !targetRock.isDead && !targets.Contains(targetRock))
            {
                targets.Add(targetRock);
            }
        }

        // Apply physical impact push and damage to each target
        foreach (Rock targetRock in targets)
        {
            if (targetRock == null || targetRock.isDead) continue;

            Vector2 toTarget = (Vector2)targetRock.transform.position - explosionCenter;
            float dist = toTarget.magnitude;
            Vector2 pushDir = dist > 0.001f ? toTarget / dist : UnityEngine.Random.insideUnitCircle.normalized;

            // Physical impulse: stronger push the closer the asteroid is to blast center
            float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);
            Vector2 impulse = pushDir * (explosionForce * Mathf.Lerp(0.5f, 1.2f, falloff));

            Rigidbody2D targetRb = targetRock.GetComponent<Rigidbody2D>();
            if (targetRb != null)
            {
                targetRb.AddForce(impulse, ForceMode2D.Impulse);
            }

            // Register hit on combo if enabled
            if (explosionCountsForCombo)
            {
                ComboManager.RegisterHit();
            }

            // Deal explosion damage (can trigger chain reaction if target reaches 0 HP!)
            targetRock.TakeDamage(expDamage);
        }

        Debug.Log($"[Rock] {name} exploded (Damage: {expDamage}, Radius: {explosionRadius}, Targets: {targets.Count})");
    }

    /// <summary>
    /// Called when the asteroid impacts Earth's surface.
    /// Destroys the asteroid without dropping rewards.
    /// </summary>
    private void HitEarth()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"[Rock] {name} impacted Earth!");
        Destroy(gameObject);
    }

    private void RollAndSpawnDrops()
    {
        bool droppedRegular = false;

        // Roll for Regular Ammo drop (fixed 30 ammo)
        if (Random.value <= regularDropChance)
        {
            AmmoDrop.SpawnDrop(transform.position, AmmoType.Regular, regularDropAmount, regularDropPrefab);
            droppedRegular = true;
        }

        // Roll for Ricochet Ammo drop (fixed 5 ammo)
        if (Random.value <= ricochetDropChance)
        {
            // Slight position offset if both happen to drop simultaneously
            Vector3 offset = droppedRegular ? (Vector3)(Random.insideUnitCircle * 0.4f) : Vector3.zero;
            AmmoDrop.SpawnDrop(transform.position + offset, AmmoType.Ricochet, ricochetDropAmount, ricochetDropPrefab);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if collided with Earth
        if (IsEarth(collision.gameObject))
        {
            HitEarth();
            return;
        }

        // Grace period check: don't damage each other right after spawn
        if (Time.time < spawnTime + spawnDamageGracePeriod)
        {
            return;
        }

        // Only deal impact damage if the collision has significant velocity (e.g. violent knockback from bullet or high-speed slingshot)
        // Gentle formation bumping bounces with physics without dealing damage
        if (collision.relativeVelocity.magnitude < minCollisionDamageSpeed)
        {
            return;
        }

        TryHit(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsEarth(other.gameObject))
        {
            HitEarth();
        }
    }

    private bool IsEarth(GameObject obj)
    {
        if (earthTransform != null && (obj.transform == earthTransform || obj.transform.IsChildOf(earthTransform)))
            return true;

        if (obj.name.IndexOf("Earth", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // obj.tag == "Earth" does not throw "Tag is not defined" error unlike CompareTag
        return obj.tag == "Earth";
    }

    private void TryHit(GameObject target)
    {
        bool tag_match = target.CompareTag(enemy_tag);
        bool layer_match = (enemy_layers.value & (1 << target.layer)) != 0;

        if (tag_match || layer_match)
        {
            // Deal damage to the other asteroid I bumped into
            Rock other = target.GetComponent<Rock>();
            if (other != null) other.TakeDamage(damage);
        }
    }

    public Vector2 GetEarthCenter()
    {
        return earthTransform != null ? (Vector2)earthTransform.position : Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        if (explodeOnDeath)
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
