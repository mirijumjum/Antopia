using UnityEngine;

namespace Antopia
{
    // Escarabajo gruñon (enemigo de las soldado), hecho con primitivas: caparazon brillante con lunares, cabeza con
    // cejas enfadadas, cuernitos y patas cortas que marchan. Mira hacia +Z.
    public static class BeetleModel
    {
        public static Transform Create(string name, Transform parent, Color shellColor)
        {
            var shell = AntModel.Tint(shellColor, 0.75f);
            var dark = AntModel.Tint(shellColor * 0.55f, 0.5f);
            var spot = AntModel.Tint(new Color(1f, 0.95f, 0.85f), 0.4f);
            var ink = AntModel.Tint(new Color(0.08f, 0.06f, 0.09f), 0.6f);
            var white = AntModel.Tint(Color.white, 0.6f);
            var horn = AntModel.Tint(new Color(0.95f, 0.85f, 0.55f), 0.5f);

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);

            // Caparazon con raya central y lunares.
            AntModel.Part(body, PrimitiveType.Sphere, "Shell", new Vector3(0f, 0.62f, -0.10f), new Vector3(1.55f, 1.05f, 1.75f), shell);
            AntModel.Part(body, PrimitiveType.Cube, "Seam", new Vector3(0f, 1.15f, -0.10f), new Vector3(0.05f, 0.05f, 1.40f), dark);
            foreach (var p in new[] { new Vector3(-0.38f, 1.02f, -0.35f), new Vector3(0.38f, 1.02f, -0.35f), new Vector3(-0.30f, 1.05f, 0.2f), new Vector3(0.30f, 1.05f, 0.2f) })
                AntModel.Part(body, PrimitiveType.Sphere, "Spot", p, new Vector3(0.24f, 0.08f, 0.24f), spot);

            // Cabeza con cara de pocos amigos.
            AntModel.Part(body, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.50f, 0.98f), new Vector3(0.90f, 0.66f, 0.72f), dark);
            foreach (float side in new[] { -1f, 1f })
            {
                AntModel.Part(body, PrimitiveType.Sphere, "Eye", new Vector3(side * 0.22f, 0.62f, 1.28f), Vector3.one * 0.26f, white);
                AntModel.Part(body, PrimitiveType.Sphere, "Pupil", new Vector3(side * 0.20f, 0.60f, 1.40f), Vector3.one * 0.13f, ink);
                // Cejas: el extremo interior mas bajo, para que parezca enfadado.
                AntModel.Bar(body, new Vector3(side * 0.40f, 0.90f, 1.30f), new Vector3(side * 0.06f, 0.78f, 1.34f), 0.075f, ink);
                // Cuernitos.
                AntModel.Bar(body, new Vector3(side * 0.20f, 0.78f, 1.05f), new Vector3(side * 0.30f, 1.18f, 1.28f), 0.10f, horn);
                AntModel.Part(body, PrimitiveType.Sphere, "HornTip", new Vector3(side * 0.30f, 1.20f, 1.29f), Vector3.one * 0.13f, horn);
            }
            AntModel.Part(body, PrimitiveType.Sphere, "Mouth", new Vector3(0f, 0.34f, 1.32f), new Vector3(0.26f, 0.05f, 0.05f), ink);

            // Seis patitas que marchan.
            var pivots = new Transform[6];
            var sides = new float[6];
            var yaws = new float[6];
            var phases = new float[6];
            float[] zs = { 0.55f, 0.05f, -0.45f };
            int n = 0;
            for (int row = 0; row < 3; row++)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var pivot = new GameObject("Leg").transform;
                    pivot.SetParent(root, false);
                    pivot.localPosition = new Vector3(side * 0.45f, 0.28f, zs[row]);
                    var foot = new Vector3(side * 0.42f, -0.26f, 0f);
                    AntModel.Bar(pivot, Vector3.zero, foot, 0.11f, dark);
                    AntModel.Part(pivot, PrimitiveType.Sphere, "Foot", foot, Vector3.one * 0.16f, dark);
                    pivots[n] = pivot;
                    sides[n] = side;
                    yaws[n] = 0f;
                    phases[n] = ((row % 2 == 0) == (side > 0f)) ? 0f : Mathf.PI;
                    n++;
                }
            }
            var rig = root.gameObject.AddComponent<AntRig>();
            rig.Init(pivots, sides, yaws, phases, body);
            rig.ForcedSpeed = 1.5f; // marcha en el sitio; el movimiento lo pone quien lo maneja
            return root;
        }
    }
}
