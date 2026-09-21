using System.Collections.Generic;
using UnityEngine;

namespace Antopia
{
    public enum GestureShape { Line, Zigzag, Triangle, Circle, Star }

    // Reconoce cinco formas dibujadas con un solo trazo: raya, zigzag, triangulo, circulo y estrella (de cinco puntas
    // de un trazo, o su contorno). Trabaja con la geometria del trazo (esquinas tras simplificarlo), no con plantillas,
    // asi no depende de la direccion, el punto de partida ni el tamano.
    public static class GestureRecognizer
    {
        public static readonly string[] Names = { "Raya", "Zigzag", "Triangulo", "Circulo", "Estrella" };

        // Tamano minimo (diagonal, en unidades del lienzo) para que cuente como dibujo.
        public const float MinSize = 140f;

        public static bool TryRecognize(IList<Vector2> raw, out GestureShape shape)
        {
            shape = GestureShape.Line;
            if (raw == null || raw.Count < 6) return false;

            var pts = Resample(raw, 64);
            Smooth(pts, 2); // quita el temblor del dedo
            float length = PathLength(pts);
            var min = pts[0];
            var max = pts[0];
            foreach (var p in pts)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            float diag = (max - min).magnitude;
            if (diag < MinSize || length < 1f) return false;

            float gap = Vector2.Distance(pts[0], pts[pts.Count - 1]);
            bool closed = gap < 0.22f * length;

            // Raya: abierta y casi recta.
            if (!closed)
            {
                float maxDev = 0f;
                var chord = (pts[pts.Count - 1] - pts[0]).normalized;
                foreach (var p in pts)
                {
                    var v = p - pts[0];
                    maxDev = Mathf.Max(maxDev, Mathf.Abs(v.x * chord.y - v.y * chord.x));
                }
                if (gap > 0.82f * length && maxDev < 0.15f * gap)
                {
                    shape = GestureShape.Line;
                    return true;
                }
            }

            var poly = Simplify(pts, 0.08f * diag);

            if (!closed)
            {
                // Zigzag: al menos tres esquinas marcadas que giran alternando de lado.
                if (poly.Count < 4) return false;
                int prevSign = 0;
                for (int i = 1; i < poly.Count - 1; i++)
                {
                    var a = poly[i] - poly[i - 1];
                    var b = poly[i + 1] - poly[i];
                    if (Vector2.Angle(a, b) < 55f) return false;
                    int sign = a.x * b.y - a.y * b.x > 0f ? 1 : -1;
                    if (sign == prevSign) return false;
                    prevSign = sign;
                }
                shape = GestureShape.Zigzag;
                return true;
            }

            // Formas cerradas: el poligono simplificado sin el punto repetido del cierre.
            if (poly.Count > 3 && Vector2.Distance(poly[0], poly[poly.Count - 1]) < 0.22f * length) poly.RemoveAt(poly.Count - 1);
            int n = poly.Count;
            if (n < 3) return false;

            var turns = new float[n];
            var signs = new int[n];
            float maxTurn = 0f;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i] - poly[(i + n - 1) % n];
                var b = poly[(i + 1) % n] - poly[i];
                turns[i] = Vector2.Angle(a, b);
                signs[i] = a.x * b.y - a.y * b.x > 0f ? 1 : -1;
                maxTurn = Mathf.Max(maxTurn, turns[i]);
            }

            // Circulo: sin esquinas fuertes y casi la misma distancia al centro.
            var c = Vector2.zero;
            foreach (var p in pts) c += p;
            c /= pts.Count;
            float mean = 0f;
            foreach (var p in pts) mean += Vector2.Distance(p, c);
            mean /= pts.Count;
            float variance = 0f;
            foreach (var p in pts) variance += Mathf.Pow(Vector2.Distance(p, c) - mean, 2f);
            float cv = Mathf.Sqrt(variance / pts.Count) / Mathf.Max(mean, 1e-3f);
            float aspect = (max.x - min.x) / Mathf.Max(max.y - min.y, 1e-3f);
            if (maxTurn < 75f && cv < 0.25f && aspect > 0.5f && aspect < 2f)
            {
                shape = GestureShape.Circle;
                return true;
            }

            // Cuenta las esquinas de verdad.
            int corners = 0;
            int sharp = 0;
            for (int i = 0; i < n; i++)
            {
                if (turns[i] > 50f) corners++;
                if (turns[i] > 110f) sharp++;
            }

            if (corners == 3 && maxTurn < 150f)
            {
                shape = GestureShape.Triangle;
                return true;
            }

            // Estrella de un trazo (pentagrama): cinco esquinas muy agudas.
            if (corners == 5 && sharp >= 4)
            {
                shape = GestureShape.Star;
                return true;
            }

            // Contorno de estrella: diez esquinas alternando entrante y saliente.
            if (corners >= 9 && corners <= 11)
            {
                int alternations = 0;
                for (int i = 0; i < n; i++)
                    if (turns[i] > 50f && turns[(i + 1) % n] > 50f && signs[i] != signs[(i + 1) % n]) alternations++;
                if (alternations >= 8)
                {
                    shape = GestureShape.Star;
                    return true;
                }
            }
            return false;
        }

        // ---------------- Geometria ----------------
        static float PathLength(IList<Vector2> p)
        {
            float l = 0f;
            for (int i = 1; i < p.Count; i++) l += Vector2.Distance(p[i - 1], p[i]);
            return l;
        }

        // Media movil de tres puntos (los extremos no se mueven).
        static void Smooth(List<Vector2> p, int passes)
        {
            for (int k = 0; k < passes; k++)
            {
                var copy = new List<Vector2>(p);
                for (int i = 1; i < p.Count - 1; i++) p[i] = (copy[i - 1] + copy[i] + copy[i + 1]) / 3f;
            }
        }

        // Reparte n puntos a igual distancia a lo largo del trazo.
        static List<Vector2> Resample(IList<Vector2> raw, int n)
        {
            var pts = new List<Vector2>(raw);
            float step = PathLength(pts) / (n - 1);
            var result = new List<Vector2> { pts[0] };
            float acc = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                float d = Vector2.Distance(pts[i - 1], pts[i]);
                if (d <= 0f) continue;
                if (acc + d >= step)
                {
                    float t = (step - acc) / d;
                    var q = Vector2.Lerp(pts[i - 1], pts[i], t);
                    result.Add(q);
                    pts.Insert(i, q);
                    acc = 0f;
                }
                else acc += d;
            }
            while (result.Count < n) result.Add(pts[pts.Count - 1]);
            return result;
        }

        // Douglas-Peucker: deja solo los vertices que se apartan mas de epsilon.
        static List<Vector2> Simplify(List<Vector2> pts, float epsilon)
        {
            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            SimplifyRange(pts, 0, pts.Count - 1, epsilon, keep);
            var result = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
                if (keep[i]) result.Add(pts[i]);
            return result;
        }

        static void SimplifyRange(List<Vector2> pts, int a, int b, float epsilon, bool[] keep)
        {
            if (b <= a + 1) return;
            float best = 0f;
            int index = -1;
            for (int i = a + 1; i < b; i++)
            {
                float d = DistanceToSegment(pts[i], pts[a], pts[b]);
                if (d > best)
                {
                    best = d;
                    index = i;
                }
            }
            if (best > epsilon && index >= 0)
            {
                keep[index] = true;
                SimplifyRange(pts, a, index, epsilon, keep);
                SimplifyRange(pts, index, b, epsilon, keep);
            }
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------------- Plantillas ideales (para dibujar el icono de cada forma) ----------------
        public static List<Vector2> Template(GestureShape shape)
        {
            var l = new List<Vector2>();
            switch (shape)
            {
                case GestureShape.Line:
                    l.Add(new Vector2(-0.9f, -0.5f));
                    l.Add(new Vector2(0.9f, 0.5f));
                    break;
                case GestureShape.Zigzag:
                    foreach (var v in new[] { new Vector2(-0.9f, -0.5f), new Vector2(-0.45f, 0.5f), new Vector2(0f, -0.5f), new Vector2(0.45f, 0.5f), new Vector2(0.9f, -0.5f) })
                        l.Add(v);
                    break;
                case GestureShape.Triangle:
                    l.Add(new Vector2(0f, 0.85f));
                    l.Add(new Vector2(0.9f, -0.65f));
                    l.Add(new Vector2(-0.9f, -0.65f));
                    l.Add(new Vector2(0f, 0.85f));
                    break;
                case GestureShape.Circle:
                    for (int i = 0; i <= 40; i++)
                    {
                        float a = i / 40f * Mathf.PI * 2f;
                        l.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.85f);
                    }
                    break;
                default: // pentagrama: vertices 0,2,4,1,3,0 de un pentagono
                    foreach (int k in new[] { 0, 2, 4, 1, 3, 0 })
                    {
                        float a = Mathf.PI * 0.5f + k * Mathf.PI * 2f / 5f;
                        l.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.95f);
                    }
                    break;
            }
            return l;
        }
    }
}
