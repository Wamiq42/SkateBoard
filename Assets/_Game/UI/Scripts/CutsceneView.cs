using System;
using UnityEngine;
using UnityEngine.UIElements;
using Mixtape.Ads;

namespace Mixtape.UITK
{
    /// <summary>
    /// Opening cutscene dialogue overlay (UI Toolkit). Mirrors the old uGUI
    /// CutsceneController's UI side: advancing dialogue lines, tap-to-advance, free
    /// SKIP, and watch-ad skip. The game-logic hand-off (swap cameras / hide the
    /// intro stage / StartRace) is NOT done here — subscribe to <see cref="onFinish"/>
    /// from the Game scene so this stays a pure overlay and runs in the sandbox.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CutsceneView : MonoBehaviour
    {
        [TextArea] public string[] lines =
        {
            "Tomorrow, we turn eighteen.",
            "One last ride before we grow up...",
            "From the top of the hill, all the way down.",
            "First one to the bottom wins. Ready?"
        };
        public float lineDuration = 3.5f;

        /// <summary>Raised once when the cutscene ends (last line, SKIP, or watch-ad skip).</summary>
        public Action onFinish;

        private Label _line;
        private VisualElement _fader;
        private int _idx;
        private float _timer;
        private bool _finished;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _line  = root.Q<Label>("cut-line");
            _fader = root.Q<VisualElement>("cut-fader");

            if (_fader != null) _fader.RegisterCallback<ClickEvent>(_ => Advance()); // tap empty area
            Bind(root, "cut-skip", WatchAdSkip); // single skip button = watch-ad to skip

            _idx = 0; _timer = 0f; _finished = false;
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
            if (_line != null && i >= 0 && i < lines.Length) _line.text = lines[i];
        }

        public void Skip() => Finish();
        public void WatchAdSkip() => AdManager.ShowInterstitial("skip_cutscene", Finish);

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            Debug.Log("[Cutscene] finished");
            onFinish?.Invoke();
        }

        private static void Bind(VisualElement root, string name, Action cb)
        {
            var b = root.Q<Button>(name);
            if (b != null) b.clicked += cb;
        }
    }
}
