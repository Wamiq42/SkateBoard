using UnityEngine;

namespace Mixtape.Data
{
    /// <summary>Definition for one playable girl character.</summary>
    [CreateAssetMenu(fileName = "CharacterDef", menuName = "Mixtape/Character", order = 0)]
    public class CharacterDef : ScriptableObject
    {
        public string displayName = "Girl";
        [Tooltip("Rigged character prefab/model used in gameplay.")]
        public GameObject modelPrefab;
        [Tooltip("Portrait shown on the character-select screen.")]
        public Sprite portrait;
        [Tooltip("Coin price to unlock. 0 = free/unlocked by default.")]
        public int price = 0;
        [Tooltip("If true, can also be unlocked by watching a rewarded ad.")]
        public bool unlockableByAd = true;

        [Header("Display stats (0-100, shown as bars on Character Select)")]
        [Range(0, 100)] public int accuracy = 85;
        [Range(0, 100)] public int stamina = 65;
        [Range(0, 100)] public int health = 28;
        [Range(0, 100)] public int speed = 50;
    }
}
