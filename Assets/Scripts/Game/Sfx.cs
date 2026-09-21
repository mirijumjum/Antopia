using System;
using System.Collections.Generic;
using UnityEngine;

namespace Antopia
{
    // Sonido del juego sin archivos de audio: todos los efectos y la musica se sintetizan en codigo la primera vez
    // que se usan (ondas, barridos y ruido con envolventes). Los ajustes de sonido, musica y vibracion se guardan
    // en PlayerPrefs.
    public static class Sfx
    {
        public enum Id
        {
            Click, Pickup, Deliver, Coin, Discover, Poof, Boing, Trill, Whoosh, Waltz, Sparkle,
            Thump, Hurt, Fanfare, Drop, Perfect, Collapse, Swoosh, Wrong, Defeat,
        }

        const int Rate = 44100;
        const string KeySfx = "antopia_sfx", KeyMusic = "antopia_music", KeyHaptics = "antopia_haptics";

        static readonly Dictionary<Id, AudioClip> Clips = new Dictionary<Id, AudioClip>();
        static readonly Dictionary<Id, float> LastPlayed = new Dictionary<Id, float>();
        static AudioSource[] _pool;
        static AudioSource _music;
        static int _next;

        public static bool SfxOn
        {
            get => PlayerPrefs.GetInt(KeySfx, 1) == 1;
            set { PlayerPrefs.SetInt(KeySfx, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(KeyMusic, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(KeyMusic, value ? 1 : 0);
                PlayerPrefs.Save();
                if (_music != null)
                {
                    if (value && !_music.isPlaying) _music.Play();
                    else if (!value) _music.Pause();
                }
            }
        }

        public static bool HapticsOn
        {
            get => PlayerPrefs.GetInt(KeyHaptics, 1) == 1;
            set { PlayerPrefs.SetInt(KeyHaptics, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ---------------- Reproduccion ----------------
        static void Init()
        {
            if (_pool != null && _pool[0] != null) return;
            var go = new GameObject("Sfx");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _pool = new AudioSource[8];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = go.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
            }
            _music = go.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = 0.22f;
        }

        public static void Play(Id id, float volume = 1f, float pitch = 1f)
        {
            if (!SfxOn) return;
            Init();
            float now = Time.unscaledTime;
            if (LastPlayed.TryGetValue(id, out float last) && now - last < 0.05f) return; // evita que se pise a si mismo
            LastPlayed[id] = now;
            var src = _pool[_next++ % _pool.Length];
            src.pitch = pitch;
            src.PlayOneShot(Get(id), Mathf.Clamp01(volume) * 0.8f);
        }

        public static void StartMusic()
        {
            Init();
            if (_music.clip == null) _music.clip = MakeMusic();
            if (MusicOn && !_music.isPlaying) _music.Play();
        }

        static AudioClip Get(Id id)
        {
            if (!Clips.TryGetValue(id, out var clip) || clip == null) Clips[id] = clip = Synthesize(id);
            return clip;
        }

        // Para comprobar la sintesis sin oirla: duracion, pico y si hay valores no validos en cada sonido y en la musica.
        public static string DebugReport()
        {
            var sb = new System.Text.StringBuilder();
            void Line(string name, AudioClip clip)
            {
                var d = new float[clip.samples];
                clip.GetData(d, 0);
                float peak = 0f;
                bool bad = false;
                foreach (float v in d)
                {
                    if (float.IsNaN(v) || float.IsInfinity(v)) bad = true;
                    peak = Mathf.Max(peak, Mathf.Abs(v));
                }
                sb.AppendLine($"  {name,-9} {clip.length,5:0.00}s  pico {peak:0.00}{(bad ? "  NAN!" : "")}");
            }
            foreach (Id id in Enum.GetValues(typeof(Id))) Line(id.ToString(), Get(id));
            Line("musica", MakeMusic());
            return sb.ToString();
        }

        // ---------------- Sintesis ----------------
        static float Sin(float f, float t) => Mathf.Sin(6.2831853f * f * t);

        // Seno con frecuencia que va de f0 a f1 durante "dur" segundos.
        static float Sweep(float f0, float f1, float dur, float t)
        {
            float u = Mathf.Min(t, dur);
            return Mathf.Sin(6.2831853f * (f0 * u + 0.5f * (f1 - f0) / dur * u * u));
        }

        // Nota con un armonico, ataque corto y caida exponencial que empieza en "start".
        static float Note(float freq, float t, float start, float dur, float amp = 1f)
        {
            float u = t - start;
            if (u < 0f || u > dur) return 0f;
            return (Sin(freq, u) + 0.3f * Sin(freq * 2f, u)) * Mathf.Min(1f, u / 0.004f) * Mathf.Exp(-5f * u / dur) * amp;
        }

        static float Hash(int i)
        {
            float x = Mathf.Sin(i * 12.9898f) * 43758.5453f;
            return (x - Mathf.Floor(x)) * 2f - 1f;
        }

        static AudioClip Build(string name, float seconds, Func<float, float> f, int rate = Rate, float gain = 1f)
        {
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            int fade = rate / 200;
            for (int i = 0; i < n; i++)
            {
                float v = f(i / (float)rate) * gain;
                if (i > n - fade) v *= (n - i) / (float)fade;
                data[i] = (float)Math.Tanh(v * 1.1f); // limitador suave: sin chasquidos aunque el pico pase de 1
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Synthesize(Id id)
        {
            const float C5 = 523.25f, D5 = 587.33f, E5 = 659.25f, G5 = 783.99f, A5 = 880f, C6 = 1046.5f, E6 = 1318.5f, G6 = 1568f;
            int ni = 0;
            float lp = 0f;
            switch (id)
            {
                case Id.Click: return Build("click", 0.06f, t => Sweep(1000f, 600f, 0.06f, t) * Mathf.Exp(-40f * t) * 0.6f);
                case Id.Pickup: return Build("pickup", 0.12f, t => Sweep(380f, 1050f, 0.1f, t) * Mathf.Exp(-16f * t) * 0.7f);
                case Id.Coin: return Build("coin", 0.35f, t => (Note(1318f, t, 0f, 0.3f) + Note(1760f, t, 0.07f, 0.28f)) * 0.5f);
                case Id.Deliver:
                    return Build("deliver", 0.55f, t => (Note(C5, t, 0f, 0.3f) + Note(E5, t, 0.08f, 0.3f) + Note(G5, t, 0.16f, 0.4f) + Note(C6, t, 0.24f, 0.3f)) * 0.5f);
                case Id.Discover:
                    return Build("discover", 0.95f, t =>
                        (Note(C5, t, 0f, 0.4f) + Note(E5, t, 0.1f, 0.4f) + Note(G5, t, 0.2f, 0.4f) + Note(C6, t, 0.3f, 0.5f) + Note(E6, t, 0.42f, 0.5f) + Note(G6, t, 0.54f, 0.4f)) * 0.42f);
                case Id.Poof:
                    return Build("poof", 0.3f, t =>
                    {
                        lp += 0.12f * (Hash(ni++) - lp);
                        return (lp * 3.2f + Sin(120f, t) * 0.4f) * Mathf.Exp(-9f * t) * Mathf.Min(1f, t / 0.01f);
                    });
                case Id.Boing:
                    return Build("boing", 0.4f, t => Sin(280f * (1f + 0.35f * Mathf.Sin(6.2831853f * 9f * t)) * t + 40f * t * t, 1f) * Mathf.Exp(-6f * t) * 0.6f);
                case Id.Trill:
                    return Build("trill", 0.35f, t => Sin(((int)(t * 24f) % 2 == 0) ? 700f : 940f, t) * Mathf.Exp(-5f * t) * 0.45f);
                case Id.Whoosh:
                    return Build("whoosh", 0.45f, t =>
                    {
                        lp += 0.2f * (Hash(ni++) - lp);
                        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.45f));
                        return (lp * 2.5f + Sweep(300f, 900f, 0.45f, t) * 0.25f) * env * 0.6f;
                    });
                case Id.Waltz:
                    return Build("waltz", 0.9f, t => (Note(392f, t, 0f, 0.4f) + Note(494f, t, 0.3f, 0.4f) + Note(587f, t, 0.6f, 0.45f)) * 0.5f);
                case Id.Sparkle:
                    return Build("sparkle", 0.8f, t => (Note(G6, t, 0f, 0.25f) + Note(E6, t, 0.08f, 0.25f) + Note(C6, t, 0.16f, 0.25f) + Note(2093f, t, 0.24f, 0.3f) + Note(G6, t, 0.34f, 0.35f) + Note(2637f, t, 0.44f, 0.35f)) * 0.4f);
                case Id.Thump:
                    return Build("thump", 0.22f, t =>
                    {
                        lp += 0.3f * (Hash(ni++) - lp);
                        return (Sweep(190f, 55f, 0.18f, t) * 0.9f + lp * 0.5f * Mathf.Exp(-60f * t)) * Mathf.Exp(-14f * t);
                    });
                case Id.Hurt:
                    return Build("hurt", 0.4f, t =>
                    {
                        float f = Mathf.Lerp(280f, 110f, t / 0.4f);
                        float saw = 2f * (f * t - Mathf.Floor(f * t + 0.5f));
                        return saw * Mathf.Exp(-5f * t) * 0.45f;
                    });
                case Id.Fanfare:
                    return Build("fanfare", 1.3f, t =>
                        (Note(C5, t, 0f, 0.5f) + Note(E5, t, 0.15f, 0.5f) + Note(G5, t, 0.3f, 0.5f) + Note(C6, t, 0.45f, 0.9f) + Note(E6, t, 0.45f, 0.9f) + Note(G6, t, 0.45f, 0.9f)) * 0.36f);
                case Id.Drop:
                    return Build("drop", 0.18f, t => Sweep(140f, 70f, 0.14f, t) * Mathf.Exp(-16f * t) * 0.9f);
                case Id.Perfect:
                    return Build("perfect", 0.5f, t => (Note(E6, t, 0f, 0.35f) + Note(1975f, t, 0.08f, 0.4f)) * 0.5f);
                case Id.Collapse:
                    return Build("collapse", 0.7f, t =>
                    {
                        lp += 0.06f * (Hash(ni++) - lp);
                        return lp * 4f * Mathf.Exp(-4.5f * t) * Mathf.Min(1f, t / 0.02f);
                    });
                case Id.Swoosh:
                    return Build("swoosh", 0.3f, t =>
                    {
                        lp += 0.25f * (Hash(ni++) - lp);
                        return (lp * 2f + Sweep(250f, 1100f, 0.3f, t) * 0.2f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f)) * 0.7f;
                    });
                case Id.Wrong: return Build("wrong", 0.22f, t => Sweep(330f, 250f, 0.2f, t) * Mathf.Exp(-9f * t) * 0.5f);
                default: // Defeat: enemigo vencido, un "pop" que baja
                    return Build("defeat", 0.4f, t => Sweep(900f, 180f, 0.35f, t) * Mathf.Exp(-7f * t) * 0.6f);
            }
        }

        // Musica de fondo: bucle suave de unos 11 segundos (bajo, acordes y una melodia pentatonica) sobre Do - La menor - Fa - Sol.
        static AudioClip MakeMusic()
        {
            const int rate = 22050;
            float beat = 60f / 84f;
            float total = 16f * beat;
            float[] bass = { 65.41f, 55f, 87.31f, 98f };
            float[][] chord =
            {
                new[] { 261.63f, 329.63f, 392f }, new[] { 220f, 261.63f, 329.63f },
                new[] { 261.63f, 349.23f, 440f }, new[] { 246.94f, 293.66f, 392f },
            };
            // Melodia en corcheas (0 = silencio); notas de la escala pentatonica de Do.
            float[] mel =
            {
                659f, 0, 784f, 0, 880f, 784f, 0, 659f,   587f, 0, 659f, 0, 523f, 0, 0, 0,
                440f, 0, 523f, 0, 659f, 523f, 0, 440f,   587f, 0, 784f, 0, 659f, 587f, 523f, 0,
            };
            float eighth = beat * 0.5f;
            var data = new float[Mathf.CeilToInt(total * rate)];
            float peak = 0.001f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                int bar = Mathf.Min(3, (int)(t / (4f * beat)));
                float inBar = t - bar * 4f * beat;
                float v = 0f;

                // Bajo: dos notas largas por compas.
                float bt = inBar % (2f * beat);
                v += Sin(bass[bar], t) * 0.3f * Mathf.Exp(-1.6f * bt) * Mathf.Min(1f, bt / 0.02f);

                // Acorde suave que respira durante el compas.
                float pad = Mathf.Sin(Mathf.PI * Mathf.Clamp01(inBar / (4f * beat)));
                foreach (float f in chord[bar]) v += Sin(f, t) * 0.07f * pad;

                // Melodia pulsada.
                int slot = Mathf.Min(mel.Length - 1, (int)(t / eighth));
                float mt = t - slot * eighth;
                if (mel[slot] > 0f) v += (Sin(mel[slot], t) + 0.25f * Sin(mel[slot] * 2f, t)) * 0.22f * Mathf.Exp(-4.5f * mt) * Mathf.Min(1f, mt / 0.005f);

                data[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * 0.85f;
            var clip = AudioClip.Create("music", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
