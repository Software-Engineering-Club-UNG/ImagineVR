using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public sealed class WeatherDef : Entry
    {
        public float Rate, Life, Speed, SizeMin, SizeMax, Gravity, Noise, Stretch, BoxHeight = 1f, BoxY = 12f, Spread = 30f;
        public Color ColorA, ColorB;
        public bool Additive, Up, Drift;
        public int Max = 600;
    }

    public static partial class Library
    {
        public static readonly List<WeatherDef> WeatherList = new List<WeatherDef>();

        static WeatherDef Weather(string id, string desc, string tags, string aliases, float rate, float life, float speed,
            float sizeMin, float sizeMax, string colorA, string colorB, float alpha)
        {
            Color a = Pal.Hex(colorA), b = Pal.Hex(colorB);
            a.a = b.a = alpha;
            var def = new WeatherDef
            {
                Id = id, Desc = desc, Tags = Split(tags), Aliases = Split(aliases),
                Rate = rate, Life = life, Speed = speed, SizeMin = sizeMin, SizeMax = sizeMax, ColorA = a, ColorB = b,
            };
            WeatherList.Add(def);
            return def;
        }

        static partial void RegisterWeather()
        {
            WeatherList.Add(new WeatherDef { Id = "none", Desc = "Clear air: stops any weather", Tags = Split("clear,stop,off"), Aliases = Split("clear,nothing,stop") });
            WeatherDef w;
            w = Weather("rain", "Falling rain", "wet,storm,drizzle", "raining,rainfall,downpour,drizzle", 480, 1.0f, 15f, 0.018f, 0.026f, "#B8CCE8", "#DCE8F8", 0.5f);
            w.Stretch = 5f;
            w = Weather("snow", "Gently falling snow", "winter,cold,flakes", "snowing,snowfall,flurry,blizzard", 170, 7f, 1.5f, 0.04f, 0.09f, "#FFFFFF", "#E8F2FF", 0.92f);
            w.Noise = 0.6f;
            w = Weather("fireflies", "Drifting points of warm light", "night,magic,glow,summer", "lightning bugs,glowing bugs", 12, 6f, 0.15f, 0.05f, 0.09f, "#E8FF7A", "#FFD95A", 1f);
            w.Additive = true; w.Noise = 0.5f; w.Drift = true; w.BoxY = 1.6f; w.BoxHeight = 3f; w.Spread = 26f; w.Max = 120;
            w = Weather("dust", "Dust or sand blowing past", "desert,sand,wind,dry", "sand,sandstorm,dusty,haze", 70, 6f, 1.8f, 0.03f, 0.08f, "#D8C09A", "#E8D8B8", 0.28f);
            w.Noise = 0.5f; w.Drift = true; w.BoxY = 2.5f; w.BoxHeight = 5f;
            w = Weather("embers", "Glowing sparks rising", "fire,volcano,sparks,hot", "sparks,cinders,ash sparks", 45, 4f, 1.3f, 0.025f, 0.05f, "#FF8A2A", "#FFD060", 1f);
            w.Additive = true; w.Up = true; w.Noise = 0.7f; w.BoxY = 0.2f; w.Spread = 24f; w.Max = 260;
            w = Weather("leaves", "Autumn leaves tumbling down", "autumn,fall,wind,forest", "falling leaves,autumn leaves", 22, 8f, 1.2f, 0.08f, 0.14f, "#D9792B", "#B8432F", 1f);
            w.Noise = 0.9f; w.Max = 240;
            w = Weather("petals", "Pink blossom petals drifting down", "spring,cherry,blossom,romantic", "cherry blossoms,sakura,blossom", 26, 8f, 0.9f, 0.05f, 0.09f, "#F6B7CF", "#FFE0EC", 1f);
            w.Noise = 0.9f; w.Max = 260;
            w = Weather("bubbles", "Bubbles floating upward", "underwater,soap,sea,playful", "soap bubbles,air bubbles", 30, 6f, 0.8f, 0.04f, 0.12f, "#BFEFFF", "#FFFFFF", 0.4f);
            w.Up = true; w.Noise = 0.4f; w.BoxY = 0.2f; w.Spread = 22f; w.Max = 240;
            w = Weather("sparkles", "Twinkling motes of light in the air", "magic,fairy,dream,glitter", "magic dust,glitter,fairy dust,twinkles", 34, 2.6f, 0.1f, 0.025f, 0.05f, "#FFFFFF", "#FFE9A0", 1f);
            w.Additive = true; w.Drift = true; w.Noise = 0.3f; w.BoxY = 1.8f; w.BoxHeight = 4f; w.Spread = 22f; w.Max = 140;
            w = Weather("ash", "Grey ash sifting down", "volcano,fire,apocalypse,grey", "ashfall,soot", 90, 7f, 0.9f, 0.025f, 0.06f, "#8A8A8A", "#5A5A5A", 0.8f);
            w.Noise = 0.5f;
        }
    }

    /// <summary>One-shot particle bursts.</summary>
    public static class Fx
    {
        static Material _soft, _add;

        public static Material Material(bool additive)
        {
            if (_soft == null)
            {
                Shader shader = Shader.Find("ImagineVR/Particle");
                _soft = new Material(shader) { name = "ParticleSoft" };
                _soft.SetFloat("_Additive", 0f);
                _add = new Material(shader) { name = "ParticleAdd" };
                _add.SetFloat("_Additive", 1f);
            }
            return additive ? _add : _soft;
        }

        static ParticleSystem Make(string name, Vector3 position, bool additive, int max)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Material(additive);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void FadeOut(ParticleSystem ps)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>
        /// A particle system that emits only when told to (with Emit), for things that leave a trail:
        /// dust behind wheels, spray behind a boat. Each puff swells and fades.
        /// </summary>
        public static ParticleSystem Stream(string name, Transform parent, int max)
        {
            ParticleSystem ps = Make(name, Vector3.zero, false, max);
            ps.transform.SetParent(parent, false);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.gravityModifier = -0.02f;
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ps.Play();
            return ps;
        }

        public static ParticleSystem Burst(Vector3 position, Color a, Color b, int count, float speed, float size, float life, float gravity, bool additive)
        {
            ParticleSystem ps = Make("Burst", position, additive, count);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.04f;
            FadeOut(ps);
            ps.Play();
            Object.Destroy(ps.gameObject, life + 0.5f);
            return ps;
        }

        public static ParticleSystem Confetti(Vector3 position, int count = 80, float life = 2.6f, float size = .06f, Color? tint = null)
        {
            ParticleSystem ps = Make("Confetti", position, false, count);
            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * .6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(Pal.Red, 0f), new GradientColorKey(Pal.Yellow, 0.25f), new GradientColorKey(Pal.Green, 0.5f),
                new GradientColorKey(Pal.Cyan, 0.75f), new GradientColorKey(Pal.Pink, 1f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            if (tint.HasValue) main.startColor = tint.Value;
            main.gravityModifier = 0.7f;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 35f;
            sh.radius = 0.05f;
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            FadeOut(ps);
            ps.Play();
            Object.Destroy(ps.gameObject, life + .6f);
            return ps;
        }
    }

    /// <summary>Particles in the air around the user: rain, snow, fireflies and so on. One kind at a time.</summary>
    public sealed class WeatherSystem
    {
        sealed class Live
        {
            public WeatherDef Def;
            public ParticleSystem System;
            public float Rate, Target;
        }

        readonly World _world;
        readonly Transform _root;
        readonly List<Live> _live = new List<Live>();

        public bool IsActive
        {
            get
            {
                foreach (Live l in _live) if (l.Target > 0f) return true;
                return false;
            }
        }

        public WeatherSystem(World world)
        {
            _world = world;
            _root = new GameObject("Weather").transform;
            _root.SetParent(world.transform, false);
        }

        public void Apply(Statement s)
        {
            Library.EnsureLoaded();
            WeatherDef def = Library.Resolve(Library.WeatherList, s.Str("type", "none"));
            if (def == null || def.Id == "none") { Stop(); return; }
            float intensity = Mathf.Clamp(s.Num("intensity", 0.5f), 0.02f, 1f);
            foreach (Live l in _live) if (l.Def != def) l.Target = 0f;
            Live live = _live.Find(l => l.Def == def);
            if (live == null)
            {
                live = new Live { Def = def, System = Create(def) };
                _live.Add(live);
            }
            live.Target = def.Rate * Mathf.Lerp(0.15f, 1.6f, intensity);
            if (s.TryColor("color", out Color c))
            {
                ParticleSystem.MainModule main = live.System.main;
                Color a = c; a.a = def.ColorA.a;
                main.startColor = new ParticleSystem.MinMaxGradient(a, Color.Lerp(a, Color.white, 0.25f));
            }
        }

        public void Stop()
        {
            foreach (Live l in _live) l.Target = 0f;
        }

        ParticleSystem Create(WeatherDef def)
        {
            var go = new GameObject("Weather_" + def.Id);
            go.transform.SetParent(_root, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = def.Max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(def.Life * 0.7f, def.Life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(def.Speed * 0.75f, def.Speed);
            main.startSize = new ParticleSystem.MinMaxCurve(def.SizeMin, def.SizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(def.ColorA, def.ColorB);
            main.gravityModifier = def.Gravity;

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            if (def.Drift)
            {
                // born anywhere in a volume around the user, moving in random directions
                sh.scale = new Vector3(def.Spread, def.BoxHeight, def.Spread);
                sh.randomDirectionAmount = 1f;
                go.transform.localPosition = new Vector3(0f, def.BoxY, 0f);
            }
            else if (def.Up)
            {
                sh.scale = new Vector3(def.Spread, def.Spread, 0.5f);
                go.transform.localPosition = new Vector3(0f, def.BoxY, 0f);
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // emit upward
            }
            else
            {
                sh.scale = new Vector3(def.Spread, def.Spread, 0.5f);
                go.transform.localPosition = new Vector3(0f, def.BoxY, 0f);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);    // emit downward
            }

            if (def.Noise > 0f)
            {
                ParticleSystem.NoiseModule noise = ps.noise;
                noise.enabled = true;
                noise.strength = def.Noise;
                noise.frequency = 0.35f;
                noise.scrollSpeed = 0.4f;
            }

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Fx.Material(def.Additive);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (def.Stretch > 0f)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = def.Stretch;
                r.velocityScale = 0.02f;
            }
            ps.Play();
            return ps;
        }

        public void Tick(float dt)
        {
            if (_world.Head != null)
            {
                Vector3 p = _world.Head.position;
                _root.position = new Vector3(p.x, 0f, p.z);
            }
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live l = _live[i];
                l.Rate = Mathf.MoveTowards(l.Rate, l.Target, Mathf.Max(l.Def.Rate, 10f) * dt);
                ParticleSystem.EmissionModule em = l.System.emission;
                em.rateOverTime = l.Rate;
                if (l.Target <= 0f && l.Rate <= 0f && l.System.particleCount == 0)
                {
                    Object.Destroy(l.System.gameObject);
                    _live.RemoveAt(i);
                }
            }
        }
    }
}
