using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// Short sound effects, synthesised at startup so the project needs no audio files.
    /// A clip placed at Resources/Sfx/&lt;name&gt; replaces the synthesised one.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 24000;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static AudioSource[] _pool;
        static int _next;
        static Transform _root;

        public static void Init(Transform parent)
        {
            if (_root != null) return;
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            _root = go.transform;
            _pool = new AudioSource[10];
            for (int i = 0; i < _pool.Length; i++)
            {
                var child = new GameObject("Voice" + i);
                child.transform.SetParent(_root, false);
                AudioSource src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1f;
                src.maxDistance = 40f;
                src.dopplerLevel = 0f;
                _pool[i] = src;
            }
        }

        public static void Play(string name, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (_pool == null) return;
            AudioClip clip = Get(name);
            if (clip == null) return;
            AudioSource src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.transform.position = position;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = Mathf.Clamp(pitch, 0.3f, 3f);
            src.Play();
        }

        public static AudioClip Get(string name)
        {
            if (Clips.TryGetValue(name, out AudioClip clip)) return clip;
            clip = Resources.Load<AudioClip>("Sfx/" + name);
            if (clip == null)
            {
                float[] data = Synthesise(name);
                if (data != null)
                {
                    clip = AudioClip.Create(name, data.Length, 1, Rate, false);
                    clip.SetData(data, 0);
                }
            }
            Clips[name] = clip;
            return clip;
        }

        // ------------------------------------------------------------------ synthesis

        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        static void Tone(float[] buf, float start, float length, float f0, float f1, float volume, float decay = 4f, float attack = 0.004f)
        {
            int s0 = Mathf.Clamp((int)(start * Rate), 0, buf.Length), n = Mathf.Min((int)(length * Rate), buf.Length - s0);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = i / (float)n;
                float f = Mathf.Lerp(f0, f1, u);
                phase += 2.0 * Math.PI * f / Rate;
                float env = Mathf.Min(1f, t / attack) * Mathf.Exp(-decay * u) * (1f - u * u * u);
                buf[s0 + i] += (float)Math.Sin(phase) * env * volume;
            }
        }

        /// <summary>A tone that swells and dies away with a wobble in its pitch, as a throat makes.</summary>
        static void Voice(float[] buf, float start, float length, float f0, float f1, float volume, float wobbleHz, float wobble, float attack)
        {
            int s0 = Mathf.Clamp((int)(start * Rate), 0, buf.Length), n = Mathf.Min((int)(length * Rate), buf.Length - s0);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = i / (float)n;
                float f = Mathf.Lerp(f0, f1, u) * (1f + wobble * Mathf.Sin(2f * Mathf.PI * wobbleHz * t) * u);
                phase += 2.0 * Math.PI * f / Rate;
                float env = Mathf.Min(1f, t / attack) * (1f - u * u);
                // a little of the octave above gives it a rasp
                buf[s0 + i] += ((float)Math.Sin(phase) + 0.35f * (float)Math.Sin(phase * 2.0 + 0.5)) * env * volume;
            }
        }

        /// <summary>A sound that plays round and round on its own source: an engine, the wind of speed.</summary>
        public static AudioSource Loop(string name, Transform parent, float spatial)
        {
            var go = new GameObject("Loop_" + name);
            go.transform.SetParent(parent, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = Get(name);
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = spatial;
            src.dopplerLevel = 0f;
            src.volume = 0f;
            return src;
        }

        static void Noise(float[] buf, float start, float length, float volume, float decay, float lowpass, int seed)
        {
            var rng = new System.Random(seed);
            int s0 = Mathf.Clamp((int)(start * Rate), 0, buf.Length), n = Mathf.Min((int)(length * Rate), buf.Length - s0);
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float x = (float)rng.NextDouble() * 2f - 1f;
                y += (x - y) * lowpass;
                buf[s0 + i] += y * Mathf.Exp(-decay * u) * (1f - u) * volume;
            }
        }

        static float[] Synthesise(string name)
        {
            float[] b;
            switch (name)
            {
                case "blip":      // a tool call landing
                    b = Buffer(0.12f); Tone(b, 0, 0.12f, 880, 880, 0.22f, 7); Tone(b, 0, 0.1f, 1760, 1760, 0.06f, 9); return b;
                case "tick":      // a character being typed
                    b = Buffer(0.03f); Noise(b, 0, 0.03f, 0.12f, 6, 0.6f, 3); return b;
                case "listen_on":
                    b = Buffer(0.22f); Tone(b, 0, 0.1f, 520, 520, 0.2f, 5); Tone(b, 0.09f, 0.13f, 780, 780, 0.2f, 5); return b;
                case "listen_off":
                    b = Buffer(0.22f); Tone(b, 0, 0.1f, 780, 780, 0.18f, 5); Tone(b, 0.09f, 0.13f, 520, 520, 0.18f, 5); return b;
                case "done":      // a turn finishing
                    b = Buffer(0.9f); Tone(b, 0, 0.6f, 523.25f, 523.25f, 0.14f, 4); Tone(b, 0.08f, 0.6f, 659.25f, 659.25f, 0.12f, 4);
                    Tone(b, 0.16f, 0.7f, 783.99f, 783.99f, 0.12f, 4); return b;
                case "error":
                    b = Buffer(0.35f); Tone(b, 0, 0.16f, 220, 200, 0.2f, 3); Tone(b, 0.17f, 0.18f, 165, 150, 0.2f, 3); return b;
                case "pop":
                    b = Buffer(0.14f); Noise(b, 0, 0.09f, 0.6f, 5, 0.5f, 7); Tone(b, 0, 0.1f, 600, 120, 0.35f, 5, 0.001f); return b;
                case "thud":
                    b = Buffer(0.22f); Tone(b, 0, 0.2f, 130, 55, 0.5f, 5, 0.002f); Noise(b, 0, 0.05f, 0.25f, 6, 0.15f, 11); return b;
                case "tap":       // light things colliding
                    b = Buffer(0.08f); Tone(b, 0, 0.07f, 420, 300, 0.3f, 7, 0.001f); Noise(b, 0, 0.03f, 0.2f, 6, 0.4f, 13); return b;
                case "bell":
                    b = Buffer(2.4f); Tone(b, 0, 2.4f, 440, 440, 0.2f, 3.5f, 0.002f); Tone(b, 0, 2.0f, 1188, 1188, 0.1f, 4.5f, 0.002f);
                    Tone(b, 0, 1.5f, 1760, 1760, 0.07f, 6f, 0.002f); Tone(b, 0, 1.0f, 2376, 2376, 0.05f, 8f, 0.002f); return b;
                case "drum":
                    b = Buffer(0.5f); Tone(b, 0, 0.45f, 190, 70, 0.6f, 5, 0.001f); Noise(b, 0, 0.06f, 0.3f, 6, 0.3f, 17); return b;
                case "chime":
                    b = Buffer(1.4f); Tone(b, 0, 1.4f, 1318.5f, 1318.5f, 0.14f, 4, 0.002f); Tone(b, 0, 1.0f, 2637f, 2637f, 0.06f, 6, 0.002f);
                    Tone(b, 0, 0.7f, 3951f, 3951f, 0.03f, 8, 0.002f); return b;
                case "grab":
                    b = Buffer(0.06f); Tone(b, 0, 0.05f, 300, 380, 0.2f, 6, 0.002f); return b;
                case "release":
                    b = Buffer(0.06f); Tone(b, 0, 0.05f, 380, 260, 0.14f, 6, 0.002f); return b;
                case "zap":       // a blaster: a crack, a falling whine, and a thump you feel
                    b = Buffer(0.32f); Noise(b, 0, 0.04f, 0.5f, 5, 0.8f, 31);
                    Tone(b, 0, 0.26f, 2400, 180, 0.3f, 4, 0.001f); Tone(b, 0, 0.22f, 3600, 300, 0.1f, 5, 0.001f);
                    Tone(b, 0, 0.18f, 150, 50, 0.45f, 5, 0.001f); return b;
                case "groan":     // a zombie somewhere nearby: a low voice that wavers and breaks
                    b = Buffer(1.3f); Voice(b, 0, 1.3f, 104, 70, 0.3f, 6.5f, 0.06f, 0.25f);
                    Voice(b, 0.05f, 1.2f, 157, 104, 0.14f, 7.5f, 0.08f, 0.25f); Voice(b, 0.1f, 1.0f, 312, 205, 0.05f, 9f, 0.1f, 0.3f);
                    Noise(b, 0, 1.2f, 0.16f, 2, 0.06f, 29); return b;
                case "step":      // a footstep
                    b = Buffer(0.1f); Noise(b, 0, 0.07f, 0.5f, 6, 0.12f, 37); Tone(b, 0, 0.08f, 95, 60, 0.3f, 6, 0.002f); return b;
                case "hoof":
                    b = Buffer(0.1f); Tone(b, 0, 0.08f, 700, 380, 0.3f, 8, 0.001f); Noise(b, 0, 0.04f, 0.35f, 6, 0.5f, 41);
                    Tone(b, 0, 0.09f, 130, 80, 0.25f, 6, 0.001f); return b;
                case "flap":      // one beat of large wings
                    b = Buffer(0.45f);
                    {
                        var rng = new System.Random(43);
                        float y = 0f;
                        for (int i = 0; i < b.Length; i++)
                        {
                            float u = i / (float)b.Length, swell = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2f);
                            y += (((float)rng.NextDouble() * 2f - 1f) - y) * Mathf.Lerp(0.02f, 0.14f, swell);
                            b[i] = y * swell * 0.9f;
                        }
                    }
                    return b;
                case "engine":    // loops: a motor ticking over; pitch it up with speed
                    b = Buffer(1f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = i / (float)Rate, x = 0f;
                        // whole numbers of cycles in one second, so the loop has no seam
                        for (int k = 1; k <= 6; k++) x += Mathf.Sin(2f * Mathf.PI * 46f * k * t + k * 0.7f) / k;
                        float chug = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 23f * t);
                        b[i] = x * chug * 0.16f + Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.05f;
                    }
                    return b;
                case "rush":      // loops: air going past
                    b = Buffer(2f);
                    {
                        var rng = new System.Random(47);
                        float y = 0f, z = 0f;
                        for (int i = 0; i < b.Length; i++)
                        {
                            y += (((float)rng.NextDouble() * 2f - 1f) - y) * 0.06f;
                            z += (y - z) * 0.3f;
                            b[i] = z * 1.6f;
                        }
                        // fold the tail over the head so the end runs into the start
                        int fade = Rate / 4;
                        for (int i = 0; i < fade; i++)
                        {
                            float u = i / (float)fade;
                            b[i] = b[i] * u + b[b.Length - fade + i] * (1f - u);
                        }
                        System.Array.Resize(ref b, b.Length - fade);
                    }
                    return b;
                case "sparkle":
                    b = Buffer(0.7f);
                    for (int i = 0; i < 7; i++) Tone(b, i * 0.07f, 0.25f, 1600 + i * 310, 1600 + i * 310, 0.07f, 6, 0.002f);
                    return b;
                case "confetti":
                    b = Buffer(1.0f); Noise(b, 0, 0.25f, 0.3f, 4, 0.7f, 19);
                    for (int i = 0; i < 6; i++) Tone(b, 0.05f + i * 0.09f, 0.4f, 1047 * Mathf.Pow(1.122f, i * 2), 1047 * Mathf.Pow(1.122f, i * 2), 0.08f, 5, 0.002f);
                    return b;
                case "whoosh":
                    b = Buffer(0.6f);
                    {
                        var rng = new System.Random(23);
                        float y = 0f;
                        for (int i = 0; i < b.Length; i++)
                        {
                            float u = i / (float)b.Length;
                            y += (((float)rng.NextDouble() * 2f - 1f) - y) * Mathf.Lerp(0.03f, 0.35f, Mathf.Sin(u * Mathf.PI));
                            b[i] = y * Mathf.Sin(u * Mathf.PI) * 0.5f;
                        }
                    }
                    return b;
                case "reveal":    // light entering the dark room
                    b = Buffer(3.2f);
                    Tone(b, 0, 3.2f, 65.4f, 65.4f, 0.22f, 1.2f, 0.9f);
                    Tone(b, 0, 3.2f, 98f, 98f, 0.14f, 1.2f, 1.1f);
                    Tone(b, 0.3f, 2.9f, 261.6f, 261.6f, 0.08f, 1.5f, 0.8f);
                    Tone(b, 0.6f, 2.6f, 392f, 392f, 0.07f, 1.6f, 0.8f);
                    Tone(b, 0.9f, 2.3f, 523.25f, 523.25f, 0.06f, 1.8f, 0.7f);
                    Tone(b, 1.2f, 2.0f, 783.99f, 783.99f, 0.045f, 2f, 0.6f);
                    return b;
                default:
                    return null;
            }
        }
    }
}
