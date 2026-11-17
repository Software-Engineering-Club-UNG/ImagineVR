using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// Sounds made from a recipe: layers of oscillators and noise, each with a pitch that sweeps, an
    /// envelope, filters and an echo. The model writes one for anything the library cannot already
    /// say (`sfx`: a dragon's roar, a ray gun's charge, a cuckoo clock), and the third version's own
    /// new sound names (horn, screech, coin, fanfare...) are recipes here too. Nothing is downloaded.
    /// </summary>
    public static class Synth
    {
        const int Rate = 24000;

        public struct Layer
        {
            public string Wave;
            public float Freq, To, Dur, Attack, Decay, Vol, Vibrato, Depth, Lowpass, Highpass, Drive, Delay, Echo;
        }

        /// <summary>`sfx`: a named sound defined by its layers.</summary>
        public static void Define(Statement s)
        {
            string name = s.Str("name");
            if (string.IsNullOrEmpty(name)) return;
            var layers = new List<Layer>();
            foreach (Dictionary<string, object> d in s.ObjList("layers"))
            {
                Statement l = Statement.Wrap(d);
                layers.Add(new Layer
                {
                    Wave = l.Str("wave", "sine"), Freq = l.Num("freq", 220f), To = l.Num("to", l.Num("freq", 220f)), Dur = l.Num("dur", 0.4f),
                    Attack = l.Num("attack", 0.01f), Decay = l.Num("decay", 0.2f), Vol = l.Num("vol", 0.6f), Vibrato = l.Num("vibrato", 0f),
                    Depth = l.Num("depth", 0f), Lowpass = l.Num("lowpass", 0f), Highpass = l.Num("highpass", 0f), Drive = l.Num("drive", 0f),
                    Delay = l.Num("delay", 0f), Echo = l.Num("echo", 0f),
                });
            }
            AudioClip clip = Render(name, layers, s.Flag("loop"));
            Sfx.Register(name, clip);
            SessionLog.Write("sfx", "name", name, "layers", layers.Count, "seconds", clip != null ? clip.length : 0f);
        }

        static Layer L(string wave, float freq, float to, float dur, float vol = 0.6f, float attack = 0.005f, float decay = 0.15f, float lowpass = 0f, float drive = 0f, float delay = 0f, float vibrato = 0f, float depth = 0f, float echo = 0f, float highpass = 0f)
            => new Layer { Wave = wave, Freq = freq, To = to, Dur = dur, Vol = vol, Attack = attack, Decay = decay, Lowpass = lowpass, Drive = drive, Delay = delay, Vibrato = vibrato, Depth = depth, Echo = echo, Highpass = highpass };

        /// <summary>The recipes for the names the third version added; null for a name that is not one of them.</summary>
        public static AudioClip Builtin(string name)
        {
            Layer[] layers;
            switch (name)
            {
                case "horn": layers = new[] { L("square", 392, 392, 0.55f, 0.35f, 0.01f, 0.08f, 1800), L("square", 494, 494, 0.55f, 0.3f, 0.01f, 0.08f, 1800) }; break;
                case "screech": layers = new[] { L("noise", 3000, 3000, 0.4f, 0.4f, 0.02f, 0.15f, 5000, 0f, 0f, 0f, 0f, 0f, 1800), L("saw", 1900, 1500, 0.4f, 0.12f, 0.02f, 0.2f, 3000) }; break;
                case "crash": layers = new[] { L("noise", 0, 0, 0.6f, 0.9f, 0.001f, 0.5f, 2500, 0.6f), L("sine", 90, 40, 0.4f, 0.8f, 0.001f, 0.3f) }; break;
                case "glass": layers = new[] { L("noise", 0, 0, 0.35f, 0.5f, 0.001f, 0.3f, 0f, 0f, 0f, 0f, 0f, 0f, 3000), L("sine", 2600, 2400, 0.5f, 0.25f, 0.001f, 0.4f), L("sine", 3700, 3500, 0.4f, 0.2f, 0.001f, 0.35f, 0f, 0f, 0.03f) }; break;
                case "crack": layers = new[] { L("noise", 0, 0, 0.12f, 0.9f, 0.001f, 0.1f, 4000, 0.7f) }; break;
                case "splash": layers = new[] { L("noise", 0, 0, 0.6f, 0.6f, 0.005f, 0.45f, 1800) }; break;
                case "clang": layers = new[] { L("triangle", 520, 515, 0.8f, 0.5f, 0.001f, 0.7f), L("sine", 1340, 1330, 0.6f, 0.3f, 0.001f, 0.5f) }; break;
                case "twang": layers = new[] { L("saw", 180, 140, 0.5f, 0.5f, 0.001f, 0.45f, 1200, 0f, 0f, 6f, 0.04f) }; break;
                case "thunk": layers = new[] { L("sine", 160, 70, 0.18f, 0.9f, 0.001f, 0.15f), L("noise", 0, 0, 0.05f, 0.3f, 0.001f, 0.04f, 900) }; break;
                case "bounce": layers = new[] { L("sine", 300, 120, 0.15f, 0.7f, 0.001f, 0.12f) }; break;
                case "swing": layers = new[] { L("noise", 0, 0, 0.3f, 0.5f, 0.08f, 0.2f, 1400) }; break;
                case "roll": layers = new[] { L("noise", 0, 0, 1.2f, 0.35f, 0.1f, 0.6f, 300) }; break;
                case "strike": layers = new[] { L("noise", 0, 0, 0.08f, 0.9f, 0.001f, 0.07f, 0f, 0f, 0f, 0f, 0f, 0f, 1200), L("sine", 900, 500, 0.12f, 0.6f, 0.001f, 0.1f) }; break;
                case "click": layers = new[] { L("square", 1800, 1800, 0.02f, 0.4f, 0.001f, 0.015f) }; break;
                case "beep": layers = new[] { L("square", 880, 880, 0.15f, 0.35f, 0.005f, 0.04f, 3000) }; break;
                case "coin": layers = new[] { L("square", 988, 988, 0.08f, 0.35f, 0.002f, 0.05f, 4000), L("square", 1319, 1319, 0.35f, 0.35f, 0.002f, 0.3f, 4000, 0f, 0.08f) }; break;
                case "powerup": layers = new[] { L("square", 300, 1200, 0.45f, 0.35f, 0.01f, 0.1f, 3500, 0f, 0f, 14f, 0.03f) }; break;
                case "heal": layers = new[] { L("sine", 523, 523, 0.25f, 0.4f, 0.02f, 0.2f), L("sine", 659, 659, 0.25f, 0.4f, 0.02f, 0.2f, 0f, 0f, 0.12f), L("sine", 784, 784, 0.4f, 0.4f, 0.02f, 0.3f, 0f, 0f, 0.24f) }; break;
                case "hurt": layers = new[] { L("saw", 220, 110, 0.25f, 0.5f, 0.002f, 0.2f, 1200, 0.4f) }; break;
                case "reload": layers = new[] { L("noise", 0, 0, 0.05f, 0.6f, 0.001f, 0.04f, 3000), L("noise", 0, 0, 0.06f, 0.7f, 0.001f, 0.05f, 2200, 0f, 0.35f) }; break;
                case "door": layers = new[] { L("saw", 90, 70, 0.7f, 0.25f, 0.05f, 0.4f, 600, 0f, 0f, 3f, 0.05f) }; break;
                case "lock": layers = new[] { L("noise", 0, 0, 0.05f, 0.7f, 0.001f, 0.04f, 2500), L("noise", 0, 0, 0.05f, 0.5f, 0.001f, 0.04f, 2000, 0f, 0.09f) }; break;
                case "unlock": layers = new[] { L("noise", 0, 0, 0.05f, 0.6f, 0.001f, 0.04f, 3000), L("triangle", 660, 990, 0.25f, 0.3f, 0.005f, 0.2f, 0f, 0f, 0.08f) }; break;
                case "jet": layers = new[] { L("noise", 0, 0, 2f, 0.5f, 0.3f, 0.8f, 1200, 0f, 0f, 0f, 0f, 0f, 200) }; break;
                case "laser": layers = new[] { L("square", 1600, 200, 0.25f, 0.4f, 0.001f, 0.2f, 4000) }; break;
                case "alarm": layers = new[] { L("square", 700, 1000, 0.5f, 0.3f, 0.01f, 0.05f, 2500), L("square", 1000, 700, 0.5f, 0.3f, 0.01f, 0.05f, 2500, 0f, 0.5f) }; break;
                case "siren": layers = new[] { L("triangle", 600, 900, 0.7f, 0.4f, 0.05f, 0.05f), L("triangle", 900, 600, 0.7f, 0.4f, 0.05f, 0.1f, 0f, 0f, 0.7f) }; break;
                case "heartbeat": layers = new[] { L("sine", 60, 40, 0.12f, 0.9f, 0.002f, 0.1f), L("sine", 55, 38, 0.12f, 0.7f, 0.002f, 0.1f, 0f, 0f, 0.22f) }; break;
                case "thunder": layers = new[] { L("noise", 0, 0, 2.5f, 0.9f, 0.01f, 2f, 500, 0.5f), L("sine", 50, 30, 1.5f, 0.6f, 0.01f, 1.2f) }; break;
                case "gust": layers = new[] { L("noise", 0, 0, 1.6f, 0.4f, 0.5f, 0.8f, 700) }; break;
                case "magic": layers = new[] { L("sine", 1200, 2400, 0.6f, 0.3f, 0.01f, 0.5f, 0f, 0f, 0f, 9f, 0.05f, 0.4f) }; break;
                case "cheer": layers = new[] { L("noise", 0, 0, 1.6f, 0.5f, 0.15f, 0.9f, 2500, 0f, 0f, 5f, 0.3f, 0f, 400) }; break;
                case "applause": layers = new[] { L("noise", 0, 0, 2.2f, 0.45f, 0.2f, 1.2f, 6000, 0f, 0f, 18f, 0.6f, 0f, 900) }; break;
                case "whistle": layers = new[] { L("sine", 2000, 2200, 0.6f, 0.35f, 0.02f, 0.1f, 0f, 0f, 0f, 30f, 0.03f) }; break;
                case "fanfare": layers = new[] { L("saw", 523, 523, 0.18f, 0.3f, 0.01f, 0.06f, 2500), L("saw", 659, 659, 0.18f, 0.3f, 0.01f, 0.06f, 2500, 0f, 0.18f), L("saw", 784, 784, 0.6f, 0.35f, 0.01f, 0.4f, 2500, 0f, 0.36f), L("saw", 1047, 1047, 0.6f, 0.25f, 0.01f, 0.4f, 2500, 0f, 0.36f) }; break;
                case "lose": layers = new[] { L("triangle", 392, 392, 0.3f, 0.4f, 0.01f, 0.1f), L("triangle", 370, 370, 0.3f, 0.4f, 0.01f, 0.1f, 0f, 0f, 0.3f), L("triangle", 330, 300, 0.8f, 0.4f, 0.01f, 0.6f, 0f, 0f, 0.6f, 5f, 0.02f) }; break;
                case "ticktock": layers = new[] { L("square", 2400, 2400, 0.02f, 0.3f, 0.001f, 0.015f), L("square", 1800, 1800, 0.02f, 0.3f, 0.001f, 0.015f, 0f, 0f, 0.5f) }; break;
                case "bark": layers = new[] { L("saw", 420, 260, 0.14f, 0.6f, 0.005f, 0.1f, 1400, 0.5f) }; break;
                case "meow": layers = new[] { L("saw", 500, 800, 0.5f, 0.4f, 0.05f, 0.3f, 2000, 0f, 0f, 6f, 0.02f) }; break;
                case "moo": layers = new[] { L("saw", 130, 110, 1.1f, 0.5f, 0.1f, 0.4f, 700, 0f, 0f, 4f, 0.02f) }; break;
                case "roar": layers = new[] { L("saw", 110, 70, 1.4f, 0.7f, 0.1f, 0.7f, 900, 0.8f, 0f, 9f, 0.06f), L("noise", 0, 0, 1.2f, 0.4f, 0.1f, 0.6f, 600) }; break;
                case "growl": layers = new[] { L("saw", 90, 75, 0.7f, 0.5f, 0.05f, 0.4f, 600, 0.7f, 0f, 22f, 0.08f) }; break;
                case "chirp": layers = new[] { L("sine", 3000, 4200, 0.08f, 0.3f, 0.005f, 0.06f), L("sine", 3200, 4400, 0.08f, 0.3f, 0.005f, 0.06f, 0f, 0f, 0.12f) }; break;
                case "neigh": layers = new[] { L("saw", 700, 500, 0.9f, 0.4f, 0.05f, 0.4f, 1800, 0f, 0f, 14f, 0.06f) }; break;
                case "babble": layers = new[] { L("saw", 180, 220, 0.12f, 0.35f, 0.01f, 0.05f, 1200, 0f, 0f), L("saw", 240, 190, 0.12f, 0.35f, 0.01f, 0.05f, 1200, 0f, 0.14f), L("saw", 200, 260, 0.16f, 0.35f, 0.01f, 0.08f, 1200, 0f, 0.3f) }; break;
                default: return null;
            }
            return Render(name, new List<Layer>(layers), false);
        }

        /// <summary>Mixes the layers into one clip. Deterministic noise, so the same recipe always sounds the same.</summary>
        public static AudioClip Render(string name, List<Layer> layers, bool loop)
        {
            if (layers.Count == 0) return null;
            float length = 0f;
            foreach (Layer l in layers) length = Mathf.Max(length, l.Delay + l.Dur + Mathf.Max(l.Decay, 0f) + (l.Echo > 0f ? 0.6f : 0f));
            length = Mathf.Clamp(length, 0.03f, 8f);
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            uint seed = 22222;
            foreach (Layer l in layers)
            {
                int start = Mathf.RoundToInt(l.Delay * Rate), body = Mathf.RoundToInt(l.Dur * Rate), tail = Mathf.RoundToInt(Mathf.Max(l.Decay, 0.005f) * Rate);
                int attack = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(l.Attack, 0.001f) * Rate));
                double phase = 0;
                float low = 0f, high = 0f, lastIn = 0f;
                float lpA = l.Lowpass > 0f ? 1f - Mathf.Exp(-2f * Mathf.PI * l.Lowpass / Rate) : 1f;
                float hpA = l.Highpass > 0f ? Mathf.Exp(-2f * Mathf.PI * l.Highpass / Rate) : 0f;
                int total = body + tail;
                for (int i = 0; i < total && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float sweep = l.Dur > 0f ? Mathf.Clamp01(t / l.Dur) : 1f;
                    float f = Mathf.Lerp(l.Freq, l.To, sweep);
                    if (l.Vibrato > 0f) f *= 1f + l.Depth * Mathf.Sin(2f * Mathf.PI * l.Vibrato * t);
                    phase += f / Rate;
                    float p = (float)(phase - System.Math.Floor(phase));
                    float s;
                    switch (l.Wave)
                    {
                        case "square": s = p < 0.5f ? 1f : -1f; break;
                        case "saw": s = 2f * p - 1f; break;
                        case "triangle": s = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                        case "noise": seed = seed * 1664525u + 1013904223u; s = (seed >> 9) / 4194304f - 1f; break;
                        default: s = Mathf.Sin(2f * Mathf.PI * p); break;
                    }
                    if (l.Drive > 0f) s = (float)System.Math.Tanh(s * (1f + l.Drive * 6f));
                    low += (s - low) * lpA;
                    s = l.Lowpass > 0f ? low : s;
                    if (l.Highpass > 0f) { high = hpA * (high + s - lastIn); lastIn = s; s = high; }
                    float env = i < attack ? i / (float)attack : i < body ? 1f : 1f - (i - body) / (float)Mathf.Max(tail, 1);
                    data[start + i] += s * env * Mathf.Clamp01(l.Vol);
                    if (l.Echo > 0f)
                    {
                        int echoAt = start + i + Rate / 5;
                        if (echoAt < n) data[echoAt] += s * env * Mathf.Clamp01(l.Vol) * l.Echo * 0.6f;
                    }
                }
            }
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = peak > 0.95f ? 0.95f / peak : 1f;
            for (int i = 0; i < n; i++) data[i] *= gain;
            if (loop)
            {
                // a loop's ends are faded into each other, so it does not click each time round
                int fade = Mathf.Min(n / 8, Rate / 20);
                for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] = data[i] * k + data[n - fade + i] * (1f - k); }
                System.Array.Resize(ref data, n - fade);
                n -= fade;
            }
            AudioClip clip = AudioClip.Create("Synth_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
