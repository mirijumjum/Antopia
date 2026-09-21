using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Antopia
{
    // Batalla de baile de las soldado: un escarabajo enfadado avanza hacia ellas y un globo pide una forma. Dibuja esa
    // forma con el dedo (raya, zigzag, triangulo, circulo o estrella) y las soldado bailan un baile distinto para cada
    // una; solo la forma pedida hace dano. Cinco olas, tres vidas; cada enemigo vencido da monedas.
    public class SoldierGame : MonoBehaviour
    {
        const int Waves = 5;
        const int StartLives = 3;
        const float EnemyStartZ = 9.5f;    // distancia inicial del enemigo (en Z, desde el escenario)
        const float ContactZ = 2.3f;     // a esta distancia alcanza a las soldado
        static readonly Vector3 Arena = new Vector3(-9f, 0f, -6f);
        static readonly int[] EnemyHp = { 2, 3, 3, 4, 5 };
        static readonly Color[] ShellColors =
        {
            new Color(0.55f, 0.35f, 0.75f), new Color(0.30f, 0.62f, 0.68f), new Color(0.85f, 0.50f, 0.25f),
            new Color(0.80f, 0.30f, 0.50f), new Color(0.72f, 0.16f, 0.18f),
        };
        static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.85f, 0.25f), new Color(0.95f, 0.45f, 0.65f), new Color(0.45f, 0.85f, 0.95f),
            new Color(0.65f, 0.95f, 0.45f), new Color(1f, 0.6f, 0.25f),
        };

        class Particle
        {
            public Transform t;
            public Vector3 vel;
            public float life, spin;
        }

        RectTransform _root;
        Text _left, _center, _msg, _hint, _promptName, _comboText;
        Image _promptIcon, _hpFill;
        Image[] _hearts;
        GesturePad _pad;
        Transform _world, _enemy, _dying;
        AntDancer[] _squad;
        Camera _cam;
        Vector3 _camBase;
        Vector3 _camHome;
        Quaternion _camHomeRot;
        float _camHomeFov;
        readonly List<Particle> _particles = new List<Particle>();

        int _wave, _lives = StartLives, _coins, _combo, _maxCombo, _enemyHp, _enemyMaxHp, _defeated;
        GestureShape _prompt, _lastPrompt;
        float _enemyZ, _knock, _stun, _grace, _nextWaveIn = -1f, _dyingT, _dyingScale, _msgUntil;
        bool _ended;
        Action _onClose;
        Action<bool> _onEnd;
        int _first, _total = Waves; // ola inicial (para la dificultad) y cuantas se juegan
        bool _victory;

        // Dificultad de la ola actual.
        int Diff => _first + _wave;

        public static SoldierGame Open(Transform parent, Action onClose, int firstWave = 0, int waves = Waves, Action<bool> onEnd = null)
        {
            var go = new GameObject("SoldierGame", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var g = go.AddComponent<SoldierGame>();
            g._first = firstWave;
            g._total = waves;
            g._onEnd = onEnd;
            g.Build(rt, onClose);
            return g;
        }

        void Build(RectTransform root, Action onClose)
        {
            _root = root;
            _onClose = onClose;

            var pad = UiKit.Box(root, "Pad", new Color(0f, 0f, 0f, 0f), 0f, 0f, 1f, 0.915f);
            _pad = GesturePad.Attach(pad.gameObject);
            _pad.Drawn += OnDrawn;

            var hud = new MinigameHud(root, Finish);
            _left = hud.Left;
            _center = hud.Center;
            _msg = hud.Msg;
            _hint = hud.Hint;
            _msg.rectTransform.anchorMin = new Vector2(0.05f, 0.14f);
            _msg.rectTransform.anchorMax = new Vector2(0.95f, 0.22f);
            _hint.text = "Dibuja con el dedo la forma del globo y las soldado bailaran para defender el nido.";

            // Vidas: tres corazones en la barra de datos.
            _hearts = new Image[StartLives];
            for (int i = 0; i < StartLives; i++)
                _hearts[i] = UiKit.Icon(root, "Icon79", Color.white, 0.69f + i * 0.09f, 0.862f, 0.77f + i * 0.09f, 0.908f);

            // Globo con la forma que hay que dibujar y barra de vida del enemigo.
            UiKit.Pill(root, "PromptBg", 0.88f, 0.10f, 0.745f, 0.90f, 0.85f);
            _promptIcon = UiKit.Box(root, "PromptIcon", Color.white, 0.13f, 0.75f, 0.29f, 0.845f);
            _promptIcon.preserveAspect = true;
            _promptIcon.raycastTarget = false;
            _promptName = UiKit.Label(root, "", 40, TextAnchor.MiddleLeft, UiKit.Gold, 0.31f, 0.745f, 0.68f, 0.85f);
            _promptName.fontStyle = FontStyle.Bold;
            _comboText = UiKit.Outlined(UiKit.Label(root, "", 34, TextAnchor.MiddleCenter, UiKit.Cream, 0.66f, 0.745f, 0.90f, 0.85f));
            UiKit.Pill(root, "HpBg", 0.9f, 0.26f, 0.716f, 0.74f, 0.738f);
            _hpFill = UiKit.Icon(root, "Progress05", Color.white, 0.27f, 0.721f, 0.73f, 0.733f);
            _hpFill.preserveAspect = false;
            _hpFill.type = Image.Type.Sliced;

            BuildWorld();
            StartWave(0);
            RefreshTexts();
        }

        // ---------------- Mundo ----------------
        void BuildWorld()
        {
            _world = new GameObject("SoldierWorld").transform;

            // Pista de baile de tierra clara bajo las soldado.
            var floor = NestView.Prim(PrimitiveType.Sphere, "DanceFloor", Arena, new Vector3(7.5f, 0.06f, 7.5f), "Ant");
            floor.transform.SetParent(_world, true);
            floor.GetComponent<MeshRenderer>().sharedMaterial = AntModel.Tint(new Color(0.86f, 0.72f, 0.48f), 0.15f);

            _squad = new AntDancer[3];
            var offsets = new[] { new Vector3(-1.9f, 0f, 0.35f), new Vector3(0f, 0f, -0.25f), new Vector3(1.9f, 0f, 0.35f) };
            for (int i = 0; i < _squad.Length; i++)
            {
                var ant = AntModel.Create("Soldier" + i, AntRoles.All[AntRoles.Soldado].Variant, _world);
                ant.localScale = Vector3.one * 1.2f;
                ant.position = Arena + offsets[i];
                ant.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(0f, (i - 1) * 12f, 0f);
                ant.GetComponent<AntRig>().ForcedSpeed = 0f;
                _squad[i] = ant.gameObject.AddComponent<AntDancer>();
            }

            _cam = Camera.main;
            if (_cam != null)
            {
                _camHome = _cam.transform.position;
                _camHomeRot = _cam.transform.rotation;
                _camHomeFov = _cam.fieldOfView;
                _cam.transform.position = Arena + new Vector3(0f, 5.4f, -9.6f);
                _cam.transform.rotation = Quaternion.LookRotation(Arena + new Vector3(0f, 0.6f, 3.6f) - _cam.transform.position);
                _cam.fieldOfView = 50f;
                _camBase = _cam.transform.position;
            }
        }

        // ---------------- Olas ----------------
        void StartWave(int wave)
        {
            _wave = wave;
            _enemyMaxHp = _enemyHp = EnemyHp[Mathf.Min(Diff, EnemyHp.Length - 1)];
            _enemyZ = EnemyStartZ;
            _knock = 0f;
            _stun = 0f;
            _grace = wave == 0 ? 2.5f : 1.2f;
            _nextWaveIn = -1f;
            bool boss = Diff == Waves - 1 && _total > 1;
            _enemy = BeetleModel.Create("Beetle", _world, ShellColors[Diff % ShellColors.Length]);
            _enemy.localScale = Vector3.one * (boss ? 1.9f : 1.3f);
            _enemy.position = Arena + new Vector3(0f, 0f, _enemyZ);
            _enemy.rotation = Quaternion.LookRotation(Vector3.back);
            NextPrompt();
            Say(boss ? "Ola final: el escarabajo jefe!" : _total == 1 ? "Un escarabajo cierra el paso!" : $"Ola {wave + 1}");
            RefreshTexts();
        }

        void NextPrompt()
        {
            var pool = new List<GestureShape> { GestureShape.Line, GestureShape.Triangle };
            if (Diff >= 1) pool.Add(GestureShape.Circle);
            if (Diff >= 2) pool.Add(GestureShape.Zigzag);
            GestureShape pick;
            int guard = 0;
            do
            {
                pick = Diff >= 3 && Random.value < 0.25f ? GestureShape.Star : pool[Random.Range(0, pool.Count)];
            } while (pick == _lastPrompt && ++guard < 10);
            _prompt = _lastPrompt = pick;
            _promptIcon.sprite = GestureIcons.Get(pick);
            _promptName.text = "Dibuja: " + GestureRecognizer.Names[(int)pick];
        }

        // ---------------- Jugada ----------------
        void OnDrawn(List<Vector2> stroke)
        {
            if (_ended || _enemy == null) return;
            if (!GestureRecognizer.TryRecognize(stroke, out var shape))
            {
                Say("No lo he entendido, dibuja la forma grande y de un solo trazo");
                Sfx.Play(Sfx.Id.Wrong);
                return;
            }
            Perform(shape);
        }

        void Perform(GestureShape shape)
        {
            var move = AntDancer.MoveFor(shape);
            for (int i = 0; i < _squad.Length; i++) _squad[i].Play(move, i * 0.12f);
            switch (move)
            {
                case DanceMove.Hop: Sfx.Play(Sfx.Id.Boing); break;
                case DanceMove.Shimmy: Sfx.Play(Sfx.Id.Trill); break;
                case DanceMove.Spin: Sfx.Play(Sfx.Id.Whoosh); break;
                case DanceMove.Waltz: Sfx.Play(Sfx.Id.Waltz); break;
                default: Sfx.Play(Sfx.Id.Sparkle); break;
            }
            Burst(Arena + new Vector3(0f, 1.2f, 0f), shape == GestureShape.Star ? 26 : 14);

            if (shape != _prompt)
            {
                _combo = 0;
                Sfx.Play(Sfx.Id.Wrong);
                Say($"Bonito baile, pero pedia {GestureRecognizer.Names[(int)_prompt]}");
                RefreshTexts();
                return;
            }

            _combo++;
            _maxCombo = Mathf.Max(_maxCombo, _combo);
            int dmg = shape == GestureShape.Star ? 2 : 1;
            _enemyHp -= dmg;
            _knock = 5.5f * dmg;
            _stun = 0.5f;
            Burst(_enemy.position + Vector3.up * 1f, 12);
            Sfx.Play(Sfx.Id.Thump, 1f, Random.Range(0.9f, 1.1f));
            Haptics.Bump();
            CamShake.Trigger(0.10f, 0.2f);
            Juice.Popup(_root, _enemy.position, $"-{dmg}", new Color(1f, 0.45f, 0.4f), 56);
            if (_combo >= 3) Juice.Popup(_root, Arena + new Vector3(0f, 1.6f, 0f), $"Combo x{_combo}", UiKit.Gold, 48);

            if (_enemyHp <= 0)
            {
                Defeat();
                return;
            }
            Say(_combo >= 3 ? $"Combo x{_combo}!" : shape == GestureShape.Star ? "Superestrella!" : "Bien!");
            NextPrompt();
            RefreshTexts();
        }

        // Para las capturas automaticas: ejecuta la forma que pide el globo.
        internal void DebugPerform() => Perform(_prompt);

        void Defeat()
        {
            _defeated++;
            Sfx.Play(Sfx.Id.Defeat);
            Juice.Popup(_root, _enemy.position, $"+{15 + Mathf.Min(_combo, 5) * 3}", UiKit.Gold, 60);
            _coins += 15 + Mathf.Min(_combo, 5) * 3;
            _dying = _enemy;
            _dyingT = 0f;
            _dyingScale = _enemy.localScale.x;
            _enemy = null;
            if (_wave + 1 >= _total)
            {
                if (_total > 1) _coins += 50;
                Say("Victoria! El nido esta a salvo");
                End(true);
            }
            else
            {
                Say("Enemigo vencido!");
                _nextWaveIn = 1.8f;
            }
            RefreshTexts();
        }

        void HitSquad()
        {
            _lives--;
            _combo = 0;
            Sfx.Play(Sfx.Id.Hurt);
            Haptics.Big();
            CamShake.Trigger(0.35f, 0.4f);
            _knock = 7f;
            _stun = 0.8f;
            for (int i = 0; i < _hearts.Length; i++) _hearts[i].color = i < _lives ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.45f);
            for (int i = 0; i < _squad.Length; i++) _squad[i].Play(DanceMove.Shimmy, 0f);
            Say(_lives > 0 ? "Ay! El bicho ha llegado" : "Las soldado estan agotadas...");
            RefreshTexts();
            if (_lives <= 0) End(false);
        }

        void Say(string text)
        {
            _msg.text = text;
            _msgUntil = Time.time + 1.8f;
        }

        // ---------------- Bucle ----------------
        void Update()
        {
            float dt = Time.deltaTime;

            if (!_ended && _enemy != null)
            {
                if (_grace > 0f) _grace -= dt;
                else if (_stun > 0f) _stun -= dt;
                else _enemyZ -= (0.85f + 0.12f * Diff) * dt;

                _enemyZ += _knock * dt;
                _knock = Mathf.MoveTowards(_knock, 0f, 12f * dt);
                _enemyZ = Mathf.Min(_enemyZ, EnemyStartZ + 2f);

                float sway = 0.35f * Mathf.Sin(Time.time * 1.7f);
                _enemy.position = Arena + new Vector3(sway, Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.06f, _enemyZ);
                _enemy.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(0f, 0f, 3f * Mathf.Sin(Time.time * 5f));

                if (_enemyZ <= ContactZ) HitSquad();
            }

            if (_nextWaveIn >= 0f)
            {
                _nextWaveIn -= dt;
                if (_nextWaveIn < 0f && !_ended) StartWave(_wave + 1);
            }

            // Enemigo vencido: sale volando dando vueltas.
            if (_dying != null)
            {
                _dyingT += dt;
                float u = _dyingT / 1.0f;
                _dying.position += new Vector3(0f, 6f * (1f - u), 4f) * dt;
                _dying.Rotate(0f, 0f, 720f * dt);
                _dying.localScale = Vector3.one * Mathf.Max(0.05f, (1f - u) * _dyingScale);
                if (u >= 1f)
                {
                    Destroy(_dying.gameObject);
                    _dying = null;
                }
            }

            UpdateParticles(dt);
            if (_msg.text.Length > 0 && Time.time > _msgUntil) _msg.text = "";
        }

        // El temblor de camara se suma a la posicion fija de la camara de combate.
        void LateUpdate()
        {
            if (_cam != null) _cam.transform.position = _camBase + CamShake.Offset();
        }

        // ---------------- Confeti ----------------
        void Burst(Vector3 pos, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var t = NestView.Prim(PrimitiveType.Cube, "Confetti", pos, Vector3.one * Random.Range(0.10f, 0.18f), "Ant").transform;
                t.SetParent(_world, true);
                t.GetComponent<MeshRenderer>().sharedMaterial = AntModel.Tint(ConfettiColors[Random.Range(0, ConfettiColors.Length)], 0.3f);
                var dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.8f, 1.8f), Random.Range(-1f, 1f));
                _particles.Add(new Particle { t = t, vel = dir * Random.Range(2.5f, 4.5f), life = Random.Range(0.8f, 1.4f), spin = Random.Range(-500f, 500f) });
            }
        }

        void UpdateParticles(float dt)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.vel.y -= 10f * dt;
                p.t.position += p.vel * dt;
                p.t.Rotate(p.spin * dt, p.spin * 0.7f * dt, 0f);
                p.life -= dt;
                if (p.life <= 0f)
                {
                    Destroy(p.t.gameObject);
                    _particles.RemoveAt(i);
                }
            }
        }

        // ---------------- UI ----------------
        void RefreshTexts()
        {
            _left.text = _total == 1 ? "Combate" : $"Ola {Mathf.Min(_wave + 1, _total)}/{_total}";
            _center.text = $"Monedas {_coins}";
            _comboText.text = _combo >= 2 ? $"Combo x{_combo}" : "";
            float frac = _enemyMaxHp > 0 ? Mathf.Clamp01(_enemyHp / (float)_enemyMaxHp) : 0f;
            _hpFill.gameObject.SetActive(frac > 0f && _enemy != null);
            _hpFill.rectTransform.anchorMax = new Vector2(0.27f + 0.46f * frac, 0.733f);
        }

        void End(bool victory)
        {
            _ended = true;
            _victory = victory;
            Sfx.Play(victory ? Sfx.Id.Fanfare : Sfx.Id.Wrong);
            _pad.Enabled = false;
            _hint.text = victory ? "Batalla ganada. Recoge tus monedas." : "Batalla perdida. Recoge las monedas que ganaste.";
            RefreshTexts();
            string summary = $"Enemigos vencidos: {_defeated}/{_total}\nMejor combo: x{_maxCombo}\n+{_coins} monedas";
            var panel = UiKit.Frame(_root, "Result", UiKit.Skin.Green, 0.05f, 0.28f, 0.95f, 0.72f);
            UiKit.Label(panel.transform, victory ? "Batalla ganada!" : "Batalla terminada", 50, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.72f, 0.95f, 0.98f);
            UiKit.Label(panel.transform, summary, 40, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.30f, 0.95f, 0.72f);
            UiKit.MakeButton(panel.transform, "Recoger", UiKit.Gold, 54, Finish, 0.25f, 0.05f, 0.75f, 0.26f);
        }

        // Al salir antes de tiempo se conservan las monedas ya ganadas.
        void Finish()
        {
            Game.CompleteBattle(_coins);
            _onEnd?.Invoke(_victory);
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
