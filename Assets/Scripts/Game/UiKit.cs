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

        static Sprite _circle, _ring;

        // Circulo (relleno o solo aro) generado en codigo, con borde suave.
        public static Sprite CircleSprite(bool ring)
        {
            if (ring ? _ring != null : _circle != null) return ring ? _ring : _circle;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            float r = N * 0.5f - 2f;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N * 0.5f, N * 0.5f));
                    float a = Mathf.Clamp01(r - d);
                    if (ring) a = Mathf.Min(a, Mathf.Clamp01(d - (r - 8f))) * 0.9f;
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            if (ring) _ring = sprite; else _circle = sprite;
            return sprite;
        }

        // Contorno oscuro para que el texto se lea sobre el mundo 3D.
        public static Text Outlined(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.08f, 0.06f, 0.03f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        // Barra redondeada oscura: fondo de indicadores, tarjetas y barras de progreso.
        public static Image Pill(Transform parent, string name, float alpha, float xMin, float yMin, float xMax, float yMax)
        {
            var img = Box(parent, name, new Color(1f, 1f, 1f, alpha), xMin, yMin, xMax, yMax);
            var sprite = LoadSprite("SliderBar_ProgressBar");
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            else img.color = new Color(0.10f, 0.08f, 0.06f, alpha);
            img.raycastTarget = false;
            return img;
        }

        // Imagen suelta de un sprite de Resources/UI (icono, gema, relleno de barra).
        public static Image Icon(Transform parent, string sprite, Color tint, float xMin, float yMin, float xMax, float yMax)
        {
            var img = Box(parent, "Icon", tint, xMin, yMin, xMax, yMax);
            img.sprite = LoadSprite(sprite);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        // Boton que es el propio icono (los iconos del pack ya son botones brillantes con dibujo).
        public static Button IconButton(Transform parent, string sprite, UnityAction onClick, float xMin, float yMin, float xMax, float yMax)
        {
            var img = Box(parent, "IconButton", Color.white, xMin, yMin, xMax, yMax);
            img.sprite = LoadSprite(sprite);
            img.preserveAspect = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            return btn;
        }

        // Boton grande con titulo y subtitulo.
        public static Button MakeBigButton(Transform parent, Color color, string icon, UnityAction onClick,
            float xMin, float yMin, float xMax, float yMax, out Text title, out Text subtitle)
        {
            var btn = MakeButton(parent, "", color, 30, onClick, xMin, yMin, xMax, yMax, icon);
            var label = btn.GetComponentInChildren<Text>();
            label.gameObject.SetActive(false);
            title = Outlined(Label(btn.transform, "", 60, TextAnchor.MiddleCenter, Color.white, 0.25f, 0.44f, 0.98f, 0.96f));
            subtitle = Outlined(Label(btn.transform, "", 30, TextAnchor.MiddleCenter, Color.white, 0.25f, 0.08f, 0.98f, 0.50f));
            title.fontStyle = FontStyle.Bold;
            return btn;
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
            var sprite = LoadSprite(ButtonSprites[(int)(disabled ? Skin.Cyan : SkinFor(color))]);
            if (sprite == null)
            {
                img.color = color;
                return;
            }
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = disabled ? new Color(0.55f, 0.60f, 0.64f, 1f) : Color.white;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }
    }
}
