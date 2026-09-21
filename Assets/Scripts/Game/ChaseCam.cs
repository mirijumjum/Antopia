using UnityEngine;

namespace Antopia
{
    // Hace que la camara del nido siga a la hormiga y la devuelve a su sitio al terminar.
    public class ChaseCam
    {
        readonly Camera _cam;
        readonly Vector3 _home;
        readonly Quaternion _homeRot;
        readonly float _homeFov;
        const float PlayFov = 47f; // un poco mas cerca que en el nido para ver mejor a la hormiga

        public ChaseCam()
        {
            _cam = Camera.main;
            if (_cam == null) return;
            _home = _cam.transform.position;
            _homeRot = _cam.transform.rotation;
            _homeFov = _cam.fieldOfView;
        }

        public void Follow(Vector3 pos, float dt)
        {
            if (_cam == null) return;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, PlayFov, 1f - Mathf.Exp(-4f * dt));
            float lim = AntRunner.WorldHalf - 4f;
            var target = new Vector3(Mathf.Clamp(pos.x, -lim, lim), 0f, Mathf.Clamp(pos.z, -lim, lim));
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, _home + target, 1f - Mathf.Exp(-6f * dt));
        }

        public void Restore()
        {
            if (_cam == null) return;
            _cam.transform.position = _home;
            _cam.transform.rotation = _homeRot;
            _cam.fieldOfView = _homeFov;
        }
    }
}
