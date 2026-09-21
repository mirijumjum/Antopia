using System.Collections.Generic;
using UnityEngine;

namespace Antopia
{
    // Iconos de las formas (blancos, con borde suave) dibujados en codigo a partir de las plantillas del reconocedor.
    public static class GestureIcons
    {
        static readonly Dictionary<GestureShape, Sprite> Cache = new Dictionary<GestureShape, Sprite>();

        public static Sprite Get(GestureShape shape)
        {
            if (Cache.TryGetValue(shape, out var s) && s != null) return s;
            const int N = 128;
            const float half = 6.5f; // mitad del grosor del trazo en pixeles
            var poly = GestureRecognizer.Template(shape);
            var pts = new List<Vector2>();
            foreach (var p in poly) pts.Add(new Vector2(N * 0.5f + p.x * N * 0.42f, N * 0.5f + p.y * N * 0.42f));

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    var q = new Vector2(x + 0.5f, y + 0.5f);
                    float d = float.MaxValue;
                    for (int i = 1; i < pts.Count; i++) d = Mathf.Min(d, DistanceToSegment(q, pts[i - 1], pts[i]));
                    px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(half + 1f - d));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            Cache[shape] = s;
            return s;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
