# GI351: Doom Drawing — UI Canvas Team Guide & Reference

This guide is prepared for UI artists, technical artists, and designers who want to design and build their own custom UI in Unity using the **uGUI Canvas** system without writing boilerplate C# code.

---

## 1. Quick Start: 1-Click HUD Generator

We created a custom Unity Editor tool so you don't have to build the Canvas from scratch or wire 25+ fields by hand.

### How to Generate:
1. In the Unity Editor top menu bar, click:
   **`Tools` > `GI351` > `Generate Canvas HUD Template`**
   *(or right-click in the Hierarchy: `UI` > `GI351 Canvas HUD Template`)*
2. Unity will automatically:
   - Check and create an `EventSystem` if your scene doesn't have one.
   - Create a clean `GameHUD_Canvas` hierarchy set to **1920×1080 (Scale With Screen Size)**.
   - Populate standard `TextMeshProUGUI`, `Slider`, `Image`, and `Button` elements for all game features.
   - Wire every serialized reference directly into `GameCanvasUI.cs`.
   - Save a reusable prefab at:
     ```
     Assets/Prefab/UI/GameHUD_Canvas.prefab
     ```
3. Press **Play** in Unity — your UI is instantly live and working!

---

## 2. Canvas Hierarchy Breakdown

The generated Canvas is grouped into clean, self-contained sections:

```
GameHUD_Canvas (Root with Canvas, CanvasScaler, GraphicRaycaster, GameCanvasUI)
│
├── HUD_TopLeft_Resources          <- Resource counter & planetary defense bar
│   ├── Label_Defense              (TMP: "PLANETARY DEFENSE POOL")
│   ├── Text_ResourceCount         (TMP: "100 Res" - live updates)
│   └── Slider_ResourceBar         (Slider: visual capacity gauge)
│
├── HUD_BottomCenter_Weapons       <- Weapon arsenal selector
│   ├── Slot_0_Laser               (Border/Highlight, Name, Cost: "10 Res")
│   ├── Slot_1_Railgun             (Border/Highlight, Name, Cost: "25 Res")
│   └── Slot_2_Mine                (Border/Highlight, Name, Cost: "50 Res")
│
├── HUD_TopRight_Combo             <- Chain combo streak, rank & decay bar
│   ├── Text_ComboHits             (TMP: "18 HITS")
│   ├── Badge_Grade                (Image tinting by grade: D, C, B, A, S, S+)
│   │   └── Text_Grade             (TMP: Grade letter)
│   └── Slider_ComboTimer          (Slider: 2.0s+ decay window countdown)
│
├── HUD_BottomRight_Overload       <- Overload gauge & activation alert
│   ├── Label_Overload             (TMP: "OVERLOAD GAUGE")
│   ├── Slider_OverloadCharge      (Slider: 0% -> 100% progress)
│   ├── Prompt_OverloadReady       (Panel: "SPACE - OVERLOAD READY!")
│   └── Banner_OverloadActive      (Panel & TMP: "OVERLOAD: 4.8s")
│
├── HUD_TopCenter_WaveRadar        <- Wave progression & incoming threat warning
│   ├── Text_WaveNumber            (TMP: "WAVE 1")
│   ├── Text_EnemiesRemaining      (TMP: "8 Meteors Remaining" or countdown)
│   ├── Text_WaveStatus            (TMP: Sector angle & threat introduction)
│   └── Arrow_ThreatDirection      (RectTransform: points toward incoming sector)
│
├── FX_EarthDamageFlash            <- Fullscreen red vignette flashing on Earth hit
│
└── Modal_GameOver                 <- Game Over Debrief Screen (starts inactive)
    └── Card_Debrief
        ├── Text_Title             ("PLANETARY DEFENSE BREACHED")
        ├── Text_CauseOfDeath      ("Resource pool depleted by meteor impact")
        ├── Box_Score              (Final Score, High Score, "NEW BEST!" badge)
        ├── Grid_Stats             (Detailed kill breakdown & survival stats)
        └── Row_Buttons            (RETRY [R], MAIN MENU [M], QUIT [ESC])
```

---

## 3. How to Style & Customize (Artist / Designer Guide)

### 3.1 Styling Visuals
- **Custom Sprites:** Select any `Image` component (such as slot frames, bars, panels, or buttons) and assign your own 2D sprites in the **Source Image** slot.
- **Custom Fonts:** Select any `TextMeshProUGUI` component and assign your team's Font Asset into the **Font Asset** field.
- **Color Palettes:** Adjust panel tints, highlight borders, or button colors directly in the Inspector.

### 3.2 Rearranging & Resizing
- You are free to move panels anywhere on screen or change their **Anchor Presets** (e.g. shift Weapons to the left, move the Combo panel to the center).
- `GameCanvasUI.cs` only updates values and text; it never forces or hardcodes RectTransform positions or sizes.

### 3.3 Deleting or Omitting Elements (100% Null-Safe)
- If your team decides you don't need a specific element (for example, you don't want the `Slider_ResourceBar` or the `Arrow_ThreatDirection`):
  - You can safely **delete** the GameObject, or leave the slot blank (`None`) on `GameCanvasUI`.
  - `GameCanvasUI.cs` performs null-checks on every single field. Missing elements will **never** throw `NullReferenceException`.

---

## 4. Key Gameplay Behaviors Handled by `GameCanvasUI`

| UI Section | Gameplay Behavior Connected |
| :--- | :--- |
| **Resources** | Updates to `Player_controller.Instance.Resources`. When Earth is struck by a meteor, `FX_EarthDamageFlash` flashes red. |
| **Weapons** | Highlight automatically moves to weapon slot 0 (Laser), 1 (Railgun), or 2 (Mine) when the player presses `[1]`, `[2]`, `[3]` or scrolls the mouse wheel. |
| **Overload Mode** | When Overload triggers: weapon costs automatically display **`FREE`** in gold text, the Overload active banner turns on, and the 5-second countdown timer runs. |
| **Combo & Rank** | `Text_ComboHits` updates per hit. `Badge_Grade` and `Text_Grade` automatically switch color and letter (D $\rightarrow$ C $\rightarrow$ B $\rightarrow$ A $\rightarrow$ S $\rightarrow$ S+). `Slider_ComboTimer` drains smoothly. |
| **Wave Radar** | Shows the active wave, meteors remaining, and upcoming spawn sector direction. |
| **Game Over Screen** | Automatically pops up when planetary resources hit 0 from an asteroid impact. Shows Final Score, High Score, "NEW BEST!" badge, breakdown of Normal/Speedy/Tanky/Magnet kills, peak streak, survival time, and overload count. |

---

## 5. Game Over Buttons & Scene Navigation

The Game Over modal buttons are wired automatically:
- **`Button_Retry`:** Calls `ComboManager.Instance.RestartGame()`. Reloads the game scene fresh. *(Hotkey: `[R]` also works)*.
- **`Button_MainMenu`:** Calls `ComboManager.Instance.ReturnToMainMenu()`. Returns to `GameMenu` scene. *(Hotkey: `[M]` also works)*.
- **`Button_Quit`:** Calls `Application.Quit()`. *(Hotkey: `[ESC]` also works)*.

---

## 6. Legacy IMGUI (`OnGUI`) Toggle

Previously, gameplay numbers were displayed using old programmer text (`GUI.Label` inside `OnGUI`).

- `GameCanvasUI` includes an Inspector checkbox: **`Disable Legacy On GUI`** (enabled by default).
- When checked, it automatically suppresses the old developer text so only your clean Canvas HUD is visible.
- If you ever need to inspect internal physics or debug stats, unchecking that box will restore the developer IMGUI text.

---

## 7. Programmer Reference (Data Accessors)

If your team needs to write custom UI scripts or extra visual hooks, all core managers expose clean public properties:

### `Player_controller.Instance`
- `Resources`: Current unified resource amount (`int`).
- `CurrentWeapon`: Currently selected weapon enum (`LaserCannon`, `Railgun`, `DisposableMine`).
- `LaserCannonCost`, `RailgunCost`, `DisposableMineCost`: Weapon resource costs (`int`).
- `EarthDamageAlertTimer`: > 0 when Earth was recently hit (`float`).
- `LastEarthDamageDrain`: Amount drained by the last impact (`int`).

### `ComboManager.Instance`
- `TotalComboHits`: Current active combo streak (`int`).
- `CurrentGrade`: Current rank enum (`D`, `C`, `B`, `A`, `S`, `S_Plus`).
- `RemainingTime`: Seconds remaining before combo expires (`float`).
- `ActiveTimeout`: Total active window duration including dynamic extensions (`float`).
- `IsOverloadReady`: True when combo milestone is reached (`bool`).
- `IsOverloadActive`: True while 5-second free firing is active (`bool`).
- `OverloadProgress`: 0.0 to 1.0 progress toward Overload (`float`).
- `OverloadRemainingTime`: Seconds remaining of Overload burst (`float`).
- `CurrentScore`, `HighScore`, `IsNewHighScore`: Score tracking.
- `TotalAsteroidsDestroyed`, `NormalDestroyed`, `SpeedyDestroyed`, `TankyDestroyed`, `MagnetDestroyed`: Kill statistics.
- `MaxComboHits`, `HighestGrade`, `SurvivalDuration`, `OverloadActivations`: Performance metrics.

### `AsteroidSpawner.Instance`
- `CurrentWave`: Active wave number (`int`).
- `ActiveAsteroidCount`: Count of living asteroids (`int`).
- `CurrentState`: `Spawning`, `WaveActive`, or `Intermission`.
- `WaveTimer`: Countdown timer during intermission (`float`).
- `NextSpawnAngle`: Direction angle in degrees of upcoming swarm (`float`).
- `CurrentThreatDescription`: Summary of active wave threat types (`string`).
