using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player_controller : MonoBehaviour
{
    public static Player_controller Instance { get; private set; }

    [SerializeField] private Transform mouse_transform;

    [Header("Earth Center & Orbit")]
    [Tooltip("Reference to the Earth object. If empty, automatically finds a GameObject named 'Earth' or defaults to (0,0).")]
    [SerializeField] private Transform earth;
    [Tooltip("Distance from Earth to the gun. If 0, auto-detects from the initial scene position.")]
    [SerializeField] private float orbitDistance = 0f;

    [Header("Rotation & Pull Force")]
    [Tooltip("Maximum rotation speed in degrees per second.")]
    [SerializeField] private float turnSpeed = 240f;
    [Tooltip("Pull force inertia / smoothing time in seconds. Higher = heavier feel with smooth acceleration and deceleration.")]
    [SerializeField] private float smoothTime = 0.2f;

    [Header("Bullet")]
    [SerializeField] private Transform leftPrefab;
    [Tooltip("Legacy ricochet bullet prefab reference (now replaced by Chain Lightning).")]
    [SerializeField] private Transform rightPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 0.3f;
    private float nextFireTime;

    [Header("Chain Lightning (Right Click Secondary Weapon)")]
    [Tooltip("Cooldown time in seconds before chain lightning can be fired again.")]
    public float lightningCooldown = 3.0f;

    [Tooltip("Maximum number of enemies the lightning can chain to (Amount).")]
    public int maxChainTargets = 5;

    [Tooltip("Damage dealt to each enemy struck by the lightning.")]
    public int lightningDamage = 2;

    [Tooltip("Maximum range to find the initial target from the cannon.")]
    public float lightningRange = 14f;

    [Tooltip("Maximum jump distance between successive chained enemies.")]
    public float chainRadius = 7.5f;

    [Tooltip("How long the lightning beam trail remains visible before disappearing (in seconds).")]
    public float beamDuration = 0.25f;

    [Tooltip("Physical knockback impulse force applied to enemies struck by the lightning.")]
    public float lightningKnockback = 8f;

    [Tooltip("Layer mask for targets that can be hit by lightning. Defaults to 'Enemy' layer.")]
    [SerializeField] private LayerMask lightningTargetLayers;

    [Tooltip("Whether firing chain lightning consumes secondary ammunition (if false, cooldown only).")]
    [SerializeField] private bool lightningUsesAmmo = true;

    [Tooltip("Optional custom beam visual prefab (with LineRenderer). If null, a procedural electric LineRenderer is generated.")]
    [SerializeField] private GameObject lightningBeamPrefab;

    [Header("Unified Resource Economy")]
    [Tooltip("Current unified resource pool. Earned from destroying meteors.")]
    [SerializeField] private int resources = 100;
    [Tooltip("Resource cost to fire the primary Laser Cannon (10 resources). 0 during Overload.")]
    [SerializeField] private int laserCannonCost = 10;
    [Tooltip("Resource cost to fire the Chain Lightning secondary weapon.")]
    [SerializeField] private int lightningResourceCost = 25;

    [Tooltip("Show current resource and weapon HUD on screen.")]
    [SerializeField] private bool showAmmoHUD = true;

    private float lightningCooldownTimer = 0f;
    private float currentAngle;
    private float angleVelocity;

    // Public Getters for Economy & Weapon Systems
    public int Resources => resources;
    public int RegularAmmo => resources; // Legacy alias
    public int MaxRegularAmmo => -1;     // Unlimited capacity pool
    public int RicochetAmmo => resources; // Legacy alias
    public int MaxRicochetAmmo => -1;
    public float LightningCooldownTimer => lightningCooldownTimer;
    public bool IsLightningReady => lightningCooldownTimer <= 0f && CanAfford(lightningUsesAmmo ? lightningResourceCost : 0);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        // Serialized fields are NOT auto-filled by Unity, so fall back to sensible defaults
        if (mouse_transform == null) mouse_transform = transform;   // rotate this object
        if (firePoint == null) firePoint = mouse_transform;         // shoot from the aim pivot
        if (leftPrefab == null) Debug.LogError("Left Bullet Prefab is not assigned on " + name, this);
    }

    private void Start()
    {
        if (earth == null)
        {
            GameObject earthObj = GameObject.Find("Earth");
            if (earthObj != null) earth = earthObj.transform;
        }

        Vector2 earthCenter = GetEarthCenter();
        Vector2 startOffset = (Vector2)transform.position - earthCenter;

        // Auto-detect orbit radius from starting position if left at 0
        if (orbitDistance <= 0.001f)
        {
            orbitDistance = startOffset.magnitude;
        }

        // Initialize angle smoothly from starting placement so it never snaps on frame 1
        if (startOffset.sqrMagnitude > 0.01f)
        {
            currentAngle = Mathf.Atan2(startOffset.y, startOffset.x) * Mathf.Rad2Deg;
        }
        else
        {
            currentAngle = mouse_transform.eulerAngles.z;
        }
    }

    private void MouseAim()
    {
        if (Mouse.current == null || Camera.main == null) return;

        Vector2 earthCenter = GetEarthCenter();
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 dirToMouse = mouseWorld - earthCenter;

        // Only update target angle if mouse is moved outside Earth center
        if (dirToMouse.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(dirToMouse.y, dirToMouse.x) * Mathf.Rad2Deg;

            // Apply pull force & speed limits instead of instant snapping
            if (smoothTime > 0.001f)
            {
                currentAngle = Mathf.SmoothDampAngle(currentAngle, targetAngle, ref angleVelocity, smoothTime, turnSpeed);
            }
            else
            {
                currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);
            }
        }

        // Position smoothly on the circular orbit around Earth
        if (orbitDistance > 0.001f)
        {
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector2 orbitPos = earthCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitDistance;
            transform.position = orbitPos;
        }

        // Face outward along orbit
        Quaternion rotation = Quaternion.AngleAxis(currentAngle, Vector3.forward);
        mouse_transform.rotation = rotation;
    }

    void Update()
    {
        MouseAim();

        if (Mouse.current == null) return;

        // Primary Fire: Laser Cannon (Left click, full-auto): requires 10 resources (0 during Overload)
        if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            if (CanAfford(laserCannonCost))
            {
                ConsumeResources(laserCannonCost);
                Instantiate(leftPrefab, firePoint.position, firePoint.rotation);
                nextFireTime = Time.time + fireRate;
            }
        }

        // Chain Lightning cooldown tick
        if (lightningCooldownTimer > 0f)
        {
            lightningCooldownTimer -= Time.deltaTime;
        }

        // Chain Lightning fire (Right click, semi-auto): requires cooldown ready and resources (if enabled)
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (lightningCooldownTimer <= 0f)
            {
                int cost = lightningUsesAmmo ? lightningResourceCost : 0;
                if (CanAfford(cost))
                {
                    ConsumeResources(cost);
                    FireChainLightning();
                    lightningCooldownTimer = lightningCooldown;
                }
            }
        }
    }

    private void FireChainLightning()
    {
        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        // Direction the cannon barrel is pointing outward into space
        Vector2 aimDir = firePoint != null ? (Vector2)firePoint.right : (Vector2)(mouse_transform.rotation * Vector3.right);
        aimDir = aimDir.normalized;

        LayerMask targetMask = lightningTargetLayers.value != 0 ? lightningTargetLayers : LayerMask.GetMask("Enemy");
        if (targetMask.value == 0) targetMask = (1 << 3); // Fallback to layer 3 (Enemy)

        List<Transform> chainedTargets = new List<Transform>();
        List<Vector3> beamWaypoints = new List<Vector3>();
        beamWaypoints.Add(origin);

        // Step 1: Find initial target within lightningRange
        Collider2D[] initialCandidates = Physics2D.OverlapCircleAll(origin, lightningRange, targetMask);
        Collider2D bestInitialTarget = null;
        float bestScore = float.MaxValue;

        foreach (Collider2D col in initialCandidates)
        {
            if (col == null) continue;
            Vector2 toCand = (Vector2)col.transform.position - origin;
            float dist = toCand.magnitude;
            if (dist > lightningRange) continue;

            float angle = Vector2.Angle(aimDir, toCand);

            // Prioritize enemies in the forward cone (up to 75 deg), then by distance
            float anglePenalty = angle <= 75f ? angle * 0.1f : 50f + angle;
            float score = dist + anglePenalty;

            if (score < bestScore)
            {
                bestScore = score;
                bestInitialTarget = col;
            }
        }

        // Also check if any AmmoDrop is aimed at directly
        Collider2D[] allNearby = Physics2D.OverlapCircleAll(origin, lightningRange);
        foreach (Collider2D col in allNearby)
        {
            if (col == null) continue;
            AmmoDrop drop = col.GetComponent<AmmoDrop>();
            if (drop != null)
            {
                Vector2 toDrop = (Vector2)col.transform.position - origin;
                float angle = Vector2.Angle(aimDir, toDrop);
                if (angle <= 30f && toDrop.magnitude < (bestInitialTarget != null ? bestScore : lightningRange))
                {
                    bestInitialTarget = col;
                    break;
                }
            }
        }

        if (bestInitialTarget != null)
        {
            chainedTargets.Add(bestInitialTarget.transform);
            beamWaypoints.Add(bestInitialTarget.transform.position);

            // Step 2: Chain to subsequent enemies up to maxChainTargets
            while (chainedTargets.Count < maxChainTargets)
            {
                Vector2 currentPos = chainedTargets[chainedTargets.Count - 1].position;
                Collider2D[] nextCandidates = Physics2D.OverlapCircleAll(currentPos, chainRadius, targetMask);

                Transform closestNext = null;
                float closestDistSq = float.MaxValue;

                foreach (Collider2D cand in nextCandidates)
                {
                    if (cand == null) continue;
                    if (chainedTargets.Contains(cand.transform)) continue;

                    Rock rock = cand.GetComponent<Rock>();
                    if (rock != null && rock.hp <= 0) continue; // Skip dying/dead rocks

                    float dSq = ((Vector2)cand.transform.position - currentPos).sqrMagnitude;
                    if (dSq < closestDistSq)
                    {
                        closestDistSq = dSq;
                        closestNext = cand.transform;
                    }
                }

                if (closestNext != null)
                {
                    chainedTargets.Add(closestNext);
                    beamWaypoints.Add(closestNext.position);
                }
                else
                {
                    break; // No more enemies within chain radius
                }
            }
        }
        else
        {
            // Missed shot: shoot straight electric bolt forward into space
            beamWaypoints.Add(origin + aimDir * lightningRange);
        }

        // Step 3: Apply damage, knockback, combo, and resource collection to all hit targets
        foreach (Transform target in chainedTargets)
        {
            if (target == null) continue;

            // Check resource drop
            AmmoDrop drop = target.GetComponent<AmmoDrop>();
            if (drop != null)
            {
                drop.Collect();
                continue;
            }

            // Check asteroid
            Rock rock = target.GetComponent<Rock>();
            if (rock != null)
            {
                // Register combo hit
                ComboManager.RegisterHit();

                // Apply outward physical knockback impulse
                Vector2 pushDir = ((Vector2)target.position - origin).normalized;
                rock.Push(pushDir * lightningKnockback);

                // Deal lightning damage
                rock.TakeDamage(lightningDamage);
            }
        }

        // Step 4: Render the crackling chain lightning beam trail
        ChainLightningBeam.Spawn(beamWaypoints.ToArray(), beamDuration, lightningBeamPrefab);

        Debug.Log($"[ChainLightning] Fired lightning! Chained to {chainedTargets.Count} targets. (Cooldown: {lightningCooldown}s)");
    }

    /// <summary>
    /// Adds resources to the player's unified economy pool.
    /// </summary>
    public void AddResources(int amount)
    {
        resources += amount;
        Debug.Log($"[Economy] +{amount} Resources! Current Pool: {resources}");
    }

    /// <summary>
    /// Checks if player can afford an action. Weapon costs are 0 during Overload!
    /// </summary>
    public bool CanAfford(int cost)
    {
        if (ComboManager.Instance != null && ComboManager.Instance.IsOverloadActive)
            return true;

        return resources >= cost;
    }

    /// <summary>
    /// Consumes resources if available. During Overload, firing is completely free!
    /// </summary>
    public bool ConsumeResources(int cost)
    {
        if (ComboManager.Instance != null && ComboManager.Instance.IsOverloadActive)
            return true;

        if (resources >= cost)
        {
            resources -= cost;
            return true;
        }
        return false;
    }

    // Legacy wrappers for backward compatibility
    public void AddRegularAmmo(int amount) => AddResources(amount);
    public void AddRicochetAmmo(int amount) => AddResources(amount);
    public void AddLightningAmmo(int amount) => AddResources(amount);

    public Vector2 GetEarthCenter()
    {
        return earth != null ? (Vector2)earth.position : Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 center = GetEarthCenter();
        float radius = orbitDistance > 0.001f ? orbitDistance : Vector2.Distance(transform.position, center);
        if (radius > 0.001f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, radius);
        }

        // Draw lightning range arc from firePoint
        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.25f);
        Gizmos.DrawWireSphere(origin, lightningRange);
    }

    private void OnGUI()
    {
        if (!showAmmoHUD) return;

        // Clean weapon & ammo HUD at bottom-left corner
        float cardWidth = 240f;
        float cardHeight = 96f;
        float x = 20f;
        float y = Screen.height - cardHeight - 20f;

        GUI.Box(new Rect(x, y, cardWidth, cardHeight), GUIContent.none);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        titleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x + 12, y + 6, cardWidth - 24, 16), "UNIFIED WEAPON ARSENAL", titleStyle);

        // Unified Resources Pool Display
        GUIStyle resStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        resStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);
        GUI.Label(new Rect(x + 12, y + 24, cardWidth - 24, 20), $"RESOURCES: {resources}", resStyle);

        bool isOverload = ComboManager.Instance != null && ComboManager.Instance.IsOverloadActive;

        GUIStyle primaryStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        primaryStyle.normal.textColor = isOverload ? new Color(1f, 0.85f, 0.2f) : (CanAfford(laserCannonCost) ? new Color(0.2f, 0.85f, 1f) : Color.red);
        string primaryCostText = isOverload ? "Laser Cannon (LMB): FREE!" : $"Laser Cannon (LMB): {laserCannonCost} Res";
        GUI.Label(new Rect(x + 12, y + 46, cardWidth - 24, 20), primaryCostText, primaryStyle);

        // Secondary / Lightning indicator & cooldown
        GUIStyle cdStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        if (lightningCooldownTimer > 0f)
        {
            cdStyle.normal.textColor = new Color(1f, 0.45f, 0.2f);
            GUI.Label(new Rect(x + 12, y + 68, 120f, 18), $"RECHARGE: {lightningCooldownTimer:F1}s", cdStyle);

            float barX = x + 130f;
            float barY = y + 73f;
            float barW = cardWidth - 142f;
            float barH = 8f;
            GUI.Box(new Rect(barX, barY, barW, barH), GUIContent.none);
            float progress = 1f - Mathf.Clamp01(lightningCooldownTimer / lightningCooldown);
            if (progress > 0.01f)
            {
                Color prevColor = GUI.color;
                GUI.color = new Color(0.2f, 0.85f, 1f, 0.9f);
                GUI.DrawTexture(new Rect(barX + 1, barY + 1, (barW - 2) * progress, barH - 2), Texture2D.whiteTexture);
                GUI.color = prevColor;
            }
        }
        else
        {
            bool canAffordSec = CanAfford(lightningUsesAmmo ? lightningResourceCost : 0);
            cdStyle.normal.textColor = canAffordSec ? (isOverload ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.4f)) : Color.gray;
            string readyText = canAffordSec ? (isOverload ? "Lightning (RMB): [READY - FREE]" : $"Lightning (RMB): [READY - {lightningResourceCost} Res]") : "[OUT OF RESOURCES]";
            GUI.Label(new Rect(x + 12, y + 68, cardWidth - 24, 18), readyText, cdStyle);
        }
    }
}
