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

    [Header("On-Screen Display")]
    [Tooltip("Render sleek combo, overload, and game-over UI on screen.")]
    [SerializeField] private bool showOnScreenUI = true;

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

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Handle Game Over restart hotkey
        if (isGameOver)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartGame();
            }
            return;
        }

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
    /// </summary>
    public void RegisterMeteorKill(MeteorType meteorType, int baseDrop, int baseChainCost)
    {
        if (isGameOver) return;

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

            Debug.Log($"[Combo] Chain started with {meteorType}! Kill 1 -> Earned {earned} base resources. Timer: {remainingTime:F1}s");
        }
        else
        {
            // Subsequent kills in chain: BaseChainCost + floor(currentCombo / 2)
            // (e.g. Kill 2 has comboIndex 1 -> floor(1/2) = 0)
            int comboIndex = totalComboHits;
            int chainBonus = comboIndex / 2;
            earned = baseChainCost + chainBonus;

            totalComboHits++;
            hitsInCurrentGrade++;
            remainingTime = GetActiveTimeout();

            CheckGradeProgression();

            Debug.Log($"[Combo] Chain Kill #{totalComboHits} ({meteorType})! Combo {comboIndex} -> Earned {earned} resources ({baseChainCost} + {chainBonus}). Timer: {remainingTime:F1}s");
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

        isOverloadReady = false;
        isOverloadActive = true;
        overloadRemainingTime = overloadDuration;

        Debug.Log($"[Overload] ACTIVATED! Unlimited free firing for {overloadDuration:F1}s!");
        OnOverloadActivated?.Invoke();
    }

    /// <summary>
    /// Triggered when asteroid impacts deplete Earth's resource pool to 0.
    /// Freezes gameplay and shows Game Over banner.
    /// </summary>
    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        Time.timeScale = 0f;
        Debug.LogWarning("[Game Over] Earth defenses collapsed! Resource pool depleted to 0 from asteroid impacts. Final Hits: " + totalComboHits);
        OnGameOver?.Invoke();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

        // Render Game Over Screen Overlay
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
        GUIStyle gradeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 36,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        gradeStyle.normal.textColor = GetGradeColor(currentGrade);
        GUI.Label(new Rect(x + 10, y + 8, 60, 48), isComboActive ? GetGradeString(currentGrade) : "-", gradeStyle);

        // Combo hits label
        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        textStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 80, y + 10, 150, 24), $"{totalComboHits} Hits", textStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };
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
        GUIStyle timerTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };
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
        GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };
        statusStyle.normal.textColor = isOverloadActive ? new Color(1f, 0.9f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);
        string bottomInfo = isOverloadActive ? "ALL WEAPONS FREE!" : (isOverloadReady ? "Press SPACE / SHIFT" : $"Earned: {totalResourcesEarned} Res");
        GUI.Label(new Rect(x + 15, y + 104, cardWidth - 30, 18), bottomInfo, statusStyle);

        GUI.color = prevColor;
    }

    private void DrawGameOverUI()
    {
        // Darkened background overlay
        Color prevColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = prevColor;

        float modalWidth = 420f;
        float modalHeight = 220f;
        float x = (Screen.width - modalWidth) * 0.5f;
        float y = (Screen.height - modalHeight) * 0.5f;

        GUI.Box(new Rect(x, y, modalWidth, modalHeight), GUIContent.none);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);
        GUI.Label(new Rect(x, y + 15, modalWidth, 40), "EARTH DESTROYED", titleStyle);

        GUIStyle descStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        descStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        GUI.Label(new Rect(x, y + 65, modalWidth, 24), "Earth Defenses Depleted: Resources reached 0 from asteroid impacts!", descStyle);

        GUIStyle statStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        statStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
        GUI.Label(new Rect(x, y + 100, modalWidth, 26), $"Max Combo: {totalComboHits} Hits  |  Total Resources: {totalResourcesEarned}", statStyle);

        GUIStyle restartStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        restartStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);
        GUI.Label(new Rect(x, y + 150, modalWidth, 30), "Press [R] to Try Again", restartStyle);
    }
}
