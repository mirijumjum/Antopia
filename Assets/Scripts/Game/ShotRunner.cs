using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antopia
{
    // Herramienta de desarrollo: si el juego se lanza con "-antopia-shots <carpeta>" recorre las pantallas,
    // guarda una captura de cada una y se cierra. Se usa con la build de Windows (AntopiaBuild.BuildWindowsShots).
    public class ShotRunner : MonoBehaviour
    {
        static ShotRunner _instance;

        public static void StartIfRequested()
        {
            if (_instance != null) return;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-antopia-shots");
            if (i < 0 || i + 1 >= args.Length) return;
            Application.runInBackground = true; // que no se pause si la ventana pierde el foco
            var go = new GameObject("ShotRunner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShotRunner>();
            _instance._dir = args[i + 1];
            Directory.CreateDirectory(_instance._dir);
            _instance.StartCoroutine(_instance.Run());
        }

        string _dir;

        // Cada paso recarga la escena para partir de un estado limpio, ejecuta la accion y captura.
        IEnumerator Run()
        {
            Game.Data.coins = 340;
            Game.Data.leaves = 14;
            Game.Data.twigs = 9;
            Game.Data.pieces = 6;
            Game.Data.buildingLevels = new[] { 2, 3, 1 };
            Game.Data.forageLevels = new[] { 1, 0, 2, 1 };
            // Mundo recien empezado: sin niebla destapada ni caminos abiertos.
            Game.Data.revealed.Clear();
            Game.Data.poiFound = new bool[2];
            Game.Data.guardDefeated = false;
            Game.Data.tunnelBuilt = false;
            Game.Data.questStep = 0;
            Game.Data.npcWork = new float[2];
            Game.Data.statPickups = Game.Data.statDeliveries = Game.Data.statHoney = Game.Data.statCrystals = 0;
            Game.Data.role = 0;
            Game.Save();

            var steps = new (string name, Action<GameController> act, float wait)[]
            {
                ("main", c => { }, 1.5f),
                ("nest_buildings", c => c.OpenNest(0), 1f),
                ("nest_forager", c => c.OpenNest(1), 1f),
                ("daily", c => c.OpenDaily(), 1f),
                ("settings", c => c.OpenSettings(), 1f),
                ("roles", c => c.OpenRoles(), 1.5f),
                ("world_obrera", c =>
                {
                    c.PlayRole(AntRoles.Obrera);
                    FindFirstObjectByType<WorldGame>().DebugInput = new Vector2(0.3f, 1f).normalized;
                }, 3.5f),
                ("world_guard", c =>
                {
                    c.PlayRole(AntRoles.Obrera);
                    FindFirstObjectByType<WorldGame>().DebugTeleport(new Vector3(0f, 0f, 9f));
                }, 1.5f),
                ("world_explorer", c =>
                {
                    c.PlayRole(AntRoles.Exploradora);
                    var w = FindFirstObjectByType<WorldGame>();
                    w.DebugTeleport(new Vector3(0f, 0f, 19f));
                    w.DebugInput = new Vector2(0f, 1f);
                }, 2.5f),
                ("world_tunnel", c =>
                {
                    c.PlayRole(AntRoles.Constructora);
                    FindFirstObjectByType<WorldGame>().DebugTeleport(new Vector3(9.5f, 0f, -6f));
                }, 1.5f),
                ("npc_nest", c =>
                {
                    // Escuadras de obreras y exploradoras trabajando cerca del nido mientras juegas de soldado.
                    c.PlayRole(AntRoles.Soldado);
                }, 7f),
                ("npc_soldier", c =>
                {
                    // Soldados NPC bailando ante el escarabajo (con la mitad del avance ya hecho).
                    c.PlayRole(AntRoles.Obrera);
                    var w = FindFirstObjectByType<WorldGame>();
                    w.DebugNpcWork(Game.NpcSoldierSeconds * 0.45f, 0f);
                    w.DebugTeleport(new Vector3(0f, 0f, 5f));
                }, 7f),
                ("npc_builder", c =>
                {
                    // Constructoras NPC en la obra del tunel.
                    c.PlayRole(AntRoles.Obrera);
                    var w = FindFirstObjectByType<WorldGame>();
                    w.DebugNpcWork(0f, Game.NpcBuildSeconds * 0.4f);
                    w.DebugTeleport(new Vector3(10f, 0f, -1.5f));
                }, 8f),
                ("npc_done_guard", c =>
                {
                    // Las soldado NPC casi han terminado: el escarabajo debe desaparecer solo.
                    c.PlayRole(AntRoles.Obrera);
                    var w = FindFirstObjectByType<WorldGame>();
                    w.DebugNpcWork(Game.NpcSoldierSeconds - 1.5f, 0f);
                    w.DebugTeleport(new Vector3(0f, 0f, 6f));
                }, 8f),
                ("npc_done_tunnel", c =>
                {
                    // Las constructoras NPC casi han terminado: el tapon del tunel debe desaparecer solo.
                    c.PlayRole(AntRoles.Obrera);
                    var w = FindFirstObjectByType<WorldGame>();
                    w.DebugNpcWork(0f, Game.NpcBuildSeconds - 1.5f);
                    w.DebugTeleport(new Vector3(9f, 0f, -2f));
                }, 9f),
                ("trigger_battle", c =>
                {
                    // La soldado se acerca al escarabajo y el combate debe empezar solo.
                    c.PlayRole(AntRoles.Soldado);
                    var w = FindFirstObjectByType<WorldGame>();
                    w.DebugTeleport(new Vector3(0f, 0f, 8.5f));
                    w.DebugInput = new Vector2(0f, 1f);
                }, 3f),
                ("trigger_build", c =>
                {
                    // La constructora, con ramas, se acerca al muro del este y la obra debe empezar sola.
                    c.PlayRole(AntRoles.Constructora);
                    var w = FindFirstObjectByType<WorldGame>();
                    w.DebugTeleport(new Vector3(8f, 0f, 0f));
                    w.DebugInput = new Vector2(1f, 0f);
                }, 3f),
                ("soldier_dance", c =>
                {
                    c.PlayRole(AntRoles.Soldado);
                    FindFirstObjectByType<WorldGame>().DebugBattle();
                    FindFirstObjectByType<SoldierGame>().DebugPerform();
                }, 0.55f),
                ("builder", c =>
                {
                    c.PlayRole(AntRoles.Constructora);
                    FindFirstObjectByType<WorldGame>().DebugBuild();
                    FindFirstObjectByType<BuilderGame>().DebugAutoPlay(5);
                }, 2f),
            };

            foreach (var step in steps)
            {
                SceneManager.LoadScene(0);
                yield return null;
                yield return new WaitForSeconds(0.5f);
                var controller = FindFirstObjectByType<GameController>();
                Game.Data.twigs = Mathf.Max(Game.Data.twigs, 9); // las pruebas del tunel gastan ramas
                try { step.act(controller); }
                catch (Exception e) { Debug.LogError($"[ShotRunner] {step.name}: {e}"); }
                yield return new WaitForSeconds(step.wait);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(_dir, step.name + ".png"));
                yield return new WaitForSeconds(0.5f);
            }
            Application.Quit();
        }
    }
}
