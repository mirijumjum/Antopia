using UnityEngine;

namespace Antopia
{
    // Anima las patas de una hormiga de AntModel segun lo rapido que se desplaza su transform.
    public class AntRig : MonoBehaviour
    {
        const float MaxSwing = 28f;
        const float Lift = 0.05f;

        Transform[] _legs;
        float[] _sides, _baseYaw, _phaseOffset, _hipY;
        public float ForcedSpeed = -1f; // >= 0: ignora el desplazamiento real (para escaparates)
        Vector3 _last;
        float _phase;

        float _bounceT = 1f;

        // Saltito de alegria del cuerpo (sin mover a la hormiga).
        public void Bounce() => _bounceT = 0f;

        public Transform Body { get; private set; }
        public Transform[] Legs => _legs;
        public float[] Sides => _sides;
        public float[] BaseYaw => _baseYaw;

        public void Init(Transform[] legs, float[] sides, float[] baseYaw, float[] phaseOffset, Transform body = null)
        {
            Body = body;
            _legs = legs;
            _sides = sides;
            _baseYaw = baseYaw;
            _phaseOffset = phaseOffset;
            _hipY = new float[legs.Length];
            for (int i = 0; i < legs.Length; i++) _hipY[i] = legs[i].localPosition.y;
            _last = transform.position;
        }

        void Update()
        {
            if (_legs == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_bounceT < 1f && Body != null)
            {
                _bounceT = Mathf.Min(1f, _bounceT + dt * 3.2f);
                Body.localPosition = _bounceT >= 1f ? Vector3.zero : new Vector3(0f, Mathf.Sin(_bounceT * Mathf.PI) * 0.3f, 0f);
            }
            var p = transform.position;
            float speed = ForcedSpeed >= 0f ? ForcedSpeed : Mathf.Min(Vector3.Distance(p, _last) / dt / transform.lossyScale.x, 12f);
            _last = p;

            _phase += speed * 2.2f * dt;
            float amount = Mathf.Clamp01(speed / 1.5f);
            for (int i = 0; i < _legs.Length; i++)
            {
                float s = Mathf.Sin(_phase + _phaseOffset[i]);
                _legs[i].localRotation = Quaternion.Euler(0f, _baseYaw[i] - _sides[i] * MaxSwing * amount * s, 0f);
                var lp = _legs[i].localPosition;
                lp.y = _hipY[i] + Lift * amount * Mathf.Max(0f, Mathf.Cos(_phase + _phaseOffset[i]));
                _legs[i].localPosition = lp;
            }
        }
    }
}
