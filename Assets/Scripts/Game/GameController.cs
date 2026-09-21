using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Antopia
{
    // Punto de entrada: monta la escena, el HUD y los paneles. Se crea desde una escena casi vacia.
    // Pantalla principal: recursos arriba, el mundo en el centro y abajo un unico boton grande de jugar con
    // cuatro accesos redondos. Los paneles (Nido, Diarias) son tarjetas sobre un marco a pantalla casi completa.
    public class GameController : MonoBehaviour
    {
        RectTransform _safe, _actions;
        Text _coins, _leaves, _twigs, _pieces, _toast, _playTitle, _playSub, _sellCaption, _badgeText;
        Button _playBtn;
        CountText _coinsC, _leavesC, _twigsC, _piecesC;
        bool _hudShown;
        GameObject _badge;
        GameObject _overlay;
        Action<RectTransform> _overlayBuild;
        float _toastUntil;
        float _dayCheck;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Game.Load();
            ShotRunner.StartIfRequested();
            UiKit.CreateCanvas(out _safe);
            BuildHud();
            gameObject.AddComponent<NestView>();
            Game.Changed += RefreshHud;
        }

        void Start()
        {
            RefreshHud();
            Sfx.StartMusic();
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
            if (_toast != null && _toast.transform.parent.gameObject.activeSelf && Time.time > _toastUntil)
                _toast.transform.parent.gameObject.SetActive(false);

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
            // Recursos: una sola barra con cuatro indicadores (icono + cifra).
            UiKit.Pill(_safe, "HudBg", 0.78f, 0.03f, 0.925f, 0.97f, 0.985f);
            _coins = HudStat(0, "Icon47", Color.white);
            _leaves = HudStat(1, "Btn08", Color.white);
            _twigs = HudStat(2, "Btn06", new Color(0.85f, 0.62f, 0.45f));
            _pieces = HudStat(3, "Btn10", Color.white);
            _coinsC = CountText.Attach(_coins);
            _leavesC = CountText.Attach(_leaves);
            _twigsC = CountText.Attach(_twigs);
            _piecesC = CountText.Attach(_pieces);

            var toastBg = UiKit.Pill(_safe, "ToastBg", 0.85f, 0.10f, 0.86f, 0.90f, 0.915f);
            _toast = UiKit.Label(toastBg.transform, "", 30, TextAnchor.MiddleCenter, UiKit.Cream, 0.03f, 0f, 0.97f, 1f);
            toastBg.gameObject.SetActive(false);

            // Abajo: un boton principal y cuatro accesos redondos.
            _actions = UiKit.Rect(_safe, "Actions", 0f, 0f, 1f, 1f);
            _playBtn = UiKit.MakeBigButton(_actions, UiKit.Leaf, "Icon37", PlaySelectedRole,
                0.06f, 0.145f, 0.94f, 0.255f, out _playTitle, out _playSub);

            DockButton(0, "Icon09", "Nido", () => OpenNest(0));
            DockButton(1, "Icon10", "Hormigas", OpenRoles);
            DockButton(2, "Icon08", "Diarias", OpenDaily);
            _sellCaption = DockButton(3, "Icon54", "Vender", () =>
            {
                int gain = Game.Data.leaves * Game.LeafPrice;
                Game.SellLeaves();
                if (gain > 0)
                {
                    Sfx.Play(Sfx.Id.Coin);
                    Haptics.Tap();
                    Toast($"Vendiste hojas por {gain} monedas", 3f);
                }
            });

            // Ajustes (sonido, musica, vibracion).
            UiKit.IconButton(_actions, "Icon12", OpenSettings, 0.015f, 0.868f, 0.095f, 0.915f);

            // Aviso rojo sobre "Diarias" cuando hay premios listos.
            float cx = DockX(2);
            var badge = UiKit.Icon(_actions, "Btn07", Color.white, cx + 0.035f, 0.105f, cx + 0.115f, 0.15f);
            _badge = badge.gameObject;
            _badgeText = UiKit.Outlined(UiKit.Label(badge.transform, "", 30, TextAnchor.MiddleCenter, Color.white, 0f, 0f, 1f, 1f));
        }

        Text HudStat(int i, string icon, Color tint)
        {
            float x0 = 0.03f + i * 0.235f;
            UiKit.Icon(_safe, icon, tint, x0 + 0.015f, 0.93f, x0 + 0.09f, 0.98f);
            var t = UiKit.Label(_safe, "", 40, TextAnchor.MiddleCenter, UiKit.Cream, x0 + 0.09f, 0.925f, x0 + 0.235f, 0.985f);
            t.fontStyle = FontStyle.Bold;
            return t;
        }

        static float DockX(int i) => 0.14f + i * 0.24f;

        Text DockButton(int i, string icon, string caption, UnityAction onClick)
        {
            float cx = DockX(i);
            UiKit.IconButton(_actions, icon, onClick, cx - 0.10f, 0.05f, cx + 0.10f, 0.135f);
            return UiKit.Outlined(UiKit.Label(_actions, caption, 28, TextAnchor.MiddleCenter, UiKit.Cream, cx - 0.12f, 0.012f, cx + 0.12f, 0.055f));
        }

        void RefreshHud()
        {
            var d = Game.Data;
            _coinsC.Set(d.coins, !_hudShown);
            _leavesC.Set(d.leaves, !_hudShown);
            _twigsC.Set(d.twigs, !_hudShown);
            _piecesC.Set(d.pieces, !_hudShown);
            _hudShown = true;

            var role = AntRoles.All[d.role];
            UiKit.SetButtonColor(_playBtn, role.Ui);
            _playTitle.text = "JUGAR";
            _playSub.text = $"{role.Name}  -  {role.Verb}";

            int pending = Game.DailyPending();
            _badge.SetActive(pending > 0);
            _badgeText.text = pending.ToString();
            _sellCaption.text = d.leaves > 0 ? $"Vender ({d.leaves * Game.LeafPrice})" : "Vender";
            if (_overlay != null && _overlayBuild != null) RebuildOverlay();
        }

        void HideToast() => _toast.transform.parent.gameObject.SetActive(false);

        void Toast(string msg, float seconds)
        {
            _toast.text = msg;
            _toast.transform.parent.gameObject.SetActive(true);
            _toastUntil = Time.time + seconds;
        }

        // ---------------- Roles y mundo ----------------
        // Elegir un rol lo guarda y entra al mundo con el.
        internal void PlayRole(int role)
        {
            Game.SetRole(role);
            OpenWorld();
        }

        void PlaySelectedRole() => PlayRole(Game.Data.role);

        internal void OpenRoles()
        {
            CloseOverlay();
            HideToast();
            _actions.gameObject.SetActive(false);
            RoleScreen.Open(_safe, PlayRole, () => _actions.gameObject.SetActive(true));
        }

        void OpenWorld()
        {
            CloseOverlay();
            HideToast();
            _actions.gameObject.SetActive(false); // deja ver el mundo
            WorldGame.Open(_safe, () =>
            {
                _actions.gameObject.SetActive(true);
                Toast("Vuelves al nido", 2f);
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
            UiKit.Frame(rt, "Bg", UiKit.Skin.Green, 0, 0, 1, 1);
            _overlayBuild(rt);
        }

        void CloseOverlay()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
            _overlayBuild = null;
        }

        // Titulo y boton redondo de cerrar, iguales en todos los paneles.
        void PanelHeader(RectTransform rt, string title)
        {
            UiKit.Label(rt, title, 56, TextAnchor.MiddleCenter, UiKit.Cream, 0.14f, 0.895f, 0.86f, 0.98f).fontStyle = FontStyle.Bold;
            UiKit.IconButton(rt, "Icon78", CloseOverlay, 0.83f, 0.895f, 0.96f, 0.975f);
        }

        // Tarjeta oscura donde va una mejora o una tarea.
        RectTransform Card(RectTransform rt, float top, float height)
        {
            return UiKit.Pill(rt, "Card", 0.55f, 0.05f, top - height, 0.95f, top).rectTransform;
        }

        // Nombre y descripcion a la izquierda, accion a la derecha.
        void CardTexts(RectTransform card, string name, string detail, int nameSize, int detailSize)
        {
            UiKit.Label(card, name, nameSize, TextAnchor.MiddleLeft, UiKit.Gold, 0.06f, 0.58f, 0.60f, 0.96f).fontStyle = FontStyle.Bold;
            UiKit.Label(card, detail, detailSize, TextAnchor.UpperLeft, UiKit.Cream, 0.06f, 0.08f, 0.60f, 0.60f);
        }

        internal void OpenSettings()
        {
            OpenOverlay(rt =>
            {
                PanelHeader(rt, "Ajustes");
                string[] names = { "Efectos de sonido", "Musica", "Vibracion" };
                Func<bool>[] get = { () => Sfx.SfxOn, () => Sfx.MusicOn, () => Sfx.HapticsOn };
                Action<bool>[] set = { v => Sfx.SfxOn = v, v => Sfx.MusicOn = v, v => Sfx.HapticsOn = v };
                for (int i = 0; i < names.Length; i++)
                {
                    int k = i;
                    var card = Card(rt, 0.80f - i * 0.2f, 0.17f);
                    UiKit.Label(card, names[k], 44, TextAnchor.MiddleLeft, UiKit.Cream, 0.06f, 0.15f, 0.55f, 0.85f).fontStyle = FontStyle.Bold;
                    bool on = get[k]();
                    UiKit.MakeButton(card, on ? "Activado" : "Desactivado", on ? UiKit.Leaf : UiKit.Disabled, 36, () =>
                    {
                        set[k](!get[k]());
                        OpenSettings();
                    }, 0.60f, 0.18f, 0.96f, 0.82f);
                }
            });
        }

        // tab 0 = edificios del nido (monedas + piezas), tab 1 = mejoras de la obrera (monedas + ramas).
        internal void OpenNest(int tab)
        {
            OpenOverlay(rt =>
            {
                PanelHeader(rt, "Nido");
                UiKit.MakeButton(rt, "Edificios", tab == 0 ? UiKit.Leaf : UiKit.Cream, 38, () => OpenNest(0), 0.06f, 0.80f, 0.49f, 0.88f);
                UiKit.MakeButton(rt, "Obrera", tab == 1 ? UiKit.Leaf : UiKit.Cream, 38, () => OpenNest(1), 0.51f, 0.80f, 0.94f, 0.88f);
                if (tab == 0) BuildBuildingCards(rt);
                else BuildForageCards(rt);
            });
        }

        void BuildBuildingCards(RectTransform rt)
        {
            for (int i = 0; i < 3; i++)
            {
                int b = i;
                var card = Card(rt, 0.775f - i * 0.255f, 0.235f);
                int lvl = Game.Data.buildingLevels[b];
                CardTexts(card, Game.BuildingNames[b], $"Nivel {lvl}/{Game.MaxLevel}\n{Game.BuildingEffect(b)}", 50, 32);
                bool max = lvl >= Game.MaxLevel;
                string cost = max ? "Nivel\nmaximo" : $"Mejorar\n{Game.UpgradeCoinCost(b)} mon. + {Game.UpgradePieceCost(b)} pz.";
                UiKit.MakeButton(card, cost, max || !Game.CanUpgrade(b) ? UiKit.Disabled : UiKit.Gold, 32, () =>
                {
                    if (Game.TryUpgrade(b)) Toast($"{Game.BuildingNames[b]} sube de nivel", 3f);
                    else Toast("Te faltan monedas o piezas", 3f);
                }, 0.62f, 0.20f, 0.97f, 0.80f);
            }
        }

        void BuildForageCards(RectTransform rt)
        {
            for (int i = 0; i < Game.ForageUpgradeNames.Length; i++)
            {
                int u = i;
                var card = Card(rt, 0.775f - i * 0.19f, 0.175f);
                int lvl = Game.Data.forageLevels[u];
                CardTexts(card, Game.ForageUpgradeNames[u], $"Nivel {lvl}/{Game.ForageMaxLevel}\n{Game.ForageEffect(u)}", 42, 28);
                bool max = lvl >= Game.ForageMaxLevel;
                string cost = max ? "Nivel\nmaximo" : $"Mejorar\n{Game.ForageCoinCost(u)} mon. + {Game.ForageTwigCost(u)} ramas";
                UiKit.MakeButton(card, cost, max || !Game.CanUpgradeForage(u) ? UiKit.Disabled : UiKit.Gold, 28, () =>
                {
                    if (Game.TryUpgradeForage(u)) Toast($"{Game.ForageUpgradeNames[u]} sube de nivel", 3f);
                    else Toast("Te faltan monedas o ramas", 3f);
                }, 0.60f, 0.16f, 0.97f, 0.84f);
            }
        }

        internal void OpenDaily()
        {
            Game.RefreshDay();
            OpenOverlay(rt =>
            {
                PanelHeader(rt, "Diarias");
                for (int i = 0; i < 3; i++)
                {
                    int t = i;
                    var card = Card(rt, 0.86f - i * 0.28f, 0.255f);
                    UiKit.Label(card, Game.DailyTitles[t], 38, TextAnchor.MiddleLeft, UiKit.Cream, 0.06f, 0.48f, 0.60f, 0.96f);

                    // Barra de progreso: fondo oscuro y relleno verde.
                    float frac = Mathf.Clamp01(Game.DailyProgress(t) / (float)Game.DailyTargets[t]);
                    UiKit.Pill(card, "Bar", 0.9f, 0.06f, 0.16f, 0.48f, 0.38f);
                    if (frac > 0f)
                    {
                        var fill = UiKit.Icon(card, "Progress03", Color.white, 0.065f, 0.18f, 0.065f + 0.405f * frac, 0.36f);
                        fill.preserveAspect = false;
                        fill.type = Image.Type.Sliced;
                    }
                    UiKit.Label(card, $"{Game.DailyProgress(t)}/{Game.DailyTargets[t]}", 34, TextAnchor.MiddleLeft, UiKit.Gold, 0.50f, 0.12f, 0.62f, 0.42f);

                    bool claimed = Game.Data.dailyClaimed[t];
                    bool ready = Game.DailyDone(t) && !claimed;
                    string txt = claimed ? "Reclamado" : ready ? $"Reclamar\n{Game.DailyRewards[t]} mon." : $"Premio\n{Game.DailyRewards[t]} mon.";
                    UiKit.MakeButton(card, txt, ready ? UiKit.Leaf : UiKit.Disabled, 32, () =>
                    {
                        if (Game.ClaimDaily(t)) Toast($"+{Game.DailyRewards[t]} monedas", 3f);
                    }, 0.63f, 0.20f, 0.97f, 0.80f);
                }
            });
        }
    }
}
