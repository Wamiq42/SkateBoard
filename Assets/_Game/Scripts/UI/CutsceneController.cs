using UnityEngine;
using UnityEngine.UI;
using Mixtape.Core;
using Mixtape.Ads;
using Mixtape.Gameplay;

namespace Mixtape.UI
{
    /// <summary>
    /// Opening intro, now staged inside the Game scene at the Home apartment: the three
    /// friends plan their final race while dialogue lines advance. On finish it hands off
    /// to the race (swaps the intro camera for the follow camera and starts the countdown)
    /// instead of loading a separate scene. Tap advances; Skip / watch-ad-skip jump
    /// straight to the countdown.
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        [Header("Refs (set by builder)")]
        public Text lineText;
        public CanvasGroup fader;

        [Header("Race hand-off (Game scene)")]
        [Tooltip("Cutscene camera framing the apartment; disabled on finish.")]
        public GameObject introCamera;
        [Tooltip("The gameplay follow camera; enabled on finish.")]
        public GameObject raceCamera;
        [Tooltip("The posed friends / set-dressing placed in the apartment for the intro; hidden on finish.")]
        public GameObject introStage;

        [TextArea] public string[] lines =
        {
            "Tomorrow, we turn eighteen.",
            "One last ride before we grow up...",
            "From the top of the hill, all the way down.",
            "First one to the bottom wins. Ready?"
        };
        public float lineDuration = 3.5f;

        private int _idx;
        private float _timer;
        private bool _finished;

        private void OnEnable()
        {
            _idx = 0;
            _timer = 0f;
            _finished = false;
            ShowLine(0);
        }

        private void Update()
        {
            if (_finished) return;
            _timer += Time.deltaTime;
            if (_timer >= lineDuration) Advance();
        }

        public void Advance()
        {
            if (_finished) return;
            _timer = 0f;
            _idx++;
            if (_idx >= lines.Length) { Finish(); return; }
            ShowLine(_idx);
        }

        private void ShowLine(int i)
        {
            if (lineText != null && i >= 0 && i < lines.Length) lineText.text = lines[i];
        }

        public void Skip() => Finish();

        public void WatchAdSkip() => AdManager.ShowInterstitial("skip_cutscene", Finish);

        private void Finish()
        {
            if (_finished) return;
            _finished = true;

            // Swap the intro framing for gameplay and kick off the countdown.
            if (introStage) introStage.SetActive(false);
            if (introCamera) introCamera.SetActive(false);
            if (raceCamera) raceCamera.SetActive(true);

            RaceManager.Instance?.StartRace();
            gameObject.SetActive(false); // hide the dialogue overlay
        }
    }
}
