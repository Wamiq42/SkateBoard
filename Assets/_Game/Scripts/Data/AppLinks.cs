using UnityEngine;

namespace Mixtape.Data
{
    /// <summary>
    /// External store / legal links surfaced by menu buttons (Rate Us, More Games,
    /// Privacy Policy, …). Kept in one asset so URLs can be edited without touching
    /// any scene or script. Assign the asset to the menu view.
    /// </summary>
    [CreateAssetMenu(fileName = "AppLinks", menuName = "Mixtape/App Links", order = -9)]
    public class AppLinks : ScriptableObject
    {
        [Tooltip("Store page opened by the Rate Us button.")]
        public string rateUrl = "https://play.google.com/store";

        [Tooltip("Developer / more-games page.")]
        public string moreGamesUrl = "https://play.google.com/store";

        [Tooltip("Privacy policy page.")]
        public string privacyUrl = "https://example.com/privacy";
    }
}
