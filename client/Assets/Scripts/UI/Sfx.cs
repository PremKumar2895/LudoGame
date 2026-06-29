// Procedural sound effects — short decaying tones generated at runtime so we need no audio assets.
using UnityEngine;

namespace LudoGame.UI
{
    public sealed class Sfx : MonoBehaviour
    {
        private static Sfx _i;
        private AudioSource _src;

        private static Sfx I()
        {
            if (_i == null)
            {
                var go = new GameObject("Sfx");
                DontDestroyOnLoad(go);
                _i = go.AddComponent<Sfx>();
                _i._src = go.AddComponent<AudioSource>();
                _i._src.playOnAwake = false;
            }
            return _i;
        }

        public static void Roll()    => Tone(520f, 0.10f, 0.45f);
        public static void Move()    => Tone(680f, 0.06f, 0.30f);
        public static void Capture() => Tone(170f, 0.28f, 0.55f);
        public static void Win()     => Chord(0.5f);

        private static void Tone(float freq, float dur, float vol)
        {
            var i = I();
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(rate * dur));
            var clip = AudioClip.Create("sfx", n, 1, rate, false);
            var data = new float[n];
            for (int s = 0; s < n; s++)
            {
                float t = s / (float)rate;
                data[s] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 9f); // decaying sine
            }
            clip.SetData(data, 0);
            i._src.PlayOneShot(clip, vol);
        }

        // a little rising arpeggio for the win
        private static void Chord(float vol)
        {
            Tone(523f, 0.5f, vol);
            Tone(659f, 0.5f, vol * 0.8f);
            Tone(784f, 0.6f, vol * 0.8f);
        }
    }
}
