using System;
using UnityEngine;
using UnityEngine.UI;

namespace Antopia
{
    // Minijuego de la constructora: para el marcador dentro de la zona verde para colocar cada pieza.
    public class BuilderGame : MonoBehaviour
    {
        RectTransform _bar, _zone, _marker, _tower;
        Text _info, _result;
        Button _place, _done;
        float _t, _dir = 1f, _speed, _zoneCenter, _zoneWidth;
        int _attempt, _hits;
        bool _running;
        Action _onClose;

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
            _onClose = onClose;
            UiKit.Box(root, "Bg", new Color(0.36f, 0.27f, 0.16f, 1f), 0, 0, 1, 1);
            UiKit.Label(root, "CONSTRUCTORA: coloca piezas", 56, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.90f, 0.95f, 0.98f);
            _info = UiKit.Label(root, "", 48, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.84f, 0.95f, 0.90f);

            _tower = UiKit.Rect(root, "Tower", 0.32f, 0.48f, 0.68f, 0.82f);
            UiKit.Box(_tower, "Base", new Color(0f, 0f, 0f, 0.25f), 0, 0, 1, 1);

            var barBg = UiKit.Box(root, "Bar", new Color(0.15f, 0.11f, 0.08f, 1f), 0.08f, 0.32f, 0.92f, 0.40f);
            _bar = barBg.rectTransform;
            _zone = UiKit.Box(_bar, "Zone", UiKit.Leaf, 0.4f, 0f, 0.6f, 1f).rectTransform;
            _marker = UiKit.Box(_bar, "Marker", UiKit.Gold, 0.5f, -0.15f, 0.5f, 1.15f).rectTransform;
            _marker.sizeDelta = new Vector2(30, 0);

            _place = UiKit.MakeButton(root, "Colocar pieza", UiKit.Gold, 64, OnPlace, 0.2f, 0.14f, 0.8f, 0.26f);
            _result = UiKit.Label(root, "", 52, TextAnchor.MiddleCenter, UiKit.Cream, 0.1f, 0.45f, 0.9f, 0.58f);
            _done = UiKit.MakeButton(root, "Recoger", UiKit.Gold, 60, Finish, 0.25f, 0.24f, 0.75f, 0.36f);
            _done.gameObject.SetActive(false);

            _attempt = 0;
            _hits = 0;
            StartAttempt();
            _running = true;
        }

        void StartAttempt()
        {
            _speed = 0.9f + 0.25f * _attempt;
            _zoneWidth = Mathf.Lerp(0.30f, 0.16f, _attempt / (float)(Game.BuildAttempts - 1));
            _zoneCenter = UnityEngine.Random.Range(_zoneWidth * 0.5f + 0.05f, 1f - _zoneWidth * 0.5f - 0.05f);
            _zone.anchorMin = new Vector2(_zoneCenter - _zoneWidth * 0.5f, 0f);
            _zone.anchorMax = new Vector2(_zoneCenter + _zoneWidth * 0.5f, 1f);
            _info.text = $"Pieza {_attempt + 1} de {Game.BuildAttempts}   Colocadas: {_hits}";
        }

        void Update()
        {
            if (!_running) return;
            _t += _dir * _speed * Time.deltaTime;
            if (_t >= 1f) { _t = 1f; _dir = -1f; }
            if (_t <= 0f) { _t = 0f; _dir = 1f; }
            _marker.anchorMin = new Vector2(_t, -0.15f);
            _marker.anchorMax = new Vector2(_t, 1.15f);
        }

        void OnPlace()
        {
            if (!_running) return;
            bool hit = Mathf.Abs(_t - _zoneCenter) <= _zoneWidth * 0.5f;
            float h = 1f / Game.BuildAttempts;
            var block = UiKit.Box(_tower, "Block", hit ? UiKit.Gold : new Color(0.75f, 0.2f, 0.15f, 0.55f),
                hit ? 0.02f : 0.25f, _attempt * h, hit ? 0.98f : 0.75f, (_attempt + 1) * h);
            block.raycastTarget = false;
            if (hit) _hits++;

            _attempt++;
            if (_attempt >= Game.BuildAttempts) EndRound();
            else StartAttempt();
        }

        void EndRound()
        {
            _running = false;
            _place.gameObject.SetActive(false);
            int pieces = _hits + (_hits == Game.BuildAttempts ? 1 : 0);
            _info.text = "Obra terminada";
            _result.text = _hits == Game.BuildAttempts
                ? $"Perfecto: {pieces} piezas (bonus +1)"
                : $"Colocaste {pieces} piezas";
            _done.gameObject.SetActive(true);
        }

        void Finish()
        {
            int pieces = _hits + (_hits == Game.BuildAttempts ? 1 : 0);
            Game.CompleteBuild(pieces);
            _onClose?.Invoke();
            Destroy(gameObject);
        }
    }
}
