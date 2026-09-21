using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Antopia
{
    // Zona tactil que avisa en cuanto se toca (sin esperar a soltar), para juegos de reflejos.
    public class TapPad : MonoBehaviour, IPointerDownHandler
    {
        public event Action Tapped;

        public static TapPad Attach(GameObject pad) => pad.AddComponent<TapPad>();

        public void OnPointerDown(PointerEventData e) => Tapped?.Invoke();
    }
}
