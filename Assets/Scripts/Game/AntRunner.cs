using UnityEngine;

namespace Antopia
{
    // Movimiento por deslizamiento de la hormiga jugable por el mundo del nido (compartido por los minijuegos).
    // Se mueve siempre hacia delante y gira hacia la ultima direccion deslizada; no atraviesa el nido ni los edificios.
    public class AntRunner
    {
        public const float WorldHalf = 13f;
        public const float AntRadius = 0.3f;
        public const float MoundRadius = 2.15f;
        public const float DeliverRadius = MoundRadius + AntRadius + 0.35f;
        public const float Speed = 4.2f;
        public const float TurnSpeed = 540f;
        public static readonly float[] BuildingRadius = { 1.15f, 0.95f, 0.95f };

        public readonly Transform Ant;
        public bool Moving { get; private set; }
        Vector3 _heading = Vector3.forward;

        readonly float _speed;

        public AntRunner(Transform ant, float speedMultiplier = 1f)
        {
            Ant = ant;
            _speed = Speed * speedMultiplier;
        }

        // dir es una direccion de pantalla (arriba = +Z del mundo, derecha = +X).
        public void Steer(Vector2 dir)
        {
            _heading = new Vector3(dir.x, 0f, dir.y).normalized;
            Moving = true;
        }

        public void Move(float dt)
        {
            if (!Moving) return;
            Ant.rotation = Quaternion.RotateTowards(Ant.rotation, Quaternion.LookRotation(_heading), TurnSpeed * dt);
            var pos = Ant.position + Ant.forward * _speed * dt;
            pos.x = Mathf.Clamp(pos.x, -WorldHalf, WorldHalf);
            pos.z = Mathf.Clamp(pos.z, -WorldHalf, WorldHalf);
            pos = PushOut(pos, NestView.NestEntrance, MoundRadius);
            for (int i = 0; i < BuildingRadius.Length; i++) pos = PushOut(pos, NestView.BuildingPos[i], BuildingRadius[i]);
            pos.y = 0f;
            Ant.position = pos;
        }

        public bool Near(Vector3 p, float radius) => Flat(p - Ant.position).magnitude <= radius;

        // Punto libre de nido y edificios con un margen extra (para colocar objetos).
        public static bool IsClear(Vector3 p, float margin)
        {
            if (Flat(p - NestView.NestEntrance).magnitude < MoundRadius + margin) return false;
            for (int i = 0; i < BuildingRadius.Length; i++)
                if (Flat(p - NestView.BuildingPos[i]).magnitude < BuildingRadius[i] + margin) return false;
            return true;
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static Vector3 PushOut(Vector3 pos, Vector3 center, float radius)
        {
            var d = Flat(pos - center);
            float min = radius + AntRadius;
            if (d.magnitude >= min) return pos;
            var dir = d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
            var p = center + dir * min;
            return new Vector3(p.x, pos.y, p.z);
        }
    }
}
