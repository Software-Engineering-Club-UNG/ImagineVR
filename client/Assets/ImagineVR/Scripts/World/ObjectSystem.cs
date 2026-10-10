using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>One thing standing in the world: which mesh, where, how big, what colour.</summary>
    public struct Placement
    {
        public Mesh Mesh;
        public Vector3 Position;     // y is height above the ground when Grounded, absolute otherwise
        public Quaternion Rotation;
        public Vector3 Scale;
        public Color Tint;
        public bool Grounded;
        public bool OnWater;

        public Placement(Mesh mesh, Vector3 position, float yaw, float scale, Color tint, bool grounded = true)
        {
            Mesh = mesh; Position = position; Rotation = Quaternion.Euler(0f, yaw, 0f);
            Scale = new Vector3(scale, scale, scale); Tint = tint; Grounded = grounded; OnWater = false;
        }
    }

    /// <summary>
    /// Everything one statement created: a spawned object, a scatter, or a whole kit.
    /// Drawn with GPU instancing, one draw call per mesh and tint, with no GameObjects.
    /// </summary>
    public sealed class Group
    {
        public string Id;
        public string Kind;                 // spawn, scatter, kit
        public Statement Source;
        public readonly List<Placement> Items = new List<Placement>();
        public readonly List<GameObject> Extras = new List<GameObject>();   // labels, colliders, triggers
        public readonly List<Mesh> OwnedMeshes = new List<Mesh>();          // meshes built just for this group

        public float Appear, AppearTarget = 1f, AppearSeconds = 1.5f;
        public bool Removing;
        public Vector4 Motion;              // orbit speed, bob height, bob speed, spin speed
        public float Sway = 1f;
        public Color Tint = Color.white;
        public float Scale = 1f;
        public bool Dirty = true;

        internal readonly List<Batch> Batches = new List<Batch>();

        internal sealed class Batch
        {
            public Mesh Mesh;
            public Matrix4x4[] Matrices;
            public int Count;
            public Bounds Bounds;
            public Color Tint;
            public MaterialPropertyBlock Props = new MaterialPropertyBlock();
        }

        public void Add(Placement p) { Items.Add(p); Dirty = true; }
    }

    /// <summary>Spawns, scatters, modifies, removes and draws everything that is not ground, sky or a physics prop.</summary>
    public sealed class ObjectSystem
    {
        static readonly int AppearId = Shader.PropertyToID("_Appear"), TintId = Shader.PropertyToID("_Tint");
        static readonly int MotionId = Shader.PropertyToID("_Motion"), SwayId = Shader.PropertyToID("_Sway");

        public const int MaxSpawned = 60, MaxScatterCount = 300, MaxScatterTotal = 1500;

        readonly World _world;
        readonly Material _lit, _glow;
        readonly Dictionary<string, Group> _groups = new Dictionary<string, Group>();
        readonly List<Group> _order = new List<Group>();
        readonly List<Group> _dying = new List<Group>();
        int _auto;

        public IReadOnlyList<Group> Groups => _order;
        public Material LitMaterial => _lit;
        public Material GlowMaterial => _glow;
        public int Count => _order.Count;

        public ObjectSystem(World world)
        {
            _world = world;
            _lit = new Material(Shader.Find("ImagineVR/Lit")) { name = "ImagineLit", enableInstancing = true };
            _glow = new Material(Shader.Find("ImagineVR/Glow")) { name = "ImagineGlow", enableInstancing = true };
        }

        // ------------------------------------------------------------------ bookkeeping

        public Group Get(string id) => id != null && _groups.TryGetValue(id, out Group g) ? g : null;

        /// <summary>The exact id, or every "id.part" piece of a sculpture.</summary>
        public List<Group> Matching(string id)
        {
            var result = new List<Group>();
            if (string.IsNullOrEmpty(id)) return result;
            if (_groups.TryGetValue(id, out Group exact)) { result.Add(exact); return result; }
            string prefix = id + ".";
            foreach (Group g in _order) if (g.Id.StartsWith(prefix, StringComparison.Ordinal)) result.Add(g);
            return result;
        }

        string NextId(string kind)
        {
            string id;
            do { id = kind + (++_auto); } while (_groups.ContainsKey(id));
            return id;
        }

        public Group Begin(string id, string kind, Statement source, float seconds)
        {
            if (string.IsNullOrEmpty(id)) id = NextId(kind);
            if (_groups.TryGetValue(id, out Group old)) Retire(old, 0.4f);
            var g = new Group { Id = id, Kind = kind, Source = source, AppearSeconds = Mathf.Max(0.2f, seconds) };
            _groups[id] = g;
            _order.Add(g);
            return g;
        }

        void Retire(Group g, float seconds)
        {
            if (g.Removing) return;
            g.Removing = true;
            g.AppearTarget = 0f;
            g.AppearSeconds = Mathf.Max(0.2f, seconds);
            if (_groups.TryGetValue(g.Id, out Group current) && current == g) _groups.Remove(g.Id);
            _order.Remove(g);
            _dying.Add(g);
            foreach (GameObject extra in g.Extras) if (extra != null) UnityEngine.Object.Destroy(extra);
            g.Extras.Clear();
        }

        public int CountKind(string kind)
        {
            int n = 0;
            foreach (Group g in _order) if (g.Kind == kind) n++;
            return n;
        }

        public int ScatterTotal()
        {
            int n = 0;
            foreach (Group g in _order) if (g.Kind == "scatter") n += g.Items.Count;
            return n;
        }

        // ------------------------------------------------------------------ statements

        static Vector4 MotionFor(string name, AssetDef def)
        {
            switch (name)
            {
                case "bob": return new Vector4(0f, 0.25f, 1.4f, 0f);
                case "spin": return new Vector4(0f, 0f, 0f, 0.8f);
                case "hover": return new Vector4(0f, 0.18f, 1.1f, 0.35f);
                case "orbit": return new Vector4(0.12f, 0.4f, 0.7f, 0f);
                case "drift": return new Vector4(0.02f, 0.1f, 0.4f, 0f);
                default: return Vector4.zero;
            }
        }

        static float Fit(AssetDef def)
        {
            Bounds b = def.Bounds;
            return Mathf.Max(b.extents.x, b.extents.z);
        }

        public Group Spawn(Statement s)
        {
            string name = s.Str("asset", "cube");
            AssetDef def = Library.FindAsset(name);
            bool standIn = def == null;
            if (standIn) def = Library.Asset("cube");
            if (CountKind("spawn") >= MaxSpawned && Get(s.Str("id")) == null) { _world.Note("object limit reached, skipped one"); return null; }

            Vector3 scale = s.Scale("scale", 1f);
            float footprint = Fit(def) * Mathf.Max(scale.x, scale.z);
            Vector3 pos;
            bool free = false;   // exact position requested: do not push it out of the safe area
            if (s.TryVec3("at", out Vector3 at))
            {
                pos = at;
                free = true;
            }
            else
            {
                string place = s.Str("place", "front");
                float d = s.Num("distance", 4f);
                if (place != "center" && place != "above") d = Mathf.Max(d, Mathf.Min(footprint + 1.2f, 40f));
                switch (place)
                {
                    case "left": pos = new Vector3(-d, 0f, 0f); break;
                    case "right": pos = new Vector3(d, 0f, 0f); break;
                    case "behind": pos = new Vector3(0f, 0f, -d); break;
                    case "center": pos = Vector3.zero; free = true; break;
                    case "above": pos = new Vector3(0f, Mathf.Max(d, 3f), 0f); free = true; break;
                    default: pos = new Vector3(0f, 0f, d); break;
                }
                pos.y += s.Num("height", 0f);
            }
            if (!free && def.Airborne && pos.y < 1f) pos.y = 6f;

            float yaw = Mathf.Atan2(-pos.x, -pos.z) * Mathf.Rad2Deg + s.Num("rot_y", 0f);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            if (s.TryVec3("rot", out Vector3 euler)) rot = Quaternion.Euler(euler.x, yaw + euler.y, euler.z);

            if (s.Flag("physics") && _world.Props != null && (def.Prop || def.Primitive))
            {
                _world.Props.SpawnPhysics(s, def, pos, rot, scale);
                return null;
            }

            Group g = Begin(s.Str("id"), "spawn", s, s.T(0.9f));
            if (s.TryColor("tint", out Color tint)) g.Tint = tint;
            g.Motion = MotionFor(s.Str("motion", "none"), def);
            g.Add(new Placement { Mesh = def.Mesh, Position = pos, Rotation = rot, Scale = scale, Tint = Color.white, Grounded = true, OnWater = def.OnWater });
            if (standIn) _world.AddLabel(g, name.Replace('_', ' '), pos, def.Bounds.size.y * scale.y + 0.35f);
            return g;
        }

        public Group Scatter(Statement s)
        {
            string name = s.Str("asset", "cube");
            AssetDef def = Library.FindAsset(name) ?? Library.Asset("cube");
            int count = Mathf.Clamp(s.Int("count", 12), 1, MaxScatterCount);

            if (s.Flag("physics") && _world.Props != null && (def.Prop || def.Primitive))
            {
                _world.Props.ScatterPhysics(s, def, count);
                return null;
            }

            Group existing = Get(s.Str("id"));
            int room = MaxScatterTotal - ScatterTotal() + (existing != null ? existing.Items.Count : 0);
            if (count > room)
            {
                if (room <= 0) { _world.Note("the world is full, skipped a scatter"); return null; }
                count = room;
            }

            Group g = Begin(s.Str("id"), "scatter", s, s.T(1.6f));
            if (s.TryColor("tint", out Color tint)) g.Tint = tint;
            g.Motion = MotionFor(s.Str("motion", def.Airborne ? "orbit" : "none"), def);
            Populate(g, s, def, count);
            return g;
        }

        /// <summary>Fills a scatter. The same seed always gives the same first N positions, so raising the count only adds.</summary>
        void Populate(Group g, Statement s, AssetDef def, int count)
        {
            g.Items.Clear();
            string area = s.Str("area", def.Airborne ? "sky" : "around");
            int seed = s.Has("seed") ? s.Int("seed") : (g.Id.GetHashCode() & 0x7FFFFFFF);
            var rng = new System.Random(seed);
            float footprint = Fit(def);

            float rMin, rMax, arcCenter = 0f, arcHalf = 180f;
            switch (area)
            {
                case "front": rMin = 5f; rMax = 45f; arcHalf = 50f; break;
                case "behind": rMin = 5f; rMax = 45f; arcCenter = 180f; arcHalf = 50f; break;
                case "left": rMin = 5f; rMax = 45f; arcCenter = -90f; arcHalf = 50f; break;
                case "right": rMin = 5f; rMax = 45f; arcCenter = 90f; arcHalf = 50f; break;
                case "ring": rMin = 10f; rMax = 13f; break;
                case "edges": rMin = 70f; rMax = 108f; break;
                case "everywhere": rMin = 4f; rMax = 108f; break;
                case "sky": rMin = 8f; rMax = 85f; break;
                default: rMin = 6f; rMax = 60f; break;
            }
            if (s.TryRange("radius", out float a, out float b)) { rMin = a; rMax = Mathf.Max(b, a + 0.01f); }
            float safe = World.SafeRadius + Mathf.Min(footprint, 6f);
            bool sky = area == "sky" || def.Airborne;
            float hMin = sky ? 10f : 0f, hMax = sky ? 40f : 0f;
            if (s.TryRange("height", out a, out b)) { hMin = a; hMax = b; }
            if (hMin < 0.5f && hMax < 0.5f) rMin = Mathf.Max(rMin, Mathf.Min(safe, rMax - 0.01f));
            float sMin = 0.8f, sMax = 1.25f;
            if (s.TryRange("scale", out a, out b)) { sMin = a; sMax = b; }

            float waterY = _world.Terrain.WaterY;
            bool hasWater = !float.IsNaN(waterY);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = Vector3.zero;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float angle, radius;
                    if (area == "ring")
                    {
                        angle = (i + (float)rng.NextDouble() * 0.3f) / count * 360f;
                        radius = Mathf.Lerp(rMin, rMax, (float)rng.NextDouble());
                    }
                    else
                    {
                        angle = arcCenter + ((float)rng.NextDouble() * 2f - 1f) * arcHalf;
                        radius = Mathf.Lerp(rMin, rMax, Mathf.Pow((float)rng.NextDouble(), 0.75f));
                    }
                    float rad = angle * Mathf.Deg2Rad;
                    pos = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
                    if (!hasWater || sky) break;
                    bool wet = _world.Terrain.HeightAt(pos.x, pos.z) < waterY + 0.15f;
                    if (wet == def.OnWater) break;   // floating things want water, everything else wants land
                }
                pos.y = Mathf.Lerp(hMin, hMax, (float)rng.NextDouble());
                float scale = Mathf.Lerp(sMin, sMax, (float)rng.NextDouble());
                float yaw = (float)rng.NextDouble() * 360f;
                var p = new Placement(def.Mesh, pos, yaw, scale, Color.white) { OnWater = def.OnWater };
                g.Items.Add(p);
            }
            g.Dirty = true;
        }

        public void Modify(Statement s)
        {
            foreach (Group g in Matching(s.Str("id")))
            {
                if (s.TryColor("tint", out Color tint)) g.Tint = tint;
                if (s.Has("scale")) { g.Scale = Mathf.Clamp(s.Num("scale", 1f), 0.05f, 60f); g.Dirty = true; }
                if (s.Has("motion")) g.Motion = MotionFor(s.Str("motion"), null);
                if (s.Has("count") && g.Kind == "scatter" && g.Source != null)
                {
                    AssetDef def = Library.FindAsset(g.Source.Str("asset", "cube")) ?? Library.Asset("cube");
                    int room = MaxScatterTotal - ScatterTotal() + g.Items.Count;
                    int count = Mathf.Clamp(s.Int("count"), 0, Mathf.Min(MaxScatterCount, room));
                    if (count == 0) { Retire(g, s.T(0.8f)); continue; }
                    Populate(g, g.Source, def, count);
                }
                if (s.Has("density") && g.Kind == "kit") _world.Kits.Rebuild(g, s.Num("density"));
            }
        }

        public void Remove(Statement s)
        {
            foreach (Group g in Matching(s.Str("id"))) Retire(g, s.T(0.8f));
        }

        public void Remove(string id, float seconds)
        {
            foreach (Group g in Matching(id)) Retire(g, seconds);
        }

        public void Clear(float seconds)
        {
            foreach (Group g in new List<Group>(_order)) Retire(g, seconds);
        }

        /// <summary>The ground changed: everything standing on it settles onto the new heights.</summary>
        public void Resettle()
        {
            foreach (Group g in _order) g.Dirty = true;
        }

        // ------------------------------------------------------------------ drawing

        struct BatchKey : IEquatable<BatchKey>
        {
            public Mesh Mesh; public Color32 Tint; public int Sector;
            public bool Equals(BatchKey o) => Mesh == o.Mesh && Sector == o.Sector && Tint.r == o.Tint.r && Tint.g == o.Tint.g && Tint.b == o.Tint.b;
            public override bool Equals(object obj) => obj is BatchKey k && Equals(k);
            public override int GetHashCode() => (Mesh != null ? Mesh.GetHashCode() : 0) ^ (Tint.r << 16 | Tint.g << 8 | Tint.b) ^ (Sector * 7919);
        }

        readonly Dictionary<BatchKey, List<Matrix4x4>> _scratch = new Dictionary<BatchKey, List<Matrix4x4>>();

        void Rebuild(Group g)
        {
            g.Dirty = false;
            g.Batches.Clear();
            _scratch.Clear();
            bool moving = g.Motion.x != 0f;
            float waterY = _world.Terrain.WaterY;
            foreach (Placement p in g.Items)
            {
                if (p.Mesh == null) continue;
                Vector3 pos = p.Position;
                if (p.Grounded)
                {
                    float ground = _world.Terrain.HeightAt(pos.x, pos.z);
                    if (p.OnWater && !float.IsNaN(waterY) && ground < waterY) ground = waterY;
                    pos.y += ground;
                }
                // split by quadrant so groups behind the user can be culled as a whole
                int sector = moving ? 0 : (pos.x >= 0f ? 1 : 0) | (pos.z >= 0f ? 2 : 0);
                var key = new BatchKey { Mesh = p.Mesh, Tint = p.Tint, Sector = sector };
                if (!_scratch.TryGetValue(key, out List<Matrix4x4> list))
                {
                    list = new List<Matrix4x4>();
                    _scratch[key] = list;
                }
                list.Add(Matrix4x4.TRS(pos, p.Rotation, p.Scale * g.Scale));
            }
            foreach (KeyValuePair<BatchKey, List<Matrix4x4>> pair in _scratch)
            {
                List<Matrix4x4> list = pair.Value;
                for (int start = 0; start < list.Count; start += 500)
                {
                    int n = Mathf.Min(500, list.Count - start);
                    var batch = new Group.Batch { Mesh = pair.Key.Mesh, Count = n, Matrices = new Matrix4x4[n], Tint = pair.Key.Tint };
                    Bounds mb = pair.Key.Mesh.bounds;
                    var bounds = new Bounds();
                    for (int i = 0; i < n; i++)
                    {
                        Matrix4x4 m = list[start + i];
                        batch.Matrices[i] = m;
                        Vector3 center = m.MultiplyPoint3x4(mb.center);
                        float radius = mb.extents.magnitude * m.lossyScale.x;
                        var one = new Bounds(center, Vector3.one * (radius * 2f));
                        if (i == 0) bounds = one; else bounds.Encapsulate(one);
                    }
                    bounds.Expand(2f);
                    batch.Bounds = moving ? new Bounds(Vector3.zero, Vector3.one * 2000f) : bounds;
                    g.Batches.Add(batch);
                }
            }
        }

        public void Tick(float dt)
        {
            foreach (Group g in _order) Step(g, dt);
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                Group g = _dying[i];
                Step(g, dt);
                if (g.Appear <= 0.0001f)
                {
                    foreach (Mesh m in g.OwnedMeshes) if (m != null) UnityEngine.Object.Destroy(m);
                    g.OwnedMeshes.Clear();
                    _dying.RemoveAt(i);
                }
            }
        }

        static void Step(Group g, float dt)
        {
            g.Appear = Mathf.MoveTowards(g.Appear, g.AppearTarget, dt / g.AppearSeconds);
        }

        public void Render()
        {
            foreach (Group g in _order) Draw(g);
            foreach (Group g in _dying) Draw(g);
        }

        void Draw(Group g)
        {
            if (g.Dirty) Rebuild(g);
            if (g.Appear <= 0.0001f) return;
            foreach (Group.Batch b in g.Batches)
            {
                Color tint = g.Tint * (Color)b.Tint;
                b.Props.SetFloat(AppearId, g.Appear);
                b.Props.SetColor(TintId, tint);
                b.Props.SetVector(MotionId, g.Motion);
                b.Props.SetFloat(SwayId, g.Sway);
                var rp = new RenderParams(_lit)
                {
                    matProps = b.Props,
                    worldBounds = b.Bounds,
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                Graphics.RenderMeshInstanced(rp, b.Mesh, 0, b.Matrices, b.Count);
                if (b.Mesh.subMeshCount > 1)
                {
                    rp.material = _glow;
                    Graphics.RenderMeshInstanced(rp, b.Mesh, 1, b.Matrices, b.Count);
                }
            }
        }
    }
}
