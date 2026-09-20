using UnityEngine;

namespace Antopia
{
    public class AntRole
    {
        public readonly string Name, Verb, Description;
        public readonly int Variant;   // color de AntModel: cada color es un rol
        public readonly Color Ui;      // color del boton de jugar
        public readonly bool HasGame;  // false = solo modo de prueba (moverse por el mundo)

        public AntRole(string name, string verb, string description, int variant, Color ui, bool hasGame)
        {
            Name = name;
            Verb = verb;
            Description = description;
            Variant = variant;
            Ui = ui;
            HasGame = hasGame;
        }
    }

    // Los cuatro tipos de hormiga. El indice es el que se guarda en SaveData.role.
    public static class AntRoles
    {
        public const int Obrera = 0, Constructora = 1, Soldado = 2, Exploradora = 3;

        public static readonly AntRole[] All =
        {
            new AntRole("Obrera", "recolectar", "Recorre el mundo recogiendo hierba y ramas y las lleva al nido.",
                AntModel.Red, UiKit.Leaf, true),
            new AntRole("Constructora", "construir", "Lleva piezas al solar para levantar la torre. Cada obra gasta ramas.",
                AntModel.Black, UiKit.Twig, true),
            new AntRole("Soldado", "patrullar", "Defendera el nido de invasores. En desarrollo: de momento solo puedes moverte por el mundo.",
                AntModel.Brown, new Color(0.80f, 0.50f, 0.40f), false),
            new AntRole("Exploradora", "explorar", "Descubrira zonas nuevas del mundo. En desarrollo: de momento solo puedes moverte por el mundo.",
                AntModel.Green, new Color(0.65f, 0.78f, 0.30f), false),
        };
    }
}
