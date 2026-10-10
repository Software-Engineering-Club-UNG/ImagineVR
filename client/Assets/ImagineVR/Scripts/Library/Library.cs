using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>Named colours used by the asset recipes. All values are sRGB.</summary>
    public static class Pal
    {
        static readonly Dictionary<string, Color> Cache = new Dictionary<string, Color>();

        public static Color Hex(string hex)
        {
            if (Cache.TryGetValue(hex, out Color c)) return c;
            Statement.TryParseColor(hex, out c);
            Cache[hex] = c;
            return c;
        }

        public static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);
        public static Color Dark(Color c, float amount = 0.25f) => Color.Lerp(c, Color.black, amount);
        public static Color Light(Color c, float amount = 0.25f) => Color.Lerp(c, Color.white, amount);

        // nature
        public static readonly Color Leaf = Hex("#4E9A4A"), LeafDark = Hex("#2F6B3A"), LeafLight = Hex("#7CC25C");
        public static readonly Color Pine = Hex("#2E6B4F"), PineDark = Hex("#1F4A3A");
        public static readonly Color Autumn = Hex("#D9792B"), AutumnRed = Hex("#B8432F"), AutumnGold = Hex("#E0B040");
        public static readonly Color Blossom = Hex("#F6B7CF"), BlossomDeep = Hex("#E889AE");
        public static readonly Color Trunk = Hex("#6B4A32"), TrunkDark = Hex("#4A3324"), Birch = Hex("#E6E1D4");
        public static readonly Color Rock = Hex("#8C8C8C"), RockDark = Hex("#5E6066"), RockWarm = Hex("#A08C78");
        public static readonly Color Sand = Hex("#E3CC9A"), SandDark = Hex("#C4A870");
        public static readonly Color Snow = Hex("#F2F6FA"), Ice = Hex("#BFE3F2");
        public static readonly Color Water = Hex("#2E7FA8"), Grass = Hex("#5FA84E"), Dirt = Hex("#7A5B3E");
        public static readonly Color Cactus = Hex("#5C9A5A"), Coral = Hex("#FF7F6A"), Kelp = Hex("#3C8A5A");

        // built things
        public static readonly Color Concrete = Hex("#A7A9AC"), ConcreteDark = Hex("#6F7276"), Asphalt = Hex("#3A3C40");
        public static readonly Color Brick = Hex("#A8573F"), Plaster = Hex("#E8DFCF"), Roof = Hex("#8C3B32"), RoofDark = Hex("#4E5560");
        public static readonly Color Wood = Hex("#9A6B42"), WoodDark = Hex("#6E4A2C"), WoodLight = Hex("#C79A62");
        public static readonly Color Metal = Hex("#9AA3AB"), MetalDark = Hex("#4C535A"), Steel = Hex("#C8CED3"), Gold = Hex("#E6B84A");
        public static readonly Color Glass = Hex("#8FC7E6"), GlassDark = Hex("#2C4A63"), WindowLit = Hex("#FFD98A");
        public static readonly Color Rubber = Hex("#26282B"), White = Hex("#F2F2F0"), OffWhite = Hex("#E4E0D6"), Black = Hex("#1C1D20");

        // accents
        public static readonly Color Red = Hex("#D9463D"), Orange = Hex("#F08A2E"), Yellow = Hex("#F2CF3B"), Green = Hex("#3FAE5A");
        public static readonly Color Blue = Hex("#3B7DD8"), Purple = Hex("#8A5BD1"), Pink = Hex("#EE7FB0"), Teal = Hex("#2FB8A8");
        public static readonly Color Cyan = Hex("#46C7E8"), Magenta = Hex("#E0409A"), Lime = Hex("#B4E04A"), Cream = Hex("#F7EBD0");
        public static readonly Color Skin = Hex("#E8B58E"), Fire = Hex("#FF8A2A"), FireCore = Hex("#FFE27A"), Neon = Hex("#39F5E0");
    }

    /// <summary>Anything a statement can name: has an ID, a one-line description, tags and aliases.</summary>
    public class Entry
    {
        public string Id;
        public string Desc;
        public string[] Tags = Array.Empty<string>();
        public string[] Aliases = Array.Empty<string>();

        public Dictionary<string, object> ToCatalog()
        {
            var d = new Dictionary<string, object> { ["id"] = Id, ["desc"] = Desc };
            if (Tags.Length > 0) d["tags"] = new List<object>(Tags);
            if (Aliases.Length > 0) d["aliases"] = new List<object>(Aliases);
            return d;
        }
    }

    public enum ColliderShape { Box, Sphere, Capsule }

    /// <summary>One library object: how to build its mesh, and how it behaves when it is a solid thing.</summary>
    public sealed class AssetDef : Entry
    {
        public string Category;
        public float Size = 1f;                 // nominal height in metres, for the catalog
        public Action<MeshBuilder> Recipe;
        public bool Holdable;                   // offered as something to hold
        public bool Prop;                       // may be spawned with physics
        public bool Tool;                       // snaps into the hand along the controller's forward axis
        public bool Primitive;
        public float Mass = 1f;
        public float Bounce = 0.25f;
        public float Drag = 0.05f;
        public ColliderShape Shape = ColliderShape.Box;
        public bool OnWater;                    // floats: placed on the water surface where there is water
        public bool Airborne;                   // belongs in the sky: scattered above the ground by default
        public string Behavior = "";            // see Prop.cs
        public float Pitch = 1f;                // for things that make a sound when hit

        Mesh _mesh;
        public Mesh Mesh
        {
            get
            {
                if (_mesh == null)
                {
                    var mb = new MeshBuilder();
                    Recipe(mb);
                    _mesh = mb.Build(Id);
                }
                return _mesh;
            }
        }

        public AssetDef Hold() { Holdable = true; Prop = true; return this; }
        public AssetDef AsTool() { Holdable = true; Prop = true; Tool = true; return this; }
        public AssetDef Solid(float mass = 1f, float bounce = 0.25f, ColliderShape shape = ColliderShape.Box)
        {
            Prop = true; Mass = mass; Bounce = bounce; Shape = shape; return this;
        }
        public AssetDef Ball(float mass = 0.3f, float bounce = 0.7f)
        {
            Prop = true; Holdable = true; Mass = mass; Bounce = bounce; Shape = ColliderShape.Sphere;
            if (Behavior.Length == 0) Behavior = "ball";
            return this;
        }
        public AssetDef Does(string behavior, float pitch = 1f) { Behavior = behavior; Pitch = pitch; return this; }
        public AssetDef Floats() { OnWater = true; return this; }
        public AssetDef Flies() { Airborne = true; return this; }
    }

    /// <summary>The baked library: every asset, preset, terrain type and kit the build contains.</summary>
    public static partial class Library
    {
        public static readonly List<AssetDef> AssetList = new List<AssetDef>();
        public static readonly Dictionary<string, AssetDef> Assets = new Dictionary<string, AssetDef>();
        public static readonly string[] Motions = { "none", "bob", "spin", "hover", "sway", "orbit", "drift" };

        static bool _ready;

        // Each of these lives in its own file. A missing one is simply skipped.
        static partial void RegisterShapes();
        static partial void RegisterNature();
        static partial void RegisterCity();
        static partial void RegisterProps();
        static partial void RegisterHoldables();
        static partial void RegisterThemes();
        static partial void RegisterWeather();
        static partial void RegisterAmbience();
        static partial void RegisterKits();

        /// <summary>Registers everything. Safe to call more than once.</summary>
        public static void EnsureLoaded()
        {
            if (_ready) return;
            _ready = true;
            RegisterShapes();
            RegisterNature();
            RegisterCity();
            RegisterProps();
            RegisterHoldables();
            RegisterThemes();
            RegisterSkyPresets();
            RegisterGround();
            RegisterWeather();
            RegisterAmbience();
            RegisterKits();
        }

        static string[] Split(string list)
        {
            if (string.IsNullOrWhiteSpace(list)) return Array.Empty<string>();
            string[] parts = list.Split(',');
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim().ToLowerInvariant();
            return parts;
        }

        static AssetDef Add(string id, string category, float size, string desc, string tags, string aliases, Action<MeshBuilder> recipe)
        {
            var def = new AssetDef
            {
                Id = id, Category = category, Size = size, Desc = desc,
                Tags = Split(tags), Aliases = Split(aliases), Recipe = recipe,
            };
            if (Assets.ContainsKey(id)) Debug.LogWarning("ImagineVR: duplicate asset id " + id);
            Assets[id] = def;
            AssetList.Add(def);
            return def;
        }

        // ------------------------------------------------------------------ lookup

        public static AssetDef Asset(string id)
        {
            EnsureLoaded();
            return id != null && Assets.TryGetValue(id, out AssetDef def) ? def : null;
        }

        /// <summary>Exact match, else the nearest entry by alias and shared words, else null.</summary>
        public static T Resolve<T>(IReadOnlyList<T> entries, string name) where T : Entry
        {
            if (string.IsNullOrEmpty(name)) return null;
            string raw = name.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            foreach (T e in entries) if (e.Id == raw) return e;

            var query = new HashSet<string>();
            foreach (string w in raw.Split('_')) if (w.Length > 0) query.Add(Stem(w));
            T best = null;
            float bestScore = 0f;
            foreach (T e in entries)
            {
                float score = 0f;
                foreach (string alias in e.Aliases)
                {
                    if (alias.Replace(' ', '_') == raw) { score = 100f; break; }
                }
                if (score == 0f)
                {
                    int idWords = 0, idHits = 0;
                    foreach (string w in e.Id.Split('_'))
                    {
                        idWords++;
                        if (query.Contains(Stem(w))) { score += 3f; idHits++; }
                    }
                    foreach (string tag in e.Tags) foreach (string w in tag.Split(' ', '_')) if (query.Contains(Stem(w))) score += 1.5f;
                    foreach (string alias in e.Aliases) foreach (string w in alias.Split(' ', '_')) if (query.Contains(Stem(w))) score += 1.5f;
                    if (score > 0f) score -= 0.1f * (idWords - idHits);
                }
                if (score > bestScore) { best = e; bestScore = score; }
            }
            return best;
        }

        static string Stem(string word)
        {
            if (word.Length > 5 && word.EndsWith("ies")) return word.Substring(0, word.Length - 3) + "y";
            if (word.Length > 4 && word.EndsWith("es")) return word.Substring(0, word.Length - 2);
            if (word.Length > 3 && word.EndsWith("s")) return word.Substring(0, word.Length - 1);
            return word;
        }

        /// <summary>Finds an asset by name, substituting the nearest one. Null means "use a stand-in shape".</summary>
        public static AssetDef FindAsset(string name)
        {
            EnsureLoaded();
            return Resolve(AssetList, name);
        }
    }
}
