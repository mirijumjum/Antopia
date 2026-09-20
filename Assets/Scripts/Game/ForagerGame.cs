using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Antopia.AntRunner;
using Random = UnityEngine.Random;

namespace Antopia
{
    // Expedicion de la obrera: se mueve por el mundo 3D con deslizamientos, recoge hierba y ramas que van
    // apareciendo y las lleva al nido. Cada entrega en el nido cuenta como una recolecta.
    public class ForagerGame : MonoBehaviour
    {
        const int MaxCargoViews = Game.ForageBaseCapacity + Game.ForageMaxLevel;
        static float Duration => Game.ForageDuration;
        static int Capacity => Game.ForageCapacity;
        static int MaxItems => 10 + Capacity;
        static float PickupRadius => Game.ForagePickupRadius;

        class Item
        {
            public GameObject go;
            public bool twig;
            public float born;
            public float phase;
        }

        RectTransform _root;
        Text _timer, _cargoText, _deliveredText, _msg, _hint;
        Material _matLeaf, _matTwig;
        Transform _world, _ant;
        Transform[] _cargoViews;
        AntRunner _run;
        ChaseCam _chase;
        TargetPointer _pointer;
        readonly List<Item> _items = new List<Item>();
        readonly List<bool> _cargo = new List<bool>(); // true = rama, false = hierba
        float _timeLeft, _spawnTimer, _msgUntil;
        int _lastSecond = -1;
        int _trips, _gotLeaves, _gotTwigs;
        bool _running;
        Action _onClose;

        public static ForagerGame Open(Transform parent, Action onClose)
        {
            var go = new GameObject("ForagerGame", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var g = go.AddComponent<ForagerGame>();
            g.Build(rt, onClose);
            return g;
        }

        void Build(RectTransform root, Action onClose)
        {
            _root = root;
            _onClose = onClose;

            // El mundo 3D se ve a traves de la UI: solo hay una zona tactil transparente y barras encima.
            var padImg = UiKit.Box(root, "SwipePad", new Color(0f, 0f, 0f, 0f), 0f, 0f, 1f, 0.915f);
            padImg.gameObject.AddComponent<SwipePad>().Swiped += OnSwipe;

            UiKit.Box(root, "InfoBg", new Color(0.12f, 0.09f, 0.06f, 0.85f), 0f, 0.85f, 1f, 0.915f).raycastTarget = false;
            _timer = UiKit.Label(root, "", 38, TextAnchor.MiddleCenter, UiKit.Cream, 0.01f, 0.85f, 0.25f, 0.915f);
            _cargoText = UiKit.Label(root, "", 38, TextAnchor.MiddleCenter, UiKit.Gold, 0.25f, 0.85f, 0.55f, 0.915f);
            _deliveredText = UiKit.Label(root, "", 34, TextAnchor.MiddleCenter, UiKit.Cream, 0.55f, 0.85f, 0.99f, 0.915f);
            _msg = UiKit.Label(root, "", 46, TextAnchor.MiddleCenter, UiKit.Gold, 0.05f, 0.78f, 0.95f, 0.85f);

            UiKit.Box(root, "HintBg", new Color(0f, 0f, 0f, 0.5f), 0.02f, 0.02f, 0.70f, 0.10f).raycastTarget = false;
            _hint = UiKit.Label(root, "", 32, TextAnchor.MiddleCenter, UiKit.Cream, 0.03f, 0.02f, 0.69f, 0.10f);
            UiKit.MakeButton(root, "Salir", UiKit.Cream, 40, Finish, 0.73f, 0.02f, 0.98f, 0.10f);
            _pointer = new TargetPointer(root);

            BuildWorld();

            _timeLeft = Duration;
            _running = true;
            RefreshTimer();
            RefreshCargo();
            RefreshDelivered();
            RefreshHint();
            for (int i = 0; i < 8; i++) Spawn();
        }

        // ---------------- Mundo ----------------
        void BuildWorld()
        {
            _matLeaf = Resources.Load<Material>("Materials/Leaf");
            _matTwig = Resources.Load<Material>("Materials/Twig");
            _world = new GameObject("ForagerWorld").transform;

            _ant = AntModel.Create("PlayerAnt", AntRoles.All[AntRoles.Obrera].Variant, _world);
            _ant.localScale = Vector3.one * 0.9f;
            _ant.position = NestView.NestEntrance + new Vector3(0f, 0f, -(DeliverRadius + 0.2f));
            _run = new AntRunner(_ant, Game.ForageSpeedMultiplier);
            _chase = new ChaseCam();
            _cargoViews = new Transform[MaxCargoViews];
            for (int i = 0; i < MaxCargoViews; i++)
            {
                _cargoViews[i] = Part(_ant, PrimitiveType.Cube, "Cargo", AntModel.CargoBase + Vector3.up * (i * 0.13f),
                    new Vector3(0.5f, 0.11f, 0.4f), "Leaf");
                _cargoViews[i].gameObject.SetActive(false);
            }
        }

        static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, string mat)
        {
            var t = NestView.Prim(type, name, Vector3.zero, scale, mat).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            return t;
        }

        void Spawn()
        {
            if (_items.Count >= MaxItems || !FindSpot(out var p)) return;
            bool twig = Random.value < 0.3f;
            var go = new GameObject(twig ? "Twig" : "Grass");
            go.transform.SetParent(_world, false);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            if (twig)
            {
                Part(go.transform, PrimitiveType.Cube, "A", new Vector3(0f, 0.06f, 0f), new Vector3(1.0f, 0.1f, 0.14f), "Twig");
                var b = Part(go.transform, PrimitiveType.Cube, "B", new Vector3(0.1f, 0.11f, 0.05f), new Vector3(0.7f, 0.09f, 0.12f), "Twig");
                b.localRotation = Quaternion.Euler(0f, 35f, 0f);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    var blade = Part(go.transform, PrimitiveType.Cube, "Blade", new Vector3((i - 1.5f) * 0.13f, 0.28f, (i % 2) * 0.1f),
                        new Vector3(0.09f, 0.55f, 0.06f), "Leaf");
                    blade.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), 0f, Random.Range(-18f, 18f));
                }
            }
            go.transform.localScale = Vector3.zero;
            _items.Add(new Item { go = go, twig = twig, born = Time.time, phase = Random.value * 6.28f });
        }

        bool FindSpot(out Vector3 p)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                p = new Vector3(Random.Range(-WorldHalf + 1f, WorldHalf - 1f), 0f, Random.Range(-WorldHalf + 1f, WorldHalf - 1f));
                if (Flat(p - _ant.position).magnitude < 4f) continue;
                if (!IsClear(p, 1.0f)) continue;
                bool ok = true;
                for (int i = 0; i < _items.Count && ok; i++)
                    if (Flat(p - _items[i].go.transform.position).magnitude < 1.5f) ok = false;
                if (ok) return true;
            }
            p = default;
            return false;
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
                _timeLeft -= Time.deltaTime;
                if (_timeLeft <= 0f)
                {
                    EndRound();
                }
                else
                {
                    _spawnTimer -= Time.deltaTime;
                    if (_spawnTimer <= 0f)
                    {
                        _spawnTimer = Random.Range(0.6f, 1.1f);
                        Spawn();
                    }
                    _run.Move(Time.deltaTime);
                    Pickups();
                    TryDeliver();
                    int sec = Mathf.CeilToInt(_timeLeft);
                    if (sec != _lastSecond) RefreshTimer();
                }
            }
            AnimateItems();
            if (_msg.text.Length > 0 && Time.time > _msgUntil) _msg.text = "";
        }

        void LateUpdate()
        {
            _chase.Follow(_ant.position, Time.deltaTime);
            if (_running) UpdatePointer();
            else _pointer.Hide();
        }

        // Cargado: senala el nido. Vacio: senala la hierba o rama mas cercana.
        void UpdatePointer()
        {
            if (_cargo.Count > 0)
            {
                _pointer.Show(NestView.NestEntrance, UiKit.Gold);
                return;
            }
            Item best = null;
            float bestDist = float.MaxValue;
            foreach (var it in _items)
            {
                float d = Flat(it.go.transform.position - _ant.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = it; }
            }
            if (best == null) _pointer.Hide();
            else _pointer.Show(best.go.transform.position, best.twig ? UiKit.Twig : UiKit.Leaf);
        }

        void Pickups()
        {
            if (_cargo.Count >= Capacity) return;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                if (!_run.Near(it.go.transform.position, PickupRadius)) continue;
                _cargo.Add(it.twig);
                Destroy(it.go);
                _items.RemoveAt(i);
                RefreshCargo();
                RefreshHint();
                if (_cargo.Count >= Capacity) return;
            }
        }

        void TryDeliver()
        {
            if (_cargo.Count == 0) return;
            if (!_run.Near(NestView.NestEntrance, DeliverRadius)) return;
            int twigs = 0;
            foreach (bool t in _cargo) if (t) twigs++;
            int leaves = _cargo.Count - twigs;
            var got = Game.CompleteForage(leaves, twigs);
            _trips++;
            _gotLeaves += got.leaves;
            _gotTwigs += got.twigs;
            _cargo.Clear();
            _msg.text = $"+{got.leaves} hojas   +{got.twigs} ramas";
            _msgUntil = Time.time + 2f;
            RefreshCargo();
            RefreshDelivered();
            RefreshHint();
        }

        void AnimateItems()
        {
            foreach (var it in _items)
            {
                float grow = Mathf.Clamp01((Time.time - it.born) / 0.3f);
                it.go.transform.localScale = Vector3.one * (grow * (1f + 0.06f * Mathf.Sin(Time.time * 4f + it.phase)));
            }
        }

        // ---------------- UI ----------------
        void RefreshTimer()
        {
            _lastSecond = Mathf.CeilToInt(Mathf.Max(0f, _timeLeft));
            _timer.text = $"Tiempo {_lastSecond}";
        }

        void RefreshCargo()
        {
            _cargoText.text = $"Carga {_cargo.Count}/{Capacity}";
            for (int i = 0; i < MaxCargoViews; i++)
            {
                bool has = i < _cargo.Count;
                _cargoViews[i].gameObject.SetActive(has);
                if (has) _cargoViews[i].GetComponent<MeshRenderer>().sharedMaterial = _cargo[i] ? _matTwig : _matLeaf;
            }
        }

        void RefreshDelivered()
        {
            _deliveredText.text = $"Entregado: {_gotLeaves} hojas, {_gotTwigs} ramas";
        }

        void RefreshHint()
        {
            if (!_run.Moving) _hint.text = "Desliza el dedo para moverte. Recoge hierba y ramas y llevalas al nido.";
            else if (_cargo.Count >= Capacity) _hint.text = "Carga llena: vuelve al nido.";
            else if (_cargo.Count > 0) _hint.text = "Lleva la carga al nido (monticulo marron).";
            else _hint.text = "Desliza para cambiar de direccion.";
        }

        void EndRound()
        {
            _running = false;
            _timer.text = "Fin";
            int lost = _cargo.Count;
            string summary = $"{_trips} entregas en el nido\n+{_gotLeaves} hojas   +{_gotTwigs} ramas\n(incluye bonus de Despensa x{Game.HarvestMultiplier:0.00})";
            if (lost > 0) summary += $"\nSe perdio la carga que llevabas ({lost}).";
            var panel = UiKit.Frame(_root, "Result", UiKit.Skin.Green, 0.05f, 0.30f, 0.95f, 0.74f);
            UiKit.Label(panel.transform, "Expedicion terminada", 54, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.72f, 0.95f, 0.98f);
            UiKit.Label(panel.transform, summary, 40, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.30f, 0.95f, 0.72f);
            UiKit.MakeButton(panel.transform, "Volver", UiKit.Gold, 54, Finish, 0.25f, 0.05f, 0.75f, 0.26f);
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
