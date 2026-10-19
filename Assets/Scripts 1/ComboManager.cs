using System;
using UnityEngine;

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
/// Tracks player hits, combo decay timer, and grade progression (F -> D -> C -> B -> A -> A+ -> S).
/// Automatically creates an instance if not present in the scene.
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
                _instance = FindFirstObjectByType<ComboManager>();
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
    [Tooltip("Time in seconds without hitting an enemy before the combo resets.")]
    [SerializeField] private float comboTimeout = 10f;

    [Header("Grade Thresholds (Hits required in each grade to advance)")]
    [Tooltip("Hits needed at Grade F to reach D.")]
    [SerializeField] private int hitsFToD = 10;
    [Tooltip("Hits needed at Grade D to reach C.")]
    [SerializeField] private int hitsDToC = 20;
    [Tooltip("Hits needed at Grade C to reach B.")]
    [SerializeField] private int hitsCToB = 40;
    [Tooltip("Hits needed at Grade B to reach A.")]
    [SerializeField] private int hitsBToA = 60;
    [Tooltip("Hits needed at Grade A to reach A+.")]
    [SerializeField] private int hitsAToAPlus = 120;
    [Tooltip("Hits needed at Grade A+ to reach S.")]
    [SerializeField] private int hitsAPlusToS = 240;

    [Header("Live Status (Read-Only)")]
    [SerializeField] private ComboGrade currentGrade = ComboGrade.None;
    [SerializeField] private int totalComboHits = 0;
    [SerializeField] private int hitsInCurrentGrade = 0;
    [SerializeField] private float remainingTime = 0f;
    [SerializeField] private bool isComboActive = false;

    [Header("On-Screen Display")]
    [Tooltip("Render a sleek combo UI on screen for immediate testing.")]
    [SerializeField] private bool showOnScreenUI = true;

    // Events for UI, audio, particle effects, etc.
    public static event Action<ComboGrade, int> OnComboUpdated; // (currentGrade, totalHits)
    public static event Action<ComboGrade> OnGradeUp;           // (newGrade)
    public static event Action OnComboReset;

    // Public Getters
    public ComboGrade CurrentGrade => currentGrade;
    public int TotalComboHits => totalComboHits;
    public int HitsInCurrentGrade => hitsInCurrentGrade;
    public float RemainingTime => remainingTime;
    public float ComboTimeout => comboTimeout;
    public bool IsComboActive => isComboActive;

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
        if (!isComboActive) return;

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            ResetCombo();
        }
    }

    /// <summary>
    /// Static helper so bullets can register hits easily without scene lookups.
    /// </summary>
    public static void RegisterHit()
    {
        Instance.AddHit();
    }

    public void AddHit()
    {
        // First hit starts the combo at Grade F
        if (!isComboActive)
        {
            isComboActive = true;
            currentGrade = ComboGrade.F;
            totalComboHits = 1;
            hitsInCurrentGrade = 1;
            remainingTime = comboTimeout;

            Debug.Log($"[Combo] Started! Grade: F | Hits: 1 | Timer: {comboTimeout:F1}s");
            OnComboUpdated?.Invoke(currentGrade, totalComboHits);
            return;
        }

        // Subsequent hits reset the 10-second decay timer and increment counts
        remainingTime = comboTimeout;
        totalComboHits++;
        hitsInCurrentGrade++;

        CheckGradeProgression();

        OnComboUpdated?.Invoke(currentGrade, totalComboHits);
    }

    private void CheckGradeProgression()
    {
        switch (currentGrade)
        {
            case ComboGrade.F:
                if (hitsInCurrentGrade >= hitsFToD)
                {
                    AdvanceGrade(ComboGrade.D);
                }
                break;

            case ComboGrade.D:
                if (hitsInCurrentGrade >= hitsDToC)
                {
                    AdvanceGrade(ComboGrade.C);
                }
                break;

            case ComboGrade.C:
                if (hitsInCurrentGrade >= hitsCToB)
                {
                    AdvanceGrade(ComboGrade.B);
                }
                break;

            case ComboGrade.B:
                if (hitsInCurrentGrade >= hitsBToA)
                {
                    AdvanceGrade(ComboGrade.A);
                }
                break;

            case ComboGrade.A:
                if (hitsInCurrentGrade >= hitsAToAPlus)
                {
                    AdvanceGrade(ComboGrade.APlus);
                }
                break;

            case ComboGrade.APlus:
                if (hitsInCurrentGrade >= hitsAPlusToS)
                {
                    AdvanceGrade(ComboGrade.S);
                }
                break;

            case ComboGrade.S:
                // S rank reached: no higher grade, keep counting hits indefinitely
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

    /// <summary>
    /// Returns the target hits required to complete the current grade, or -1 if at S.
    /// </summary>
    public int GetRequiredHitsForCurrentGrade()
    {
        switch (currentGrade)
        {
            case ComboGrade.F: return hitsFToD;
            case ComboGrade.D: return hitsDToC;
            case ComboGrade.C: return hitsCToB;
            case ComboGrade.B: return hitsBToA;
            case ComboGrade.A: return hitsAToAPlus;
            case ComboGrade.APlus: return hitsAPlusToS;
            case ComboGrade.S: return -1; // Max grade
            default: return 0;
        }
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
            case ComboGrade.F: return new Color(0.7f, 0.7f, 0.7f); // Silver/Gray
            case ComboGrade.D: return new Color(0.4f, 0.85f, 0.4f); // Green
            case ComboGrade.C: return new Color(0.2f, 0.8f, 1f);   // Cyan
            case ComboGrade.B: return new Color(0.4f, 0.5f, 1f);   // Blue
            case ComboGrade.A: return new Color(1f, 0.65f, 0.1f);  // Orange
            case ComboGrade.APlus: return new Color(1f, 0.3f, 0.2f); // Red-Orange
            case ComboGrade.S: return new Color(1f, 0.85f, 0.0f);  // Gold
            default: return Color.white;
        }
    }

    private void OnGUI()
    {
        if (!showOnScreenUI || !isComboActive) return;

        // Clean on-screen GUI card in the top-right corner
        float cardWidth = 220f;
        float cardHeight = 110f;
        float x = Screen.width - cardWidth - 20f;
        float y = 20f;

        GUI.Box(new Rect(x, y, cardWidth, cardHeight), GUIContent.none);

        // Grade label with grade-specific color
        GUIStyle gradeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 36,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        gradeStyle.normal.textColor = GetGradeColor(currentGrade);

        GUI.Label(new Rect(x + 10, y + 10, 60, 50), GetGradeString(currentGrade), gradeStyle);

        // Display current hit count without revealing the requirement to reach the next rank
        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        textStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 80, y + 14, 130, 24), $"{totalComboHits} Hits", textStyle);

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft
        };
        subStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        GUI.Label(new Rect(x + 80, y + 38, 130, 18), "COMBO", subStyle);

        // Countdown timer bar
        float timerPct = Mathf.Clamp01(remainingTime / comboTimeout);
        Rect barBg = new Rect(x + 15, y + 70, cardWidth - 30, 14);
        Rect barFill = new Rect(x + 15, y + 70, (cardWidth - 30) * timerPct, 14);

        // Draw timer background
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.DrawTexture(barBg, Texture2D.whiteTexture);

        // Draw timer fill (color fades from green to red as time runs out)
        GUI.color = Color.Lerp(Color.red, Color.green, timerPct);
        GUI.DrawTexture(barFill, Texture2D.whiteTexture);

        // Timer text
        GUI.color = Color.white;
        GUIStyle timerTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter
        };
        timerTextStyle.normal.textColor = Color.white;
        GUI.Label(barBg, $"{remainingTime:F1}s", timerTextStyle);
    }
}
