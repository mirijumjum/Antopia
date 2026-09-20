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

            var padImg = UiKit.Box(root, "SwipePad", new Color(0f, 0f, 0f, 0f), 0f, 0f, 1f, 0.915f);
            padImg.gameObject.AddComponent<SwipePad>().Swiped += dir =>
            {
                _run.Steer(dir);
                _hint.text = "Desliza para cambiar de direccion.";
            };

            UiKit.Box(root, "InfoBg", new Color(0.12f, 0.09f, 0.06f, 0.85f), 0f, 0.85f, 1f, 0.915f).raycastTarget = false;
            UiKit.Label(root, $"{r.Name}: modo de prueba", 40, TextAnchor.MiddleCenter, UiKit.Gold, 0.02f, 0.85f, 0.98f, 0.915f);

            UiKit.Box(root, "HintBg", new Color(0f, 0f, 0f, 0.5f), 0.02f, 0.02f, 0.70f, 0.10f).raycastTarget = false;
            _hint = UiKit.Label(root, "Este rol aun no tiene juego. Desliza el dedo para moverte.", 32, TextAnchor.MiddleCenter,
                UiKit.Cream, 0.03f, 0.02f, 0.69f, 0.10f);
            UiKit.MakeButton(root, "Salir", UiKit.Cream, 40, Finish, 0.73f, 0.02f, 0.98f, 0.10f);

            _world = new GameObject("RoamWorld").transform;
            _ant = AntModel.Create("RoamAnt", r.Variant, _world);
            _ant.localScale = Vector3.one * 0.9f;
            _ant.position = NestView.NestEntrance + new Vector3(0f, 0f, -(DeliverRadius + 0.2f));
            _run = new AntRunner(_ant);
            _chase = new ChaseCam();
        }

        void Update()
        {
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
