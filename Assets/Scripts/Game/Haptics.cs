using UnityEngine;

namespace Antopia
{
    // Vibracion corta en Android (el Handheld.Vibrate normal dura medio segundo). Solo actua si esta activada en Ajustes.
    public static class Haptics
    {
        // Solo para que Unity anada el permiso de vibracion al manifiesto: detecta el uso de Handheld.Vibrate.
        static readonly bool Never = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
#endif

        public static void Tap() => Vibrate(15);
        public static void Bump() => Vibrate(35);
        public static void Big() => Vibrate(70);

        static void Vibrate(int ms)
        {
            if (!Sfx.HapticsOn) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                _vibrator?.Call("vibrate", (long)ms);
            }
            catch (System.Exception)
            {
                // Sin vibrador o sin permiso: no pasa nada.
            }
#endif
#if UNITY_ANDROID
            if (Never) Handheld.Vibrate();
#endif
        }
    }
}
