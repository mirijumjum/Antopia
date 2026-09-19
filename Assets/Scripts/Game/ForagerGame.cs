using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Antopia
{
    // Minijuego de la obrera: toca las hojas y ramas antes de que desaparezcan.
    public class ForagerGame : MonoBehaviour
    {
        const float Duration = 25f;
        const float NodeLife = 1.7f;

        class Node
        {
            public GameObject go;
            public float expires;
            public float born;
        }

        RectTransform _play;
        Text _timer, _score, _result;
        Button _done;
        readonly List<Node> _nodes = new List<Node>();
        float _timeLeft, _spawnTimer;
        int _leaves, _twigs;
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
            _onClose = onClose;
            UiKit.Box(root, "Bg", new Color(0.20f, 0.36f, 0.16f, 1f), 0, 0, 1, 1);
            UiKit.Label(root, "OBRERA: recolecta", 60, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.90f, 0.95f, 0.98f);
            _timer = UiKit.Label(root, "", 54, TextAnchor.MiddleLeft, UiKit.Cream, 0.06f, 0.83f, 0.5f, 0.90f);
            _score = UiKit.Label(root, "", 54, TextAnchor.MiddleRight, UiKit.Cream, 0.5f, 0.83f, 0.94f, 0.90f);
            _play = UiKit.Rect(root, "Play", 0.03f, 0.12f, 0.97f, 0.82f);
            UiKit.Label(root, "Toca las hojas (verde) y las ramas (marron) antes de que se vayan.",
                38, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.02f, 0.95f, 0.11f);
            _result = UiKit.Label(root, "", 56, TextAnchor.MiddleCenter, UiKit.Cream, 0.1f, 0.5f, 0.9f, 0.75f);
            _done = UiKit.MakeButton(root, "Recoger", UiKit.Gold, 60, Finish, 0.25f, 0.3f, 0.75f, 0.42f);
            _done.gameObject.SetActive(false);

            _timeLeft = Duration;
            _running = true;
            RefreshTexts();
        }

        void Update()
        {
            if (!_running) return;
            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f)
            {
                EndRound();
                return;
            }

            float progress = 1f - _timeLeft / Duration;
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = Mathf.Lerp(0.8f, 0.45f, progress);
                Spawn();
            }

            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                var n = _nodes[i];
                if (Time.time >= n.expires)
                {
                    Destroy(n.go);
                    _nodes.RemoveAt(i);
                    continue;
                }
                float grow = Mathf.Clamp01((Time.time - n.born) / 0.15f);
                float shrink = Mathf.Clamp01((n.expires - Time.time) / 0.3f);
                n.go.transform.localScale = Vector3.one * Mathf.Min(grow, shrink);
            }
            RefreshTexts();
        }

        void Spawn()
        {
            bool twig = UnityEngine.Random.value < 0.25f;
            var rt = UiKit.Rect(_play, twig ? "Twig" : "Leaf", 0, 0, 0, 0);
            float x = UnityEngine.Random.Range(0.12f, 0.88f);
            float y = UnityEngine.Random.Range(0.08f, 0.92f);
            rt.anchorMin = rt.anchorMax = new Vector2(x, y);
            rt.sizeDelta = twig ? new Vector2(190, 90) : new Vector2(150, 150);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = twig ? UiKit.Twig : UiKit.Leaf;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var node = new Node { go = rt.gameObject, born = Time.time, expires = Time.time + NodeLife };
            btn.onClick.AddListener(() =>
            {
                if (!_running) return;
                if (twig) _twigs++; else _leaves++;
                _nodes.Remove(node);
                Destroy(node.go);
                RefreshTexts();
            });
            UiKit.Label(rt, twig ? "Rama" : "Hoja", 34, TextAnchor.MiddleCenter, UiKit.Ink, 0, 0, 1, 1);
            _nodes.Add(node);
        }

        void RefreshTexts()
        {
            _timer.text = $"Tiempo: {Mathf.CeilToInt(Mathf.Max(0, _timeLeft))}";
            _score.text = $"Hojas {_leaves}  Ramas {_twigs}";
        }

        void EndRound()
        {
            _running = false;
            foreach (var n in _nodes) Destroy(n.go);
            _nodes.Clear();
            int l = Mathf.RoundToInt(_leaves * Game.HarvestMultiplier);
            int t = Mathf.RoundToInt(_twigs * Game.HarvestMultiplier);
            _timer.text = "Fin";
            _result.text = $"Traes {l} hojas y {t} ramas\n(bonus de Despensa x{Game.HarvestMultiplier:0.00})";
            _done.gameObject.SetActive(true);
        }

        void Finish()
        {
            Game.CompleteForage(_leaves, _twigs);
            _onClose?.Invoke();
            Destroy(gameObject);
        }
    }
}
