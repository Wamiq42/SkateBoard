using System;
using UnityEngine;
using UnityEngine.UIElements;
using Mixtape.Core;
using Mixtape.Data;
using Mixtape.Ads;

namespace Mixtape.UITK
{
    /// <summary>
    /// Character Select (UI Toolkit). Shows a live rotating 3D preview of the
    /// selected girl (rendered to a RenderTexture shown in the UI), stat bars,
    /// and prev/next + buy/watch-ad/select wired to the catalog & player data.
    ///
    /// Standalone-friendly: assign <see cref="database"/> so it works in the
    /// sandbox with no GameManager. Unlock/selection state falls back to
    /// "free chars unlocked, first selected" when GameManager isn't present.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CharacterSelectView : MonoBehaviour
    {
        [Tooltip("Catalog. Falls back to GameManager.Database when left null.")]
        public GameDatabase database;
        public float rotateSpeed = 35f;
        public float previewYaw = 180f;

        // ui refs
        private VisualElement _charImg;
        private VisualElement _fAcc, _fSta, _fHea, _fSpe;
        private Label _vAcc, _vSta, _vHea, _vSpe;
        private Button _btnSelect, _btnBuy, _btnWatch;

        // 3d preview rig
        private Camera _cam;
        private Transform _mount;
        private GameObject _rig, _model;
        private RenderTexture _rt;

        private int _index;
        private int _localSelected = -1; // sandbox-only selection memory

        private GameDatabase DB =>
            database != null ? database :
            (GameManager.Instance != null ? GameManager.Instance.Database : null);
        private int Count => DB != null ? DB.CharacterCount : 0;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _charImg = root.Q<VisualElement>("char-img");
            _fAcc = root.Q<VisualElement>("fill-accuracy");
            _fSta = root.Q<VisualElement>("fill-stamina");
            _fHea = root.Q<VisualElement>("fill-health");
            _fSpe = root.Q<VisualElement>("fill-speed");
            _vAcc = root.Q<Label>("val-accuracy");
            _vSta = root.Q<Label>("val-stamina");
            _vHea = root.Q<Label>("val-health");
            _vSpe = root.Q<Label>("val-speed");
            _btnSelect = root.Q<Button>("cs-select");
            _btnBuy = root.Q<Button>("cs-buy");
            _btnWatch = root.Q<Button>("cs-watchad");

            Bind(root, "cs-prev", Prev);
            Bind(root, "cs-next", Next);
            Bind(root, "cs-select", Select);
            Bind(root, "cs-buy", Buy);
            Bind(root, "cs-watchad", WatchAd);

            BuildRig();
            _index = GameManager.Instance != null ? GameManager.Instance.Data.selectedCharacter : 0;
            if (Count > 0) _index = Mathf.Clamp(_index, 0, Count - 1);
            ShowCurrent();
        }

        private void OnDisable()
        {
            if (_model != null) Destroy(_model);
            if (_rig != null) Destroy(_rig);
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            _model = null; _rig = null; _rt = null; _cam = null; _mount = null;
        }

        private void Update()
        {
            if (_model != null) _model.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }

        // ---- 3D preview rig (built far from the scene so the main camera never sees it) ----
        private void BuildRig()
        {
            if (_cam != null) return;
            _rig = new GameObject("CharPreviewRig");
            _rig.transform.position = new Vector3(10000f, 0f, 0f);

            _mount = new GameObject("Mount").transform;
            _mount.SetParent(_rig.transform, false);

            var camGo = new GameObject("PreviewCam");
            camGo.transform.SetParent(_rig.transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent
            _cam.nearClipPlane = 0.01f;
            _cam.farClipPlane = 100f;

            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_rig.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 200f, 0f);

            // wide enough (ar ~0.75) that the figure's hands aren't clipped
            _rt = new RenderTexture(800, 1067, 24, RenderTextureFormat.ARGB32) { name = "CharPreviewRT" };
            _rt.Create();
            _cam.targetTexture = _rt;

            if (_charImg != null)
                _charImg.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_rt));
        }

        private void ShowCurrent()
        {
            if (DB == null || Count == 0) return;
            var def = DB.GetCharacter(_index);

            // spawn the model
            if (_model != null) Destroy(_model);
            if (def != null && def.modelPrefab != null && _mount != null)
            {
                _model = Instantiate(def.modelPrefab, _mount);
                _model.transform.localPosition = Vector3.zero;
                _model.transform.localRotation = Quaternion.Euler(0f, previewYaw, 0f);
                FrameModel();
            }

            // stats
            SetStat(_fAcc, _vAcc, def != null ? def.accuracy : 0);
            SetStat(_fSta, _vSta, def != null ? def.stamina : 0);
            SetStat(_fHea, _vHea, def != null ? def.health : 0);
            SetStat(_fSpe, _vSpe, def != null ? def.speed : 0);

            // buttons: unlocked -> SELECT only; locked -> BUY + WATCH AD
            bool unlocked = IsUnlocked(_index);
            Show(_btnSelect, unlocked);
            Show(_btnBuy, !unlocked);
            Show(_btnWatch, !unlocked && (def == null || def.unlockableByAd));
        }

        private void FrameModel()
        {
            if (_model == null || _cam == null) return;
            var rends = _model.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            // frame the feet (b.min.y) to the bottom of the texture so the character
            // stands on the podium, with ~10% headroom above.
            float size = Mathf.Max(0.1f, b.extents.y * 1.1f);
            _cam.orthographicSize = size;
            _cam.transform.position = new Vector3(b.center.x, b.min.y + size, b.center.z - 10f);
            _cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        private static void SetStat(VisualElement fill, Label val, int pct)
        {
            pct = Mathf.Clamp(pct, 0, 100);
            if (fill != null) fill.style.width = Length.Percent(pct);
            if (val != null) val.text = pct + "%";
        }

        private static void Show(VisualElement e, bool v) { if (e != null) e.style.display = v ? DisplayStyle.Flex : DisplayStyle.None; }

        private bool IsUnlocked(int i)
        {
            if (GameManager.Instance != null) return GameManager.Instance.Data.IsCharacterUnlocked(i);
            var def = DB != null ? DB.GetCharacter(i) : null;
            return def != null && def.price <= 0; // sandbox fallback
        }

        private void SetSelected(int i)
        {
            if (GameManager.Instance != null) GameManager.Instance.SelectCharacter(i);
            else _localSelected = i;
        }

        // ---- actions ----
        public void Prev() { if (Count == 0) return; _index = (_index - 1 + Count) % Count; ShowCurrent(); }
        public void Next() { if (Count == 0) return; _index = (_index + 1) % Count; ShowCurrent(); }

        public void Select()
        {
            if (!IsUnlocked(_index)) return;
            SetSelected(_index);
            Debug.Log("[CharSelect] selected " + _index);
        }

        public void Buy()
        {
            if (IsUnlocked(_index)) return;
            var def = DB != null ? DB.GetCharacter(_index) : null;
            int price = def != null ? def.price : 0;
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.TrySpendCoins(price))
                {
                    GameManager.Instance.Data.UnlockCharacter(_index);
                    GameManager.Instance.SaveData();
                    SetSelected(_index);
                    ShowCurrent();
                }
            }
            else Debug.Log("[CharSelect] BUY (no GameManager in sandbox) price=" + price);
        }

        public void WatchAd()
        {
            AdManager.ShowRewarded("Character_unlock", ok =>
            {
                if (!ok) return;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.Data.UnlockCharacter(_index);
                    GameManager.Instance.SaveData();
                }
                SetSelected(_index);
                ShowCurrent();
            });
        }

        private static void Bind(VisualElement root, string name, Action cb)
        {
            var b = root.Q<Button>(name);
            if (b != null) b.clicked += cb;
        }
    }
}
