using System.Collections.Generic;
using UnityEngine;

namespace Antopia
{
    // Vista 3D low-poly del nido: edificios que crecen con el nivel y hormigas pasivas que van y vienen.
    public class NestView : MonoBehaviour
    {
        class Ant
        {
            public Transform t;
            public Transform load;
            public Vector3 target;
            public bool carrying;
        }

        static readonly Vector3 NestEntrance = new Vector3(0f, 0f, 0.5f);
        static readonly Vector3[] BuildingPos =
        {
            new Vector3(-3.4f, 0f, -1.6f),
            new Vector3(3.4f, 0f, -1.6f),
            new Vector3(0f, 0f, 3.9f),
        };
        static readonly string[] BuildingMats = { "Despensa", "Tuneles", "Camara" };

        readonly Transform[] _buildings = new Transform[3];
        readonly List<Ant> _ants = new List<Ant>();
        readonly List<Vector3> _spots = new List<Vector3>();
        const float AntSpeed = 1.7f;

        void Start()
        {
            SetupCamera();
            Prim(PrimitiveType.Plane, "Ground", new Vector3(0, 0, 0), new Vector3(3f, 1f, 3f), "Ground");
            Prim(PrimitiveType.Sphere, "Mound", new Vector3(0, 0, 0.5f), new Vector3(4.2f, 2.2f, 4.2f), "Mound");

            for (int i = 0; i < 3; i++)
            {
                var type = i == 0 ? PrimitiveType.Cube : PrimitiveType.Cylinder;
                _buildings[i] = Prim(type, Game.BuildingNames[i], BuildingPos[i], Vector3.one, BuildingMats[i]).transform;
            }

            var rng = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float rad = 5.5f + (float)rng.NextDouble() * 3.5f;
                var p = new Vector3(Mathf.Cos(ang) * rad * 0.9f, 0f, Mathf.Sin(ang) * rad * 0.7f + 0.5f);
                bool twig = i % 3 == 2;
                var spot = Prim(PrimitiveType.Cube, twig ? "TwigSpot" : "LeafSpot", p + Vector3.up * 0.1f,
                    twig ? new Vector3(0.9f, 0.15f, 0.2f) : new Vector3(0.6f, 0.1f, 0.6f), twig ? "Twig" : "Leaf");
                spot.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 180f, 0);
                _spots.Add(p);
            }

            Game.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            Game.Changed -= Refresh;
        }

        static void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(0f, 12f, -9.5f);
            cam.transform.LookAt(new Vector3(0f, 0f, 0.8f));
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.78f, 0.9f);
        }

        static GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, string mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            var m = Resources.Load<Material>("Materials/" + mat);
            if (m != null) mr.sharedMaterial = m;
            return go;
        }

        void Refresh()
        {
            if (Game.Data == null) return;
            for (int i = 0; i < 3; i++)
            {
                float g = 1f + 0.18f * Game.Data.buildingLevels[i];
                float h = i == 0 ? 1.2f * g : 0.6f * g;
                var b = _buildings[i];
                b.localScale = i == 0 ? new Vector3(1.7f, h, 1.7f) : new Vector3(1.6f, h, 1.6f);
                float lift = i == 0 ? h * 0.5f : h;
                b.position = BuildingPos[i] + Vector3.up * lift;
            }

            int want = Mathf.Min(Game.Ants, 8);
            while (_ants.Count < want) _ants.Add(CreateAnt());
        }

        Ant CreateAnt()
        {
            var body = Prim(PrimitiveType.Sphere, "Ant", NestEntrance, new Vector3(0.4f, 0.3f, 0.6f), "Ant");
            var load = Prim(PrimitiveType.Cube, "Load", Vector3.zero, new Vector3(0.35f, 0.06f, 0.35f), "Leaf");
            load.transform.SetParent(body.transform, false);
            load.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            load.transform.localScale = new Vector3(0.9f, 0.2f, 0.9f);
            load.SetActive(false);
            var ant = new Ant { t = body.transform, load = load.transform };
            PickSpot(ant);
            return ant;
        }

        void PickSpot(Ant a)
        {
            a.carrying = false;
            a.load.gameObject.SetActive(false);
            a.target = _spots[Random.Range(0, _spots.Count)] + Vector3.up * 0.15f;
        }

        void Update()
        {
            Game.Tick(Time.deltaTime);
            foreach (var a in _ants)
            {
                var pos = a.t.position;
                var goal = a.carrying ? NestEntrance + Vector3.up * 0.15f : a.target;
                var flat = new Vector3(goal.x - pos.x, 0f, goal.z - pos.z);
                if (flat.magnitude < 0.25f)
                {
                    if (a.carrying)
                    {
                        PickSpot(a);
                    }
                    else
                    {
                        a.carrying = true;
                        a.load.gameObject.SetActive(true);
                    }
                    continue;
                }
                var dir = flat.normalized;
                a.t.forward = Vector3.Lerp(a.t.forward, dir, 10f * Time.deltaTime);
                float bob = Mathf.Sin(Time.time * 12f + a.t.GetInstanceID()) * 0.03f;
                a.t.position = new Vector3(pos.x + dir.x * AntSpeed * Time.deltaTime, 0.15f + bob,
                    pos.z + dir.z * AntSpeed * Time.deltaTime);
            }
        }
    }
}
