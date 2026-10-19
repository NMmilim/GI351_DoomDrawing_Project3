using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to Earth (or references Earth) to spawn incoming asteroid waves
/// in a 360-degree perimeter ring outside the screen.
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

    [Header("Asteroid Prefabs & Weighted Mix")]
    [Tooltip("Weak asteroid prefab (destroyed in 1 hit).")]
    [SerializeField] private GameObject weakAsteroidPrefab;
    [Tooltip("Chance weighting for Weak asteroid (default 70%).")]
    [Range(0, 100)] [SerializeField] private int weakWeight = 70;

    [Tooltip("Medium asteroid prefab (takes multiple hits).")]
    [SerializeField] private GameObject mediumAsteroidPrefab;
    [Tooltip("Chance weighting for Medium asteroid (default 30%).")]
    [Range(0, 100)] [SerializeField] private int mediumWeight = 30;

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
    private readonly List<GameObject> activeAsteroids = new List<GameObject>();

    public int CurrentWave => currentWave;
    public SpawnerState CurrentState => currentState;
    public int ActiveAsteroidCount => activeAsteroids.Count;

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
        // Auto-assign prefabs if not assigned via Resources or Project
        if (weakAsteroidPrefab == null)
            weakAsteroidPrefab = Resources.Load<GameObject>("Rock2");
        if (mediumAsteroidPrefab == null)
            mediumAsteroidPrefab = Resources.Load<GameObject>("Rock1");

        // Start countdown to Wave 1
        StartIntermission(timeBetweenWaves);
    }

    private void Update()
    {
        // Clean up nulls in active list (destroyed rocks)
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
                // When all asteroids in the wave have been destroyed, wave is cleared!
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
        targetWaveCount = baseWaveCount + (currentWave - 1) * countIncreasePerWave;
        spawnedInCurrentWave = 0;
        currentState = SpawnerState.Spawning;

        Debug.Log($"[Spawner] --- STARTING WAVE {currentWave} ({targetWaveCount} Asteroids) ---");
        OnWaveStarted?.Invoke(currentWave);

        StartCoroutine(SpawnWaveRoutine());
    }

    private IEnumerator SpawnWaveRoutine()
    {
        while (spawnedInCurrentWave < targetWaveCount)
        {
            int remaining = targetWaveCount - spawnedInCurrentWave;
            // Pick chunk size between minChunkSize and maxChunkSize, clamped to remaining
            int chunkSize = UnityEngine.Random.Range(minChunkSize, maxChunkSize + 1);
            if (chunkSize > remaining)
            {
                chunkSize = remaining;
            }

            SpawnChunk(chunkSize);
            spawnedInCurrentWave += chunkSize;

            // Wait before spawning the next chunk if there are more to spawn
            if (spawnedInCurrentWave < targetWaveCount)
            {
                yield return new WaitForSeconds(timeBetweenChunks);
            }
        }

        currentState = SpawnerState.WaveInProgress;
    }

    private void SpawnChunk(int chunkSize)
    {
        if (chunkSize <= 0) return;

        Vector3 center = earthCenter != null ? earthCenter.position : Vector3.zero;

        // Choose a central angle for this chunk, at least 60 degrees away from the last chunk
        float angleOffset = UnityEngine.Random.Range(60f, 300f);
        float chunkCenterAngle = (lastSpawnAngle + angleOffset) % 360f;
        lastSpawnAngle = chunkCenterAngle;

        // Base spawn radius for the chunk's front/lead asteroid
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

            GameObject asteroid = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            activeAsteroids.Add(asteroid);
        }

        Debug.Log($"[Spawner] Spawned chunk of {placedPositions.Count} asteroids at angle {chunkCenterAngle:F1}° (Base Radius: {baseRadius:F1})");
    }

    private Vector3 CalculateChunkPosition(
        int index,
        int totalInChunk,
        float centerAngle,
        float baseRadius,
        Vector3 center,
        List<Vector3> existingPositions)
    {
        // Stagger radial depth: front asteroid is closest, each subsequent asteroid is further out in space
        // This ensures they do not collide along their inward approach path toward Earth
        float radius = baseRadius + index * radialDepthStep + UnityEngine.Random.Range(-0.2f, 0.2f);

        // Lateral angular spread:
        // index 0 -> 0 (Lead)
        // index 1 -> +35% spread
        // index 2 -> -35% spread
        // index 3 -> +50% spread
        // index 4 -> -50% spread
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

        // Verify minimum distance from all previously placed asteroids in this chunk to prevent spawn collision
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

            // If too close, nudge radially outward and jitter the angle
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

    private void StartIntermission(float duration)
    {
        currentState = SpawnerState.Intermission;
        waveTimer = duration;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = earthCenter != null ? earthCenter.position : transform.position;

        // Draw inner and outer spawn perimeter rings in Scene view
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(center, minSpawnRadius);
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.3f);
        Gizmos.DrawWireSphere(center, maxSpawnRadius);
    }

    private void OnGUI()
    {
        if (!showWaveHUD) return;

        // Wave status banner in top-center of the screen
        float bannerWidth = 240f;
        float bannerHeight = 45f;
        float x = (Screen.width - bannerWidth) * 0.5f;
        float y = 15f;

        GUI.Box(new Rect(x, y, bannerWidth, bannerHeight), GUIContent.none);

        GUIStyle waveStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        waveStyle.normal.textColor = Color.white;

        string statusText;
        if (currentState == SpawnerState.Intermission)
        {
            waveStyle.normal.textColor = new Color(0.4f, 0.9f, 1f);
            statusText = $"Wave {currentWave} in {Mathf.CeilToInt(waveTimer)}s";
        }
        else
        {
            waveStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
            statusText = $"WAVE {currentWave} ({activeAsteroids.Count} Left)";
        }

        GUI.Label(new Rect(x, y + 4, bannerWidth, 24), statusText, waveStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        string subText = currentState == SpawnerState.Intermission ? "PREPARE DEFENSES" : "DEFEND EARTH";
        GUI.Label(new Rect(x, y + 26, bannerWidth, 16), subText, subStyle);
    }
}
