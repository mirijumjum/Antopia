using System;
using UnityEngine;
using UnityEngine.UI;

namespace Antopia
{
    // Punto de entrada: monta la escena, el HUD y los paneles. Se crea desde una escena casi vacia.
    public class GameController : MonoBehaviour
    {
        RectTransform _safe;
        Text _coins, _leaves, _twigs, _pieces, _ants, _toast;
        Button _dailyBtn, _buildBtn, _sellBtn;
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

            UiKit.MakeButton(_safe, "OBRERA\nrecolectar", UiKit.Leaf, 46, OpenForager, 0.03f, 0.14f, 0.49f, 0.27f);
            _buildBtn = UiKit.MakeButton(_safe, "", UiKit.Twig, 46, OpenBuilder, 0.51f, 0.14f, 0.97f, 0.27f);
            UiKit.MakeButton(_safe, "Nido\n(mejoras)", UiKit.Cream, 42, OpenNest, 0.03f, 0.01f, 0.34f, 0.13f);
            _dailyBtn = UiKit.MakeButton(_safe, "", UiKit.Gold, 42, OpenDaily, 0.35f, 0.01f, 0.66f, 0.13f);
            _sellBtn = UiKit.MakeButton(_safe, "", UiKit.Gold, 42, () =>
            {
                int gain = Game.Data.leaves * Game.LeafPrice;
                Game.SellLeaves();
                if (gain > 0) Toast($"Vendiste hojas por {gain} monedas", 3f);
            }, 0.67f, 0.01f, 0.97f, 0.13f);
        }

        void RefreshHud()
        {
            var d = Game.Data;
            _coins.text = $"Monedas\n{d.coins}";
            _leaves.text = $"Hojas\n{d.leaves}";
            _twigs.text = $"Ramas\n{d.twigs}";
            _pieces.text = $"Piezas\n{d.pieces}";
            _ants.text = $"Hormigas pasivas: {Game.Ants}   Hoja = {Game.LeafPrice} mon.";
            UiKit.SetButtonText(_buildBtn, $"CONSTRUCTORA\nconstruir ({Game.BuildTwigCost} ramas)");
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

        // ---------------- Minijuegos ----------------
        void OpenForager()
        {
            CloseOverlay();
            ForagerGame.Open(_safe, () => Toast("Recolecta completada", 3f));
        }

        void OpenBuilder()
        {
            CloseOverlay();
            if (!Game.TryStartBuild())
            {
                Toast($"Necesitas {Game.BuildTwigCost} ramas. Recolectalas con la obrera.", 4f);
                return;
            }
            BuilderGame.Open(_safe, () => Toast("Obra completada", 3f));
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
            UiKit.Box(rt, "Bg", UiKit.Panel, 0, 0, 1, 1);
            _overlayBuild(rt);
        }

        void CloseOverlay()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
            _overlayBuild = null;
        }

        void OpenNest()
        {
            OpenOverlay(rt =>
            {
                UiKit.Label(rt, "NIDO: mejoras", 60, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.90f, 0.95f, 0.99f);
                UiKit.Label(rt, "Cuestan monedas (vende hojas) y piezas (la constructora las hace).",
                    34, TextAnchor.MiddleCenter, UiKit.Cream, 0.05f, 0.85f, 0.95f, 0.90f);
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
                    var btn = UiKit.MakeButton(row.transform, cost, Game.CanUpgrade(b) ? UiKit.Gold : UiKit.Disabled, 36, () =>
                    {
                        if (Game.TryUpgrade(b)) Toast($"{Game.BuildingNames[b]} sube de nivel", 3f);
                        else Toast("Te faltan monedas o piezas", 3f);
                    }, 0.04f, 0.04f, 0.96f, 0.36f);
                }
                UiKit.MakeButton(rt, "Cerrar", UiKit.Cream, 46, CloseOverlay, 0.3f, 0.02f, 0.7f, 0.10f);
            });
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
