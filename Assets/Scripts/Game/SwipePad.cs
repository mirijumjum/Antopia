using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Antopia
{
    // Zona tactil que convierte deslizamientos en una direccion de pantalla (normalizada).
    // Mientras el dedo sigue arrastrando se emiten nuevas direcciones, asi se puede girar sin levantarlo.
    public class SwipePad : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public event Action<Vector2> Swiped;

        Vector2 _anchor;

        float MinDistance => Mathf.Max(30f, Screen.height * 0.02f);

        public void OnPointerDown(PointerEventData e)
        {
            _anchor = e.position;
        }

        public void OnDrag(PointerEventData e)
        {
            var d = e.position - _anchor;
            if (d.magnitude < MinDistance) return;
            _anchor = e.position;
            Swiped?.Invoke(d.normalized);
        }
    }
}
