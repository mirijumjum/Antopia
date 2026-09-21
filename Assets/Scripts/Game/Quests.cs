using System;
using UnityEngine;

namespace Antopia
{
    public class Quest
    {
        public string Title;
        public int Role;                 // rol que la cumple (AntRoles.*)
        public int Target;
        public Func<int> Progress;       // avance actual, de 0 a Target
        public int Reward;               // monedas al completarla
        public Func<Vector3?> Waypoint;  // hacia donde apuntar la flecha (o null)
    }

    // Mision guiada: una cadena corta que ensena el bucle del mundo pasando por los cuatro roles. Cada paso se
    // cumple con datos que ya guarda SaveData (contadores y caminos abiertos), asi sobrevive a cerrar el juego.
    public static class Quests
    {
        public static readonly Quest[] All =
        {
            new Quest { Title = "Recoge 5 recursos", Role = AntRoles.Obrera, Target = 5, Reward = 30,
                Progress = () => Mathf.Min(5, Game.Data.statPickups), Waypoint = () => null },
            new Quest { Title = "Entrega una carga en el nido", Role = AntRoles.Obrera, Target = 1, Reward = 30,
                Progress = () => Mathf.Min(1, Game.Data.statDeliveries), Waypoint = () => NestView.NestEntrance },
            new Quest { Title = "Vence al escarabajo del paso norte", Role = AntRoles.Soldado, Target = 1, Reward = 50,
                Progress = () => Game.Data.guardDefeated ? 1 : 0, Waypoint = () => WorldLayout.GuardPos },
            new Quest { Title = "Descubre la Colmena de miel", Role = AntRoles.Exploradora, Target = 1, Reward = 50,
                Progress = () => Game.Data.poiFound[0] ? 1 : 0, Waypoint = () => WorldLayout.PoiPos[0] },
            new Quest { Title = "Lleva 3 mieles al nido", Role = AntRoles.Obrera, Target = 3, Reward = 60,
                Progress = () => Mathf.Min(3, Game.Data.statHoney), Waypoint = () => WorldLayout.PoiPos[0] },
            new Quest { Title = "Levanta el tunel del este", Role = AntRoles.Constructora, Target = 1, Reward = 60,
                Progress = () => Game.Data.tunnelBuilt ? 1 : 0, Waypoint = () => WorldLayout.TunnelPos },
            new Quest { Title = "Descubre la Cueva de cristales", Role = AntRoles.Exploradora, Target = 1, Reward = 60,
                Progress = () => Game.Data.poiFound[1] ? 1 : 0, Waypoint = () => WorldLayout.PoiPos[1] },
            new Quest { Title = "Lleva 2 cristales al nido", Role = AntRoles.Obrera, Target = 2, Reward = 100,
                Progress = () => Mathf.Min(2, Game.Data.statCrystals), Waypoint = () => WorldLayout.PoiPos[1] },
        };

        public static Quest Current => Game.Data.questStep < All.Length ? All[Game.Data.questStep] : null;

        // Da por cumplidas las misiones que ya lo estan (una tras otra) y devuelve la ultima completada, o null.
        public static Quest CompleteFinished()
        {
            Quest last = null;
            var q = Current;
            while (q != null && q.Progress() >= q.Target)
            {
                Game.Data.questStep++;
                Game.AddCoins(q.Reward);
                last = q;
                q = Current;
            }
            return last;
        }
    }
}
