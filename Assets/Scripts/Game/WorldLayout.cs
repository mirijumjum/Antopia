using System.Collections.Generic;
using UnityEngine;

namespace Antopia
{
    // Plano del mundo (coordenadas X/Z). La zona del nido es un cuadrado de 28 m rodeado de muros de piedra. Al
    // norte hay otra zona, cuyo paso esta tapado por un escarabajo (lo despejan las soldado); al este hay una
    // tercera, cuyo muro solo se abre con un tunel (lo levantan las constructoras). Cada zona esconde un sitio de
    // interes que descubre la exploradora al destapar la niebla.
    public static class WorldLayout
    {
        public const float Half = 14f;    // medio lado de la zona del nido
        public const float Far = 30f;     // limite exterior de las zonas norte y este
        public const float Thick = 1.2f;  // grosor de los muros
        public const float GuardGap = 2f; // medio ancho del paso del norte
        public const float TunnelGap = 1.6f;

        public static readonly Vector3 GuardPos = new Vector3(0f, 0f, Half);
        public static readonly Vector3 TunnelPos = new Vector3(Half, 0f, 0f);
        public static readonly Vector3[] PoiPos = { new Vector3(0f, 0f, 23f), new Vector3(23f, 0f, 0f) };
        public static readonly string[] PoiNames = { "la Colmena de miel", "la Cueva de cristales" };

        // Zonas: 0 = nido, 1 = norte, 2 = este.
        public static readonly Rect[] Zones =
        {
            Rect.MinMaxRect(-Half, -Half, Half, Half),
            Rect.MinMaxRect(-Half, Half, Half, Far),
            Rect.MinMaxRect(Half, -Half, Far, Half),
        };

        // Punto de la zona (dejando "margin" de aire junto a los muros).
        public static bool InZone(int zone, Vector3 p, float margin = 0f)
        {
            var r = Zones[zone];
            return p.x > r.xMin + margin && p.x < r.xMax - margin && p.z > r.yMin + margin && p.z < r.yMax - margin;
        }

        public static int ZoneOf(Vector3 p)
        {
            for (int i = 0; i < Zones.Length; i++)
                if (InZone(i, p)) return i;
            return -1;
        }

        // Muros como rectangulos en el plano XZ (Rect.x = X, Rect.y = Z). El tapon del tunel solo existe hasta que
        // se construye.
        public static List<Rect> BuildWalls(bool tunnelBuilt, out Rect plug)
        {
            var w = new List<Rect>();
            void Seg(float x0, float z0, float x1, float z1)
            {
                float t = Thick * 0.5f;
                w.Add(Rect.MinMaxRect(Mathf.Min(x0, x1) - t, Mathf.Min(z0, z1) - t, Mathf.Max(x0, x1) + t, Mathf.Max(z0, z1) + t));
            }
            Seg(-Half, -Half, Far, -Half);                 // sur
            Seg(-Half, -Half, -Half, Far);                 // oeste
            Seg(-Half, Far, Half, Far);                    // norte
            Seg(Half, Half, Half, Far);                    // lado este de la zona norte
            Seg(Half, Half, Far, Half);                    // techo de la zona este
            Seg(Far, -Half, Far, Half);                    // fondo de la zona este
            Seg(-Half, Half, -GuardGap, Half);             // divisoria norte, con el hueco del escarabajo
            Seg(GuardGap, Half, Half, Half);
            Seg(Half, -Half, Half, -TunnelGap);            // divisoria este, con el hueco del tunel
            Seg(Half, TunnelGap, Half, Half);
            float tt = Thick * 0.5f;
            plug = Rect.MinMaxRect(Half - tt, -TunnelGap, Half + tt, TunnelGap);
            if (!tunnelBuilt) w.Add(plug);
            return w;
        }
    }
}
