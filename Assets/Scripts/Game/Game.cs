using System;
using UnityEngine;

namespace Antopia
{
    [Serializable]
    public class SaveData
    {
        public int leaves, twigs, coins, pieces;
        public int[] buildingLevels = new int[3];
        public string dayKey = "";
        public int dailyForages, dailyPieces, dailyUpgrades;
        public bool[] dailyClaimed = new bool[3];
        public long lastSeenTicks;
        public float idleBuffer;
    }

    // Estado del juego, reglas de economia y guardado local.
    public static class Game
    {
        const string SaveKey = "antopia_save_v1";
        public const int MaxLevel = 10;
        public const int BuildTwigCost = 3;
        public const int BuildAttempts = 5;
        const float IdleLeavesPerAntPerSecond = 0.05f;
        const double OfflineCapSeconds = 2 * 3600;

        public static readonly string[] BuildingNames = { "Despensa", "Tuneles", "Camara real" };

        public static SaveData Data { get; private set; }
        public static event Action Changed;
        public static string OfflineMessage { get; private set; }

        public static readonly string[] DailyTitles =
        {
            "Recolecta 3 veces con la obrera",
            "Coloca 6 piezas con la constructora",
            "Mejora 1 edificio del nido",
        };
        public static readonly int[] DailyTargets = { 3, 6, 1 };
        public static readonly int[] DailyRewards = { 60, 90, 120 };

        public static void Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(SaveKey, "");
                Data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                Data = new SaveData();
            }
            if (Data.buildingLevels == null || Data.buildingLevels.Length != 3) Data.buildingLevels = new int[3];
            if (Data.dailyClaimed == null || Data.dailyClaimed.Length != 3) Data.dailyClaimed = new bool[3];

            ApplyOfflineIncome();
            CheckDailyReset();
            Save();
        }

        public static void Save()
        {
            if (Data == null) return;
            Data.lastSeenTicks = DateTime.UtcNow.Ticks;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        static void Notify()
        {
            Save();
            Changed?.Invoke();
        }

        // ---- Economia ----
        public static int Ants => 1 + Data.buildingLevels[1];
        public static float HarvestMultiplier => 1f + 0.25f * Data.buildingLevels[0];
        public static int LeafPrice => 1 + Data.buildingLevels[2];

        public static int UpgradeCoinCost(int b) => Mathf.RoundToInt(50f * Mathf.Pow(1.7f, Data.buildingLevels[b]) * (1f + 0.2f * b));
        public static int UpgradePieceCost(int b) => 3 + 2 * Data.buildingLevels[b];

        public static string BuildingEffect(int b)
        {
            int l = Data.buildingLevels[b];
            switch (b)
            {
                case 0: return $"Cosecha x{1f + 0.25f * l:0.00} (siguiente x{1f + 0.25f * (l + 1):0.00})";
                case 1: return $"{1 + l} hormigas pasivas (siguiente {2 + l})";
                default: return $"Cada hoja vale {1 + l} monedas (siguiente {2 + l})";
            }
        }

        public static void Tick(float dt)
        {
            if (Data == null) return;
            Data.idleBuffer += Ants * IdleLeavesPerAntPerSecond * dt;
            if (Data.idleBuffer >= 1f)
            {
                int add = Mathf.FloorToInt(Data.idleBuffer);
                Data.idleBuffer -= add;
                Data.leaves += add;
                Changed?.Invoke();
            }
        }

        static void ApplyOfflineIncome()
        {
            OfflineMessage = null;
            if (Data.lastSeenTicks <= 0) return;
            double secs = (DateTime.UtcNow - new DateTime(Data.lastSeenTicks, DateTimeKind.Utc)).TotalSeconds;
            secs = Math.Max(0, Math.Min(secs, OfflineCapSeconds));
            int gained = Mathf.FloorToInt((float)(secs * Ants * IdleLeavesPerAntPerSecond));
            if (gained > 0)
            {
                Data.leaves += gained;
                OfflineMessage = $"Mientras no estabas, tus hormigas trajeron {gained} hojas.";
            }
        }

        static void CheckDailyReset()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (Data.dayKey == today) return;
            Data.dayKey = today;
            Data.dailyForages = 0;
            Data.dailyPieces = 0;
            Data.dailyUpgrades = 0;
            Data.dailyClaimed = new bool[3];
        }

        public static void OnResume()
        {
            if (Data == null) return;
            ApplyOfflineIncome();
            CheckDailyReset();
            Notify();
        }

        public static void RefreshDay()
        {
            string before = Data.dayKey;
            CheckDailyReset();
            if (before != Data.dayKey) Notify();
        }

        // ---- Acciones ----
        public static void SellLeaves()
        {
            if (Data.leaves <= 0) return;
            Data.coins += Data.leaves * LeafPrice;
            Data.leaves = 0;
            Notify();
        }

        public static void CompleteForage(int leaves, int twigs)
        {
            Data.leaves += Mathf.RoundToInt(leaves * HarvestMultiplier);
            Data.twigs += Mathf.RoundToInt(twigs * HarvestMultiplier);
            Data.dailyForages++;
            Notify();
        }

        public static bool TryStartBuild()
        {
            if (Data.twigs < BuildTwigCost) return false;
            Data.twigs -= BuildTwigCost;
            Notify();
            return true;
        }

        public static void CompleteBuild(int pieces)
        {
            Data.pieces += pieces;
            Data.dailyPieces += pieces;
            Notify();
        }

        public static bool CanUpgrade(int b) =>
            Data.buildingLevels[b] < MaxLevel && Data.coins >= UpgradeCoinCost(b) && Data.pieces >= UpgradePieceCost(b);

        public static bool TryUpgrade(int b)
        {
            if (!CanUpgrade(b)) return false;
            Data.coins -= UpgradeCoinCost(b);
            Data.pieces -= UpgradePieceCost(b);
            Data.buildingLevels[b]++;
            Data.dailyUpgrades++;
            Notify();
            return true;
        }

        public static int DailyProgress(int i) =>
            Mathf.Min(DailyTargets[i], i == 0 ? Data.dailyForages : i == 1 ? Data.dailyPieces : Data.dailyUpgrades);

        public static bool DailyDone(int i) => DailyProgress(i) >= DailyTargets[i];

        public static bool ClaimDaily(int i)
        {
            if (!DailyDone(i) || Data.dailyClaimed[i]) return false;
            Data.dailyClaimed[i] = true;
            Data.coins += DailyRewards[i];
            Notify();
            return true;
        }

        public static int DailyPending()
        {
            int n = 0;
            for (int i = 0; i < 3; i++) if (DailyDone(i) && !Data.dailyClaimed[i]) n++;
            return n;
        }
    }
}
