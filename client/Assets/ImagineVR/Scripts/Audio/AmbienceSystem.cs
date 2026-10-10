using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public sealed class AmbienceDef : Entry
    {
        public Action<LoopSynth> Recipe;
    }

    public static partial class Library
    {
        public static readonly List<AmbienceDef> AmbienceList = new List<AmbienceDef>();

        static void Loop(string id, string desc, string tags, string aliases, Action<LoopSynth> recipe)
        {
            AmbienceList.Add(new AmbienceDef { Id = id, Desc = desc, Tags = Split(tags), Aliases = Split(aliases), Recipe = recipe });
        }

        static partial void RegisterAmbience()
        {
            Loop("birds_breeze", "Daytime birdsong and a light breeze", "day,outdoor,calm,park,meadow,golf", "birdsong,birds,nature", s =>
            { s.Wind(0.10f, 0.06f, 2); s.Birds(0.11f, 26, 2600f, 5200f); });
            Loop("forest", "Deep forest: many birds, rustling leaves", "woods,jungle,trees,nature", "woods,woodland,jungle", s =>
            { s.Wind(0.08f, 0.05f, 3); s.Rustle(0.07f, 3); s.Birds(0.10f, 44, 1800f, 5600f); });
            Loop("night_crickets", "Warm night: crickets and stillness", "night,summer,camp,calm,field", "crickets,night sounds", s =>
            { s.Wind(0.05f, 0.04f, 1); s.Crickets(0.07f); });
            Loop("ocean_waves", "Waves rolling in and drawing back", "beach,sea,shore,island,coast", "waves,sea,surf,beach", s =>
            { s.Waves(0.3f, 2); s.Wind(0.05f, 0.08f, 1); });
            Loop("wind", "Open wind across a wide empty place", "desert,mountain,plain,empty,arctic", "breeze,gusts,windy", s =>
            { s.Wind(0.22f, 0.07f, 3); });
            Loop("desert_wind", "Dry whistling wind over sand", "desert,dunes,canyon,dry,hot", "sandstorm,dry wind", s =>
            { s.Wind(0.16f, 0.06f, 2); s.Whistle(0.05f, 700f, 2); });
            Loop("rain", "Steady rainfall", "rainy,wet,storm,grey", "rainfall,raining,drizzle", s =>
            { s.Rain(0.2f); });
            Loop("storm", "Heavy rain with rolling thunder", "thunder,rain,dark,dramatic", "thunderstorm,tempest", s =>
            { s.Rain(0.24f); s.Rumble(0.26f, 2); s.Wind(0.1f, 0.07f, 2); });
            Loop("city_night", "City at night: a low hum and passing traffic", "urban,traffic,downtown,street", "city,traffic,urban", s =>
            { s.Rumble(0.1f, 1); s.Hum(0.03f, 60f); s.Passes(0.1f, 3); });
            Loop("space_hum", "The deep hum of open space", "space,station,void,sci-fi,moon", "space,cosmic,spaceship", s =>
            { s.Drone(0.14f, 55f, 55.7f); s.Pad(0.035f, new[] { 220f, 329.6f, 440f }, 2); });
            Loop("underwater", "Muffled deep water and rising bubbles", "ocean,sea,reef,deep,diving", "under the sea,submerged", s =>
            { s.Rumble(0.2f, 2); s.Bubbles(0.07f, 22); });
            Loop("fire_crackle", "A crackling fire", "campfire,camp,cozy,night,torch,volcano", "campfire,fireplace,bonfire", s =>
            { s.Rumble(0.08f, 1); s.Crackle(0.2f, 260); });
            Loop("cave_drips", "A quiet cave: dripping water and distant echo", "cave,cavern,underground,dungeon", "cave,cavern,dripping", s =>
            { s.Rumble(0.07f, 1); s.Drips(0.14f, 9); });
            Loop("synth_pad", "A slow warm synthesiser chord", "retro,neon,dream,80s,calm,music", "synth,pad,vaporwave music", s =>
            { s.Pad(0.085f, new[] { 110f, 164.8f, 220f, 261.6f, 329.6f }, 2); s.Drone(0.05f, 55f, 55.3f); });
            Loop("zen_chimes", "Soft wind and a few slow chimes", "garden,temple,japan,peaceful,dream,heaven", "wind chimes,chimes,meditation", s =>
            { s.Wind(0.06f, 0.05f, 2); s.Chimes(0.09f, 9); });
            Loop("spooky", "A low uneasy drone with distant moans", "haunted,halloween,graveyard,creepy,horror", "eerie,creepy,haunted", s =>
            { s.Drone(0.1f, 49f, 51.9f); s.Wind(0.08f, 0.05f, 2); s.Whistle(0.035f, 310f, 1); });
        }
    }

    /// <summary>Builds a seamless loop out of simple layers. All layers repeat cleanly at the loop length.</summary>
    public sealed class LoopSynth
    {
        public const int Rate = 22050;
        public const float Seconds = 10f;
        const float Overlap = 0.8f;
        readonly float[] _buf = new float[(int)((Seconds + Overlap) * Rate)];
        readonly System.Random _rng;

        public LoopSynth(int seed) { _rng = new System.Random(seed); }

        float R() => (float)_rng.NextDouble();
        float White() => (float)_rng.NextDouble() * 2f - 1f;

        /// <summary>A whole number of cycles over the loop, so it wraps without a click.</summary>
        static float Lfo(int i, int cycles, float phase = 0f)
        {
            return Mathf.Sin((i / (Seconds * Rate)) * cycles * Mathf.PI * 2f + phase);
        }

        public void Wind(float volume, float cutoff, int gusts)
        {
            float y = 0f, y2 = 0f;
            float phase = R() * 6f;
            for (int i = 0; i < _buf.Length; i++)
            {
                float gust = 0.6f + 0.4f * Lfo(i, gusts, phase) * (0.6f + 0.4f * Lfo(i, gusts * 2 + 1, phase * 2f));
                float c = cutoff * (0.6f + 0.6f * gust);
                y += (White() - y) * c;
                y2 += (y - y2) * c;
                _buf[i] += y2 * 5f * volume * gust;
            }
        }

        public void Rustle(float volume, int gusts)
        {
            float y = 0f, phase = R() * 6f;
            for (int i = 0; i < _buf.Length; i++)
            {
                float x = White();
                y += (x - y) * 0.5f;
                float gust = Mathf.Max(0f, Lfo(i, gusts, phase));
                _buf[i] += (x - y) * volume * gust * gust;
            }
        }

        public void Whistle(float volume, float freq, int sweeps)
        {
            double ph = 0;
            float phase = R() * 6f;
            for (int i = 0; i < _buf.Length; i++)
            {
                float f = freq * (1f + 0.18f * Lfo(i, sweeps, phase));
                ph += 2.0 * Math.PI * f / Rate;
                float amp = Mathf.Max(0f, Lfo(i, sweeps * 2 + 1, phase * 1.3f));
                _buf[i] += (float)Math.Sin(ph) * volume * amp * amp;
            }
        }

        public void Waves(float volume, int swells)
        {
            float y = 0f, y2 = 0f;
            for (int i = 0; i < _buf.Length; i++)
            {
                float swell = 0.5f + 0.5f * Lfo(i, swells, -1.2f);
                float crest = swell * swell;
                float c = 0.02f + 0.22f * crest;
                y += (White() - y) * c;
                y2 += (y - y2) * c;
                _buf[i] += y2 * 2.6f * volume * (0.18f + 0.82f * crest);
            }
        }

        public void Rain(float volume)
        {
            float y = 0f;
            for (int i = 0; i < _buf.Length; i++)
            {
                float x = White();
                y += (x - y) * 0.35f;
                _buf[i] += (x - y) * volume * 0.8f + y * volume * 0.25f;
            }
            int drops = 420;
            for (int d = 0; d < drops; d++) Blip(R() * Seconds, 0.012f, 1800f + R() * 2600f, 900f, volume * (0.2f + R() * 0.4f), 6f);
        }

        public void Rumble(float volume, int swells)
        {
            float y = 0f, y2 = 0f, phase = R() * 6f;
            for (int i = 0; i < _buf.Length; i++)
            {
                y += (White() - y) * 0.012f;
                y2 += (y - y2) * 0.012f;
                float swell = 0.55f + 0.45f * Lfo(i, swells, phase);
                _buf[i] += y2 * 40f * volume * swell;
            }
        }

        public void Hum(float volume, float freq)
        {
            // snap to a whole number of cycles per loop
            float f = Mathf.Round(freq * Seconds) / Seconds;
            for (int i = 0; i < _buf.Length; i++)
            {
                float t = i / (float)Rate;
                _buf[i] += (Mathf.Sin(t * f * Mathf.PI * 2f) + 0.5f * Mathf.Sin(t * f * 2f * Mathf.PI * 2f)) * volume;
            }
        }

        public void Drone(float volume, float f1, float f2)
        {
            f1 = Mathf.Round(f1 * Seconds) / Seconds;
            f2 = Mathf.Round(f2 * Seconds) / Seconds;
            for (int i = 0; i < _buf.Length; i++)
            {
                float t = i / (float)Rate;
                float s = Mathf.Sin(t * f1 * Mathf.PI * 2f) + Mathf.Sin(t * f2 * Mathf.PI * 2f) + 0.3f * Mathf.Sin(t * f1 * 2f * Mathf.PI * 2f);
                _buf[i] += s * volume * 0.5f * (0.8f + 0.2f * Lfo(i, 1));
            }
        }

        public void Pad(float volume, float[] notes, int swells)
        {
            for (int n = 0; n < notes.Length; n++)
            {
                float f = Mathf.Round(notes[n] * Seconds) / Seconds;
                float phase = n * 1.7f;
                for (int i = 0; i < _buf.Length; i++)
                {
                    float t = i / (float)Rate;
                    float s = Mathf.Sin(t * f * Mathf.PI * 2f) + 0.3f * Mathf.Sin(t * f * 2f * Mathf.PI * 2f) + 0.12f * Mathf.Sin(t * f * 3f * Mathf.PI * 2f);
                    _buf[i] += s * volume * (0.6f + 0.4f * Lfo(i, swells, phase));
                }
            }
        }

        void Blip(float start, float length, float f0, float f1, float volume, float decay)
        {
            int s0 = (int)(start * Rate), n = (int)(length * Rate);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int k = s0 + i;
                if (k >= _buf.Length) break;
                float u = i / (float)n;
                ph += 2.0 * Math.PI * Mathf.Lerp(f0, f1, u) / Rate;
                _buf[k] += (float)Math.Sin(ph) * Mathf.Sin(u * Mathf.PI) * Mathf.Exp(-decay * u) * volume;
            }
        }

        public void Birds(float volume, int phrases, float fMin, float fMax)
        {
            for (int p = 0; p < phrases; p++)
            {
                float start = R() * (Seconds - 1f);
                float f = Mathf.Lerp(fMin, fMax, R());
                int notes = 2 + _rng.Next(5);
                float loud = volume * (0.3f + 0.7f * R());
                float gap = 0.07f + R() * 0.09f;
                bool rising = R() > 0.5f;
                for (int n = 0; n < notes; n++)
                {
                    float f0 = f * (1f + (rising ? 0.06f : -0.05f) * n);
                    Blip(start + n * gap, 0.05f + R() * 0.05f, f0, f0 * (0.85f + R() * 0.4f), loud, 1.5f);
                }
            }
        }

        public void Crickets(float volume)
        {
            for (int voice = 0; voice < 3; voice++)
            {
                float f = 4200f + voice * 380f, period = 0.42f + voice * 0.11f, offset = R() * period;
                float loud = volume * (0.5f + 0.5f * R());
                for (float t = offset; t < Seconds + Overlap - 0.2f; t += period)
                {
                    for (int pulse = 0; pulse < 3; pulse++) Blip(t + pulse * 0.035f, 0.022f, f, f, loud, 0.5f);
                }
            }
        }

        public void Bubbles(float volume, int count)
        {
            for (int b = 0; b < count; b++)
            {
                float f = 300f + R() * 700f;
                Blip(R() * Seconds, 0.05f + R() * 0.08f, f, f * (1.6f + R()), volume * (0.4f + 0.6f * R()), 1f);
            }
        }

        public void Crackle(float volume, int count)
        {
            for (int c = 0; c < count; c++)
            {
                int s0 = (int)(R() * Seconds * Rate), n = 30 + _rng.Next(160);
                float loud = volume * R() * R();
                for (int i = 0; i < n && s0 + i < _buf.Length; i++) _buf[s0 + i] += White() * loud * (1f - i / (float)n);
            }
        }

        public void Drips(float volume, int count)
        {
            for (int d = 0; d < count; d++)
            {
                float start = R() * (Seconds - 1f), f = 900f + R() * 900f, loud = volume * (0.5f + 0.5f * R());
                Blip(start, 0.06f, f, f * 1.5f, loud, 2f);
                Blip(start + 0.19f, 0.06f, f, f * 1.5f, loud * 0.35f, 2f);   // echo
                Blip(start + 0.38f, 0.06f, f, f * 1.5f, loud * 0.12f, 2f);
            }
        }

        public void Passes(float volume, int count)
        {
            for (int p = 0; p < count; p++)
            {
                int s0 = (int)(R() * (Seconds - 3.5f) * Rate), n = (int)((2.5f + R()) * Rate);
                float y = 0f;
                for (int i = 0; i < n && s0 + i < _buf.Length; i++)
                {
                    float u = i / (float)n, env = Mathf.Sin(u * Mathf.PI);
                    y += (White() - y) * (0.02f + 0.05f * env);
                    _buf[s0 + i] += y * 6f * volume * env * env;
                }
            }
        }

        public void Chimes(float volume, int count)
        {
            float[] scale = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f };
            for (int c = 0; c < count; c++)
            {
                float start = R() * (Seconds - 2.5f), f = scale[_rng.Next(scale.Length)], loud = volume * (0.4f + 0.6f * R());
                int s0 = (int)(start * Rate), n = (int)(2.4f * Rate);
                for (int i = 0; i < n && s0 + i < _buf.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-2.2f * t);
                    _buf[s0 + i] += (Mathf.Sin(t * f * Mathf.PI * 2f) + 0.35f * Mathf.Sin(t * f * 2.76f * Mathf.PI * 2f)) * env * loud;
                }
            }
        }

        /// <summary>Folds the tail back onto the head so the loop repeats without a seam.</summary>
        public float[] Finish()
        {
            int length = (int)(Seconds * Rate), overlap = _buf.Length - length;
            var result = new float[length];
            float peak = 0f;
            for (int i = 0; i < length; i++)
            {
                float v = _buf[i];
                if (i < overlap)
                {
                    float f = i / (float)overlap;
                    v = v * f + _buf[length + i] * (1f - f);
                }
                result[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            if (peak > 0.6f)
            {
                float k = 0.6f / peak;
                for (int i = 0; i < length; i++) result[i] *= k;
            }
            return result;
        }
    }

    /// <summary>Background sound for the place. One loop at a time, cross-faded.</summary>
    public sealed class AmbienceSystem
    {
        readonly AudioSource[] _sources = new AudioSource[2];
        readonly float[] _targets = new float[2];
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        int _active;
        float _fadeSeconds = 2f;
        public float MasterVolume = 0.55f;

        public bool IsPlaying => _targets[_active] > 0.001f || _job != null;
        public string Current { get; private set; }

        public AmbienceSystem(World world)
        {
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Ambience" + i);
                go.transform.SetParent(world.transform, false);
                AudioSource src = go.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.volume = 0f;
                _sources[i] = src;
            }
        }

        // Loops are synthesised on a worker thread the first time they are wanted, so the
        // frame rate does not dip when a new place starts to sound.
        System.Threading.Tasks.Task<float[]> _job;
        AmbienceDef _jobDef;
        float _jobVolume;

        public void Apply(Statement s)
        {
            Library.EnsureLoaded();
            AmbienceDef def = Library.Resolve(Library.AmbienceList, s.Str("loop"));
            if (def == null) return;
            float volume = Mathf.Clamp01(s.Num("volume", 0.7f));
            _fadeSeconds = s.T(2.5f);
            if (def.Id == Current && (IsPlaying || _job != null))
            {
                if (_job != null) _jobVolume = volume; else _targets[_active] = volume;
                return;
            }
            Current = def.Id;
            _job = null;
            if (!_clips.TryGetValue(def.Id, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>("Ambience/" + def.Id);
                if (clip != null) _clips[def.Id] = clip;
            }
            if (clip != null) { Start(clip, volume); return; }

            int seed = def.Id.GetHashCode() & 0xFFFF;
            _jobDef = def;
            _jobVolume = volume;
            _job = System.Threading.Tasks.Task.Run(() =>
            {
                var synth = new LoopSynth(seed);
                def.Recipe(synth);
                return synth.Finish();
            });
        }

        void Start(AudioClip clip, float volume)
        {
            _targets[_active] = 0f;
            _active = 1 - _active;
            AudioSource src = _sources[_active];
            src.clip = clip;
            src.volume = 0f;
            src.Play();
            _targets[_active] = volume;
        }

        public void Stop(float seconds)
        {
            _fadeSeconds = Mathf.Max(0.2f, seconds);
            _targets[0] = _targets[1] = 0f;
            _job = null;
            Current = null;
        }

        public void Tick(float dt)
        {
            if (_job != null && _job.IsCompleted)
            {
                System.Threading.Tasks.Task<float[]> job = _job;
                _job = null;
                if (!job.IsFaulted && job.Result != null && _jobDef != null && _jobDef.Id == Current)
                {
                    AudioClip clip = AudioClip.Create(_jobDef.Id, job.Result.Length, 1, LoopSynth.Rate, false);
                    clip.SetData(job.Result, 0);
                    _clips[_jobDef.Id] = clip;
                    Start(clip, _jobVolume);
                }
                else if (job.IsFaulted) Debug.LogError("ImagineVR: ambience synthesis failed: " + job.Exception);
            }
            for (int i = 0; i < 2; i++)
            {
                AudioSource src = _sources[i];
                float target = _targets[i] * MasterVolume;
                src.volume = Mathf.MoveTowards(src.volume, target, dt / _fadeSeconds);
                if (src.volume <= 0.0005f && target <= 0f && src.isPlaying) src.Stop();
            }
        }
    }
}
