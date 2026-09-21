using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antopia
{
    // Suelo con textura procedural y vegetacion decorativa del mundo del nido. Todo lo decorativo se combina en
    // pocas mallas (una por color) para que sea barato en movil. La zona de juego queda limpia (solo flores
    // y piedras); la hierba alta y los arbustos rodean el borde para cerrar el mundo.
    public static class WorldDecor
    {
        public const float GroundSize = 96f;
        const float TileWorldSize = 6f; // metros que cubre una repeticion de la textura

        static Mesh _cube, _sphere, _cylinder;

        // ---------------- Materiales y piezas ----------------
        public static Material GroundMaterial(float planeSize = GroundSize)
        {
            var baseMat = Resources.Load<Material>("Materials/Ground");
            var m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", MakeGroundTexture());
            m.SetTextureScale("_BaseMap", Vector2.one * (planeSize / TileWorldSize));
            m.SetFloat("_Smoothness", 0f);
            return m;
        }

        // Hierba (cuatro hojas) o rama que se puede recoger. Se crea en el origen del objeto devuelto.
        public static GameObject CreateItem(bool twig, Transform parent)
        {
            var go = new GameObject(twig ? "Twig" : "Grass");
            go.transform.SetParent(parent, false);
            if (twig)
            {
                Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(1.0f, 0.1f, 0.14f), "Twig");
                var b = Part(go.transform, PrimitiveType.Cube, new Vector3(0.1f, 0.11f, 0.05f), new Vector3(0.7f, 0.09f, 0.12f), "Twig");
                b.localRotation = Quaternion.Euler(0f, 35f, 0f);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    var blade = Part(go.transform, PrimitiveType.Cube, new Vector3((i - 1.5f) * 0.13f, 0.28f, (i % 2) * 0.1f),
                        new Vector3(0.09f, 0.55f, 0.06f), "Leaf");
                    blade.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), 0f, Random.Range(-18f, 18f));
                }
            }
            return go;
        }

        static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, string mat)
        {
            var t = NestView.Prim(type, "Part", Vector3.zero, scale, mat).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            return t;
        }

        // ---------------- Escenario ----------------
        public static void Build(Transform parent)
        {
            var root = new GameObject("Decor").transform;
            root.SetParent(parent, false);
            _cube = PrimMesh(PrimitiveType.Cube);
            _sphere = PrimMesh(PrimitiveType.Sphere);
            _cylinder = PrimMesh(PrimitiveType.Cylinder);

            var ground = NestView.Prim(PrimitiveType.Plane, "Ground", Vector3.zero, Vector3.one * (GroundSize / 10f), "Ground");
            ground.transform.SetParent(root, false);
            ground.GetComponent<MeshRenderer>().sharedMaterial = GroundMaterial();

            var rng = new System.Random(21);
            var batches = new Dictionary<Color, Batch>();
            Batch B(Color c)
            {
                if (!batches.TryGetValue(c, out var b)) batches[c] = b = new Batch(AntModel.Tint(c));
                return b;
            }
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 Spot(float half) => new Vector3(R(-half, half), 0f, R(-half, half));

            // Piedras pequenas.
            Color[] stone = { new Color(0.63f, 0.61f, 0.56f), new Color(0.50f, 0.48f, 0.45f) };
            for (int i = 0; i < 34; i++)
            {
                var p = Spot(12.5f);
                if (!AntRunner.IsClear(p, 0.8f)) continue;
                float s = R(0.25f, 0.55f);
                B(stone[i % 2]).Add(_sphere, p + Vector3.up * 0.05f, Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(s, s * 0.5f, s * R(0.7f, 1f)));
            }

            // Flores en grupos de uno a tres.
            Color[] petals =
            {
                new Color(0.98f, 0.97f, 0.90f), new Color(0.98f, 0.85f, 0.25f),
                new Color(0.95f, 0.55f, 0.70f), new Color(0.72f, 0.58f, 0.92f),
            };
            var stem = new Color(0.30f, 0.55f, 0.22f);
            for (int i = 0; i < 28; i++)
            {
                var p = Spot(12.5f);
                if (!AntRunner.IsClear(p, 1.0f)) continue;
                var petal = petals[rng.Next(petals.Length)];
                int n = rng.Next(1, 4);
                for (int k = 0; k < n; k++)
                {
                    var q = p + new Vector3(R(-0.35f, 0.35f), 0f, R(-0.35f, 0.35f));
                    float h = R(0.22f, 0.38f);
                    B(stem).Add(_cube, q + Vector3.up * (h * 0.5f), Quaternion.identity, new Vector3(0.035f, h, 0.035f));
                    B(petal).Add(_sphere, q + Vector3.up * h, Quaternion.identity, Vector3.one * R(0.15f, 0.22f));
                }
            }

            // Hierba alta fuera de la zona de juego.
            Color[] tall = { new Color(0.28f, 0.58f, 0.22f), new Color(0.36f, 0.66f, 0.26f), new Color(0.24f, 0.50f, 0.20f) };
            for (int i = 0; i < 300; i++)
            {
                var p = Spot(23f);
                if (Mathf.Abs(p.x) < 14f && Mathf.Abs(p.z) < 14f) continue;
                for (int k = 0; k < 4; k++)
                {
                    float h = R(0.5f, 1.1f);
                    var q = p + new Vector3(R(-0.25f, 0.25f), 0f, R(-0.25f, 0.25f));
                    B(tall[rng.Next(tall.Length)]).Add(_cube, q + Vector3.up * (h * 0.5f),
                        Quaternion.Euler(R(-14f, 14f), R(0f, 180f), R(-14f, 14f)), new Vector3(0.09f, h, 0.05f));
                }
            }

            // Arbustos en anillo cerrando el mundo.
            Color[] bush = { new Color(0.20f, 0.42f, 0.19f), new Color(0.27f, 0.51f, 0.22f), new Color(0.17f, 0.37f, 0.18f) };
            for (int i = 0; i < 52; i++)
            {
                float ang = (i + R(-0.3f, 0.3f)) / 52f * Mathf.PI * 2f;
                float rad = R(19f, 26f);
                float s = R(2.4f, 4.6f);
                B(bush[rng.Next(bush.Length)]).Add(_sphere, new Vector3(Mathf.Cos(ang) * rad, s * 0.28f, Mathf.Sin(ang) * rad),
                    Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(s, s * 0.8f, s));
            }

            // Rocas grandes entre los arbustos.
            for (int i = 0; i < 8; i++)
            {
                float ang = R(0f, Mathf.PI * 2f);
                float rad = R(17f, 21f);
                float s = R(1.2f, 2.0f);
                B(stone[i % 2]).Add(_sphere, new Vector3(Mathf.Cos(ang) * rad, s * 0.2f, Mathf.Sin(ang) * rad),
                    Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(s, s * 0.55f, s * 0.8f));
            }

            int idx = 0;
            foreach (var b in batches.Values) b.Flush(root, "Decor_" + idx++);
        }

        // ---------------- Utilidades ----------------
        static Mesh PrimMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return mesh;
        }

        class Batch
        {
            readonly Material _mat;
            readonly List<CombineInstance> _list = new List<CombineInstance>();

            public Batch(Material mat)
            {
                _mat = mat;
            }

            public void Add(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale)
            {
                _list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale) });
            }

            public void Flush(Transform parent, string name)
            {
                if (_list.Count == 0) return;
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(_list.ToArray(), true, true);
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ---------------- Textura del suelo ----------------
        static Texture2D MakeGroundTexture()
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };
            var px = new Color32[N * N];
            var light = new Color(0.64f, 0.84f, 0.36f);
            var mid = new Color(0.48f, 0.73f, 0.31f);
            var dark = new Color(0.36f, 0.60f, 0.28f);
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float t = Tile(u, v, 3f, 0f) * 0.5f + Tile(u, v, 9f, 17f) * 0.32f + Tile(u, v, 30f, 41f) * 0.18f;
                    t = Mathf.Clamp01(0.5f + (t - 0.5f) * 2.2f);
                    var c = t < 0.5f ? Color.Lerp(dark, mid, t * 2f) : Color.Lerp(mid, light, (t - 0.5f) * 2f);
                    float speck = Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);
                    c *= 0.95f + 0.10f * speck;
                    px[y * N + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        // Ruido Perlin repetible: mezcla cuatro muestras desplazadas un periodo para que los bordes encajen.
        static float Tile(float u, float v, float f, float off)
        {
            float x = u * f + off + 50f, y = v * f + off + 50f;
            float a = Mathf.PerlinNoise(x, y), b = Mathf.PerlinNoise(x - f, y);
            float c = Mathf.PerlinNoise(x, y - f), d = Mathf.PerlinNoise(x - f, y - f);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
