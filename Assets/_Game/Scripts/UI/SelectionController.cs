using UnityEngine;
using UnityEngine.UI;
using Mixtape.Core;
using Mixtape.Data;
using Mixtape.Ads;

namespace Mixtape.UI
{
    /// <summary>
    /// Drives both the Character and Skateboard selection screens. Shows a rotating 3D
    /// preview, handles prev/next browsing, lock/buy/watch-ad-to-unlock, select, and
    /// proceeding to the next step in the pre-race flow.
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        public enum Mode { Character, Board }

        [Header("Config")]
        public Mode mode = Mode.Character;
        public Transform previewMount;
        public float rotateSpeed = 30f;
        public float previewYaw = 180f;

        [Header("Refs (set by builder)")]
        public Text titleText;
        public Text nameText;
        public Text priceText;
        public Text coinText;
        public GameObject lockIcon;
        public GameObject buyButton;
        public GameObject adUnlockButton;
        public GameObject selectButton;
        public GameObject selectedLabel;

        private int _index;
        private GameObject _preview;

        private GameDatabase DB => GameManager.Instance != null ? GameManager.Instance.Database : null;
        private int Count => DB == null ? 0 : (mode == Mode.Character ? DB.CharacterCount : DB.BoardCount);

        private void Start()
        {
            if (titleText) titleText.text = mode == Mode.Character ? "SELECT YOUR CHARACTER" : "SELECT SKATEBOARD";
            _index = mode == Mode.Character ? GameManager.Instance.Data.selectedCharacter : GameManager.Instance.Data.selectedBoard;
            ShowCurrent();
        }

        private void Update()
        {
            if (_preview != null) _preview.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }

        private GameObject ModelOf(int i) => mode == Mode.Character ? DB.GetCharacter(i)?.modelPrefab : DB.GetBoard(i)?.modelPrefab;
        private string NameOf(int i) => mode == Mode.Character ? DB.GetCharacter(i)?.displayName : DB.GetBoard(i)?.displayName;
        private int PriceOf(int i) => mode == Mode.Character ? DB.GetCharacter(i).price : DB.GetBoard(i).price;
        private bool Unlocked(int i) => mode == Mode.Character ? GameManager.Instance.Data.IsCharacterUnlocked(i) : GameManager.Instance.Data.IsBoardUnlocked(i);
        private bool IsSelected(int i) => i == (mode == Mode.Character ? GameManager.Instance.Data.selectedCharacter : GameManager.Instance.Data.selectedBoard);

        private void Unlock(int i)
        {
            if (mode == Mode.Character) GameManager.Instance.Data.UnlockCharacter(i);
            else GameManager.Instance.Data.UnlockBoard(i);
            GameManager.Instance.SaveData();
        }

        private void SetSelected(int i)
        {
            if (mode == Mode.Character) GameManager.Instance.SelectCharacter(i);
            else GameManager.Instance.SelectBoard(i);
        }

        private void ShowCurrent()
        {
            if (DB == null || Count == 0) return;

            if (_preview != null) Destroy(_preview);
            var prefab = ModelOf(_index);
            if (prefab != null && previewMount != null)
            {
                _preview = Instantiate(prefab, previewMount);
                _preview.transform.localPosition = Vector3.zero;
                _preview.transform.localRotation = Quaternion.Euler(0, previewYaw, 0);
            }

            bool unlocked = Unlocked(_index);
            bool selected = IsSelected(_index);
            int price = PriceOf(_index);

            if (nameText) nameText.text = NameOf(_index);
            if (priceText) priceText.text = price.ToString();
            if (coinText && GameManager.Instance != null) coinText.text = GameManager.Instance.Data.coins.ToString("N0");

            if (lockIcon) lockIcon.SetActive(!unlocked);
            if (buyButton) buyButton.SetActive(!unlocked);
            if (adUnlockButton) adUnlockButton.SetActive(!unlocked);
            if (selectButton) selectButton.SetActive(unlocked && !selected);
            if (selectedLabel) selectedLabel.SetActive(unlocked && selected);
        }

        public void Next() { _index = (_index + 1) % Count; ShowCurrent(); }
        public void Prev() { _index = (_index - 1 + Count) % Count; ShowCurrent(); }

        public void Buy()
        {
            if (Unlocked(_index)) return;
            int price = PriceOf(_index);
            if (GameManager.Instance.TrySpendCoins(price)) { Unlock(_index); SetSelected(_index); ShowCurrent(); }
        }

        public void WatchAdToUnlock()
        {
            AdManager.ShowRewarded(mode + "_unlock", ok =>
            {
                if (ok) { Unlock(_index); SetSelected(_index); ShowCurrent(); }
            });
        }

        public void Select()
        {
            if (!Unlocked(_index)) return;
            SetSelected(_index);
            ShowCurrent();
        }

        /// <summary>Big "Next/Go" button: advance the pre-race flow.</summary>
        public void Proceed()
        {
            if (Unlocked(_index)) SetSelected(_index);
            if (mode == Mode.Character) SceneFlow.LoadDirect(SceneFlow.BoardSelect);
            else SceneFlow.LoadVia(SceneFlow.Game);
        }

        public void Back()
        {
            if (mode == Mode.Character) SceneFlow.LoadDirect(SceneFlow.MainMenu);
            else SceneFlow.LoadDirect(SceneFlow.CharacterSelect);
        }
    }
}
