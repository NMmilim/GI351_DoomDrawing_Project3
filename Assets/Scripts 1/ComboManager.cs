using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum ComboGrade
{
    None,
    F,
    D,
    C,
    B,
    A,
    APlus,
    S
}

/// <summary>
/// Tracks player hits, dynamic decay timer (2.0s base + 0.5s per 15 combo),
/// dynamic chain resource calculations, Overload state, and Instant Sudden Death Game Over.
/// </summary>
public class ComboManager : MonoBehaviour
{
    private static ComboManager _instance;
    public static ComboManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ComboManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ComboManager");
                    _instance = go.AddComponent<ComboManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Combo Timer Settings")]
    [Tooltip("Base decay timeout in seconds before combo resets (2.0s).")]
    [SerializeField] private float baseComboTimeout = 2.0f;
    [Tooltip("Additional decay time added for every 15 combo achieved (+0.5s).")]
    [SerializeField] private float timeExtensionPerTier = 0.5f;
    [Tooltip("Number of combo hits required to gain one time extension tier (15).")]
    [SerializeField] private int hitsPerTimeTier = 15;

    [Header("Overload System")]
    [Tooltip("Combo count required to fully charge and ready Overload.")]
    [SerializeField] private int overloadThreshold = 20;
    [Tooltip("Duration in seconds of the Overload state (unlimited free firing).")]
    [SerializeField] private float overloadDuration = 5.0f;
    [SerializeField] private bool isOverloadReady = false;
    [SerializeField] private bool isOverloadActive = false;
    [SerializeField] private float overloadRemainingTime = 0f;

    [Header("Grade Thresholds (Hits required in each grade to advance)")]
    [SerializeField] private int hitsFToD = 10;
    [SerializeField] private int hitsDToC = 20;
    [SerializeField] private int hitsCToB = 40;
    [SerializeField] private int hitsBToA = 60;
    [SerializeField] private int hitsAToAPlus = 120;
    [SerializeField] private int hitsAPlusToS = 240;

    [Header("Live Status (Read-Only)")]
    [SerializeField] private ComboGrade currentGrade = ComboGrade.None;
    [SerializeField] private int totalComboHits = 0;
    [SerializeField] private int hitsInCurrentGrade = 0;
    [SerializeField] private float remainingTime = 0f;
    [SerializeField] private bool isComboActive = false;
    [SerializeField] private int totalResourcesEarned = 0;
    [SerializeField] private bool isGameOver = false;

    [Header("Game Statistics & Records")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int highScore = 0;
    [SerializeField] private int waveReached = 1;
    [SerializeField] private int wavesCleared = 0;
    [SerializeField] private int bestWave = 1;
    [SerializeField] private int totalAsteroidsDestroyed = 0;
    [SerializeField] private int normalDestroyed = 0;
    [SerializeField] private int speedyDestroyed = 0;
    [SerializeField] private int tankyDestroyed = 0;
    [SerializeField] private int magnetDestroyed = 0;
    [SerializeField] private int maxComboHits = 0;
    [SerializeField] private ComboGrade highestGrade = ComboGrade.None;
    [SerializeField] private float survivalDuration = 0f;
    [SerializeField] private int overloadActivations = 0;
    [SerializeField] private string gameOverCause = "Earth Defenses Breached";
    [SerializeField] private bool isNewHighScore = false;
    [SerializeField] private bool isNewBestWave = false;

    [Header("On-Screen Display")]
    [Tooltip("Render sleek combo, overload, and game-over UI on screen.")]
    [SerializeField] private bool showOnScreenUI = false;
    [Tooltip("Log every kill/chain calculation to the Console. Keep OFF for large swarms (Debug.Log is expensive).")]
    [SerializeField] private bool verboseLogging = false;

    // Events
    public static event Action<ComboGrade, int> OnComboUpdated; // (currentGrade, totalHits)
    public static event Action<ComboGrade> OnGradeUp;           // (newGrade)
    public static event Action OnComboReset;
    public static event Action OnOverloadReady;
    public static event Action OnOverloadActivated;
    public static event Action OnOverloadEnded;
    public static event Action OnGameOver;

    // Public Getters
    public ComboGrade CurrentGrade => currentGrade;
    public int TotalComboHits => totalComboHits;
    public int HitsInCurrentGrade => hitsInCurrentGrade;
    public float RemainingTime => remainingTime;
    public float ActiveTimeout => GetActiveTimeout();
    public bool IsComboActive => isComboActive;
    public bool IsOverloadReady => isOverloadReady;
    public bool IsOverloadActive => isOverloadActive;
    public float OverloadRemainingTime => overloadRemainingTime;
    public float OverloadProgress => Mathf.Clamp01((float)totalComboHits / Mathf.Max(1, overloadThreshold));
    public int TotalResourcesEarned => totalResourcesEarned;
    public bool IsGameOver => isGameOver;
    public bool ShowOnScreenUI { get => showOnScreenUI; set => showOnScreenUI = value; }

    public int CurrentScore => currentScore;
    public int HighScore => highScore;
    public int WaveReached => waveReached;
    public int WavesCleared => wavesCleared;
    public int BestWave => bestWave;
    public int TotalAsteroidsDestroyed => totalAsteroidsDestroyed;
    public int NormalDestroyed => normalDestroyed;
    public int SpeedyDestroyed => speedyDestroyed;
    public int TankyDestroyed => tankyDestroyed;
    public int MagnetDestroyed => magnetDestroyed;
    public int MaxComboHits => maxComboHits;
    public ComboGrade HighestGrade => highestGrade;
    public float SurvivalDuration => survivalDuration;
    public int OverloadActivations => overloadActivations;
    public string GameOverCause => gameOverCause;
    public bool IsNewHighScore => isNewHighScore;
    public bool IsNewBestWave => isNewBestWave;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        highScore = PlayerPrefs.GetInt("DoomDefense_HighScore", 0);
        bestWave = PlayerPrefs.GetInt("DoomDefense_BestWave", 1);
    }

    private void OnEnable()
    {
        AsteroidSpawner.OnWaveCleared += HandleWaveCleared;
    }

    private void OnDisable()
    {
        AsteroidSpawner.OnWaveCleared -= HandleWaveCleared;
    }

    private void HandleWaveCleared(int wave)
    {
        wavesCleared = Mathf.Max(wavesCleared, wave);
        int waveBonus = wave * 500;
        currentScore += waveBonus;
        Debug.Log($"[Score] Wave {wave} Cleared! +{waveBonus} Bonus Points. Total Score: {currentScore}");
    }

    private void Update()
    {
        // Handle Game Over navigation hotkeys
        if (isGameOver)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    RestartGame();
                }
                else if (Keyboard.current.mKey.wasPressedThisFrame)
                {
                    ReturnToMainMenu();
                }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    Application.Quit();
                }
            }
            return;
        }

        // Track elapsed defense survival time
        survivalDuration += Time.unscaledDeltaTime;

        // Handle Overload activation hotkey (Spacebar or Shift)
        if (isOverloadReady && !isOverloadActive)
        {
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.leftShiftKey.wasPressedThisFrame))
            {
                ActivateOverload();
            }
        }

        // Overload duration countdown
        if (isOverloadActive)
        {
            overloadRemainingTime -= Time.deltaTime;
            if (overloadRemainingTime <= 0f)
            {
                isOverloadActive = false;
                overloadRemainingTime = 0f;
                Debug.Log("[Overload] Ended! Weapon costs restored.");
                OnOverloadEnded?.Invoke();
            }
        }

        // Combo decay timer countdown
        if (!isComboActive) return;

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            ResetCombo();
        }
    }

    /// <summary>
    /// Calculates dynamic timeout window: 2.0s base + 0.5s per 15 combo hits achieved.
    /// </summary>
    public float GetActiveTimeout()
    {
        int tiers = totalComboHits / Mathf.Max(1, hitsPerTimeTier);
        return baseComboTimeout + tiers * timeExtensionPerTier;
    }

    /// <summary>
    /// Registers an enemy meteor kill, computing resources according to the Chain Combo formula:
    /// - 1st kill: grants flat Base Drop.
    /// - Subsequent kills: grants BaseChainCost + floor(CurrentCombo / 2).
    /// Also records destruction statistics, applies combo score multiplier, and updates high scores.
    /// </summary>
    public void RegisterMeteorKill(MeteorType meteorType, int baseDrop, int baseChainCost)
    {
        if (isGameOver) return;

        // Track asteroid kill statistics
        totalAsteroidsDestroyed++;
        switch (meteorType)
        {
            case MeteorType.Normal: normalDestroyed++; break;
            case MeteorType.Speedy: speedyDestroyed++; break;
            case MeteorType.Tanky: tankyDestroyed++; break;
            case MeteorType.Magnet: magnetDestroyed++; break;
        }

        // Calculate score with combo streak multiplier
        int baseThreatScore = 100;
        switch (meteorType)
        {
            case MeteorType.Normal: baseThreatScore = 100; break;
            case MeteorType.Speedy: baseThreatScore = 150; break;
            case MeteorType.Tanky: baseThreatScore = 250; break;
            case MeteorType.Magnet: baseThreatScore = 350; break;
        }

        int earned = 0;

        if (!isComboActive)
        {
            // 1st Kill: Base Drop
            isComboActive = true;
            totalComboHits = 1;
            hitsInCurrentGrade = 1;
            currentGrade = ComboGrade.F;

            earned = baseDrop;
            remainingTime = GetActiveTimeout();

            currentScore += baseThreatScore;

            if (verboseLogging) Debug.Log($"[Combo] Chain started with {meteorType}! Kill 1 -> Earned {earned} base resources. Timer: {remainingTime:F1}s");
        }
        else
        {
            // Subsequent kills in chain: BaseChainCost + floor(currentCombo / 2)
            int comboIndex = totalComboHits;
            int chainBonus = comboIndex / 2;
            earned = baseChainCost + chainBonus;

            totalComboHits++;
            hitsInCurrentGrade++;
            remainingTime = GetActiveTimeout();

            CheckGradeProgression();

            float comboMultiplier = 1.0f + (totalComboHits * 0.05f);
            int pts = Mathf.RoundToInt(baseThreatScore * comboMultiplier);
            currentScore += pts;

            if (verboseLogging) Debug.Log($"[Combo] Chain Kill #{totalComboHits} ({meteorType})! Combo {comboIndex} -> Earned {earned} resources ({baseChainCost} + {chainBonus}). Timer: {remainingTime:F1}s");
        }

        // Update peak combo and highest grade reached
        if (totalComboHits > maxComboHits)
        {
            maxComboHits = totalComboHits;
        }
        if ((int)currentGrade > (int)highestGrade)
        {
            highestGrade = currentGrade;
        }

        totalResourcesEarned += earned;

        // Credit earned resources directly to player's unified pool
        if (Player_controller.Instance != null)
        {
            Player_controller.Instance.AddResources(earned);
        }

        // Check Overload readiness
        if (!isOverloadActive && !isOverloadReady && totalComboHits >= overloadThreshold)
        {
            isOverloadReady = true;
            Debug.Log($"[Overload] READY! Threshold ({overloadThreshold}) reached. Press SPACE to activate free firing!");
            OnOverloadReady?.Invoke();
        }

        OnComboUpdated?.Invoke(currentGrade, totalComboHits);
    }

    /// <summary>
    /// Fallback hit registration for weapon impacts or explosions that do not outright kill a meteor.
    /// </summary>
    public static void RegisterHit()
    {
        Instance.AddHit();
    }

    public void AddHit()
    {
        if (isGameOver) return;

        if (!isComboActive)
        {
            isComboActive = true;
            currentGrade = ComboGrade.F;
            totalComboHits = 1;
            hitsInCurrentGrade = 1;
            remainingTime = GetActiveTimeout();

            OnComboUpdated?.Invoke(currentGrade, totalComboHits);
            return;
        }

        remainingTime = GetActiveTimeout();
        totalComboHits++;
        hitsInCurrentGrade++;

        CheckGradeProgression();

        if (totalComboHits > maxComboHits)
        {
            maxComboHits = totalComboHits;
        }
        if ((int)currentGrade > (int)highestGrade)
        {
            highestGrade = currentGrade;
        }

        if (!isOverloadActive && !isOverloadReady && totalComboHits >= overloadThreshold)
        {
            isOverloadReady = true;
            OnOverloadReady?.Invoke();
        }

        OnComboUpdated?.Invoke(currentGrade, totalComboHits);
    }

    public void ActivateOverload()
    {
        if (!isOverloadReady || isOverloadActive) return;
        SoundManager.Instance.PlaySound2D("Overload");

        isOverloadReady = false;
        isOverloadActive = true;
        overloadRemainingTime = overloadDuration;
        overloadActivations++;

        Debug.Log($"[Overload] ACTIVATED! Unlimited free firing for {overloadDuration:F1}s!");
        OnOverloadActivated?.Invoke();
    }

    /// <summary>
    /// Triggered when asteroid impacts deplete Earth's resource pool to 0.
    /// Freezes gameplay, computes mission debrief records, saves high scores, and shows Game Over menu.
    /// </summary>
    public void TriggerGameOver(string cause = "Planetary Defenses Breached")
    {
        if (isGameOver) return;
        isGameOver = true;
        gameOverCause = string.IsNullOrEmpty(cause) ? "Earth Defenses Breached" : cause;

        // Freeze gameplay
        Time.timeScale = 0f;

        // Record wave stats
        if (AsteroidSpawner.Instance != null)
        {
            waveReached = AsteroidSpawner.Instance.CurrentWave;
            wavesCleared = Mathf.Max(0, waveReached - 1);
        }
        else
        {
            waveReached = Mathf.Max(1, wavesCleared + 1);
        }

        // Check and save High Score
        if (currentScore > highScore)
        {
            isNewHighScore = true;
            highScore = currentScore;
            PlayerPrefs.SetInt("DoomDefense_HighScore", highScore);
        }

        // Check and save Best Wave
        if (waveReached > bestWave)
        {
            isNewBestWave = true;
            bestWave = waveReached;
            PlayerPrefs.SetInt("DoomDefense_BestWave", bestWave);
        }

        PlayerPrefs.Save();

        Debug.LogWarning($"[Game Over] {gameOverCause}! Final Score: {currentScore}, Wave: {waveReached}, Kills: {totalAsteroidsDestroyed}, Max Combo: {maxComboHits}");
        OnGameOver?.Invoke();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.LoadScene("GameMenu", "CrossFade");
        }
        else
        {
            SceneManager.LoadScene("GameMenu");
        }
    }

    private void CheckGradeProgression()
    {
        switch (currentGrade)
        {
            case ComboGrade.F:
                if (hitsInCurrentGrade >= hitsFToD) AdvanceGrade(ComboGrade.D);
                break;
            case ComboGrade.D:
                if (hitsInCurrentGrade >= hitsDToC) AdvanceGrade(ComboGrade.C);
                break;
            case ComboGrade.C:
                if (hitsInCurrentGrade >= hitsCToB) AdvanceGrade(ComboGrade.B);
                break;
            case ComboGrade.B:
                if (hitsInCurrentGrade >= hitsBToA) AdvanceGrade(ComboGrade.A);
                break;
            case ComboGrade.A:
                if (hitsInCurrentGrade >= hitsAToAPlus) AdvanceGrade(ComboGrade.APlus);
                break;
            case ComboGrade.APlus:
                if (hitsInCurrentGrade >= hitsAPlusToS) AdvanceGrade(ComboGrade.S);
                break;
            case ComboGrade.S:
                break;
        }
    }

    private void AdvanceGrade(ComboGrade nextGrade)
    {
        currentGrade = nextGrade;
        hitsInCurrentGrade = 0;
        Debug.Log($"[Combo] GRADE UP! Reached Grade: {GetGradeString(currentGrade)} (Total Hits: {totalComboHits})");
        OnGradeUp?.Invoke(currentGrade);
    }

    public void ResetCombo()
    {
        if (!isComboActive) return;

        Debug.Log($"[Combo] Reset! Expired at Grade: {GetGradeString(currentGrade)} with {totalComboHits} total hits.");

        isComboActive = false;
        currentGrade = ComboGrade.None;
        totalComboHits = 0;
        hitsInCurrentGrade = 0;
        remainingTime = 0f;

        OnComboReset?.Invoke();
    }

    public static string GetGradeString(ComboGrade grade)
    {
        switch (grade)
        {
            case ComboGrade.F: return "F";
            case ComboGrade.D: return "D";
            case ComboGrade.C: return "C";
            case ComboGrade.B: return "B";
            case ComboGrade.A: return "A";
            case ComboGrade.APlus: return "A+";
            case ComboGrade.S: return "S";
            default: return "-";
        }
    }

    public static Color GetGradeColor(ComboGrade grade)
    {
        switch (grade)
        {
            case ComboGrade.F: return new Color(0.7f, 0.7f, 0.7f);
            case ComboGrade.D: return new Color(0.4f, 0.85f, 0.4f);
            case ComboGrade.C: return new Color(0.2f, 0.8f, 1f);
            case ComboGrade.B: return new Color(0.4f, 0.5f, 1f);
            case ComboGrade.A: return new Color(1f, 0.65f, 0.1f);
            case ComboGrade.APlus: return new Color(1f, 0.3f, 0.2f);
            case ComboGrade.S: return new Color(1f, 0.85f, 0.0f);
            default: return Color.white;
        }
    }

    private void OnGUI()
    {
        if (!showOnScreenUI) return;

        // Render Game Over Screen Overlay (render when game is over)
        if (isGameOver)
        {
            DrawGameOverUI();
            return;
        }

        // Render Combo & Overload HUD
        DrawComboAndOverloadUI();
    }

    private void DrawComboAndOverloadUI()
    {
        float cardWidth = 240f;
        float cardHeight = 135f;
        float x = Screen.width - cardWidth - 20f;
        float y = 20f;

        GUI.Box(new Rect(x, y, cardWidth, cardHeight), GUIContent.none);

        // Grade label
        GUIStyle gradeStyle = HudStyles.Label(36, FontStyle.Bold, TextAnchor.MiddleCenter);
        gradeStyle.normal.textColor = GetGradeColor(currentGrade);
        GUI.Label(new Rect(x + 10, y + 8, 60, 48), isComboActive ? GetGradeString(currentGrade) : "-", gradeStyle);

        // Combo hits label
        GUIStyle textStyle = HudStyles.Label(18, FontStyle.Bold, TextAnchor.MiddleLeft);
        textStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 80, y + 10, 150, 24), $"{totalComboHits} Hits", textStyle);

        GUIStyle subStyle = HudStyles.Label(11, FontStyle.Normal, TextAnchor.MiddleLeft);
        subStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        GUI.Label(new Rect(x + 80, y + 32, 150, 18), "CHAIN COMBO", subStyle);

        // Countdown timer bar
        float activeMax = Mathf.Max(0.1f, GetActiveTimeout());
        float timerPct = isComboActive ? Mathf.Clamp01(remainingTime / activeMax) : 0f;
        Rect barBg = new Rect(x + 15, y + 58, cardWidth - 30, 14);
        Rect barFill = new Rect(x + 15, y + 58, (cardWidth - 30) * timerPct, 14);

        Color prevColor = GUI.color;
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.DrawTexture(barBg, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(Color.red, Color.green, timerPct);
        GUI.DrawTexture(barFill, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUIStyle timerTextStyle = HudStyles.Label(10, FontStyle.Normal, TextAnchor.MiddleCenter);
        timerTextStyle.normal.textColor = Color.white;
        string timerStr = isComboActive ? $"{remainingTime:F1}s / {activeMax:F1}s" : "0.0s";
        GUI.Label(barBg, timerStr, timerTextStyle);

        // Overload gauge bar & prompt
        Rect ovBg = new Rect(x + 15, y + 84, cardWidth - 30, 14);
        float ovPct = isOverloadActive ? Mathf.Clamp01(overloadRemainingTime / overloadDuration) : OverloadProgress;
        Rect ovFill = new Rect(x + 15, y + 84, (cardWidth - 30) * ovPct, 14);

        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        GUI.DrawTexture(ovBg, Texture2D.whiteTexture);

        Color ovColor = isOverloadActive ? new Color(1f, 0.85f, 0.1f) : (isOverloadReady ? new Color(0.2f, 0.9f, 1f) : new Color(0.4f, 0.4f, 0.8f));
        GUI.color = ovColor;
        GUI.DrawTexture(ovFill, Texture2D.whiteTexture);

        GUI.color = Color.white;
        string ovText;
        if (isOverloadActive)
            ovText = $"OVERLOAD: {overloadRemainingTime:F1}s (FREE FIRE)";
        else if (isOverloadReady)
            ovText = "[SPACE] OVERLOAD READY!";
        else
            ovText = $"Overload: {Mathf.RoundToInt(OverloadProgress * 100)}%";

        GUI.Label(ovBg, ovText, timerTextStyle);

        // Subtitle status
        // Same cached instance as timerTextStyle (identical params): safe because timerTextStyle's last use is above
        GUIStyle statusStyle = HudStyles.Label(10, FontStyle.Normal, TextAnchor.MiddleCenter);
        statusStyle.normal.textColor = isOverloadActive ? new Color(1f, 0.9f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);
        string bottomInfo = isOverloadActive ? "ALL WEAPONS FREE!" : (isOverloadReady ? "Press SPACE / SHIFT" : $"Earned: {totalResourcesEarned} Res");
        GUI.Label(new Rect(x + 15, y + 104, cardWidth - 30, 18), bottomInfo, statusStyle);

        GUI.color = prevColor;
    }

    private void DrawGameOverUI()
    {
        // 1. Dark atmospheric backdrop overlay
        Color prevColor = GUI.color;
        GUI.color = new Color(0.04f, 0.04f, 0.07f, 0.93f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prevColor;

        // 2. Centered Modal Window
        float modalWidth = Mathf.Min(600f, Screen.width - 40f);
        float modalHeight = Mathf.Min(500f, Screen.height - 40f);
        float x = (Screen.width - modalWidth) * 0.5f;
        float y = (Screen.height - modalHeight) * 0.5f;

        // Background card
        GUI.Box(new Rect(x, y, modalWidth, modalHeight), GUIContent.none);

        // Header Title: "MISSION FAILED"
        GUIStyle titleStyle = HudStyles.Label(28, FontStyle.Bold, TextAnchor.MiddleCenter);
        titleStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);
        GUI.Label(new Rect(x, y + 14, modalWidth, 34), "MISSION FAILED", titleStyle);

        // Subtitle: "EARTH DEFENSES DESTROYED"
        GUIStyle subTitleStyle = HudStyles.Label(13, FontStyle.Bold, TextAnchor.MiddleCenter);
        subTitleStyle.normal.textColor = new Color(1f, 0.75f, 0.25f);
        GUI.Label(new Rect(x, y + 46, modalWidth, 20), "EARTH DEFENSES COLLAPSED", subTitleStyle);

        // Cause of defeat
        GUIStyle causeStyle = HudStyles.Label(11, FontStyle.Normal, TextAnchor.MiddleCenter);
        causeStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        string causeStr = !string.IsNullOrEmpty(gameOverCause) ? gameOverCause : "Resources drained to 0 from asteroid impacts";
        GUI.Label(new Rect(x, y + 66, modalWidth, 18), $"Cause: {causeStr}", causeStyle);

        // 3. Highlighted Score & High Score Banner
        float scoreBoxY = y + 88f;
        float scoreBoxH = 50f;
        Rect scoreBoxRect = new Rect(x + 20f, scoreBoxY, modalWidth - 40f, scoreBoxH);
        GUI.Box(scoreBoxRect, GUIContent.none);

        GUIStyle scoreStyle = HudStyles.Label(20, FontStyle.Bold, TextAnchor.MiddleLeft);
        scoreStyle.normal.textColor = new Color(1f, 0.88f, 0.25f);
        GUI.Label(new Rect(x + 35f, scoreBoxY + 6f, 260f, 24), $"FINAL SCORE: {currentScore:N0}", scoreStyle);

        GUIStyle highScoreStyle = HudStyles.Label(13, FontStyle.Bold, TextAnchor.MiddleRight);
        highScoreStyle.normal.textColor = isNewHighScore ? new Color(0.3f, 1f, 0.5f) : new Color(0.8f, 0.85f, 0.9f);
        string hsText = isNewHighScore ? "★ NEW BEST HIGH SCORE! ★" : $"ALL-TIME BEST: {highScore:N0}";
        GUI.Label(new Rect(x + modalWidth - 300f, scoreBoxY + 8f, 265f, 20), hsText, highScoreStyle);

        GUIStyle scoreSubStyle = HudStyles.Label(10, FontStyle.Normal, TextAnchor.MiddleLeft);
        scoreSubStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x + 35f, scoreBoxY + 28f, modalWidth - 70f, 16), "Earned from asteroid threat eliminations, combo multiplier chains, and cleared waves", scoreSubStyle);

        // 4. Two-Column Detailed Mission Statistics
        float colY = scoreBoxY + scoreBoxH + 10f;
        float colW = (modalWidth - 50f) * 0.5f;
        float colH = 205f;

        // Left Column: Mission & Progression
        Rect leftColRect = new Rect(x + 20f, colY, colW, colH);
        GUI.Box(leftColRect, GUIContent.none);

        GUIStyle colHeaderStyle = HudStyles.Label(12, FontStyle.Bold, TextAnchor.MiddleLeft);
        colHeaderStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);
        GUI.Label(new Rect(leftColRect.x + 12f, colY + 8f, colW - 24f, 18), "MISSION PROGRESSION", colHeaderStyle);

        GUIStyle statLabelStyle = HudStyles.Label(11, FontStyle.Normal, TextAnchor.MiddleLeft);
        statLabelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        GUIStyle statValStyle = HudStyles.Label(11, FontStyle.Bold, TextAnchor.MiddleRight);
        statValStyle.normal.textColor = Color.white;

        float rowY = colY + 30f;
        float rowH = 22f;

        // Wave Reached
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), "Wave Reached:", statLabelStyle);
        string waveStr = isNewBestWave ? $"Wave {waveReached} (NEW BEST!)" : $"Wave {waveReached} (Best: {bestWave})";
        statValStyle.normal.textColor = isNewBestWave ? new Color(0.3f, 1f, 0.5f) : Color.white;
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), waveStr, statValStyle);
        rowY += rowH;

        // Waves Cleared
        statValStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), "Waves Cleared:", statLabelStyle);
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), $"{wavesCleared} Waves", statValStyle);
        rowY += rowH;

        // Time Survived
        int mins = Mathf.FloorToInt(survivalDuration / 60f);
        int secs = Mathf.FloorToInt(survivalDuration % 60f);
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), "Time Survived:", statLabelStyle);
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), $"{mins:00}:{secs:00}", statValStyle);
        rowY += rowH;

        // Overloads Activated
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), "Overloads Triggered:", statLabelStyle);
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), $"{overloadActivations} Times", statValStyle);
        rowY += rowH;

        // Max Combo Streak
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), "Peak Chain Combo:", statLabelStyle);
        string gradeStr = highestGrade != ComboGrade.None ? $"Grade {GetGradeString(highestGrade)}" : "Rank -";
        statValStyle.normal.textColor = GetGradeColor(highestGrade);
        GUI.Label(new Rect(leftColRect.x + 12f, rowY, colW - 24f, 18), $"{maxComboHits} Hits ({gradeStr})", statValStyle);

        // Right Column: Destruction & Harvesting Record
        Rect rightColRect = new Rect(x + 20f + colW + 10f, colY, colW, colH);
        GUI.Box(rightColRect, GUIContent.none);

        colHeaderStyle.normal.textColor = new Color(1f, 0.55f, 0.25f);
        GUI.Label(new Rect(rightColRect.x + 12f, colY + 8f, colW - 24f, 18), "DESTRUCTION & HARVEST", colHeaderStyle);

        rowY = colY + 30f;
        statValStyle.normal.textColor = Color.white;

        // Total Asteroids Destroyed
        GUI.Label(new Rect(rightColRect.x + 12f, rowY, colW - 24f, 18), "Total Meteors Destroyed:", statLabelStyle);
        statValStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
        GUI.Label(new Rect(rightColRect.x + 12f, rowY, colW - 24f, 18), $"{totalAsteroidsDestroyed}", statValStyle);
        rowY += rowH;

        // Breakdown sublabels
        GUIStyle subBreakdown = HudStyles.Label(10);
        subBreakdown.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
        GUIStyle subBreakdownRight = HudStyles.Label(10, FontStyle.Normal, TextAnchor.MiddleRight);
        subBreakdownRight.normal.textColor = new Color(0.75f, 0.75f, 0.75f);

        GUI.Label(new Rect(rightColRect.x + 16f, rowY, colW - 28f, 16), "• Normal (Threat 1.0):", subBreakdown);
        GUI.Label(new Rect(rightColRect.x + 16f, rowY, colW - 32f, 16), $"{normalDestroyed}", subBreakdownRight);
        rowY += 17f;

        GUI.Label(new Rect(rightColRect.x + 16f, rowY, colW - 28f, 16), "• Speedy (Threat 1.5):", subBreakdown);
        GUI.Label(new Rect(rightColRect.x + 16f, rowY, colW - 32f, 16), $"{speedyDestroyed}", subBreakdownRight);
        rowY += 17f;

        GUI.Label(new Rect(rightColRect.x + 16f, rowY, colW - 28f, 16), "• Tanky (Threat 2.0):", subBreakdown);
        GUI.Label(new Rect(rightColRect.x + 16f, rowY, colW - 32f, 16), $"{tankyDestroyed}", subBreakdownRight);
        rowY += 17f;

        GUI.Label(new Rect(rightColRect.x + 16f, rowY, colW - 28f, 16), "• Magnet (Threat 3.0):", subBreakdown);
        GUI.Label(new Rect(rightColRect.x + 16f, rowY, colW - 32f, 16), $"{magnetDestroyed}", subBreakdownRight);
        rowY += 21f;

        // Total Resources Harvested
        statValStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);
        GUI.Label(new Rect(rightColRect.x + 12f, rowY, colW - 24f, 18), "Resources Harvested:", statLabelStyle);
        GUI.Label(new Rect(rightColRect.x + 12f, rowY, colW - 24f, 18), $"{totalResourcesEarned} Res", statValStyle);

        // 5. Interactive Navigation Buttons Row
        float btnY = colY + colH + 12f;
        float btnW = (modalWidth - 60f) / 3f;
        float btnH = 38f;

        // Button 1: Retry
        Rect retryRect = new Rect(x + 20f, btnY, btnW, btnH);
        if (GUI.Button(retryRect, "RETRY MISSION [R]"))
        {
            RestartGame();
        }

        // Button 2: Main Menu
        Rect menuRect = new Rect(x + 20f + btnW + 10f, btnY, btnW, btnH);
        if (GUI.Button(menuRect, "MAIN MENU [M]"))
        {
            ReturnToMainMenu();
        }

        // Button 3: Quit
        Rect quitRect = new Rect(x + 20f + (btnW + 10f) * 2f, btnY, btnW, btnH);
        if (GUI.Button(quitRect, "QUIT GAME [ESC]"))
        {
            Application.Quit();
        }
    }
}
