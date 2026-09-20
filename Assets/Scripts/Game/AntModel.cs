using System.Collections.Generic;
using UnityEngine;

namespace Antopia
{
    // Modelo 3D de hormiga hecho con primitivas, copiado de la hoja de sprites Ants.png:
    // cabeza pequena con antenas acodadas, torax, cintura fina, abdomen grande y seis patas articuladas.
    // Mira hacia +Z y apoya las patas en y = 0. Las patas las anima AntRig.
    public static class AntModel
    {
        public const int Brown = 0, Red = 1, Green = 2, Black = 3;

        // Colores tomados de las cuatro variantes de la imagen.
        static readonly Color[] Palette =
        {
            new Color(0.55f, 0.34f, 0.27f),
            new Color(0.62f, 0.17f, 0.10f),
            new Color(0.36f, 0.42f, 0.07f),
            new Color(0.24f, 0.24f, 0.27f),
        };

        // Donde apoyar la carga (encima del torax) y su tamano.
        public static readonly Vector3 CargoBase = new Vector3(0f, 0.5f, -0.05f);

        static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();

        public static Transform Create(string name, int variant, Transform parent)
        {
            var color = Palette[variant % Palette.Length];
            var body = Tint(color);
            var head = Tint(color * 0.85f);
            var dark = Tint(new Color(0.10f, 0.08f, 0.07f));

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);

            Part(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.30f, 0.46f), new Vector3(0.26f, 0.24f, 0.28f), head);
            Part(root, PrimitiveType.Sphere, "Thorax", new Vector3(0f, 0.29f, 0.12f), new Vector3(0.28f, 0.26f, 0.40f), body);
            Part(root, PrimitiveType.Sphere, "Waist", new Vector3(0f, 0.30f, -0.12f), new Vector3(0.12f, 0.12f, 0.14f), dark);
            Part(root, PrimitiveType.Sphere, "Abdomen", new Vector3(0f, 0.31f, -0.42f), new Vector3(0.40f, 0.36f, 0.56f), body);

            foreach (float side in new[] { -1f, 1f })
            {
                // Antenas acodadas y mandibulas.
                Bar(root, new Vector3(side * 0.05f, 0.38f, 0.56f), new Vector3(side * 0.10f, 0.52f, 0.72f), 0.03f, dark);
                Bar(root, new Vector3(side * 0.10f, 0.52f, 0.72f), new Vector3(side * 0.20f, 0.48f, 0.90f), 0.03f, dark);
                Bar(root, new Vector3(side * 0.06f, 0.26f, 0.58f), new Vector3(side * 0.02f, 0.24f, 0.70f), 0.04f, dark);
            }

            // Seis patas: delantera, media y trasera a cada lado, con la marcha de trinca (alternando tres y tres).
            var pivots = new Transform[6];
            var sides = new float[6];
            var yaws = new float[6];
            var phases = new float[6];
            float[] zs = { 0.28f, 0.12f, -0.04f };
            float[] splay = { 35f, 0f, -35f };
            int n = 0;
            for (int row = 0; row < 3; row++)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var pivot = new GameObject("Leg").transform;
                    pivot.SetParent(root, false);
                    pivot.localPosition = new Vector3(side * 0.08f, 0.22f, zs[row]);
                    var knee = new Vector3(side * 0.20f, 0.14f, 0f);
                    Bar(pivot, Vector3.zero, knee, 0.045f, dark);
                    Bar(pivot, knee, new Vector3(side * 0.38f, -0.20f, 0f), 0.04f, dark);
                    pivots[n] = pivot;
                    sides[n] = side;
                    yaws[n] = -side * splay[row];
                    phases[n] = ((row % 2 == 0) == (side > 0f)) ? 0f : Mathf.PI;
                    pivot.localRotation = Quaternion.Euler(0f, yaws[n], 0f);
                    n++;
                }
            }
            root.gameObject.AddComponent<AntRig>().Init(pivots, sides, yaws, phases);
            return root;
        }

        // Material propio con el color pedido, copiando el shader del material Ant.
        static Material Tint(Color color)
        {
            if (Cache.TryGetValue(color, out var cached) && cached != null) return cached;
            var baseMat = Resources.Load<Material>("Materials/Ant");
            var m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            Cache[color] = m;
            return m;
        }

        static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = NestView.Prim(type, name, Vector3.zero, scale, "Ant").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return t;
        }

        // Barra fina entre dos puntos (locales al padre).
        static void Bar(Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            var d = b - a;
            var t = Part(parent, PrimitiveType.Cube, "Bar", (a + b) * 0.5f, new Vector3(thickness, thickness, d.magnitude), mat);
            t.localRotation = Quaternion.LookRotation(d);
        }
    }
}
