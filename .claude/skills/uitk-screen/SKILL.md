---
name: uitk-screen
description: Build or edit a UI Toolkit panel/screen for the Mixtape skateboard game (main menu, character select, board select, loading, level complete, pause, settings, results, HUD, popups). Use whenever creating a new full-screen UITK panel from a mock, restyling one, wiring its buttons, or previewing/screenshotting a UITK screen. Covers the project's UITK foundation, USS/UXML conventions, the view-script pattern, scene wiring (new Input System), and the play-mode capture loop. Does NOT apply to the legacy uGUI Canvas UI.
---

# Building a UI Toolkit screen (Mixtape)

The whole game UI is being **rebuilt in UI Toolkit** inside a sandbox scene, replacing the
old uGUI Canvas UI. Build every new screen here; only delete the old uGUI UI once the
*entire* UITK UI is done. **Never edit the Canvas scenes/scripts while building** (MainMenu.unity,
MainMenuController.cs, ScreenManager, etc.).

The **Main Menu is the canonical reference** — copy its structure. Read these before starting:
- `Assets/_Game/UI/Screens/MainMenu.uxml` + `MainMenu.uss`
- `Assets/_Game/UI/Scripts/MainMenuView.cs`
- `Assets/_Game/UI/Styles/mixtape.uss` (tokens + reusable classes)

## Foundation (already exists — reuse, don't recreate)
- `Assets/_Game/UI/MixtapePanelSettings.asset` — `ScaleWithScreenSize`, reference **1920×1080**,
  `match = 1` (height-priority). All layout is authored in 1920×1080 reference space.
- `Assets/_Game/UI/Theme/Mixtape.tss` — runtime theme (default Unity theme).
- `Assets/_Game/UI/Styles/mixtape.uss` — `:root` tokens (`--accent`, `--text-light`, `--scrim`,
  `--font` = Teko-SemiBold) and reusable classes: `.sprite-btn`, `.ad-slot` / `.ad-slot__label`,
  `.scrim`, `.is-hidden`, `.bg-cover`.
- `Assets/_Game/UI/Art/halftone.png` — dotted print-texture overlay (generated).

## File layout for a new screen
```
Assets/_Game/UI/Screens/<Screen>.uxml      # structure
Assets/_Game/UI/Screens/<Screen>.uss       # screen-specific layout
Assets/_Game/UI/Scripts/<Screen>View.cs    # MonoBehaviour, namespace Mixtape.UITK
```

## Authoring rules (learned the hard way)
1. **USS `url()` must be RELATIVE** — `url("../../../Art/UI/<folder>/<file>.png")` from a
   `Screens/*.uss`. Project-absolute `url("/Assets/...")` **fails silently** (no warning, no image).
   URL-encode spaces: `setting.png` is fine, `rzte us.png` → `rzte%20us.png`,
   `ad  coins.png` (two spaces) → `ad%20%20coins.png`.
2. **Load styles in the UXML** via `<Style src="project://database/Assets/_Game/UI/Styles/mixtape.uss" />`
   then the screen's own `.uss`. (The `project://database/...` scheme works for `<Style>`; only
   `url()` needs relative paths.)
3. **Size sprite boxes by native aspect ratio.** First get pixel sizes (script below) and set
   width/height to that ratio, or use `-unity-background-scale-mode: scale-to-fit`. The menu art
   lives in `Assets/Art/UI/<screen>/` as pre-sliced PNGs (mocks in `Assets/Art/UI/0-mockups/`).
4. **Full-bleed background**: root stage `position:absolute; left/top/right/bottom:0;` with the bg
   image + `.bg-cover` (scale-and-crop). Position children `position:absolute` in 1920×1080 coords.
   Anchor edge elements with `right:`/`bottom:` so they survive narrower (4:3) aspects.
5. **Reserved ad slots**: every screen reserves a **top banner** strip AND a **left vertical rail**
   (`.ad-slot`, dark ~rgba(8,8,9,0.82) over the full-bleed art, labeled "AD BANNER" — the left one
   rotated `-90deg`). They're placeholders to be removed when the real AdMob SDK is integrated;
   nudge any left-edge content (e.g. EXIT) right to clear the ~110px rail. See the menu for the pattern.
6. **Buttons** use `.sprite-btn` (transparent, press-scale). Each button is usually a baked sprite
   (frame+text); just set its `background-image`.
7. **Dynamic text** (coin counts etc.): `-unity-font-definition: var(--font)`.
8. **Popups**: a `.scrim` overlay containing a panel; toggle with the `.is-hidden` class.
   **Linear color space gotcha:** the project renders linear, so a semi-transparent black
   scrim composites *much brighter* than its alpha implies (e.g. alpha 0.88 looks ~38% dim).
   Use **~0.95–0.96 alpha** for a properly dark popup backdrop (`--scrim` token already set).
9. Some panel art (e.g. `settings/Panel.png`) has the **title and labels baked in** — only
   overlay the interactive bits (toggles, save). Segmented toggles (ON/OFF) use the blank
   orange (`on.png`) / dark (`Off.png`) backgrounds + a text `Label`; the selected segment gets
   the orange bg via a class swap (`seg--sel` / `seg--unsel`).

Get sprite sizes before laying out (run via `script-execute`):
```csharp
foreach (var p in new[]{ "Assets/Art/UI/menui/Play.png" /* ... */ }) {
    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    Debug.Log(p + " = " + t.width + "x" + t.height + " ar " + (t.width/(float)t.height));
}
```

## View-script pattern
- MonoBehaviour in namespace **`Mixtape.UITK`**, `[RequireComponent(typeof(UIDocument))]`.
- In `OnEnable`: `var root = GetComponent<UIDocument>().rootVisualElement;` then `root.Q<Button>("id")`
  and wire `btn.clicked += handler`. **Null-guard everything** (`if (btn != null)`), and null-guard
  all singletons (`GameManager.Instance`, `AdManager`, `AudioManager`) so the screen runs standalone
  in the sandbox scene with no Boot/GameManager.
- Toggle visibility: `el.EnableInClassList("is-hidden", hidden)`. Toggle sprite state (e.g. on/off):
  swap modifier classes, e.g. `.menu-toggle--on` / `--off`, so no runtime Sprite refs are needed.
- Project APIs: coins/settings via `GameManager.Instance.Data` (`.coins`, `.soundOn`),
  `SaveData()`, `AddCoins(int)`; rewarded ads `AdManager.ShowRewarded("placement", ok => {...})`;
  external links via the `AppLinks` ScriptableObject (`Assets/_Game/Data/AppLinks.asset`);
  catalog via `GameDatabase`.
- **Navigation:** front-end is a single-scene panel system (`ScreenManager`), NOT scene loads.
  Only 3 build scenes exist (`MainMenu`, `Game`, `Loading`); `HANDOFF.md` is stale on this. A UITK
  screen router will replace ScreenManager once the panels are rebuilt — until then stub cross-screen
  nav with a `Debug.Log`.

## Wire a screen into the sandbox scene (`script-execute`)
```csharp
var go = new GameObject("<Screen>UI");
var doc = go.AddComponent<UIDocument>();
doc.panelSettings  = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/MixtapePanelSettings.asset");
doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Game/UI/Screens/<Screen>.uxml");
go.AddComponent(System.Type.GetType("Mixtape.UITK.<Screen>View, Assembly-CSharp"));
// EventSystem: project uses the NEW Input System — StandaloneInputModule THROWS every frame.
var es = new GameObject("EventSystem");
es.AddComponent<UnityEngine.EventSystems.EventSystem>();
es.AddComponent(System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem"));
```
Sandbox scene: `Assets/_Game/Scenes/UIToolkitTesting.unity`.

## Preview / screenshot loop
After writing files, `assets-refresh` with `ForceSynchronousImport` and check `console-get-logs`
for compile errors.

**Preferred — `screenshot-game-view` (true composite).** Available via the **Skill tool** /
`unity-mcp-cli`. This is the *accurate* render — use it for anything with transparency/dim
(popups, scrims); the `targetTexture` route can misrepresent semi-transparent layers. It returns
the PNG as **base64 inside JSON**, so run the CLI and decode it:
```powershell
npx unity-mcp-cli run-tool screenshot-game-view --input '{}' > "$env:TEMP\gv.txt" 2>&1
$raw = Get-Content "$env:TEMP\gv.txt" -Raw
$json = $raw.Substring($raw.IndexOf('SUCCESS: Response:') + 18).Trim() | ConvertFrom-Json
$img = $json.content | Where-Object { $_.type -eq 'image' } | Select-Object -First 1
[IO.File]::WriteAllBytes("$PWD\Assets\_gv.png", [Convert]::FromBase64String($img.data))  # then Read + delete it
```
Set the Game-view window to 16:9 (1920×1080 or 960×540) so it matches the reference. Enter/exit
play with `editor-application-set-state` (also a Skill-tool tool); it throws if there are compile
errors. To screenshot a *popup*, first `RemoveFromClassList("is-hidden")` on it at runtime via
`script-execute`.

**Fallback (guaranteed full-res 1920×1080)** when you need an exact-size frame regardless of the
Game-view window — drive it through `script-execute`:
1. Ensure the sandbox scene is open & active and clear any bootstrap override:
   `EditorSceneManager.playModeStartScene = null;` then `OpenScene(".../UIToolkitTesting.unity", Single)`.
   (A bootstrap/start-scene can hijack play mode and render the wrong scene — verify your root
   GameObject exists in `Application.isPlaying` before capturing.)
2. Enter play: `EditorApplication.isPlaying = true;` (separate call — triggers a domain reload).
3. **Call A:** set `panelSettings.targetTexture = new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32)` (`.Create()`).
4. **Call B** (a frame later): `RenderTexture.active = rt; tex.ReadPixels(...); File.WriteAllBytes("Assets/_tmp.png", tex.EncodeToPNG());`
   then `panelSettings.targetTexture = null; rt.Release();` and `AssetDatabase.Refresh()`. `Read` the PNG.
5. **Clean up**: `AssetDatabase.DeleteAsset` the temp PNG and `EditorApplication.isPlaying = false`.
   Never leave `_*.png` temp captures in the repo.

(`ScreenCapture.CaptureScreenshotAsTexture()` also works in play mode but only at the Game-view size;
the targetTexture route guarantees a full-res 1920×1080 frame.)

## Definition of done for a screen
Refreshes with no compile errors, renders faithfully vs its mock in `Assets/Art/UI/0-mockups/`,
buttons wired & null-guarded, popups toggle, edges survive narrow aspects, temp captures cleaned up,
Canvas UI untouched.
