using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mixtape.Core
{
    /// <summary>
    /// All persisted player state: economy, unlocks, current selections, settings.
    /// Plain serializable object so it round-trips cleanly through JsonUtility.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public int coins = 0;

        // Selected indices into the GameDatabase character / board arrays.
        public int selectedCharacter = 0;
        public int selectedBoard = 0;

        // Unlock state. Index 0 of each is always unlocked by default.
        public List<int> unlockedCharacters = new List<int>();
        public List<int> unlockedBoards = new List<int>();

        // Settings.
        public bool soundOn = true;
        public bool musicOn = true;

        public static PlayerData CreateDefault()
        {
            var d = new PlayerData
            {
                coins = 0,
                selectedCharacter = 0,
                selectedBoard = 0,
                soundOn = true,
                musicOn = true,
            };
            d.unlockedCharacters = new List<int> { 0 };
            d.unlockedBoards = new List<int> { 0 };
            return d;
        }

        /// <summary>Guards against malformed/old saves.</summary>
        public PlayerData Validated()
        {
            unlockedCharacters ??= new List<int>();
            unlockedBoards ??= new List<int>();
            if (!unlockedCharacters.Contains(0)) unlockedCharacters.Add(0);
            if (!unlockedBoards.Contains(0)) unlockedBoards.Add(0);
            if (coins < 0) coins = 0;
            return this;
        }

        public bool IsCharacterUnlocked(int index) => unlockedCharacters != null && unlockedCharacters.Contains(index);
        public bool IsBoardUnlocked(int index) => unlockedBoards != null && unlockedBoards.Contains(index);

        public void UnlockCharacter(int index)
        {
            if (unlockedCharacters == null) unlockedCharacters = new List<int>();
            if (!unlockedCharacters.Contains(index)) unlockedCharacters.Add(index);
        }

        public void UnlockBoard(int index)
        {
            if (unlockedBoards == null) unlockedBoards = new List<int>();
            if (!unlockedBoards.Contains(index)) unlockedBoards.Add(index);
        }
    }
}
