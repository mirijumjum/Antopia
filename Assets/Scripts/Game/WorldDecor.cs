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
        public const float GroundSize = 128f;
        const float TileWorldSize = 6f; // metros que cubre una repeticion de la textura

        static Mesh _cube, _sphere, _cylinder;
        static readonly Vector3 Center = new Vector3(8f, 0f, 8f); // centro de todo el mundo con sus tres zonas

        internal static Mesh SphereMesh => _sphere;

        // Tapon de piedras que cierra el hueco del tunel; se destruye cuando la constructora lo abre.
        public static Transform PlugRoot { get; private set; }

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

            var ground = NestView.Prim(PrimitiveType.Plane, "Ground", Center, Vector3.one * (GroundSize / 10f), "Ground");
            ground.transform.SetParent(root, false);
            ground.GetComponent<MeshRenderer>().sharedMaterial = GroundMaterial();

            var rng = new System.Random(21);
            var batches = new Dictionary<Color, Batch>();
            Batch B(Color col)
            {
                if (!batches.TryGetValue(col, out var bb)) batches[col] = bb = new Batch(AntModel.Tint(col));
                return bb;
            }
            float R(float a0, float b0) => a0 + (float)rng.NextDouble() * (b0 - a0);

            // Punto al azar dentro de alguna de las tres zonas jugables (repartido segun su superficie).
            Vector3 InWorld()
            {
                float total = 0f;
                foreach (var z in WorldLayout.Zones) total += z.width * z.height;
                float pick = R(0f, total);
                foreach (var z in WorldLayout.Zones)
                {
                    pick -= z.width * z.height;
                    if (pick <= 0f) return new Vector3(R(z.xMin + 1.5f, z.xMax - 1.5f), 0f, R(z.yMin + 1.5f, z.yMax - 1.5f));
                }
                return Vector3.zero;
            }

            // Piedras pequenas.
            Color[] stone = { new Color(0.63f, 0.61f, 0.56f), new Color(0.50f, 0.48f, 0.45f), new Color(0.72f, 0.68f, 0.62f) };
            for (int i = 0; i < 80; i++)
            {
                var p = InWorld();
                if (!AntRunner.IsClear(p, 0.8f)) continue;
                float s0 = R(0.25f, 0.55f);
                B(stone[i % 2]).Add(_sphere, p + Vector3.up * 0.05f, Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(s0, s0 * 0.5f, s0 * R(0.7f, 1f)));
            }

            // Flores en grupos de uno a tres.
            Color[] petals =
            {
                new Color(0.98f, 0.97f, 0.90f), new Color(0.98f, 0.85f, 0.25f),
                new Color(0.95f, 0.55f, 0.70f), new Color(0.72f, 0.58f, 0.92f),
            };
            var stem = new Color(0.30f, 0.55f, 0.22f);
            for (int i = 0; i < 70; i++)
            {
                var p = InWorld();
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

            // Hierba alta fuera de los muros (y en la esquina sin acceso, entre las dos zonas).
            Color[] tall = { new Color(0.28f, 0.58f, 0.22f), new Color(0.36f, 0.66f, 0.26f), new Color(0.24f, 0.50f, 0.20f) };
            for (int i = 0; i < 700; i++)
            {
                var p = Center + new Vector3(R(-56f, 56f), 0f, R(-56f, 56f));
                bool insideWalls = p.x > -15.5f && p.x < 31.5f && p.z > -15.5f && p.z < 31.5f;
                bool deadCorner = p.x > 16f && p.z > 16f && p.x < 29f && p.z < 29f;
                if (insideWalls && !deadCorner) continue;
                for (int k = 0; k < 4; k++)
                {
                    float h = R(0.5f, 1.1f);
                    var q = p + new Vector3(R(-0.25f, 0.25f), 0f, R(-0.25f, 0.25f));
                    B(tall[rng.Next(tall.Length)]).Add(_cube, q + Vector3.up * (h * 0.5f),
                        Quaternion.Euler(R(-14f, 14f), R(0f, 180f), R(-14f, 14f)), new Vector3(0.09f, h, 0.05f));
                }
            }

            // Arbustos: un anillo lejano cerrando el mundo y algunos en la esquina sin acceso.
            Color[] bush = { new Color(0.20f, 0.42f, 0.19f), new Color(0.27f, 0.51f, 0.22f), new Color(0.17f, 0.37f, 0.18f) };
            for (int i = 0; i < 110; i++)
            {
                float ang = (i + R(-0.3f, 0.3f)) / 110f * Mathf.PI * 2f;
                float rad = R(40f, 50f);
                float s1 = R(2.6f, 5f);
                B(bush[rng.Next(bush.Length)]).Add(_sphere, Center + new Vector3(Mathf.Cos(ang) * rad, s1 * 0.28f, Mathf.Sin(ang) * rad),
                    Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(s1, s1 * 0.8f, s1));
            }
            for (int i = 0; i < 9; i++)
            {
                float s1 = R(2.4f, 4f);
                B(bush[rng.Next(bush.Length)]).Add(_sphere, new Vector3(R(18f, 27f), s1 * 0.28f, R(18f, 27f)),
                    Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(s1, s1 * 0.8f, s1));
            }

            // Rocas grandes entre los arbustos.
            for (int i = 0; i < 12; i++)
            {
                float ang = R(0f, Mathf.PI * 2f);
                float rad = R(34f, 40f);
                float s1 = R(1.2f, 2.2f);
                B(stone[i % 2]).Add(_sphere, Center + new Vector3(Mathf.Cos(ang) * rad, s1 * 0.2f, Mathf.Sin(ang) * rad),
                    Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(s1, s1 * 0.55f, s1 * 0.8f));
            }

            // Muros de piedra entre las zonas.
            var walls = WorldLayout.BuildWalls(Game.Data.tunnelBuilt, out var plug);
            foreach (var w in walls)
            {
                if (w == plug) continue;
                bool alongX = w.width > w.height;
                float len = alongX ? w.width : w.height;
                int count = Mathf.Max(1, Mathf.RoundToInt(len / 1.4f));
                for (int k = 0; k < count; k++)
                {
                    float t = (k + 0.5f) / count;
                    var pos = alongX ? new Vector3(Mathf.Lerp(w.xMin, w.xMax, t), 0.42f, w.center.y) : new Vector3(w.center.x, 0.42f, Mathf.Lerp(w.yMin, w.yMax, t));
                    pos += new Vector3(R(-0.12f, 0.12f), R(-0.05f, 0.12f), R(-0.12f, 0.12f));
                    float sz = R(1.25f, 1.7f);
                    B(stone[rng.Next(stone.Length)]).Add(_sphere, pos, Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(sz, sz * R(0.6f, 0.85f), sz));
                }
            }

            int idx = 0;
            foreach (var b in batches.Values) b.Flush(root, "Decor_" + idx++);

            // Tapon del tunel: piedras sueltas para poder quitarlas al construirlo.
            PlugRoot = null;
            if (!Game.Data.tunnelBuilt)
            {
                PlugRoot = new GameObject("TunnelPlug").transform;
                PlugRoot.SetParent(root, false);
                for (int k = 0; k < 3; k++)
                {
                    var t = NestView.Prim(PrimitiveType.Sphere, "PlugStone", new Vector3(WorldLayout.Half, 0.6f, -1.1f + k * 1.1f), new Vector3(1.7f, 1.3f, 1.5f), "Ant").transform;
                    t.SetParent(PlugRoot, true);
                    t.GetComponent<MeshRenderer>().sharedMaterial = AntModel.Tint(stone[k % stone.Length]);
                }
            }
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
