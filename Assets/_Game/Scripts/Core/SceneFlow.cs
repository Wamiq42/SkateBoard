using UnityEngine.SceneManagement;

namespace Mixtape.Core
{
    /// <summary>Central scene-name constants and the loading hand-off target.</summary>
    public static class SceneFlow
    {
        // Front-end (MainMenu) and pre-race screens are now panels inside MainMenu,
        // and the opening cutscene plays inside the Game scene. Only three real
        // scenes remain in the build.
        public const string MainMenu = "MainMenu";
        public const string Game = "Game";
        public const string Loading = "Loading";

        /// <summary>Scene the Loading screen should load next.</summary>
        public static string Target = Game;

        public static void LoadDirect(string scene) => SceneManager.LoadScene(scene);

        /// <summary>Go through the Loading screen to reach <paramref name="scene"/>.</summary>
        public static void LoadVia(string scene)
        {
            Target = scene;
            SceneManager.LoadScene(Loading);
        }
    }
}
