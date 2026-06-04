using UnityEngine;
using UnityEngine.UI;

namespace Mixtape.UI
{
    /// <summary>
    /// Helper methods for building uGUI hierarchies in code with explicit anchoring.
    /// Used by scene/screen builders so layouts stay consistent and responsive.
    /// </summary>
    public static class UIFactory
    {
        public static Canvas CreateCanvas(string name, int sortOrder, Vector2 refRes)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = refRes;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>Full-stretch child (e.g. safe-area root or background).</summary>
        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
            bool preserveAspect = true, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = raycast;
            if (sprite == null) img.color = new Color(1, 1, 1, 0.15f);
            var rt = img.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            return img;
        }

        public static Image FullBackground(Transform parent, string name, Sprite sprite)
        {
            var img = Image(parent, name, sprite, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, false, false);
            return img;
        }

        public static Button Button(Transform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
            bool preserveAspect = true)
        {
            var img = Image(parent, name, sprite, anchorMin, anchorMax, pivot, anchoredPos, size, preserveAspect, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors; colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f); btn.colors = colors;
            return btn;
        }

        public static HoldButton HoldBtn(Transform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var img = Image(parent, name, sprite, anchorMin, anchorMax, pivot, anchoredPos, size, true, true);
            return img.gameObject.AddComponent<HoldButton>();
        }

        public static Text Text(Transform parent, string name, string content, Font font, int fontSize,
            TextAnchor align, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var t = go.GetComponent<Text>();
            t.text = content; t.font = font; t.fontSize = fontSize; t.alignment = align; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var rt = t.rectTransform; rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            return t;
        }
    }
}
