# Mixtape — Session Handoff

A 3rd-person **physics-based** downhill skateboard **racing** game for **iOS + Android**.
Player races 2 AI "girl" friends down a mountain road; first to the bottom wins.
Built on a pre-made art pack + imported UI mocks. Unity **6000.3.8f1** (URP).

## Repo & environment
- Repo: https://github.com/Wamiq42/SkateBoard  — work on branch **`game-development`** (not yet merged to `main`).
- Unity-MCP bridge ("AI Game Developer" window) must be set to **Custom/Local** (serves `localhost:26591`), NOT Cloud, or the agent can't touch the scene.
- Commit convention: **NO `Co-Authored-By` trailer** on commits.
- Roadmap/notes live in `DEVELOPMENT.md`. This file is the current state of truth.

## Current architecture (IMPORTANT: physics, not waypoints)
The game was first built kinematic/waypoint-based, then **rebuilt to arcade physics** because
waypoint-following felt like "watching, not racing" and ignored the road's up/down terrain.
The OLD path scripts (`RacerMotor`, `SkaterController`, `AIRacer`, `TrackPath`) still exist as
files but are **no longer used** in the Game scene — safe to ignore or delete later.

Core physics scripts (`Assets/_Game/Scripts/Gameplay/`):
- **PhysicsSkater** — arcade Rigidbody skater. Ground-"magnetized" to the road (only a real
  jump or a true cliff-edge leaves the surface), aligns to surface normal (up/down terrain),
  gravity-fed downhill speed, real steering, jump. Shared by player + AI. Key tuning fields:
  baseSpeed 21 / maxSpeed 44 / steerRate 120 / downhillGain 26 / jumpSpeed / groundProbe 6.
  Set its GameObject layer to "Ignore Raycast" (2); groundMask excludes layer 2.
- **RaceRoute** — sparse 23-checkpoint chain (auto-built from the road's painted direction-arrow
  decals). NOT a rail — used only for AI steering targets, race ranking, and respawn. Has
  `Progress(pos)`, `SteerTarget(pos, lookahead)`, `IsFinish(pos)`.
- **AISkater** — steers a PhysicsSkater toward a look-ahead route point + rubber-bands speed.
- **SkaterRespawn** — fall-off-world + off-track + anti-stuck (traffic) recovery.
- **PlayerSkaterInput** — feeds InputService (touch + keyboard) into the player's PhysicsSkater.
- **RaceManager** — countdown (gates each skater's `Active`), live ranking by route progress,
  finish/win-lose events. `Booster`/`Obstacle` act on PhysicsSkater (jump to clear an obstacle).

Other systems (`Scripts/Core`, `Scripts/UI`, `Scripts/Ads`, `Scripts/Data`):
- GameManager (persistent singleton, economy/selection/nav), SaveSystem/PlayerData (PlayerPrefs),
  AudioManager (hooks, no clips yet), SceneFlow, Bootstrapper.
- Ads behind `IAdService` + `StubAdService` + `AdManager` (instant-grant stub; real SDK TBD by owner).
- GameDatabase + CharacterDef ×3 + BoardDef ×5 (ScriptableObjects in `Assets/_Game/Data/`).
- UI: UIFactory, HoldButton, SafeArea, and controllers: MainMenu, HUD, Loading, Selection
  (character+board), Results, Cutscene. uGUI Canvas, ScaleWithScreenSize 1920×1080, Safe Area.

## Scenes & flow (build order in `Assets/_Game/Scenes/`)
Boot → MainMenu → CharacterSelect → BoardSelect → Cutscene → Loading → Game → (Results overlay).
- **Game.unity** is the live physics race: `RaceRoute`, `Player`+`AI_1`+`AI_2` (physics),
  `_Race` (RaceManager+InputService), `Systems` (GameManager+AudioManager prefab), `HUD`,
  `Interactions` (boosters/obstacles), `Main Camera` (CameraFollow), the `TRACK`.

## How to run / test
Open `Assets/_Game/Scenes/Game.unity`, press Play → countdown → race.
Controls: **arrow keys / A-D steer, Space jump**; on-screen HUD buttons also work.
(Editor play mode sometimes self-exits on domain reload — just re-enter.)

## ✅ What works (verified in play)
- Full front-end flow Boot→…→Game→Results, persistence (coins/unlocks/selection/settings).
- Physics race: countdown, controllable player, 2 AI driving the route, live ranking, finish,
  **Level Complete** screen + coin reward (the win-screen SetActive/Awake bug is fixed via CanvasGroup).
- Player rides the real road incl. descents/undulation; respawn + anti-stuck keep it safe.
- HUD, menus, and selection screens match the imported mocks (verified via render captures).

## ⚠️ Remaining work / known issues (good things to assign)
1. **AI obstacle-avoidance** — AI still rely on the anti-stuck *nudge* at the parked-cars jam
   (~65% down the course) instead of *weaving*. Add raycast-and-dodge steering to AISkater.
2. **Feel tuning pass** — drive it and adjust PhysicsSkater (speed/steer/grip/jump) +
   CameraFollow (distance/height/smoothing). Target: "arcade & fast". All inspector values.
3. **Placeholder art** — boosters = green cubes, obstacles = red cubes. Swap for the cones/
   barricades in `Assets/skate board 1/` (the pack has road barriers, cones, signs).
4. **3rd character model** — only 2 of 3 unique meshes read as girls; `Girl 3.asset`
   (`Assets/_Game/Data/Characters/`) points to an androgynous/male mesh. One-field swap.
5. **Animations** — characters are T-posed. Animator hooks exist; add clips + Animator Controller.
6. **Ads SDK** — implement `IAdService` with the real network, `AdManager.SetService(...)` at boot.
7. **Audio** — assign clips to AudioManager.
8. **Route end** — race finishes at the last arrow decal (~z 852), a bit before the true road
   end; extend the route if you want it to run to the tarmac end.
9. **Cutscene** staging is minimal (DRAW.fbx is a flat tile, not a room).

## Commit log (branch game-development, newest first)
- ec4b76b  Integrate physics race (player+AI physics, rewire systems, win-screen fix)
- 6ffc59d  Add physics-based skater system (prototype)
- 4632140  Fix race path via arrow decals (pre-physics; path now unused)
- 566a05e  Track interactions + opening cutscene
- ed5851a  Full UI flow (HUD, menu, loading, selection, results)
- 3b328d2  Real characters/boards + rider visuals
- 0fddea5  Game foundation + core engine
