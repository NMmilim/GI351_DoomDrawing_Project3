# GI351: Doom Drawing - Space Defense Game Design & Architecture

## 1. Game Concept Overview
A 2D orbital wave-defense shooter set in Earth's orbit. The player controls an orbital cannon gunner defending Earth against incoming waves of asteroids and meteors pulled inward by Earth's gravity.

- **Perspective:** 2D top-down orbital view. Earth is at the center (0, 0, 0).
- **Game Mode:** Endless Survival. The player aims to survive as long as possible while maximizing Chain Combo multipliers and resource scores.
- **Game Over Condition:** Sudden Death. The game ends immediately when ANY meteor / asteroid collides with Earth before being destroyed.
- **Player Defense:** Gunner rotates along an orbital ring around Earth, facing outward to engage 360° incoming threats.

---

## 2. Core Mechanics

### 2.1 Player Orbit & Movement
- **Control Scheme:** Orbital Mouse-Direction with Pull Force.
- **Center:** Centered on Earth (`earth` reference or auto-detected).
- **Orbit Distance:** Fixed radius ring around Earth (`orbitDistance`).
- **Physical Pull Force & Speed:** Cannon rotates along the orbit ring towards the mouse direction with physical inertia (`smoothTime`) and maximum angular speed (`turnSpeed` in deg/s).
- **Outward Facing:** Cannon faces outward into space along the orbit normal to engage incoming threats.

---

### 2.2 Weapons & Arsenal (Unified Resource Economy)
All weapons consume a shared **Resource Pool** earned from destroying meteors.

#### 1. Laser Cannon (Primary Blaster)
- **Firing Mode:** Straight projectile forward along aim vector.
- **Pierce:** 0 (stops on first enemy hit).
- **Projectile Speed:** Medium.
- **Special Ability:** None (standard rapid-fire cannon).
- **Cost:** 10 resources per shot.

#### 2. Railgun (Heavy Penetration Weapon)
- **Firing Mode:** High-velocity straight beam/slug projectile.
- **Pierce:** 1 (penetrates through 1 enemy, striking up to 2 targets in a line).
- **Projectile Speed:** High.
- **Armor-Bypassing Trait:** Can directly penetrate and destroy **Tanky** and **Magnet** meteors without requiring an external explosion.
- **Cost:** 25 resources per shot.

#### 3. Disposable Mine (Tactical Deployable)
- **Firing Mode:** Launches a deployable mine toward the mouse cursor's world position.
- **Projectile Speed:** Slow flight to destination.
- **Manual Trigger Requirement:** Does NOT detonate automatically on contact. Must be manually detonated by shooting it with the **Laser Cannon** or **Railgun**.
- **Dynamic Blast Radius Scaling:**
  - Blast radius expands the longer the mine remains active in space.
  - If left deployed for too long past its peak, the blast radius decays/shrinks and the mine eventually fizzles out.
- **Cost:** 50 resources per deployment.

---

### 2.3 Overload System (ระบบ Overload)
- **Combo Threshold:** Reaching designated combo milestones charges the Overload gauge.
- **Manual Activation:** When the threshold is reached, the player activates Overload via a hotkey (e.g. Spacebar / Shift).
- **Effect:** Grants **unlimited free firing** (all weapon costs reduced to 0) for a short duration, enabling massive rapid-fire destruction.

---

### 2.4 Meteor Types & Behaviors (ประเภทอุกกาบาต)

| Meteor Type | Size | Speed | Blast / Effect Radius | Special Trait | Base Drop | Chain Cost Formula |
| :--- | :---: | :---: | :---: | :--- | :---: | :---: |
| **Normal** | Medium (1.0) | Medium | $2.5\times$ Size ($r=2.5$) | None. Standard exploding asteroid. | 7 | $5 + \lfloor\frac{\text{Combo}}{2}\rfloor$ |
| **Speedy** | Small (<1.0) | Fast | $1.5\times$ Size | Fast agile threat. | 3 | $10 + \lfloor\frac{\text{Combo}}{2}\rfloor$ |
| **Tanky** | Large (>1.0) | Slow | $1.5\times$ Size | **Immune to normal fire (Laser Cannon).** Must be destroyed by explosions or Railgun. | 5 | $7 + \lfloor\frac{\text{Combo}}{2}\rfloor$ |
| **Magnet** | Medium (1.0) | Slow | $5.0\times$ Size (Vortex) | **Immune to normal fire.** On death, does NOT deal blast damage; creates a gravitational vortex pulling all nearby meteors into a tight cluster. | 8 | $15 + \lfloor\frac{\text{Combo}}{2}\rfloor$ |

#### Detailed Meteor Specifications:
1. **Normal Meteor:**
   - Balanced speed and medium size.
   - Detonates on death with a blast radius equal to $2.5\times$ its body radius (e.g. size 1.0 $\rightarrow$ blast radius 2.5).
   - Yields 7 base resources on first destruction.
2. **Speedy Meteor:**
   - Small hitbox moving at high velocity toward Earth.
   - Blast radius of $1.5\times$ its size.
   - Yields 3 base resources. Chain cost: $10 + \frac{\text{combo}}{2}$.
3. **Tanky Meteor:**
   - Heavy, slow-moving asteroid with high presence.
   - Cannot be damaged or destroyed by the Laser Cannon.
   - Destruction requires an explosive blast from a neighboring meteor/mine OR a direct shot from the **Railgun**.
   - Blast radius of $1.5\times$ its size. Yields 5 base resources. Chain cost: $7 + \frac{\text{combo}}{2}$.
4. **Magnet Meteor:**
   - Slow-moving utility asteroid.
   - Cannot be damaged by the Laser Cannon; requires an explosion or Railgun shot.
   - **Vortex Implosion:** On death, does not deal damage. Instead emits an inward gravitational pull covering $5.0\times$ its size, dragging all surrounding meteors inward into a tight cluster for easy chaining.
   - Yields 8 base resources. Chain cost: $15 + \frac{\text{combo}}{2}$.

---

### 2.5 Chain Combo System & Resource Formula (ระบบ Chain Combo)
- **Initiation:** Combo counter begins incrementing from the first meteor destroyed.
- **Decay Timer Window:**
  - Base window is **2.0 seconds** (`comboTimeout = 2.0s`).
  - If 2.0 seconds elapse without destroying an enemy, the combo resets to 0.
- **Dynamic Time Extension:**
  - For every **15 combo** achieved, the active timer window expands by **+0.5 seconds** (e.g. 15 combo $\rightarrow$ 2.5s, 30 combo $\rightarrow$ 3.0s, etc.).
- **Chain Resource Calculation:**
  - 1st kill grants the meteor's flat **Base Drop** (e.g. Normal = 7).
  - Every subsequent kill in the chain grants resources according to:
    $$\text{Earned Resource} = \text{BaseChainCost} + \lfloor\frac{\text{CurrentCombo}}{2}\rfloor$$
    *(where Combo increments by 1 for each destruction in the chain)*

#### Calculation Examples:
- **Example 1 (5 Normal Meteors Destroyed in Chain):**
  - Kill 1: $7$ (Base return)
  - Kill 2 (Combo 1): $5 + \lfloor\frac{1}{2}\rfloor = 5$
  - Kill 3 (Combo 2): $5 + \lfloor\frac{2}{2}\rfloor = 6$
  - Kill 4 (Combo 3): $5 + \lfloor\frac{3}{2}\rfloor = 6$
  - Kill 5 (Combo 4): $5 + \lfloor\frac{4}{2}\rfloor = 7$
  - **Total Earned:** $7 + [5+0] + [5+1] + [5+1] + [5+2] = 31$ resources.

- **Example 2 (2 Normal $\rightarrow$ 1 Speedy $\rightarrow$ 1 Tanky):**
  - Kill 1 (Normal): $7$
  - Kill 2 (Normal, Combo 1): $5 + \lfloor\frac{1}{2}\rfloor = 5$
  - Kill 3 (Speedy, Combo 2): $10 + \lfloor\frac{2}{2}\rfloor = 11$
  - Kill 4 (Tanky, Combo 3): $7 + \lfloor\frac{3}{2}\rfloor = 8$
  - **Total Earned:** $7 + 5 + 11 + 8 = 31$ resources.

---

### 2.6 Spawning, Flight & Anti-Collision Formation
- **Bulk Chunk Spawning (`AsteroidSpawner.cs`):** Meteors spawn in coordinated swarms (3-5 per chunk) arriving from designated sectors around Earth.
- **Radial Depth Staggering (`radialDepthStep = 1.8f`):** Lead meteors spawn closer to Earth and experience higher gravitational pull ($a = \frac{G}{r^2}$), naturally pulling ahead of rear meteors to eliminate rear-end collisions during flight.
- **Angular Spread & Clearance (`minSeparationDistance = 2.2f`):** Spread across a $30^\circ$ arc with enforced minimum spawn distance.
- **Dynamic Flocking & Anti-Collision (`Rock.cs`):**
  - Mutual soft repulsion (`separationRadius = 2.0f`, `separationForce = 15f`) prevents crowding as meteors approach Earth.
  - Spawn grace period (1.5s) and relative impact velocity threshold ($\ge 2.5\text{ m/s}$) ensure asteroids only damage each other during high-velocity impacts or intended explosions.

---

## 3. Script Structure & Architecture

- `Player_controller.cs`: Orbital movement, aiming, weapon selection (Laser Cannon, Railgun, Disposable Mine), Overload activation, and unified resource tracking/HUD.
- `Bullet.cs`: Base projectile class handling velocity, collision, and damage.
- `RailgunBullet.cs`: Penetrating high-speed projectile with pierce count (Pierce = 1) and armor-bypass capability against Tanky/Magnet meteors.
- `DisposableMine.cs`: Deployable mine script managing slow travel to mouse position, dynamic radius scaling over time, shoot-to-detonate trigger, and explosion blast.
- `ChainLightningBeam.cs`: (Utility/VFX) Procedural electric beam and fade-out trail renderer.
- `Rock.cs`: Meteor base script with polymorphic/configured variants:
  - Tracks HP, speed, size, and immunity flags (`immuneToNormalFire`).
  - Handles death effects: standard damage explosion vs Magnet implosion vortex.
  - Computes dynamic explosion radius based on body size ($2.5\times$ or $1.5\times$ body size).
- `ComboManager.cs`: Singleton managing the 2.0s decay window, +0.5s extension per 15 combo, chain resource formulas, Overload threshold tracking, and UI display.
- `AsteroidSpawner.cs`: 360-degree wave spawner managing weighted spawn rates of Normal, Speedy, Tanky, and Magnet meteors in spaced chunks.

---

## 4. Development Roadmap & Implementation Status

- [x] Player orbital movement with mouse pull force and inertia.
- [x] Gravitational pull towards Earth and central orbital physics.
- [x] Bulk chunk spawning with anti-collision spacing and depth staggering.
- [x] Mutual flocking separation and low-speed bump protection.
- [x] Chain reaction death explosion logic with physical impulse push.
- [x] Chain Lightning secondary beam with cooldown and procedural electric visuals.
- [x] **Transition to Unified Resource Economy:** Replace isolated ammo counts with a single Resource pool.
- [x] **Implement 4 Meteor Types (`Rock.cs`):**
  - [x] Normal (Medium, 2.5x blast, 7 base, $5 + \text{combo}/2$).
  - [x] Speedy (Fast, small, 1.5x blast, 3 base, $10 + \text{combo}/2$).
  - [x] Tanky (Slow, large, normal-fire immune, 5 base, $7 + \text{combo}/2$).
  - [x] Magnet (Slow, medium, normal-fire immune, 5x vortex pull on death, 8 base, $15 + \text{combo}/2$).
- [x] **Chain Combo System Upgrade (`ComboManager.cs`):**
  - [x] 2.0s base decay window + 0.5s extension per 15 combo.
  - [x] Dynamic chain resource calculation formulas based on destroyed meteor type.
- [x] **Overload System:**
  - [x] Combo milestone charge gauge.
  - [x] Activation key trigger for unlimited free firing duration.
- [x] **Endless Mode & Instant Game Over:**
  - [x] Instant Game Over when any meteor impacts Earth's surface.
- [x] **Weapon Arsenal Expansion:**
  - [x] Laser Cannon (Cost: 10, Pierce: 0, Medium speed).
  - [x] Railgun (Cost: 25, Pierce: 1, High speed, destroys Tanky/Magnet).
  - [x] Disposable Mine (Cost: 50, Slow, shoot-to-detonate, time-scaling radius).
