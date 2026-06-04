using UnityEngine;

namespace Mixtape.Core
{
    /// <summary>
    /// Lives in the Boot scene alongside the persistent systems (GameManager, AudioManager).
    /// Lets them initialise for one frame, then moves to the Main Menu.
    /// </summary>
    public class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private string firstScene = SceneFlow.MainMenu;

        private void Start() => SceneFlow.LoadDirect(firstScene);
    }
}
