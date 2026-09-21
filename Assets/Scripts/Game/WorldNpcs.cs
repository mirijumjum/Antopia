using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Antopia.AntRunner;
using Random = UnityEngine.Random;

namespace Antopia
{
    // Escuadras NPC del mundo: cada rol que NO juegas tiene ayudantes (1 + nivel de Tuneles / 4) que hacen su trabajo
    // a su ritmo, en el mundo y a la vista, sin que tengas que estar en ese rol:
    //   Obrera      recoge recursos por las zonas abiertas y los entrega al nido (sin bonus de la Despensa).
    //   Exploradora recorre la niebla mas cercana y la deshace (radio pequeno) descubriendo sitios.
    //   Constructora va al muro del este y, con ramas, levanta el tunel poco a poco (Game.NpcBuildSeconds).
    //   Soldado     baila delante del escarabajo del norte hasta despejar el paso (Game.NpcSoldierSeconds).
    // Jugar tu rol es siempre mas rapido y es lo unico que cuenta para las cifras de las misiones (recoger, entregar...).
    public partial class WorldGame
    {
        class Npc
        {
            public int role, idx;
            public Transform ant;
            public AntRunner run;
            public AntRig rig;
            public readonly List<Kind> cargo = new List<Kind>();
            public Item item;
            public bool returning;
            public int cell = -1;
            public Vector3 wander;
            public float wanderT, bounceT;
        }

        const int NpcCapacity = 3;
        const float NpcReveal = 4.5f;
        const float LaneOffset = 1.6f; // distancia antes y despues de un hueco del muro

        readonly List<Npc> _npcs = new List<Npc>();
        Text _npcTag;
        Npc _tagOwner;
        string _tagText = "";

        // ---------------- Montaje ----------------
        void BuildNpcs()
        {
            _npcTag = UiKit.Outlined(UiKit.Label(_root, "", 30, TextAnchor.MiddleCenter, UiKit.Cream, 0f, 0f, 0.5f, 0.05f));
            _npcTag.fontStyle = FontStyle.Bold;
            _npcTag.gameObject.SetActive(false);
            for (int r = 0; r < AntRoles.All.Length; r++)
                if (r != _role) AddSquad(r, null);
        }

        void AddSquad(int role, Vector3? at)
        {
            int count = Game.Helpers;
            for (int i = 0; i < count; i++)
            {
                var pos = at.HasValue ? at.Value + new Vector3((i - (count - 1) / 2f) * 1.3f, 0f, -1.4f) : NpcSpawn(role, i);
                var t = AntModel.Create("Npc", AntRoles.All[role].Variant, _world);
                t.localScale = Vector3.one * 0.82f;
                t.position = pos;
                t.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float speed = role == AntRoles.Obrera ? 0.8f : role == AntRoles.Exploradora ? 0.95f : 0.85f;
                _npcs.Add(new Npc { role = role, idx = i, ant = t, run = new AntRunner(t, speed), rig = t.GetComponent<AntRig>(), wanderT = Random.Range(0f, 2f) });
            }
        }

        // Punto de salida alrededor del nido, libre de edificios.
        static Vector3 NpcSpawn(int role, int i)
        {
            float a = (role * 3 + i) * 0.85f + 0.4f;
            float radius = 6.8f;
            var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius + 0.5f);
            for (int k = 0; k < 4 && !IsClear(p, 0.9f); k++)
            {
                radius += 1.2f;
                p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius + 0.5f);
            }
            return p;
        }

        // Al cambiar de rol, la escuadra del nuevo rol desaparece (ahora eres tu) y aparece la del rol que dejas.
        void SwapNpcs(int oldRole, int newRole)
        {
            for (int i = _npcs.Count - 1; i >= 0; i--)
            {
                if (_npcs[i].role != newRole) continue;
                if (_npcs[i].item != null) _npcs[i].item.claim = null;
                Destroy(_npcs[i].ant.gameObject);
                _npcs.RemoveAt(i);
            }
            if (_tagOwner != null && !_npcs.Contains(_tagOwner)) _tagOwner = null;
            AddSquad(oldRole, _ant.position);
        }

        // ---------------- Bucle ----------------
        void UpdateNpcs(float dt)
        {
            _tagOwner = null;
            foreach (var n in _npcs)
            {
                switch (n.role)
                {
                    case AntRoles.Obrera: NpcForage(n, dt); break;
                    case AntRoles.Exploradora: NpcExplore(n, dt); break;
                    case AntRoles.Constructora: NpcBuild(n, dt); break;
                    default: NpcFight(n, dt); break;
                }
            }
        }

        // El cartel lo lleva la hormiga que trabaja mas cerca del jugador.
        void OfferTag(Npc n, string text)
        {
            if (_tagOwner != null && Flat(n.ant.position - _ant.position).sqrMagnitude >= Flat(_tagOwner.ant.position - _ant.position).sqrMagnitude) return;
            _tagOwner = n;
            _tagText = text;
        }

        // Cartel con el avance de la obra o del baile, sobre la primera hormiga que trabaja en ello.
        void PlaceNpcTag()
        {
            if (_npcTag == null) return;
            var cam = Camera.main;
            bool show = false;
            if (_tagOwner != null && cam != null)
            {
                // Debajo de la hormiga, para no chocar con los carteles de arriba.
                var vp = cam.WorldToViewportPoint(_tagOwner.ant.position + new Vector3(0f, 0f, -0.6f));
                if (vp.z > 0f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.12f && vp.y < 0.75f)
                {
                    show = true;
                    _npcTag.text = _tagText;
                    _npcTag.rectTransform.anchorMin = new Vector2(vp.x - 0.3f, vp.y - 0.045f);
                    _npcTag.rectTransform.anchorMax = new Vector2(vp.x + 0.3f, vp.y);
                }
            }
            if (_npcTag.gameObject.activeSelf != show) _npcTag.gameObject.SetActive(show);
        }

        // ---------------- Obrera ----------------
        void NpcForage(Npc n, float dt)
        {
            if (n.returning)
            {
                if (!Steer(n, NestView.NestEntrance, DeliverRadius - 0.3f, dt)) return;
                NpcDeliver(n);
                return;
            }
            if (n.item != null && !_items.Contains(n.item)) n.item = null;
            if (n.item == null) n.item = NpcFindItem(n);
            if (n.item == null)
            {
                if (n.cargo.Count > 0) n.returning = true;
                else Wander(n, dt);
                return;
            }
            var target = n.item.go.transform.position;
            var goal = Route(n.ant.position, target);
            bool final = goal == target;
            if (!Steer(n, goal, final ? 0.55f : 0.6f, dt) || !final) return;

            _items.Remove(n.item);
            Destroy(n.item.go);
            n.cargo.Add(n.item.kind);
            n.item = null;
            if (n.cargo.Count >= NpcCapacity) n.returning = true;
        }

        Item NpcFindItem(Npc n)
        {
            Item best = null;
            float bd = float.MaxValue;
            foreach (var it in _items)
            {
                if (it.kind == Kind.Gem || !ZoneOpen(it.zone) || (it.claim != null && it.claim != n)) continue;
                float d = Flat(it.go.transform.position - n.ant.position).sqrMagnitude;
                if (d < bd) { bd = d; best = it; }
            }
            if (best != null) best.claim = n;
            return best;
        }

        void NpcDeliver(Npc n)
        {
            int leaves = 0, twigs = 0, honey = 0, crystals = 0;
            foreach (var k in n.cargo)
            {
                if (k == Kind.Leaf) leaves++;
                else if (k == Kind.Twig) twigs++;
                else if (k == Kind.Honey) honey++;
                else if (k == Kind.Crystal) crystals++;
            }
            n.cargo.Clear();
            n.returning = false;
            var got = Game.NpcDelivery(leaves, twigs, honey, crystals);
            n.rig?.Bounce();
            Sfx.Play(Sfx.Id.Deliver, 0.3f, Random.Range(1.05f, 1.25f));
            Juice.Popup(_root, NestView.NestEntrance + Vector3.up * 1.0f, $"+{got.leaves + got.twigs + got.pieces}", UiKit.Cream, 30);
            RefreshTexts();
        }

        // ---------------- Exploradora ----------------
        void NpcExplore(Npc n, float dt)
        {
            if (n.cell >= 0 && !_fog.IsFogged(n.cell)) n.cell = -1;
            if (n.cell < 0) n.cell = _fog.NearestFogged(n.ant.position, i => ZoneOpen(WorldLayout.ZoneOf(FogGrid.Center(i))));
            if (n.cell < 0)
            {
                Wander(n, dt);
                return;
            }
            var target = FogGrid.Center(n.cell);
            var goal = Route(n.ant.position, target);
            Steer(n, goal, 0.6f, dt);
            RevealAt(n.ant.position, NpcReveal, true);
        }

        // ---------------- Constructora ----------------
        void NpcBuild(Npc n, float dt)
        {
            if (Game.Data.tunnelBuilt)
            {
                Wander(n, dt);
                return;
            }
            bool started = Game.Data.npcWork[1] > 0f;
            if (!started && Game.Data.twigs < Game.TunnelTwigCost)
            {
                Wander(n, dt);
                return;
            }
            var site = WorldLayout.TunnelPos + new Vector3(-2.6f, 0f, 1.5f - n.idx * 1.4f);
            if (!Steer(n, Route(n.ant.position, site), 0.4f, dt)) return;

            if (!started)
            {
                if (!Game.TryPayTwigs(Game.TunnelTwigCost)) return;
                Game.Data.npcWork[1] = 0.001f;
            }
            Face(n, WorldLayout.TunnelPos, dt);
            Game.Data.npcWork[1] += dt;
            n.bounceT -= dt;
            if (n.bounceT <= 0f)
            {
                n.bounceT = 0.7f;
                n.rig?.Bounce();
                if (n.idx == 0) Sfx.Play(Sfx.Id.Thump, 0.2f, Random.Range(0.9f, 1.1f));
            }
            OfferTag(n, $"Construyendo tunel {Mathf.RoundToInt(100f * Game.Data.npcWork[1] / Game.NpcBuildSeconds)}%");
            if (Game.Data.npcWork[1] >= Game.NpcBuildSeconds) OpenTunnel("Tus constructoras han abierto el tunel del este!");
        }

        // ---------------- Soldado ----------------
        void NpcFight(Npc n, float dt)
        {
            if (_guard == null)
            {
                Wander(n, dt);
                return;
            }
            int count = Game.Helpers;
            var site = WorldLayout.GuardPos + new Vector3((n.idx - (count - 1) / 2f) * 1.4f, 0f, -3.4f);
            if (!Steer(n, Route(n.ant.position, site), 0.4f, dt)) return;

            Face(n, WorldLayout.GuardPos, dt);
            Game.Data.npcWork[0] += dt;
            n.bounceT -= dt;
            if (n.bounceT <= 0f)
            {
                n.bounceT = Random.Range(0.6f, 1.0f);
                n.rig?.Bounce();
                if (n.idx == 0) Sfx.Play(Sfx.Id.Boing, 0.2f, Random.Range(0.9f, 1.2f));
            }
            _guard.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 9f) * 5f);
            OfferTag(n, $"Baile de guerra {Mathf.RoundToInt(100f * Game.Data.npcWork[0] / Game.NpcSoldierSeconds)}%");
            if (Game.Data.npcWork[0] >= Game.NpcSoldierSeconds) ClearGuard("Tus soldados han vencido al escarabajo del norte!");
        }

        // ---------------- Acciones compartidas con el jugador ----------------
        void ClearGuard(string message)
        {
            Game.Data.guardDefeated = true;
            Game.Data.npcWork[0] = 0f;
            Blockers.Clear();
            if (_guard != null) Destroy(_guard.gameObject);
            _guard = null;
            Game.Save();
            Say(message);
            Sfx.Play(Sfx.Id.Fanfare);
            RefreshAll();
        }

        void OpenTunnel(string message)
        {
            Game.Data.tunnelBuilt = true;
            Game.Data.npcWork[1] = 0f;
            Walls.Clear();
            Walls.AddRange(WorldLayout.BuildWalls(true, out _));
            if (WorldDecor.PlugRoot != null) Destroy(WorldDecor.PlugRoot.gameObject);
            if (_sign != null) Destroy(_sign.gameObject);
            BuildArch();
            Game.Save();
            Say(message);
            Sfx.Play(Sfx.Id.Fanfare);
            CamShake.Trigger(0.2f, 0.4f);
            RefreshAll();
        }

        // ---------------- Movimiento ----------------
        // Avanza hacia "goal"; devuelve true al llegar a "stop" metros. Esquiva el nido y los edificios.
        bool Steer(Npc n, Vector3 goal, float stop, float dt)
        {
            var pos = n.ant.position;
            var d = Flat(goal - pos);
            if (d.magnitude <= stop)
            {
                n.run.SetInput(Vector2.zero);
                n.run.Move(dt);
                return true;
            }
            var dir = d.normalized;
            dir = Avoid(dir, pos, NestView.NestEntrance, MoundRadius, goal, d.magnitude);
            for (int i = 0; i < 3; i++) dir = Avoid(dir, pos, NestView.BuildingPos[i], BuildingRadius(i), goal, d.magnitude);
            n.run.SetInput(new Vector2(dir.x, dir.z));
            n.run.Move(dt);
            return false;
        }

        static Vector3 Avoid(Vector3 dir, Vector3 pos, Vector3 c, float radius, Vector3 goal, float goalDist)
        {
            if (Flat(goal - c).magnitude < radius + 1.5f) return dir; // el destino esta pegado al obstaculo
            var to = Flat(c - pos);
            float dist = to.magnitude;
            float reach = radius + 2.2f;
            if (dist > reach || dist > goalDist + radius) return dir;
            var toN = to / Mathf.Max(dist, 0.01f);
            if (Vector3.Dot(dir, toN) < 0.2f) return dir;
            var side = new Vector3(-toN.z, 0f, toN.x);
            if (Vector3.Dot(side, dir) < 0f) side = -side;
            float k = Mathf.Clamp01((reach - dist) / 1.5f) * 2.5f;
            return (dir + side * k).normalized;
        }

        void Face(Npc n, Vector3 at, float dt)
        {
            var d = Flat(at - n.ant.position);
            if (d.sqrMagnitude < 0.01f) return;
            n.ant.rotation = Quaternion.RotateTowards(n.ant.rotation, Quaternion.LookRotation(d), 360f * dt);
        }

        // Pasea sin rumbo por su zona cuando no tiene nada que hacer.
        void Wander(Npc n, float dt)
        {
            n.wanderT -= dt;
            if (n.wanderT <= 0f)
            {
                n.wanderT = Random.Range(3f, 7f);
                var r = WorldLayout.Zones[ZoneNear(n.ant.position)];
                var p = new Vector3(Random.Range(r.xMin + 3f, r.xMax - 3f), 0f, Random.Range(r.yMin + 3f, r.yMax - 3f));
                n.wander = IsClear(p, 1.3f) ? p : Vector3.zero;
            }
            if (n.wander == Vector3.zero || Steer(n, n.wander, 0.5f, dt)) n.run.SetInput(Vector2.zero);
        }

        // Zona en la que esta un punto (si cae dentro de un muro, la mas cercana).
        static int ZoneNear(Vector3 p)
        {
            int z = WorldLayout.ZoneOf(p);
            if (z >= 0) return z;
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < WorldLayout.Zones.Length; i++)
            {
                var r = WorldLayout.Zones[i];
                float dx = Mathf.Max(Mathf.Max(r.xMin - p.x, 0f), p.x - r.xMax);
                float dz = Mathf.Max(Mathf.Max(r.yMin - p.z, 0f), p.z - r.yMax);
                float d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        // Siguiente punto para ir de "from" a "to": directo dentro de una zona, o por el hueco del muro entre zonas.
        static Vector3 Route(Vector3 from, Vector3 to)
        {
            int zf = ZoneNear(from), zt = ZoneNear(to);
            if (zf == zt) return to;
            int outer = zf != 0 ? zf : zt; // la zona exterior que se cruza
            var gap = outer == 1 ? WorldLayout.GuardPos : WorldLayout.TunnelPos;
            var outDir = outer == 1 ? Vector3.forward : Vector3.right;
            float s = zf == 0 ? 1f : -1f; // hacia fuera (+1) o de vuelta al nido (-1)
            var fromSide = gap - s * outDir * LaneOffset;
            var toSide = gap + s * outDir * LaneOffset;
            var rel = from - gap;
            float along = Vector3.Dot(rel, outDir) * s;
            float lateral = (rel - Vector3.Dot(rel, outDir) * outDir).magnitude;
            return along < 0f && lateral > 0.6f ? fromSide : toSide;
        }
    }
}
