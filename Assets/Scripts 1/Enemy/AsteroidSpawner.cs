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

    [Header("Asteroid Prefabs & Weighted Mix")]
    [Tooltip("Weak asteroid prefab (destroyed in 1 hit).")]
    [SerializeField] private GameObject weakAsteroidPrefab;
    [Tooltip("Chance weighting for Weak asteroid (default 70%).")]
    [Range(0, 100)] [SerializeField] private int weakWeight = 70;

    [Tooltip("Medium asteroid prefab (takes multiple hits).")]
    [SerializeField] private GameObject mediumAsteroidPrefab;
    [Tooltip("Chance weighting for Medium asteroid (default 30%).")]
    [Range(0, 100)] [SerializeField] private int mediumWeight = 30;

    [Header("Meteor Type Spawn Weights (Normal, Speedy, Tanky, Magnet)")]
    [Tooltip("Weight for Normal meteors (medium size, 2.5x blast).")]
    [SerializeField] private int normalWeight = 50;
    [Tooltip("Weight for Speedy meteors (fast, small, 1.5x blast).")]
    [SerializeField] private int speedyWeight = 25;
    [Tooltip("Weight for Tanky meteors (slow, large, immune to normal fire).")]
    [SerializeField] private int tankyWeight = 15;
    [Tooltip("Weight for Magnet meteors (slow, immune to normal fire, vortex pull).")]
    [SerializeField] private int magnetWeight = 10;

    [Header("Wave Progression")]
    [Tooltip("Current wave number.")]
    [SerializeField] private int currentWave = 1;
    [Tooltip("Number of asteroids in Wave 1.")]
    [SerializeField] private int baseWaveCount = 6;
    [Tooltip("Additional asteroids added per wave.")]
    [SerializeField] private int countIncreasePerWave = 3;
    [Tooltip("Break time (in seconds) between waves.")]
    [SerializeField] private float timeBetweenWaves = 4.0f;

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
    private float lastSpawnAngle = 0f;
    private float nextSpawnAngle = 0f;
    private readonly List<GameObject> activeAsteroids = new List<GameObject>();
    private readonly List<GameObject> activeIndicators = new List<GameObject>();

    public int CurrentWave => currentWave;
    public SpawnerState CurrentState => currentState;
    public int ActiveAsteroidCount => activeAsteroids.Count;
    public float NextSpawnAngle => nextSpawnAngle;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(this);

        if (earthCenter == null)
            earthCenter = transform;
    }

    private void Start()
    {
        if (weakAsteroidPrefab == null)
            weakAsteroidPrefab = Resources.Load<GameObject>("Rock2");
        if (mediumAsteroidPrefab == null)
            mediumAsteroidPrefab = Resources.Load<GameObject>("Rock1");

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

        targetWaveCount = baseWaveCount + (currentWave - 1) * countIncreasePerWave;
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
            int chunkSize = UnityEngine.Random.Range(minChunkSize, maxChunkSize + 1);
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

        float baseRadius = UnityEngine.Random.Range(minSpawnRadius, maxSpawnRadius);
        List<Vector3> placedPositions = new List<Vector3>();

        for (int i = 0; i < chunkSize; i++)
        {
            GameObject prefabToSpawn = PickAsteroidPrefab();
            if (prefabToSpawn == null)
            {
                Debug.LogWarning("[Spawner] No asteroid prefab assigned to spawn!");
                continue;
            }

            Vector3 spawnPos = CalculateChunkPosition(i, chunkSize, chunkCenterAngle, baseRadius, center, placedPositions);
            placedPositions.Add(spawnPos);

            MeteorType rolledType = RollMeteorType();
            GameObject asteroid = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            Rock rock = asteroid.GetComponent<Rock>();
            if (rock != null)
            {
                rock.meteorType = rolledType;
                rock.ApplyMeteorTypeConfiguration();
            }
            activeAsteroids.Add(asteroid);
        }

        Debug.Log($"[Spawner] Spawned chunk of {placedPositions.Count} asteroids at angle {chunkCenterAngle:F1}° (Base Radius: {baseRadius:F1})");
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

    private GameObject PickAsteroidPrefab()
    {
        int totalWeight = weakWeight + mediumWeight;
        if (totalWeight <= 0) return weakAsteroidPrefab;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        if (roll < weakWeight)
        {
            return weakAsteroidPrefab != null ? weakAsteroidPrefab : mediumAsteroidPrefab;
        }
        else
        {
            return mediumAsteroidPrefab != null ? mediumAsteroidPrefab : weakAsteroidPrefab;
        }
    }

    private MeteorType RollMeteorType()
    {
        int totalWeight = normalWeight + speedyWeight + tankyWeight + magnetWeight;
        if (totalWeight <= 0) return MeteorType.Normal;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        if (roll < normalWeight) return MeteorType.Normal;
        roll -= normalWeight;
        if (roll < speedyWeight) return MeteorType.Speedy;
        roll -= speedyWeight;
        if (roll < tankyWeight) return MeteorType.Tanky;
        return MeteorType.Magnet;
    }

    private void StartIntermission(float duration)
    {
        currentState = SpawnerState.Intermission;
        waveTimer = duration;

        // Choose incoming threat angle for the upcoming wave and display warning indicator!
        float angleOffset = UnityEngine.Random.Range(60f, 300f);
        nextSpawnAngle = (lastSpawnAngle + angleOffset) % 360f;

        ShowDirectionIndicator(nextSpawnAngle);
    }

    private void OnDestroy()
    {
        ClearIndicators();
    }

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

        float bannerWidth = 260f;
        float bannerHeight = 48f;
        float x = (Screen.width - bannerWidth) * 0.5f;
        float y = 15f;

        GUI.Box(new Rect(x, y, bannerWidth, bannerHeight), GUIContent.none);

        GUIStyle waveStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        string statusText;
        if (currentState == SpawnerState.Intermission)
        {
            float pulse = Mathf.PingPong(Time.time * 4f, 1f);
            waveStyle.normal.textColor = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.3f, 0.15f), pulse);
            statusText = $"Wave {currentWave} in {Mathf.CeilToInt(waveTimer)}s";
        }
        else
        {
            waveStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
            statusText = $"WAVE {currentWave} ({activeAsteroids.Count} Left)";
        }

        GUI.Label(new Rect(x, y + 3, bannerWidth, 22), statusText, waveStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        string subText;
        if (currentState == SpawnerState.Intermission)
        {
            subText = $"⚠ INCOMING SECTOR: {Mathf.RoundToInt(nextSpawnAngle)}°";
        }
        else
        {
            subText = "DEFEND EARTH";
        }

        GUI.Label(new Rect(x, y + 25, bannerWidth, 18), subText, subStyle);
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
