using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Mixtape.Core;
using Mixtape.Data;
using Mixtape.Ads;

namespace Mixtape.UITK
{
    /// <summary>
    /// Board Select (UI Toolkit). A horizontal drag-scroller of cards, each showing a
    /// LIVE rotating 3D board (its own camera + RenderTexture). Free drag/swipe via a
    /// ScrollView; arrows nudge one card; a slider tracks position. Prices/owned + select/
    /// buy/watch-ad wired to the catalog + player data. Standalone via <see cref="database"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BoardSelectView : MonoBehaviour
    {
        [Tooltip("Catalog. Falls back to GameManager.Database when null.")]
        public GameDatabase database;
        [Tooltip("Base rotation that stands a board vertical with its deck toward the camera.")]
        public Vector3 boardEuler = new Vector3(-90f, 0f, 0f);
        public float spinSpeed = 45f;

        /// <summary>Nav hooks assigned by <see cref="UIRouter"/>.</summary>
        public Action onNext, onBack;

        private class Card
        {
            public VisualElement root, img, coin;
            public Label lbl;
            public Transform mount;
            public Camera cam;
            public RenderTexture rt;
            public GameObject model;
        }

        private readonly List<Card> _cards = new List<Card>();
        private ScrollView _scroll;
        private VisualElement _sliderFill;
        private Button _btnSelect, _btnBuy, _btnWatch;
        private Label _buyPrice;
        private GameObject _rig;
        private int _focus;

        // drag-to-scroll (mobile-style; works for mouse + touch)
        private bool _dragging;
        private float _downX, _lastX;
        private int _pid;

        private GameDatabase DB =>
            database != null ? database :
            (GameManager.Instance != null ? GameManager.Instance.Database : null);
        private int Count => DB != null ? DB.BoardCount : 0;

        private const float CardStride = 290f + 56f; // width + margin

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _scroll = root.Q<ScrollView>("bs-scroll");
            _sliderFill = root.Q<VisualElement>("bs-slider-fill");
            if (_scroll != null)
            {
                _scroll.mode = ScrollViewMode.Horizontal;
                _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                // drive scrolling from pointer drag directly (UITK ScrollView only
                // drag-scrolls on real touch otherwise; this makes mouse + touch work).
                _scroll.RegisterCallback<PointerDownEvent>(OnPtrDown);
                _scroll.RegisterCallback<PointerMoveEvent>(OnPtrMove);
                _scroll.RegisterCallback<PointerUpEvent>(OnPtrUp);
            }

            _btnSelect = root.Q<Button>("bs-select");
            _btnBuy = root.Q<Button>("bs-buy");
            _btnWatch = root.Q<Button>("bs-watchad");
            _buyPrice = root.Q<Label>("bs-buy-price");

            Bind(root, "bs-prev",       () => Nudge(-1));
            Bind(root, "bs-next-arrow", () => Nudge(1));
            Bind(root, "bs-select",     Select);
            Bind(root, "bs-buy",        Buy);
            Bind(root, "bs-watchad",    WatchAd);
            Bind(root, "bs-next",       Proceed);
            Bind(root, "bs-back",       () => { if (onBack != null) onBack(); else Debug.Log("[BoardSelect] BACK (no router)"); });

            _rig = new GameObject("BoardPreviewRig");
            _rig.transform.position = new Vector3(12000f, 0f, 0f);
            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_rig.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

            _focus = GameManager.Instance != null ? GameManager.Instance.Data.selectedBoard : 0;
            BuildCards();
        }

        private void OnDisable()
        {
            foreach (var c in _cards)
            {
                if (c.model != null) Destroy(c.model);
                if (c.cam != null) c.cam.targetTexture = null;   // detach before release (console error otherwise)
                if (c.rt != null) { c.rt.Release(); Destroy(c.rt); }
            }
            _cards.Clear();
            if (_rig != null) Destroy(_rig);
            _rig = null;
        }

        private void Update()
        {
            foreach (var c in _cards)
                if (c.model != null) c.model.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            SyncSlider();
        }

        // ---- build one card + live preview stage per board ----
        private void BuildCards()
        {
            if (_scroll == null || DB == null) return;
            for (int i = 0; i < Count; i++)
            {
                var def = DB.GetBoard(i);
                var c = new Card();

                c.root = new VisualElement(); c.root.AddToClassList("bs-card");
                c.img = new VisualElement(); c.img.AddToClassList("bs-card-img"); c.root.Add(c.img);
                var label = new VisualElement(); label.AddToClassList("bs-card-label");
                c.coin = new VisualElement(); c.coin.AddToClassList("bs-card-coin"); label.Add(c.coin);
                c.lbl = new Label(); c.lbl.AddToClassList("bs-card-price"); label.Add(c.lbl);
                c.root.Add(label);
                int bi = i;
                c.root.RegisterCallback<ClickEvent>(_ => { _focus = bi; RefreshLabels(); });
                _scroll.Add(c.root);

                // live preview stage
                var stage = new GameObject("Stage" + i).transform;
                stage.SetParent(_rig.transform, false);
                stage.localPosition = new Vector3(i * 80f, 0f, 0f);
                c.mount = new GameObject("Mount").transform;
                c.mount.SetParent(stage, false);
                var camGo = new GameObject("Cam"); camGo.transform.SetParent(stage, false);
                c.cam = camGo.AddComponent<Camera>();
                c.cam.orthographic = true;
                c.cam.clearFlags = CameraClearFlags.SolidColor;
                c.cam.backgroundColor = new Color(0, 0, 0, 0);
                c.cam.nearClipPlane = 0.01f; c.cam.farClipPlane = 60f;
                c.rt = new RenderTexture(256, 340, 24, RenderTextureFormat.ARGB32); c.rt.Create();
                c.cam.targetTexture = c.rt;

                if (def != null && def.modelPrefab != null)
                {
                    c.model = Instantiate(def.modelPrefab, c.mount);
                    c.model.transform.localPosition = Vector3.zero;
                    c.model.transform.localRotation = Quaternion.Euler(boardEuler);
                    FrameCard(c);
                    c.img.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(c.rt));
                }
                _cards.Add(c);
            }
            RefreshLabels();
        }

        private void FrameCard(Card c)
        {
            var rends = c.model.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            var b = rends[0].bounds;
            for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
            c.cam.orthographicSize = Mathf.Max(0.05f, b.extents.y * 1.12f);
            c.cam.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - 10f);
            c.cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        private void RefreshLabels()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                var def = DB.GetBoard(i);
                bool unlocked = IsUnlocked(i);
                _cards[i].coin.style.display = unlocked ? DisplayStyle.None : DisplayStyle.Flex;
                _cards[i].lbl.text = unlocked ? "OWN" : (def != null ? def.price.ToString("N0") : "");
                _cards[i].root.EnableInClassList("bs-card--focus", i == _focus);
            }

            // action buttons follow the FOCUSED board: owned -> SELECT only;
            // locked -> BUY (with the real price) + WATCH AD. START is always available.
            var fDef = DB != null ? DB.GetBoard(_focus) : null;
            bool fUnlocked = IsUnlocked(_focus);
            Show(_btnSelect, fUnlocked);
            Show(_btnBuy, !fUnlocked);
            Show(_btnWatch, !fUnlocked && (fDef == null || fDef.unlockableByAd));
            if (_buyPrice != null && fDef != null) _buyPrice.text = fDef.price.ToString("N0");
        }

        private static void Show(VisualElement e, bool v) { if (e != null) e.style.display = v ? DisplayStyle.Flex : DisplayStyle.None; }

        // ---- arrows nudge the scroll by one card ----
        private void Nudge(int dir)
        {
            if (_scroll == null) return;
            var o = _scroll.scrollOffset;
            o.x = Mathf.Clamp(o.x + dir * CardStride, 0f, MaxScroll());
            _scroll.scrollOffset = o;
        }

        private void OnPtrDown(PointerDownEvent e) { _downX = _lastX = e.position.x; _pid = e.pointerId; _dragging = false; }

        private void OnPtrMove(PointerMoveEvent e)
        {
            if ((e.pressedButtons & 1) == 0) return; // primary button / finger must be held
            float x = e.position.x;
            // only treat as a drag once past a small threshold (so taps still register)
            if (!_dragging && Mathf.Abs(x - _downX) > 6f) { _dragging = true; _scroll.CapturePointer(_pid); }
            if (_dragging)
            {
                var o = _scroll.scrollOffset;
                o.x = Mathf.Clamp(o.x - (x - _lastX), 0f, MaxScroll());
                _scroll.scrollOffset = o;
            }
            _lastX = x;
        }

        private void OnPtrUp(PointerUpEvent e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_scroll.HasPointerCapture(e.pointerId)) _scroll.ReleasePointer(e.pointerId);
        }

        private float MaxScroll()
        {
            if (_scroll == null) return 0f;
            float content = _scroll.contentContainer.worldBound.width;
            float view = _scroll.contentViewport.worldBound.width;
            return Mathf.Max(0f, content - view);
        }

        private void SyncSlider()
        {
            if (_sliderFill == null || _scroll == null) return;
            float content = _scroll.contentContainer.worldBound.width;
            float view = _scroll.contentViewport.worldBound.width;
            if (content <= 1f || view <= 1f) return;
            float fillFrac = Mathf.Clamp01(view / content);
            float max = Mathf.Max(1f, content - view);
            float scrollFrac = Mathf.Clamp01(_scroll.scrollOffset.x / max);
            _sliderFill.style.width = Length.Percent(fillFrac * 100f);
            _sliderFill.style.left = Length.Percent(scrollFrac * (1f - fillFrac) * 100f);
        }

        // ---- data actions (act on the focused board) ----
        private bool IsUnlocked(int i)
        {
            if (GameManager.Instance != null) return GameManager.Instance.Data.IsBoardUnlocked(i);
            var def = DB != null ? DB.GetBoard(i) : null;
            return def != null && def.price <= 0;
        }

        private void Select()
        {
            if (!IsUnlocked(_focus)) return;
            if (GameManager.Instance != null) GameManager.Instance.SelectBoard(_focus);
            Debug.Log("[BoardSelect] selected " + _focus);
        }

        /// <summary>NEXT: confirm the focused board (if owned) and advance to the race.</summary>
        private void Proceed()
        {
            if (IsUnlocked(_focus) && GameManager.Instance != null) GameManager.Instance.SelectBoard(_focus);
            if (onNext != null) onNext();
            else Debug.Log("[BoardSelect] NEXT (no router)");
        }

        private void Buy()
        {
            if (IsUnlocked(_focus)) return;
            var def = DB != null ? DB.GetBoard(_focus) : null;
            int price = def != null ? def.price : 0;
            if (GameManager.Instance != null && GameManager.Instance.TrySpendCoins(price))
            {
                GameManager.Instance.Data.UnlockBoard(_focus);
                GameManager.Instance.SaveData();
                GameManager.Instance.SelectBoard(_focus);
                RefreshLabels();
            }
            else Debug.Log("[BoardSelect] BUY focus=" + _focus + " price=" + price);
        }

        private void WatchAd()
        {
            AdManager.ShowRewarded("Board_unlock", ok =>
            {
                if (!ok) return;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.Data.UnlockBoard(_focus);
                    GameManager.Instance.SaveData();
                }
                RefreshLabels();
            });
        }

        private static void Bind(VisualElement root, string name, Action cb)
        {
            var b = root.Q<Button>(name);
            if (b != null) b.clicked += cb;
        }
    }
}
