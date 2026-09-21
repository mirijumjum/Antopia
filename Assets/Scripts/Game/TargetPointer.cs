using UnityEngine;
using UnityEngine.UI;

namespace Antopia
{
    // Flecha en el borde de la pantalla que senala un objetivo del mundo cuando queda fuera de vista.
    public class TargetPointer
    {
        readonly RectTransform _rt;
        readonly Image _bg;
        readonly Camera _cam;

        public TargetPointer(Transform parent)
        {
            _cam = Camera.main;
            _bg = UiKit.Icon(parent, "Icon45", Color.white, 0.5f, 0.5f, 0.5f, 0.5f);
            _rt = _bg.rectTransform;
            _bg.gameObject.SetActive(false);
        }

        public void Hide()
        {
            if (_bg.gameObject.activeSelf) _bg.gameObject.SetActive(false);
        }

        public void Show(Vector3 target, Color color) // el color ya no se usa: la flecha es siempre la del pack
        {
            if (_cam == null) { Hide(); return; }
            var vp = _cam.WorldToViewportPoint(target);
            bool front = vp.z > 0f;
            if (front && vp.x > 0.06f && vp.x < 0.94f && vp.y > 0.13f && vp.y < 0.84f) { Hide(); return; }

            var dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (!front) dir = -dir;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
            float t = Mathf.Min(0.40f / Mathf.Max(Mathf.Abs(dir.x), 1e-4f), 0.30f / Mathf.Max(Mathf.Abs(dir.y), 1e-4f));
            var p = new Vector2(0.5f, 0.49f) + dir * t;

            _bg.gameObject.SetActive(true);
            _rt.anchorMin = _rt.anchorMax = p;
            _rt.anchoredPosition = Vector2.zero;
            _rt.sizeDelta = new Vector2(96f, 96f);
            float deg = Mathf.Atan2(dir.y * Screen.height, dir.x * Screen.width) * Mathf.Rad2Deg;
            _rt.localRotation = Quaternion.Euler(0f, 0f, deg);
        }
    }
}
