using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Antopia
{
    // Torre de la constructora: una pieza se desliza de lado a lado sobre la torre. Toca la pantalla para soltarla;
    // lo que sobresale se corta y cae. Cada piso levantado es una pieza y, si llegas arriba, hay bonus. Si la pieza
    // cae fuera de la torre, la obra termina.
    public class BuilderGame : MonoBehaviour
    {
        const int MaxFloors = 10;
        const int CompleteBonus = 2;
        const float BaseWidth = 2.4f;
        const float Depth = 2.0f;
        const float Height = 0.45f;
        const float BaseTop = 0.2f;       // altura de la tarima
        const float Perfect = 0.10f;      // margen para colocar sin recorte
        const float Range = 2.9f;         // recorrido de la pieza a cada lado del centro
        static readonly Vector3 Site = new Vector3(7.5f, 0f, -5f);

        static readonly Color[] BlockColors =
        {
            new Color(0.88f, 0.66f, 0.42f), new Color(0.74f, 0.52f, 0.34f), new Color(0.94f, 0.80f, 0.50f),
            new Color(0.66f, 0.60f, 0.54f), new Color(0.84f, 0.58f, 0.42f), new Color(0.70f, 0.64f, 0.46f),
        };

        class Debris
        {
            public Transform t;
            public Vector3 vel;
            public float spin, life;
        }

        RectTransform _root;
        Text _left, _center, _right, _msg, _hint;
        Transform _world, _ant, _mover;
        Camera _cam;
        Vector3 _camHome;
        Quaternion _camHomeRot;
        float _camHomeFov;
        readonly List<Debris> _debris = new List<Debris>();
        float _topX = Site.x, _topW = BaseWidth;
        float _moverX, _dir = 1f, _speed, _msgUntil, _hopT = 1f;
        int _floors, _streak, _perfects;
        bool _ended, _complete;
        Action _onClose;

        int Pieces => Mathf.Min(_floors, MaxFloors) + (_complete ? CompleteBonus : 0);
        float TopY => BaseTop + _floors * Height;

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

            var pad = UiKit.Box(root, "Pad", new Color(0f, 0f, 0f, 0f), 0f, 0f, 1f, 0.915f);
            TapPad.Attach(pad.gameObject).Tapped += Drop;

            var hud = new MinigameHud(root, Finish);
            _left = hud.Left;
            _center = hud.Center;
            _right = hud.Right;
            _msg = hud.Msg;
            _hint = hud.Hint;
            _hint.text = "Toca la pantalla para soltar la pieza justo encima de la torre.";

            BuildWorld();
            RefreshTexts();
        }

        // ---------------- Mundo ----------------
        void BuildWorld()
        {
            _world = new GameObject("BuilderWorld").transform;

            // Tarima de madera y aro de tierra en el solar.
            Part("Ring", PrimitiveType.Sphere, new Vector3(Site.x, 0f, Site.z), new Vector3(6.4f, 0.06f, 6.4f), AntModel.Tint(new Color(0.46f, 0.33f, 0.21f)));
            Part("Pallet", PrimitiveType.Cube, new Vector3(Site.x, BaseTop * 0.5f, Site.z), new Vector3(BaseWidth + 0.7f, BaseTop, Depth + 0.7f),
                AntModel.Tint(new Color(0.42f, 0.29f, 0.18f), 0.15f));

            // La constructora anima desde el suelo, junto a la torre.
            _ant = AntModel.Create("BuilderAnt", AntRoles.All[AntRoles.Constructora].Variant, _world);
            _ant.localScale = Vector3.one * 1.1f;
            _ant.position = new Vector3(Site.x - 3.4f, 0f, Site.z - 1.4f);
            _ant.rotation = Quaternion.LookRotation(new Vector3(Site.x, 0f, Site.z) - _ant.position);

            _cam = Camera.main;
            if (_cam != null)
            {
                _camHome = _cam.transform.position;
                _camHomeRot = _cam.transform.rotation;
                _camHomeFov = _cam.fieldOfView;
            }

            NextMover();
        }

        Transform Part(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = NestView.Prim(type, name, pos, scale, "Ant").transform;
            t.SetParent(_world, true);
            t.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return t;
        }

        static Material BlockMaterial(int level) => AntModel.Tint(BlockColors[level % BlockColors.Length], 0.2f);

        // Nueva pieza que se desliza sobre la torre, del ancho del ultimo piso.
        void NextMover()
        {
            float y = BaseTop + (_floors + 0.5f) * Height;
            _mover = Part("Block", PrimitiveType.Cube, new Vector3(Site.x, y, Site.z), new Vector3(_topW, Height, Depth), BlockMaterial(_floors));
            _dir = _floors % 2 == 0 ? 1f : -1f;
            _moverX = Site.x - _dir * Range;
            _speed = Mathf.Min(2.4f + 0.38f * _floors, 6.2f);
        }

        // ---------------- Jugada ----------------
        void Drop()
        {
            if (_ended || _mover == null) return;
            float dx = _moverX - _topX;
            float adx = Mathf.Abs(dx);
            float y = BaseTop + (_floors + 0.5f) * Height;

            if (adx >= _topW)
            {
                // Fallo total: la pieza cae y la obra termina.
                SpawnDebris(_mover, Mathf.Sign(dx));
                _mover = null;
                Say("Se cayo la pieza!");
                End(false);
                return;
            }

            float newW = _topW;
            float center = _topX;
            if (adx <= Perfect)
            {
                _streak++;
                _perfects++;
                if (_streak >= 3) newW = Mathf.Min(BaseWidth, _topW + 0.25f); // racha: la torre recupera anchura
                Say(_streak >= 3 ? "Perfecto! La torre se ensancha" : "Perfecto!");
                _hopT = 0f;
            }
            else
            {
                _streak = 0;
                newW = _topW - adx;
                center = _topX + dx * 0.5f;
                float sign = Mathf.Sign(dx);
                // La parte que sobresale se corta y cae.
                var cut = Part("Cut", PrimitiveType.Cube, new Vector3(_topX + sign * _topW * 0.5f + dx * 0.5f, y, Site.z),
                    new Vector3(adx, Height, Depth), BlockMaterial(_floors));
                SpawnDebris(cut, sign);
            }

            _mover.position = new Vector3(center, y, Site.z);
            _mover.localScale = new Vector3(newW, Height, Depth);
            _mover = null;
            _topX = center;
            _topW = newW;
            _floors++;

            if (_floors >= MaxFloors)
            {
                _complete = true;
                Say("Torre completa!");
                End(true);
                return;
            }
            NextMover();
            RefreshTexts();
        }

        void SpawnDebris(Transform t, float sign)
        {
            _debris.Add(new Debris { t = t, vel = new Vector3(sign * 1.6f, 1.5f, 0f), spin = sign * -140f, life = 1.6f });
        }

        void Say(string text)
        {
            _msg.text = text;
            _msgUntil = Time.time + 1.6f;
        }

        // Para las capturas automaticas: suelta n piezas, alternando colocacion casi perfecta y con recorte.
        internal void DebugAutoPlay(int n)
        {
            for (int i = 0; i < n && !_ended; i++)
            {
                _moverX = _topX + (i % 2 == 0 ? 0.04f : 0.55f);
                Drop();
            }
        }

        // ---------------- Bucle ----------------
        void Update()
        {
            float dt = Time.deltaTime;
            if (!_ended && _mover != null)
            {
                _moverX += _dir * _speed * dt;
                if (_moverX > Site.x + Range) { _moverX = Site.x + Range; _dir = -1f; }
                else if (_moverX < Site.x - Range) { _moverX = Site.x - Range; _dir = 1f; }
                _mover.position = new Vector3(_moverX, BaseTop + (_floors + 0.5f) * Height, Site.z);
            }

            for (int i = _debris.Count - 1; i >= 0; i--)
            {
                var d = _debris[i];
                d.vel.y -= 14f * dt;
                d.t.position += d.vel * dt;
                d.t.Rotate(0f, 0f, d.spin * dt);
                d.life -= dt;
                if (d.life <= 0f)
                {
                    Destroy(d.t.gameObject);
                    _debris.RemoveAt(i);
                }
            }

            // Saltito de alegria de la constructora al colocar bien.
            if (_hopT < 1f)
            {
                _hopT = Mathf.Min(1f, _hopT + dt * 2.2f);
                var p = _ant.position;
                p.y = Mathf.Sin(_hopT * Mathf.PI) * 0.7f;
                _ant.position = p;
            }

            if (_msg.text.Length > 0 && Time.time > _msgUntil) _msg.text = "";
        }

        // La camara sube con la torre mirandola en diagonal.
        void LateUpdate()
        {
            if (_cam == null) return;
            var look = new Vector3(Site.x, TopY + 0.4f, Site.z);
            var pos = look + new Vector3(0f, 7.6f, -11.5f);
            float k = 1f - Mathf.Exp(-4f * Time.deltaTime);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, pos, k);
            var want = Quaternion.LookRotation(look - _cam.transform.position);
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, want, k);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 56f, k);
        }

        // ---------------- UI ----------------
        void RefreshTexts()
        {
            _left.text = $"Piso {_floors + (_ended ? 0 : 1)}/{MaxFloors}";
            _center.text = $"Piezas {Pieces}";
            _right.text = $"Perfectos {_perfects}";
        }

        void End(bool complete)
        {
            _ended = true;
            RefreshTexts();
            _left.text = $"Pisos {_floors}/{MaxFloors}";
            _hint.text = complete ? "Obra terminada. Recoge tus piezas." : "La torre ha caido. Recoge las piezas que conseguiste.";
            string summary = complete
                ? $"Torre completa: {_floors} pisos\n{Pieces} piezas (bonus +{CompleteBonus})"
                : $"Torre de {_floors} pisos\n{Pieces} piezas";
            var panel = UiKit.Frame(_root, "Result", UiKit.Skin.Green, 0.05f, 0.30f, 0.95f, 0.74f);
            UiKit.Label(panel.transform, "Obra terminada", 50, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.72f, 0.95f, 0.98f);
            UiKit.Label(panel.transform, summary, 42, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.30f, 0.95f, 0.72f);
            UiKit.MakeButton(panel.transform, "Recoger", UiKit.Gold, 54, Finish, 0.25f, 0.05f, 0.75f, 0.26f);
        }

        // Al salir antes de tiempo se conservan las piezas ya levantadas.
        void Finish()
        {
            Game.CompleteBuild(Pieces);
            _onClose?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_world != null) Destroy(_world.gameObject);
            if (_cam != null)
            {
                _cam.transform.position = _camHome;
                _cam.transform.rotation = _camHomeRot;
                _cam.fieldOfView = _camHomeFov;
            }
        }
    }
}
