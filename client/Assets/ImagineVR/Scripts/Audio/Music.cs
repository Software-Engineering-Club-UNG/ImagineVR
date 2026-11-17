using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// `music`: a score made on the headset. Eight bars of pads, bass, an arpeggio and drums in the
    /// mood asked for, rendered on a worker thread (as the ambience loops are) and looped under the
    /// ambience, cross-faded when the mood changes. No files and no service: a mood, a tempo, a key
    /// and a few chords are enough to sound like a score.
    /// </summary>
    public sealed class Music
    {
        const int Rate = 22050;
        readonly World _world;
        readonly AudioSource[] _sources = new AudioSource[2];
        int _active;
        float _volume = 0.35f, _fadeT = 1f, _fadeSeconds = 2.5f;
        Task<float[]> _job;
        string _pending;

        sealed class Spec
        {
            public string Mood = "calm";
            public float Tempo = 90f, Energy = 0.5f;
            public int Key;            // semitones above A
            public bool Minor;
            public List<int[]> Chords = new List<int[]>();   // each chord: semitones above the key
            public bool Pad = true, Bass = true, Arp = true, Drums = true, Bells, Lead;
        }

        public Music(World world)
        {
            _world = world;
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Music" + i);
                go.transform.SetParent(world.transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
                _sources[i] = s;
            }
        }

        public void Apply(Statement s)
        {
            if (s.Flag("stop") || s.Str("mood") == "none") { Stop(2f); return; }
            if (ImagineApp.Quiet) return;
            Spec spec = Read(s);
            _volume = Mathf.Clamp01(s.Num("volume", 0.35f));
            _pending = spec.Mood;
            Spec captured = spec;
            _job = Task.Run(() => Render(captured));
            SessionLog.Write("music", "mood", spec.Mood, "tempo", spec.Tempo, "chords", spec.Chords.Count);
        }

        public void Stop(float seconds)
        {
            _job = null;
            _fadeSeconds = Mathf.Max(0.2f, seconds);
            _fadeT = 0f;
            _active = 1 - _active;     // fade the playing one out towards a silent one
            _sources[_active].Stop();
            _sources[_active].clip = null;
        }

        public void Tick(float dt)
        {
            if (_job != null && _job.IsCompleted)
            {
                float[] data = _job.IsFaulted ? null : _job.Result;
                _job = null;
                if (data != null && data.Length > 0)
                {
                    AudioClip clip = AudioClip.Create("Music_" + _pending, data.Length, 1, Rate, false);
                    clip.SetData(data, 0);
                    _active = 1 - _active;
                    AudioSource next = _sources[_active];
                    next.clip = clip;
                    next.volume = 0f;
                    next.Play();
                    _fadeT = 0f;
                    _fadeSeconds = 2.5f;
                }
            }
            if (_fadeT < 1f)
            {
                _fadeT = Mathf.Min(1f, _fadeT + dt / _fadeSeconds);
                AudioSource now = _sources[_active], old = _sources[1 - _active];
                if (now.clip != null) now.volume = _volume * _fadeT;
                old.volume = _volume * (1f - _fadeT) * (old.clip != null ? 1f : 0f);
                if (_fadeT >= 1f && old.isPlaying) { old.Stop(); old.clip = null; }
            }
        }

        // ------------------------------------------------------------------ what was asked for

        static readonly string[] Names = { "A", "A#", "B", "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#" };

        static int Note(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            string root = name.Length > 1 && (name[1] == '#' || name[1] == 'b') ? name.Substring(0, 2) : name.Substring(0, 1);
            int flat = root.EndsWith("b") ? -1 : 0;
            root = root.Replace("b", "");
            int i = System.Array.IndexOf(Names, root.ToUpperInvariant());
            return i < 0 ? 0 : (i + flat + 12) % 12;
        }

        static int[] Chord(string name, int key)
        {
            int root = (Note(name) - key + 12) % 12;
            string rest = name.Length > 1 && (name[1] == '#' || name[1] == 'b') ? name.Substring(2) : name.Substring(1);
            bool minor = rest.StartsWith("m") && !rest.StartsWith("maj");
            var notes = new List<int> { root, root + (minor ? 3 : 4), root + 7 };
            if (rest.Contains("7")) notes.Add(root + (rest.Contains("maj7") ? 11 : 10));
            if (rest.Contains("sus4")) notes[1] = root + 5;
            if (rest.Contains("sus2")) notes[1] = root + 2;
            if (rest.Contains("dim")) { notes[1] = root + 3; notes[2] = root + 6; }
            return notes.ToArray();
        }

        static Spec Read(Statement s)
        {
            var spec = new Spec { Mood = s.Str("mood", "calm") };
            switch (spec.Mood)
            {
                case "happy": case "playful": spec.Tempo = 112; spec.Energy = 0.6f; break;
                case "tense": case "action": spec.Tempo = 132; spec.Energy = 0.85f; spec.Minor = true; break;
                case "epic": case "heroic": spec.Tempo = 96; spec.Energy = 0.8f; spec.Lead = true; break;
                case "eerie": case "spooky": case "mysterious": spec.Tempo = 70; spec.Energy = 0.25f; spec.Minor = true; spec.Drums = false; spec.Bells = true; break;
                case "sad": spec.Tempo = 68; spec.Energy = 0.2f; spec.Minor = true; spec.Drums = false; break;
                case "dreamy": spec.Tempo = 76; spec.Energy = 0.3f; spec.Bells = true; spec.Drums = false; break;
                case "lofi": spec.Tempo = 84; spec.Energy = 0.45f; break;
                case "retro": spec.Tempo = 120; spec.Energy = 0.7f; spec.Lead = true; break;
                case "western": spec.Tempo = 100; spec.Energy = 0.5f; spec.Minor = true; break;
                default: spec.Tempo = 80; spec.Energy = 0.3f; spec.Drums = false; break;
            }
            if (s.Has("tempo")) spec.Tempo = Mathf.Clamp(s.Num("tempo"), 40f, 200f);
            if (s.Has("energy")) spec.Energy = Mathf.Clamp01(s.Num("energy"));
            string key = s.Str("key");
            if (!string.IsNullOrEmpty(key)) { spec.Key = Note(key); spec.Minor = key.EndsWith("m"); }
            foreach (string c in s.StrList("chords")) spec.Chords.Add(Chord(c.Length > 0 ? char.ToUpperInvariant(c[0]) + c.Substring(1) : c, spec.Key));
            if (spec.Chords.Count == 0)
            {
                // a progression that suits the mood: i-VI-III-VII in minor, I-V-vi-IV in major
                int[][] prog = spec.Minor ? new[] { new[] { 0, 3, 7 }, new[] { 8, 12, 15 }, new[] { 3, 7, 10 }, new[] { 10, 14, 17 } }
                                          : new[] { new[] { 0, 4, 7 }, new[] { 7, 11, 14 }, new[] { 9, 12, 16 }, new[] { 5, 9, 12 } };
                spec.Chords.AddRange(prog);
            }
            List<string> instruments = s.StrList("instruments");
            if (instruments.Count > 0)
            {
                spec.Pad = instruments.Contains("pad") || instruments.Contains("strings");
                spec.Bass = instruments.Contains("bass");
                spec.Arp = instruments.Contains("arp") || instruments.Contains("pluck");
                spec.Drums = instruments.Contains("drums");
                spec.Bells = instruments.Contains("bells");
                spec.Lead = instruments.Contains("lead");
            }
            return spec;
        }

        // ------------------------------------------------------------------ making it (worker thread: no Unity calls)

        static float Freq(int semitonesAboveA, int octave) => 440f * Mathf.Pow(2f, (semitonesAboveA + 12 * (octave - 4)) / 12f);

        static float[] Render(Spec spec)
        {
            int bars = 8, beats = bars * 4;
            float beat = 60f / spec.Tempo;
            int n = Mathf.CeilToInt(beats * beat * Rate);
            var data = new float[n];
            var rng = new System.Random(spec.Mood.GetHashCode());
            for (int bar = 0; bar < bars; bar++)
            {
                int[] chord = spec.Chords[bar % spec.Chords.Count];
                float barStart = bar * 4 * beat;
                if (spec.Pad) foreach (int note in chord) Tone(data, barStart, 4 * beat, Freq(spec.Key + note, 3), 0.07f, 0.5f, 0.8f, 0);
                if (spec.Bass)
                    for (int b = 0; b < 4; b++)
                        if (spec.Energy > 0.4f || b % 2 == 0) Tone(data, barStart + b * beat, beat * 0.9f, Freq(spec.Key + chord[0], 2), 0.16f, 0.01f, 0.15f, 2);
                if (spec.Arp)
                {
                    int steps = spec.Energy > 0.55f ? 8 : 4;
                    for (int i = 0; i < steps; i++)
                    {
                        int note = chord[i % chord.Length] + (i >= chord.Length ? 12 : 0);
                        Tone(data, barStart + i * 4 * beat / steps, beat * 0.45f, Freq(spec.Key + note, 4), 0.06f, 0.005f, 0.25f, 1);
                    }
                }
                if (spec.Bells && bar % 2 == 0) Tone(data, barStart + beat * (rng.Next(4)), beat * 3f, Freq(spec.Key + chord[rng.Next(chord.Length)], 5), 0.05f, 0.002f, 2f, 0);
                if (spec.Lead && bar % 4 >= 2)
                    for (int i = 0; i < 4; i++) Tone(data, barStart + i * beat, beat * 0.8f, Freq(spec.Key + chord[rng.Next(chord.Length)] + 12, 4), 0.05f, 0.02f, 0.3f, 2);
                if (spec.Drums)
                    for (int b = 0; b < 4; b++)
                    {
                        float t = barStart + b * beat;
                        if (b % 2 == 0) Kick(data, t);
                        else Snare(data, t, rng);
                        if (spec.Energy > 0.5f) { Hat(data, t, rng); Hat(data, t + beat * 0.5f, rng); }
                    }
            }
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.8f / peak;
            for (int i = 0; i < n; i++) data[i] *= gain;
            return data;
        }

        /// <summary>One note. Wave 0 is a soft sine-and-fifth pad, 1 a plucked triangle, 2 a warm saw.</summary>
        static void Tone(float[] data, float start, float length, float freq, float vol, float attack, float release, int wave)
        {
            int s = Mathf.RoundToInt(start * Rate), len = Mathf.RoundToInt((length + release) * Rate);
            int a = Mathf.Max(1, Mathf.RoundToInt(attack * Rate)), body = Mathf.RoundToInt(length * Rate);
            double phase = 0;
            float low = 0f;
            for (int i = 0; i < len; i++)
            {
                int at = (s + i) % data.Length;      // the tail of the last bar rings into the first: a seamless loop
                phase += freq / Rate;
                float p = (float)(phase - System.Math.Floor(phase));
                float v;
                if (wave == 1) v = 4f * Mathf.Abs(p - 0.5f) - 1f;
                else if (wave == 2) { v = 2f * p - 1f; low += (v - low) * 0.15f; v = low; }
                else v = Mathf.Sin(2f * Mathf.PI * p) * 0.7f + Mathf.Sin(3f * Mathf.PI * p) * 0.2f;
                float env = i < a ? i / (float)a : i < body ? (wave == 1 ? Mathf.Exp(-(i - a) / (Rate * 0.25f)) : 1f) : Mathf.Max(0f, 1f - (i - body) / (float)Mathf.Max(len - body, 1));
                data[at] += v * env * vol;
            }
        }

        static void Kick(float[] data, float start)
        {
            int s = Mathf.RoundToInt(start * Rate), len = Rate / 5;
            double phase = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                phase += Mathf.Lerp(120f, 45f, Mathf.Clamp01(t * 8f)) / Rate;
                data[(s + i) % data.Length] += Mathf.Sin((float)(phase * 2 * System.Math.PI)) * Mathf.Exp(-t * 18f) * 0.35f;
            }
        }

        static void Snare(float[] data, float start, System.Random rng)
        {
            int s = Mathf.RoundToInt(start * Rate), len = Rate / 6;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                data[(s + i) % data.Length] += ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 25f) * 0.18f;
            }
        }

        static void Hat(float[] data, float start, System.Random rng)
        {
            int s = Mathf.RoundToInt(start * Rate), len = Rate / 25;
            float last = 0f;
            for (int i = 0; i < len; i++)
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float high = noise - last;
                last = noise;
                data[(s + i) % data.Length] += high * Mathf.Exp(-i / (float)len * 5f) * 0.06f;
            }
        }
    }
}
