using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antopia
{
    // Niebla de las zonas norte y este: una nube por casilla de 4 m que se deshace al acercarse alguien. Las casillas
    // destapadas se guardan en SaveData.revealed. La zona del nido nunca tiene niebla.
    public class FogGrid
    {
        public const float CellSize = 4f;
        public const int Cols = 11, Rows = 11;
        static readonly Vector2 Origin = new Vector2(-WorldLayout.Half, -WorldLayout.Half);

        class Cloud
        {
            public GameObject go;
            public float t = -1f; // >= 0 mientras se deshace
        }

        readonly Cloud[] _clouds = new Cloud[Cols * Rows];

        // Casillas que empiezan con niebla (las de las zonas norte y este).
        public static readonly int TotalFogged = CountFogged();

        static int CountFogged()
        {
            int n = 0;
            for (int i = 0; i < Cols * Rows; i++)
                if (WorldLayout.ZoneOf(Center(i)) >= 1) n++;
            return n;
        }

        public FogGrid(Transform parent)
        {
            var mat = AntModel.Tint(new Color(0.97f, 0.98f, 1f), 0.15f);
            var revealed = new HashSet<int>(Game.Data.revealed);
            var rng = new System.Random(3);
            for (int i = 0; i < _clouds.Length; i++)
            {
                var c = Center(i);
                if (WorldLayout.ZoneOf(c) < 1 || revealed.Contains(i)) continue; // zona del nido, fuera del mapa o ya destapada

                var list = new List<CombineInstance>();
                void Puff(float x, float y, float z, float sx, float sy)
                {
                    list.Add(new CombineInstance { mesh = WorldDecor.SphereMesh, transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.identity, new Vector3(sx, sy, sx)) });
                }
                Puff(0f, 1.4f, 0f, 3.6f, 2.3f);
                for (int k = 0; k < 4; k++)
                {
                    float ox = (k % 2 == 0 ? -1f : 1f) * 1.35f + (float)(rng.NextDouble() - 0.5) * 0.5f;
                    float oz = (k < 2 ? -1f : 1f) * 1.35f + (float)(rng.NextDouble() - 0.5) * 0.5f;
                    Puff(ox, 1.1f + (float)rng.NextDouble() * 0.4f, oz, 2.5f + (float)rng.NextDouble() * 0.6f, 1.8f);
                }
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(list.ToArray(), true, true);
                var go = new GameObject("Cloud" + i);
                go.transform.SetParent(parent, false);
                go.transform.position = c;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                _clouds[i] = new Cloud { go = go };
            }
        }

        public static Vector3 Center(int index)
        {
            int col = index % Cols, row = index / Cols;
            return new Vector3(Origin.x + (col + 0.5f) * CellSize, 0f, Origin.y + (row + 0.5f) * CellSize);
        }

        public static int IndexOf(Vector3 p)
        {
            int col = Mathf.FloorToInt((p.x - Origin.x) / CellSize);
            int row = Mathf.FloorToInt((p.z - Origin.y) / CellSize);
            if (col < 0 || col >= Cols || row < 0 || row >= Rows) return -1;
            return row * Cols + col;
        }

        // Una casilla esta despejada si nunca tuvo nube o ya se destapo.
        public bool IsClear(Vector3 p)
        {
            int i = IndexOf(p);
            return i < 0 || _clouds[i] == null;
        }

        // Casilla con nube que aun no se esta deshaciendo.
        public bool IsFogged(int i) => i >= 0 && i < _clouds.Length && _clouds[i] != null && _clouds[i].t < 0f;

        // La casilla con nube mas cercana a "from" que cumpla "allow" (o -1).
        public int NearestFogged(Vector3 from, System.Func<int, bool> allow)
        {
            int best = -1;
            float bd = float.MaxValue;
            for (int i = 0; i < _clouds.Length; i++)
            {
                if (!IsFogged(i) || !allow(i)) continue;
                var d = Center(i) - from;
                d.y = 0f;
                float m = d.sqrMagnitude;
                if (m < bd) { bd = m; best = i; }
            }
            return best;
        }

        // Deshace las nubes a menos de "radius" de pos y devuelve las casillas destapadas ahora.
        public List<int> Reveal(Vector3 pos, float radius)
        {
            List<int> opened = null;
            int center = IndexOf(pos);
            if (center < 0) return null;
            int span = Mathf.CeilToInt(radius / CellSize) + 1;
            int cc = center % Cols, cr = center / Cols;
            for (int r = cr - span; r <= cr + span; r++)
            {
                for (int c = cc - span; c <= cc + span; c++)
                {
                    if (c < 0 || c >= Cols || r < 0 || r >= Rows) continue;
                    int i = r * Cols + c;
                    var cloud = _clouds[i];
                    if (cloud == null || cloud.t >= 0f) continue;
                    if (Vector3.Distance(Center(i), pos) > radius) continue;
                    cloud.t = 0f;
                    Game.Data.revealed.Add(i);
                    (opened ?? (opened = new List<int>())).Add(i);
                }
            }
            return opened;
        }

        public void Update(float dt)
        {
            for (int i = 0; i < _clouds.Length; i++)
            {
                var cloud = _clouds[i];
                if (cloud == null || cloud.t < 0f) continue;
                cloud.t += dt / 0.7f;
                float s = Mathf.Clamp01(1f - cloud.t);
                cloud.go.transform.localScale = new Vector3(s, s * 0.6f, s);
                if (cloud.t >= 1f)
                {
                    Object.Destroy(cloud.go);
                    _clouds[i] = null;
                }
            }
        }
    }
}
