using UnityEngine;
using UnityEngine.SceneManagement;
using Mixtape.Data;

namespace Mixtape.Core
{
    /// <summary>
    /// Persistent root object. Owns player data, the game database, the current race
    /// selection, and scene navigation. Survives scene loads as a singleton.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private GameDatabase database;

        [Header("Scene names (must be in Build Settings)")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameScene = "Game";

        public GameDatabase Database => database;
        public PlayerData Data { get; private set; }

        public string MainMenuScene => mainMenuScene;
        public string GameScene => gameScene;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Mobile platforms default to 30 FPS; the speed feel needs 60.
            Application.targetFrameRate = 60;

            Data = SaveSystem.Load();
        }

        public void SaveData() => SaveSystem.Save(Data);

        // ---- Economy ----
        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            Data.coins = Mathf.Max(0, Data.coins + amount);
            SaveData();
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return true;
            if (Data.coins < amount) return false;
            Data.coins -= amount;
            SaveData();
            return true;
        }

        // ---- Selection ----
        public CharacterDef SelectedCharacter => database != null ? database.GetCharacter(Data.selectedCharacter) : null;
        public BoardDef SelectedBoard => database != null ? database.GetBoard(Data.selectedBoard) : null;

        public void SelectCharacter(int index) { Data.selectedCharacter = index; SaveData(); }
        public void SelectBoard(int index) { Data.selectedBoard = index; SaveData(); }

        // ---- Level selection ----
        // 0 = Level 1, 1 = Level 2, ... Kept in memory (survives scene loads via the singleton);
        // set this before loading the Game scene to choose which level plays.
        public int SelectedLevel { get; private set; } = 0;
        public void SelectLevel(int index) => SelectedLevel = Mathf.Max(0, index);

        // ---- Navigation ----
        // All routes go through the Loading screen: heavy scene loads (Game especially)
        // used to be direct LoadScene calls = a visible freeze with no feedback.
        public void LoadMainMenu() => SceneFlow.LoadVia(mainMenuScene);
        public void LoadGame() => SceneFlow.LoadVia(gameScene);
        public void ReloadCurrent() => SceneFlow.LoadVia(SceneManager.GetActiveScene().name);
    }
}
