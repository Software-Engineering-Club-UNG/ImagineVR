using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>A themed generator: one statement, a whole place.</summary>
    public sealed class KitDef : Entry
    {
        public Action<KitContext> Build;
        public string Sky, Ambience, Weather, Greeting;
        public string TerrainType, TerrainMaterial, GridColor;
        public float TerrainHeight = -1f, TerrainGrid;
        public float Gravity = float.NaN;
        public readonly Dictionary<string, string> Params = new Dictionary<string, string>();

        public KitDef Defaults(string sky = null, string terrain = null, string material = null, float height = -1f,
            string ambience = null, string weather = null, float gravity = float.NaN, float grid = 0f, string gridColor = null)
        {
            Sky = sky; TerrainType = terrain; TerrainMaterial = material; TerrainHeight = height;
            Ambience = ambience; Weather = weather; Gravity = gravity; TerrainGrid = grid; GridColor = gridColor;
            return this;
        }

        public KitDef Param(string name, string description) { Params[name] = description; return this; }
        public KitDef Greet(string text) { Greeting = text; return this; }

        public Statement TerrainStatement(int seed)
        {
            if (TerrainType == null) return null;
            Statement s = Statement.Make("terrain", "type", TerrainType, "seed", (double)seed, "t", 3.0);
            if (TerrainMaterial != null) s.Fields["material"] = TerrainMaterial;
            if (TerrainHeight >= 0f) s.Fields["height"] = (double)TerrainHeight;
            if (TerrainGrid > 0f) s.Fields["grid"] = (double)TerrainGrid;
            if (GridColor != null) s.Fields["grid_color"] = GridColor;
            return s;
        }
    }

    public static partial class Library
    {
        public static readonly List<KitDef> Kits = new List<KitDef>();

        static KitDef Kit(string id, string desc, string tags, string aliases, Action<KitContext> build)
        {
            var def = new KitDef { Id = id, Desc = desc, Tags = Split(tags), Aliases = Split(aliases), Build = build };
            Kits.Add(def);
            return def;
        }
    }

    /// <summary>What a kit's build function works with: seeded randomness, the ground, and ways to place things.</summary>
    public sealed class KitContext
    {
        public readonly World World;
        public readonly Group Group;
        public readonly Statement Source;
        public readonly float Density, Size;
        readonly System.Random _rng;
        readonly Dictionary<string, object> _params;

        public KitContext(World world, Group group, Statement source, int seed, float density, float size)
        {
            World = world; Group = group; Source = source; Density = density; Size = size;
            _rng = new System.Random(seed);
            _params = source != null ? source.Obj("params") : null;
        }

        // ---------------------------------------------------------------- numbers

        public float Rand() => (float)_rng.NextDouble();
        public float Range(float a, float b) => a + (b - a) * (float)_rng.NextDouble();
        public int RangeInt(int a, int b) => _rng.Next(a, b);
        public bool Chance(float p) => _rng.NextDouble() < p;
        public T Pick<T>(params T[] options) => options[_rng.Next(options.Length)];

        /// <summary>Scales a count by the kit's density (0.4x to 1.5x).</summary>
        public int N(int atNormalDensity) => Mathf.Max(1, Mathf.RoundToInt(atNormalDensity * Mathf.Lerp(0.4f, 1.5f, Density)));

        /// <summary>How far the kit spreads, in metres, from its size (40 to 108).</summary>
        public float Reach => Mathf.Lerp(40f, 108f, Size);

        public float ParamNum(string name, float fallback)
        {
            if (_params != null && _params.TryGetValue(name, out object v))
            {
                if (v is double d) return (float)d;
                if (v is string s && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
            }
            return fallback;
        }

        public string ParamStr(string name, string fallback)
        {
            return _params != null && _params.TryGetValue(name, out object v) && v is string s ? s.ToLowerInvariant() : fallback;
        }

        // ---------------------------------------------------------------- ground

        public float Ground(float x, float z) => World.Terrain.HeightAt(x, z);
        public bool Wet(float x, float z) { float w = World.Terrain.WaterY; return !float.IsNaN(w) && Ground(x, z) < w + 0.15f; }
        public float WaterY => World.Terrain.WaterY;
        public bool HasWater => !float.IsNaN(World.Terrain.WaterY);

        // ---------------------------------------------------------------- placing

        public static Mesh MeshOf(string asset)
        {
            AssetDef def = Library.Asset(asset) ?? Library.FindAsset(asset) ?? Library.Asset("cube");
            return def.Mesh;
        }

        static readonly Color NoTint = new Color(1f, 1f, 1f, 1f);

        /// <summary>Stands an asset on the ground. yaw &lt; 0 picks a random heading.</summary>
        public void Put(string asset, float x, float z, float yaw = -1f, float scale = 1f, Color? tint = null, float lift = 0f)
        {
            AssetDef def = Library.Asset(asset) ?? Library.FindAsset(asset) ?? Library.Asset("cube");
            if (yaw < 0f) yaw = Rand() * 360f;
            Group.Add(new Placement(def.Mesh, new Vector3(x, lift, z), yaw, scale, tint ?? NoTint) { OnWater = def.OnWater });
        }

        /// <summary>Like Put, with separate scale per axis (stretching a building, say).</summary>
        public void Put(string asset, float x, float z, float yaw, Vector3 scale, Color? tint = null, float lift = 0f)
        {
            AssetDef def = Library.Asset(asset) ?? Library.FindAsset(asset) ?? Library.Asset("cube");
            Group.Add(new Placement
            {
                Mesh = def.Mesh, Position = new Vector3(x, lift, z), Rotation = Quaternion.Euler(0f, yaw, 0f),
                Scale = scale, Tint = tint ?? NoTint, Grounded = true, OnWater = def.OnWater,
            });
        }

        /// <summary>Heading that makes something at (x, z) face the point (tx, tz).</summary>
        public static float Facing(float x, float z, float tx = 0f, float tz = 0f)
        {
            return Mathf.Atan2(tx - x, tz - z) * Mathf.Rad2Deg;
        }

        /// <summary>Random spots in a ring around the user. `ok` can veto a spot.</summary>
        public void Scatter(string asset, int count, float rMin, float rMax, float sMin = 0.8f, float sMax = 1.25f,
            Color? tint = null, Func<float, float, bool> ok = null, bool avoidWater = true)
        {
            AssetDef def = Library.Asset(asset) ?? Library.FindAsset(asset) ?? Library.Asset("cube");
            rMin = Mathf.Max(rMin, World.SafeRadius + 0.5f);
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float angle = Rand() * Mathf.PI * 2f;
                    float radius = Mathf.Lerp(rMin, rMax, Mathf.Pow(Rand(), 0.75f));
                    float x = Mathf.Sin(angle) * radius, z = Mathf.Cos(angle) * radius;
                    if (avoidWater && HasWater && Wet(x, z) != def.OnWater) continue;
                    if (ok != null && !ok(x, z)) continue;
                    Group.Add(new Placement(def.Mesh, new Vector3(x, 0f, z), Rand() * 360f, Range(sMin, sMax), tint ?? NoTint) { OnWater = def.OnWater });
                    break;
                }
            }
        }

        /// <summary>Evenly spaced around a circle.</summary>
        public void Ring(string asset, int count, float radius, float scale = 1f, bool faceCenter = true, Color? tint = null,
            float jitter = 0f, float cx = 0f, float cz = 0f, float startAngle = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle * Mathf.Deg2Rad + (i / (float)count) * Mathf.PI * 2f + Range(-jitter, jitter);
                float x = cx + Mathf.Sin(angle) * radius, z = cz + Mathf.Cos(angle) * radius;
                Put(asset, x, z, faceCenter ? Facing(x, z, cx, cz) : Rand() * 360f, scale, tint);
            }
        }

        /// <summary>Evenly spaced along a line.</summary>
        public void Row(string asset, Vector2 from, Vector2 to, int count, float scale = 1f, float yaw = float.NaN, Color? tint = null)
        {
            if (float.IsNaN(yaw)) yaw = Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = count == 1 ? (from + to) * 0.5f : Vector2.Lerp(from, to, i / (float)(count - 1));
                if (p.magnitude < World.SafeRadius) continue;
                Put(asset, p.x, p.y, yaw, scale, tint);
            }
        }

        /// <summary>Adds a mesh the kit built itself, already in world coordinates.</summary>
        public void AddOwn(Mesh mesh, Color? tint = null)
        {
            Group.OwnedMeshes.Add(mesh);
            Group.Add(new Placement
            {
                Mesh = mesh, Position = Vector3.zero, Rotation = Quaternion.identity, Scale = Vector3.one,
                Tint = tint ?? NoTint, Grounded = false,
            });
        }

        /// <summary>A strip lying on the ground along a path: roads, paths, fairways, rivers of lava.</summary>
        public void Ribbon(IList<Vector2> path, float width, Color color, float lift = 0.06f, float emit = 0f, string name = "ribbon")
        {
            var mb = new MeshBuilder();
            mb.Col(color).Emit(emit);
            var left = new List<Vector3>();
            var right = new List<Vector3>();
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector2 a = path[i], b = path[i + 1];
                float length = Vector2.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 2.5f));
                Vector2 dir = (b - a).normalized;
                var side = new Vector2(-dir.y, dir.x) * (width * 0.5f);
                for (int k = (i == 0 ? 0 : 1); k <= steps; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, k / (float)steps);
                    Vector2 l = p - side, r = p + side;
                    left.Add(new Vector3(l.x, Ground(l.x, l.y) + lift, l.y));
                    right.Add(new Vector3(r.x, Ground(r.x, r.y) + lift, r.y));
                }
            }
            for (int i = 0; i + 1 < left.Count; i++) mb.Quad(left[i], right[i], right[i + 1], left[i + 1], Vector3.up);
            AddOwn(mb.Build(name));
        }

        /// <summary>A blob-shaped patch lying on the ground: greens, bunkers, ponds, plazas, clearings.</summary>
        public void Patch(float cx, float cz, float rx, float rz, Color color, float lift = 0.06f, float wobble = 0.12f,
            float emit = 0f, int sides = 22, string name = "patch")
        {
            var mb = new MeshBuilder();
            mb.Col(color).Emit(emit);
            int rings = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(rx, rz) / 2.5f), 1, 10);
            float phase = Rand() * 10f;
            var centre = new Vector3(cx, Ground(cx, cz) + lift, cz);
            var prev = new List<Vector3>();
            var cur = new List<Vector3>();
            for (int ring = 1; ring <= rings; ring++)
            {
                cur.Clear();
                float f = ring / (float)rings;
                for (int j = 0; j < sides; j++)
                {
                    float a = (j / (float)sides) * Mathf.PI * 2f;
                    float w = 1f + Mathf.Sin(a * 3f + phase) * wobble + Mathf.Sin(a * 5f + phase * 1.7f) * wobble * 0.5f;
                    float x = cx + Mathf.Cos(a) * rx * f * w, z = cz + Mathf.Sin(a) * rz * f * w;
                    cur.Add(new Vector3(x, Ground(x, z) + lift, z));
                }
                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    if (ring == 1) mb.Tri(centre, cur[j], cur[k], Vector3.up);
                    else mb.Quad(prev[j], cur[j], cur[k], prev[k], Vector3.up);
                }
                prev.Clear();
                prev.AddRange(cur);
            }
            AddOwn(mb.Build(name));
        }

        // ---------------------------------------------------------------- interaction

        /// <summary>A place where something happens when the right thing arrives (a ball in a hole, say).</summary>
        public void Zone(Vector3 worldPosition, float radius, string accepts, string message)
        {
            Group.Extras.Add(ScoreZone.Create(World, worldPosition, radius, accepts, message));
        }

        /// <summary>An invisible solid box so thrown and rolling things collide with part of the kit.</summary>
        public void Solid(float x, float z, Vector3 size, float yaw = 0f, float lift = 0f, float bounce = 0.2f)
        {
            var go = new GameObject("KitSolid");
            go.transform.SetParent(World.transform, false);
            go.transform.position = new Vector3(x, Ground(x, z) + lift + size.y * 0.5f, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = PropSystem.Bouncy(bounce);
            Group.Extras.Add(go);
        }
    }

    /// <summary>Builds kits, rebuilds them when the ground changes, and remembers what each one was made from.</summary>
    public sealed class KitSystem
    {
        public const int MaxKits = 3;

        sealed class Live { public KitDef Def; public Statement Source; public float Density, Size; public int Seed; }

        readonly World _world;
        readonly Dictionary<Group, Live> _live = new Dictionary<Group, Live>();

        public KitSystem(World world) { _world = world; }

        public void Apply(Statement s)
        {
            Library.EnsureLoaded();
            KitDef def = Library.Resolve(Library.Kits, s.Str("kit"));
            if (def == null) { _world.Note("no kit called '" + s.Str("kit", "?") + "'"); return; }
            string id = s.Str("id", def.Id);
            if (_world.Objects.Get(id) == null && _world.Objects.CountKind("kit") >= MaxKits)
            {
                _world.Note("kit limit reached, skipped " + def.Id);
                return;
            }
            int seed = s.Has("seed") ? s.Int("seed") : (id.GetHashCode() & 0x7FFF);

            // A kit on its own still makes a whole place: fill in whatever is missing.
            if (_world.Sky.IsDark) _world.Sky.ApplySky(Statement.Make("sky", "preset", def.Sky ?? "morning", "t", 4.0));
            if (!_world.Terrain.HasGround && !_world.Terrain.Generating)
            {
                Statement terrain = def.TerrainStatement(seed);
                if (terrain != null) _world.Apply(terrain);
            }
            if (!float.IsNaN(def.Gravity)) _world.Props.SetGravity(def.Gravity);
            if (def.Ambience != null && !_world.Ambience.IsPlaying) _world.Ambience.Apply(Statement.Make("ambience", "loop", def.Ambience));
            if (def.Weather != null && !_world.Weather.IsActive) _world.Weather.Apply(Statement.Make("weather", "type", def.Weather, "intensity", 0.5));

            Group g = _world.Objects.Begin(id, "kit", s, s.T(2.2f));
            if (s.TryColor("tint", out Color tint)) g.Tint = tint;
            var live = new Live
            {
                Def = def, Source = s, Seed = seed,
                Density = Mathf.Clamp01(s.Num("density", 0.6f)), Size = Mathf.Clamp01(s.Num("size", 0.6f)),
            };
            _live[g] = live;
            Run(g, live);
        }

        void Run(Group g, Live live)
        {
            foreach (GameObject extra in g.Extras) if (extra != null) UnityEngine.Object.Destroy(extra);
            g.Extras.Clear();
            foreach (Mesh m in g.OwnedMeshes) if (m != null) UnityEngine.Object.Destroy(m);
            g.OwnedMeshes.Clear();
            g.Items.Clear();
            try
            {
                live.Def.Build(new KitContext(_world, g, live.Source, live.Seed, live.Density, live.Size));
            }
            catch (Exception e)
            {
                Debug.LogError("ImagineVR: kit " + live.Def.Id + " failed: " + e);
            }
            g.Dirty = true;
        }

        public void Rebuild(Group g, float density)
        {
            if (!_live.TryGetValue(g, out Live live)) return;
            live.Density = Mathf.Clamp01(density);
            Run(g, live);
        }

        /// <summary>The ground changed under the kits: lay them out again on the new heights.</summary>
        public void RebuildAll()
        {
            foreach (KeyValuePair<Group, Live> pair in new List<KeyValuePair<Group, Live>>(_live))
            {
                if (pair.Key.Removing) { _live.Remove(pair.Key); continue; }
                Run(pair.Key, pair.Value);
            }
        }

        public void Forget() { _live.Clear(); }
    }
}
