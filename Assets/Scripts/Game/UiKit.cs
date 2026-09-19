using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Antopia
{
    // Helpers para construir uGUI por codigo.
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.16f, 0.12f, 0.09f, 1f);
        public static readonly Color Cream = new Color(0.98f, 0.94f, 0.83f, 1f);
        public static readonly Color Leaf = new Color(0.35f, 0.66f, 0.29f, 1f);
        public static readonly Color Twig = new Color(0.62f, 0.42f, 0.23f, 1f);
        public static readonly Color Gold = new Color(0.95f, 0.72f, 0.18f, 1f);
        public static readonly Color Panel = new Color(0.12f, 0.09f, 0.06f, 0.92f);
        public static readonly Color Disabled = new Color(0.45f, 0.42f, 0.38f, 1f);

        static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static RectTransform CreateCanvas(out RectTransform safeArea)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            var safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(go.transform, false);
            safeArea = safe.GetComponent<RectTransform>();
            ApplySafeArea(safeArea);
            return go.GetComponent<RectTransform>();
        }

        static void ApplySafeArea(RectTransform rt)
        {
            var sa = Screen.safeArea;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static RectTransform Rect(Transform parent, string name, float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Box(Transform parent, string name, Color color, float xMin, float yMin, float xMax, float yMax)
        {
            var rt = Rect(parent, name, xMin, yMin, xMax, yMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var rt = Rect(parent, "Label", xMin, yMin, xMax, yMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string text, Color color, int fontSize, UnityAction onClick,
            float xMin, float yMin, float xMax, float yMax)
        {
            var img = Box(parent, "Button", color, xMin, yMin, xMax, yMax);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            Label(img.transform, text, fontSize, TextAnchor.MiddleCenter, Ink, 0.02f, 0.02f, 0.98f, 0.98f);
            return btn;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }
    }
}
