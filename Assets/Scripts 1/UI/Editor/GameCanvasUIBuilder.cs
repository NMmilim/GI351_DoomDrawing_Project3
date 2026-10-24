#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace DoomDrawing.UI.Editor
{
    public static class GameCanvasUIBuilder
    {
        private const string PrefabPath = "Assets/Prefab/UI/GameHUD_Canvas.prefab";

        [MenuItem("Tools/GI351/Generate Canvas HUD Template", false, 10)]
        [MenuItem("GameObject/UI/GI351 Canvas HUD Template", false, 20)]
        public static void CreateOrRegenerateCanvasHUD()
        {
            // 1. Ensure EventSystem exists in the scene
            EnsureEventSystem();

            // 2. Check if a Canvas HUD already exists in scene
            GameCanvasUI existing = Object.FindFirstObjectByType<GameCanvasUI>();
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "GameCanvasUI Already Exists",
                    "A GameCanvasUI instance is already present in this scene. Do you want to select it, or create a fresh one?",
                    "Select Existing",
                    "Create Fresh Copy");

                if (replace)
                {
                    Selection.activeGameObject = existing.gameObject;
                    EditorGUIUtility.PingObject(existing.gameObject);
                    return;
                }
            }

            // 3. Build the Canvas Hierarchy
            GameObject canvasGO = BuildCanvasHUDHierarchy();

            // 4. Register Undo & Select
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create GI351 Game Canvas HUD");
            Selection.activeGameObject = canvasGO;
            EditorGUIUtility.PingObject(canvasGO);

            // 5. Save as Prefab
            SaveCanvasAsPrefab(canvasGO);

            EditorUtility.DisplayDialog(
                "GI351 Canvas HUD Ready!",
                "Game Canvas HUD has been generated successfully!\n\n" +
                "- Hierarchy is organized and all fields are connected to GameCanvasUI.\n" +
                "- Saved as a reusable Prefab at:\n  " + PrefabPath + "\n\n" +
                "Your UI team can now easily restyle colors, swap sprites, replace fonts, and rearrange panels in Unity without touching C# code!",
                "Got it!");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                EditorApplication.ExecuteMenuItem("GameObject/UI/Event System");
            }
        }

        private static GameObject BuildCanvasHUDHierarchy()
        {
            // Root Canvas
            GameObject canvasGO = new GameObject("GameHUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameCanvasUI));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameCanvasUI canvasUI = canvasGO.GetComponent<GameCanvasUI>();
            SerializedObject so = new SerializedObject(canvasUI);

            // ----------------------------------------------------
            // 1. Resource & Planetary Defense (Top-Left)
            // ----------------------------------------------------
            GameObject resPanel = CreatePanel("HUD_TopLeft_Resources", canvasGO,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -30f), new Vector2(340f, 90f),
                new Color(0.08f, 0.10f, 0.14f, 0.85f));

            TextMeshProUGUI resLabel = CreateTMPText("Label_Defense", resPanel, "PLANETARY DEFENSE POOL", 12f,
                FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.55f, 0.65f, 0.75f));
            SetRect(resLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -14f), new Vector2(-32f, 20f));

            TextMeshProUGUI resVal = CreateTMPText("Text_ResourceCount", resPanel, "100 Res", 28f,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(0.3f, 0.95f, 0.85f));
            SetRect(resVal.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(16f, 5f), new Vector2(-32f, 34f));

            Slider resSlider = CreateSlider("Slider_ResourceBar", resPanel,
                new Color(0.15f, 0.18f, 0.22f, 1f), new Color(0.25f, 0.9f, 0.8f, 1f));
            SetRect(resSlider.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 12f), new Vector2(-32f, 14f));

            // Fullscreen Earth Damage Red Flash
            GameObject flashGO = CreatePanel("FX_EarthDamageFlash", canvasGO,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(1f, 0f, 0f, 0.45f));
            Image flashImg = flashGO.GetComponent<Image>();
            flashImg.raycastTarget = false;
            flashGO.SetActive(false);

            // ----------------------------------------------------
            // 2. Weapon Arsenal Selector (Bottom-Center)
            // ----------------------------------------------------
            GameObject wepPanel = CreatePanel("HUD_BottomCenter_Weapons", canvasGO,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 25f), new Vector2(560f, 95f),
                new Color(0.08f, 0.10f, 0.14f, 0.85f));

            string[] wepNames = { "1. LASER", "2. RAILGUN", "3. MINE" };
            string[] defaultCosts = { "10 Res", "25 Res", "50 Res" };
            GameObject[] wepSlots = new GameObject[3];
            Image[] wepHighlights = new Image[3];
            TextMeshProUGUI[] wepCosts = new TextMeshProUGUI[3];

            float slotW = 165f;
            float slotH = 75f;
            float spacing = 15f;
            float startX = -((slotW * 3 + spacing * 2) / 2f) + slotW / 2f;

            for (int i = 0; i < 3; i++)
            {
                float posX = startX + i * (slotW + spacing);
                GameObject slot = CreatePanel($"Slot_{i}_{wepNames[i]}", wepPanel,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(posX, 0f), new Vector2(slotW, slotH),
                    new Color(0.12f, 0.15f, 0.20f, 0.9f));

                // Border / Highlight
                Image highlight = slot.GetComponent<Image>();

                TextMeshProUGUI title = CreateTMPText("Text_Name", slot, wepNames[i], 13f,
                    FontStyles.Bold, TextAlignmentOptions.TopLeft, Color.white);
                SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                    new Vector2(10f, -8f), new Vector2(-20f, 22f));

                TextMeshProUGUI cost = CreateTMPText("Text_Cost", slot, defaultCosts[i], 16f,
                    FontStyles.Bold, TextAlignmentOptions.BottomRight, new Color(0.4f, 0.85f, 1f));
                SetRect(cost.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                    new Vector2(10f, 8f), new Vector2(-20f, 24f));

                wepSlots[i] = slot;
                wepHighlights[i] = highlight;
                wepCosts[i] = cost;
            }

            // ----------------------------------------------------
            // 3. Chain Combo & Rank (Top-Right)
            // ----------------------------------------------------
            GameObject comboPanel = CreatePanel("HUD_TopRight_Combo", canvasGO,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -30f), new Vector2(270f, 110f),
                new Color(0.08f, 0.10f, 0.14f, 0.85f));

            TextMeshProUGUI comboHits = CreateTMPText("Text_ComboHits", comboPanel, "0 HITS", 28f,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.7f, 0.2f));
            SetRect(comboHits.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(16f, 10f), new Vector2(-100f, 36f));

            GameObject gradeBadge = CreatePanel("Badge_Grade", comboPanel,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-16f, 10f), new Vector2(55f, 45f),
                new Color(0.3f, 0.8f, 0.3f, 0.8f));
            Image gradeBadgeImg = gradeBadge.GetComponent<Image>();

            TextMeshProUGUI gradeText = CreateTMPText("Text_Grade", gradeBadge, "D", 26f,
                FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetRect(gradeText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Slider comboBar = CreateSlider("Slider_ComboTimer", comboPanel,
                new Color(0.2f, 0.2f, 0.25f, 1f), new Color(1f, 0.65f, 0.1f, 1f));
            SetRect(comboBar.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 12f), new Vector2(-32f, 12f));

            // ----------------------------------------------------
            // 4. Overload System (Bottom-Right)
            // ----------------------------------------------------
            GameObject overloadPanel = CreatePanel("HUD_BottomRight_Overload", canvasGO,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-30f, 25f), new Vector2(280f, 95f),
                new Color(0.08f, 0.10f, 0.14f, 0.85f));

            TextMeshProUGUI ovLabel = CreateTMPText("Label_Overload", overloadPanel, "OVERLOAD GAUGE", 12f,
                FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.6f, 0.7f, 0.8f));
            SetRect(ovLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -12f), new Vector2(-32f, 20f));

            Slider ovBar = CreateSlider("Slider_OverloadCharge", overloadPanel,
                new Color(0.18f, 0.15f, 0.22f, 1f), new Color(0.85f, 0.25f, 1f, 1f));
            SetRect(ovBar.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(16f, -6f), new Vector2(-32f, 16f));

            GameObject ovPrompt = CreatePanel("Prompt_OverloadReady", overloadPanel,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 8f), new Vector2(-32f, 22f),
                new Color(0.9f, 0.75f, 0.1f, 0.9f));
            TextMeshProUGUI ovPromptText = CreateTMPText("Text_Prompt", ovPrompt, "SPACE - OVERLOAD READY!", 13f,
                FontStyles.Bold, TextAlignmentOptions.Center, Color.black);
            SetRect(ovPromptText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ovPrompt.SetActive(false);

            GameObject ovActiveBanner = CreatePanel("Banner_OverloadActive", overloadPanel,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 8f), new Vector2(-32f, 22f),
                new Color(1f, 0.2f, 0.5f, 0.95f));
            TextMeshProUGUI ovTimer = CreateTMPText("Text_OverloadTimer", ovActiveBanner, "OVERLOAD: 5.0s", 13f,
                FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetRect(ovTimer.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ovActiveBanner.SetActive(false);

            // ----------------------------------------------------
            // 5. Wave & Threat Radar (Top-Center)
            // ----------------------------------------------------
            GameObject wavePanel = CreatePanel("HUD_TopCenter_WaveRadar", canvasGO,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -25f), new Vector2(440f, 85f),
                new Color(0.08f, 0.10f, 0.14f, 0.85f));

            TextMeshProUGUI waveNum = CreateTMPText("Text_WaveNumber", wavePanel, "WAVE 1", 22f,
                FontStyles.Bold, TextAlignmentOptions.TopCenter, Color.white);
            SetRect(waveNum.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(0f, 26f));

            TextMeshProUGUI enemiesRem = CreateTMPText("Text_EnemiesRemaining", wavePanel, "8 Meteors Remaining", 14f,
                FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.75f, 0.85f, 0.95f));
            SetRect(enemiesRem.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f), new Vector2(0f, 22f));

            TextMeshProUGUI waveStatus = CreateTMPText("Text_WaveStatus", wavePanel, "Normal Meteor Swarm", 12f,
                FontStyles.Italic, TextAlignmentOptions.BottomCenter, new Color(1f, 0.85f, 0.4f));
            SetRect(waveStatus.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 6f), new Vector2(0f, 20f));

            // Direction arrow pointer
            GameObject arrowGO = CreatePanel("Arrow_ThreatDirection", wavePanel,
                new Vector2(0.9f, 0.5f), new Vector2(0.9f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(28f, 28f), new Color(1f, 0.3f, 0.3f, 1f));
            RectTransform arrowRect = arrowGO.GetComponent<RectTransform>();
            arrowGO.SetActive(false);

            // ----------------------------------------------------
            // 6. Game Over Debrief Modal
            // ----------------------------------------------------
            GameObject modalRoot = CreatePanel("Modal_GameOver", canvasGO,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.03f, 0.04f, 0.07f, 0.92f));

            GameObject card = CreatePanel("Card_Debrief", modalRoot,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 640f),
                new Color(0.08f, 0.10f, 0.15f, 0.98f));

            TextMeshProUGUI goTitle = CreateTMPText("Text_Title", card, "PLANETARY DEFENSE BREACHED", 30f,
                FontStyles.Bold, TextAlignmentOptions.TopCenter, new Color(1f, 0.3f, 0.3f));
            SetRect(goTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -25f), new Vector2(0f, 40f));

            TextMeshProUGUI goCause = CreateTMPText("Text_CauseOfDeath", card, "Unified resource pool depleted by meteor impact.", 14f,
                FontStyles.Normal, TextAlignmentOptions.TopCenter, new Color(0.7f, 0.75f, 0.85f));
            SetRect(goCause.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(0f, 24f));

            // Score Banner
            GameObject scoreBox = CreatePanel("Box_Score", card,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -145f), new Vector2(680f, 90f),
                new Color(0.12f, 0.15f, 0.22f, 0.85f));

            TextMeshProUGUI scoreVal = CreateTMPText("Text_FinalScore", scoreBox, "0", 38f,
                FontStyles.Bold, TextAlignmentOptions.MidlineCenter, new Color(1f, 0.85f, 0.25f));
            SetRect(scoreVal.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(0f, 42f));

            TextMeshProUGUI highVal = CreateTMPText("Text_HighScore", scoreBox, "High Score: 0", 14f,
                FontStyles.Normal, TextAlignmentOptions.BottomCenter, new Color(0.6f, 0.75f, 0.9f));
            SetRect(highVal.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(0f, 22f));

            GameObject newHighBadge = CreatePanel("Badge_NewHighScore", scoreBox,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-12f, -12f), new Vector2(120f, 28f),
                new Color(1f, 0.4f, 0.1f, 1f));
            TextMeshProUGUI newHighText = CreateTMPText("Text_NewHigh", newHighBadge, "NEW BEST!", 12f,
                FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetRect(newHighText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            newHighBadge.SetActive(false);

            // Debrief Stats Grid (Container)
            GameObject statsGrid = CreatePanel("Grid_Stats", card,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -320f), new Vector2(680f, 220f),
                new Color(0.06f, 0.08f, 0.12f, 0.75f));

            TextMeshProUGUI wavesCleared = CreateStatItem("Stat_WavesCleared", statsGrid, "Waves Cleared", "0", new Vector2(-180f, 75f));
            TextMeshProUGUI totalDestroyed = CreateStatItem("Stat_TotalDestroyed", statsGrid, "Total Destroyed", "0", new Vector2(-180f, 35f));
            TextMeshProUGUI normalDestroyed = CreateStatItem("Stat_Normal", statsGrid, "Normal Meteors", "0", new Vector2(-180f, -5f));
            TextMeshProUGUI speedyDestroyed = CreateStatItem("Stat_Speedy", statsGrid, "Speedy Meteors", "0", new Vector2(-180f, -45f));
            TextMeshProUGUI tankyDestroyed = CreateStatItem("Stat_Tanky", statsGrid, "Tanky Meteors", "0", new Vector2(-180f, -85f));

            TextMeshProUGUI peakCombo = CreateStatItem("Stat_PeakCombo", statsGrid, "Peak Combo Streak", "0", new Vector2(180f, 75f));
            TextMeshProUGUI highestRank = CreateStatItem("Stat_HighestRank", statsGrid, "Highest Rank", "D", new Vector2(180f, 35f));
            TextMeshProUGUI survivalTime = CreateStatItem("Stat_SurvivalTime", statsGrid, "Survival Time", "00:00", new Vector2(180f, -5f));
            TextMeshProUGUI overloadCount = CreateStatItem("Stat_OverloadCount", statsGrid, "Overloads Fired", "0", new Vector2(180f, -45f));
            TextMeshProUGUI magnetDestroyed = CreateStatItem("Stat_Magnet", statsGrid, "Magnet Meteors", "0", new Vector2(180f, -85f));

            // Buttons Row
            GameObject btnRow = CreatePanel("Row_Buttons", card,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(680f, 55f),
                Color.clear);

            Button btnRetry = CreateButton("Button_Retry", btnRow, "RETRY [R]", new Vector2(-230f, 0f), new Vector2(200f, 50f),
                new Color(0.12f, 0.65f, 0.35f, 1f));
            Button btnMenu = CreateButton("Button_MainMenu", btnRow, "MAIN MENU [M]", new Vector2(0f, 0f), new Vector2(200f, 50f),
                new Color(0.25f, 0.35f, 0.48f, 1f));
            Button btnQuit = CreateButton("Button_Quit", btnRow, "QUIT [ESC]", new Vector2(230f, 0f), new Vector2(200f, 50f),
                new Color(0.65f, 0.18f, 0.22f, 1f));

            modalRoot.SetActive(false);

            // ----------------------------------------------------
            // Wire Serialized Properties on GameCanvasUI
            // ----------------------------------------------------
            so.FindProperty("resourceText").objectReferenceValue = resVal;
            so.FindProperty("resourceSlider").objectReferenceValue = resSlider;
            so.FindProperty("earthDamageFlash").objectReferenceValue = flashImg;

            SerializedProperty wepSlotsProp = so.FindProperty("weaponSlots");
            wepSlotsProp.arraySize = 3;
            for (int i = 0; i < 3; i++) wepSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = wepSlots[i];

            SerializedProperty wepHighProp = so.FindProperty("weaponHighlights");
            wepHighProp.arraySize = 3;
            for (int i = 0; i < 3; i++) wepHighProp.GetArrayElementAtIndex(i).objectReferenceValue = wepHighlights[i];

            SerializedProperty wepCostProp = so.FindProperty("weaponCostTexts");
            wepCostProp.arraySize = 3;
            for (int i = 0; i < 3; i++) wepCostProp.GetArrayElementAtIndex(i).objectReferenceValue = wepCosts[i];

            so.FindProperty("comboContainer").objectReferenceValue = comboPanel;
            so.FindProperty("comboHitsText").objectReferenceValue = comboHits;
            so.FindProperty("comboGradeText").objectReferenceValue = gradeText;
            so.FindProperty("comboGradeBadge").objectReferenceValue = gradeBadgeImg;
            so.FindProperty("comboTimerBar").objectReferenceValue = comboBar;

            so.FindProperty("overloadChargeBar").objectReferenceValue = ovBar;
            so.FindProperty("overloadReadyPrompt").objectReferenceValue = ovPrompt;
            so.FindProperty("overloadActiveBanner").objectReferenceValue = ovActiveBanner;
            so.FindProperty("overloadTimerText").objectReferenceValue = ovTimer;

            so.FindProperty("waveNumberText").objectReferenceValue = waveNum;
            so.FindProperty("enemiesRemainingText").objectReferenceValue = enemiesRem;
            so.FindProperty("waveStatusText").objectReferenceValue = waveStatus;
            so.FindProperty("threatDirectionArrow").objectReferenceValue = arrowRect;

            so.FindProperty("gameOverModal").objectReferenceValue = modalRoot;
            so.FindProperty("finalScoreText").objectReferenceValue = scoreVal;
            so.FindProperty("highScoreText").objectReferenceValue = highVal;
            so.FindProperty("newHighScoreBadge").objectReferenceValue = newHighBadge;

            so.FindProperty("wavesClearedText").objectReferenceValue = wavesCleared;
            so.FindProperty("totalDestroyedText").objectReferenceValue = totalDestroyed;
            so.FindProperty("normalDestroyedText").objectReferenceValue = normalDestroyed;
            so.FindProperty("speedyDestroyedText").objectReferenceValue = speedyDestroyed;
            so.FindProperty("tankyDestroyedText").objectReferenceValue = tankyDestroyed;
            so.FindProperty("magnetDestroyedText").objectReferenceValue = magnetDestroyed;

            so.FindProperty("peakComboText").objectReferenceValue = peakCombo;
            so.FindProperty("highestRankText").objectReferenceValue = highestRank;
            so.FindProperty("survivalTimeText").objectReferenceValue = survivalTime;
            so.FindProperty("overloadCountText").objectReferenceValue = overloadCount;
            so.FindProperty("causeOfDeathText").objectReferenceValue = goCause;

            so.FindProperty("retryButton").objectReferenceValue = btnRetry;
            so.FindProperty("mainMenuButton").objectReferenceValue = btnMenu;
            so.FindProperty("quitButton").objectReferenceValue = btnQuit;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(canvasGO);

            return canvasGO;
        }

        private static GameObject CreatePanel(string name, GameObject parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent.transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            SetRect(rt, anchorMin, anchorMax, pivot, pos, size);

            Image img = go.GetComponent<Image>();
            img.color = color;
            return go;
        }

        private static TextMeshProUGUI CreateTMPText(string name, GameObject parent, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent.transform, false);

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Slider CreateSlider(string name, GameObject parent, Color bgColor, Color fillColor)
        {
            GameObject sliderGO = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderGO.transform.SetParent(parent.transform, false);
            Slider slider = sliderGO.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;

            // Background
            GameObject bgGO = CreatePanel("Background", sliderGO, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, bgColor);
            bgGO.GetComponent<Image>().raycastTarget = false;

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGO.transform, false);
            SetRect(fillArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Fill
            GameObject fill = CreatePanel("Fill", fillArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fillColor);
            Image fillImg = fill.GetComponent<Image>();
            fillImg.raycastTarget = false;

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }

        private static Button CreateButton(string name, GameObject parent, string label, Vector2 pos, Vector2 size, Color btnColor)
        {
            GameObject btnGO = CreatePanel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, btnColor);
            Button btn = btnGO.AddComponent<Button>();

            ColorBlock cb = btn.colors;
            cb.normalColor = btnColor;
            cb.highlightedColor = btnColor * 1.25f;
            cb.pressedColor = btnColor * 0.85f;
            cb.selectedColor = btnColor;
            btn.colors = cb;

            TextMeshProUGUI txt = CreateTMPText("Text_Label", btnGO, label, 16f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            SetRect(txt.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            return btn;
        }

        private static TextMeshProUGUI CreateStatItem(string name, GameObject parent, string label, string defaultVal, Vector2 pos)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent.transform, false);
            SetRect(item.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(300f, 30f));

            TextMeshProUGUI lbl = CreateTMPText("Label", item, label, 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Color(0.7f, 0.75f, 0.85f));
            SetRect(lbl.rectTransform, new Vector2(0f, 0f), new Vector2(0.65f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            TextMeshProUGUI val = CreateTMPText("Value", item, defaultVal, 16f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, Color.white);
            SetRect(val.rectTransform, new Vector2(0.65f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);

            return val;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.localScale = Vector3.one;
        }

        private static void SaveCanvasAsPrefab(GameObject canvasGO)
        {
            string dir = Path.GetDirectoryName(PrefabPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGO, PrefabPath, InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
