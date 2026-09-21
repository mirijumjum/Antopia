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
            new AntRole("Obrera", "recolectar", "Recorre el mundo sin prisa recogiendo recursos y los lleva al nido. Sus hallazgos pagan el trabajo de las demas.",
                AntModel.Red, UiKit.Leaf, true),
            new AntRole("Constructora", "construir", "Levanta tuneles para llegar a los sitios que descubre la exploradora. Necesita ramas y un buen pulso.",
                AntModel.Black, UiKit.Twig, true),
            new AntRole("Soldado", "despejar", "Despeja el camino de bichos bailando: dibuja con el dedo la forma que pide el globo.",
                AntModel.Brown, new Color(0.80f, 0.50f, 0.40f), true),
            new AntRole("Exploradora", "explorar", "Destapa la niebla del mapa y descubre sitios y hallazgos para todas. Ve mas lejos que nadie.",
                AntModel.Green, new Color(0.65f, 0.78f, 0.30f), true),
        };
    }
}
