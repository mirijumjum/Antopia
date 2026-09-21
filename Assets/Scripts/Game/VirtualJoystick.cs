using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Antopia
{
    // Joystick virtual flotante, el tipico de los juegos de movil: aparece donde pones el dedo, el punto sigue al
    // dedo y Value da la direccion (x derecha, y arriba) con longitud de 0 a 1. Si el dedo se aleja mas del
    // radio, la base lo sigue. Al soltar, Value vuelve a cero y el joystick desaparece.
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const float Radius = 120f; // en unidades del lienzo (1080 de ancho)

        public Vector2 Value { get; private set; }

        RectTransform _pad, _baseRt, _knobRt;
        Vector2 _center;

        // Convierte cualquier zona tactil (una Image transparente) en un joystick.
        public static VirtualJoystick Attach(GameObject pad) => pad.AddComponent<VirtualJoystick>();

        void Awake()
        {
            _pad = (RectTransform)transform;
            _baseRt = Circle("JoystickBase", UiKit.CircleSprite(true), Radius * 2f + 30f, 0.55f);
            _knobRt = Circle("JoystickKnob", UiKit.CircleSprite(false), 104f, 0.75f);
        }

        RectTransform Circle(string name, Sprite sprite, float size, float alpha)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
            go.SetActive(false);
            return rt;
        }

        Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_pad, e.position, e.pressEventCamera, out var p);
            return p;
        }

        public void OnPointerDown(PointerEventData e)
        {
            _center = Local(e);
            Value = Vector2.zero;
            _baseRt.anchoredPosition = _knobRt.anchoredPosition = _center;
            _baseRt.gameObject.SetActive(true);
            _knobRt.gameObject.SetActive(true);
        }

        public void OnDrag(PointerEventData e)
        {
            var p = Local(e);
            var delta = p - _center;
            if (delta.magnitude > Radius)
            {
                _center += delta.normalized * (delta.magnitude - Radius); // la base sigue al dedo
                delta = p - _center;
            }
            Value = delta / Radius;
            _baseRt.anchoredPosition = _center;
            _knobRt.anchoredPosition = _center + delta;
        }

        public void OnPointerUp(PointerEventData e) => Release();

        void OnDisable() => Release();

        void Release()
        {
            Value = Vector2.zero;
            if (_baseRt != null) _baseRt.gameObject.SetActive(false);
            if (_knobRt != null) _knobRt.gameObject.SetActive(false);
        }
    }
}
