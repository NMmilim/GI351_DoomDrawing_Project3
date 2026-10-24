using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Central Canvas UI Controller for GI351: Doom Drawing.
/// Designed for UI Artists & Designers:
/// - Connects Unity UI / TextMeshPro elements directly to gameplay data without code.
/// - Handles Resource Economy, Weapon Arsenal, Chain Combo, Overload, Wave Radar, and Game Over modal.
/// - Automatically silences legacy IMGUI (OnGUI) when active so nothing overlaps.
/// </summary>
[DisallowMultipleComponent]
public class GameCanvasUI : MonoBehaviour
{
    public static GameCanvasUI Instance { get; private set; }

    [Header("Legacy IMGUI Override")]
    [Tooltip("If checked, automatically hides the old OnGUI developer text from Player, ComboManager, and Spawner.")]
    [SerializeField] private bool disableLegacyOnGUI = true;

    [Header("1. Resource Pool & Planetary Defense")]
    [Tooltip("Displays current unified resources (e.g. '120 Res').")]
    [SerializeField] private TMP_Text resourceText;
    [Tooltip("Optional slider showing resource pool capacity.")]
    [SerializeField] private Slider resourceSlider;
    [Tooltip("Optional red vignette/flash image that flashes when Earth takes impact damage.")]
    [SerializeField] private Image earthDamageFlash;
    [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0.45f);

    [Header("2. Weapon Arsenal Selector")]
    [Tooltip("UI slots for weapons: [0] Laser Cannon, [1] Railgun, [2] Disposable Mine.")]
    [SerializeField] private GameObject[] weaponSlots;
    [Tooltip("Highlight border or glowing backdrop for the active weapon.")]
    [SerializeField] private Image[] weaponHighlights;
    [Tooltip("Cost text labels for each weapon (shows '10 Res', '25 Res', etc., or 'FREE!' in Overload).")]
    [SerializeField] private TMP_Text[] weaponCostTexts;
    [SerializeField] private Color activeWeaponColor = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField] private Color inactiveWeaponColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

    [Header("3. Chain Combo & Rank")]
    [Tooltip("Container panel for Combo HUD. Auto-hidden when combo is 0 if hideWhenNoCombo is true.")]
    [SerializeField] private GameObject comboContainer;
    [SerializeField] private bool hideWhenNoCombo = true;
    [Tooltip("Displays combo hit count (e.g. '24 HITS').")]
    [SerializeField] private TMP_Text comboHitsText;
    [Tooltip("Displays current combo rank (e.g. 'S', 'A+', 'B').")]
    [SerializeField] private TMP_Text comboGradeText;
    [Tooltip("Optional badge image whose color tints according to the active grade.")]
    [SerializeField] private Image comboGradeBadge;
    [Tooltip("Countdown fill bar for the active combo window (1.0 -> 0.0).")]
    [SerializeField] private Slider comboTimerBar;

    [Header("4. Overload System")]
    [Tooltip("Progress bar towards readying Overload (0.0 to 1.0).")]
    [SerializeField] private Slider overloadChargeBar;
    [Tooltip("Badge or text shown when Overload is ready to fire (e.g. 'PRESS SPACE').")]
    [SerializeField] private GameObject overloadReadyPrompt;
    [Tooltip("Active banner shown while Overload unlimited fire is in progress.")]
    [SerializeField] private GameObject overloadActiveBanner;
    [Tooltip("Countdown text while Overload is active (e.g. 'OVERLOAD: 4.8s').")]
    [SerializeField] private TMP_Text overloadTimerText;

    [Header("5. Wave & Threat Radar")]
    [Tooltip("Current wave number (e.g. 'WAVE 3').")]
    [SerializeField] private TMP_Text waveNumberText;
    [Tooltip("Enemies remaining in current wave (e.g. '14 Left').")]
    [SerializeField] private TMP_Text enemiesRemainingText;
    [Tooltip("Wave status or upcoming threat warning (e.g. '⚠ SECTOR: 180°').")]
    [SerializeField] private TMP_Text waveStatusText;
    [Tooltip("Optional arrow pointer that rotates to face the upcoming threat angle.")]
    [SerializeField] private RectTransform threatDirectionArrow;

    [Header("6. Game Over Debrief Modal")]
    [Tooltip("Root modal panel for Game Over screen. Inactive during gameplay.")]
    [SerializeField] private GameObject gameOverModal;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private GameObject newHighScoreBadge;
    [SerializeField] private TMP_Text wavesClearedText;
    [SerializeField] private TMP_Text totalDestroyedText;
    [SerializeField] private TMP_Text normalDestroyedText;
    [SerializeField] private TMP_Text speedyDestroyedText;
    [SerializeField] private TMP_Text tankyDestroyedText;
    [SerializeField] private TMP_Text magnetDestroyedText;
    [SerializeField] private TMP_Text peakComboText;
    [SerializeField] private TMP_Text highestRankText;
    [SerializeField] private TMP_Text survivalTimeText;
    [SerializeField] private TMP_Text overloadCountText;
    [SerializeField] private TMP_Text causeOfDeathText;

    [Header("Game Over Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Destroy(gameObject);

        // Ensure Game Over panel starts inactive
        if (gameOverModal != null)
            gameOverModal.SetActive(false);

        // Auto-wire buttons
        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void Start()
    {
        // Silence legacy IMGUI if requested
        ApplyLegacyIMGUIOverride();
    }

    private void Update()
    {
        ApplyLegacyIMGUIOverride();

        UpdateResourceUI();
        UpdateWeaponUI();
        UpdateComboUI();
        UpdateOverloadUI();
        UpdateWaveUI();
        UpdateGameOverUI();
    }

    private void ApplyLegacyIMGUIOverride()
    {
        if (!disableLegacyOnGUI) return;

        if (Player_controller.Instance != null && Player_controller.Instance.ShowAmmoHUD)
            Player_controller.Instance.ShowAmmoHUD = false;

        if (ComboManager.Instance != null && ComboManager.Instance.ShowOnScreenUI)
            ComboManager.Instance.ShowOnScreenUI = false;

        if (AsteroidSpawner.Instance != null && AsteroidSpawner.Instance.ShowWaveHUD)
            AsteroidSpawner.Instance.ShowWaveHUD = false;
    }

    // ==========================================
    // 1. RESOURCE & HEALTH UI
    // ==========================================
    private void UpdateResourceUI()
    {
        if (Player_controller.Instance == null) return;

        int res = Player_controller.Instance.Resources;

        if (resourceText != null)
        {
            resourceText.text = $"{res} Res";
        }

        if (resourceSlider != null)
        {
            // If starting resources is e.g. 100, slider scales dynamically
            if (resourceSlider.maxValue < res) resourceSlider.maxValue = res;
            resourceSlider.value = res;
        }

        // Earth damage red flash
        if (earthDamageFlash != null)
        {
            float alertTimer = Player_controller.Instance.EarthDamageAlertTimer;
            if (alertTimer > 0f)
            {
                Color c = flashColor;
                c.a = Mathf.Clamp01(alertTimer / 0.8f) * flashColor.a;
                earthDamageFlash.color = c;
                earthDamageFlash.gameObject.SetActive(true);
            }
            else
            {
                earthDamageFlash.gameObject.SetActive(false);
            }
        }
    }

    // ==========================================
    // 2. WEAPON ARSENAL UI
    // ==========================================
    private void UpdateWeaponUI()
    {
        if (Player_controller.Instance == null) return;

        WeaponType current = Player_controller.Instance.CurrentWeapon;
        bool isOverload = ComboManager.Instance != null && ComboManager.Instance.IsOverloadActive;

        int laserCost = isOverload ? 0 : Player_controller.Instance.LaserCannonCost;
        int railCost = isOverload ? 0 : Player_controller.Instance.RailgunCost;
        int mineCost = isOverload ? 0 : Player_controller.Instance.DisposableMineCost;

        int[] costs = { laserCost, railCost, mineCost };

        for (int i = 0; i < 3; i++)
        {
            bool isSelected = (int)current == i;

            if (weaponHighlights != null && i < weaponHighlights.Length && weaponHighlights[i] != null)
            {
                weaponHighlights[i].color = isSelected ? activeWeaponColor : inactiveWeaponColor;
            }

            if (weaponCostTexts != null && i < weaponCostTexts.Length && weaponCostTexts[i] != null)
            {
                weaponCostTexts[i].text = isOverload ? "FREE" : $"{costs[i]} Res";
                weaponCostTexts[i].color = isOverload ? new Color(1f, 0.9f, 0.2f) : (isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            }
        }
    }

    // ==========================================
    // 3. COMBO & GRADE UI
    // ==========================================
    private void UpdateComboUI()
    {
        if (ComboManager.Instance == null) return;

        int comboHits = ComboManager.Instance.TotalComboHits;
        ComboGrade grade = ComboManager.Instance.CurrentGrade;
        float remainingTime = ComboManager.Instance.RemainingTime;
        float windowDuration = ComboManager.Instance.ActiveTimeout;

        if (comboContainer != null)
        {
            if (hideWhenNoCombo)
            {
                comboContainer.SetActive(comboHits > 0);
            }
            else
            {
                comboContainer.SetActive(true);
            }
        }

        if (comboHitsText != null)
        {
            comboHitsText.text = comboHits > 0 ? $"{comboHits} HITS" : "0";
        }

        if (comboGradeText != null)
        {
            comboGradeText.text = ComboManager.GetGradeString(grade);
            comboGradeText.color = ComboManager.GetGradeColor(grade);
        }

        if (comboGradeBadge != null)
        {
            comboGradeBadge.color = ComboManager.GetGradeColor(grade);
        }

        if (comboTimerBar != null)
        {
            float fill = windowDuration > 0.001f ? Mathf.Clamp01(remainingTime / windowDuration) : 0f;
            comboTimerBar.value = fill;
        }
    }

    // ==========================================
    // 4. OVERLOAD SYSTEM UI
    // ==========================================
    private void UpdateOverloadUI()
    {
        if (ComboManager.Instance == null) return;

        bool isReady = ComboManager.Instance.IsOverloadReady;
        bool isActive = ComboManager.Instance.IsOverloadActive;
        float progress = ComboManager.Instance.OverloadProgress;
        float remaining = ComboManager.Instance.OverloadRemainingTime;

        if (overloadChargeBar != null)
        {
            overloadChargeBar.value = isActive ? Mathf.Clamp01(remaining / 5.0f) : progress;
        }

        if (overloadReadyPrompt != null)
        {
            overloadReadyPrompt.SetActive(isReady && !isActive);
        }

        if (overloadActiveBanner != null)
        {
            overloadActiveBanner.SetActive(isActive);
        }

        if (overloadTimerText != null)
        {
            if (isActive)
            {
                overloadTimerText.gameObject.SetActive(true);
                overloadTimerText.text = $"OVERLOAD: {remaining:F1}s";
            }
            else
            {
                overloadTimerText.gameObject.SetActive(false);
            }
        }
    }

    // ==========================================
    // 5. WAVE & THREAT RADAR UI
    // ==========================================
    private void UpdateWaveUI()
    {
        if (AsteroidSpawner.Instance == null) return;

        int wave = AsteroidSpawner.Instance.CurrentWave;
        int activeAsteroids = AsteroidSpawner.Instance.ActiveAsteroidCount;
        AsteroidSpawner.SpawnerState state = AsteroidSpawner.Instance.CurrentState;
        float nextAngle = AsteroidSpawner.Instance.NextSpawnAngle;
        string threatDesc = AsteroidSpawner.Instance.CurrentThreatDescription;

        if (waveNumberText != null)
        {
            waveNumberText.text = $"WAVE {wave}";
        }

        if (enemiesRemainingText != null)
        {
            if (state == AsteroidSpawner.SpawnerState.Intermission)
            {
                float t = AsteroidSpawner.Instance.WaveTimer;
                enemiesRemainingText.text = $"Next wave in {t:F1}s";
            }
            else
            {
                enemiesRemainingText.text = $"{activeAsteroids} Meteors Remaining";
            }
        }

        if (waveStatusText != null)
        {
            if (state == AsteroidSpawner.SpawnerState.Intermission)
            {
                waveStatusText.text = $"⚠ SECTOR: {Mathf.RoundToInt(nextAngle)}° | {threatDesc}";
            }
            else
            {
                waveStatusText.text = threatDesc;
            }
        }

        if (threatDirectionArrow != null)
        {
            if (state == AsteroidSpawner.SpawnerState.Intermission)
            {
                threatDirectionArrow.gameObject.SetActive(true);
                threatDirectionArrow.localRotation = Quaternion.Euler(0f, 0f, nextAngle - 90f);
            }
            else
            {
                threatDirectionArrow.gameObject.SetActive(false);
            }
        }
    }

    // ==========================================
    // 6. GAME OVER DEBRIEF MODAL
    // ==========================================
    private void UpdateGameOverUI()
    {
        if (ComboManager.Instance == null) return;

        bool isDead = ComboManager.Instance.IsGameOver;

        if (gameOverModal != null)
        {
            if (isDead && !gameOverModal.activeSelf)
            {
                gameOverModal.SetActive(true);
                PopulateGameOverStats();
            }
            else if (!isDead && gameOverModal.activeSelf)
            {
                gameOverModal.SetActive(false);
            }
        }
    }

    private void PopulateGameOverStats()
    {
        if (ComboManager.Instance == null) return;

        if (finalScoreText != null) finalScoreText.text = $"{ComboManager.Instance.CurrentScore:N0}";
        if (highScoreText != null) highScoreText.text = $"{ComboManager.Instance.HighScore:N0}";
        if (newHighScoreBadge != null) newHighScoreBadge.SetActive(ComboManager.Instance.IsNewHighScore);

        if (wavesClearedText != null) wavesClearedText.text = $"{ComboManager.Instance.WavesCleared}";
        if (totalDestroyedText != null) totalDestroyedText.text = $"{ComboManager.Instance.TotalAsteroidsDestroyed}";

        if (normalDestroyedText != null) normalDestroyedText.text = $"{ComboManager.Instance.NormalDestroyed}";
        if (speedyDestroyedText != null) speedyDestroyedText.text = $"{ComboManager.Instance.SpeedyDestroyed}";
        if (tankyDestroyedText != null) tankyDestroyedText.text = $"{ComboManager.Instance.TankyDestroyed}";
        if (magnetDestroyedText != null) magnetDestroyedText.text = $"{ComboManager.Instance.MagnetDestroyed}";

        if (peakComboText != null) peakComboText.text = $"{ComboManager.Instance.MaxComboHits}";
        if (highestRankText != null)
        {
            ComboGrade grade = ComboManager.Instance.HighestGrade;
            highestRankText.text = ComboManager.GetGradeString(grade);
            highestRankText.color = ComboManager.GetGradeColor(grade);
        }

        if (survivalTimeText != null)
        {
            float t = ComboManager.Instance.SurvivalDuration;
            int mins = Mathf.FloorToInt(t / 60f);
            int secs = Mathf.FloorToInt(t % 60f);
            survivalTimeText.text = $"{mins:00}:{secs:00}";
        }

        if (overloadCountText != null) overloadCountText.text = $"{ComboManager.Instance.OverloadActivations}";
        if (causeOfDeathText != null) causeOfDeathText.text = ComboManager.Instance.GameOverCause;
    }

    // Button Callbacks
    public void OnRetryClicked()
    {
        if (ComboManager.Instance != null)
            ComboManager.Instance.RestartGame();
    }

    public void OnMainMenuClicked()
    {
        if (ComboManager.Instance != null)
            ComboManager.Instance.ReturnToMainMenu();
    }

    public void OnQuitClicked()
    {
        Application.Quit();
    }
}
