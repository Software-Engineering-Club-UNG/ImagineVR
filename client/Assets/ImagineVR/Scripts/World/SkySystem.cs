using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>Everything that defines the look of the air: sky, sun, light, ambient and fog.</summary>
    public struct SkyState
    {
        public Color Top, Horizon, Bottom, SunColor, CloudColor, LightColor, AmbSky, AmbGround, FogColor;
        public float SunElevation, SunAzimuth, SunSize, SunGlow, Stars, Clouds, Moon, Aurora, Nebula;
        public float LightIntensity, FogDensity, Haze, Night, Wind;

        public static SkyState Lerp(SkyState a, SkyState b, float t)
        {
            SkyState s;
            s.Top = Color.Lerp(a.Top, b.Top, t);
            s.Horizon = Color.Lerp(a.Horizon, b.Horizon, t);
            s.Bottom = Color.Lerp(a.Bottom, b.Bottom, t);
            s.SunColor = Color.Lerp(a.SunColor, b.SunColor, t);
            s.CloudColor = Color.Lerp(a.CloudColor, b.CloudColor, t);
            s.LightColor = Color.Lerp(a.LightColor, b.LightColor, t);
            s.AmbSky = Color.Lerp(a.AmbSky, b.AmbSky, t);
            s.AmbGround = Color.Lerp(a.AmbGround, b.AmbGround, t);
            s.FogColor = Color.Lerp(a.FogColor, b.FogColor, t);
            s.SunElevation = Mathf.Lerp(a.SunElevation, b.SunElevation, t);
            s.SunAzimuth = Mathf.LerpAngle(a.SunAzimuth, b.SunAzimuth, t);
            s.SunSize = Mathf.Lerp(a.SunSize, b.SunSize, t);
            s.SunGlow = Mathf.Lerp(a.SunGlow, b.SunGlow, t);
            s.Stars = Mathf.Lerp(a.Stars, b.Stars, t);
            s.Clouds = Mathf.Lerp(a.Clouds, b.Clouds, t);
            s.Moon = Mathf.Lerp(a.Moon, b.Moon, t);
            s.Aurora = Mathf.Lerp(a.Aurora, b.Aurora, t);
            s.Nebula = Mathf.Lerp(a.Nebula, b.Nebula, t);
            s.LightIntensity = Mathf.Lerp(a.LightIntensity, b.LightIntensity, t);
            s.FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t);
            s.Haze = Mathf.Lerp(a.Haze, b.Haze, t);
            s.Night = Mathf.Lerp(a.Night, b.Night, t);
            s.Wind = Mathf.Lerp(a.Wind, b.Wind, t);
            return s;
        }

        public Vector3 SunDirection => Direction(SunElevation, SunAzimuth);
        public Vector3 MoonDirection => Direction(38f, SunAzimuth + 160f);

        /// <summary>Azimuth 0 is straight ahead (+Z), 90 is to the right (+X).</summary>
        public static Vector3 Direction(float elevation, float azimuth)
        {
            float el = elevation * Mathf.Deg2Rad, az = azimuth * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        }

        /// <summary>Where the light comes from: the sun while it is up, otherwise the moon or the zenith.</summary>
        public Vector3 LightDirection
        {
            get
            {
                float sunUp = Mathf.InverseLerp(-6f, 2f, SunElevation);
                Vector3 night = Moon > 0.05f ? MoonDirection : new Vector3(0.25f, 0.9f, 0.35f).normalized;
                Vector3 sun = Direction(Mathf.Max(SunElevation, 6f), SunAzimuth);
                return Vector3.Slerp(night, sun, sunUp).normalized;
            }
        }
    }

    public sealed class SkyPreset : Entry
    {
        public SkyState State;
    }

    public static partial class Library
    {
        public static readonly List<SkyPreset> SkyPresets = new List<SkyPreset>();

        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static Color ScaleTo(Color c, float luminance)
        {
            float k = luminance / Mathf.Max(Lum(c), 0.04f);
            return new Color(Mathf.Min(c.r * k, 1.3f), Mathf.Min(c.g * k, 1.3f), Mathf.Min(c.b * k, 1.3f), 1f);
        }

        static SkyPreset Sky(string id, string desc, string tags, string aliases,
            string top, string horizon, string bottom,
            float sunEl, float sunAz, string sun, float light, float fog,
            float stars = 0f, float clouds = 0f, float night = 0f, float moon = 0f, float aurora = 0f, float nebula = 0f,
            float amb = 0.45f, float sunSize = 0.2f, float sunGlow = 1f, string cloud = null, string lightColor = null,
            string fogColor = null, float haze = 0.3f, float wind = 0.5f)
        {
            Color cTop = Pal.Hex(top), cHor = Pal.Hex(horizon), cBot = Pal.Hex(bottom), cSun = Pal.Hex(sun);
            var s = new SkyState
            {
                Top = cTop, Horizon = cHor, Bottom = cBot,
                SunColor = cSun, SunElevation = sunEl, SunAzimuth = sunAz, SunSize = sunSize, SunGlow = sunGlow,
                Stars = stars, Clouds = clouds, Moon = moon, Aurora = aurora, Nebula = nebula,
                CloudColor = cloud != null ? Pal.Hex(cloud) : Color.Lerp(Color.white, cHor, 0.25f),
                LightColor = lightColor != null ? Pal.Hex(lightColor) : cSun,
                LightIntensity = light,
                AmbSky = amb > 0f ? ScaleTo(Color.Lerp(cTop, cHor, 0.6f), amb) : Color.black,
                AmbGround = amb > 0f ? ScaleTo(Color.Lerp(cBot, cHor, 0.35f), amb * 0.6f) : Color.black,
                FogColor = fogColor != null ? Pal.Hex(fogColor) : cHor,
                FogDensity = fog, Haze = haze, Night = night, Wind = wind,
            };
            var preset = new SkyPreset { Id = id, Desc = desc, Tags = Split(tags), Aliases = Split(aliases), State = s };
            SkyPresets.Add(preset);
            return preset;
        }

        static void RegisterSkyPresets()
        {
            Sky("void", "Pure darkness, the empty room before anything is built", "dark,black,empty,nothing", "darkness,blank,dark room",
                "#000000", "#000000", "#000000", 30, 0, "#000000", 0f, 0f, amb: 0f, night: 1f, sunGlow: 0f, haze: 0f, wind: 0f);
            Sky("clear_noon", "Bright blue midday, high sun, a few clouds", "day,sunny,blue,bright,afternoon", "noon,midday,daytime,sunny day,clear sky",
                "#2F6FD0", "#BFE0FF", "#8FB5D8", 62, 40, "#FFF6E0", 0.78f, 0.08f, clouds: 0.25f, amb: 0.48f);
            Sky("morning", "Fresh morning light, soft warm horizon", "day,early,soft,fresh", "early morning,am",
                "#5AA0E8", "#FFE6C8", "#C8B8A0", 22, 80, "#FFE2B0", 0.7f, 0.15f, clouds: 0.3f, amb: 0.46f);
            Sky("golden_hour", "Low warm sun, orange horizon, long golden light", "sunset,evening,warm,orange,gold", "sunset,sundown,evening,magic hour",
                "#3A5FA8", "#FFB070", "#A06848", 7, 25, "#FFB060", 0.72f, 0.18f, clouds: 0.35f, night: 0.15f, amb: 0.4f, cloud: "#FFC9A0", sunSize: 0.3f);
            Sky("sunset_pink", "Pink and violet sunset, sun on the horizon", "sunset,pink,romantic,evening,purple", "pink sky,rose sunset",
                "#4A4A9A", "#FF8FA8", "#8A5070", 3, 10, "#FF8866", 0.6f, 0.2f, stars: 0.05f, clouds: 0.45f, night: 0.3f, amb: 0.4f, cloud: "#FFB0C0", sunSize: 0.35f);
            Sky("dusk", "Just after sunset, deep blue with the first stars", "twilight,evening,blue hour", "twilight,blue hour,nightfall",
                "#1B2350", "#6A5A8A", "#2A2840", -6, 10, "#FF9070", 0.26f, 0.2f, stars: 0.5f, night: 0.7f, amb: 0.3f, lightColor: "#8090C0");
            Sky("night_stars", "Clear night full of stars", "night,dark,stars,starry", "night,starry night,nighttime,midnight",
                "#050A1E", "#131C3A", "#070A14", -40, 0, "#000000", 0.24f, 0.1f, stars: 1f, night: 1f, amb: 0.22f, lightColor: "#9FB4FF", sunGlow: 0f);
            Sky("moonlit", "Night under a bright full moon", "night,moon,blue,calm", "moonlight,full moon,moon",
                "#0A1230", "#1E2C54", "#0A0E1C", -40, 20, "#000000", 0.34f, 0.12f, stars: 0.7f, clouds: 0.15f, night: 1f, moon: 1f, amb: 0.26f,
                lightColor: "#A8C0FF", cloud: "#2A3658", sunGlow: 0f);
            Sky("night_city", "City night, glow on the horizon, few stars", "night,urban,neon,downtown", "city night,urban night",
                "#0B0F24", "#3A2A48", "#101018", -40, 0, "#000000", 0.22f, 0.22f, stars: 0.25f, clouds: 0.2f, night: 1f, amb: 0.28f,
                lightColor: "#8FA0E0", cloud: "#2A2438", sunGlow: 0f);
            Sky("overcast", "Flat grey cloud cover, soft even light", "grey,gray,cloudy,dull,rainy", "cloudy,grey sky,gray sky",
                "#8A96A4", "#C4CCD4", "#8A9098", 50, 0, "#FFFFFF", 0.45f, 0.3f, clouds: 0.9f, amb: 0.55f, lightColor: "#E8EEF5", cloud: "#B8C0C8", sunSize: 0f, sunGlow: 0f);
            Sky("stormy", "Dark storm clouds, heavy and windy", "storm,thunder,dark,rain,dramatic", "storm,thunderstorm,tempest",
                "#2E3540", "#5A6470", "#2A2E34", 40, 0, "#FFFFFF", 0.3f, 0.45f, clouds: 1f, night: 0.35f, amb: 0.36f,
                lightColor: "#C8D4E0", cloud: "#3E4650", sunSize: 0f, sunGlow: 0f, wind: 1.6f);
            Sky("dawn", "First light, peach horizon under a deep blue sky", "sunrise,early,pink,quiet", "sunrise,daybreak,first light",
                "#2A3A78", "#FFB8A0", "#70607A", 2, 90, "#FFC0A0", 0.5f, 0.25f, stars: 0.15f, clouds: 0.3f, night: 0.35f, amb: 0.36f, cloud: "#FFC8C0", sunSize: 0.3f);
            Sky("foggy_morning", "Thick pale mist, everything soft and close", "fog,mist,grey,quiet,mysterious", "misty,fog,mist,haze",
                "#A8B4BC", "#D8DEE0", "#A0A8AC", 15, 60, "#FFFFFF", 0.5f, 0.7f, clouds: 0.5f, amb: 0.55f, sunGlow: 0.6f, sunSize: 0f, haze: 0.9f);
            Sky("alien_dusk", "Alien sky: violet above, teal horizon, magenta sun", "alien,planet,sci-fi,strange,purple,teal", "alien planet,another world,exoplanet",
                "#1A0A3A", "#20C0B0", "#102030", 12, 330, "#FF60D0", 0.6f, 0.2f, stars: 0.6f, nebula: 0.5f, night: 0.5f, moon: 0.8f, amb: 0.36f, lightColor: "#D090FF", sunSize: 0.5f);
            Sky("deep_space", "Open space: black, dense stars, coloured nebula", "space,stars,galaxy,cosmos,black", "outer space,space,galaxy,cosmos",
                "#000000", "#02030A", "#000000", 35, 60, "#FFFFFF", 0.85f, 0f, stars: 1f, nebula: 0.8f, night: 0.6f, amb: 0.12f, sunSize: 0.08f, haze: 0f, wind: 0f);
            Sky("aurora_night", "Polar night with green and violet northern lights", "aurora,night,arctic,green,magic", "northern lights,aurora,aurora borealis",
                "#04101E", "#0E2A3A", "#050A12", -40, 0, "#000000", 0.24f, 0.08f, stars: 0.9f, night: 1f, aurora: 1f, amb: 0.24f, lightColor: "#A0FFD8", sunGlow: 0f);
            Sky("underwater", "Beneath the sea: blue-green light from above, murky distance", "ocean,sea,deep,blue,water", "under the sea,undersea,deep sea",
                "#0A6AA0", "#0E5080", "#052038", 80, 0, "#BFEFFF", 0.5f, 0.6f, night: 0.3f, amb: 0.5f, sunSize: 0f, sunGlow: 2f, haze: 0.8f, wind: 0.8f);
            Sky("synthwave", "Retro neon: magenta horizon, huge low sun, stars", "retro,neon,80s,vaporwave,outrun,pink", "vaporwave,outrun,retrowave,80s",
                "#14052E", "#FF4FA0", "#0A0218", 4, 0, "#FFD060", 0.5f, 0.15f, stars: 0.6f, night: 0.9f, amb: 0.32f, lightColor: "#FF70C0", sunSize: 0.9f);
            Sky("desert_noon", "Bleached desert midday, pale horizon, harsh sun", "desert,hot,dry,bright", "hot day,scorching",
                "#3E8AE0", "#F4E2C0", "#D8B888", 75, 30, "#FFFBEA", 0.9f, 0.12f, clouds: 0.05f, amb: 0.5f);
            Sky("winter_day", "Pale cold daylight, low sun, thin cloud", "winter,cold,snow,pale", "snowy day,cold day",
                "#7FAEDC", "#E8F0F8", "#C8D4E0", 20, 60, "#FFF4E8", 0.6f, 0.25f, clouds: 0.4f, amb: 0.6f);
            Sky("volcanic", "Ash-black sky lit red from below", "volcano,lava,hell,fire,red,dark", "hellscape,inferno,lava sky",
                "#1A0806", "#C03A10", "#200804", 5, 0, "#FF5020", 0.45f, 0.35f, clouds: 0.7f, night: 0.7f, amb: 0.32f,
                lightColor: "#FF7030", cloud: "#3A1410", fogColor: "#5A1A0A", sunSize: 0f, sunGlow: 0.6f);
            Sky("candy", "Pastel pink and baby blue, fluffy white clouds", "pastel,cute,sweet,pink,dream", "pastel,cotton candy,kawaii",
                "#8FD0FF", "#FFD6F0", "#FFF0C0", 45, 30, "#FFFFFF", 0.7f, 0.12f, clouds: 0.5f, amb: 0.6f, cloud: "#FFFFFF");
            Sky("toxic", "Sickly green haze, heavy and strange", "green,poison,swamp,radioactive,wasteland", "radioactive,poison,acid",
                "#1A2A0A", "#9AC020", "#20300A", 20, 0, "#E0FF80", 0.5f, 0.4f, clouds: 0.6f, night: 0.4f, amb: 0.36f, lightColor: "#D0FF60", cloud: "#5A7010", sunSize: 0f, sunGlow: 0.5f);
            Sky("spooky", "Haunted night: moon behind drifting cloud, purple mist", "halloween,haunted,creepy,scary,night,ghost", "haunted,creepy,eerie,halloween night",
                "#0A0A18", "#3A2A4A", "#08080C", -40, 340, "#000000", 0.26f, 0.5f, stars: 0.3f, clouds: 0.5f, night: 1f, moon: 1f, amb: 0.24f,
                lightColor: "#B0A0D0", cloud: "#1E1830", fogColor: "#2A2038", sunGlow: 0f, haze: 0.7f);
            Sky("dreamy", "Soft heavenly light above a sea of cloud", "heaven,dream,soft,clouds,peaceful,white", "heaven,dreamscape,cloud kingdom",
                "#BFD8FF", "#FFF2D8", "#FFE0E8", 30, 20, "#FFFFFF", 0.7f, 0.3f, clouds: 0.8f, amb: 0.65f, cloud: "#FFFFFF", haze: 0.6f);
            Sky("white_room", "A bright, clean white studio with no horizon", "white,studio,gallery,clean,minimal,blank", "white void,studio,gallery light,blank white",
                "#F2F2F0", "#FFFFFF", "#E4E4E0", 60, 30, "#FFFFFF", 0.55f, 0.15f, amb: 0.78f, sunSize: 0f, sunGlow: 0f, haze: 0.9f, wind: 0f);
        }
    }

    /// <summary>Owns the sky material and the world's light and fog. Every change is a blend over time.</summary>
    public sealed class SkySystem
    {
        static readonly int TopId = Shader.PropertyToID("_Top"), HorizonId = Shader.PropertyToID("_Horizon"), BottomId = Shader.PropertyToID("_Bottom");
        static readonly int SunColorId = Shader.PropertyToID("_SunColor"), SunDirId = Shader.PropertyToID("_SunDir"), SunSizeId = Shader.PropertyToID("_SunSize");
        static readonly int MoonDirId = Shader.PropertyToID("_MoonDir"), MoonId = Shader.PropertyToID("_Moon"), StarsId = Shader.PropertyToID("_Stars");
        static readonly int CloudsId = Shader.PropertyToID("_Clouds"), CloudColorId = Shader.PropertyToID("_CloudColor");
        static readonly int AuroraId = Shader.PropertyToID("_Aurora"), NebulaId = Shader.PropertyToID("_Nebula"), HazeId = Shader.PropertyToID("_Haze");
        static readonly int GSunDir = Shader.PropertyToID("_ImSunDir"), GSunColor = Shader.PropertyToID("_ImSunColor");
        static readonly int GAmbSky = Shader.PropertyToID("_ImAmbSky"), GAmbGround = Shader.PropertyToID("_ImAmbGround");
        static readonly int GFogColor = Shader.PropertyToID("_ImFogColor"), GFogParams = Shader.PropertyToID("_ImFogParams");
        static readonly int GWind = Shader.PropertyToID("_ImWind"), GNight = Shader.PropertyToID("_ImNight");

        readonly Material _material;
        SkyState _from, _to, _now;
        float _elapsed, _duration = 1f;
        float _worldRadius = 120f;

        public SkyState Current => _now;
        public SkyState Target => _to;
        public bool IsDark { get; private set; } = true;

        public SkySystem()
        {
            Library.EnsureLoaded();
            _material = new Material(Shader.Find("ImagineVR/Sky")) { name = "ImagineSky" };
            _from = _to = _now = Library.SkyPresets[0].State;
            RenderSettings.skybox = _material;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Push();
        }

        public void SetWorldRadius(float radius) { _worldRadius = radius; }

        void BlendTo(SkyState target, float seconds)
        {
            _from = _now;
            _to = target;
            _elapsed = 0f;
            _duration = Mathf.Max(0.05f, seconds);
        }

        public void Reset(float seconds)
        {
            IsDark = true;
            BlendTo(Library.SkyPresets[0].State, seconds);
        }

        public void ApplySky(Statement s)
        {
            SkyState target = _to;
            if (s.Has("preset"))
            {
                SkyPreset preset = Library.Resolve(Library.SkyPresets, s.Str("preset"));
                if (preset != null)
                {
                    target = preset.State;
                    IsDark = preset.Id == "void";
                }
            }
            else IsDark = false;

            bool recolour = false;
            if (s.TryColor("top", out Color c)) { target.Top = c; recolour = true; }
            if (s.TryColor("horizon", out c)) { target.Horizon = c; target.FogColor = c; recolour = true; }
            if (s.TryColor("bottom", out c)) { target.Bottom = c; recolour = true; }
            if (recolour && target.LightIntensity + Library.Lum2(target.AmbSky) > 0.01f)
            {
                float amb = Mathf.Max(0.2f, Library.Lum2(target.AmbSky));
                target.AmbSky = Library.Scale2(Color.Lerp(target.Top, target.Horizon, 0.6f), amb);
                target.AmbGround = Library.Scale2(Color.Lerp(target.Bottom, target.Horizon, 0.35f), amb * 0.6f);
            }
            if (s.Has("sun_elevation"))
            {
                target.SunElevation = Mathf.Clamp(s.Num("sun_elevation"), -90f, 90f);
                if (!s.Has("preset")) target.Night = Mathf.Clamp01(Mathf.InverseLerp(8f, -8f, target.SunElevation));
            }
            if (s.Has("sun_azimuth")) target.SunAzimuth = s.Num("sun_azimuth");
            if (s.TryColor("sun_color", out c)) { target.SunColor = c; target.LightColor = c; if (target.SunGlow < 0.3f) target.SunGlow = 1f; }
            if (s.Has("sun_size")) target.SunSize = Mathf.Clamp01(s.Num("sun_size"));
            if (s.Has("stars")) target.Stars = Mathf.Clamp01(s.Num("stars"));
            if (s.Has("clouds")) target.Clouds = Mathf.Clamp01(s.Num("clouds"));
            if (s.TryColor("cloud_color", out c)) target.CloudColor = c;
            if (s.Has("moon")) target.Moon = Mathf.Clamp01(s.Num("moon"));
            if (s.Has("aurora")) target.Aurora = Mathf.Clamp01(s.Num("aurora"));
            if (s.Has("nebula")) target.Nebula = Mathf.Clamp01(s.Num("nebula"));
            BlendTo(target, s.T(2.5f));
        }

        public void ApplyLight(Statement s)
        {
            SkyState target = _to;
            if (s.Has("intensity")) target.LightIntensity = Mathf.Clamp(s.Num("intensity"), 0f, 2f);
            if (s.TryColor("color", out Color c)) target.LightColor = c;
            if (s.TryColor("ambient", out c)) { target.AmbSky = c; target.AmbGround = c * 0.6f; }
            BlendTo(target, s.T(1.5f));
        }

        public void ApplyFog(Statement s)
        {
            SkyState target = _to;
            if (s.Has("density")) target.FogDensity = Mathf.Clamp01(s.Num("density"));
            if (s.TryColor("color", out Color c)) target.FogColor = c;
            BlendTo(target, s.T(1.5f));
        }

        public void Tick(float dt)
        {
            if (_elapsed < _duration)
            {
                _elapsed += dt;
                _now = SkyState.Lerp(_from, _to, Ease.InOutCubic(_elapsed / _duration));
            }
            Push();
        }

        void Push()
        {
            SkyState s = _now;
            _material.SetColor(TopId, s.Top);
            _material.SetColor(HorizonId, s.Horizon);
            _material.SetColor(BottomId, s.Bottom);
            Color sun = s.SunColor; sun.a = s.SunGlow;
            _material.SetColor(SunColorId, sun);
            _material.SetVector(SunDirId, s.SunDirection);
            _material.SetVector(MoonDirId, s.MoonDirection);
            _material.SetFloat(SunSizeId, s.SunSize);
            _material.SetFloat(MoonId, s.Moon);
            _material.SetFloat(StarsId, s.Stars);
            _material.SetFloat(CloudsId, s.Clouds);
            _material.SetColor(CloudColorId, s.CloudColor);
            _material.SetFloat(AuroraId, s.Aurora);
            _material.SetFloat(NebulaId, s.Nebula);
            _material.SetFloat(HazeId, Mathf.Clamp01(s.Haze + s.FogDensity * 0.6f));

            Shader.SetGlobalVector(GSunDir, s.LightDirection);
            Shader.SetGlobalColor(GSunColor, s.LightColor * s.LightIntensity);
            Shader.SetGlobalColor(GAmbSky, s.AmbSky);
            Shader.SetGlobalColor(GAmbGround, s.AmbGround);
            Shader.SetGlobalColor(GFogColor, s.FogColor);
            float d = s.FogDensity;
            float k = 0.0035f * Mathf.Clamp01(d * 20f) + 0.12f * Mathf.Pow(d, 2.2f);
            Shader.SetGlobalVector(GFogParams, new Vector4(k, _worldRadius * 0.78f, _worldRadius * 0.98f, 0f));
            Shader.SetGlobalVector(GWind, new Vector4(0.8f, 0.6f, 0.1f * s.Wind, 0f));
            Shader.SetGlobalFloat(GNight, s.Night);
            RenderSettings.ambientLight = s.AmbSky;
        }
    }

    public static partial class Library
    {
        // exposed for SkySystem's colour overrides
        public static float Lum2(Color c) => Lum(c);
        public static Color Scale2(Color c, float luminance) => ScaleTo(c, luminance);
    }
}
