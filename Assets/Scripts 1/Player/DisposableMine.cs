using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tactical Deployable Mine.
/// Slowly travels toward the target position, floats in space, and dynamically scales its blast radius:
/// 1. Expands from minBlastRadius to maxBlastRadius.
/// 2. Holds peak blast radius for peakDuration.
/// 3. Shrinks/decays back down, fizzling out if left unexploded.
/// Does NOT detonate on contact with enemies. Must be shot by Laser Cannon, Railgun, or Lightning!
/// Cost: 50 resources per deployment.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class DisposableMine : MonoBehaviour
{
    public enum MineState
    {
        Deploying,   // Traveling to target position
        Expanding,   // Armed: expanding blast radius
        Peak,        // Maximum blast radius
        Decaying,    // Radius shrinking
        Fizzled,     // Fizzled out without detonating
        Detonated    // Exploded!
    }

    [Header("Flight & Positioning")]
    [Tooltip("Flight speed towards target world position.")]
    [SerializeField] private float travelSpeed = 8.0f;
    [Tooltip("Distance threshold to consider mine arrived at destination.")]
    [SerializeField] private float arrivalThreshold = 0.2f;

    [Header("Dynamic Blast Radius Scaling")]
    [Tooltip("Starting minimum blast radius.")]
    [SerializeField] private float minBlastRadius = 2.0f;
    [Tooltip("Peak maximum blast radius.")]
    [SerializeField] private float maxBlastRadius = 5.5f;
    [Tooltip("Duration in seconds to expand from min to max blast radius.")]
    [SerializeField] private float expandDuration = 2.5f;
    [Tooltip("Duration in seconds to hold peak blast radius.")]
    [SerializeField] private float peakDuration = 3.0f;
    [Tooltip("Duration in seconds to decay back to zero before fizzling out.")]
    [SerializeField] private float decayDuration = 2.5f;

    [Header("Explosion Properties")]
    [Tooltip("Damage dealt to enemies within blast radius.")]
    [SerializeField] private int explosionDamage = 4;
    [Tooltip("Physical impulse force pushing surrounding asteroids away from the detonation.")]
    [SerializeField] private float explosionForce = 22f;
    [Tooltip("Layer mask for enemies caught in the explosion.")]
    [SerializeField] private LayerMask enemyLayers;
    [Tooltip("Optional explosion VFX prefab.")]
    [SerializeField] private GameObject explosionVFXPrefab;

    private Vector2 targetPosition;
    private MineState state = MineState.Deploying;
    private float stateTimer = 0f;
    private float currentRadius = 0f;
    private LineRenderer rangeRing;
    private CircleCollider2D col;

    public bool IsDetonated => state == MineState.Detonated || state == MineState.Fizzled;
    public float CurrentBlastRadius => currentRadius;
    public MineState State => state;

    public void Initialize(Vector2 destination)
    {
        targetPosition = destination;
        state = MineState.Deploying;
        currentRadius = minBlastRadius;
    }

    private void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        if (col.radius < 0.45f) col.radius = 0.5f;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        SetupRangeRing();
    }

    private void SetupRangeRing()
    {
        rangeRing = GetComponent<LineRenderer>();
        if (rangeRing == null)
        {
            rangeRing = gameObject.AddComponent<LineRenderer>();
            Shader s = Shader.Find("Sprites/Default");
            if (s != null) rangeRing.material = new Material(s);
            rangeRing.startWidth = 0.08f;
            rangeRing.endWidth = 0.08f;
            rangeRing.useWorldSpace = false;
            rangeRing.loop = true;
            rangeRing.positionCount = 48;
        }
    }

    private void Update()
    {
        if (IsDetonated) return;

        switch (state)
        {
            case MineState.Deploying:
                transform.position = Vector2.MoveTowards(transform.position, targetPosition, travelSpeed * Time.deltaTime);
                if (Vector2.Distance(transform.position, targetPosition) <= arrivalThreshold)
                {
                    state = MineState.Expanding;
                    stateTimer = 0f;
                }
                currentRadius = minBlastRadius;
                break;

            case MineState.Expanding:
                stateTimer += Time.deltaTime;
                float expandPct = Mathf.Clamp01(stateTimer / expandDuration);
                currentRadius = Mathf.Lerp(minBlastRadius, maxBlastRadius, expandPct);
                if (expandPct >= 1f)
                {
                    state = MineState.Peak;
                    stateTimer = 0f;
                }
                break;

            case MineState.Peak:
                stateTimer += Time.deltaTime;
                currentRadius = maxBlastRadius;
                if (stateTimer >= peakDuration)
                {
                    state = MineState.Decaying;
                    stateTimer = 0f;
                }
                break;

            case MineState.Decaying:
                stateTimer += Time.deltaTime;
                float decayPct = Mathf.Clamp01(stateTimer / decayDuration);
                currentRadius = Mathf.Lerp(maxBlastRadius, 0.5f, decayPct);
                if (decayPct >= 1f)
                {
                    Fizzle();
                    return;
                }
                break;
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (rangeRing != null && rangeRing.positionCount > 0)
        {
            float angleStep = 360f / rangeRing.positionCount;
            for (int i = 0; i < rangeRing.positionCount; i++)
            {
                float a = i * angleStep * Mathf.Deg2Rad;
                rangeRing.SetPosition(i, new Vector3(Mathf.Cos(a) * currentRadius, Mathf.Sin(a) * currentRadius, 0f));
            }

            float pulse = Mathf.PingPong(Time.time * 6f, 1f);
            Color ringColor;
            if (state == MineState.Peak)
                ringColor = Color.Lerp(new Color(1f, 0.85f, 0.1f, 0.9f), new Color(1f, 0.2f, 0.1f, 1f), pulse);
            else if (state == MineState.Expanding)
                ringColor = Color.Lerp(new Color(1f, 0.5f, 0.1f, 0.5f), new Color(1f, 0.8f, 0.2f, 0.85f), pulse);
            else
                ringColor = new Color(0.8f, 0.2f, 0.1f, 0.4f * (1f - (stateTimer / decayDuration)));

            rangeRing.startColor = ringColor;
            rangeRing.endColor = ringColor;
        }

        transform.Rotate(0f, 0f, 30f * Time.deltaTime);
    }

    /// <summary>
    /// Manually detonates the mine when struck by bullet or lightning.
    /// Deals heavy explosion damage and outward physical impulse within current dynamic radius.
    /// </summary>
    public void Detonate()
    {
        if (IsDetonated) return;
        state = MineState.Detonated;

        Vector2 center = transform.position;

        if (explosionVFXPrefab != null)
        {
            Instantiate(explosionVFXPrefab, center, Quaternion.identity);
        }
        else
        {
            SpawnProceduralShockwave(center, currentRadius);
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound3D("BulletImpact", center);
        }

        LayerMask searchMask = enemyLayers.value != 0 ? enemyLayers : LayerMask.GetMask("Enemy");
        if (searchMask.value == 0) searchMask = Physics2D.AllLayers;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, currentRadius, searchMask);
        List<Rock> hitRocks = new List<Rock>();

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;
            Rock rock = hit.GetComponent<Rock>();
            if (rock != null && !hitRocks.Contains(rock))
            {
                hitRocks.Add(rock);
            }
        }

        foreach (Rock rock in hitRocks)
        {
            if (rock == null) continue;

            Vector2 toTarget = (Vector2)rock.transform.position - center;
            float dist = toTarget.magnitude;
            Vector2 pushDir = dist > 0.001f ? toTarget / dist : Random.insideUnitCircle.normalized;
            float falloff = 1f - Mathf.Clamp01(dist / currentRadius);

            // Apply explosion impulse
            rock.Push(pushDir * (explosionForce * Mathf.Lerp(0.6f, 1.3f, falloff)));

            // Mine explosion is an explosion -> passes isExplosionOrPiercing = true (damages Tanky & Magnet!)
            rock.TakeDamage(explosionDamage, isExplosionOrPiercing: true);
        }

        Debug.Log($"[DisposableMine] DETONATED! Radius: {currentRadius:F1}, Hit Targets: {hitRocks.Count}");

        Destroy(gameObject);
    }

    private void Fizzle()
    {
        if (IsDetonated) return;
        state = MineState.Fizzled;
        Debug.Log("[DisposableMine] Expired and fizzled out without detonating.");
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsDetonated) return;

        // 1. Struck by player projectile (Laser, Railgun, etc.) -> remote detonation
        Bullet bullet = other.GetComponent<Bullet>();
        if (bullet != null)
        {
            Detonate();
            Destroy(bullet.gameObject);
            return;
        }

        // 2. Struck by enemy meteor -> contact / proximity detonation!
        Rock rock = other.GetComponent<Rock>();
        if (rock != null || other.CompareTag("Enemy"))
        {
            Detonate();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsDetonated) return;

        Bullet bullet = collision.gameObject.GetComponent<Bullet>();
        if (bullet != null)
        {
            Detonate();
            Destroy(bullet.gameObject);
            return;
        }

        Rock rock = collision.gameObject.GetComponent<Rock>();
        if (rock != null || collision.gameObject.CompareTag("Enemy"))
        {
            Detonate();
        }
    }

    private void SpawnProceduralShockwave(Vector2 center, float radius)
    {
        GameObject shock = new GameObject("MineShockwave");
        shock.transform.position = center;
        LineRenderer lr = shock.AddComponent<LineRenderer>();
        Shader s = Shader.Find("Sprites/Default");
        if (s != null) lr.material = new Material(s);
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 36;
        lr.startWidth = 0.3f;
        lr.endWidth = 0.3f;

        MineShockwaveFX fx = shock.AddComponent<MineShockwaveFX>();
        fx.Initialize(radius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, currentRadius > 0.1f ? currentRadius : minBlastRadius);
    }
}

/// <summary>
/// Procedural expanding shockwave ring and flash for Disposable Mine detonation.
/// </summary>
public class MineShockwaveFX : MonoBehaviour
{
    private float maxRadius = 4f;
    private float duration = 0.32f;
    private float elapsed = 0f;
    private LineRenderer lr;

    public void Initialize(float targetRadius)
    {
        maxRadius = Mathf.Max(targetRadius, 1.5f);
        lr = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        float currentR = Mathf.Lerp(0.4f, maxRadius, Mathf.Sin(t * Mathf.PI * 0.5f));
        float alpha = 1f - t;

        if (lr != null && lr.positionCount > 0)
        {
            float step = 360f / lr.positionCount;
            for (int i = 0; i < lr.positionCount; i++)
            {
                float a = i * step * Mathf.Deg2Rad;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * currentR, Mathf.Sin(a) * currentR, 0f));
            }
            lr.startColor = new Color(1f, 0.65f, 0.15f, alpha * 0.95f);
            lr.endColor = new Color(1f, 0.25f, 0.05f, alpha * 0.6f);
            lr.startWidth = Mathf.Lerp(0.35f, 0.02f, t);
            lr.endWidth = lr.startWidth;
        }

        if (elapsed >= duration)
        {
            Destroy(gameObject);
        }
    }
}
