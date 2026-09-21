using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Antopia.EditorTools
{
    // Prueba el reconocedor de formas con trazos sinteticos (con ruido, giros, distintos puntos de partida y
    // sentido) y escribe la tabla de aciertos en el log.
    // -executeMethod Antopia.EditorTools.GestureTests.Run -batchmode -nographics -quit
    public static class GestureTests
    {
        [MenuItem("Antopia/Test Gestures")]
        public static void Run()
        {
            var rng = new System.Random(5);
            var sb = new StringBuilder("\n[GestureTests] resultados (500 intentos por forma)\n");
            int total = 0, ok = 0;
            foreach (GestureShape shape in System.Enum.GetValues(typeof(GestureShape)))
            {
                int hit = 0;
                var confusion = new Dictionary<string, int>();
                for (int i = 0; i < 500; i++)
                {
                    var stroke = Make(GestureRecognizer.Template(shape), shape != GestureShape.Line && shape != GestureShape.Zigzag, rng);
                    string got = GestureRecognizer.TryRecognize(stroke, out var r) ? r.ToString() : "nada";
                    if (got == shape.ToString()) hit++;
                    else confusion[got] = confusion.TryGetValue(got, out var c) ? c + 1 : 1;
                }
                total += 500;
                ok += hit;
                sb.Append($"  {shape,-9} {hit / 5f,5:0.0}%");
                foreach (var kv in confusion) sb.Append($"   {kv.Key}:{kv.Value}");
                sb.Append('\n');
            }

            // Formas que NO deben reconocerse como ninguna.
            int falsePositives = 0, negatives = 0;
            var neg = new Dictionary<string, int>();
            for (int i = 0; i < 500; i++)
            {
                foreach (var pair in Negatives(rng))
                {
                    negatives++;
                    if (GestureRecognizer.TryRecognize(pair.Value, out var r))
                    {
                        falsePositives++;
                        string key = pair.Key + "->" + r;
                        neg[key] = neg.TryGetValue(key, out var c) ? c + 1 : 1;
                    }
                }
            }
            sb.Append($"  negativos reconocidos por error: {falsePositives}/{negatives}");
            foreach (var kv in neg) sb.Append($"   {kv.Key}:{kv.Value}");
            sb.Append($"\n  TOTAL aciertos: {100f * ok / total:0.0}%\n");
            Debug.Log(sb.ToString());
        }

        // Trazo de una plantilla: tamano, giro y posicion al azar, ruido, punto de partida y sentido al azar.
        static List<Vector2> Make(List<Vector2> template, bool closed, System.Random rng)
        {
            float size = Range(rng, 180f, 420f);
            float angle = Range(rng, 0f, 360f);
            var center = new Vector2(Range(rng, 300f, 700f), Range(rng, 500f, 1200f));
            var dense = Densify(template, 9f / size);
            if (closed)
            {
                dense.RemoveAt(dense.Count - 1);
                int start = rng.Next(dense.Count);
                var rotated = new List<Vector2>();
                for (int i = 0; i < dense.Count + 6; i++) rotated.Add(dense[(start + i) % dense.Count]); // el dedo se pasa un poco del cierre
                dense = rotated;
            }
            if (rng.Next(2) == 0) dense.Reverse();
            float sigma = size * Range(rng, 0.005f, 0.025f);
            // Ruido correlado (el temblor de un dedo real es lento, no punto a punto).
            var result = new List<Vector2>();
            var noise = Vector2.zero;
            foreach (var p in dense)
            {
                noise = noise * 0.85f + new Vector2(Gauss(rng), Gauss(rng)) * 0.5f;
                result.Add(Rotate(p * size * 0.5f, angle) + center + noise * sigma);
            }
            return result;
        }

        static Dictionary<string, List<Vector2>> Negatives(System.Random rng)
        {
            var d = new Dictionary<string, List<Vector2>>();
            d["cuadrado"] = Make(new List<Vector2> { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1) }, true, rng);
            d["ele"] = Make(new List<Vector2> { new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) }, false, rng);
            d["garabato"] = Scribble(rng);
            d["punto"] = Dot(rng);
            d["pentagono"] = Make(Polygon(5), true, rng);
            return d;
        }

        static List<Vector2> Polygon(int n)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / n;
                l.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            return l;
        }

        static List<Vector2> Scribble(System.Random rng)
        {
            var l = new List<Vector2>();
            var p = new Vector2(500f, 800f);
            float a = Range(rng, 0f, 6f);
            for (int i = 0; i < 60; i++)
            {
                a += Range(rng, -1.6f, 1.6f);
                p += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 18f;
                l.Add(p);
            }
            return l;
        }

        static List<Vector2> Dot(System.Random rng)
        {
            var l = new List<Vector2>();
            for (int i = 0; i < 12; i++) l.Add(new Vector2(500f + Gauss(rng) * 5f, 800f + Gauss(rng) * 5f));
            return l;
        }

        // Puntos cada "step" (en unidades de plantilla) a lo largo de la polilinea.
        static List<Vector2> Densify(List<Vector2> poly, float step)
        {
            var l = new List<Vector2> { poly[0] };
            for (int i = 1; i < poly.Count; i++)
            {
                float d = Vector2.Distance(poly[i - 1], poly[i]);
                int n = Mathf.Max(1, Mathf.CeilToInt(d / step));
                for (int k = 1; k <= n; k++) l.Add(Vector2.Lerp(poly[i - 1], poly[i], k / (float)n));
            }
            return l;
        }

        static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
        }

        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static float Gauss(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
        }
    }
}
