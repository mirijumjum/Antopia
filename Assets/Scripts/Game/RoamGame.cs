using System;
using UnityEngine;
using UnityEngine.UI;
using static Antopia.AntRunner;

namespace Antopia
{
    // Modo de prueba para los roles que aun no tienen minijuego: la hormiga del rol recorre el mundo con deslizamientos.
    public class RoamGame : MonoBehaviour
    {
        Transform _world, _ant;
        AntRunner _run;
        VirtualJoystick _stick;
        ChaseCam _chase;
        Text _hint;
        Action _onClose;

        public static RoamGame Open(Transform parent, int role, Action onClose)
        {
            var go = new GameObject("RoamGame", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var g = go.AddComponent<RoamGame>();
            g.Build(rt, role, onClose);
            return g;
        }

        void Build(RectTransform root, int role, Action onClose)
        {
            _onClose = onClose;
            var r = AntRoles.All[role];

            var padImg = UiKit.Box(root, "Pad", new Color(0f, 0f, 0f, 0f), 0f, 0f, 1f, 0.915f);
            _stick = VirtualJoystick.Attach(padImg.gameObject);

            var hud = new MinigameHud(root, Finish);
            hud.Center.text = $"{r.Name} (prueba)";
            _hint = hud.Hint;
            _hint.text = "Este rol aun no tiene juego. Manten el dedo y arrastra para moverte.";

            _world = new GameObject("RoamWorld").transform;
            _ant = AntModel.Create("RoamAnt", r.Variant, _world);
            _ant.localScale = Vector3.one * 0.9f;
            _ant.position = NestView.NestEntrance + new Vector3(0f, 0f, -(DeliverRadius + 0.2f));
            _run = new AntRunner(_ant);
            _chase = new ChaseCam();
        }

        void Update()
        {
            _run.SetInput(_stick.Value);
            _run.Move(Time.deltaTime);
        }

        void LateUpdate()
        {
            _chase.Follow(_ant.position, Time.deltaTime);
        }

        void Finish()
        {
            _onClose?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_world != null) Destroy(_world.gameObject);
            _chase?.Restore();
        }
    }
}
