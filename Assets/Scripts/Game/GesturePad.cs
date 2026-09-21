using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Antopia
{
    // Zona donde se dibuja con el dedo: recoge el trazo (en unidades del lienzo), lo pinta como un rastro de puntos
    // dorados que se desvanece al soltar y avisa con Drawn cuando el dedo se levanta.
    public class GesturePad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int MaxDots = 220;
        const float DotSpacing = 9f;
        const float FadeSeconds = 0.55f;

        public event Action<List<Vector2>> Drawn;
        public bool Enabled = true;

        RectTransform _pad;
        readonly List<Vector2> _points = new List<Vector2>();
        readonly List<Image> _dots = new List<Image>();
        int _used;
        float _fade = -1f;

        public static GesturePad Attach(GameObject pad) => pad.AddComponent<GesturePad>();

        void Awake()
        {
            _pad = (RectTransform)transform;
            var sprite = UiKit.CircleSprite(false);
            for (int i = 0; i < MaxDots; i++)
            {
                var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(30f, 30f);
                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                go.SetActive(false);
                _dots.Add(img);
            }
        }

        Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_pad, e.position, e.pressEventCamera, out var p);
            return p;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!Enabled) return;
            ClearDots();
            _points.Clear();
            AddPoint(Local(e));
        }

        public void OnDrag(PointerEventData e)
        {
            if (!Enabled || _points.Count == 0) return;
            AddPoint(Local(e));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!Enabled || _points.Count == 0) return;
            _fade = 0f;
            var stroke = new List<Vector2>(_points);
            _points.Clear();
            Drawn?.Invoke(stroke);
        }

        void AddPoint(Vector2 p)
        {
            if (_points.Count > 0 && Vector2.Distance(_points[_points.Count - 1], p) < 2f) return;
            _points.Add(p);
            if (_used == 0 || Vector2.Distance(_dots[_used - 1].rectTransform.anchoredPosition, p) >= DotSpacing)
            {
                if (_used >= MaxDots) return;
                var dot = _dots[_used++];
                dot.rectTransform.anchoredPosition = p;
                dot.color = new Color(1f, 0.85f, 0.25f, 0.95f);
                dot.gameObject.SetActive(true);
            }
        }

        void ClearDots()
        {
            for (int i = 0; i < _used; i++) _dots[i].gameObject.SetActive(false);
            _used = 0;
            _fade = -1f;
        }

        void Update()
        {
            if (_fade < 0f) return;
            _fade += Time.deltaTime;
            float a = 1f - _fade / FadeSeconds;
            if (a <= 0f)
            {
                ClearDots();
                return;
            }
            for (int i = 0; i < _used; i++) _dots[i].color = new Color(1f, 0.85f, 0.25f, 0.95f * a);
        }
    }
}
