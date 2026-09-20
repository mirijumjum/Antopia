using System;
using UnityEngine;
using UnityEngine.UI;

namespace Antopia
{
    // Punto de entrada: monta la escena, el HUD y los paneles. Se crea desde una escena casi vacia.
    public class GameController : MonoBehaviour
    {
        RectTransform _safe, _actions;
        Text _coins, _leaves, _twigs, _pieces, _ants, _toast;
        Button _dailyBtn, _playBtn, _sellBtn;
        GameObject _overlay;
        Action<RectTransform> _overlayBuild;
        float _toastUntil;
        float _dayCheck;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Game.Load();
            UiKit.CreateCanvas(out _safe);
            BuildHud();
            gameObject.AddComponent<NestView>();
            Game.Changed += RefreshHud;
        }

        void Start()
        {
            RefreshHud();
            if (!string.IsNullOrEmpty(Game.OfflineMessage)) Toast(Game.OfflineMessage, 6f);
            else Toast("Bienvenida a Antopia", 3f);
        }

        void OnDestroy()
        {
            Game.Changed -= RefreshHud;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Game.Save();
            else
            {
                Game.OnResume();
                if (!string.IsNullOrEmpty(Game.OfflineMessage)) Toast(Game.OfflineMessage, 6f);
            }
        }

        void OnApplicationQuit()
        {
            Game.Save();
        }

        void Update()
        {
            if (_toast != null && _toast.gameObject.activeSelf && Time.time > _toastUntil)
                _toast.gameObject.SetActive(false);

            _dayCheck += Time.deltaTime;
            if (_dayCheck > 30f)
            {
                _dayCheck = 0f;
                Game.RefreshDay();
            }
        }

        // ---------------- HUD ----------------
        void BuildHud()
        {
            UiKit.Box(_safe, "HudBg", new Color(0.12f, 0.09f, 0.06f, 0.85f), 0f, 0.915f, 1f, 1f);
            _coins = UiKit.Label(_safe, "", 42, TextAnchor.MiddleCenter, UiKit.Gold, 0.00f, 0.955f, 0.25f, 1f);
            _leaves = UiKit.Label(_safe, "", 42, TextAnchor.MiddleCenter, UiKit.Leaf, 0.25f, 0.955f, 0.50f, 1f);
            _twigs = UiKit.Label(_safe, "", 42, TextAnchor.MiddleCenter, new Color(0.85f, 0.62f, 0.38f), 0.50f, 0.955f, 0.75f, 1f);
            _pieces = UiKit.Label(_safe, "", 42, TextAnchor.MiddleCenter, UiKit.Cream, 0.75f, 0.955f, 1f, 1f);
            _ants = UiKit.Label(_safe, "", 34, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.915f, 0.95f, 0.955f);

            var toastBg = UiKit.Box(_safe, "ToastBg", new Color(0f, 0f, 0f, 0.6f), 0.05f, 0.84f, 0.95f, 0.91f);
            toastBg.raycastTarget = false;
            _toast = UiKit.Label(toastBg.transform, "", 36, TextAnchor.MiddleCenter, UiKit.Cream, 0.02f, 0f, 0.98f, 1f);
            _toast.gameObject.SetActive(false);

            _actions = UiKit.Rect(_safe, "Actions", 0f, 0f, 1f, 1f);
            _playBtn = UiKit.MakeButton(_actions, "", UiKit.Leaf, 46, PlaySelectedRole, 0.03f, 0.14f, 0.66f, 0.27f, "Icon37");
            UiKit.MakeButton(_actions, "Hormigas\n(cambiar rol)", UiKit.Cream, 42, OpenRoles, 0.68f, 0.14f, 0.97f, 0.27f, "Icon03");
            UiKit.MakeButton(_actions, "Nido\n(mejoras)", UiKit.Cream, 42, OpenNest, 0.03f, 0.01f, 0.34f, 0.13f, "Icon02");
            _dailyBtn = UiKit.MakeButton(_actions, "", UiKit.Gold, 42, OpenDaily, 0.35f, 0.01f, 0.66f, 0.13f, "Icon08");
            _sellBtn = UiKit.MakeButton(_actions, "", UiKit.Gold, 42, () =>
            {
                int gain = Game.Data.leaves * Game.LeafPrice;
                Game.SellLeaves();
                if (gain > 0) Toast($"Vendiste hojas por {gain} monedas", 3f);
            }, 0.67f, 0.01f, 0.97f, 0.13f, "Icon54");
        }

        void RefreshHud()
        {
            var d = Game.Data;
            _coins.text = $"Monedas\n{d.coins}";
            _leaves.text = $"Hojas\n{d.leaves}";
            _twigs.text = $"Ramas\n{d.twigs}";
            _pieces.text = $"Piezas\n{d.pieces}";
            _ants.text = $"Hormigas pasivas: {Game.Ants}   Hoja = {Game.LeafPrice} mon.";
            var role = AntRoles.All[d.role];
            UiKit.SetButtonColor(_playBtn, role.Ui);
            UiKit.SetButtonText(_playBtn, d.role == AntRoles.Constructora
                ? $"JUGAR: {role.Name}\nconstruir ({Game.BuildTwigCost} ramas)"
                : $"JUGAR: {role.Name}\n{(role.HasGame ? role.Verb : "modo de prueba")}");
            int pending = Game.DailyPending();
            UiKit.SetButtonText(_dailyBtn, pending > 0 ? $"Diarias\n(!) {pending} listas" : "Diarias");
            UiKit.SetButtonText(_sellBtn, $"Vender\nhojas ({d.leaves * Game.LeafPrice})");
            if (_overlay != null && _overlayBuild != null) RebuildOverlay();
        }

        void Toast(string msg, float seconds)
        {
            _toast.text = msg;
            _toast.gameObject.SetActive(true);
            _toastUntil = Time.time + seconds;
        }

        // ---------------- Roles ----------------
        void PlayRole(int role)
        {
            switch (role)
            {
                case AntRoles.Obrera: OpenForager(); break;
                case AntRoles.Constructora: OpenBuilder(); break;
                default: OpenRoam(role); break;
            }
        }

        void PlaySelectedRole() => PlayRole(Game.Data.role);

        void OpenRoles()
        {
            CloseOverlay();
            _actions.gameObject.SetActive(false);
            RoleScreen.Open(_safe, PlayRole, () => _actions.gameObject.SetActive(true));
        }

        void OpenRoam(int role)
        {
            CloseOverlay();
            _actions.gameObject.SetActive(false);
            RoamGame.Open(_safe, role, () => _actions.gameObject.SetActive(true));
        }

        // ---------------- Minijuegos ----------------
        void OpenForager()
        {
            CloseOverlay();
            _actions.gameObject.SetActive(false); // deja ver el mundo durante la expedicion
            ForagerGame.Open(_safe, () =>
            {
                _actions.gameObject.SetActive(true);
                Toast("Expedicion terminada", 3f);
            });
        }

        void OpenBuilder()
        {
            CloseOverlay();
            if (!Game.TryStartBuild())
            {
                Toast($"Necesitas {Game.BuildTwigCost} ramas. Recolectalas con la obrera.", 4f);
                return;
            }
            _actions.gameObject.SetActive(false);
            BuilderGame.Open(_safe, () =>
            {
                _actions.gameObject.SetActive(true);
                Toast("Obra completada", 3f);
            });
        }

        // ---------------- Paneles ----------------
        void OpenOverlay(Action<RectTransform> build)
        {
            _overlayBuild = build;
            RebuildOverlay();
        }

        void RebuildOverlay()
        {
            if (_overlay != null) Destroy(_overlay);
            var rt = UiKit.Rect(_safe, "Overlay", 0f, 0f, 1f, 0.915f);
            _overlay = rt.gameObject;
            UiKit.Frame(rt, "Bg", UiKit.Skin.Orange, 0, 0, 1, 1);
            _overlayBuild(rt);
        }

        void CloseOverlay()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
            _overlayBuild = null;
        }

        void OpenNest() => OpenNest(0);

        // tab 0 = edificios del nido (monedas + piezas), tab 1 = mejoras de la obrera (monedas + ramas).
        void OpenNest(int tab)
        {
            OpenOverlay(rt =>
            {
                UiKit.MakeButton(rt, "Edificios", tab == 0 ? UiKit.Gold : UiKit.Disabled, 46, () => OpenNest(0), 0.04f, 0.90f, 0.49f, 0.99f);
                UiKit.MakeButton(rt, "Obrera", tab == 1 ? UiKit.Gold : UiKit.Disabled, 46, () => OpenNest(1), 0.51f, 0.90f, 0.96f, 0.99f);
                UiKit.Label(rt, tab == 0
                        ? "Cuestan monedas (vende hojas) y piezas (la constructora las hace)."
                        : "Cuestan monedas y ramas. Mejoran la expedicion de la obrera.",
                    34, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.85f, 0.95f, 0.90f);
                if (tab == 0) BuildBuildingRows(rt);
                else BuildForageRows(rt);
                UiKit.MakeButton(rt, "Cerrar", UiKit.Cream, 46, CloseOverlay, 0.3f, 0.02f, 0.7f, 0.10f);
            });
        }

        void BuildBuildingRows(RectTransform rt)
        {
            for (int i = 0; i < 3; i++)
            {
                int b = i;
                float top = 0.83f - i * 0.24f;
                var row = UiKit.Box(rt, "Row", new Color(1f, 1f, 1f, 0.08f), 0.04f, top - 0.22f, 0.96f, top);
                int lvl = Game.Data.buildingLevels[b];
                UiKit.Label(row.transform, $"{Game.BuildingNames[b]}  (nivel {lvl}/{Game.MaxLevel})", 46,
                    TextAnchor.MiddleLeft, UiKit.Gold, 0.04f, 0.62f, 0.96f, 0.98f);
                UiKit.Label(row.transform, Game.BuildingEffect(b), 34, TextAnchor.MiddleLeft, UiKit.Cream,
                    0.04f, 0.38f, 0.96f, 0.64f);
                bool max = lvl >= Game.MaxLevel;
                string cost = max ? "Nivel maximo" : $"Mejorar: {Game.UpgradeCoinCost(b)} mon. + {Game.UpgradePieceCost(b)} piezas";
                UiKit.MakeButton(row.transform, cost, Game.CanUpgrade(b) ? UiKit.Gold : UiKit.Disabled, 36, () =>
                {
                    if (Game.TryUpgrade(b)) Toast($"{Game.BuildingNames[b]} sube de nivel", 3f);
                    else Toast("Te faltan monedas o piezas", 3f);
                }, 0.04f, 0.04f, 0.96f, 0.36f);
            }
        }

        void BuildForageRows(RectTransform rt)
        {
            for (int i = 0; i < Game.ForageUpgradeNames.Length; i++)
            {
                int u = i;
                float top = 0.83f - i * 0.185f;
                var row = UiKit.Box(rt, "Row", new Color(1f, 1f, 1f, 0.08f), 0.04f, top - 0.17f, 0.96f, top);
                int lvl = Game.Data.forageLevels[u];
                UiKit.Label(row.transform, $"{Game.ForageUpgradeNames[u]}  (nivel {lvl}/{Game.ForageMaxLevel})", 42,
                    TextAnchor.MiddleLeft, UiKit.Gold, 0.04f, 0.64f, 0.96f, 0.98f);
                UiKit.Label(row.transform, Game.ForageEffect(u), 32, TextAnchor.MiddleLeft, UiKit.Cream,
                    0.04f, 0.36f, 0.96f, 0.64f);
                bool max = lvl >= Game.ForageMaxLevel;
                string cost = max ? "Nivel maximo" : $"Mejorar: {Game.ForageCoinCost(u)} mon. + {Game.ForageTwigCost(u)} ramas";
                UiKit.MakeButton(row.transform, cost, Game.CanUpgradeForage(u) ? UiKit.Gold : UiKit.Disabled, 34, () =>
                {
                    if (Game.TryUpgradeForage(u)) Toast($"{Game.ForageUpgradeNames[u]} sube de nivel", 3f);
                    else Toast("Te faltan monedas o ramas", 3f);
                }, 0.04f, 0.03f, 0.96f, 0.34f);
            }
        }

        void OpenDaily()
        {
            Game.RefreshDay();
            OpenOverlay(rt =>
            {
                UiKit.Label(rt, "TAREAS DIARIAS", 60, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.90f, 0.95f, 0.99f);
                UiKit.Label(rt, "Se reinician cada dia. Unos 40 minutos de juego las completan.",
                    34, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.85f, 0.95f, 0.90f);
                for (int i = 0; i < 3; i++)
                {
                    int t = i;
                    float top = 0.83f - i * 0.24f;
                    var row = UiKit.Box(rt, "Row", new Color(1f, 1f, 1f, 0.08f), 0.04f, top - 0.22f, 0.96f, top);
                    UiKit.Label(row.transform, Game.DailyTitles[t], 40, TextAnchor.MiddleLeft, UiKit.Cream,
                        0.04f, 0.62f, 0.96f, 0.98f);
                    UiKit.Label(row.transform, $"Progreso: {Game.DailyProgress(t)} / {Game.DailyTargets[t]}", 38,
                        TextAnchor.MiddleLeft, UiKit.Gold, 0.04f, 0.38f, 0.96f, 0.64f);
                    bool claimed = Game.Data.dailyClaimed[t];
                    bool ready = Game.DailyDone(t) && !claimed;
                    string txt = claimed ? "Reclamado" : ready ? $"Reclamar {Game.DailyRewards[t]} monedas" : $"Premio: {Game.DailyRewards[t]} monedas";
                    UiKit.MakeButton(row.transform, txt, ready ? UiKit.Gold : UiKit.Disabled, 38, () =>
                    {
                        if (Game.ClaimDaily(t)) Toast($"+{Game.DailyRewards[t]} monedas", 3f);
                    }, 0.04f, 0.04f, 0.96f, 0.36f);
                }
                UiKit.MakeButton(rt, "Cerrar", UiKit.Cream, 46, CloseOverlay, 0.3f, 0.02f, 0.7f, 0.10f);
            });
        }
    }
}
