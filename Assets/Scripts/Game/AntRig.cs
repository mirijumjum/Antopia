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
        Vector3 _last;
        float _phase;

        public void Init(Transform[] legs, float[] sides, float[] baseYaw, float[] phaseOffset)
        {
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
            var p = transform.position;
            float speed = Mathf.Min(Vector3.Distance(p, _last) / dt / transform.lossyScale.x, 12f);
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
