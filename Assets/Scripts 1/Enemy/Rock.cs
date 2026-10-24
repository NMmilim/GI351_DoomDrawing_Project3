using System.Collections.Generic;
using UnityEngine;

public enum MeteorType
{
    Normal,
    Speedy,
    Tanky,
    Magnet
}

[RequireComponent(typeof(Rigidbody2D))]
public class Rock : MonoBehaviour
{
    [Header("Meteor Type & Specifications")]
    public MeteorType meteorType = MeteorType.Normal;
    [Tooltip("If true, retains the custom inspector values on this component/prefab instead of overwriting them with default presets.")]
    public bool useCustomStats = false;
    [Tooltip("If true, automatically adjusts size, health, speed, and drop parameters based on meteorType on Start (only if useCustomStats is false).")]
    [SerializeField] private bool autoConfigureStatsFromType = true;

    [Header("Combat & Vulnerability (Inspector Editable)")]
    [Tooltip("Immune to standard Laser Cannon fire. Can only be destroyed by explosions or piercing weapons.")]
    public bool immuneToNormalFire = false;
    [Tooltip("Flat base resources earned on 1st destruction.")]
    public int baseResourceDrop = 7;
    [Tooltip("Base chain cost added in subsequent chain combo destructions.")]
    public int baseChainCost = 5;
    [Tooltip("Multiplier for blast radius or vortex radius relative to body size.")]
    public float blastRadiusMultiplier = 2.5f;
    [Tooltip("Inward gravitational pull impulse for Magnet meteor vortex.")]
    public float vortexPullForce = 22f;

    [Header("Threat Rating & Earth Impact (Inspector Editable)")]
    [Tooltip("Threat value rating of this meteor (Normal: 1, Speedy: 1.5, Tanky: 2, Magnet: 3).")]
    public float threatValue = 1.0f;
    [Tooltip("Amount of resources drained from player's pool when this asteroid impacts Earth.")]
    public int earthImpactDrain = 20;

    [Header("Enemy Collision & Health")]
    [SerializeField] private string enemy_tag = "Enemy";
    [SerializeField] private LayerMask enemy_layers;
    public int damage = 1;
    public int hp = 1;
    public int maxhp = 1;

    [Header("Visual Sprites & Variants")]
    [Tooltip("If true, preserves any existing sprite assigned to this prefab's SpriteRenderer rather than overriding it with spawner sprites.")]
    public bool retainExistingSprite = true;
    [Tooltip("Optional custom sprites for this meteor prefab. If assigned, one is randomly selected on start.")]
    [SerializeField] private Sprite[] customSprites;

    [Header("Earth & Gravity Pull System (Injected by Spawner)")]
    [Tooltip("Reference to Earth. Injected by AsteroidSpawner.")]
    [SerializeField] private Transform earthTransform;
    [Tooltip("Radius of Earth where impact occurs.")]
    [SerializeField] private float earthRadius = 2.35f;
    [Tooltip("Standard gravitational parameter G * M for Earth. Gravity a = (earthGravityGM / r^2). Injected by AsteroidSpawner.")]
    [SerializeField] private float earthGravityGM = 260f;
    [Tooltip("Minimum distance clamp for 1/r^2 gravity to prevent infinite acceleration near center.")]
    [SerializeField] private float minGravityDistance = 2.35f;
    [Tooltip("Deceleration rate on outward velocity when pushed away, ensuring the asteroid stops and returns like an apple thrown in the sky.")]
    [SerializeField] private float outwardBrakeRate = 10f;
    [Tooltip("Maximum fall speed toward Earth (0 = uncapped). Injected by AsteroidSpawner.")]
    public float terminalVelocity = 12f;
    [Tooltip("Multiplier for gravitational acceleration on this rock.")]
    public float gravityMultiplier = 1.0f;

    [Header("Movement & Tumble (Injected by Spawner)")]
    [Tooltip("Initial inward push speed toward Earth when the rock spawns.")]
    public float initialInwardSpeed = 1.5f;
    [Tooltip("Random initial rotational tumble speed in degrees per second.")]
    [SerializeField] private float randomTumbleSpeed = 35f;
    [Tooltip("Damping on sideways (tangential) velocity over time so knocked asteroids always fall back toward Earth.")]
    [SerializeField] private float tangentialDamping = 1.0f;

    [Header("Formation Separation & Anti-Collision (Injected by Spawner)")]
    [Tooltip("Distance threshold within which nearby asteroids exert a gentle mutual repulsion force to maintain formation spacing.")]
    [SerializeField] private float separationRadius = 2.0f;
    [Tooltip("Strength of the gentle repulsion force between nearby asteroids.")]
    [SerializeField] private float separationForce = 15f;
    [Tooltip("Time in seconds after spawning during which this rock cannot damage other rocks.")]
    [SerializeField] private float spawnDamageGracePeriod = 1.5f;
    [Tooltip("Minimum relative collision speed required to deal asteroid-to-asteroid impact damage.")]
    [SerializeField] private float minCollisionDamageSpeed = 2.5f;

    [Header("Death Explosion & Chain Reaction")]
    [Tooltip("Whether this rock explodes when destroyed (Normal, Speedy, Tanky). Magnet uses Vortex instead.")]
    [SerializeField] private bool explodeOnDeath = true;
    [Tooltip("Fraction of max health dealt as explosion damage (0.5 = 50% of max health).")]
    [SerializeField] private float explosionDamageFraction = 0.5f;
    [Tooltip("Physical impulse force pushing surrounding asteroids away from the blast.")]
    [SerializeField] private float explosionForce = 12f;
    [Tooltip("Whether explosion hits count toward the player combo counter.")]
    [SerializeField] private bool explosionCountsForCombo = true;
    [Tooltip("Optional visual effect prefab to instantiate at explosion position.")]
    [SerializeField] private GameObject explosionVFXPrefab;

    [Header("Debris Chain Reaction (Big Boss Prototype)")]
    [Tooltip("On death, shed shards in random directions instead of detonating. Each shard destroys the first meteor it hits, which sheds its own shards (domino chain). While ON, the area-of-effect explosion above is shelved (not deleted - untick this to bring it back).")]
    [SerializeField] private bool spawnDebrisOnDeath = true;
    [Tooltip("Minimum shards shed per destroyed meteor.")]
    [SerializeField] private int debrisMinCount = 2;
    [Tooltip("Maximum shards shed per destroyed meteor.")]
    [SerializeField] private int debrisMaxCount = 5;
    [Tooltip("Shard flight speed in units/second. Lower = easier to watch the chain ripple outward.")]
    [SerializeField] private float debrisSpeed = 6f;
    [Tooltip("Seconds before an unspent shard fizzles out. Reach = speed x lifetime.")]
    [SerializeField] private float debrisLifetime = 1.1f;
    [Tooltip("Damage each shard deals on impact (Normal meteors have 1 HP).")]
    [SerializeField] private int debrisDamage = 1;
    [Tooltip("How far from the meteor's center the shards start, so they clear the dying body.")]
    [SerializeField] private float debrisSpawnOffset = 0.35f;
    [Tooltip("Optional shard prefab (MeteorDebris is added automatically if missing). If empty, a small glowing shard using this meteor's own sprite is generated.")]
    [SerializeField] private GameObject debrisPrefab;

    [Header("Debug")]
    [Tooltip("Log every hit/explosion to the Console. Keep OFF for large swarms: Debug.Log is expensive and causes hitches during chain reactions.")]
    [SerializeField] private bool verboseLogging = false;

    private Rigidbody2D rb;
    private bool isDead = false;
    private float spawnTime = 0f;
    private bool hasAppliedConfig = false;
    private static readonly Collider2D[] nearbyColliders = new Collider2D[16];

    public bool IsDead => isDead;

    public float EffectiveRadius => blastRadiusMultiplier * Mathf.Max(0.5f, transform.localScale.x);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        spawnTime = Time.time;

        if (!hasAppliedConfig)
        {
            if (autoConfigureStatsFromType && !useCustomStats)
            {
                ApplyMeteorTypeConfiguration();
            }

            if (hp <= 0)
            {
                hp = maxhp;
            }

            if (customSprites != null && customSprites.Length > 0)
            {
                ApplySpriteFromSprites(null);
            }

            if (earthTransform == null)
            {
                GameObject earthObj = GameObject.Find("Earth");
                if (earthObj != null) earthTransform = earthObj.transform;
            }

            ApplyInitialVelocity();
        }
    }

    /// <summary>
    /// Master initialization called by AsteroidSpawner.
    /// Injects all stats, speed, and real-world Earth gravity physics.
    /// Rock functions as an info carriage executing AsteroidSpawner's settings.
    /// </summary>
    public void InitFromSpawner(AsteroidSpawner spawner, AsteroidSpawner.MeteorTypeConfig config, bool isSnailSwarm)
    {
        hasAppliedConfig = true;

        if (spawner != null)
        {
            earthTransform = spawner.EarthCenter;
            earthRadius = spawner.EarthRadius;
            earthGravityGM = spawner.EarthGravityGM;
            minGravityDistance = spawner.MinGravityDistance;
            outwardBrakeRate = spawner.OutwardBrakeRate;
            tangentialDamping = spawner.TangentialDamping;
            separationRadius = spawner.SeparationRadius;
            separationForce = spawner.SeparationForce;
            randomTumbleSpeed = spawner.RandomTumbleSpeed;
        }

        if (config != null)
        {
            meteorType = config.type;
            maxhp = config.maxHp;
            hp = maxhp;
            initialInwardSpeed = config.initialSpeed;
            terminalVelocity = config.terminalVelocity;
            gravityMultiplier = config.gravityMultiplier;
            transform.localScale = Vector3.one * config.sizeScale;
            blastRadiusMultiplier = config.blastRadiusMultiplier;
            immuneToNormalFire = config.immuneToNormalFire;
            baseResourceDrop = config.baseResourceDrop;
            baseChainCost = config.baseChainCost;
            vortexPullForce = config.vortexPullForce;
            threatValue = config.threatValue;
            earthImpactDrain = config.earthImpactDrain;

            ApplySpriteFromSprites(config.sprites);
        }

        if (isSnailSwarm && spawner != null)
        {
            initialInwardSpeed = spawner.SwarmInwardSpeed;
            terminalVelocity = spawner.SwarmMaxSpeed;
            gravityMultiplier = spawner.SwarmGravityScale;
            separationForce *= spawner.SwarmSeparationMultiplier;
        }

        ApplyInitialVelocity();
    }

    /// <summary>
    /// Updates physics settings live during play mode when values change on AsteroidSpawner.
    /// </summary>
    public void UpdatePhysicsSettings(Transform earthCenter, float earthRadius, float earthGravityGM, float minGravityDistance, float outwardBrakeRate, float tangentialDamping, float separationRadius, float separationForce)
    {
        this.earthTransform = earthCenter;
        this.earthRadius = earthRadius;
        this.earthGravityGM = earthGravityGM;
        this.minGravityDistance = minGravityDistance;
        this.outwardBrakeRate = outwardBrakeRate;
        this.tangentialDamping = tangentialDamping;
        this.separationRadius = separationRadius;
        this.separationForce = separationForce;
    }

    /// <summary>
    /// Applies starting inward velocity and tumble angular velocity.
    /// </summary>
    public void ApplyInitialVelocity()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb == null) return;

        Vector2 earthCenter = GetEarthCenter();
        Vector2 toEarth = earthCenter - (Vector2)transform.position;

        if (initialInwardSpeed > 0f && toEarth.sqrMagnitude > 0.01f)
        {
            rb.linearVelocity = toEarth.normalized * initialInwardSpeed;
        }

        if (randomTumbleSpeed > 0f)
        {
            rb.angularVelocity = UnityEngine.Random.Range(-randomTumbleSpeed, randomTumbleSpeed);
        }
    }

    private void ApplySpriteFromSprites(Sprite[] configSprites)
    {
        Sprite chosenSprite = null;
        if (configSprites != null && configSprites.Length > 0)
        {
            chosenSprite = configSprites[UnityEngine.Random.Range(0, configSprites.Length)];
        }
        else if (customSprites != null && customSprites.Length > 0)
        {
            chosenSprite = customSprites[UnityEngine.Random.Range(0, customSprites.Length)];
        }

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && (sr.sprite == null || !retainExistingSprite))
        {
            if (chosenSprite != null)
            {
                sr.sprite = chosenSprite;
            }
        }
    }

    /// <summary>
    /// Applies status configuration passed from AsteroidSpawner (backward compatibility wrapper).
    /// </summary>
    public void ApplyConfig(AsteroidSpawner.MeteorTypeConfig config)
    {
        InitFromSpawner(AsteroidSpawner.Instance, config, false);
    }

    /// <summary>
    /// Configures default meteor traits, scaling, health, and drop parameters based on MeteorType.
    /// Ignored if useCustomStats is enabled.
    /// </summary>
    public void ApplyMeteorTypeConfiguration()
    {
        if (useCustomStats) return;

        switch (meteorType)
        {
            case MeteorType.Normal:
                baseResourceDrop = 7;
                baseChainCost = 5;
                blastRadiusMultiplier = 2.5f;
                immuneToNormalFire = false;
                maxhp = 1;
                initialInwardSpeed = 1.5f;
                terminalVelocity = 12.0f;
                gravityMultiplier = 1.0f;
                threatValue = 1.0f;
                earthImpactDrain = 20;
                transform.localScale = Vector3.one * 1.0f;
                break;

            case MeteorType.Speedy:
                baseResourceDrop = 3;
                baseChainCost = 10;
                blastRadiusMultiplier = 1.5f;
                immuneToNormalFire = false;
                maxhp = 1;
                terminalVelocity = 16.0f;
                initialInwardSpeed = 2.4f;
                gravityMultiplier = 1.1f;
                threatValue = 1.5f;
                earthImpactDrain = 30;
                transform.localScale = Vector3.one * 0.7f;
                break;

            case MeteorType.Tanky:
                baseResourceDrop = 5;
                baseChainCost = 7;
                blastRadiusMultiplier = 1.5f;
                immuneToNormalFire = true; // Immune to normal Laser Cannon
                maxhp = 3;
                terminalVelocity = 8.0f;
                initialInwardSpeed = 1.2f;
                gravityMultiplier = 0.9f;
                threatValue = 2.0f;
                earthImpactDrain = 40;
                transform.localScale = Vector3.one * 1.5f;
                break;

            case MeteorType.Magnet:
                baseResourceDrop = 8;
                baseChainCost = 15;
                blastRadiusMultiplier = 5.0f; // Vortex radius
                vortexPullForce = 22f;
                immuneToNormalFire = true; // Immune to normal Laser Cannon
                maxhp = 2;
                terminalVelocity = 9.0f;
                initialInwardSpeed = 1.3f;
                gravityMultiplier = 1.0f;
                threatValue = 3.0f;
                earthImpactDrain = 60;
                transform.localScale = Vector3.one * 1.0f;
                break;
        }
    }

    /// <summary>
    /// Overrides movement for the sandbox swarm: crawl speed, and weakened mutual repulsion.
    /// Full-strength flocking repulsion would shove a packed swarm apart far faster than snail pace.
    /// Call right after ApplyConfig (before Start reads initialInwardSpeed).
    /// </summary>
    public void ApplySwarmMovement(float inwardSpeed, float maxSpeed, float separationMultiplier)
    {
        initialInwardSpeed = inwardSpeed;
        terminalVelocity = maxSpeed;
        gravityMultiplier = 0.05f;
        separationForce *= Mathf.Max(0f, separationMultiplier);
        ApplyInitialVelocity();
    }

    [ContextMenu("Reset to Preset Defaults for MeteorType")]
    private void ContextMenuLoadDefaults()
    {
        useCustomStats = false;
        ApplyMeteorTypeConfiguration();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        // 1. Formation anti-collision spacing
        if (separationForce > 0f && separationRadius > 0f)
        {
            ApplyFlockingSeparation();
        }

        // 2. Continuous Gravity Pull System towards Earth
        ApplyGravityPull();
    }

    /// <summary>
    /// Gravitational pull towards Earth:
    /// Uses real-world Newtonian inverse-square gravity: a = (G * M) / r^2.
    /// Pulls the asteroid continuously toward Earth's center, accelerating smoothly as it falls closer.
    /// If knocked back outward, actively brakes outward velocity so it comes back down to Earth like an apple thrown in the sky.
    /// </summary>
    public void ApplyGravityPull()
    {
        if (isDead || rb == null) return;

        Vector2 earthCenter = GetEarthCenter();
        Vector2 toEarth = earthCenter - (Vector2)transform.position;
        float dist = toEarth.magnitude;

        // Check if the asteroid has reached Earth's surface -> Sudden Death / Resource Drain!
        if (dist <= earthRadius)
        {
            HitEarth();
            return;
        }

        Vector2 dirToEarth = toEarth / dist;

        // Decompose velocity into radial (toward/away from Earth) and tangential (sideways)
        Vector2 currentVel = rb.linearVelocity;
        float radialSpeed = Vector2.Dot(currentVel, dirToEarth);
        Vector2 tangentialVel = currentVel - (dirToEarth * radialSpeed);

        // Real-world Newtonian gravity: a = (G * M) / r^2
        // Clamped at minGravityDistance (surface radius) to prevent division by zero / singularity
        float r = Mathf.Max(dist, minGravityDistance);
        float gravityAcc = (earthGravityGM / (r * r)) * gravityMultiplier;

        if (radialSpeed < 0f)
        {
            // Moving AWAY from Earth (e.g. knocked back by bullet, lightning, or explosion)
            // Brake outward speed rapidly via gravity pull + outwardBrakeRate so it slows, stops, and reverses
            float brakeAcc = gravityAcc + outwardBrakeRate;
            radialSpeed += brakeAcc * Time.fixedDeltaTime;
        }
        else
        {
            // Moving TOWARD Earth: accelerate smoothly via real-world 1/r^2 gravity
            radialSpeed += gravityAcc * Time.fixedDeltaTime;
            if (terminalVelocity > 0f)
            {
                radialSpeed = Mathf.Min(radialSpeed, terminalVelocity);
            }
        }

        // Damp sideways (tangential) velocity so it doesn't get stuck in wide orbits
        if (tangentialDamping > 0f)
        {
            tangentialVel = Vector2.Lerp(tangentialVel, Vector2.zero, tangentialDamping * Time.fixedDeltaTime);
        }

        rb.linearVelocity = (dirToEarth * radialSpeed) + tangentialVel;
    }

    /// <summary>
    /// Applies an external push / knockback impulse to this asteroid (from weapons, explosions, or impacts).
    /// Immediately updates linearVelocity so physics reacts reliably and gravity pulls it back.
    /// </summary>
    public void Push(Vector2 pushImpulse)
    {
        if (isDead || rb == null) return;
        float mass = Mathf.Max(0.2f, rb.mass);
        rb.linearVelocity += pushImpulse / mass;
    }

    /// <summary>
    /// Applies an inward pull force to this asteroid (e.g. from Magnet meteor vortex).
    /// </summary>
    public void Pull(Vector2 pullForce)
    {
        if (isDead || rb == null) return;
        float mass = Mathf.Max(0.2f, rb.mass);
        rb.linearVelocity += pullForce / mass;
    }

    private void ApplyFlockingSeparation()
    {
        LayerMask searchMask = enemy_layers.value != 0 ? enemy_layers : (1 << gameObject.layer);
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(searchMask);
        filter.useLayerMask = true;
        int count = Physics2D.OverlapCircle(transform.position, separationRadius, filter, nearbyColliders);
        Vector2 separationPush = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = nearbyColliders[i];
            if (col == null || col.gameObject == gameObject) continue;

            Vector2 away = (Vector2)transform.position - (Vector2)col.transform.position;
            float dist = away.magnitude;
            if (dist > 0.001f && dist < separationRadius)
            {
                float factor = 1f - (dist / separationRadius);
                separationPush += (away / dist) * (factor * separationForce);
            }
        }

        if (separationPush.sqrMagnitude > 0.001f)
        {
            rb.linearVelocity += separationPush * Time.fixedDeltaTime;
        }
    }

    /// <summary>
    /// Applies damage to the meteor.
    /// If isExplosionOrPiercing is false, Tanky and Magnet meteors are immune!
    /// Combo bookkeeping happens HERE so weapons don't need to call ComboManager themselves:
    /// - Lethal damage  -> Die() -> ComboManager.RegisterMeteorKill (counts once).
    /// - Non-lethal hit -> ComboManager.RegisterHit (if countsForCombo).
    /// Returns true if this damage destroyed the meteor.
    /// </summary>
    public bool TakeDamage(int amount, bool isExplosionOrPiercing = false, bool countsForCombo = true)
    {
        if (isDead) return false;

        // Check immunity against normal primary fire
        if (immuneToNormalFire && !isExplosionOrPiercing)
        {
            if (verboseLogging) Debug.Log($"[Rock] {name} ({meteorType}) is immune to normal Laser Cannon fire! Needs an explosion or piercing weapon.");
            return false;
        }

        hp -= amount;
        if (verboseLogging) Debug.Log($"{name} ({meteorType}) took {amount} damage, hp now {hp}");

        if (hp <= 0)
        {
            Die();
            return true;
        }

        if (countsForCombo)
        {
            ComboManager.RegisterHit();
        }
        return false;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Register kill and award chain resources through ComboManager
        if (ComboManager.Instance != null)
        {
            ComboManager.Instance.RegisterMeteorKill(meteorType, baseResourceDrop, baseChainCost);
        }

        // Magnet meteors do not deal blast damage; they trigger a gravitational vortex pull
        if (meteorType == MeteorType.Magnet)
        {
            EmitVortexPull();
        }
        else if (spawnDebrisOnDeath)
        {
            SpawnDebris();
        }
        else if (explodeOnDeath)
        {
            Explode();
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Sheds debrisMinCount-debrisMaxCount shards in random outward directions.
    /// Directions are spread around the circle with random jitter: random enough to look chaotic,
    /// but shards never stack on top of each other and fly the same way.
    /// </summary>
    private void SpawnDebris()
    {
        int lo = Mathf.Max(0, Mathf.Min(debrisMinCount, debrisMaxCount));
        int hi = Mathf.Max(lo, Mathf.Max(debrisMinCount, debrisMaxCount));
        int count = Random.Range(lo, hi + 1);
        if (count <= 0) return;

        Vector2 origin = transform.position;

        if (explosionVFXPrefab != null)
        {
            Instantiate(explosionVFXPrefab, origin, Quaternion.identity);
        }

        SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
        float baseAngle = Random.Range(0f, 360f);
        float slice = 360f / count;
        float jitter = slice * 0.35f;

        for (int i = 0; i < count; i++)
        {
            float angle = (baseAngle + i * slice + Random.Range(-jitter, jitter)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 spawnPos = origin + dir * debrisSpawnOffset;

            GameObject shard = debrisPrefab != null
                ? Instantiate(debrisPrefab, spawnPos, Quaternion.identity)
                : MeteorDebris.CreateProcedural(spawnPos, source);

            MeteorDebris debris = shard.GetComponent<MeteorDebris>();
            if (debris == null) debris = shard.AddComponent<MeteorDebris>();

            // Slight speed variance so shards of one burst don't arrive in lock-step
            debris.Launch(dir, debrisSpeed * Random.Range(0.85f, 1.15f), debrisLifetime, debrisDamage);
        }

        if (verboseLogging) Debug.Log($"[Rock] {name} ({meteorType}) shed {count} debris shards.");
    }

    /// <summary>
    /// Normal, Speedy, and Tanky meteors detonate on death, dealing damage and pushing neighbors outward.
    /// </summary>
    private void Explode()
    {
        Vector2 explosionCenter = transform.position;
        float radius = EffectiveRadius;
        int expDamage = Mathf.Max(1, Mathf.RoundToInt(maxhp * explosionDamageFraction));

        if (explosionVFXPrefab != null)
        {
            Instantiate(explosionVFXPrefab, explosionCenter, Quaternion.identity);
        }

        LayerMask searchMask = enemy_layers.value != 0 ? enemy_layers : (1 << gameObject.layer);
        Collider2D[] hits = Physics2D.OverlapCircleAll(explosionCenter, radius, searchMask);

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

        foreach (Rock targetRock in targets)
        {
            if (targetRock == null || targetRock.isDead) continue;

            Vector2 toTarget = (Vector2)targetRock.transform.position - explosionCenter;
            float dist = toTarget.magnitude;
            Vector2 pushDir = dist > 0.001f ? toTarget / dist : UnityEngine.Random.insideUnitCircle.normalized;

            float falloff = 1f - Mathf.Clamp01(dist / radius);
            Vector2 impulse = pushDir * (explosionForce * Mathf.Lerp(0.5f, 1.2f, falloff));

            // Apply physical push impulse
            targetRock.Push(impulse);

            // Explosion damage can damage Tanky and Magnet meteors (isExplosionOrPiercing = true)
            targetRock.TakeDamage(expDamage, isExplosionOrPiercing: true, countsForCombo: explosionCountsForCombo);
        }

        if (verboseLogging) Debug.Log($"[Rock] {name} ({meteorType}) exploded (Damage: {expDamage}, Radius: {radius:F1}, Targets: {targets.Count})");
    }

    /// <summary>
    /// Magnet meteor death effect: creates a gravitational vortex pulling all nearby meteors inward into a tight cluster.
    /// Does NOT deal blast damage.
    /// </summary>
    private void EmitVortexPull()
    {
        Vector2 vortexCenter = transform.position;
        float radius = EffectiveRadius; // 5.0x size

        if (explosionVFXPrefab != null)
        {
            Instantiate(explosionVFXPrefab, vortexCenter, Quaternion.identity);
        }

        LayerMask searchMask = enemy_layers.value != 0 ? enemy_layers : (1 << gameObject.layer);
        Collider2D[] hits = Physics2D.OverlapCircleAll(vortexCenter, radius, searchMask);

        int pulledCount = 0;
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            Rock targetRock = hit.GetComponent<Rock>();
            if (targetRock != null && !targetRock.isDead)
            {
                // Pull inward toward vortex center
                Vector2 toCenter = vortexCenter - (Vector2)targetRock.transform.position;
                float dist = toCenter.magnitude;
                if (dist > 0.1f)
                {
                    Vector2 pullDir = toCenter / dist;
                    float pullFactor = 1f - Mathf.Clamp01(dist / radius);
                    Vector2 pullImpulse = pullDir * (vortexPullForce * Mathf.Lerp(0.8f, 1.5f, pullFactor));
                    targetRock.Pull(pullImpulse);
                    pulledCount++;
                }
            }
        }

        if (verboseLogging) Debug.Log($"[Rock] Magnet Meteor {name} imploded! Vortex pulled {pulledCount} surrounding asteroids inward (Radius: {radius:F1}).");
    }

    /// <summary>
    /// Called when the asteroid impacts Earth's surface.
    /// Drains player resources; when resources hit 0, triggers Game Over!
    /// </summary>
    private void HitEarth()
    {
        if (isDead) return;
        isDead = true;

        if (explosionVFXPrefab != null)
        {
            Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
        }

        Debug.LogWarning($"[Rock] {name} ({meteorType}, Threat: {threatValue}) collided with Earth! Draining {earthImpactDrain} resources.");
        if (Player_controller.Instance != null)
        {
            Player_controller.Instance.DrainResourcesFromEarthImpact(earthImpactDrain, this);
        }
        else if (ComboManager.Instance != null && !SandboxMode.Enabled)
        {
            ComboManager.Instance.TriggerGameOver($"Earth Breached by {meteorType} Asteroid");
        }

        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsEarth(collision.gameObject))
        {
            HitEarth();
            return;
        }

        if (Time.time < spawnTime + spawnDamageGracePeriod)
        {
            return;
        }

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

        return obj.tag == "Earth";
    }

    private void TryHit(GameObject target)
    {
        bool tag_match = target.CompareTag(enemy_tag);
        bool layer_match = (enemy_layers.value & (1 << target.layer)) != 0;

        if (tag_match || layer_match)
        {
            Rock other = target.GetComponent<Rock>();
            if (other != null) other.TakeDamage(damage, isExplosionOrPiercing: false, countsForCombo: false);
        }
    }

    public Vector2 GetEarthCenter()
    {
        return earthTransform != null ? (Vector2)earthTransform.position : Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = meteorType == MeteorType.Magnet ? new Color(0.3f, 0.5f, 1f, 0.4f) : new Color(1f, 0.45f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, EffectiveRadius);
    }
}
