using UnityEngine;

namespace Antopia
{
    // Hace que la camara del nido siga a la hormiga y la devuelve a su sitio al terminar.
    public class ChaseCam
    {
        readonly Camera _cam;
        readonly Vector3 _home;
        readonly Quaternion _homeRot;

        public ChaseCam()
        {
            _cam = Camera.main;
            if (_cam == null) return;
            _home = _cam.transform.position;
            _homeRot = _cam.transform.rotation;
        }

        public void Follow(Vector3 pos, float dt)
        {
            if (_cam == null) return;
            float lim = AntRunner.WorldHalf - 4f;
            var target = new Vector3(Mathf.Clamp(pos.x, -lim, lim), 0f, Mathf.Clamp(pos.z, -lim, lim));
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, _home + target, 1f - Mathf.Exp(-6f * dt));
        }

        public void Restore()
        {
            if (_cam == null) return;
            _cam.transform.position = _home;
            _cam.transform.rotation = _homeRot;
        }
    }
}
