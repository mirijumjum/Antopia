using UnityEditor;
using UnityEngine;

namespace Antopia.EditorTools
{
    // Comprueba que todos los sonidos sintetizados salen bien (sin NaN y con volumen razonable).
    // -executeMethod Antopia.EditorTools.SfxTests.Run -batchmode -nographics -quit
    public static class SfxTests
    {
        [MenuItem("Antopia/Test Sonidos")]
        public static void Run()
        {
            Debug.Log("\n[SfxTests]\n" + Sfx.DebugReport());
        }
    }
}
