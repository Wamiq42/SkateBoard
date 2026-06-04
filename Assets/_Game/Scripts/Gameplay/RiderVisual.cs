using UnityEngine;
using Mixtape.Core;
using Mixtape.Data;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Spawns the rider's character + board models under a mount point. The player uses
    /// the live selection from <see cref="GameManager"/>; AI (or previews) use forced
    /// defs. Exposes the spawned Animator so <see cref="SkaterController"/> can drive it.
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

        [Tooltip("Lift the character so feet rest on the board deck.")]
        public float riderYOffset = 0.22f;
        [Tooltip("Extra yaw if the model doesn't face +Z (forward).")]
        public float modelYaw = 0f;

        public Animator SpawnedAnimator { get; private set; }

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

            if (bd != null && bd.modelPrefab != null)
            {
                var b = Instantiate(bd.modelPrefab, mount);
                b.name = "Board";
                b.transform.localPosition = Vector3.zero;
                b.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
            }

            if (ch != null && ch.modelPrefab != null)
            {
                var g = Instantiate(ch.modelPrefab, mount);
                g.name = "Character";
                g.transform.localPosition = Vector3.up * riderYOffset;
                g.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
                SpawnedAnimator = g.GetComponentInChildren<Animator>();
            }
        }
    }
}
