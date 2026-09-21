using System;
using UnityEngine;
using UnityEngine.UI;

namespace Antopia
{
    // Interfaz comun de los minijuegos: una barra de datos bajo los recursos, un aviso pasajero, una pista abajo
    // y un boton redondo para salir. Todo sobre el mundo 3D, sin tapar la vista.
    public class MinigameHud
    {
        public readonly Text Left, Center, Right, Msg, Hint;

        public MinigameHud(Transform root, Action onExit)
        {
            UiKit.Pill(root, "InfoBg", 0.8f, 0.03f, 0.855f, 0.97f, 0.915f);
            Left = Stat(root, TextAnchor.MiddleLeft, 0.07f, 0.31f, UiKit.Cream);
            Center = Stat(root, TextAnchor.MiddleCenter, 0.31f, 0.65f, UiKit.Gold);
            Right = Stat(root, TextAnchor.MiddleRight, 0.65f, 0.93f, UiKit.Cream);

            Msg = UiKit.Outlined(UiKit.Label(root, "", 46, TextAnchor.MiddleCenter, UiKit.Gold, 0.05f, 0.78f, 0.95f, 0.85f));

            UiKit.Pill(root, "HintBg", 0.8f, 0.03f, 0.025f, 0.77f, 0.105f);
            Hint = UiKit.Label(root, "", 30, TextAnchor.MiddleCenter, UiKit.Cream, 0.06f, 0.025f, 0.74f, 0.105f);
            UiKit.IconButton(root, "Icon78", () => onExit(), 0.80f, 0.022f, 0.97f, 0.108f);
        }

        static Text Stat(Transform root, TextAnchor anchor, float x0, float x1, Color color)
        {
            var t = UiKit.Label(root, "", 34, anchor, color, x0, 0.855f, x1, 0.915f);
            t.fontStyle = FontStyle.Bold;
            return t;
        }
    }
}
