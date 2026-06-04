using UnityEngine;

namespace Mixtape.Data
{
    /// <summary>
    /// Central catalog of all characters and boards. Referenced by selection screens
    /// and gameplay so indices in <see cref="Mixtape.Core.PlayerData"/> stay meaningful.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Mixtape/Game Database", order = -10)]
    public class GameDatabase : ScriptableObject
    {
        public CharacterDef[] characters = new CharacterDef[0];
        public BoardDef[] boards = new BoardDef[0];

        public int CharacterCount => characters?.Length ?? 0;
        public int BoardCount => boards?.Length ?? 0;

        public CharacterDef GetCharacter(int index)
        {
            if (characters == null || characters.Length == 0) return null;
            return characters[Mathf.Clamp(index, 0, characters.Length - 1)];
        }

        public BoardDef GetBoard(int index)
        {
            if (boards == null || boards.Length == 0) return null;
            return boards[Mathf.Clamp(index, 0, boards.Length - 1)];
        }
    }
}
