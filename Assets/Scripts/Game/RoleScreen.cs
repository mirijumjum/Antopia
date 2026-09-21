using System;
using UnityEngine;
using UnityEngine.UI;

namespace Antopia
{
    // Pantalla de eleccion de rol: las cuatro hormigas giran en 3D en un escenario aparte (se ve a traves de una
    // RenderTexture). Tocar una la elige (queda guardada) y "Jugar" arranca el minijuego de ese rol.
    public class RoleScreen : MonoBehaviour
    {
        // Lejos del nido para que la camara principal no lo vea.
        static readonly Vector3 Origin = new Vector3(400f, 0f, -400f);
        // Un hueco por rol: delante izquierda/derecha y detras izquierda/derecha.
        static readonly Vector3[] Cells =
        {
            new Vector3(-1.7f, 0f, -1.2f), new Vector3(1.7f, 0f, -1.2f),
            new Vector3(-1.7f, 0f, 1.5f), new Vector3(1.7f, 0f, 1.5f),
        };
        // Zona de la pantalla (fracciones de este panel) donde se ve el escenario.
        const float X0 = 0.05f, Y0 = 0.38f, X1 = 0.95f, Y1 = 0.885f;
        const float SelectedScale = 1.3f;

        RectTransform _root;
        Text _roleName, _roleDesc;
        Button _play;
        Transform _stage, _ring;
        Camera _cam;
        RenderTexture _rt;
        Transform[] _ants;
        int _selected;
        Action<int> _onPlay;
        Action _onClose;

        int _current = -1, _cost;

        // current/cost: si se pasa un rol actual, elegir otro cuesta "cost" monedas.
        public static RoleScreen Open(Transform parent, Action<int> onPlay, Action onClose, int current = -1, int cost = 0)
        {
            var go = new GameObject("RoleScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0.915f); // deja visible la barra de recursos
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var s = go.AddComponent<RoleScreen>();
            s._current = current;
            s._cost = cost;
            s.Build(rt, onPlay, onClose);
            return s;
        }

        void Build(RectTransform root, Action<int> onPlay, Action onClose)
        {
            _root = root;
            _onPlay = onPlay;
            _onClose = onClose;

            UiKit.Frame(root, "Bg", UiKit.Skin.Green, 0f, 0f, 1f, 1f);
            UiKit.Label(root, "Hormigas", 56, TextAnchor.MiddleCenter, UiKit.Cream, 0.14f, 0.895f, 0.86f, 0.98f).fontStyle = FontStyle.Bold;
            UiKit.IconButton(root, "Icon78", Close, 0.83f, 0.895f, 0.96f, 0.975f);

            BuildStage();
            var raw = UiKit.Rect(root, "Stage", X0, Y0, X1, Y1).gameObject.AddComponent<RawImage>();
            raw.texture = _rt;
            raw.raycastTarget = false;
            BuildCellButtons();

            var detail = UiKit.Pill(root, "Detail", 0.55f, 0.05f, 0.16f, 0.95f, 0.35f);
            _roleName = UiKit.Label(detail.transform, "", 44, TextAnchor.MiddleLeft, UiKit.Gold, 0.05f, 0.62f, 0.95f, 0.98f);
            _roleName.fontStyle = FontStyle.Bold;
            _roleDesc = UiKit.Label(detail.transform, "", 28, TextAnchor.UpperLeft, UiKit.Cream, 0.05f, 0.06f, 0.95f, 0.64f);

            _play = UiKit.MakeButton(root, "", UiKit.Leaf, 44, () =>
            {
                int role = _selected;
                _onClose?.Invoke();
                _onPlay?.Invoke(role);
                Destroy(gameObject);
            }, 0.06f, 0.03f, 0.94f, 0.13f, "Icon37");

            Select(Mathf.Clamp(_current >= 0 ? _current : Game.Data.role, 0, AntRoles.All.Length - 1));
            _ring.position = Origin + Cells[_selected];
            _ants[_selected].localScale = Vector3.one * SelectedScale;
        }

        void Close()
        {
            _onClose?.Invoke();
            Destroy(gameObject);
        }

        void BuildStage()
        {
            _stage = new GameObject("RoleStage").transform;
            _stage.position = Origin;

            // Suelo de hierba con la misma textura que el mundo.
            var floor = NestView.Prim(PrimitiveType.Plane, "Floor", Origin, Vector3.one * 4f, "Ground");
            floor.transform.SetParent(_stage, true);
            floor.GetComponent<MeshRenderer>().sharedMaterial = WorldDecor.GroundMaterial(40f);
            _ring = NestView.Prim(PrimitiveType.Cylinder, "Ring", Origin + Cells[0], new Vector3(2.4f, 0.02f, 2.4f), "Despensa").transform;
            _ring.SetParent(_stage, true);

            _ants = new Transform[AntRoles.All.Length];
            for (int i = 0; i < _ants.Length; i++)
            {
                var a = AntModel.Create("Ant_" + AntRoles.All[i].Name, AntRoles.All[i].Variant, _stage);
                a.position = Origin + Cells[i];
                a.rotation = Quaternion.Euler(0f, 30f * i, 0f);
                a.GetComponent<AntRig>().ForcedSpeed = 1.6f; // camina en el sitio mientras gira
                _ants[i] = a;
            }

            // Tamano de la textura segun el espacio real que ocupa en pantalla, para no deformarla.
            var sa = Screen.safeArea;
            float w = sa.width * (X1 - X0), h = sa.height * 0.915f * (Y1 - Y0);
            float k = Mathf.Min(1f, 1024f / Mathf.Max(w, h));
            _rt = new RenderTexture(Mathf.Max(64, Mathf.RoundToInt(w * k)), Mathf.Max(64, Mathf.RoundToInt(h * k)), 16);

            var camGo = new GameObject("RoleCam");
            camGo.transform.SetParent(_stage, false);
            camGo.transform.localPosition = new Vector3(0f, 5.4f, -6.8f);
            _cam = camGo.AddComponent<Camera>();
            _cam.targetTexture = _rt;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.55f, 0.78f, 0.9f);
            _cam.fieldOfView = 40f;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 80f;
            _cam.transform.LookAt(Origin + new Vector3(0f, 0.2f, 0.1f));
        }

        // Botones transparentes sobre cada hormiga, colocados proyectando su posicion 3D en la pantalla.
        void BuildCellButtons()
        {
            Vector2 P(int i)
            {
                var vp = _cam.WorldToViewportPoint(Origin + Cells[i] + Vector3.up * 0.4f);
                return new Vector2(X0 + vp.x * (X1 - X0), Y0 + vp.y * (Y1 - Y0));
            }
            float halfW = 0.5f * Mathf.Abs(P(1).x - P(0).x);
            float halfH = Mathf.Min(0.12f, 0.5f * Mathf.Abs(P(2).y - P(0).y));
            for (int i = 0; i < _ants.Length; i++)
            {
                int idx = i;
                var c = P(i);
                var img = UiKit.Box(_root, "Cell", new Color(0f, 0f, 0f, 0f), c.x - halfW, c.y - halfH, c.x + halfW, c.y + halfH);
                var btn = img.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => Select(idx));
                var name = UiKit.Outlined(UiKit.Label(_root, AntRoles.All[i].Name, 32, TextAnchor.MiddleCenter, UiKit.Cream,
                    c.x - halfW, c.y - halfH, c.x + halfW, c.y - halfH + 0.045f));
                name.fontStyle = FontStyle.Bold;
            }
        }

        void Select(int i)
        {
            _selected = i;
            var r = AntRoles.All[i];
            _roleName.text = r.Name;
            _roleDesc.text = r.Description;
            bool change = _current >= 0 && i != _current;
            bool affordable = !change || Game.Data.coins >= _cost;
            UiKit.SetButtonText(_play, !change ? (_current >= 0 ? $"Seguir como {r.Name}" : $"Jugar: {r.Verb}") : $"Cambiar a {r.Name} ({_cost} monedas)");
            UiKit.SetButtonColor(_play, affordable ? r.Ui : UiKit.Disabled);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _ants.Length; i++)
            {
                bool sel = i == _selected;
                _ants[i].localScale = Vector3.Lerp(_ants[i].localScale, Vector3.one * (sel ? SelectedScale : 1f), 1f - Mathf.Exp(-8f * dt));
                _ants[i].Rotate(0f, (sel ? 90f : 35f) * dt, 0f);
            }
            _ring.position = Vector3.Lerp(_ring.position, Origin + Cells[_selected] + Vector3.up * 0.01f, 1f - Mathf.Exp(-10f * dt));
        }

        void OnDestroy()
        {
            if (_stage != null) Destroy(_stage.gameObject);
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
        }
    }
}
