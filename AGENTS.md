# GI351: Doom Drawing - Space Defense Game Design & Architecture

## 1. Game Concept Overview
A 2D wave-defense shooter set in Earth's orbit. The player controls a cannon gunner defending Earth against waves of incoming asteroids that are constantly pulled towards Earth by gravity.

- **Perspective:** 2D top-down orbital view. Earth is at the center (default (0, 0, 0)).
- **Enemy Spawning (360° Ring):** Enemies spawn all around Earth from full 360 degrees (top, bottom, left, right, diagonals) and are pulled inward toward Earth's center by gravity.
- **Player Position & Defense:** Gunner defends Earth along its orbit, capable of engaging threats arriving from any 360° angle.

---

## 2. Core Mechanics

### 2.1 Player Orbit & Movement
- **Control Scheme:** Orbital Mouse-Direction with Pull Force.
- **Center:** Centered on Earth (`earth` reference or auto-detected).
- **Orbit Distance:** Fixed radius ring around Earth (auto-detected from initial placement or set via `orbitDistance`).
- **Pull Force & Speed:** Rather than snapping instantly to the mouse angle, the cannon rotates along the orbit ring towards the mouse direction with physical inertia (`smoothTime`) and a maximum angular speed (`turnSpeed` in deg/s).
- **Outward Facing:** Cannon faces outward into space along the orbit normal to engage 360° incoming threats.

### 2.2 Weapons & Bullets
- **Primary Weapon (Left Click):**
  - Full-auto continuous fire while holding left mouse button (`Mouse.current.leftButton.isPressed`).
  - Standard `Bullet.cs` projectile with speed, lifetime, damage, and physics knockback.
- **Secondary Weapon (Right Click):**
  - Ricochet bullet (`Bullet_Ricochet.cs`), semi-auto per click (`wasPressedThisFrame`).
  - **Chain Ricochet:** Jumps from enemy to enemy within `search_radius` around the hit enemy, prioritized to closest un-hit enemy on the `Enemy` layer.
  - **Durability (HP):** Default `maxhp = 3`. Each enemy hit costs 1 HP. Bullet is destroyed when HP reaches 0.
  - **Physical Bounce Fallback:** If no other enemy is in range, the bullet reflects physically off the surface using `Vector2.Reflect` like a real projectile.
  - **Smooth Rotation:** Rotates smoothly towards travel direction with sprite facing offset (`-90°` default).
  - **Solid Collisions:** Uses solid colliders (`isTrigger = false`) so bullets physically push asteroids upon impact.

### 2.3 Asteroids & Physics
- **Variants:**
  - *Weak:* 1 HP (destroyed in 1 hit).
  - *Medium:* Higher HP (takes multiple hits).
- **Impact & Knockback:** Surviving asteroids get knocked back by bullet momentum.
- **Gravity Toward Earth:** Realistic gravitational attraction pulls asteroids back towards Earth, decelerating knockback and accelerating asteroids towards the center.
- **Asteroid-to-Asteroid Damage:** Asteroids deal fixed damage to each other upon colliding (`Rock.TakeDamage`).
- **Chain Collisions / Forward Thrust Accident (Intended Feature):** Asteroids can collide and accidentally push another asteroid forward towards Earth faster. This emergent behavior is explicitly intended.

---

## 3. Script Structure & References
### 2.4 Combo & Grade System
- **Grades:** F -> D -> C -> B -> A -> A+ -> S.
- **Timer & Reset:** 10-second decay timer (`comboTimeout`). Started on the first hit at Grade F. If 10 seconds elapse without hitting an enemy, combo resets. Every hit refreshes the timer back to 10 seconds.
- **Progression Thresholds (Hits required within each grade):**
  - **F -> D:** 10 hits
  - **D -> C:** 20 hits
  - **C -> B:** 40 hits
  - **B -> A:** 60 hits
  - **A -> A+:** 120 hits
  - **A+ -> S:** 240 hits
  - **S:** Max grade. No further ranks; hit count continues indefinitely.
- **HUD Presentation:** Progression goals (e.g. `X / 20`) are hidden from the player to keep rank-ups a surprise; only the current Grade letter, total hit count (e.g. `34 Hits COMBO`), and the 10s timer bar are displayed.
- **Hit Detection:** Triggered whenever a standard bullet (`Bullet.cs`) or ricochet bullet (`Bullet_Ricochet.cs`) damages an asteroid. Asteroid-to-asteroid collisions do not count toward player combo.

### 2.5 Ammunition & Resource Drops
- **Limited Ammo Supply:**
  - Primary (Regular): Default 90 ammo, consumes 1 per shot.
  - Secondary (Ricochet): Default 15 ammo, consumes 1 per shot.
  - Weapon cannot fire when corresponding ammo reaches 0.
- **Drop Chance on Destruction (`Rock.cs`):**
  - When an asteroid reaches 0 HP, it rolls chances to drop ammunition:
    - **Regular Ammo Drop:** Fixed 30 ammo per drop (default 40% roll chance).
    - **Ricochet Ammo Drop:** Fixed 5 ammo per drop (default 20% roll chance).
- **Gathering Mechanic (Shoot to Collect):**
  - Ammunition drops spawn into space as floating resource containers (`AmmoDrop.cs`).
  - The player gathers them by **shooting at them** with either bullet type.
  - Collecting a drop instantly credits the ammo to `Player_controller.Instance` and removes the drop.
  - Drops have a slight drift and a despawn timer if uncollected.

### 2.6 Bulk / Chunk Spawning & Anti-Collision Formation
- **Chunk-Based Spawning (`AsteroidSpawner.cs`):**
  - Instead of spawning single asteroids randomly around Earth, asteroids spawn in coordinated **chunks / swarms** (default 3 to 5 per chunk) arriving from designated sectors.
  - Between successive chunks in a wave, a configurable pause (`timeBetweenChunks = 3.0s`) gives the player time to engage each cluster.
- **Formation Geometry & Spatial Separation:**
  - **Radial Depth Staggering:** Asteroids within a chunk are staggered by depth (`radialDepthStep = 1.8f`). Since asteroids closer to Earth experience higher gravitational pull ($a = G / r^2$) and accelerate inward faster, lead asteroids naturally pull ahead of rear asteroids during flight, preventing rear-end collisions.
  - **Lateral Spread & Guaranteed Clearance:** Asteroids are distributed across an arc (`chunkSpreadAngle = 30°`) with an enforced minimum placement clearance (`minSeparationDistance = 2.2f`).
- **Dynamic Flight Separation & Anti-Suicide (`Rock.cs`):**
  - **Flocking / Mutual Repulsion:** While falling toward Earth, asteroids exert a gentle repulsive force against each other within `separationRadius = 2.0f` (`separationForce = 15f`) to keep their approach corridors clear and prevent bunching up near Earth.
  - **Spawn Damage Grace Period:** Asteroids are immune to friendly collision damage for `1.5s` after spawning (`spawnDamageGracePeriod`), preventing accidental instant destruction upon entry.
### 2.7 Death Explosion & Chain Reactions
- **Destruction Blast (`Rock.cs`):**
  - When an asteroid reaches 0 HP, it detonates in a death explosion (`explosionRadius = 3.0f`).
  - **Damage Calculation (Half Health):** Deals half of its max health as blast damage (`Mathf.Max(1, Mathf.RoundToInt(maxhp * 0.5f))`).
    - *Weak Asteroid (1 max HP):* Deals 1 damage.
    - *Medium Asteroid (3 max HP):* Deals 2 damage.
  - **Impact Push Impulse:** Emits a powerful radial impulse (`explosionForce = 12f`) that hurls all surrounding asteroids outward with distance-based falloff.
  - **Chain Reaction:** If an asteroid's explosion deals lethal damage to neighboring asteroids, they also detonate, triggering cascading chain reactions across asteroid clusters.
  - **Secondary Collisions:** Asteroids propelled at high velocity by the explosion blast can slam into other asteroids, triggering high-speed impact collision damage and forward thrust accidents.
  - **Combo Contribution:** Explosion hits can contribute to the combo hit counter (`explosionCountsForCombo = true`).

---

## 3. Script Structure & References
- `Player_controller.cs`: Orbit rotation around Earth (mouse pull force), outward aiming, weapon firing, ammo tracking, and ammo HUD.
- `Bullet.cs`: Base bullet class with speed, linear velocity, damage, collision handling, and ammo drop collection check.
- `Bullet_Ricochet.cs`: Inherits from `Bullet`, handles chaining, target filtering by layer, physical bounce fallback, smooth rotation, and ammo drop collection check.
- `ComboManager.cs`: Singleton tracking combo timer, grade progression, events (`OnGradeUp`, `OnComboUpdated`, `OnComboReset`), and on-screen HUD.
- `Rock.cs`: Asteroid script tracking HP, handling damage, mutual flocking separation, collision speed threshold, death explosion blast with push impulse and chain reactions, and rolling ammo drop chances upon destruction.
- `AmmoDrop.cs`: Floating ammo pickup in space that grants ammo when shot by the player.
- `AsteroidSpawner.cs`: Wave spawner attached to Earth, spawning incoming asteroid threats in bulk chunks with anti-collision formation spacing.

---

## 4. Current State & Pending Roadmap
- [x] Full-auto primary fire & right-click ricochet firing.
- [x] Chain ricochet targeting closest enemy on `Enemy` layer.
- [x] Physical bounce fallback when no enemy is nearby.
- [x] Smooth bullet rotation with sprite offset (-90°).
- [x] Fix Simulated checkbox on `Arrow.prefab`.
- [x] Player orbital movement with mouse pull force.
- [x] Combo system with 10s timer and grade progression (F -> D -> C -> B -> A -> A+ -> S).
- [x] Central gravity pull on asteroids towards Earth.
- [x] Bulk chunk spawning with anti-collision spacing and approach formation.
- [x] Asteroid death explosion (50% max HP damage, radial impulse push, and chain reaction cascading).
- [ ] (Future) Earth health and game-over conditions (held for later phase per PM).
