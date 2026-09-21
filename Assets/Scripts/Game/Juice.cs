using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Antopia
{
    // Pequenos detalles que hacen que el juego "se sienta": textos que suben y se desvanecen, temblor de camara,
    // numeros que cuentan hacia su valor y botones que se hunden al pulsarlos.
    public static class Juice
    {
        // Texto flotante sobre un punto del mundo (se coloca en pantalla al crearlo).
        public static void Popup(RectTransform parent, Vector3 worldPos, string text, Color color, int size = 44)
        {
            var cam = Camera.main;
            if (cam == null || parent == null) return;
            var vp = cam.WorldToViewportPoint(worldPos + Vector3.up * 1.2f);
            if (vp.z <= 0f || vp.x < -0.1f || vp.x > 1.1f || vp.y < 0f || vp.y > 1f) return;
            var t = UiKit.Outlined(UiKit.Label(parent, text, size, TextAnchor.MiddleCenter, color, vp.x - 0.3f, vp.y, vp.x + 0.3f, vp.y + 0.07f));
            t.fontStyle = FontStyle.Bold;
            t.gameObject.AddComponent<PopupAnim>();
        }
    }

    public class PopupAnim : MonoBehaviour
    {
        const float Life = 1.1f;
        float _t;
        Text _text;
        RectTransform _rt;

        void Awake()
        {
            _text = GetComponent<Text>();
            _rt = (RectTransform)transform;
        }

        void Update()
        {
            _t += Time.deltaTime;
            _rt.anchoredPosition += new Vector2(0f, 110f * Time.deltaTime * Mathf.Clamp01(1.2f - _t));
            float pop = 1f + 0.25f * Mathf.Clamp01(1f - _t * 6f);
            _rt.localScale = Vector3.one * pop;
            var c = _text.color;
            c.a = Mathf.Clamp01((Life - _t) / 0.4f);
            _text.color = c;
            if (_t >= Life) Destroy(gameObject);
        }
    }

    // Temblor de camara: cualquiera lo pide con Trigger y quien mueve la camara suma Offset().
    public static class CamShake
    {
        static float _amp, _until, _dur = 0.01f;

        public static void Trigger(float amplitude = 0.25f, float duration = 0.3f)
        {
            _amp = Mathf.Max(amplitude, _amp * Mathf.Clamp01((_until - Time.time) / _dur));
            _dur = duration;
            _until = Time.time + duration;
        }

        public static Vector3 Offset()
        {
            float left = _until - Time.time;
            if (left <= 0f) return Vector3.zero;
            float k = _amp * (left / _dur);
            return new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f) * 0.6f, 0f) * k;
        }
    }

    // Numero de un indicador que sube o baja contando en vez de saltar, con un saltito al aumentar.
    public class CountText : MonoBehaviour
    {
        Text _text;
        float _shown, _target, _pop;
        Vector3 _baseScale = Vector3.one;

        public static CountText Attach(Text text)
        {
            var c = text.gameObject.AddComponent<CountText>();
            c._text = text;
            return c;
        }

        public void Set(int value, bool snap = false)
        {
            if (!snap && value > _target) _pop = 1f;
            _target = value;
            if (snap) _shown = value;
            _text.text = Mathf.RoundToInt(_shown).ToString();
        }

        void Update()
        {
            if (!Mathf.Approximately(_shown, _target))
            {
                _shown = Mathf.MoveTowards(_shown, _target, Mathf.Max(1f, Mathf.Abs(_target - _shown) * 5f) * Time.deltaTime);
                _text.text = Mathf.RoundToInt(_shown).ToString();
            }
            _pop = Mathf.MoveTowards(_pop, 0f, Time.deltaTime * 4f);
            transform.localScale = _baseScale * (1f + 0.22f * _pop);
        }
    }

    // El boton se hunde un poco mientras lo mantienes pulsado.
    public class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Vector3 _base = Vector3.one;
        float _target = 1f;

        void Awake() => _base = transform.localScale;
        public void OnPointerDown(PointerEventData e) => _target = 0.93f;
        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;

        void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _base * _target, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
        }
    }
}
