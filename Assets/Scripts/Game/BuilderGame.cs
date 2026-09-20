using System;
using UnityEngine;
using UnityEngine.UI;
using static Antopia.AntRunner;
using Random = UnityEngine.Random;

namespace Antopia
{
    // Obra de la constructora: recoge una pieza en el mundo 3D y llevala al solar antes de que se acabe el tiempo.
    // Son BuildAttempts piezas; cada una que llega a tiempo suma a la torre. Se maneja con deslizamientos.
    public class BuilderGame : MonoBehaviour
    {
        const float PickupRadius = 0.8f;
        const float SiteRadius = 1.3f;
        const float BlockHeight = 0.32f;

        RectTransform _root;
        Text _timer, _info, _placedText, _msg, _hint;
        Transform _world, _ant, _piece, _carryView, _site;
        AntRunner _run;
        ChaseCam _chase;
        TargetPointer _pointer;
        Vector3 _sitePos, _piecePos;
        float _timeLeft, _msgUntil;
        int _lastSecond = -1;
        int _attempt, _hits;
        bool _running, _carrying;
        Action _onClose;

        int Pieces => _hits + (_hits == Game.BuildAttempts ? 1 : 0);

        public static BuilderGame Open(Transform parent, Action onClose)
        {
            var go = new GameObject("BuilderGame", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var g = go.AddComponent<BuilderGame>();
            g.Build(rt, onClose);
            return g;
        }

        void Build(RectTransform root, Action onClose)
        {
            _root = root;
            _onClose = onClose;

            var padImg = UiKit.Box(root, "SwipePad", new Color(0f, 0f, 0f, 0f), 0f, 0f, 1f, 0.915f);
            padImg.gameObject.AddComponent<SwipePad>().Swiped += OnSwipe;

            UiKit.Box(root, "InfoBg", new Color(0.12f, 0.09f, 0.06f, 0.85f), 0f, 0.85f, 1f, 0.915f).raycastTarget = false;
            _timer = UiKit.Label(root, "", 38, TextAnchor.MiddleCenter, UiKit.Cream, 0.01f, 0.85f, 0.25f, 0.915f);
            _info = UiKit.Label(root, "", 38, TextAnchor.MiddleCenter, UiKit.Gold, 0.25f, 0.85f, 0.60f, 0.915f);
            _placedText = UiKit.Label(root, "", 36, TextAnchor.MiddleCenter, UiKit.Cream, 0.60f, 0.85f, 0.99f, 0.915f);
            _msg = UiKit.Label(root, "", 46, TextAnchor.MiddleCenter, UiKit.Gold, 0.05f, 0.78f, 0.95f, 0.85f);

            UiKit.Box(root, "HintBg", new Color(0f, 0f, 0f, 0.5f), 0.02f, 0.02f, 0.70f, 0.10f).raycastTarget = false;
            _hint = UiKit.Label(root, "", 32, TextAnchor.MiddleCenter, UiKit.Cream, 0.03f, 0.02f, 0.69f, 0.10f);
            UiKit.MakeButton(root, "Salir", UiKit.Cream, 40, Finish, 0.73f, 0.02f, 0.98f, 0.10f);
            _pointer = new TargetPointer(root);

            BuildWorld();
            _running = true;
            StartAttempt();
        }

        // ---------------- Mundo ----------------
        void BuildWorld()
        {
            _world = new GameObject("BuilderWorld").transform;

            _ant = AntModel.Create("BuilderAnt", AntRoles.All[AntRoles.Constructora].Variant, _world);
            _ant.localScale = Vector3.one * 0.9f;
            _ant.position = NestView.NestEntrance + new Vector3(0f, 0f, -(DeliverRadius + 0.2f));
            _run = new AntRunner(_ant);
            _chase = new ChaseCam();

            _carryView = Part(_ant, PrimitiveType.Cube, "Carry", AntModel.CargoBase + Vector3.up * 0.08f,
                new Vector3(0.5f, 0.3f, 0.45f), "Tuneles");
            _carryView.gameObject.SetActive(false);

            _piece = Part(_world, PrimitiveType.Cube, "Piece", Vector3.zero, new Vector3(0.6f, 0.35f, 0.55f), "Tuneles");

            _sitePos = FindSpot(_ant.position, 4f, 4f, 8f, 2f);
            _site = new GameObject("Site").transform;
            _site.SetParent(_world, false);
            _site.position = _sitePos;
            Part(_site, PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.02f, 0f), new Vector3(2.4f, 0.02f, 2.4f), "Camara");
        }

        static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, string mat)
        {
            var t = NestView.Prim(type, name, Vector3.zero, scale, mat).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            return t;
        }

        // Punto libre a al menos minFrom de "from", entre minNest y maxNest del nido.
        static Vector3 FindSpot(Vector3 from, float minFrom, float minNest, float maxNest, float margin, Vector3? avoid = null, float minAvoid = 0f)
        {
            for (int i = 0; i < 60; i++)
            {
                var p = new Vector3(Random.Range(-WorldHalf + 2f, WorldHalf - 2f), 0f, Random.Range(-WorldHalf + 2f, WorldHalf - 2f));
                float nest = Flat(p - NestView.NestEntrance).magnitude;
                if (nest < minNest || nest > maxNest) continue;
                if (!IsClear(p, margin)) continue;
                if (Flat(p - from).magnitude < minFrom) continue;
                if (avoid.HasValue && Flat(p - avoid.Value).magnitude < minAvoid) continue;
                return p;
            }
            return new Vector3(6f, 0f, 6f);
        }

        // ---------------- Intentos ----------------
        void StartAttempt()
        {
            _carrying = false;
            _carryView.gameObject.SetActive(false);
            _piecePos = FindSpot(_ant.position, 5f, 3f, 11f, 1f, _sitePos, 5f);
            _piece.position = _piecePos + Vector3.up * 0.18f;
            _piece.gameObject.SetActive(true);

            float dist = Flat(_piecePos - _ant.position).magnitude + Flat(_sitePos - _piecePos).magnitude;
            float t = Game.BuildAttempts > 1 ? _attempt / (float)(Game.BuildAttempts - 1) : 0f;
            _timeLeft = dist / Speed * Mathf.Lerp(2.4f, 1.6f, t) + 2f;
            RefreshTimer();
            RefreshInfo();
            RefreshHint();
        }

        void PickUp()
        {
            _carrying = true;
            _piece.gameObject.SetActive(false);
            _carryView.gameObject.SetActive(true);
            RefreshHint();
        }

        void Place()
        {
            var block = Part(_site, PrimitiveType.Cube, "Block", new Vector3(0f, 0.02f + BlockHeight * (_hits + 0.5f), 0f),
                new Vector3(1.0f, BlockHeight, 1.0f), "Tuneles");
            block.localRotation = Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f);
            _hits++;
            Say("Pieza colocada");
            NextAttempt();
        }

        void Fail()
        {
            Say("Se acabo el tiempo: pieza perdida");
            NextAttempt();
        }

        void NextAttempt()
        {
            _attempt++;
            if (_attempt >= Game.BuildAttempts) EndRound();
            else StartAttempt();
        }

        void Say(string text)
        {
            _msg.text = text;
            _msgUntil = Time.time + 2f;
        }

        // ---------------- Bucle ----------------
        void OnSwipe(Vector2 dir)
        {
            if (!_running) return;
            bool wasMoving = _run.Moving;
            _run.Steer(dir);
            if (!wasMoving) RefreshHint();
        }

        void Update()
        {
            if (_running)
            {
                _run.Move(Time.deltaTime);
                if (_run.Moving) _timeLeft -= Time.deltaTime; // el reloj empieza con el primer deslizamiento
                if (!_carrying && _run.Near(_piecePos, PickupRadius)) PickUp();
                else if (_carrying && _run.Near(_sitePos, SiteRadius)) Place();
                else if (_timeLeft <= 0f) Fail();
                else if (Mathf.CeilToInt(_timeLeft) != _lastSecond) RefreshTimer();
            }
            if (_piece.gameObject.activeSelf)
            {
                _piece.position = _piecePos + Vector3.up * (0.22f + 0.06f * Mathf.Sin(Time.time * 4f));
                _piece.Rotate(0f, 60f * Time.deltaTime, 0f);
            }
            if (_msg.text.Length > 0 && Time.time > _msgUntil) _msg.text = "";
        }

        void LateUpdate()
        {
            _chase.Follow(_ant.position, Time.deltaTime);
            if (!_running) _pointer.Hide();
            else if (_carrying) _pointer.Show(_sitePos, new Color(0.62f, 0.35f, 0.70f));
            else _pointer.Show(_piecePos, UiKit.Gold);
        }

        // ---------------- UI ----------------
        void RefreshTimer()
        {
            _lastSecond = Mathf.CeilToInt(Mathf.Max(0f, _timeLeft));
            _timer.text = $"Tiempo {_lastSecond}";
        }

        void RefreshInfo()
        {
            _info.text = $"Pieza {_attempt + 1} de {Game.BuildAttempts}";
            _placedText.text = $"Colocadas: {_hits}";
        }

        void RefreshHint()
        {
            if (!_run.Moving) _hint.text = "Desliza el dedo para moverte. Coge la pieza y llevala al solar morado.";
            else if (_carrying) _hint.text = "Llevala al circulo morado antes de que se acabe el tiempo.";
            else _hint.text = "Ve a por la pieza (bloque gris).";
        }

        void EndRound()
        {
            _running = false;
            _info.text = "Obra terminada";
            _placedText.text = $"Colocadas: {_hits}";
            _timer.text = "Fin";
            string summary = _hits == Game.BuildAttempts
                ? $"Perfecto: {Pieces} piezas (bonus +1)"
                : $"Colocaste {Pieces} piezas de {Game.BuildAttempts}";
            var panel = UiKit.Box(_root, "Result", UiKit.Panel, 0.05f, 0.30f, 0.95f, 0.74f);
            UiKit.Label(panel.transform, "Obra terminada", 54, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.72f, 0.95f, 0.98f);
            UiKit.Label(panel.transform, summary, 44, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.30f, 0.95f, 0.72f);
            UiKit.MakeButton(panel.transform, "Recoger", UiKit.Gold, 54, Finish, 0.25f, 0.05f, 0.75f, 0.26f);
        }

        // Al salir antes de tiempo se conservan las piezas ya colocadas.
        void Finish()
        {
            Game.CompleteBuild(Pieces);
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
