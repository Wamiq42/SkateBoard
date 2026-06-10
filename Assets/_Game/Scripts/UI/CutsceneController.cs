using UnityEngine;
using Mixtape.Gameplay;
using Mixtape.UITK;

namespace Mixtape.UI
{
    /// <summary>
    /// Opening-intro hand-off (Game scene). The dialogue + skip UI now lives in the UI
    /// Toolkit <see cref="CutsceneView"/>; this component just listens for its
    /// <see cref="CutsceneView.onFinish"/> and swaps the intro framing for gameplay:
    /// disable the intro stage + camera, enable the race camera, and start the countdown.
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        [Header("UITK cutscene overlay")]
        [Tooltip("The UI Toolkit overlay that plays the dialogue + skip.")]
        public CutsceneView view;

        [Header("Race hand-off (Game scene)")]
        [Tooltip("Cutscene camera framing the apartment; disabled on finish.")]
        public GameObject introCamera;
        [Tooltip("The gameplay follow camera; enabled on finish.")]
        public GameObject raceCamera;
        [Tooltip("The posed friends / set-dressing placed in the apartment for the intro; hidden on finish.")]
        public GameObject introStage;

        private bool _finished;

        private void OnEnable()
        {
            _finished = false;
            if (view != null) view.onFinish += Finish;
        }

        private void OnDisable()
        {
            if (view != null) view.onFinish -= Finish;
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;

            // Swap the intro framing for gameplay and kick off the countdown.
            if (introStage) introStage.SetActive(false);
            if (introCamera) introCamera.SetActive(false);
            if (raceCamera) raceCamera.SetActive(true);

            RaceManager.Instance?.StartRace();
            if (view != null) view.gameObject.SetActive(false); // hide the dialogue overlay
        }
    }
}
