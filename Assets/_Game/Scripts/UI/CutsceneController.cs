using UnityEngine;
using UnityEngine.UI;
using Mixtape.Core;
using Mixtape.Ads;

namespace Mixtape.UI
{
    /// <summary>
    /// Opening story cutscene: shows the three friends in the drawing room planning their
    /// final race, advancing through dialogue lines. Tap advances; Skip / watch-ad-skip
    /// jump straight to the race.
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        [Header("Refs (set by builder)")]
        public Text lineText;
        public CanvasGroup fader;

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

        private void Start()
        {
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
            SceneFlow.LoadVia(SceneFlow.Game);
        }
    }
}
