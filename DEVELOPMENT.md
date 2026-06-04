# Mixtape — Development Plan

A 3rd-person, auto-forward skateboard **racing** game (mobile: iOS + Android).
Player races 2 AI friends down a hill; first to the bottom wins. Built on the
existing `skate board 1` art pack + imported UI mocks in `Assets/Art/UI/`.

## Decisions (locked)
- **Scope:** Full game, built in milestones.
- **Movement:** Auto-forward; player steers left/right + jump / double-jump (runner-style on a road).
- **Platform:** Android + iOS. Touch controls in-game; keyboard mirror for editor testing.
- **UI:** Unity uGUI (Canvas). Canvas Scaler = Scale With Screen Size (1080x1920 ref, match 0.5)
  + Safe Area handling for notches. Sprites from `Assets/Art/UI/`.
- **Characters:** 3 girls = unique meshes CHART_1, CHART_2, CHART_3 (from A–G imports).
- **Skateboards:** 5+ from `skate board/` (skate 1, 2, 3, 4, 5, SKAT 2).
- **Animations:** imported later — Animator Controllers stubbed with named states/params now.
- **Ads:** logic + buttons/panels now, wired to a stub `IAdService`; real SDK dropped in later by owner.

## Asset locations
- Track/level + obstacles: scene `Assets/skate board 1/saktter/Scenes/SKATEBOARD.unity` (TRACK root).
- Characters (rigged, mixamo): `Assets/skate board 1/skate board/CHART/*.fbx`.
- Skateboards: `Assets/skate board 1/skate board/{1..5}`, `Skateboard AA`.
- Drawing room (cutscene): `Assets/skate board 1/skate board/DRAW.fbx`.
- UI art + mockups: `Assets/Art/UI/` (per-screen folders + `0-mockups/`). Font: Teko.

## Project structure (new, under Assets/_Game/)
- `Scripts/Core` — GameManager, GameFlow/scene loading, SaveSystem, PlayerData, AudioManager
- `Scripts/Gameplay` — SkaterController, AIRacer, RaceManager, TrackPath, Booster, Obstacle, JumpZone
- `Scripts/Input` — InputService (touch + keyboard)
- `Scripts/UI` — screen controllers (Menu, HUD, Loading, CharacterSelect, BoardSelect, Pause, LevelComplete, Settings, Objective, Exit, AdPanel)
- `Scripts/Ads` — IAdService + StubAdService
- `Scripts/Data` — CharacterDef, BoardDef (ScriptableObjects)
- `Scenes` — Boot, MainMenu, Game
- `Prefabs`, `Animations`, `Materials`

## Milestones
1. [in progress] Foundation: folders, UI textures→sprites, scene skeletons, core singletons + save.
2. Core race: Game scene w/ track, SkaterController (steer/jump), CameraFollow, finish line, win/lose. Playable.
3. Opponents + race logic: AIRacer x2, positions/ranking, countdown, results.
4. Track interactions: boosters (pickup + UI ad-boost), jumpable gaps/lakes, broken-road obstacles, fail/respawn.
5. HUD: gameplay buttons (left/right/jump/double/booster/pause/skip) wired, responsive + safe area.
6. Front-end: Loading → Main Menu → flow; Settings, Exit, Objective panels.
7. Selection: Character select (3 girls) + Skateboard select, with lock/buy/coins + watch-ad-to-unlock (stub).
8. Persistence + economy: coins, unlocks, selected character/board, settings — saved.
9. Cutscene: drawing-room intro → hilltop → countdown (with skip/ad-skip).
10. Level Complete / fail screens, restart/home/next, star + coin rewards.
11. Audio hooks, polish, build settings for Android/iOS, safe-area QA.

## Conventions
- All gameplay tuning via serialized fields / ScriptableObjects.
- No hard-coded ad SDK; everything behind IAdService.
- Editor + device input both supported.
