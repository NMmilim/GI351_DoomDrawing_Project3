using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponType
{
    LaserCannon = 0,
    Railgun = 1,
    DisposableMine = 2
}

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

    [Header("Weapon Arsenal (Switch with 1, 2, 3 or Scroll Wheel)")]
    [Tooltip("Currently selected weapon from the arsenal.")]
    [SerializeField] private WeaponType currentWeapon = WeaponType.LaserCannon;
    [Tooltip("Primary Laser Cannon bullet prefab.")]
    [SerializeField] private Transform leftPrefab;
    [Tooltip("Heavy penetration Railgun bullet prefab. If null, a high-velocity piercing beam is procedurally generated.")]
    [SerializeField] private GameObject railgunPrefab;
    [Tooltip("Tactical Disposable Mine prefab. If null, a scaling tactical mine is procedurally generated.")]
    [SerializeField] private GameObject disposableMinePrefab;
    [SerializeField] private Transform firePoint;

    [Header("Weapon Firing Rates")]
    [SerializeField] private float fireRate = 0.25f;
    [SerializeField] private float railgunFireRate = 0.45f;
    [SerializeField] private float mineFireRate = 0.75f;
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

    [Header("Unified Resource Economy & Weapon Costs")]
    [Tooltip("Current unified resource pool. Earned from destroying meteors.")]
    [SerializeField] private int resources = 100;
    [Tooltip("Resource cost to fire the primary Laser Cannon (10 resources). 0 during Overload.")]
    [SerializeField] private int laserCannonCost = 10;
    [Tooltip("Resource cost to fire the heavy Railgun (25 resources). 0 during Overload.")]
    [SerializeField] private int railgunCost = 25;
    [Tooltip("Resource cost to deploy a Disposable Mine (50 resources). 0 during Overload.")]
    [SerializeField] private int disposableMineCost = 50;
    [Tooltip("Resource cost to fire the Chain Lightning secondary weapon.")]
    [SerializeField] private int lightningResourceCost = 25;

    [Tooltip("Show current resource and weapon HUD on screen.")]
    [SerializeField] private bool showAmmoHUD = true;

    private float lightningCooldownTimer = 0f;
    private float currentAngle;
    private float angleVelocity;
    private float earthDamageAlertTimer = 0f;
    private int lastEarthDamageDrain = 0;

    // Public Getters for Economy & Weapon Systems
    public WeaponType CurrentWeapon => currentWeapon;
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
        if (earthDamageAlertTimer > 0f)
        {
            earthDamageAlertTimer -= Time.deltaTime;
        }

        MouseAim();

        HandleWeaponSwitching();

        if (Mouse.current == null) return;

        // Fire active primary weapon based on selection
        HandleWeaponFiring();

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

    private void HandleWeaponSwitching()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) currentWeapon = WeaponType.LaserCannon;
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) currentWeapon = WeaponType.Railgun;
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) currentWeapon = WeaponType.DisposableMine;
        }

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll > 0.1f)
            {
                int prev = ((int)currentWeapon + 2) % 3;
                currentWeapon = (WeaponType)prev;
            }
            else if (scroll < -0.1f)
            {
                int next = ((int)currentWeapon + 1) % 3;
                currentWeapon = (WeaponType)next;
            }
        }
    }

    private void HandleWeaponFiring()
    {
        switch (currentWeapon)
        {
            case WeaponType.LaserCannon:
                if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
                {
                    FireLaserCannon();
                }
                break;

            case WeaponType.Railgun:
                if ((Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.isPressed) && Time.time >= nextFireTime)
                {
                    FireRailgun();
                }
                break;

            case WeaponType.DisposableMine:
                if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextFireTime)
                {
                    FireDisposableMine();
                }
                break;
        }
    }

    private void FireLaserCannon()
    {
        if (CanAfford(laserCannonCost))
        {
            ConsumeResources(laserCannonCost);
            Instantiate(leftPrefab, firePoint.position, firePoint.rotation);
            nextFireTime = Time.time + fireRate;
        }
    }

    private void FireRailgun()
    {
        if (CanAfford(railgunCost))
        {
            ConsumeResources(railgunCost);
            if (railgunPrefab != null)
            {
                Instantiate(railgunPrefab, firePoint.position, firePoint.rotation);
            }
            else
            {
                // Procedural Railgun projectile: spawns and configures RailgunBullet with piercing cyan trail
                Transform slug = Instantiate(leftPrefab, firePoint.position, firePoint.rotation);
                Bullet oldBullet = slug.GetComponent<Bullet>();
                if (oldBullet != null) Destroy(oldBullet);
                slug.gameObject.AddComponent<RailgunBullet>();
            }
            nextFireTime = Time.time + railgunFireRate;
        }
    }

    private void FireDisposableMine()
    {
        if (CanAfford(disposableMineCost))
        {
            ConsumeResources(disposableMineCost);

            Vector2 mouseWorld = Camera.main != null ? (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()) : (Vector2)transform.position + (Vector2)firePoint.right * 4f;

            GameObject mineObj;
            if (disposableMinePrefab != null)
            {
                mineObj = Instantiate(disposableMinePrefab, firePoint.position, Quaternion.identity);
            }
            else
            {
                mineObj = new GameObject("DisposableMine");
                mineObj.transform.position = firePoint.position;
                mineObj.transform.localScale = Vector3.one * 0.45f;
                SpriteRenderer sr = mineObj.AddComponent<SpriteRenderer>();
                sr.color = new Color(1f, 0.45f, 0.1f);
                if (leftPrefab != null)
                {
                    SpriteRenderer bulletSr = leftPrefab.GetComponent<SpriteRenderer>();
                    if (bulletSr != null)
                    {
                        sr.sprite = bulletSr.sprite;
                    }
                }
            }

            DisposableMine mine = mineObj.GetComponent<DisposableMine>();
            if (mine == null) mine = mineObj.AddComponent<DisposableMine>();
            mine.Initialize(mouseWorld);

            nextFireTime = Time.time + mineFireRate;
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

            // Check mine detonation
            DisposableMine mine = target.GetComponent<DisposableMine>();
            if (mine != null && !mine.IsDetonated)
            {
                mine.Detonate();
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

    /// <summary>
    /// Called when an asteroid breaches player defense and impacts Earth.
    /// Drains resources directly from the player's pool.
    /// When resources reach 0, Earth collapses and Game Over is triggered!
    /// </summary>
    public void DrainResourcesFromEarthImpact(int drainAmount, Rock rock)
    {
        resources = Mathf.Max(0, resources - drainAmount);
        earthDamageAlertTimer = 2.0f;
        lastEarthDamageDrain = drainAmount;

        Debug.LogWarning($"[Earth Impact] {rock.name} ({rock.meteorType}, Threat: {rock.threatValue}) struck Earth! Drained {drainAmount} resources. Remaining: {resources}");

        if (resources <= 0)
        {
            Debug.LogError("[Game Over] Earth defenses depleted! Resource pool reached 0 from asteroid impact.");
            if (ComboManager.Instance != null)
            {
                string cause = rock != null ? $"Earth Breached by {rock.meteorType} Asteroid" : "Earth Defenses Depleted";
                ComboManager.Instance.TriggerGameOver(cause);
            }
        }
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
        float cardWidth = 280f;
        float cardHeight = 118f;
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
        GUI.Label(new Rect(x + 12, y + 6, cardWidth - 24, 16), "WEAPON ARSENAL & RESOURCES", titleStyle);

        // Unified Resources Pool Display
        bool isOverload = ComboManager.Instance != null && ComboManager.Instance.IsOverloadActive;
        GUIStyle resStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        if (earthDamageAlertTimer > 0f)
        {
            float pulse = Mathf.PingPong(Time.time * 8f, 1f);
            resStyle.normal.textColor = Color.Lerp(new Color(1f, 0.2f, 0.1f), Color.yellow, pulse);
            string alertText = $"EARTH HIT! -{lastEarthDamageDrain} RES  |  RESOURCES: {resources}";
            GUI.Label(new Rect(x + 12, y + 23, cardWidth - 24, 20), alertText, resStyle);
        }
        else
        {
            if (isOverload)
                resStyle.normal.textColor = new Color(1f, 0.85f, 0.1f);
            else if (resources <= 25)
                resStyle.normal.textColor = new Color(1f, 0.35f, 0.35f);
            else
                resStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);

            string statusSuffix = (resources <= 25 && !isOverload) ? " (DEFENSE CRITICAL!)" : "";
            string resText = isOverload ? "RESOURCES: FREE FIRING (OVERLOAD!)" : $"EARTH ENERGY / RESOURCES: {resources}{statusSuffix}";
            GUI.Label(new Rect(x + 12, y + 23, cardWidth - 24, 20), resText, resStyle);
        }

        // Weapon Selection Slots Row: [1] Laser  [2] Railgun  [3] Mine
        float slotW = (cardWidth - 24f) / 3f;
        float slotY = y + 46f;
        float slotH = 38f;

        DrawWeaponSlot(new Rect(x + 12f, slotY, slotW - 3f, slotH), WeaponType.LaserCannon, "1. LASER", isOverload ? "FREE" : $"{laserCannonCost} Res");
        DrawWeaponSlot(new Rect(x + 12f + slotW, slotY, slotW - 3f, slotH), WeaponType.Railgun, "2. RAILGUN", isOverload ? "FREE" : $"{railgunCost} Res");
        DrawWeaponSlot(new Rect(x + 12f + slotW * 2f, slotY, slotW - 3f, slotH), WeaponType.DisposableMine, "3. MINE", isOverload ? "FREE" : $"{disposableMineCost} Res");

        // Secondary / Lightning indicator & cooldown at bottom
        float secY = y + 89f;
        GUIStyle cdStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        if (lightningCooldownTimer > 0f)
        {
            cdStyle.normal.textColor = new Color(1f, 0.45f, 0.2f);
            GUI.Label(new Rect(x + 12, secY, 130f, 18), $"RMB: {lightningCooldownTimer:F1}s", cdStyle);

            float barX = x + 145f;
            float barY = secY + 5f;
            float barW = cardWidth - 157f;
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
            string readyText = canAffordSec ? (isOverload ? "RMB: [READY - FREE]" : $"RMB Lightning: [READY - {lightningResourceCost} Res]") : "[OUT OF RESOURCES]";
            GUI.Label(new Rect(x + 12, secY, cardWidth - 24, 18), readyText, cdStyle);
        }
    }

    private void DrawWeaponSlot(Rect r, WeaponType type, string slotName, string cost)
    {
        bool isSelected = currentWeapon == type;
        Color prev = GUI.color;

        if (isSelected)
        {
            GUI.color = new Color(0.2f, 0.7f, 1f, 0.35f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        GUI.color = isSelected ? Color.cyan : new Color(0.45f, 0.45f, 0.45f, 0.7f);
        GUI.Box(r, GUIContent.none);

        GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        nameStyle.normal.textColor = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(r.x, r.y + 2f, r.width, 16f), slotName, nameStyle);

        GUIStyle costStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 9,
            alignment = TextAnchor.MiddleCenter
        };
        costStyle.normal.textColor = isSelected ? new Color(1f, 0.9f, 0.3f) : new Color(0.55f, 0.55f, 0.55f);
        GUI.Label(new Rect(r.x, r.y + 18f, r.width, 16f), cost, costStyle);

        GUI.color = prev;
    }
}
