using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to Earth (or references Earth) to spawn incoming asteroid waves
/// in a 360-degree perimeter ring outside the screen.
/// Displays pulsing pre-wave direction indicators pointing toward incoming threat sectors,
/// which disappear as soon as the wave begins spawning.
/// </summary>
public class AsteroidSpawner : MonoBehaviour
{
    public static AsteroidSpawner Instance { get; private set; }

    public enum SpawnerState
    {
        Spawning,
        WaveInProgress,
        Intermission
    }

    [Header("Earth Center Reference")]
    [Tooltip("Center point for the 360-degree spawn circle. Defaults to this transform or (0,0).")]
    [SerializeField] private Transform earthCenter;

    [Header("360-Degree Spawn Perimeter")]
    [Tooltip("Minimum spawn distance from Earth (kept just off-screen).")]
    [SerializeField] private float minSpawnRadius = 11.5f;
    [Tooltip("Maximum spawn distance from Earth.")]
    [SerializeField] private float maxSpawnRadius = 13.5f;

    [Header("Incoming Threat Warning Indicators")]
    [Tooltip("Show visual direction indicators before a wave or chunk spawns.")]
    [SerializeField] private bool showThreatIndicators = true;
    [Tooltip("Distance from Earth center where warning indicators appear (placed within camera viewport).")]
    [SerializeField] private float indicatorRadius = 5.2f;
    [Tooltip("Advance warning duration in seconds before subsequent chunks spawn during an active wave.")]
    [SerializeField] private float chunkWarningDuration = 1.5f;
    [Tooltip("Optional custom prefab for direction indicator. If null, a glowing procedural warning chevron is created.")]
    [SerializeField] private GameObject customIndicatorPrefab;

    [System.Serializable]
    public class MeteorTypeConfig
    {
        public string typeName = "Normal";
        public MeteorType type = MeteorType.Normal;

        [Header("Prefab & Variants")]
        [Tooltip("Specific single prefab for this meteor type. Used if variantPrefabs is empty.")]
        public GameObject prefab;
        [Tooltip("Multiple variant prefabs for this type (e.g. Normal Variant 1, 2, 3). If populated, one is randomly chosen each time this type spawns.")]
        public GameObject[] variantPrefabs;
        [Tooltip("Custom sprite(s) for this meteor type (e.g. from Assets/Meteor). If assigned, randomly applied to SpriteRenderer.")]
        public Sprite[] sprites;

        /// <summary>
        /// Returns a randomly chosen variant prefab if any are assigned, otherwise returns the primary prefab.
        /// </summary>
        public GameObject GetSpawnPrefab()
        {
            if (variantPrefabs != null && variantPrefabs.Length > 0)
            {
                List<GameObject> valid = new List<GameObject>();
                for (int i = 0; i < variantPrefabs.Length; i++)
                {
                    if (variantPrefabs[i] != null) valid.Add(variantPrefabs[i]);
                }
                if (valid.Count > 0)
                {
                    return valid[UnityEngine.Random.Range(0, valid.Count)];
                }
            }
            return prefab;
        }

        [Header("Spawn Probability Weight")]
        [Tooltip("Relative probability weight of this meteor type spawning in chunks.")]
        [Range(0, 100)] public int spawnWeight = 50;

        [Header("Status & Attributes (Inspector Editable)")]
        [Tooltip("Maximum Hit Points.")]
        public int maxHp = 1;

        [Tooltip("Initial inward movement speed toward Earth.")]
        public float initialSpeed = 1.5f;

        [Tooltip("Maximum fall velocity toward Earth. Set high (e.g. 50) for uncapped gravity fall.")]
        public float terminalVelocity = 12.0f;

        [Tooltip("Multiplier on Earth's gravitational acceleration for this meteor type (1 = standard).")]
        public float gravityMultiplier = 1.0f;

        [Tooltip("Scale multiplier for body size.")]
        public float sizeScale = 1.0f;

        [Tooltip("Blast radius multiplier relative to body size (or vortex radius for Magnet).")]
        public float blastRadiusMultiplier = 2.5f;

        [Tooltip("Immune to standard Laser Cannon. Must be destroyed by explosions or Railgun.")]
        public bool immuneToNormalFire = false;

        [Header("Chain Economy")]
        [Tooltip("Flat base resources earned on 1st destruction.")]
        public int baseResourceDrop = 7;

        [Tooltip("Base chain cost added in subsequent chain combo destructions.")]
        public int baseChainCost = 5;

        [Header("Threat Rating & Earth Impact")]
        [Tooltip("Threat value rating of this meteor (Normal: 1, Speedy: 1.5, Tanky: 2, Magnet: 3).")]
        public float threatValue = 1.0f;

        [Tooltip("Resources drained from player pool when an asteroid of this type impacts Earth.")]
        public int earthImpactDrain = 20;

        [Header("Special Ability (Magnet)")]
        [Tooltip("Inward gravitational pull force for Magnet meteor implosion vortex.")]
        public float vortexPullForce = 22f;
    }

    [Header("Meteor Type Status & Prefabs (Inspector Editable)")]
    [SerializeField] private MeteorTypeConfig normalMeteor = new MeteorTypeConfig
    {
        typeName = "Normal",
        type = MeteorType.Normal,
        spawnWeight = 50,
        maxHp = 1,
        initialSpeed = 1.5f,
        terminalVelocity = 12.0f,
        gravityMultiplier = 1.0f,
        sizeScale = 1.0f,
        blastRadiusMultiplier = 2.5f,
        immuneToNormalFire = false,
        baseResourceDrop = 7,
        baseChainCost = 5,
        threatValue = 1.0f,
        earthImpactDrain = 20
    };

    [SerializeField] private MeteorTypeConfig speedyMeteor = new MeteorTypeConfig
    {
        typeName = "Speedy",
        type = MeteorType.Speedy,
        spawnWeight = 25,
        maxHp = 1,
        initialSpeed = 2.4f,
        terminalVelocity = 16.0f,
        gravityMultiplier = 1.1f,
        sizeScale = 0.7f,
        blastRadiusMultiplier = 1.5f,
        immuneToNormalFire = false,
        baseResourceDrop = 3,
        baseChainCost = 10,
        threatValue = 1.5f,
        earthImpactDrain = 30
    };

    [SerializeField] private MeteorTypeConfig tankyMeteor = new MeteorTypeConfig
    {
        typeName = "Tanky",
        type = MeteorType.Tanky,
        spawnWeight = 15,
        maxHp = 3,
        initialSpeed = 1.2f,
        terminalVelocity = 8.0f,
        gravityMultiplier = 0.9f,
        sizeScale = 1.5f,
        blastRadiusMultiplier = 1.5f,
        immuneToNormalFire = true,
        baseResourceDrop = 5,
        baseChainCost = 7,
        threatValue = 2.0f,
        earthImpactDrain = 40
    };

    [SerializeField] private MeteorTypeConfig magnetMeteor = new MeteorTypeConfig
    {
        typeName = "Magnet",
        type = MeteorType.Magnet,
        spawnWeight = 10,
        maxHp = 2,
        initialSpeed = 1.3f,
        terminalVelocity = 9.0f,
        gravityMultiplier = 1.0f,
        sizeScale = 1.0f,
        blastRadiusMultiplier = 5.0f,
        immuneToNormalFire = true,
        baseResourceDrop = 8,
        baseChainCost = 15,
        vortexPullForce = 22f,
        threatValue = 3.0f,
        earthImpactDrain = 60
    };

    [Header("Fallback Asteroid Prefab (Optional)")]
    [Tooltip("General fallback prefab used if a specific meteor type does not have a prefab assigned. If left empty, it automatically falls back to the Normal meteor prefab.")]
    [SerializeField] private GameObject fallbackAsteroidPrefab;

    [Header("Paced Wave Progression (Inspector Editable)")]
    [Tooltip("Current wave number.")]
    [SerializeField] private int currentWave = 1;
    [Tooltip("Break time (in seconds) between waves.")]
    [SerializeField] private float timeBetweenWaves = 4.0f;
    [Tooltip("Whether to use staged enemy introduction (Wave 1-3: Normal only, Wave 4: Speedy intro, Wave 5: Tanky intro, Wave 6: Magnet intro).")]
    [SerializeField] private bool enablePacedIntroduction = true;

    [Header("Early Game Swarm (Waves 1 - 3: Normal Threat Only)")]
    [Tooltip("Drastically increasing spawn counts of Normal asteroids only (Threat 1.0) so the player masters controls and combos.")]
    [SerializeField] private int wave1Count = 6;
    [SerializeField] private int wave2Count = 12;
    [SerializeField] private int wave3Count = 20;

    [Header("Enemy Introduction Phase (Reduced Counts for Learning)")]
    [Tooltip("Wave 4: Speedy debut (Threat 1.5). Reduced count so player can adapt to high-speed targets.")]
    [SerializeField] private int wave4SpeedyIntroCount = 8;
    [Tooltip("Wave 5: Tanky debut (Threat 2.0). Reduced count so player can learn Railgun and explosion tactics.")]
    [SerializeField] private int wave5TankyIntroCount = 7;
    [Tooltip("Wave 6: Magnet debut (Threat 3.0). Reduced count so player can learn vortex implosion mechanics.")]
    [SerializeField] private int wave6MagnetIntroCount = 7;

    [Header("Endless Escalation Phase (Wave 7+)")]
    [Tooltip("Base asteroid count for Wave 7 once all enemies have been introduced.")]
    [SerializeField] private int endlessBaseCount = 10;
    [Tooltip("Incremental asteroid count added for every wave beyond Wave 7.")]
    [SerializeField] private int endlessCountIncreasePerWave = 3;

    [Header("Chunk / Bulk Spawning")]
    [Tooltip("Minimum number of asteroids in a chunk.")]
    [SerializeField] private int minChunkSize = 3;
    [Tooltip("Maximum number of asteroids in a chunk.")]
    [SerializeField] private int maxChunkSize = 5;
    [Tooltip("Delay in seconds between spawning successive chunks during a wave.")]
    [SerializeField] private float timeBetweenChunks = 3.0f;
    [Tooltip("Minimum distance required between any two asteroids spawned in the same chunk.")]
    [SerializeField] private float minSeparationDistance = 2.2f;
    [Tooltip("Radial depth step between successive staggered ranks in a chunk.")]
    [SerializeField] private float radialDepthStep = 1.8f;
    [Tooltip("Angular spread in degrees of the chunk arc (e.g. 25-35 degrees).")]
    [SerializeField] private float chunkSpreadAngle = 30f;

    [Header("Earth Gravitational Physics (Real-World Inverse-Square)")]
    [Tooltip("Standard gravitational parameter (G * M) for Earth. Real-world gravity: a = (earthGravityGM / r^2). Lower = gentle drift, higher = rapid plunge.")]
    [SerializeField] private float earthGravityGM = 260f;
    [Tooltip("Minimum distance clamp for 1/r^2 gravity to prevent infinite acceleration near center. Defaults to Earth radius.")]
    [SerializeField] private float minGravityDistance = 2.35f;
    [Tooltip("Deceleration rate applied when an asteroid is knocked away from Earth so it slows and reverses.")]
    [SerializeField] private float outwardBrakeRate = 10f;
    [Tooltip("Damping on sideways (tangential) orbital velocity so asteroids fall inward rather than orbiting forever.")]
    [SerializeField] private float tangentialDamping = 1.0f;
    [Tooltip("Radius of Earth where impact occurs (if <= 0, auto-detected from Earth collider).")]
    [SerializeField] private float earthRadius = 2.35f;

    [Header("Flocking Formation Spacing")]
    [Tooltip("Distance threshold within which nearby asteroids exert a gentle mutual repulsion force.")]
    [SerializeField] private float separationRadius = 2.0f;
    [Tooltip("Strength of the gentle repulsion force between nearby asteroids.")]
    [SerializeField] private float separationForce = 15f;
    [Tooltip("Random initial space tumble in degrees per second.")]
    [SerializeField] private float randomTumbleSpeed = 35f;

    [Header("Sandbox / Domino Test Bed (Big Boss Prototype)")]
    [Tooltip("MASTER SWITCH. ON: Earth is indestructible (no resource drain, no Game Over) and every wave is one big snail-pace swarm for testing the chain reaction. OFF: the normal game.")]
    [SerializeField] private bool sandboxMode = true;
    [Tooltip("Spawn only Normal meteors. Speedy, Tanky and Magnet are shelved for now. Works with or without sandbox mode.")]
    [SerializeField] private bool normalMeteorsOnly = true;
    [Tooltip("Sandbox: minimum meteors per swarm.")]
    [SerializeField] private int swarmMinCount = 20;
    [Tooltip("Sandbox: maximum meteors per swarm.")]
    [SerializeField] private int swarmMaxCount = 30;
    [Tooltip("When true in Sandbox, forces asteroids to crawl at snail speed. Set to FALSE if you want Sandbox testing with your custom meteor speeds!")]
    [SerializeField] private bool useSwarmSnailSpeed = false;
    [Tooltip("Sandbox: initial inward speed of swarm meteors when useSwarmSnailSpeed is true.")]
    [SerializeField] private float swarmInwardSpeed = 0.25f;
    [Tooltip("Sandbox: top fall speed of swarm meteors when useSwarmSnailSpeed is true.")]
    [SerializeField] private float swarmMaxSpeed = 0.4f;
    [Tooltip("Sandbox: gravity scaling when useSwarmSnailSpeed is true so gravity doesn't accelerate the snail swarm.")]
    [SerializeField] private float swarmGravityScale = 0.05f;
    [Tooltip("Sandbox: minimum distance between meteors in the swarm. Smaller = denser = longer chains. Shard reach is roughly 6.6 units by default.")]
    [SerializeField] private float swarmSpacing = 2.0f;
    [Tooltip("Sandbox: scales the meteors' mutual repulsion (1 = normal, 0 = none). Kept low so a packed swarm isn't shoved apart faster than snail pace.")]
    [Range(0f, 1f)] [SerializeField] private float swarmSeparationMultiplier = 0.1f;

    [Header("On-Screen HUD")]
    [SerializeField] private bool showWaveHUD = true;

    // Events
    public static event Action<int> OnWaveStarted;
    public static event Action<int> OnWaveCleared;

    // Runtime state
    private SpawnerState currentState = SpawnerState.Intermission;
    private float waveTimer = 0f;
    private int spawnedInCurrentWave = 0;
    private int targetWaveCount = 0;
    private int plannedWaveCount = 0;
    private float lastSpawnAngle = 0f;
    private float nextSpawnAngle = 0f;
    private readonly List<GameObject> activeAsteroids = new List<GameObject>();
    private readonly List<GameObject> activeIndicators = new List<GameObject>();

    public int CurrentWave => currentWave;
    public SpawnerState CurrentState => currentState;
    public int ActiveAsteroidCount => activeAsteroids.Count;
    public float NextSpawnAngle => nextSpawnAngle;

    public MeteorTypeConfig NormalConfig => normalMeteor;
    public MeteorTypeConfig SpeedyConfig => speedyMeteor;
    public MeteorTypeConfig TankyConfig => tankyMeteor;
    public MeteorTypeConfig MagnetConfig => magnetMeteor;

    public int NormalWeight => normalMeteor != null ? normalMeteor.spawnWeight : 50;
    public int SpeedyWeight => speedyMeteor != null ? speedyMeteor.spawnWeight : 25;
    public int TankyWeight => tankyMeteor != null ? tankyMeteor.spawnWeight : 15;
    public int MagnetWeight => magnetMeteor != null ? magnetMeteor.spawnWeight : 10;

    // Physics & Earth accessors
    public Transform EarthCenter => earthCenter != null ? earthCenter : transform;
    public float EarthGravityGM => earthGravityGM;
    public float MinGravityDistance => minGravityDistance;
    public float OutwardBrakeRate => outwardBrakeRate;
    public float TangentialDamping => tangentialDamping;
    public float EarthRadius => earthRadius;
    public float SeparationRadius => separationRadius;
    public float SeparationForce => separationForce;
    public float RandomTumbleSpeed => randomTumbleSpeed;
    public bool UseSwarmSnailSpeed => useSwarmSnailSpeed;
    public int SwarmMinCount => swarmMinCount;
    public int SwarmMaxCount => swarmMaxCount;
    public float SwarmInwardSpeed => swarmInwardSpeed;
    public float SwarmMaxSpeed => swarmMaxSpeed;
    public float SwarmGravityScale => swarmGravityScale;
    public float SwarmSeparationMultiplier => swarmSeparationMultiplier;
    public bool ShowWaveHUD { get => showWaveHUD; set => showWaveHUD = value; }
    public float WaveTimer => waveTimer;
    public float TimeBetweenWaves => timeBetweenWaves;
    public int TargetWaveCount => targetWaveCount;
    public string CurrentThreatDescription => GetWaveThreatDescription(currentWave);

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(this);

        if (earthCenter == null)
            earthCenter = transform;

        if (earthRadius <= 0f)
        {
            CircleCollider2D col = earthCenter != null ? earthCenter.GetComponent<CircleCollider2D>() : GetComponent<CircleCollider2D>();
            if (col != null)
                earthRadius = col.radius * (earthCenter != null ? earthCenter.lossyScale.x : transform.lossyScale.x);
            else
                earthRadius = 2.35f;
        }

        SandboxMode.Enabled = sandboxMode;
    }

    private void Start()
    {
        if (fallbackAsteroidPrefab == null)
        {
            fallbackAsteroidPrefab = normalMeteor.GetSpawnPrefab();
        }

        // Start countdown to Wave 1 with threat direction indicator
        StartIntermission(timeBetweenWaves);
    }

    private void Update()
    {
        activeAsteroids.RemoveAll(rock => rock == null);

        switch (currentState)
        {
            case SpawnerState.Intermission:
                waveTimer -= Time.deltaTime;
                if (waveTimer <= 0f)
                {
                    StartNextWave();
                }
                break;

            case SpawnerState.WaveInProgress:
                if (activeAsteroids.Count == 0)
                {
                    OnWaveCleared?.Invoke(currentWave);
                    currentWave++;
                    StartIntermission(timeBetweenWaves);
                }
                break;
        }
    }

    private void StartNextWave()
    {
        // Indicators disappear when the wave starts!
        ClearIndicators();

        targetWaveCount = plannedWaveCount > 0 ? plannedWaveCount : CalculateWaveAsteroidCount(currentWave);
        spawnedInCurrentWave = 0;
        currentState = SpawnerState.Spawning;

        Debug.Log($"[Spawner] --- STARTING WAVE {currentWave} ({targetWaveCount} Asteroids from {nextSpawnAngle:F0}°) ---");
        OnWaveStarted?.Invoke(currentWave);

        StartCoroutine(SpawnWaveRoutine());
    }

    private IEnumerator SpawnWaveRoutine()
    {
        while (spawnedInCurrentWave < targetWaveCount)
        {
            int remaining = targetWaveCount - spawnedInCurrentWave;
            // Sandbox: the whole wave arrives as ONE dense swarm instead of 3-5 meteor chunks
            int chunkSize = sandboxMode ? remaining : UnityEngine.Random.Range(minChunkSize, maxChunkSize + 1);
            if (chunkSize > remaining)
            {
                chunkSize = remaining;
            }

            // Spawn the incoming chunk at the pre-indicated angle
            SpawnChunk(chunkSize, nextSpawnAngle);
            spawnedInCurrentWave += chunkSize;

            // If there are more chunks to spawn in this wave, show advance indicator for next chunk
            if (spawnedInCurrentWave < targetWaveCount)
            {
                float angleOffset = UnityEngine.Random.Range(60f, 300f);
                nextSpawnAngle = (lastSpawnAngle + angleOffset) % 360f;

                float waitTime = timeBetweenChunks;
                float warningDelay = Mathf.Max(0.5f, waitTime - chunkWarningDuration);

                yield return new WaitForSeconds(warningDelay);

                // Show advance threat indicator for subsequent chunk
                ShowDirectionIndicator(nextSpawnAngle);

                yield return new WaitForSeconds(chunkWarningDuration);

                // Indicator disappears right as chunk starts spawning
                ClearIndicators();
            }
        }

        currentState = SpawnerState.WaveInProgress;
    }

    private void SpawnChunk(int chunkSize, float chunkCenterAngle)
    {
        if (chunkSize <= 0) return;

        Vector3 center = earthCenter != null ? earthCenter.position : Vector3.zero;
        lastSpawnAngle = chunkCenterAngle;

        // Sandbox: one big dense cluster instead of a staggered arc
        if (sandboxMode)
        {
            SpawnSwarm(chunkSize, chunkCenterAngle, center);
            return;
        }

        float baseRadius = UnityEngine.Random.Range(minSpawnRadius, maxSpawnRadius);
        List<Vector3> placedPositions = new List<Vector3>();

        for (int i = 0; i < chunkSize; i++)
        {
            Vector3 spawnPos = CalculateChunkPosition(i, chunkSize, chunkCenterAngle, baseRadius, center, placedPositions);
            if (SpawnAsteroidAt(spawnPos, false) == null) continue;
            placedPositions.Add(spawnPos);
        }

        Debug.Log($"[Spawner] Spawned chunk of {placedPositions.Count} asteroids at angle {chunkCenterAngle:F1}° (Base Radius: {baseRadius:F1})");
    }

    /// <summary>
    /// Rolls a meteor type, instantiates it at the given position and applies its configuration.
    /// Returns null if no prefab is available. snailSwarm applies the sandbox crawl-speed override.
    /// </summary>
    private GameObject SpawnAsteroidAt(Vector3 spawnPos, bool snailSwarm)
    {
        MeteorTypeConfig config = RollMeteorConfig();
        GameObject prefabToSpawn = (config != null) ? config.GetSpawnPrefab() : null;
        if (prefabToSpawn == null)
        {
            prefabToSpawn = fallbackAsteroidPrefab != null ? fallbackAsteroidPrefab : normalMeteor.GetSpawnPrefab();
        }

        if (prefabToSpawn == null)
        {
            Debug.LogWarning("[Spawner] No asteroid prefab assigned to spawn!");
            return null;
        }

        GameObject asteroid = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        Rock rock = asteroid.GetComponent<Rock>();
        if (rock != null)
        {
            bool snail = snailSwarm && useSwarmSnailSpeed;
            rock.InitFromSpawner(this, config, snail);
        }

        activeAsteroids.Add(asteroid);
        return asteroid;
    }

    /// <summary>
    /// Sandbox: spawns `count` meteors as one dense, roughly circular blob in the sector at centerAngle.
    /// Rocks are packed at least swarmSpacing apart so each has several neighbors within shard reach.
    /// </summary>
    private void SpawnSwarm(int count, float centerAngle, Vector3 center)
    {
        // Size the blob so `count` rocks fit at swarmSpacing (hex packing needs ~0.87 area/rock, +20% slack)
        float spacing = Mathf.Max(0.5f, swarmSpacing);
        float blobRadius = Mathf.Sqrt(count * spacing * spacing * 0.87f / Mathf.PI) * 1.2f;

        // Push the whole blob out so its nearest edge starts at minSpawnRadius
        float rad = centerAngle * Mathf.Deg2Rad;
        Vector3 blobCenter = center + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * (minSpawnRadius + blobRadius);

        List<Vector3> placed = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = FindSwarmPosition(blobCenter, blobRadius, spacing, placed);
            if (SpawnAsteroidAt(pos, true) == null) continue;
            placed.Add(pos);
        }

        Debug.Log($"[Spawner] SANDBOX swarm: {placed.Count} meteors in a blob of radius {blobRadius:F1} at angle {centerAngle:F0}");
    }

    /// <summary>
    /// Random point inside the blob that keeps at least `spacing` from every placed meteor.
    /// If the blob is too tight to fit another rock, it grows slightly until one fits.
    /// </summary>
    private static Vector3 FindSwarmPosition(Vector3 blobCenter, float blobRadius, float spacing, List<Vector3> placed)
    {
        float sqrSpacing = spacing * spacing;
        float r = blobRadius;

        for (int attempt = 0; attempt < 60; attempt++)
        {
            Vector3 candidate = blobCenter + (Vector3)(UnityEngine.Random.insideUnitCircle * r);

            bool clear = true;
            for (int i = 0; i < placed.Count; i++)
            {
                if (((Vector2)candidate - (Vector2)placed[i]).sqrMagnitude < sqrSpacing)
                {
                    clear = false;
                    break;
                }
            }

            if (clear) return candidate;
            if (attempt % 15 == 14) r *= 1.15f;
        }

        return blobCenter + (Vector3)(UnityEngine.Random.insideUnitCircle * r);
    }

    /// <summary>
    /// Displays a visual threat indicator showing the direction enemies will approach from.
    /// </summary>
    public void ShowDirectionIndicator(float angle)
    {
        if (!showThreatIndicators) return;

        ClearIndicators();

        Vector3 center = earthCenter != null ? earthCenter.position : Vector3.zero;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 pos = center + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * indicatorRadius;

        // Facing directly inward toward Earth center
        Vector3 inwardDir = (center - pos).normalized;
        float rotZ = Mathf.Atan2(inwardDir.y, inwardDir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, rotZ);

        GameObject indicator;
        if (customIndicatorPrefab != null)
        {
            indicator = Instantiate(customIndicatorPrefab, pos, rot);
        }
        else
        {
            indicator = CreateProceduralChevron(pos, rot);
        }

        activeIndicators.Add(indicator);
    }

    /// <summary>
    /// Clears and removes all active threat direction indicators when the wave begins.
    /// </summary>
    public void ClearIndicators()
    {
        foreach (GameObject ind in activeIndicators)
        {
            if (ind != null)
            {
                Destroy(ind);
            }
        }
        activeIndicators.Clear();
    }

    private GameObject CreateProceduralChevron(Vector3 pos, Quaternion rot)
    {
        GameObject parentObj = new GameObject("ThreatDirectionIndicator");
        parentObj.transform.position = pos;
        parentObj.transform.rotation = rot;

        // Double chevron layout (two nested arrows pointing toward Earth)
        CreateChevronLine(parentObj, new Vector3(-0.35f, 0f, 0f), 0.55f);
        CreateChevronLine(parentObj, new Vector3(0.2f, 0f, 0f), 0.75f);

        parentObj.AddComponent<ThreatIndicatorPulse>();

        return parentObj;
    }

    private static void CreateChevronLine(GameObject parent, Vector3 localOffset, float scale)
    {
        GameObject child = new GameObject("Chevron");
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = localOffset;

        LineRenderer lr = child.AddComponent<LineRenderer>();
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null) spriteShader = Shader.Find("Unlit/Color");
        lr.material = new Material(spriteShader);

        Color warningColor = new Color(1f, 0.25f, 0.05f, 0.95f);
        lr.startColor = warningColor;
        lr.endColor = warningColor;
        lr.startWidth = 0.16f;
        lr.endWidth = 0.16f;
        lr.useWorldSpace = false;
        lr.positionCount = 3;

        float w = 0.45f * scale;
        float h = 0.45f * scale;
        lr.SetPosition(0, new Vector3(-w, h, 0f));
        lr.SetPosition(1, new Vector3(w, 0f, 0f));
        lr.SetPosition(2, new Vector3(-w, -h, 0f));
    }

    private Vector3 CalculateChunkPosition(
        int index,
        int totalInChunk,
        float centerAngle,
        float baseRadius,
        Vector3 center,
        List<Vector3> existingPositions)
    {
        float radius = baseRadius + index * radialDepthStep + UnityEngine.Random.Range(-0.2f, 0.2f);

        float angleFraction = 0f;
        if (index > 0)
        {
            int rank = (index + 1) / 2;
            float sign = (index % 2 == 1) ? 1f : -1f;
            float rankScale = Mathf.Min(0.5f, rank * 0.35f);
            angleFraction = sign * rankScale;
        }

        float angleOffset = angleFraction * chunkSpreadAngle + UnityEngine.Random.Range(-2.5f, 2.5f);
        float angle = (centerAngle + angleOffset) % 360f;

        float rad = angle * Mathf.Deg2Rad;
        Vector3 candidatePos = center + new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            bool tooClose = false;
            foreach (Vector3 existing in existingPositions)
            {
                if (Vector2.Distance(candidatePos, existing) < minSeparationDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                break;

            radius += minSeparationDistance * 0.8f;
            angle += UnityEngine.Random.Range(-6f, 6f);
            rad = angle * Mathf.Deg2Rad;
            candidatePos = center + new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
        }

        return candidatePos;
    }


    /// <summary>
    /// Calculates the number of asteroids to spawn for the specified wave:
    /// - Waves 1-3 (Early Game): Threat 1 only (Normal), count drastically increases (6 -> 12 -> 20) to learn basic shooting & combos.
    /// - Wave 4 (Speedy Intro): Reduced count (8) so player can adapt to fast targets.
    /// - Wave 5 (Tanky Intro): Reduced count (7) so player can practice Railgun & explosions.
    /// - Wave 6 (Magnet Intro): Reduced count (7) so player can understand vortex mechanics.
    /// - Wave 7+ (Endless): Count increases incrementally wave by wave.
    /// </summary>
    public int CalculateWaveAsteroidCount(int wave)
    {
        if (!enablePacedIntroduction)
        {
            return wave1Count + (wave - 1) * endlessCountIncreasePerWave;
        }

        switch (wave)
        {
            case 1: return wave1Count;
            case 2: return wave2Count;
            case 3: return wave3Count;
            case 4: return wave4SpeedyIntroCount;
            case 5: return wave5TankyIntroCount;
            case 6: return wave6MagnetIntroCount;
            default:
                // Wave 7+: Increases gradually
                return endlessBaseCount + (wave - 7) * endlessCountIncreasePerWave;
        }
    }

    /// <summary>
    /// Selects an asteroid configuration based on wave progression and threat gating:
    /// - Wave 1-3 (Early Game): Threat 1.0 only (Normal asteroids).
    /// - Wave 4: Speedy debut (Threat 1.5). Normal & Speedy only.
    /// - Wave 5: Tanky debut (Threat 2.0). Normal, Speedy & Tanky.
    /// - Wave 6: Magnet debut (Threat 3.0). All 4 types with Magnet featured.
    /// - Wave 7+: Endless mode - all 4 enemy types active, weights and chances scale with wave progress.
    /// </summary>
    public MeteorTypeConfig RollMeteorConfig()
    {
        // Speedy / Tanky / Magnet are shelved for the prototype
        if (normalMeteorsOnly) return normalMeteor;
        if (!enablePacedIntroduction)
        {
            return RollFromWeights(
                normalMeteor != null ? normalMeteor.spawnWeight : 50,
                speedyMeteor != null ? speedyMeteor.spawnWeight : 25,
                tankyMeteor != null ? tankyMeteor.spawnWeight : 15,
                magnetMeteor != null ? magnetMeteor.spawnWeight : 10
            );
        }

        // Wave 1-3: Early game - Only Normal Asteroids (Threat 1.0)
        if (currentWave <= 3)
        {
            return normalMeteor;
        }

        // Wave 4: Speedy debut - Normal & Speedy only
        if (currentWave == 4)
        {
            int wNormal = Mathf.Max(1, normalMeteor != null ? normalMeteor.spawnWeight : 50);
            int wSpeedy = Mathf.Max(1, speedyMeteor != null ? Mathf.Max(speedyMeteor.spawnWeight, 45) : 45);
            return UnityEngine.Random.Range(0, wNormal + wSpeedy) < wNormal ? normalMeteor : speedyMeteor;
        }

        // Wave 5: Tanky debut - Normal, Speedy, and Tanky
        if (currentWave == 5)
        {
            int wNormal = Mathf.Max(1, normalMeteor != null ? normalMeteor.spawnWeight : 50);
            int wSpeedy = Mathf.Max(1, speedyMeteor != null ? speedyMeteor.spawnWeight : 25);
            int wTanky = Mathf.Max(1, tankyMeteor != null ? Mathf.Max(tankyMeteor.spawnWeight, 35) : 35);
            int roll = UnityEngine.Random.Range(0, wNormal + wSpeedy + wTanky);
            if (roll < wNormal) return normalMeteor;
            roll -= wNormal;
            if (roll < wSpeedy) return speedyMeteor;
            return tankyMeteor;
        }

        // Wave 6: Magnet debut - All 4 types with Magnet featured
        if (currentWave == 6)
        {
            int wNormal = Mathf.Max(1, normalMeteor != null ? normalMeteor.spawnWeight : 50);
            int wSpeedy = Mathf.Max(1, speedyMeteor != null ? speedyMeteor.spawnWeight : 25);
            int wTanky = Mathf.Max(1, tankyMeteor != null ? tankyMeteor.spawnWeight : 20);
            int wMagnet = Mathf.Max(1, magnetMeteor != null ? Mathf.Max(magnetMeteor.spawnWeight, 30) : 30);
            int roll = UnityEngine.Random.Range(0, wNormal + wSpeedy + wTanky + wMagnet);
            if (roll < wNormal) return normalMeteor;
            roll -= wNormal;
            if (roll < wSpeedy) return speedyMeteor;
            roll -= wSpeedy;
            if (roll < wTanky) return tankyMeteor;
            return magnetMeteor;
        }

        // Wave 7+: Endless mode - all 4 enemy types active
        // Chances and weights increase little by little with wave progress
        int baseWNormal = Mathf.Max(0, normalMeteor != null ? normalMeteor.spawnWeight : 50);
        int baseWSpeedy = Mathf.Max(0, speedyMeteor != null ? speedyMeteor.spawnWeight : 25);
        int baseWTanky = Mathf.Max(0, tankyMeteor != null ? tankyMeteor.spawnWeight : 15);
        int baseWMagnet = Mathf.Max(0, magnetMeteor != null ? magnetMeteor.spawnWeight : 10);

        int waveBonus = currentWave - 7;
        int scaledSpeedy = baseWSpeedy + waveBonus * 1;
        int scaledTanky = baseWTanky + waveBonus * 1;
        int scaledMagnet = baseWMagnet + (waveBonus / 2);

        return RollFromWeights(baseWNormal, scaledSpeedy, scaledTanky, scaledMagnet);
    }

    private MeteorTypeConfig RollFromWeights(int wNormal, int wSpeedy, int wTanky, int wMagnet)
    {
        int totalWeight = Mathf.Max(0, wNormal) + Mathf.Max(0, wSpeedy) + Mathf.Max(0, wTanky) + Mathf.Max(0, wMagnet);
        if (totalWeight <= 0) return normalMeteor;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        if (roll < wNormal) return normalMeteor;
        roll -= wNormal;
        if (roll < wSpeedy) return speedyMeteor;
        roll -= wSpeedy;
        if (roll < wTanky) return tankyMeteor;
        return magnetMeteor;
    }

    public MeteorType RollMeteorType()
    {
        MeteorTypeConfig cfg = RollMeteorConfig();
        return cfg != null ? cfg.type : MeteorType.Normal;
    }

    public string GetWaveThreatDescription(int wave)
    {
        if (sandboxMode) return "SANDBOX: DOMINO SWARM TEST (EARTH INVINCIBLE)";
        if (!enablePacedIntroduction) return "ALL THREATS ACTIVE";
        switch (wave)
        {
            case 1: return "STAGE 1: NORMAL THREAT (LEARN COMBOS)";
            case 2: return "STAGE 2: SWARM INTENSIFIES";
            case 3: return "STAGE 3: DENSE SWARM (FINAL DRILL)";
            case 4: return "NEW THREAT: SPEEDY METEORS (FAST)";
            case 5: return "NEW THREAT: TANKY METEORS (RAILGUN/EXPLOSION)";
            case 6: return "NEW THREAT: MAGNET VORTEX (GRAVITY PULL)";
            default: return "ENDLESS SURVIVAL: ALL THREATS ESCALATING";
        }
    }

    private void StartIntermission(float duration)
    {
        currentState = SpawnerState.Intermission;
        waveTimer = duration;

        // Decide the upcoming wave's size now so the HUD countdown shows a stable number
        plannedWaveCount = sandboxMode
            ? UnityEngine.Random.Range(Mathf.Min(swarmMinCount, swarmMaxCount), Mathf.Max(swarmMinCount, swarmMaxCount) + 1)
            : CalculateWaveAsteroidCount(currentWave);

        // Choose incoming threat angle for the upcoming wave and display warning indicator!
        float angleOffset = UnityEngine.Random.Range(60f, 300f);
        nextSpawnAngle = (lastSpawnAngle + angleOffset) % 360f;

        ShowDirectionIndicator(nextSpawnAngle);
    }

    private void OnDestroy()
    {
        ClearIndicators();
    }

#if UNITY_EDITOR
    // Lets you flip the Sandbox checkbox and adjust physics during Play Mode and see it take effect immediately
    private void OnValidate()
    {
        SandboxMode.Enabled = sandboxMode;
        if (Application.isPlaying)
        {
            UpdateActiveAsteroidsPhysics();
        }
    }

    /// <summary>
    /// Propagates live inspector changes to all currently active asteroids in the scene.
    /// </summary>
    public void UpdateActiveAsteroidsPhysics()
    {
        for (int i = activeAsteroids.Count - 1; i >= 0; i--)
        {
            if (activeAsteroids[i] == null)
            {
                activeAsteroids.RemoveAt(i);
                continue;
            }
            Rock rock = activeAsteroids[i].GetComponent<Rock>();
            if (rock != null && !rock.IsDead)
            {
                rock.UpdatePhysicsSettings(
                    earthCenter: EarthCenter,
                    earthRadius: earthRadius,
                    earthGravityGM: earthGravityGM,
                    minGravityDistance: minGravityDistance,
                    outwardBrakeRate: outwardBrakeRate,
                    tangentialDamping: tangentialDamping,
                    separationRadius: separationRadius,
                    separationForce: separationForce
                );
            }
        }
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Vector3 center = earthCenter != null ? earthCenter.position : transform.position;

        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(center, minSpawnRadius);
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.3f);
        Gizmos.DrawWireSphere(center, maxSpawnRadius);

        // Draw indicator radius ring
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(center, indicatorRadius);
    }

    private void OnGUI()
    {
        if (!showWaveHUD) return;

        float bannerWidth = 340f;
        float bannerHeight = 52f;
        float x = (Screen.width - bannerWidth) * 0.5f;
        float y = 15f;

        GUI.Box(new Rect(x, y, bannerWidth, bannerHeight), GUIContent.none);

        GUIStyle waveStyle = HudStyles.Label(17, FontStyle.Bold, TextAnchor.MiddleCenter);

        string statusText;
        if (currentState == SpawnerState.Intermission)
        {
            float pulse = Mathf.PingPong(Time.time * 4f, 1f);
            waveStyle.normal.textColor = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.3f, 0.15f), pulse);
            statusText = $"Wave {currentWave} in {Mathf.CeilToInt(waveTimer)}s ({plannedWaveCount} Meteors)";
        }
        else
        {
            waveStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
            statusText = $"WAVE {currentWave} ({activeAsteroids.Count} Left / {targetWaveCount} Total)";
        }

        GUI.Label(new Rect(x, y + 3, bannerWidth, 22), statusText, waveStyle);

        string threatDesc = GetWaveThreatDescription(currentWave);
        bool isIntroWave = currentWave >= 4 && currentWave <= 6;
        GUIStyle subStyle = HudStyles.Label(11, isIntroWave ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleCenter);
        subStyle.normal.textColor = isIntroWave ? new Color(1f, 0.8f, 0.2f) : new Color(0.85f, 0.85f, 0.85f);

        string subText;
        if (currentState == SpawnerState.Intermission)
        {
            subText = $"⚠ SECTOR: {Mathf.RoundToInt(nextSpawnAngle)}°  |  {threatDesc}";
        }
        else
        {
            subText = threatDesc;
        }

        GUI.Label(new Rect(x, y + 26, bannerWidth, 20), subText, subStyle);
    }
}

/// <summary>
/// Lightweight pulsing animation component for incoming wave/threat direction indicators.
/// </summary>
public class ThreatIndicatorPulse : MonoBehaviour
{
    private LineRenderer[] lines;
    private Vector3 baseScale;

    private void Awake()
    {
        lines = GetComponentsInChildren<LineRenderer>();
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float t = Mathf.PingPong(Time.time * 5.5f, 1f);
        transform.localScale = baseScale * Mathf.Lerp(0.85f, 1.25f, t);

        Color col = Color.Lerp(new Color(1f, 0.75f, 0.1f, 0.75f), new Color(1f, 0.15f, 0.05f, 1f), t);
        if (lines != null)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null)
                {
                    lines[i].startColor = col;
                    lines[i].endColor = col;
                }
            }
        }
    }
}
