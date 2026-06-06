using UnityEngine;
using Mixtape.Core;
using Mixtape.Data;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Spawns the rider's character + board models under a mount point and wires up their
    /// animators. The player uses the live selection from <see cref="GameManager"/>; AI (or
    /// previews) use forced defs. Exposes the spawned animators + wheel spinner so
    /// <see cref="SkaterController"/> can drive them. Controllers are loaded from Resources
    /// so every spawned rider is wired automatically (works for all characters).
    /// </summary>
    public class RiderVisual : MonoBehaviour
    {
        [Tooltip("Where models are parented. Defaults to this transform.")]
        public Transform mount;

        [Tooltip("Player = true reads GameManager selection; AI/preview = false uses forced defs.")]
        public bool useSelected = true;

        public CharacterDef forcedCharacter;
        public BoardDef forcedBoard;

        [Tooltip("Fallbacks when no selection / forced def is available.")]
        public CharacterDef defaultCharacter;
        public BoardDef defaultBoard;

        [Tooltip("Lift the character so the boots rest on the board deck.")]
        public float deckYOffset = 0.189f;

        [Tooltip("Extra yaw if the models don't face +Z (forward). New clips face +Z, so 0.")]
        public float faceYaw = 0f;

        [Tooltip("Uniform scale applied to the board model.")]
        public float boardScale = 0.79f;

        [Tooltip("Resources paths to the gameplay animator controllers.")]
        public string charControllerResource = "SkaterChar";
        public string boardControllerResource = "SkaterBoard";

        public Animator SpawnedAnimator { get; private set; }
        public Animator SpawnedBoardAnimator { get; private set; }
        public WheelSpinner SpawnedWheels { get; private set; }

        private void Start()
        {
            if (Application.isPlaying) Build();
        }

        public void Build()
        {
            if (mount == null) mount = transform;

            // Clear existing children.
            for (int i = mount.childCount - 1; i >= 0; i--)
            {
                var child = mount.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            CharacterDef ch = forcedCharacter;
            BoardDef bd = forcedBoard;
            if (useSelected && GameManager.Instance != null)
            {
                ch = GameManager.Instance.SelectedCharacter;
                bd = GameManager.Instance.SelectedBoard;
            }
            if (ch == null) ch = defaultCharacter;
            if (bd == null) bd = defaultBoard;

            // ---- Board: parented under a rig so the board clips can animate the "Board" child
            //      (localPosition in the unscaled rig stays in world units → tracks the feet). ----
            if (bd != null && bd.modelPrefab != null)
            {
                var rig = new GameObject("BoardRig");
                rig.transform.SetParent(mount);
                rig.transform.localPosition = Vector3.zero;
                rig.transform.localRotation = Quaternion.Euler(0f, faceYaw, 0f);
                rig.transform.localScale = Vector3.one;

                var b = Instantiate(bd.modelPrefab, rig.transform);
                b.name = "Board";
                b.transform.localPosition = Vector3.zero;
                b.transform.localRotation = Quaternion.identity;
                b.transform.localScale = Vector3.one * boardScale;

                SpawnedWheels = rig.AddComponent<WheelSpinner>();

                var ba = rig.AddComponent<Animator>();
                ba.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(boardControllerResource);
                ba.applyRootMotion = false;
                SpawnedBoardAnimator = ba;
            }

            // ---- Character ----
            if (ch != null && ch.modelPrefab != null)
            {
                var g = Instantiate(ch.modelPrefab, mount);
                g.name = "Character";
                g.transform.localPosition = Vector3.up * deckYOffset;
                g.transform.localRotation = Quaternion.Euler(0f, faceYaw, 0f);

                var a = g.GetComponentInChildren<Animator>();
                if (a == null) a = g.AddComponent<Animator>();
                a.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(charControllerResource);
                a.applyRootMotion = false;
                SpawnedAnimator = a;
            }

            // Drive the animators from the rider's PhysicsSkater (player + AI alike).
            if (Application.isPlaying && GetComponent<PhysicsSkater>() != null
                && GetComponent<RiderAnimDriver>() == null)
                gameObject.AddComponent<RiderAnimDriver>();
        }
    }
}
