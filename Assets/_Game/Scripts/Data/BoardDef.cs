using UnityEngine;

namespace Mixtape.Data
{
    /// <summary>Definition for one skateboard.</summary>
    [CreateAssetMenu(fileName = "BoardDef", menuName = "Mixtape/Board", order = 1)]
    public class BoardDef : ScriptableObject
    {
        public string displayName = "Board";
        [Tooltip("Skateboard prefab/model placed under the rider's feet.")]
        public GameObject modelPrefab;
        [Tooltip("Preview shown on the skateboard-select screen.")]
        public Sprite preview;
        [Tooltip("Coin price to unlock. 0 = free/unlocked by default.")]
        public int price = 0;
        public bool unlockableByAd = true;

        [Header("Tuning (optional per-board feel)")]
        [Tooltip("Multiplier applied to base forward speed.")]
        public float speedMultiplier = 1f;
    }
}
