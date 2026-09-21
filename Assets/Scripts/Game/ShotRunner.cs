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
            Game.Save();

            var steps = new (string name, Action<GameController> act, float wait)[]
            {
                ("main", c => { }, 1.5f),
                ("nest_buildings", c => c.OpenNest(0), 1f),
                ("nest_forager", c => c.OpenNest(1), 1f),
                ("daily", c => c.OpenDaily(), 1f),
                ("roles", c => c.OpenRoles(), 1.5f),
                ("forager", c =>
                {
                    c.PlayRole(AntRoles.Obrera);
                    FindFirstObjectByType<ForagerGame>().DebugInput = new Vector2(0.4f, 1f).normalized;
                }, 3f),
                ("builder", c =>
                {
                    c.PlayRole(AntRoles.Constructora);
                    FindFirstObjectByType<BuilderGame>().DebugAutoPlay(5);
                }, 2f),
                ("roam", c => c.PlayRole(AntRoles.Soldado), 1.5f),
            };

            foreach (var step in steps)
            {
                SceneManager.LoadScene(0);
                yield return null;
                yield return new WaitForSeconds(0.5f);
                var controller = FindFirstObjectByType<GameController>();
                step.act(controller);
                yield return new WaitForSeconds(step.wait);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(_dir, step.name + ".png"));
                yield return new WaitForSeconds(0.5f);
            }
            Application.Quit();
        }
    }
}
