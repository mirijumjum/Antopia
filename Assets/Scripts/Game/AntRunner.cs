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
        static readonly float[] BaseBuildingRadius = { 1.15f, 1.1f, 1.05f };

        // Radio de colision del edificio i: crece con su nivel igual que el modelo.
        public static float BuildingRadius(int i) => BaseBuildingRadius[i] * NestView.BuildingScale(Game.Data.buildingLevels[i]);

        public readonly Transform Ant;
        public bool HasMoved { get; private set; }  // ya se ha movido alguna vez
        public bool IsMoving { get; private set; }  // se esta moviendo ahora mismo
        Vector2 _input;
        const float DeadZone = 0.15f;

        readonly float _speed;

        public AntRunner(Transform ant, float speedMultiplier = 1f)
        {
            Ant = ant;
            _speed = Speed * speedMultiplier;
        }

        // v es la entrada del joystick (x derecha = +X del mundo, y arriba = +Z), con longitud de 0 a 1.
        public void SetInput(Vector2 v) => _input = Vector2.ClampMagnitude(v, 1f);

        public void Move(float dt)
        {
            float mag = _input.magnitude;
            IsMoving = mag > DeadZone;
            if (!IsMoving) return;
            HasMoved = true;
            var heading = new Vector3(_input.x, 0f, _input.y);
            Ant.rotation = Quaternion.RotateTowards(Ant.rotation, Quaternion.LookRotation(heading), TurnSpeed * dt);
            float k = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(DeadZone, 1f, mag)); // mas empuje, mas rapido
            var pos = Ant.position + Ant.forward * _speed * k * dt;
            pos.x = Mathf.Clamp(pos.x, -WorldHalf, WorldHalf);
            pos.z = Mathf.Clamp(pos.z, -WorldHalf, WorldHalf);
            pos = PushOut(pos, NestView.NestEntrance, MoundRadius);
            for (int i = 0; i < BaseBuildingRadius.Length; i++) pos = PushOut(pos, NestView.BuildingPos[i], BuildingRadius(i));
            pos.y = 0f;
            Ant.position = pos;
        }

        public bool Near(Vector3 p, float radius) => Flat(p - Ant.position).magnitude <= radius;

        // Punto libre de nido y edificios con un margen extra (para colocar objetos).
        public static bool IsClear(Vector3 p, float margin)
        {
            if (Flat(p - NestView.NestEntrance).magnitude < MoundRadius + margin) return false;
            for (int i = 0; i < BaseBuildingRadius.Length; i++)
                if (Flat(p - NestView.BuildingPos[i]).magnitude < BuildingRadius(i) + margin) return false;
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
