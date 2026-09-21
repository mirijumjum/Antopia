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

        internal static readonly Vector3 NestEntrance = new Vector3(0f, 0f, 0.5f);
        internal static readonly Vector3[] BuildingPos =
        {
            new Vector3(-3.4f, 0f, -1.6f),
            new Vector3(3.4f, 0f, -1.6f),
            new Vector3(0f, 0f, 3.9f),
        };

        readonly Transform[] _buildings = new Transform[3];
        readonly List<Ant> _ants = new List<Ant>();
        readonly List<Vector3> _spots = new List<Vector3>();
        static readonly int[] PassiveVariants = { AntRoles.All[AntRoles.Obrera].Variant }; // las pasivas son obreras
        const float AntSpeed = 1.7f;

        void Start()
        {
            SetupCamera();
            WorldDecor.Build(transform);
            var ring = Prim(PrimitiveType.Sphere, "DirtRing", new Vector3(0f, 0f, 0.5f), new Vector3(7.2f, 0.06f, 7.2f), "Mound");
            ring.GetComponent<MeshRenderer>().sharedMaterial = AntModel.Tint(new Color(0.46f, 0.33f, 0.21f));
            Prim(PrimitiveType.Sphere, "Mound", new Vector3(0, 0, 0.5f), new Vector3(4.2f, 2.2f, 4.2f), "Mound");

            for (int i = 0; i < 3; i++) _buildings[i] = BuildBuilding(i);

            var rng = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float rad = 5.5f + (float)rng.NextDouble() * 3.5f;
                var p = new Vector3(Mathf.Cos(ang) * rad * 0.9f, 0f, Mathf.Sin(ang) * rad * 0.7f + 0.5f);
                bool twig = i % 3 == 2;
                var spot = WorldDecor.CreateItem(twig, transform);
                spot.transform.position = p;
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

        internal static GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, string mat)
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
                _buildings[i].localScale = Vector3.one * BuildingScale(Game.Data.buildingLevels[i]);

            int want = Mathf.Min(Game.Ants, 8);
            while (_ants.Count < want) _ants.Add(CreateAnt());
        }

        // Los edificios crecen con el nivel; AntRunner usa la misma escala para su colision.
        internal static float BuildingScale(int level) => 1f + 0.06f * level;

        static Transform Piece(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = Prim(type, "Part", Vector3.zero, scale, "Ant").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return t;
        }

        // Cada edificio mira hacia la camara (-Z) y esta hecho con primitivas.
        static Transform BuildBuilding(int i)
        {
            var root = new GameObject(Game.BuildingNames[i]).transform;
            root.position = BuildingPos[i];
            var dark = AntModel.Tint(new Color(0.17f, 0.12f, 0.09f));
            Material Res(string n) => Resources.Load<Material>("Materials/" + n);
            switch (i)
            {
                case 0: // Despensa: caja de madera con tapa y grano en la puerta
                    Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(1.7f, 1.1f, 1.7f), Res("Despensa"));
                    Piece(root, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(1.95f, 0.2f, 1.95f), Res("Twig"));
                    Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.38f, -0.86f), new Vector3(0.55f, 0.7f, 0.06f), dark);
                    Piece(root, PrimitiveType.Sphere, new Vector3(0.75f, 0.12f, -1.1f), new Vector3(0.3f, 0.2f, 0.3f), Res("Despensa"));
                    Piece(root, PrimitiveType.Sphere, new Vector3(0.45f, 0.1f, -1.25f), new Vector3(0.24f, 0.16f, 0.24f), Res("Despensa"));
                    break;
                case 1: // Tuneles: monton de piedras con la boca de un tunel
                    Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), new Vector3(2.2f, 1.3f, 2.0f), Res("Tuneles"));
                    Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.4f, -0.88f), new Vector3(0.8f, 0.6f, 0.3f), dark);
                    Piece(root, PrimitiveType.Sphere, new Vector3(-0.95f, 0.15f, -0.8f), new Vector3(0.5f, 0.32f, 0.45f), Res("Tuneles"));
                    Piece(root, PrimitiveType.Sphere, new Vector3(0.9f, 0.12f, -0.9f), new Vector3(0.38f, 0.26f, 0.36f), Res("Tuneles"));
                    break;
                default: // Camara real: cupula morada con corona dorada y puerta
                    Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0f), new Vector3(2.0f, 1.7f, 2.0f), Res("Camara"));
                    Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 1.3f, 0f), new Vector3(0.9f, 0.05f, 0.9f), Res("Despensa"));
                    Piece(root, PrimitiveType.Sphere, new Vector3(0f, 1.42f, 0f), new Vector3(0.32f, 0.32f, 0.32f), Res("Despensa"));
                    Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.38f, -0.95f), new Vector3(0.6f, 0.75f, 0.25f), dark);
                    break;
            }
            return root;
        }

        Ant CreateAnt()
        {
            var body = AntModel.Create("Ant", PassiveVariants[_ants.Count % PassiveVariants.Length], null);
            body.position = NestEntrance;
            body.localScale = Vector3.one * 0.6f;
            var load = Prim(PrimitiveType.Cube, "Load", Vector3.zero, Vector3.one, "Leaf").transform;
            load.SetParent(body, false);
            load.localPosition = AntModel.CargoBase;
            load.localScale = new Vector3(0.5f, 0.1f, 0.4f);
            load.gameObject.SetActive(false);
            var ant = new Ant { t = body, load = load };
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
                a.t.position = new Vector3(pos.x + dir.x * AntSpeed * Time.deltaTime, 0f,
                    pos.z + dir.z * AntSpeed * Time.deltaTime);
            }
        }
    }
}
