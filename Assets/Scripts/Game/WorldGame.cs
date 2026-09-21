using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Antopia.AntRunner;
using Random = UnityEngine.Random;

namespace Antopia
{
    // El mundo compartido por los cuatro roles. Eliges un rol y recorres el mundo libremente (joystick, sin tiempo):
    //   Obrera      recoge recursos y los lleva al nido (la miel y los cristales solo salen en las zonas nuevas).
    //   Exploradora ve muy lejos: destapa la niebla, descubre sitios de interes y encuentra hallazgos.
    //   Constructora levanta el tunel que abre el muro del este (necesita ramas).
    //   Soldado     vence bailando al escarabajo que tapa el paso del norte.
    // Cada camino que abre un rol deja trabajar a los demas. Cambiar de rol cuesta monedas.
    public partial class WorldGame : MonoBehaviour
    {
        enum Kind { Leaf, Twig, Honey, Crystal, Gem }

        class Item
        {
            public GameObject go;
            public Kind kind;
            public float born, phase;
            public int zone;
            public object claim; // NPC que va a por este recurso
        }

        const int MaxCargoViews = Game.ForageBaseCapacity + Game.ForageMaxLevel;
        const float RevealNear = 3.5f;
        const float RevealExplorer = 8f;
        const float InteractDistance = 4f;
        static readonly int[] ZoneCap = { 10, 7, 7 };
        static readonly Vector3 StartPos = new Vector3(0f, 0f, -3.6f);

        RectTransform _root;
        Text _left, _center, _right, _msg, _hint, _questText;
        GameObject _questBox;
        float _questCheck;
        Transform _world, _ant, _guard, _sign;
        Transform[] _cargoViews;
        AntRunner _run;
        ChaseCam _chase;
        VirtualJoystick _stick;
        TargetPointer _pointer;
        FogGrid _fog;
        readonly List<Item> _items = new List<Item>();
        readonly List<Kind> _cargo = new List<Kind>();
        int _role;
        bool _busy, _intro;
        float _spawnTimer, _saveTimer, _msgUntil, _warnUntil;
        internal Vector2 DebugInput; // para las capturas automaticas
        Action _onClose;

        public static WorldGame Open(Transform parent, Action onClose)
        {
            var go = new GameObject("WorldGame", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var g = go.AddComponent<WorldGame>();
            g.Build(rt, onClose);
            return g;
        }

        // ---------------- Montaje ----------------
        void Build(RectTransform root, Action onClose)
        {
            _root = root;
            _onClose = onClose;
            _role = Game.Data.role;

            var pad = UiKit.Box(root, "Pad", new Color(0f, 0f, 0f, 0f), 0f, 0f, 1f, 0.915f);
            _stick = VirtualJoystick.Attach(pad.gameObject);

            var hud = new MinigameHud(root, Finish);
            _left = hud.Left;
            _center = hud.Center;
            _right = hud.Right;
            _msg = hud.Msg;
            _hint = hud.Hint;
            UiKit.IconButton(root, "Icon10", OpenRoleSwitch, 0.80f, 0.115f, 0.97f, 0.20f);

            // Cartel de la mision guiada bajo la barra de datos; los avisos pasan mas abajo.
            _msg.rectTransform.anchorMin = new Vector2(0.05f, 0.66f);
            _msg.rectTransform.anchorMax = new Vector2(0.95f, 0.74f);
            var qbg = UiKit.Pill(root, "QuestBg", 0.88f, 0.03f, 0.775f, 0.97f, 0.85f);
            _questBox = qbg.gameObject;
            UiKit.Icon(qbg.transform, "Icon36", Color.white, 0.03f, 0.12f, 0.15f, 0.88f);
            _questText = UiKit.Label(qbg.transform, "", 30, TextAnchor.MiddleLeft, UiKit.Cream, 0.17f, 0.04f, 0.97f, 0.96f);
            _pointer = new TargetPointer(root);

            _world = new GameObject("World").transform;
            Walls.Clear();
            Blockers.Clear();
            Walls.AddRange(WorldLayout.BuildWalls(Game.Data.tunnelBuilt, out _));

            BuildPois();
            BuildGuard();
            if (Game.Data.tunnelBuilt) BuildArch();
            else BuildSign();
            _fog = new FogGrid(_world);

            _cargoViews = new Transform[MaxCargoViews];
            SpawnPlayer(StartPos, Quaternion.identity);
            _chase = new ChaseCam();
            BuildNpcs();
            for (int i = 0; i < 6; i++) TrySpawn();
            RefreshAll();
        }

        // La hormiga del rol actual, con su velocidad propia.
        void SpawnPlayer(Vector3 pos, Quaternion rot)
        {
            if (_ant != null) Destroy(_ant.gameObject);
            _ant = AntModel.Create("Player", AntRoles.All[_role].Variant, _world);
            _ant.localScale = Vector3.one * 0.95f;
            _ant.position = pos;
            _ant.rotation = rot;
            float speed = _role == AntRoles.Obrera ? Game.ForageSpeedMultiplier : _role == AntRoles.Exploradora ? 1.2f : 1f;
            _run = new AntRunner(_ant, speed);
            _intro = false;
            for (int i = 0; i < MaxCargoViews; i++)
            {
                _cargoViews[i] = AntModel.Part(_ant, PrimitiveType.Cube, "Cargo", AntModel.CargoBase + Vector3.up * (i * 0.13f), new Vector3(0.5f, 0.11f, 0.4f), CargoMaterial(Kind.Leaf));
                _cargoViews[i].gameObject.SetActive(false);
            }
            RefreshCargo();
        }

        // ---------------- Escenario propio del mundo ----------------
        void BuildPois()
        {
            // Colmena de miel (zona norte).
            var hive = new GameObject("Hive").transform;
            hive.SetParent(_world, false);
            hive.position = WorldLayout.PoiPos[0];
            var honey = AntModel.Tint(new Color(0.96f, 0.68f, 0.16f), 0.5f);
            var dark = AntModel.Tint(new Color(0.42f, 0.24f, 0.08f), 0.3f);
            AntModel.Part(hive, PrimitiveType.Sphere, "Dome", new Vector3(0f, 1.4f, 0f), new Vector3(3.4f, 3.0f, 3.4f), honey);
            // Anillos oscuros algo mas anchos que la cupula a cada altura, para que se vean como capas de panal.
            foreach (float y in new[] { 0.55f, 1.15f, 1.75f, 2.35f })
            {
                float d = Mathf.Sqrt(Mathf.Max(0.05f, 1.7f * 1.7f - (y - 1.4f) * (y - 1.4f))) * 2f + 0.3f;
                AntModel.Part(hive, PrimitiveType.Sphere, "Ring", new Vector3(0f, y, 0f), new Vector3(d, 0.2f, d), dark);
            }
            AntModel.Part(hive, PrimitiveType.Sphere, "Cap", new Vector3(0f, 2.85f, 0f), new Vector3(0.9f, 0.5f, 0.9f), dark);
            AntModel.Part(hive, PrimitiveType.Sphere, "Door", new Vector3(0f, 0.6f, -1.6f), new Vector3(0.9f, 1.0f, 0.4f), dark);

            // Cueva de cristales (zona este).
            var cave = new GameObject("Cave").transform;
            cave.SetParent(_world, false);
            cave.position = WorldLayout.PoiPos[1];
            var rock = AntModel.Tint(new Color(0.52f, 0.50f, 0.56f), 0.3f);
            AntModel.Part(cave, PrimitiveType.Sphere, "Rock", new Vector3(0f, 0.7f, 0f), new Vector3(4.2f, 2.6f, 3.8f), rock);
            AntModel.Part(cave, PrimitiveType.Sphere, "Mouth", new Vector3(-1.9f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 1.4f), dark);
            Color[] gems = { new Color(0.4f, 0.85f, 0.95f), new Color(0.85f, 0.5f, 0.95f), new Color(0.5f, 0.95f, 0.7f) };
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                var shard = AntModel.Part(cave, PrimitiveType.Cube, "Shard", new Vector3(Mathf.Cos(a) * 2.6f, 0.6f, Mathf.Sin(a) * 2.4f),
                    new Vector3(0.35f, 1.0f + (i % 3) * 0.35f, 0.35f), AntModel.Tint(gems[i % 3], 0.8f));
                shard.localRotation = Quaternion.Euler(12f * (i % 2 == 0 ? 1 : -1), i * 40f, 14f);
            }
        }

        // El escarabajo que tapa el paso del norte.
        void BuildGuard()
        {
            if (Game.Data.guardDefeated) return;
            _guard = BeetleModel.Create("Guard", _world, new Color(0.55f, 0.35f, 0.75f));
            _guard.localScale = Vector3.one * 1.6f;
            _guard.position = WorldLayout.GuardPos;
            _guard.rotation = Quaternion.LookRotation(Vector3.back);
            Blockers.Add((WorldLayout.GuardPos, 1.8f));
        }

        // Cartel de obra junto al muro del este.
        void BuildSign()
        {
            _sign = new GameObject("TunnelSign").transform;
            _sign.SetParent(_world, false);
            _sign.position = WorldLayout.TunnelPos + new Vector3(-2.2f, 0f, 0f);
            var wood = AntModel.Tint(new Color(0.55f, 0.36f, 0.18f), 0.2f);
            var board = AntModel.Tint(new Color(1f, 0.78f, 0.2f), 0.4f);
            AntModel.Part(_sign, PrimitiveType.Cube, "Post", new Vector3(0f, 0.8f, 0f), new Vector3(0.16f, 1.6f, 0.16f), wood);
            AntModel.Part(_sign, PrimitiveType.Cube, "Board", new Vector3(0f, 1.5f, -0.05f), new Vector3(1.3f, 0.7f, 0.1f), board);
            AntModel.Part(_sign, PrimitiveType.Cube, "Hammer", new Vector3(0f, 1.5f, -0.12f), new Vector3(0.7f, 0.12f, 0.06f), AntModel.Tint(new Color(0.2f, 0.15f, 0.1f)));
            AntModel.Part(_sign, PrimitiveType.Cylinder, "Mark", new Vector3(0f, 0.02f, 0f), new Vector3(3.6f, 0.02f, 3.6f), AntModel.Tint(new Color(0.75f, 0.62f, 0.4f)));
        }

        // Arco de madera del tunel ya construido.
        void BuildArch()
        {
            var arch = new GameObject("TunnelArch").transform;
            arch.SetParent(_world, false);
            arch.position = WorldLayout.TunnelPos;
            var wood = AntModel.Tint(new Color(0.55f, 0.36f, 0.18f), 0.2f);
            foreach (float z in new[] { -1.9f, 1.9f })
                AntModel.Part(arch, PrimitiveType.Cube, "Pillar", new Vector3(0f, 1.0f, z), new Vector3(0.7f, 2.0f, 0.5f), wood);
            AntModel.Part(arch, PrimitiveType.Cube, "Lintel", new Vector3(0f, 2.15f, 0f), new Vector3(0.7f, 0.45f, 4.4f), wood);
            AntModel.Part(arch, PrimitiveType.Cube, "Path", new Vector3(0f, 0.02f, 0f), new Vector3(4.2f, 0.03f, 3.4f), AntModel.Tint(new Color(0.78f, 0.66f, 0.45f)));
        }

        // ---------------- Bucle ----------------
        void Update()
        {
            if (_busy) return;
            float dt = Time.deltaTime;
            _run.SetInput(DebugInput != Vector2.zero ? DebugInput : _stick.Value);
            _run.Move(dt);
            _fog.Update(dt);
            if (!_intro && _run.HasMoved)
            {
                _intro = true;
                RefreshHint();
            }

            Reveal();
            _questCheck -= dt;
            if (_questCheck <= 0f)
            {
                _questCheck = 0.4f;
                CheckQuest();
            }
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = Random.Range(0.7f, 1.2f);
                TrySpawn();
            }
            Pickups();
            if (_role == AntRoles.Obrera) TryDeliver();
            Interact();
            UpdateNpcs(dt);
            AnimateItems();

            _saveTimer += dt;
            if (_saveTimer > 10f)
            {
                _saveTimer = 0f;
                Game.Save();
            }
            if (_msg.text.Length > 0 && Time.time > _msgUntil) _msg.text = "";
        }

        void LateUpdate()
        {
            if (_busy) return;
            _chase.Follow(_ant.position, Time.deltaTime);
            PlaceNpcTag();
            var q = Quests.Current;
            var way = q != null && q.Role == _role ? q.Waypoint() : null;
            if (_role == AntRoles.Obrera && _cargo.Count > 0) _pointer.Show(NestView.NestEntrance, UiKit.Gold);
            else if (way.HasValue) _pointer.Show(way.Value, UiKit.Gold);
            else _pointer.Hide();
        }

        // ---------------- Niebla y descubrimientos ----------------
        void Reveal() => RevealAt(_ant.position, _role == AntRoles.Exploradora ? RevealExplorer : RevealNear, false);

        // Destapa la niebla alrededor de un punto (el jugador o una exploradora NPC).
        void RevealAt(Vector3 pos, float radius, bool byNpc)
        {
            var opened = _fog.Reveal(pos, radius);
            if (opened == null) return;
            Sfx.Play(Sfx.Id.Poof, byNpc ? 0.25f : 0.7f, Random.Range(0.9f, 1.1f));
            foreach (int i in opened)
            {
                bool poiCell = false;
                for (int p = 0; p < WorldLayout.PoiPos.Length; p++)
                {
                    if (FogGrid.IndexOf(WorldLayout.PoiPos[p]) != i) continue;
                    poiCell = true;
                    if (!Game.Data.poiFound[p])
                    {
                        Game.Data.poiFound[p] = true;
                        Game.AddCoins(30);
                        Say(byNpc ? $"Tu exploradora ha descubierto {WorldLayout.PoiNames[p]}! +30 monedas" : $"Descubierto: {WorldLayout.PoiNames[p]}! +30 monedas");
                        Sfx.Play(Sfx.Id.Discover);
                        Haptics.Big();
                        CamShake.Trigger(0.12f, 0.25f);
                        Juice.Popup(_root, WorldLayout.PoiPos[p], "+30", UiKit.Gold, 60);
                        Game.Save();
                    }
                }
                // A veces la niebla esconde un hallazgo.
                if (!poiCell && Random.value < 0.14f)
                    AddItem(Kind.Gem, FogGrid.Center(i) + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f)));
            }
            RefreshTexts();
        }

        // ---------------- Recursos ----------------
        bool ZoneOpen(int zone) => zone == 0 || (zone == 1 && Game.Data.guardDefeated) || (zone == 2 && Game.Data.tunnelBuilt);

        void TrySpawn()
        {
            int zone = Random.Range(0, 3);
            if (!ZoneOpen(zone)) return;
            int count = 0;
            foreach (var it in _items) if (it.zone == zone && it.kind != Kind.Gem) count++;
            if (count >= ZoneCap[zone]) return;

            var r = WorldLayout.Zones[zone];
            var p = new Vector3(Random.Range(r.xMin + 1.8f, r.xMax - 1.8f), 0f, Random.Range(r.yMin + 1.8f, r.yMax - 1.8f));
            if (zone == 0 && !IsClear(p, 1.2f)) return;
            if (!_fog.IsClear(p) || Flat(p - _ant.position).magnitude < 3f) return;
            if (zone == 1 && Flat(p - WorldLayout.PoiPos[0]).magnitude < 3.2f) return;
            if (zone == 2 && Flat(p - WorldLayout.PoiPos[1]).magnitude < 3.6f) return;

            float roll = Random.value;
            Kind kind;
            if (zone == 0) kind = roll < 0.7f ? Kind.Leaf : Kind.Twig;
            else if (zone == 1) kind = roll < 0.45f ? Kind.Honey : roll < 0.8f ? Kind.Leaf : Kind.Twig;
            else kind = roll < 0.45f ? Kind.Crystal : roll < 0.8f ? Kind.Twig : Kind.Leaf;
            AddItem(kind, p);
        }

        void AddItem(Kind kind, Vector3 p)
        {
            var go = MakeItem(kind);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.zero;
            _items.Add(new Item { go = go, kind = kind, born = Time.time, phase = Random.value * 6.28f, zone = Mathf.Max(0, WorldLayout.ZoneOf(p)) });
        }

        GameObject MakeItem(Kind kind)
        {
            switch (kind)
            {
                case Kind.Leaf: return WorldDecor.CreateItem(false, _world);
                case Kind.Twig: return WorldDecor.CreateItem(true, _world);
                case Kind.Honey:
                {
                    var g = new GameObject("Honey");
                    g.transform.SetParent(_world, false);
                    var m = AntModel.Tint(new Color(1f, 0.72f, 0.15f), 0.75f);
                    AntModel.Part(g.transform, PrimitiveType.Sphere, "Drop", new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.5f, 0.6f), m);
                    AntModel.Part(g.transform, PrimitiveType.Sphere, "Top", new Vector3(0.1f, 0.62f, 0.04f), new Vector3(0.28f, 0.28f, 0.28f), m);
                    return g;
                }
                case Kind.Crystal:
                {
                    var g = new GameObject("Crystal");
                    g.transform.SetParent(_world, false);
                    var m = AntModel.Tint(new Color(0.45f, 0.88f, 0.98f), 0.85f);
                    AntModel.Part(g.transform, PrimitiveType.Cube, "A", new Vector3(0f, 0.38f, 0f), new Vector3(0.3f, 0.8f, 0.3f), m).localRotation = Quaternion.Euler(8f, 30f, 12f);
                    AntModel.Part(g.transform, PrimitiveType.Cube, "B", new Vector3(0.22f, 0.24f, 0.05f), new Vector3(0.22f, 0.5f, 0.22f), m).localRotation = Quaternion.Euler(-10f, 10f, -24f);
                    return g;
                }
                default:
                {
                    var g = new GameObject("Gem");
                    g.transform.SetParent(_world, false);
                    AntModel.Part(g.transform, PrimitiveType.Cube, "Gem", new Vector3(0f, 0.5f, 0f), Vector3.one * 0.42f, AntModel.Tint(new Color(0.95f, 0.45f, 0.85f), 0.9f)).localRotation = Quaternion.Euler(45f, 45f, 0f);
                    return g;
                }
            }
        }

        static Material CargoMaterial(Kind kind)
        {
            switch (kind)
            {
                case Kind.Twig: return AntModel.Tint(new Color(0.55f, 0.36f, 0.18f), 0.2f);
                case Kind.Honey: return AntModel.Tint(new Color(1f, 0.72f, 0.15f), 0.7f);
                case Kind.Crystal: return AntModel.Tint(new Color(0.45f, 0.88f, 0.98f), 0.8f);
                default: return AntModel.Tint(new Color(0.35f, 0.7f, 0.25f), 0.3f);
            }
        }

        void Pickups()
        {
            float radius = Game.ForagePickupRadius;
            int capacity = Game.ForageCapacity;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                bool gem = it.kind == Kind.Gem;
                if (!gem && (_role != AntRoles.Obrera || _cargo.Count >= capacity)) continue;
                if (!_run.Near(it.go.transform.position, gem ? 1.1f : radius)) continue;
                _items.RemoveAt(i);
                Destroy(it.go);
                if (gem)
                {
                    Game.AddCoins(8);
                    Say("Un hallazgo! +8 monedas");
                    Sfx.Play(Sfx.Id.Coin);
                    Haptics.Tap();
                    Juice.Popup(_root, _ant.position, "+8", UiKit.Gold);
                    continue;
                }
                _cargo.Add(it.kind);
                Game.Data.statPickups++;
                Sfx.Play(Sfx.Id.Pickup, 0.8f, Random.Range(0.92f, 1.15f));
                Haptics.Tap();
                Juice.Popup(_root, _ant.position, "+1", it.kind == Kind.Honey ? UiKit.Gold : it.kind == Kind.Crystal ? new Color(0.5f, 0.9f, 1f) : UiKit.Cream, 36);
                if (_cargo.Count < capacity && Random.value < Game.ForageLuck) _cargo.Add(it.kind); // suerte: cuenta doble
                RefreshCargo();
                RefreshTexts();
            }
        }

        void TryDeliver()
        {
            if (_cargo.Count == 0 || !_run.Near(NestView.NestEntrance, DeliverRadius)) return;
            int leaves = 0, twigs = 0, honey = 0, crystals = 0;
            foreach (var k in _cargo)
            {
                if (k == Kind.Leaf) leaves++;
                else if (k == Kind.Twig) twigs++;
                else if (k == Kind.Honey) honey++;
                else if (k == Kind.Crystal) crystals++;
            }
            var got = Game.CompleteDelivery(leaves, twigs, honey, crystals);
            Game.Data.statDeliveries++;
            Game.Data.statHoney += honey;
            Game.Data.statCrystals += crystals;
            _cargo.Clear();
            Say($"+{got.leaves} hojas  +{got.twigs} ramas" + (got.pieces > 0 ? $"  +{got.pieces} piezas" : ""));
            Sfx.Play(Sfx.Id.Deliver);
            Haptics.Bump();
            _ant.GetComponent<AntRig>().Bounce();
            Juice.Popup(_root, NestView.NestEntrance + Vector3.up * 1.2f, $"+{got.leaves + got.twigs + got.pieces}", UiKit.Gold, 60);
            RefreshCargo();
            RefreshTexts();
        }

        void AnimateItems()
        {
            foreach (var it in _items)
            {
                float grow = Mathf.Clamp01((Time.time - it.born) / 0.3f);
                float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 4f + it.phase);
                it.go.transform.localScale = Vector3.one * (grow * pulse);
                if (it.kind == Kind.Gem) it.go.transform.Rotate(0f, 90f * Time.deltaTime, 0f);
            }
        }

        // ---------------- Interacciones con los obstaculos ----------------
        void Interact()
        {
            if (_guard != null && Flat(_ant.position - WorldLayout.GuardPos).magnitude < InteractDistance)
            {
                if (_role == AntRoles.Soldado) StartBattle();
                else Warn("Un escarabajo cierra el paso del norte. Solo las soldado pueden con el.");
            }
            if (!Game.Data.tunnelBuilt && Flat(_ant.position - WorldLayout.TunnelPos).magnitude < InteractDistance)
            {
                if (_role != AntRoles.Constructora) Warn($"Aqui hace falta un tunel. Las constructoras lo levantan ({Game.TunnelTwigCost} ramas).");
                else if (Game.Data.twigs < Game.TunnelTwigCost) Warn($"Faltan ramas: hacen falta {Game.TunnelTwigCost}. La obrera las trae.");
                else StartBuild();
            }
        }

        void Warn(string text)
        {
            if (Time.time < _warnUntil) return;
            _warnUntil = Time.time + 5f;
            Say(text);
        }

        void SetWorldVisible(bool visible)
        {
            _world.gameObject.SetActive(visible);
            _root.gameObject.SetActive(visible);
        }

        void StartBattle()
        {
            _busy = true;
            _run.SetInput(Vector2.zero);
            SetWorldVisible(false);
            SoldierGame.Open(_root.parent, () =>
            {
                SetWorldVisible(true);
                _busy = false;
                _ant.position = WorldLayout.GuardPos + new Vector3(0f, 0f, -6f);
                RefreshAll();
            }, 2, 1, victory =>
            {
                if (!victory) { Say("El escarabajo sigue ahi. Vuelve a intentarlo."); return; }
                ClearGuard("Camino del norte despejado!");
            });
        }

        void StartBuild()
        {
            if (!Game.TryPayTwigs(Game.TunnelTwigCost)) return;
            Game.Data.npcWork[1] = 0f;
            _busy = true;
            _run.SetInput(Vector2.zero);
            SetWorldVisible(false);
            BuilderGame.Open(_root.parent, () =>
            {
                SetWorldVisible(true);
                _busy = false;
                _ant.position = WorldLayout.TunnelPos + new Vector3(-6f, 0f, 0f);
                RefreshAll();
            }, floors =>
            {
                if (floors >= 4)
                {
                    OpenTunnel("Tunel abierto! Ya se puede pasar al este.");
                }
                else
                {
                    Game.AddTwigs(Game.TunnelTwigCost / 2);
                    Say($"La obra se vino abajo. Recuperas {Game.TunnelTwigCost / 2} ramas.");
                }
            });
        }

        // ---------------- Cambio de rol ----------------
        void OpenRoleSwitch()
        {
            if (_busy) return;
            _busy = true;
            _run.SetInput(Vector2.zero);
            SetWorldVisible(false);
            RoleScreen.Open(_root.parent, SwitchRole, () =>
            {
                SetWorldVisible(true);
                _busy = false;
            }, _role, Game.RoleSwitchCost);
        }

        void SwitchRole(int role)
        {
            if (role == _role) return;
            if (!Game.TryPayCoins(Game.RoleSwitchCost))
            {
                Say($"Necesitas {Game.RoleSwitchCost} monedas para cambiar de rol");
                return;
            }
            Game.SetRole(role);
            int old = _role;
            _role = role;
            _cargo.Clear();
            SpawnPlayer(_ant.position, _ant.rotation);
            SwapNpcs(old, role);
            RefreshAll();
            Say($"Ahora eres {AntRoles.All[role].Name}");
            Sfx.Play(Sfx.Id.Swoosh);
            Haptics.Tap();
            _ant.GetComponent<AntRig>().Bounce();
        }

        // ---------------- UI ----------------
        void Say(string text)
        {
            _msg.text = text;
            _msgUntil = Time.time + 3f;
        }

        // Cumple las misiones terminadas y actualiza el cartel.
        void CheckQuest()
        {
            var done = Quests.CompleteFinished();
            if (done != null)
            {
                Say($"Mision completada: {done.Title}! +{done.Reward} monedas");
                Sfx.Play(Sfx.Id.Fanfare);
                Haptics.Big();
                Juice.Popup(_root, _ant.position, $"+{done.Reward}", UiKit.Gold, 64);
                Game.Save();
            }
            RefreshQuest();
        }

        void RefreshQuest()
        {
            var q = Quests.Current;
            _questBox.SetActive(q != null);
            if (q == null) return;
            string role = q.Role == _role ? "" : $"  -  cambia a {AntRoles.All[q.Role].Name}";
            _questText.text = $"Mision: {q.Title}\n{q.Progress()}/{q.Target}{role}";
        }

        void RefreshAll()
        {
            RefreshQuest();
            RefreshCargo();
            RefreshTexts();
            RefreshHint();
        }

        void RefreshCargo()
        {
            for (int i = 0; i < MaxCargoViews; i++)
            {
                bool has = _role == AntRoles.Obrera && i < _cargo.Count;
                _cargoViews[i].gameObject.SetActive(has);
                if (has) _cargoViews[i].GetComponent<MeshRenderer>().sharedMaterial = CargoMaterial(_cargo[i]);
            }
        }

        void RefreshTexts()
        {
            var r = AntRoles.All[_role];
            _left.text = r.Name;
            int found = 0;
            foreach (bool b in Game.Data.poiFound) if (b) found++;
            _right.text = $"Sitios {found}/{Game.Data.poiFound.Length}";
            switch (_role)
            {
                case AntRoles.Obrera: _center.text = $"Carga {_cargo.Count}/{Game.ForageCapacity}"; break;
                case AntRoles.Exploradora: _center.text = $"Niebla {Mathf.RoundToInt(100f * Game.Data.revealed.Count / Mathf.Max(1, FogGrid.TotalFogged))}%"; break;
                case AntRoles.Constructora: _center.text = Game.Data.tunnelBuilt ? "Tunel listo" : $"Ramas {Game.Data.twigs}/{Game.TunnelTwigCost}"; break;
                default: _center.text = Game.Data.guardDefeated ? "Paso libre" : "Bicho en el paso"; break;
            }
        }

        void RefreshHint()
        {
            if (!_run.HasMoved) { _hint.text = "Manten el dedo y arrastra para moverte."; return; }
            switch (_role)
            {
                case AntRoles.Obrera: _hint.text = "Recoge recursos y llevalos al nido. Sin prisa."; break;
                case AntRoles.Exploradora: _hint.text = "La niebla se deshace a tu paso. Busca sitios y hallazgos."; break;
                case AntRoles.Constructora:
                    _hint.text = Game.Data.tunnelBuilt ? "El tunel del este esta abierto." : "Con ramas, levanta el tunel junto al muro del este."; break;
                default:
                    _hint.text = Game.Data.guardDefeated ? "El paso del norte esta libre." : "Vence bailando al escarabajo del paso norte."; break;
            }
        }

        void Finish()
        {
            Game.Save();
            _onClose?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_world != null) Destroy(_world.gameObject);
            _chase?.Restore();
            Walls.Clear();
            Blockers.Clear();
        }

        // ---------------- Ayudas para las capturas automaticas ----------------
        internal void DebugTeleport(Vector3 pos) => _ant.position = pos;
        internal void DebugBattle() => StartBattle();
        internal void DebugBuild() => StartBuild();
        internal void DebugNpcWork(float soldier, float builder) { Game.Data.npcWork[0] = soldier; Game.Data.npcWork[1] = builder; }
    }
}
