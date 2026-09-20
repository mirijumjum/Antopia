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

        // Aspecto de los botones y marcos: cada color del juego se traduce a una variante del pack de UI.
        public enum Skin { Cyan, Purple, Green, Orange, Red }

        static readonly string[] ButtonSprites = { "Button21", "Button22", "Button23", "Button24", "Button25" };
        static readonly string[] FrameSprites = { "Msg17", "Msg20", "Msg18", "Msg16", "Msg19" };

        static Sprite LoadSprite(string name) => Resources.Load<Sprite>("UI/" + name);

        static Skin SkinFor(Color c)
        {
            var refs = new (Skin skin, Color color)[]
            {
                (Skin.Cyan, Cream), (Skin.Green, Leaf), (Skin.Orange, Gold), (Skin.Orange, Twig),
                (Skin.Red, new Color(0.85f, 0.35f, 0.30f)), (Skin.Purple, new Color(0.62f, 0.50f, 0.90f)),
            };
            var best = Skin.Orange;
            float bestDist = float.MaxValue;
            foreach (var r in refs)
            {
                float d = Mathf.Pow(c.r - r.color.r, 2) + Mathf.Pow(c.g - r.color.g, 2) + Mathf.Pow(c.b - r.color.b, 2);
                if (d < bestDist) { bestDist = d; best = r.skin; }
            }
            return best;
        }

        // Marco 9-slice con cabecera de color y fondo oscuro.
        public static Image Frame(Transform parent, string name, Skin skin, float xMin, float yMin, float xMax, float yMax)
        {
            var img = Box(parent, name, Color.white, xMin, yMin, xMax, yMax);
            var sprite = LoadSprite(FrameSprites[(int)skin]);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            else img.color = Panel;
            return img;
        }

        // El color elige la variante del boton (Disabled = naranja apagado). "icon" es el nombre de un sprite de Resources/UI.
        public static Button MakeButton(Transform parent, string text, Color color, int fontSize, UnityAction onClick,
            float xMin, float yMin, float xMax, float yMax, string icon = null)
        {
            var img = Box(parent, "Button", color, xMin, yMin, xMax, yMax);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            SetButtonColor(btn, color);

            float textLeft = 0.02f;
            var iconSprite = icon != null ? LoadSprite(icon) : null;
            if (iconSprite != null)
            {
                var ico = Box(img.transform, "Icon", Color.white, 0.04f, 0.10f, 0.24f, 0.90f);
                ico.sprite = iconSprite;
                ico.preserveAspect = true;
                ico.raycastTarget = false;
                textLeft = 0.25f;
            }
            var label = Label(img.transform, text, fontSize, TextAnchor.MiddleCenter, img.sprite != null ? Color.white : Ink,
                textLeft, 0.02f, 0.98f, 0.98f);
            if (img.sprite != null)
            {
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.10f, 0.07f, 0.04f, 0.85f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            return btn;
        }

        // Cambia la variante del boton (por ejemplo cuando cambia el rol elegido).
        public static void SetButtonColor(Button b, Color color)
        {
            var img = (Image)b.targetGraphic;
            bool disabled = color == Disabled;
            var sprite = LoadSprite(ButtonSprites[(int)SkinFor(disabled ? Gold : color)]);
            if (sprite == null)
            {
                img.color = color;
                return;
            }
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = disabled ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }
    }
}
