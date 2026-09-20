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
            new Vector3(-1.8f, 0f, -1.5f), new Vector3(1.8f, 0f, -1.5f),
            new Vector3(-1.8f, 0f, 1.5f), new Vector3(1.8f, 0f, 1.5f),
        };
        // Zona de la pantalla (fracciones de este panel) donde se ve el escenario.
        const float X0 = 0.04f, Y0 = 0.42f, X1 = 0.96f, Y1 = 0.87f;
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

        public static RoleScreen Open(Transform parent, Action<int> onPlay, Action onClose)
        {
            var go = new GameObject("RoleScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0.915f); // deja visible el HUD de arriba
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var s = go.AddComponent<RoleScreen>();
            s.Build(rt, onPlay, onClose);
            return s;
        }

        void Build(RectTransform root, Action<int> onPlay, Action onClose)
        {
            _root = root;
            _onPlay = onPlay;
            _onClose = onClose;

            UiKit.Frame(root, "Bg", UiKit.Skin.Cyan, 0f, 0f, 1f, 1f);
            UiKit.Label(root, "HORMIGAS: elige rol", 56, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.92f, 0.95f, 0.99f);
            UiKit.Label(root, "Cada color es un rol distinto. Toca una hormiga para elegirla.", 32, TextAnchor.MiddleCenter,
                UiKit.Cream, 0.05f, 0.88f, 0.95f, 0.92f);

            BuildStage();
            var raw = UiKit.Rect(root, "Stage", X0, Y0, X1, Y1).gameObject.AddComponent<RawImage>();
            raw.texture = _rt;
            raw.raycastTarget = false;
            BuildCellButtons();

            var detail = UiKit.Box(root, "Detail", new Color(1f, 1f, 1f, 0.08f), 0.04f, 0.22f, 0.96f, 0.40f);
            _roleName = UiKit.Label(detail.transform, "", 48, TextAnchor.MiddleLeft, UiKit.Gold, 0.04f, 0.58f, 0.96f, 0.98f);
            _roleDesc = UiKit.Label(detail.transform, "", 32, TextAnchor.UpperLeft, UiKit.Cream, 0.04f, 0.04f, 0.96f, 0.60f);

            _play = UiKit.MakeButton(root, "", UiKit.Gold, 42, () =>
            {
                int role = _selected;
                _onClose?.Invoke();
                _onPlay?.Invoke(role);
                Destroy(gameObject);
            }, 0.04f, 0.06f, 0.64f, 0.20f);
            UiKit.MakeButton(root, "Volver", UiKit.Cream, 46, () =>
            {
                _onClose?.Invoke();
                Destroy(gameObject);
            }, 0.68f, 0.06f, 0.96f, 0.20f);

            Select(Mathf.Clamp(Game.Data.role, 0, AntRoles.All.Length - 1));
            _ring.position = Origin + Cells[_selected];
            _ants[_selected].localScale = Vector3.one * SelectedScale;
        }

        void BuildStage()
        {
            _stage = new GameObject("RoleStage").transform;
            _stage.position = Origin;

            var floor = NestView.Prim(PrimitiveType.Cylinder, "Floor", Origin + new Vector3(0f, -0.03f, 0f), new Vector3(9f, 0.05f, 9f), "Ground");
            floor.transform.SetParent(_stage, true);
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
            camGo.transform.localPosition = new Vector3(0f, 6f, -8f);
            _cam = camGo.AddComponent<Camera>();
            _cam.targetTexture = _rt;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.22f, 0.17f, 0.12f);
            _cam.fieldOfView = 40f;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 60f;
            _cam.transform.LookAt(Origin + new Vector3(0f, 0.2f, 0f));
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
                UiKit.Box(_root, "NameBg", new Color(0f, 0f, 0f, 0.5f), c.x - halfW * 0.9f, c.y - halfH, c.x + halfW * 0.9f, c.y - halfH + 0.04f)
                    .raycastTarget = false;
                UiKit.Label(_root, AntRoles.All[i].Name, 32, TextAnchor.MiddleCenter, UiKit.Cream,
                    c.x - halfW * 0.9f, c.y - halfH, c.x + halfW * 0.9f, c.y - halfH + 0.04f);
            }
        }

        void Select(int i)
        {
            _selected = i;
            Game.SetRole(i);
            var r = AntRoles.All[i];
            _roleName.text = r.Name;
            _roleDesc.text = r.Description;
            UiKit.SetButtonText(_play, r.HasGame ? $"Jugar: {r.Verb}" : "Probar (en desarrollo)");
            UiKit.SetButtonColor(_play, r.Ui);
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
