using UnityEngine;

namespace Antopia
{
    public enum DanceMove { Hop, Shimmy, Spin, Waltz, Star }

    // Baila una hormiga de AntModel con animacion procedural: salta, menea el cuerpo, gira, hace un vals o la gran
    // estrella. Mientras baila apaga la marcha de AntRig y mueve las patas delanteras como brazos; al acabar deja
    // todo como estaba.
    [RequireComponent(typeof(AntRig))]
    public class AntDancer : MonoBehaviour
    {
        public static DanceMove MoveFor(GestureShape shape)
        {
            switch (shape)
            {
                case GestureShape.Line: return DanceMove.Hop;
                case GestureShape.Zigzag: return DanceMove.Shimmy;
                case GestureShape.Triangle: return DanceMove.Spin;
                case GestureShape.Circle: return DanceMove.Waltz;
                default: return DanceMove.Star;
            }
        }

        public static float Duration(DanceMove m)
        {
            switch (m)
            {
                case DanceMove.Hop: return 1.0f;
                case DanceMove.Shimmy: return 1.2f;
                case DanceMove.Spin: return 1.1f;
                case DanceMove.Waltz: return 1.5f;
                default: return 1.7f;
            }
        }

        AntRig _rig;
        Vector3 _restPos;
        Quaternion _restRot;
        Vector3 _restScale;
        DanceMove _move;
        float _time = -1f, _delay, _idlePhase;

        public bool IsDancing => _time >= 0f;

        void Awake()
        {
            _rig = GetComponent<AntRig>();
            _restPos = transform.position;
            _restRot = transform.rotation;
            _restScale = transform.localScale;
            _idlePhase = Random.value * 6.28f;
        }

        // Punto y orientacion en los que descansa (por si el que la coloca la mueve despues).
        public void SetRest(Vector3 pos, Quaternion rot)
        {
            _restPos = pos;
            _restRot = rot;
            if (!IsDancing)
            {
                transform.position = pos;
                transform.rotation = rot;
            }
        }

        public void Play(DanceMove move, float delay = 0f)
        {
            _move = move;
            _delay = delay;
            _time = 0f;
            _rig.enabled = false;
        }

        void LateUpdate()
        {
            if (_time < 0f)
            {
                Idle();
                return;
            }
            _time += Time.deltaTime;
            float t = _time - _delay;
            float dur = Duration(_move);
            if (t < 0f) return;
            if (t >= dur)
            {
                Stop();
                return;
            }
            Apply(t / dur);
        }

        void Stop()
        {
            _time = -1f;
            transform.position = _restPos;
            transform.rotation = _restRot;
            transform.localScale = _restScale;
            var body = _rig.Body;
            if (body != null)
            {
                body.localPosition = Vector3.zero;
                body.localRotation = Quaternion.identity;
                body.localScale = Vector3.one;
            }
            for (int i = 0; i < _rig.Legs.Length; i++)
                _rig.Legs[i].localRotation = Quaternion.Euler(0f, _rig.BaseYaw[i], 0f);
            _rig.enabled = true;
        }

        // Respiracion suave cuando no baila.
        void Idle()
        {
            var body = _rig.Body;
            if (body == null) return;
            float s = 1f + 0.025f * Mathf.Sin(Time.time * 3f + _idlePhase);
            body.localScale = new Vector3(1f, s, 1f);
        }

        // u va de 0 a 1 durante el baile.
        void Apply(float u)
        {
            float t = _time - _delay;
            float y = 0f;
            float yaw = 0f;
            var offset = Vector3.zero;
            float bodyRoll = 0f;      // inclinacion de lado del cuerpo
            float arms = 50f;         // altura de los brazos (patas delanteras)
            float armWave = 0f;
            float squash = 1f;

            switch (_move)
            {
                case DanceMove.Hop:
                    y = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f)) * 0.6f;
                    squash = 1f + 0.12f * Mathf.Cos(u * Mathf.PI * 6f);
                    arms = 70f;
                    armWave = 25f * Mathf.Sin(t * 16f);
                    break;
                case DanceMove.Shimmy:
                    bodyRoll = 16f * Mathf.Sin(t * 22f);
                    offset.x = 0.14f * Mathf.Sin(t * 11f);
                    arms = 55f;
                    armWave = 30f * Mathf.Sin(t * 22f);
                    break;
                case DanceMove.Spin:
                    yaw = 360f * Mathf.SmoothStep(0f, 1f, u);
                    y = Mathf.Sin(u * Mathf.PI) * 0.5f;
                    arms = 80f;
                    break;
                case DanceMove.Waltz:
                    offset = new Vector3(Mathf.Sin(u * Mathf.PI * 2f), 0f, 1f - Mathf.Cos(u * Mathf.PI * 2f)) * 0.55f;
                    bodyRoll = 11f * Mathf.Sin(u * Mathf.PI * 4f);
                    arms = 60f;
                    armWave = 35f * Mathf.Sin(u * Mathf.PI * 4f);
                    break;
                default: // Star: gran salto girando con los brazos en alto
                    y = Mathf.Sin(u * Mathf.PI) * 1.4f;
                    yaw = 720f * Mathf.SmoothStep(0f, 1f, u);
                    arms = 85f;
                    squash = u < 0.12f || u > 0.88f ? 0.82f : 1.1f;
                    break;
            }

            transform.position = _restPos + _restRot * offset + Vector3.up * y;
            transform.rotation = _restRot * Quaternion.Euler(0f, yaw, 0f);

            var body = _rig.Body;
            if (body != null)
            {
                body.localRotation = Quaternion.Euler(0f, 0f, bodyRoll);
                body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
                body.localPosition = new Vector3(0f, 0.03f * Mathf.Sin(t * 30f), 0f);
            }

            // Patas delanteras (indices 0 y 1) como brazos; las demas se quedan plantadas con un pisoton.
            for (int i = 0; i < _rig.Legs.Length; i++)
            {
                float side = _rig.Sides[i];
                float lift;
                if (i < 2) lift = arms + armWave * (i == 0 ? 1f : -1f);
                else lift = 8f * Mathf.Sin(t * 12f + i);
                _rig.Legs[i].localRotation = Quaternion.Euler(0f, _rig.BaseYaw[i], side * lift);
            }
        }
    }
}
