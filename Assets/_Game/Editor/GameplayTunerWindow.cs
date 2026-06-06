using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Mixtape.Gameplay;

namespace Mixtape.EditorTools
{
    /// <summary>
    /// Runtime gameplay tuner (Window ▸ Mixtape ▸ Gameplay Tuner).
    ///
    /// Finds the live player <see cref="PhysicsSkater"/> + <see cref="CameraFollow"/> in the open
    /// scene and lets you slide speed / steering / jump / ground-feel / camera values **while you
    /// play**, so you can dial in the feel against real riding. Optionally mirrors every change to
    /// the AI skaters so opponents match the player.
    ///
    /// Persistence (per the design decision): values are AUTO-WRITTEN back to the components so they
    /// survive exiting Play. Unity normally discards play-mode edits, so on Play-exit the window
    /// snapshots the live values and re-applies them to the scene objects (with Undo + dirty), and
    /// there's a manual "Write to components now" button too. Heads-up: the skaters are prefab
    /// instances, so this records per-instance overrides on the scene objects — save the scene to keep them.
    /// </summary>
    public class GameplayTunerWindow : EditorWindow
    {
        private const string PrefKeyApplyToAI = "Mixtape.Tuner.ApplyToAI";
        private const string PrefKeySnapshot  = "Mixtape.Tuner.Snapshot";

        private PhysicsSkater _player;
        private PhysicsSkater[] _ais = new PhysicsSkater[0];
        private CameraFollow _cam;
        private PlayerSkaterInput _playerInput;

        private bool _applyToAI;
        private Vector2 _scroll;

        [MenuItem("Window/Mixtape/Gameplay Tuner")]
        public static void Open()
        {
            var w = GetWindow<GameplayTunerWindow>("Gameplay Tuner");
            w.minSize = new Vector2(320, 420);
        }

        private void OnEnable()
        {
            _applyToAI = EditorPrefs.GetBool(PrefKeyApplyToAI, true);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        // keep telemetry live while playing
        private void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        // ---------------------------------------------------------------- discovery
        private void Refresh()
        {
            var all = Object.FindObjectsByType<PhysicsSkater>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _player = null;
            var ai = new System.Collections.Generic.List<PhysicsSkater>();
            foreach (var s in all)
            {
                if (s.GetComponent<PlayerSkaterInput>() != null) { if (_player == null) _player = s; }
                else if (s.GetComponent<AISkater>() != null) ai.Add(s);
            }
            if (_player == null && all.Length > 0) _player = all[0];
            _ais = ai.ToArray();
            _playerInput = _player != null ? _player.GetComponent<PlayerSkaterInput>() : null;
            _cam = Object.FindFirstObjectByType<CameraFollow>();
        }

        // ---------------------------------------------------------------- GUI
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh targets", EditorStyles.toolbarButton)) Refresh();
                GUILayout.FlexibleSpace();
                _applyToAI = GUILayout.Toggle(_applyToAI, "Apply to AI", EditorStyles.toolbarButton);
                EditorPrefs.SetBool(PrefKeyApplyToAI, _applyToAI);
            }

            if (_player == null)
            {
                EditorGUILayout.HelpBox("No PhysicsSkater found in the open scene. Open Game.unity (or a scene with a skater) and hit Refresh.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawTelemetry();

            Section("Speed ramp (Subway-Surfers)");
            SkaterB("Use time ramp",  s => s.useTimeRamp, (s, v) => s.useTimeRamp = v);
            SkaterF("Start speed",    0, 40, s => s.startSpeed, (s, v) => s.startSpeed = v);
            SkaterF("Ramp time (s)",  1, 60, s => s.rampTime,   (s, v) => s.rampTime = v);
            SkaterF("Keep on hit",    0,  1, s => s.rampKeepOnHit, (s, v) => s.rampKeepOnHit = v);

            Section("Speed");
            SkaterF("Cruise (base)",  0, 60, s => s.baseSpeed,    (s, v) => s.baseSpeed = v);
            SkaterF("Max speed",      0, 80, s => s.maxSpeed,     (s, v) => s.maxSpeed = v);
            SkaterF("Accel",          0, 60, s => s.accel,        (s, v) => s.accel = v);
            SkaterF("Downhill gain",  0, 60, s => s.downhillGain, (s, v) => s.downhillGain = v);
            SkaterF("Uphill drag",    0, 40, s => s.uphillDrag,   (s, v) => s.uphillDrag = v);

            Section("Steering");
            SkaterF("Steer rate",      0, 300, s => s.steerRate,     (s, v) => s.steerRate = v);
            SkaterF("Steer speed ref", 1,  40, s => s.steerSpeedRef, (s, v) => s.steerSpeedRef = v);
            SkaterF("Air control",     0,   1, s => s.airControl,    (s, v) => s.airControl = v);

            Section("Jump (arc)");
            SkaterF("Jump speed", 0, 30, s => s.jumpSpeed, (s, v) => s.jumpSpeed = v);
            SkaterF("Gravity",    1, 60, s => s.gravity,   (s, v) => s.gravity = v);
            SkaterF("Land snap",  0,  1, s => s.landSnap,  (s, v) => s.landSnap = v);
            SkaterI("Max jumps",  1,  3, s => s.maxJumps,  (s, v) => s.maxJumps = v);
            EditorGUILayout.LabelField(JumpReadout(), EditorStyles.miniLabel);

            Section("Ground feel");
            SkaterF("Hover height", 0,  1, s => s.hoverHeight, (s, v) => s.hoverHeight = v);
            SkaterF("Ground probe", 0, 10, s => s.groundProbe, (s, v) => s.groundProbe = v);
            SkaterF("Align speed",  0, 30, s => s.alignSpeed,  (s, v) => s.alignSpeed = v);

            if (_playerInput != null)
            {
                Section("Boost");
                EditorGUI.BeginChangeCheck();
                float bm = EditorGUILayout.Slider("Boost multiplier", _playerInput.boostMultiplier, 1f, 3f);
                if (EditorGUI.EndChangeCheck()) { RecordObj(_playerInput); _playerInput.boostMultiplier = bm; DirtyObj(_playerInput); }
            }

            if (_cam != null)
            {
                Section("Camera");
                CamV3("Offset (x/y/z)", v => _cam.localOffset = v, _cam.localOffset);
                CamF("Position smooth", 0, 30, () => _cam.positionSmooth, v => _cam.positionSmooth = v);
                CamF("Rotation smooth", 0, 30, () => _cam.rotationSmooth, v => _cam.rotationSmooth = v);
                CamF("Look ahead",      0, 20, () => _cam.lookAhead,      v => _cam.lookAhead = v);
                CamF("Look height",     0,  5, () => _cam.lookHeight,     v => _cam.lookHeight = v);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Write to components now")) WriteToComponents();
                if (GUILayout.Button("Copy values to console")) Debug.Log(BuildSnapshot().ToString());
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- telemetry
        private void DrawTelemetry()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Not playing — edits write to the scene component (Undo + save to keep).", EditorStyles.miniLabel);
                    return;
                }
                EditorGUILayout.LabelField("Live telemetry (player)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Speed     {_player.Speed,6:0.0}   Vert {_player.VerticalSpeed,6:0.0}");
                EditorGUILayout.LabelField($"Grounded  {_player.IsGrounded,-6}  Jumps {_player.JumpsUsed}   Boost {_player.IsBoosting}");
                EditorGUILayout.LabelField($"Ramp      {_player.RampProgress * 100f,5:0}%");
            }
        }

        private string JumpReadout()
        {
            float g = Mathf.Max(0.01f, _player.gravity);
            float h = _player.jumpSpeed * _player.jumpSpeed / (2f * g);
            float air = 2f * _player.jumpSpeed / g;
            return $"≈ {h:0.0} units high, {air:0.00}s hang time per jump";
        }

        // ---------------------------------------------------------------- field helpers
        private static void Section(string title)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void SkaterF(string label, float min, float max, System.Func<PhysicsSkater, float> get, System.Action<PhysicsSkater, float> set)
        {
            EditorGUI.BeginChangeCheck();
            float nv = EditorGUILayout.Slider(label, get(_player), min, max);
            if (!EditorGUI.EndChangeCheck()) return;
            ApplyF(_player, set, nv);
            if (_applyToAI) foreach (var ai in _ais) ApplyF(ai, set, nv);
        }

        private void SkaterI(string label, int min, int max, System.Func<PhysicsSkater, int> get, System.Action<PhysicsSkater, int> set)
        {
            EditorGUI.BeginChangeCheck();
            int nv = EditorGUILayout.IntSlider(label, get(_player), min, max);
            if (!EditorGUI.EndChangeCheck()) return;
            ApplyI(_player, set, nv);
            if (_applyToAI) foreach (var ai in _ais) ApplyI(ai, set, nv);
        }

        private void SkaterB(string label, System.Func<PhysicsSkater, bool> get, System.Action<PhysicsSkater, bool> set)
        {
            EditorGUI.BeginChangeCheck();
            bool nv = EditorGUILayout.Toggle(label, get(_player));
            if (!EditorGUI.EndChangeCheck()) return;
            if (_player != null) { RecordObj(_player); set(_player, nv); DirtyObj(_player); }
            if (_applyToAI) foreach (var ai in _ais) { if (ai == null) continue; RecordObj(ai); set(ai, nv); DirtyObj(ai); }
        }

        private void CamF(string label, float min, float max, System.Func<float> get, System.Action<float> set)
        {
            EditorGUI.BeginChangeCheck();
            float nv = EditorGUILayout.Slider(label, get(), min, max);
            if (!EditorGUI.EndChangeCheck()) return;
            RecordObj(_cam); set(nv); DirtyObj(_cam);
        }

        private void CamV3(string label, System.Action<Vector3> set, Vector3 cur)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 nv = EditorGUILayout.Vector3Field(label, cur);
            if (!EditorGUI.EndChangeCheck()) return;
            RecordObj(_cam); set(nv); DirtyObj(_cam);
        }

        private void ApplyF(PhysicsSkater s, System.Action<PhysicsSkater, float> set, float v)
        {
            if (s == null) return;
            RecordObj(s); set(s, v); DirtyObj(s);
        }

        private void ApplyI(PhysicsSkater s, System.Action<PhysicsSkater, int> set, int v)
        {
            if (s == null) return;
            RecordObj(s); set(s, v); DirtyObj(s);
        }

        private static void RecordObj(Object o)
        {
            if (o != null && !Application.isPlaying) Undo.RecordObject(o, "Gameplay Tuner");
        }

        private static void DirtyObj(Object o)
        {
            if (o == null || Application.isPlaying) return;
            EditorUtility.SetDirty(o);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // ---------------------------------------------------------------- persistence
        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                // capture the live values before Unity reverts them
                if (_player != null)
                    EditorPrefs.SetString(PrefKeySnapshot, EditorJsonUtility.ToJson(BuildSnapshot()));
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                Refresh();
                var json = EditorPrefs.GetString(PrefKeySnapshot, "");
                if (!string.IsNullOrEmpty(json) && _player != null)
                {
                    var snap = new Snapshot();
                    EditorJsonUtility.FromJsonOverwrite(json, snap);
                    ApplySnapshot(snap);
                    Debug.Log("[Gameplay Tuner] Re-applied tuned values to the scene. Save the scene to keep them.");
                }
            }
        }

        private void WriteToComponents()
        {
            // when not playing, edits are already on the component; just force a save-able dirty.
            // when playing, snapshot now so the values stick on exit even without further edits.
            if (Application.isPlaying)
                EditorPrefs.SetString(PrefKeySnapshot, EditorJsonUtility.ToJson(BuildSnapshot()));
            else
            {
                DirtyObj(_player);
                foreach (var ai in _ais) DirtyObj(ai);
                DirtyObj(_cam); DirtyObj(_playerInput);
            }
        }

        private Snapshot BuildSnapshot()
        {
            var s = new Snapshot();
            var p = _player;
            s.useTimeRamp = p.useTimeRamp; s.startSpeed = p.startSpeed; s.rampTime = p.rampTime; s.rampKeepOnHit = p.rampKeepOnHit;
            s.baseSpeed = p.baseSpeed; s.maxSpeed = p.maxSpeed; s.accel = p.accel;
            s.downhillGain = p.downhillGain; s.uphillDrag = p.uphillDrag;
            s.steerRate = p.steerRate; s.steerSpeedRef = p.steerSpeedRef; s.airControl = p.airControl;
            s.jumpSpeed = p.jumpSpeed; s.gravity = p.gravity; s.landSnap = p.landSnap; s.maxJumps = p.maxJumps;
            s.hoverHeight = p.hoverHeight; s.groundProbe = p.groundProbe; s.alignSpeed = p.alignSpeed;
            if (_playerInput != null) s.boostMultiplier = _playerInput.boostMultiplier;
            if (_cam != null)
            {
                s.camOffset = _cam.localOffset; s.posSmooth = _cam.positionSmooth; s.rotSmooth = _cam.rotationSmooth;
                s.lookAhead = _cam.lookAhead; s.lookHeight = _cam.lookHeight; s.hasCam = true;
            }
            s.applyToAI = _applyToAI;
            return s;
        }

        private void ApplySnapshot(Snapshot s)
        {
            void ToSkater(PhysicsSkater p)
            {
                if (p == null) return;
                RecordObj(p);
                p.useTimeRamp = s.useTimeRamp; p.startSpeed = s.startSpeed; p.rampTime = s.rampTime; p.rampKeepOnHit = s.rampKeepOnHit;
                p.baseSpeed = s.baseSpeed; p.maxSpeed = s.maxSpeed; p.accel = s.accel;
                p.downhillGain = s.downhillGain; p.uphillDrag = s.uphillDrag;
                p.steerRate = s.steerRate; p.steerSpeedRef = s.steerSpeedRef; p.airControl = s.airControl;
                p.jumpSpeed = s.jumpSpeed; p.gravity = s.gravity; p.landSnap = s.landSnap; p.maxJumps = s.maxJumps;
                p.hoverHeight = s.hoverHeight; p.groundProbe = s.groundProbe; p.alignSpeed = s.alignSpeed;
                DirtyObj(p);
            }

            ToSkater(_player);
            if (s.applyToAI) foreach (var ai in _ais) ToSkater(ai);

            if (_playerInput != null) { RecordObj(_playerInput); _playerInput.boostMultiplier = s.boostMultiplier; DirtyObj(_playerInput); }
            if (s.hasCam && _cam != null)
            {
                RecordObj(_cam);
                _cam.localOffset = s.camOffset; _cam.positionSmooth = s.posSmooth; _cam.rotationSmooth = s.rotSmooth;
                _cam.lookAhead = s.lookAhead; _cam.lookHeight = s.lookHeight;
                DirtyObj(_cam);
            }
        }

        [System.Serializable]
        private class Snapshot
        {
            public bool useTimeRamp = true;
            public float startSpeed = 12f, rampTime = 18f, rampKeepOnHit = 0.6f;
            public float baseSpeed, maxSpeed, accel, downhillGain, uphillDrag;
            public float steerRate, steerSpeedRef, airControl;
            public float jumpSpeed, gravity, landSnap; public int maxJumps;
            public float hoverHeight, groundProbe, alignSpeed;
            public float boostMultiplier = 1.5f;
            public Vector3 camOffset; public float posSmooth, rotSmooth, lookAhead, lookHeight;
            public bool hasCam, applyToAI;

            public override string ToString() =>
                $"[Gameplay Tuner] ramp(on={useTimeRamp} start={startSpeed} time={rampTime} keepOnHit={rampKeepOnHit}) | " +
                $"cruise={baseSpeed} max={maxSpeed} accel={accel} downhill={downhillGain} uphill={uphillDrag} | " +
                $"steerRate={steerRate} steerRef={steerSpeedRef} air={airControl} | " +
                $"jump={jumpSpeed} grav={gravity} land={landSnap} maxJumps={maxJumps} | " +
                $"hover={hoverHeight} probe={groundProbe} align={alignSpeed} | boost={boostMultiplier} | " +
                $"camOffset={camOffset} posSmooth={posSmooth} rotSmooth={rotSmooth} lookAhead={lookAhead} lookHeight={lookHeight}";
        }
    }
}
