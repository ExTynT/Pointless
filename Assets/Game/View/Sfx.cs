using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pointless
{
    public sealed class Sfx
    {
        const int Voices = 8;
        const float Tau = Mathf.PI * 2f;
        const int TopStep = 19;

        static readonly int[] scale = { 0, 2, 4, 7, 9, 12, 14, 16, 19 };
        static readonly int[] fanfare = { 7, 12, 16, 19 };
        static readonly List<AudioSource> found = new List<AudioSource>();

        readonly AudioSource[] voices = new AudioSource[Voices];
        readonly AudioClip pluck, thud, tick, sink;
        int next;

        public bool Muted;

        public Sfx(GameObject host)
        {
            host.GetComponents(found);
            for (int i = 0; i < Voices; i++)
            {
                AudioSource voice = i < found.Count ? found[i] : host.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voice.bypassEffects = true;
                voice.bypassListenerEffects = true;
                voice.bypassReverbZones = true;
                voices[i] = voice;
            }
            found.Clear();

            pluck = Make("Pluck", 0.6f, t => Mathf.Min(1f, t / 0.004f) * (
                Mathf.Sin(Tau * 330f * t) * Mathf.Exp(-t * 8f) * 0.6f +
                Mathf.Sin(Tau * 660f * t) * Mathf.Exp(-t * 13f) * 0.22f +
                Mathf.Sin(Tau * 1320f * t) * Mathf.Exp(-t * 22f) * 0.08f));
            thud = Make("Thud", 0.16f, t => Mathf.Min(1f, t / 0.002f) *
                Mathf.Sin(Tau * (110f * t + 2.75f * (1f - Mathf.Exp(-40f * t)))) * Mathf.Exp(-t * 30f) * 0.9f);
            tick = Make("Tick", 0.05f, t => Mathf.Sin(Tau * 1400f * t) * Mathf.Exp(-t * 140f) * 0.4f);
            sink = Make("Sink", 1.1f, t => Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 3.5f) * 0.5f * (
                Mathf.Sin(Tau * (130f * t + 22f * (1f - Mathf.Exp(-3f * t)))) +
                Mathf.Sin(Tau * (97f * t + 16.5f * (1f - Mathf.Exp(-3f * t)))) * 0.6f));
        }

        public void Pick() => Play(tick, 1f, 0.5f, 0f);

        public void Back() => Play(tick, 0.6f, 0.4f, 0f);

        public void Place(int size) => Play(thud, 1.3f - size * 0.06f, 0.8f, 0f);

        public void Deal(float delay)
        {
            for (int i = 0; i < 3; i++) Play(tick, 0.75f + i * 0.1f, 0.22f, delay + i * 0.07f);
        }

        public void Clear(int combo, int lines)
        {
            int step = scale[Mathf.Clamp(combo - 1, 0, scale.Length - 1)];
            Play(pluck, Pitch(step), 0.7f, 0f);
            if (lines > 1) Play(pluck, Pitch(step + 7), 0.55f, 0.07f);
            if (lines > 2) Play(pluck, Pitch(step + 12), 0.5f, 0.14f);
        }

        public void Perfect()
        {
            for (int i = 0; i < fanfare.Length; i++) Play(pluck, Pitch(fanfare[i]), 0.55f, 0.18f + i * 0.075f);
        }

        public void Over(float delay) => Play(sink, 1f, 0.7f, delay);

        public void Break() => Play(tick, 0.42f, 0.5f, 0.05f);

        public void Record()
        {
            Play(pluck, Pitch(12), 0.6f, 0.05f);
            Play(pluck, Pitch(19), 0.6f, 0.17f);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(pluck);
            UnityEngine.Object.Destroy(thud);
            UnityEngine.Object.Destroy(tick);
            UnityEngine.Object.Destroy(sink);
        }

        void Play(AudioClip clip, float pitch, float volume, float delay)
        {
            if (Muted) return;
            AudioSource voice = voices[next];
            next = (next + 1) % Voices;
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = volume;
            if (delay > 0f) voice.PlayDelayed(delay);
            else voice.Play();
        }

        static float Pitch(int step) => Mathf.Pow(2f, Mathf.Min(step, TopStep) / 12f);

        static AudioClip Make(string name, float seconds, Func<float, float> wave)
        {
            int rate = AudioSettings.outputSampleRate;
            int count = Mathf.CeilToInt(seconds * rate);
            int tail = Mathf.Max(1, rate / 100);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = wave((float)i / rate) * Mathf.Min(1f, (float)(count - i) / tail);
            AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
