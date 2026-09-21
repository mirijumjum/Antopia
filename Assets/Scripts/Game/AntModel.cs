using System.Collections.Generic;
using UnityEngine;

namespace Antopia
{
    // Hormiga 3D "cute" hecha con primitivas: cabeza grande y redonda con ojos brillantes, mofletes y sonrisa,
    // cuerpo pequeno con una franja clara, antenas con bolitas y patitas cortas. Cada rol lleva un accesorio
    // (brote, casco de obra, casco de soldado, sombrero de exploradora). Mira hacia +Z y apoya las patas en y = 0.
    // Las patas las anima AntRig.
    public static class AntModel
    {
        public const int Brown = 0, Red = 1, Green = 2, Black = 3;

        // Un color vivo por rol.
        static readonly Color[] Palette =
        {
            new Color(0.76f, 0.51f, 0.34f), // marron
            new Color(0.92f, 0.32f, 0.26f), // rojo
            new Color(0.60f, 0.80f, 0.30f), // verde
            new Color(0.36f, 0.38f, 0.50f), // pizarra
        };

        // Donde apoyar la carga (encima del abdomen).
        public static readonly Vector3 CargoBase = new Vector3(0f, 0.66f, -0.30f);

        static readonly Dictionary<(Color, float), Material> Cache = new Dictionary<(Color, float), Material>();

        public static Transform Create(string name, int variant, Transform parent)
        {
            variant = Mathf.Abs(variant) % Palette.Length;
            var color = Palette[variant];
            var body = Tint(color, 0.45f);
            var light = Tint(Color.Lerp(color, Color.white, 0.45f), 0.45f);
            var limb = Tint(color * 0.55f, 0.3f);
            var ink = Tint(new Color(0.10f, 0.08f, 0.10f), 0.6f);
            var white = Tint(new Color(1f, 1f, 1f), 0.6f);
            var blush = Tint(new Color(1f, 0.60f, 0.66f), 0.2f);

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var bodyT = new GameObject("Body").transform; // todo menos las patas: asi se puede animar aparte
            bodyT.SetParent(root, false);

            // Cuerpo: cabeza grande, torax pequeno y abdomen redondo con una franja clara.
            Part(bodyT, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.44f, 0.50f), new Vector3(0.70f, 0.64f, 0.66f), body);
            Part(bodyT, PrimitiveType.Sphere, "Thorax", new Vector3(0f, 0.30f, 0.04f), new Vector3(0.36f, 0.34f, 0.40f), body);
            Part(bodyT, PrimitiveType.Sphere, "Abdomen", new Vector3(0f, 0.36f, -0.40f), new Vector3(0.58f, 0.52f, 0.64f), body);
            Part(bodyT, PrimitiveType.Sphere, "Stripe", new Vector3(0f, 0.36f, -0.42f), new Vector3(0.60f, 0.14f, 0.66f), light);

            // Cara: ojos grandes con brillo, mofletes y sonrisa.
            foreach (float side in new[] { -1f, 1f })
            {
                Part(bodyT, PrimitiveType.Sphere, "Eye", new Vector3(side * 0.17f, 0.50f, 0.77f), Vector3.one * 0.24f, white);
                Part(bodyT, PrimitiveType.Sphere, "Pupil", new Vector3(side * 0.17f, 0.50f, 0.875f), Vector3.one * 0.13f, ink);
                Part(bodyT, PrimitiveType.Sphere, "Shine", new Vector3(side * 0.145f, 0.545f, 0.935f), Vector3.one * 0.05f, white);
                Part(bodyT, PrimitiveType.Sphere, "Cheek", new Vector3(side * 0.28f, 0.36f, 0.72f), new Vector3(0.11f, 0.07f, 0.05f), blush);

                // Antena corta con bolita en la punta.
                Bar(bodyT, new Vector3(side * 0.14f, 0.70f, 0.62f), new Vector3(side * 0.25f, 0.93f, 0.74f), 0.05f, limb);
                Part(bodyT, PrimitiveType.Sphere, "Bulb", new Vector3(side * 0.25f, 0.95f, 0.75f), Vector3.one * 0.12f, light);
            }
            Part(bodyT, PrimitiveType.Sphere, "Smile", new Vector3(0f, 0.34f, 0.815f), new Vector3(0.10f, 0.03f, 0.03f), ink);

            AddAccessory(bodyT, variant);

            // Seis patitas cortas y gorditas con marcha de trinca (alternando tres y tres).
            var pivots = new Transform[6];
            var sides = new float[6];
            var yaws = new float[6];
            var phases = new float[6];
            float[] zs = { 0.20f, 0.03f, -0.14f };
            float[] splay = { 30f, 0f, -30f };
            int n = 0;
            for (int row = 0; row < 3; row++)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var pivot = new GameObject("Leg").transform;
                    pivot.SetParent(root, false);
                    pivot.localPosition = new Vector3(side * 0.10f, 0.22f, zs[row]);
                    var knee = new Vector3(side * 0.15f, 0.08f, 0f);
                    var foot = new Vector3(side * 0.23f, -0.19f, 0f);
                    Bar(pivot, Vector3.zero, knee, 0.075f, limb);
                    Bar(pivot, knee, foot, 0.07f, limb);
                    Part(pivot, PrimitiveType.Sphere, "Foot", foot, Vector3.one * 0.11f, limb);
                    pivots[n] = pivot;
                    sides[n] = side;
                    yaws[n] = -side * splay[row];
                    phases[n] = ((row % 2 == 0) == (side > 0f)) ? 0f : Mathf.PI;
                    pivot.localRotation = Quaternion.Euler(0f, yaws[n], 0f);
                    n++;
                }
            }
            root.gameObject.AddComponent<AntRig>().Init(pivots, sides, yaws, phases, bodyT);
            return root;
        }

        // Un detalle por rol sobre la cabeza.
        static void AddAccessory(Transform root, int variant)
        {
            var top = new Vector3(0f, 0.72f, 0.46f);
            switch (variant)
            {
                case Red: // obrera: brote de dos hojitas
                {
                    var leaf = Tint(new Color(0.42f, 0.78f, 0.30f), 0.4f);
                    Bar(root, top, top + new Vector3(0f, 0.10f, -0.02f), 0.04f, leaf);
                    var l = Part(root, PrimitiveType.Sphere, "Leaf", top + new Vector3(-0.09f, 0.14f, -0.02f), new Vector3(0.20f, 0.04f, 0.10f), leaf);
                    l.localRotation = Quaternion.Euler(0f, 0f, 25f);
                    var r = Part(root, PrimitiveType.Sphere, "Leaf", top + new Vector3(0.09f, 0.14f, -0.02f), new Vector3(0.20f, 0.04f, 0.10f), leaf);
                    r.localRotation = Quaternion.Euler(0f, 0f, -25f);
                    break;
                }
                case Black: // constructora: casco de obra amarillo
                {
                    var hat = Tint(new Color(1f, 0.80f, 0.18f), 0.5f);
                    Part(root, PrimitiveType.Sphere, "Hat", top + new Vector3(0f, -0.02f, -0.02f), new Vector3(0.52f, 0.34f, 0.52f), hat);
                    Part(root, PrimitiveType.Cylinder, "Brim", top + new Vector3(0f, -0.10f, 0.02f), new Vector3(0.60f, 0.02f, 0.60f), hat);
                    Part(root, PrimitiveType.Cube, "Ridge", top + new Vector3(0f, 0.08f, -0.02f), new Vector3(0.05f, 0.05f, 0.40f), Tint(new Color(0.95f, 0.65f, 0.10f), 0.5f));
                    break;
                }
                case Brown: // soldado: casco de acero con penacho
                {
                    var steel = Tint(new Color(0.66f, 0.70f, 0.78f), 0.7f);
                    Part(root, PrimitiveType.Sphere, "Helmet", top + new Vector3(0f, -0.03f, -0.02f), new Vector3(0.54f, 0.38f, 0.54f), steel);
                    Part(root, PrimitiveType.Cylinder, "Rim", top + new Vector3(0f, -0.12f, 0.02f), new Vector3(0.58f, 0.03f, 0.58f), steel);
                    Part(root, PrimitiveType.Sphere, "Plume", top + new Vector3(0f, 0.16f, -0.10f), new Vector3(0.09f, 0.16f, 0.26f), Tint(new Color(0.90f, 0.25f, 0.25f), 0.4f));
                    break;
                }
                default: // exploradora: sombrero de ala ancha
                {
                    var straw = Tint(new Color(0.90f, 0.74f, 0.42f), 0.3f);
                    Part(root, PrimitiveType.Cylinder, "Brim", top + new Vector3(0f, -0.09f, 0f), new Vector3(0.86f, 0.02f, 0.86f), straw);
                    Part(root, PrimitiveType.Sphere, "Crown", top + new Vector3(0f, 0f, -0.02f), new Vector3(0.44f, 0.30f, 0.44f), straw);
                    Part(root, PrimitiveType.Cylinder, "Band", top + new Vector3(0f, -0.06f, -0.02f), new Vector3(0.46f, 0.03f, 0.46f), Tint(new Color(0.75f, 0.30f, 0.25f), 0.4f));
                    break;
                }
            }
        }

        // Material propio con el color pedido, copiando el shader del material Ant.
        internal static Material Tint(Color color, float smoothness = 0.1f)
        {
            var key = (color, smoothness);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var baseMat = Resources.Load<Material>("Materials/Ant");
            var m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            Cache[key] = m;
            return m;
        }

        internal static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = NestView.Prim(type, name, Vector3.zero, scale, "Ant").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return t;
        }

        // Barra fina entre dos puntos (locales al padre).
        internal static void Bar(Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            var d = b - a;
            var t = Part(parent, PrimitiveType.Cube, "Bar", (a + b) * 0.5f, new Vector3(thickness, thickness, d.magnitude), mat);
            t.localRotation = Quaternion.LookRotation(d);
        }
    }
}
