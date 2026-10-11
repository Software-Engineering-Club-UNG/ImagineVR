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
        public bool AbsoluteTint;

        public Placement(Mesh mesh, Vector3 position, float yaw, float scale, Color tint, bool grounded = true)
        {
            Mesh = mesh; Position = position; Rotation = Quaternion.Euler(0f, yaw, 0f);
            Scale = new Vector3(scale, scale, scale); Tint = tint; Grounded = grounded; OnWater = false;
            AbsoluteTint = false;
        }
    }

    /// <summary>
    /// Everything one statement created: a spawned object or a whole scatter. Its still items are
    /// combined into a few ordinary meshes on ordinary renderers (see ObjectSystem.Rebuild).
    /// </summary>
    public sealed class Group
    {
        public string Id;
        public string Kind;                 // spawn, scatter
        public Statement Source;
        public string Asset;                // the asset its items are copies of
        public readonly List<Placement> Items = new List<Placement>();
        public readonly List<GameObject> Extras = new List<GameObject>();   // creatures, movers, labels, colliders

        public float Appear, AppearTarget = 1f, AppearSeconds = 1.5f;
        public bool Removing;
        public Vector4 Motion;              // orbit speed, bob height, bob speed, spin speed
        public float Sway = 1f;
        public Color Tint = Color.white;
        public float Scale = 1f;
        public bool Dirty = true;

        internal GameObject Root;
        internal readonly List<MeshRenderer> Renderers = new List<MeshRenderer>();
        internal MeshRenderer Shadow;
        internal readonly List<Mesh> Combined = new List<Mesh>();
        internal readonly MaterialPropertyBlock Props = new MaterialPropertyBlock();
        internal float ShownAppear = -1f;
        internal Color ShownTint;
        internal Vector4 ShownMotion;
        /// <summary>How many items and triangles are really in the renderers: what the session log reports.</summary>
        public int Drawn, Triangles;

        public void Add(Placement p) { Items.Add(p); Dirty = true; }
    }

    /// <summary>Spawns, scatters, modifies, removes and draws everything that is not ground, sky or a physics prop.</summary>
    public sealed class ObjectSystem
    {
        static readonly int AppearId = Shader.PropertyToID("_Appear"), TintId = Shader.PropertyToID("_Tint");
        static readonly int MotionId = Shader.PropertyToID("_Motion"), SwayId = Shader.PropertyToID("_Sway");

        public const int MaxSpawned = 60, MaxScatterCount = 300, MaxScatterTotal = 1500;

        readonly World _world;
        readonly Transform _root;
        readonly Material _lit, _glow, _litBaked, _glowBaked;
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
            _root = new GameObject("Scenery").transform;
            _root.SetParent(world.transform, false);
            _lit = new Material(Shader.Find("ImagineVR/Lit")) { name = "ImagineLit" };
            _glow = new Material(Shader.Find("ImagineVR/Glow")) { name = "ImagineGlow" };
            _litBaked = new Material(_lit) { name = "ImagineLitBaked" };
            _glowBaked = new Material(_glow) { name = "ImagineGlowBaked" };
            _litBaked.EnableKeyword("IM_BAKED");
            _glowBaked.EnableKeyword("IM_BAKED");
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

        /// <summary>How many things of a kind there are. The pieces of one sculpture ("robot.head", "robot.body") are one thing.</summary>
        public int CountKind(string kind)
        {
            var roots = new HashSet<string>();
            foreach (Group g in _order)
            {
                if (g.Kind != kind) continue;
                int dot = g.Id.IndexOf('.');
                roots.Add(dot > 0 ? g.Id.Substring(0, dot) : g.Id);
            }
            return roots.Count;
        }

        /// <summary>
        /// How high the tallest thing standing on the ground reaches, in metres above it. Something
        /// that flies has to clear this to be "over" a place and not in among it.
        /// </summary>
        public float Skyline()
        {
            float top = 0f;
            foreach (Group g in _order)
                foreach (Placement p in g.Items)
                    if (p.Mesh != null && p.Grounded) top = Mathf.Max(top, p.Position.y + p.Mesh.bounds.max.y * p.Scale.y);
            return top;
        }

        public int ScatterTotal()
        {
            int n = 0;
            // what grows on the ground by itself is not charged to what the user may ask for
            foreach (Group g in _order) if (g.Kind == "scatter" && !g.Id.StartsWith(World.Cover, System.StringComparison.Ordinal)) n += g.Items.Count;
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

        /// <summary>
        /// Living things get an object of their own that moves, in place of an instanced placement.
        /// `behavior` comes from the statement; some assets (a zombie) are alive without being told.
        /// Returns false when this one is not alive. Once the world has enough of them the rest are
        /// quietly left out, since one that stood frozen and could not be hit would look broken.
        /// </summary>
        public bool Enliven(Group g, AssetDef def, Vector3 position, float scale, string behavior = null)
        {
            if (string.IsNullOrEmpty(behavior)) behavior = Creature.DefaultFor(def);
            if (behavior == null || behavior == "still" || !Application.isPlaying) return false;
            int alive = 0;
            foreach (Group other in _order) foreach (GameObject extra in other.Extras) if (extra != null && extra.GetComponent<Creature>() != null) alive++;
            if (alive >= Creature.MaxLive) return true;
            g.Extras.Add(Creature.Create(_world, def, position, scale, g.Tint, behavior, g.Id));
            return true;
        }

        public const int MaxSolids = 240;

        /// <summary>
        /// Gives one placement a body, so the user cannot walk or drive through it and thrown things
        /// bounce off it. Scenery has none by default: a forest of colliders would cost too much.
        /// </summary>
        public void Solidify(Group g, AssetDef def, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!Application.isPlaying) return;
            int solids = 0;
            foreach (Group other in _order) foreach (GameObject extra in other.Extras) if (extra != null && extra.name == "Solid") solids++;
            if (solids >= MaxSolids) return;
            Bounds b = def.Bounds;
            var go = new GameObject("Solid");
            go.transform.SetParent(_world.transform, false);
            go.transform.SetPositionAndRotation(new Vector3(position.x, _world.SurfaceHeight(position.x, position.z) + position.y, position.z), rotation);
            go.transform.localScale = scale;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = Vector3.Max(b.size, Vector3.one * 0.05f);
            box.sharedMaterial = PropSystem.Bouncy(0.2f);
            g.Extras.Add(go);
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
            if (CountKind("spawn") >= MaxSpawned && Matching(RootOf(s.Str("id"))).Count == 0)
            {
                _world.Note("object limit reached, skipped one");
                _world.Placed(s, name, null, false, Vector3.zero, "skipped: object limit");
                return null;
            }

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

            // A thing turns to face the user. A plain shape does not: it is a building block (a lane, a
            // wall, a counter) and lies square to them, its width across and its depth away as written.
            // Turned to face them, every stretched block off to one side came out skewed.
            float yaw = (def.Primitive ? 0f : Mathf.Atan2(-pos.x, -pos.z) * Mathf.Rad2Deg) + s.Num("rot_y", 0f);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            if (s.TryVec3("rot", out Vector3 euler)) rot = Quaternion.Euler(euler.x, yaw + euler.y, euler.z);

            // all of that was worked out around the user; now put it where they actually are
            Pose frame = s.Frame();
            // a room that is not there is no reason to place nothing: the thing goes in front of the user as any other would
            if (s.Has("room") && _world.Structures.TryRoomPose(s.Str("room"), out Pose room))
            {
                frame = room;
                Vector3 roomPosition = Statement.InWorld(frame, pos);
                roomPosition.y -= _world.SurfaceHeight(roomPosition.x, roomPosition.z);
                pos = roomPosition;
                rot = frame.rotation * Quaternion.Euler(0, s.Num("rot_y"), 0);
                frame = Pose.identity;
            }
            pos = Statement.InWorld(frame, pos);
            rot = frame.rotation * rot;
            _world.Placed(s, name, def, standIn, pos, null);

            if (s.Flag("physics") && _world.Props != null && (def.Prop || def.Primitive))
            {
                _world.Props.SpawnPhysics(s, def, Copies(s, pos, rot, RowClear(def, scale)), scale);
                return null;
            }

            Group g = Begin(s.Str("id"), "spawn", s, s.T(0.9f));
            g.Asset = def.Id;
            if (s.TryColor("tint", out Color tint)) g.Tint = tint;
            g.Motion = MotionFor(s.Str("motion", "none"), def);
            if (Application.isPlaying && s.TryPath("path", out List<Vector3> points))
            {
                // it travels: a car in traffic, a rival on a track, a ship crossing the bay
                var route = new Route(points, s.Flag("loop", true));
                g.Extras.Add(Mover.Create(_world, def, route, Mathf.Clamp(s.Num("speed", 4f), 0.1f, 40f), s.Num("start", 0f), scale, g.Tint, g.Id));
            }
            else if (!Enliven(g, def, pos, scale.y, s.Str("behavior")))
            {
                foreach (Pose copy in Copies(s, pos, rot, RowClear(def, scale)))
                {
                    g.Add(new Placement { Mesh = def.Mesh, Position = copy.position, Rotation = copy.rotation, Scale = scale, Tint = Color.white, Grounded = true, OnWater = def.OnWater });
                    if (s.Flag("solid")) Solidify(g, def, copy.position, copy.rotation, scale);
                }
            }
            if (standIn) _world.AddLabel(g, name.Replace('_', ' '), pos, def.Bounds.size.y * scale.y + 0.35f);
            return g;
        }

        public const int MaxRepeat = 40;

        /// <summary>
        /// Where one `spawn` stands. Usually a single place, but `repeat` makes a run of copies,
        /// each a `step` on from the last, measured as the user sees it: right, up, away (a flight of
        /// stairs, a colonnade, a row of houses, a stack), and `ring` stands them in a circle of that
        /// radius, facing its middle (a stone circle, an arena of seats, a ring of torches).
        /// One statement, one object to the limits, however many copies.
        /// </summary>
        /// <summary>
        /// The least a row of copies of this may step along the ground without one standing inside
        /// the next. Nothing for a plain shape: those are stretched and stacked into steps, walls and
        /// floors on purpose.
        /// </summary>
        static float RowClear(AssetDef def, Vector3 scale)
        {
            if (def.Primitive) return 0f;
            Bounds b = def.Bounds;
            return Mathf.Min(b.size.x * scale.x, b.size.z * scale.z) * 0.95f;
        }

        static List<Pose> Copies(Statement s, Vector3 pos, Quaternion rot, float clear = 0f)
        {
            var copies = new List<Pose>();
            int n = Mathf.Clamp(s.Int("repeat", 1), 1, MaxRepeat);
            float ring = s.Num("ring", 0f);
            if (n < 2 || (ring <= 0f && !s.Has("step"))) { copies.Add(new Pose(pos, rot)); return copies; }
            Quaternion facing = s.Frame().rotation;
            s.TryVec3("step", out Vector3 step);
            // a row along the ground written tighter than the things are wide is opened out to fit them
            float run = new Vector2(step.x, step.z).magnitude;
            if (ring <= 0f && Mathf.Abs(step.y) < 0.01f && run > 0.01f && run < clear) step *= clear / run;
            if (ring > 0f && clear > 0f) ring = Mathf.Max(ring, clear * n / (2f * Mathf.PI));
            for (int i = 0; i < n; i++)
            {
                if (ring > 0f)
                {
                    Quaternion around = Quaternion.Euler(0f, i * 360f / n, 0f);
                    Vector3 spoke = facing * (around * Vector3.forward);
                    // each one turned to look at the middle, then by whatever turn the statement gave it
                    copies.Add(new Pose(pos + spoke * ring + Vector3.up * (step.y * i), Quaternion.LookRotation(-spoke) * Quaternion.Inverse(facing) * rot));
                }
                // (not in the object's own directions: it faces the user, so its right is their left)
                else copies.Add(new Pose(pos + facing * (step * i), rot));
            }
            return copies;
        }

        // ------------------------------------------------------------------ getting in and on things

        /// <summary>Something made to carry someone: a vehicle, an animal, anything that flies when ridden.</summary>
        public static bool Rideable(AssetDef def)
        {
            return def != null && !def.Primitive && (def.Category == "vehicles" || def.Category == "animals" || def.FliesRidden);
        }

        /// <summary>
        /// The nearest thing to ride that a hand is pointing at: any parked car, tethered horse or
        /// landed saucer standing in the world, whichever statement put it there.
        /// </summary>
        public bool PointedAt(Vector3 origin, Vector3 direction, float reach, out Group group, out int index)
        {
            group = null; index = -1;
            float best = float.MaxValue;
            foreach (Group g in _order)
            {
                AssetDef def = g.Asset != null ? Library.FindAsset(g.Asset) : null;
                if (!Rideable(def)) continue;
                Bounds b = def.Bounds;
                for (int i = 0; i < g.Items.Count; i++)
                {
                    Placement p = g.Items[i];
                    if (!p.Grounded) continue;
                    float size = Mathf.Max(p.Scale.x, p.Scale.y, p.Scale.z) * g.Scale;
                    float radius = Mathf.Max(b.extents.magnitude * size * 0.6f, 0.5f);
                    Vector3 centre = new Vector3(p.Position.x, _world.SurfaceHeight(p.Position.x, p.Position.z) + p.Position.y + b.center.y * size, p.Position.z);
                    float along = Vector3.Dot(centre - origin, direction);
                    if (along < -radius || along > reach + radius) continue;
                    float off = (origin + direction * Mathf.Max(along, 0f) - centre).magnitude;
                    if (off < radius && along < best) { best = along; group = g; index = i; }
                }
            }
            return group != null;
        }

        /// <summary>Takes one standing thing out of the world (it is about to be ridden away), with the body it was given.</summary>
        public Placement Take(Group g, int index)
        {
            Placement p = g.Items[index];
            g.Items.RemoveAt(index);
            g.Dirty = true;
            for (int i = g.Extras.Count - 1; i >= 0; i--)
            {
                GameObject extra = g.Extras[i];
                if (extra == null || extra.name != "Solid" || Flat(extra.transform.position - p.Position).sqrMagnitude > 0.01f) continue;
                UnityEngine.Object.Destroy(extra);
                g.Extras.RemoveAt(i);
            }
            return p;
        }

        /// <summary>Leaves a thing standing where its rider got off it, to be got back onto.</summary>
        public Group Park(AssetDef def, Vector3 position, float yaw, Color tint)
        {
            Group g = Begin("parked_" + def.Id, "spawn", Statement.Make("spawn", "id", "parked_" + def.Id, "asset", def.Id), 0.3f);
            g.Asset = def.Id;
            g.Tint = tint;
            g.Add(new Placement { Mesh = def.Mesh, Position = new Vector3(position.x, 0f, position.z), Rotation = Quaternion.Euler(0f, yaw, 0f), Scale = Vector3.one, Tint = Color.white, Grounded = true, OnWater = def.OnWater });
            return g;
        }

        static string RootOf(string id)
        {
            int dot = id != null ? id.IndexOf('.') : -1;
            return dot > 0 ? id.Substring(0, dot) : id;
        }

        public Group Scatter(Statement s)
        {
            string name = s.Str("asset", "cube");
            AssetDef def = Library.FindAsset(name);
            bool standIn = def == null;
            if (def == null) def = Library.Asset("cube");
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
            g.Asset = def.Id;
            if (s.TryColor("tint", out Color tint)) g.Tint = tint;
            g.Motion = MotionFor(s.Str("motion", def.Airborne ? "orbit" : "none"), def);
            Populate(g, s, def, count);
            if (!g.Id.StartsWith(World.Cover, System.StringComparison.Ordinal))
            {
                // where the nearest one actually stands, not where the scatter was centred
                Vector3 nearest = s.Frame().position;
                float best = float.MaxValue;
                Vector3 user = _world.Head != null ? _world.Head.position : Vector3.zero;
                foreach (Placement p in g.Items) { float d = Flat(p.Position - user).sqrMagnitude; if (d < best) { best = d; nearest = p.Position; } }
                foreach (GameObject extra in g.Extras) if (extra != null) { float d = Flat(extra.transform.position - user).sqrMagnitude; if (d < best) { best = d; nearest = extra.transform.position; } }
                _world.Placed(s, name, def, standIn, nearest, g.Items.Count + g.Extras.Count == 0 ? "nothing was placed: no ground suited it" : null,
                    g.Items.Count, g.Extras.Count);
            }
            return g;
        }

        /// <summary>How much ground one of these takes: the radius of the circle it is kept clear by.</summary>
        static float Radius(AssetDef def)
        {
            Bounds b = def.Bounds;
            // something long and thin (a bus, a fence) is not given a circle as wide as it is long
            return Mathf.Lerp(Mathf.Min(b.extents.x, b.extents.z), Mathf.Max(b.extents.x, b.extents.z), 0.75f);
        }

        /// <summary>
        /// The circles of ground already taken by what other statements put down: x, z, radius. Only
        /// things of some size that stand up from the ground count: not grass, not a plain shape laid
        /// as a floor or a road, not what flies.
        /// </summary>
        List<Vector4> Standing(Group self)
        {
            var result = new List<Vector4>();
            foreach (Group g in _order)
            {
                if (g == self || g.Id.StartsWith(World.Cover, StringComparison.Ordinal)) continue;
                AssetDef def = g.Asset != null ? Library.FindAsset(g.Asset) : null;
                // what lies flat (a road, a rug, a marking) is stood on, not stood clear of
                if (def == null || def.Primitive || def.Airborne || def.Bounds.size.y < 0.3f) continue;
                float radius = Radius(def);
                foreach (Placement p in g.Items)
                {
                    float r = radius * Mathf.Max(p.Scale.x, p.Scale.z) * g.Scale;
                    if (r >= 0.5f && p.Grounded && p.Position.y < 1f) result.Add(new Vector4(p.Position.x, p.Position.z, r, 0f));
                }
            }
            return result;
        }

        static float Yaw(string side) => side == "east" ? 90f : side == "south" ? 180f : side == "west" ? 270f : 0f;

        /// <summary>
        /// Where many of one thing stand in a room. `rows`: a tidy grid across the floor, all facing
        /// one way, with an aisle down the middle when the room is wide enough for one (desks, seats,
        /// beds, pews). `walls`: lined up against the walls, facing in, clear of the doorways
        /// (lockers, shelves, machines). Otherwise spread over the floor, clear of each other and of
        /// the doorways. A grid depends only on the room and the count, so two scatters of the same
        /// count pair up: chairs shifted a little by `at` sit behind their desks.
        /// Positions come back in the world, with y as height above the room's floor.
        /// </summary>
        public static List<Pose> InRoom(StructureSystem.Room room, Pose pose, Statement s, AssetDef def, int count, string arrange, float gap, float scale, System.Random rng)
        {
            var spots = new List<Pose>();
            Bounds b = def.Bounds;
            float half = Mathf.Max(b.extents.x, b.extents.z) * scale, wide = b.size.x * scale, deep = b.size.z * scale;
            s.TryVec3("at", out Vector3 shift);
            void Put(float x, float z, float yaw)
            {
                var local = new Vector3(x + shift.x, 0f, z + shift.z);
                Vector3 world = pose.position + pose.rotation * local;
                spots.Add(new Pose(new Vector3(world.x, shift.y, world.z), pose.rotation * Quaternion.Euler(0f, yaw + s.Num("rot_y", 0f), 0f)));
            }

            if (arrange == "rows")
            {
                string face = s.Str("face", "north");
                bool sideways = face == "east" || face == "west";
                float across = (sideways ? room.Size.z : room.Size.x) - 1.2f, along = (sideways ? room.Size.x : room.Size.z) - 1.6f;
                across = Mathf.Max(across, 0.5f); along = Mathf.Max(along, 0.5f);
                int cols = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(count * across / along)), 1, count);
                cols = Mathf.Min(cols, Mathf.Max(1, Mathf.FloorToInt(across / Mathf.Max(wide * 1.05f, 0.1f))));
                bool aisle = across >= 4f && cols >= 2;
                if (aisle) { across -= 1.1f; cols = Mathf.Max(2, cols - cols % 2); }
                int lines = Mathf.Min(Mathf.CeilToInt(count / (float)cols), Mathf.Max(1, Mathf.FloorToInt(along / Mathf.Max(deep * 1.05f, 0.1f))));
                float pitchX = Mathf.Min(across / cols, gap >= 0f ? wide + gap : 2.2f), pitchZ = Mathf.Min(along / lines, gap >= 0f ? deep + gap : 2.2f);
                Quaternion turn = Quaternion.Euler(0f, Yaw(face), 0f);
                for (int i = 0; i < Mathf.Min(count, cols * lines); i++)
                {
                    int c = i % cols, line = i / cols;
                    float x = (c - (cols - 1) * 0.5f) * pitchX;
                    if (aisle) x += x < 0f ? -0.55f : 0.55f;
                    // the first line is the one nearest what they all face
                    Vector3 at = turn * new Vector3(x, 0f, ((lines - 1) * 0.5f - line) * pitchZ);
                    Put(at.x, at.z, Yaw(face));
                }
                return spots;
            }

            if (arrange == "walls")
            {
                // every stretch of wall something can stand against: not the corners, not a doorway
                var runs = new List<(string side, float lo, float hi)>();
                float total = 0f;
                foreach (string side in new[] { "west", "north", "east", "south" })
                {
                    float width = (side == "north" || side == "south" ? room.Size.x : room.Size.z) - 0.6f - deep * 2f;
                    if (width < wide) continue;
                    if (room.OpenSides.Contains(side))
                    {
                        float part = width / 2f - 1f;
                        if (part >= wide) { runs.Add((side, -width / 2f, -1f)); runs.Add((side, 1f, width / 2f)); total += part * 2f; }
                    }
                    else { runs.Add((side, -width / 2f, width / 2f)); total += width; }
                }
                if (runs.Count == 0) return spots;
                float pitch = Mathf.Max(total / count, wide + (gap >= 0f ? gap : 0.06f));
                foreach (var run in runs)
                {
                    int fit = Mathf.FloorToInt((run.hi - run.lo + 0.001f) / pitch);
                    float spare = (run.hi - run.lo) - fit * pitch;
                    for (int k = 0; k < fit && spots.Count < count; k++)
                    {
                        float along = run.lo + spare / 2f + (k + 0.5f) * pitch;
                        float off = (run.side == "north" || run.side == "south" ? room.Size.z : room.Size.x) / 2f - deep / 2f - 0.14f;
                        Vector3 at = Quaternion.Euler(0f, Yaw(run.side), 0f) * new Vector3(along, 0f, off);
                        Put(at.x, at.z, Yaw(run.side) + 180f);   // its back to the wall
                    }
                }
                return spots;
            }

            // loose: anywhere on the floor that is clear of the others and of the ways in and out
            float spanX = Mathf.Max(room.Size.x / 2f - half - 0.3f, 0f), spanZ = Mathf.Max(room.Size.z / 2f - half - 0.3f, 0f);
            float apart = half * 2f + (gap >= 0f ? gap : Mathf.Min(half * 0.5f, 0.4f));
            var placed = new List<Vector2>();
            for (int i = 0; i < count; i++)
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    var at = new Vector2(((float)rng.NextDouble() * 2f - 1f) * spanX, ((float)rng.NextDouble() * 2f - 1f) * spanZ);
                    bool clear = true;
                    foreach (Vector2 other in placed) if ((other - at).sqrMagnitude < apart * apart) { clear = false; break; }
                    foreach (string side in room.OpenSides)
                    {
                        Vector3 door = Quaternion.Euler(0f, Yaw(side), 0f) * new Vector3(0f, 0f, (side == "north" || side == "south" ? room.Size.z : room.Size.x) / 2f);
                        if ((new Vector2(door.x, door.z) - at).magnitude < 1.3f + half) clear = false;
                    }
                    if (!clear) continue;
                    placed.Add(at);
                    Put(at.x, at.y, (float)rng.NextDouble() * 360f);
                    break;
                }
            return spots;
        }

        // The pitch the first rows of big things were laid at. Later rows of a like size fall on the
        // same grid, so the lanes between one statement's blocks line up with the next one's.
        float _rowPitch;

        /// <summary>
        /// Rows over open ground: a square grid with lanes between, squared up to the way the user
        /// faced: city blocks with streets to drive down, an orchard, a car park, graves, tents, an
        /// army. The user stands where two lanes cross. Nearest places first.
        /// </summary>
        List<Pose> InRows(Pose frame, AssetDef def, int count, float gap, float scale, string area, float rMin, float rMax, System.Random rng)
        {
            Bounds b = def.Bounds;
            float span = Mathf.Max(b.size.x, b.size.z) * scale;
            float pitch = span + (gap >= 0f ? gap : Mathf.Clamp(span * 0.8f, 1f, 14f));
            if (gap < 0f && span >= 3f)
            {
                if (_rowPitch > 0f && span <= _rowPitch - 3f && span >= _rowPitch * 0.3f) pitch = _rowPitch;
                else _rowPitch = pitch;
            }
            int reach = Mathf.Min(Mathf.CeilToInt(108f / pitch), 40);
            var cells = new List<Vector3>();
            for (int i = -reach; i < reach; i++)
                for (int j = -reach; j < reach; j++)
                {
                    var cell = new Vector3((i + 0.5f) * pitch, 0f, (j + 0.5f) * pitch);
                    float d = cell.magnitude;
                    if (d + span * 0.5f > 108f || d < rMin || d > rMax || d < World.SafeRadius + span * 0.5f) continue;
                    if ((area == "front" && cell.z < 0f) || (area == "behind" && cell.z > 0f) || (area == "left" && cell.x > 0f) || (area == "right" && cell.x < 0f)) continue;
                    if (area == "edges" && d < 60f) continue;
                    cells.Add(cell);
                }
            cells.Sort((p, q) => p.sqrMagnitude.CompareTo(q.sqrMagnitude));
            var spots = new List<Pose>();
            // more places than asked for are offered: some will turn out to be taken, or under water
            for (int k = 0; k < cells.Count && spots.Count < count * 2 + 8; k++)
                spots.Add(new Pose(Statement.InWorld(frame, cells[k]), frame.rotation * Quaternion.Euler(0f, rng.Next(4) * 90f, 0f)));
            return spots;
        }

        /// <summary>Fills a scatter. The same seed always gives the same first N positions, so raising the count only adds.</summary>
        void Populate(Group g, Statement s, AssetDef def, int count)
        {
            g.Items.Clear();
            foreach (GameObject extra in g.Extras) if (extra != null && (extra.name == "Solid" || extra.GetComponent<Creature>() != null)) UnityEngine.Object.Destroy(extra);
            g.Extras.RemoveAll(extra => extra == null || extra.name == "Solid" || extra.GetComponent<Creature>() != null);
            string area = s.Str("area", def.Airborne ? "sky" : "around");
            int seed = s.Has("seed") ? s.Int("seed") : (g.Id.GetHashCode() & 0x7FFFFFFF);
            var rng = new System.Random(seed);
            float footprint = Fit(def);
            Pose frame = s.Frame();   // scattered around where the user was standing when they asked

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
            bool sky = area == "sky" || def.Airborne;
            bool cover = g.Id.StartsWith(World.Cover, StringComparison.Ordinal);
            float body = Radius(def);   // how much ground one of them takes, at scale 1

            // Inside an interior a scatter belongs to a room: the one it names, or (said without one
            // by someone standing indoors, and not flung further than the room is big) the one they are in.
            StructureSystem.Room room = null;
            Pose roomPose = Pose.identity;
            if (s.Has("room")) _world.Structures.TryRoom(s.Str("room"), out room, out roomPose);
            else if (!sky && area != "edges" && !cover && !s.Has("along"))
            {
                // (a scatter with no reach given, or a long one, is the country outside: trees round a house)
                StructureSystem.Room here = _world.Structures.RoomAt(frame.position);
                bool close = here != null && s.TryRange("radius", out _, out float furthest) && furthest <= Mathf.Max(here.Size.x, here.Size.z);
                if (close) _world.Structures.TryRoom(here.Id, out room, out roomPose);
            }
            if (room != null) _world.Structures.TrackFurnishing(g.Id);
            float hMin = sky ? 10f : 0f, hMax = sky ? 40f : 0f;
            if (sky && !def.Airborne)
            {
                // Birds and clouds are made to be seen high and far off. A pig is not: up there it
                // is a speck. Whatever else is put in the sky stays near enough to be recognised.
                float big = Mathf.Max(footprint, 0.3f);
                rMin = Mathf.Clamp(big * 4f, 3f, 12f); rMax = Mathf.Clamp(big * 22f, 12f, 85f);
                hMin = Mathf.Clamp(big * 4f, 3f, 12f); hMax = Mathf.Clamp(big * 11f, 6f, 40f);
            }
            if (s.TryRange("radius", out float a, out float b)) { rMin = a; rMax = Mathf.Max(b, a + 0.01f); }
            float safe = World.SafeRadius + Mathf.Min(footprint, 6f);
            if (s.TryRange("height", out a, out b)) { hMin = a; hMax = b; }
            if (hMin < 0.5f && hMax < 0.5f) rMin = Mathf.Max(rMin, Mathf.Min(safe, rMax - 0.01f));
            // what is arranged (furniture in a room, blocks in rows) is all one size unless a range is asked for
            string arrange = s.Str("arrange", "loose");
            bool rows = arrange == "rows" && !sky;
            float sMin = room != null || rows ? 1f : 0.8f, sMax = room != null || rows ? 1f : 1.25f;
            if (s.TryRange("scale", out a, out b)) { sMin = a; sMax = b; }
            float gap = s.Has("gap") ? Mathf.Clamp(s.Num("gap"), 0f, 40f) : -1f;

            // Nothing is put where something already stands. Each copy claims a circle of ground, and
            // so do the big things other statements put down (a tower from one scatter, an office
            // block from the next): asked for a city, the model used to get buildings inside each other.
            var taken = new List<Vector4>();
            List<Vector4> standing = cover || sky ? null : Standing(g);
            float crowd = Mathf.Clamp(s.Int("clusters", 0), 0, 24) > 0 ? 0.7f : body < 0.5f ? 0.95f : 1.05f;
            bool Free(Vector3 at, float radius, bool amongItsOwn = true)
            {
                if (amongItsOwn) foreach (Vector4 t in taken)
                {
                    float dx = at.x - t.x, dz = at.z - t.y, need = (radius + t.z) * crowd;
                    if (dx * dx + dz * dz < need * need) return false;
                }
                if (standing != null && radius >= 0.5f)
                    foreach (Vector4 t in standing)
                    {
                        float dx = at.x - t.x, dz = at.z - t.y, need = radius + t.z;
                        if (dx * dx + dz * dz < need * need) return false;
                    }
                // and nothing meant for outdoors is put down inside the walls of an interior
                return room != null || float.IsNaN(_world.Structures.FloorAt(at));
            }

            float waterY = _world.Terrain.WaterY;
            bool hasWater = !float.IsNaN(waterY);

            // Three ways a scatter stops looking like confetti thrown at a map:
            //   `along`    it follows a line: a fence, an avenue of trees, lamps down a street
            //   `clusters` it gathers: groves, herds, a boulder field, clumps of flowers
            //   `where`    it keeps to the ground that suits it: reeds on the shore, pines up high
            bool line = s.TryPath("along", out List<Vector3> route);
            float length = 0f;
            if (line) for (int k = 1; k < route.Count; k++) length += Flat(route[k] - route[k - 1]).magnitude;
            line &= length > 0.01f;
            line &= room == null;
            // a line holds only as many as fit along it side by side
            // (turned to run with the line, as they are unless told otherwise, it is their length that counts)
            float alongLine = s.Flag("align", line) ? def.Bounds.size.z : Mathf.Max(def.Bounds.size.x, def.Bounds.size.z);
            if (line && !cover) count = Mathf.Min(count, Mathf.Max(1, Mathf.FloorToInt(length / Mathf.Max(alongLine * (sMin + sMax) * 0.5f * 1.05f, 0.05f))));
            float spread = s.Has("spread") ? Mathf.Clamp(s.Num("spread"), 0f, 40f) : -1f;
            bool align = s.Flag("align", line);
            string where = sky ? "any" : s.Str("where", "any");
            if (where == "shore" && !hasWater) where = "low";
            _world.Terrain.Span(out float lowest, out float highest);
            int tries = where == "any" ? 8 : 30;

            int clusters = line ? 0 : Mathf.Clamp(s.Int("clusters", 0), 0, 24);
            var centres = new List<Vector3>();
            float clump = spread >= 0f ? Mathf.Max(spread, 0.5f) : Mathf.Clamp(footprint * 3f + count / Mathf.Max(clusters, 1) * 0.35f, 3f, 14f);

            // one place for the scatter's own shape to offer: somewhere in its area
            Vector3 Offer(int i)
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
                return Statement.InWorld(frame, new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius));
            }

            for (int c = 0; c < clusters; c++)
            {
                Vector3 centre = Vector3.zero;
                for (int attempt = 0; attempt < tries; attempt++)
                {
                    centre = Offer(c);
                    if (Suits(centre, where, def, hasWater, waterY, lowest, highest, sky)) break;
                }
                centres.Add(centre);
            }

            // Arranged: every place is worked out first, in a room or in rows over open ground.
            List<Pose> spots = null;
            if (room != null) spots = InRoom(room, roomPose, s, def, count, arrange, gap, (sMin + sMax) * 0.5f, rng);
            else if (rows) spots = InRows(frame, def, count, gap, (sMin + sMax) * 0.5f, area, s.TryRange("radius", out _, out _) ? rMin : 0f, s.TryRange("radius", out _, out _) ? rMax : 108f, rng);
            int wanted = count;
            if (spots != null) count = spots.Count;
            bool sizeable = body * sMax >= 0.6f && !cover;
            if (sizeable && !line && clusters == 0) tries = Mathf.Max(tries, 30);

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = Vector3.zero;
                float yaw = (float)rng.NextDouble() * 360f, lift = 0f;
                float scale = Mathf.Lerp(sMin, sMax, (float)rng.NextDouble());
                float claim = body * scale;
                Quaternion? turned = null;
                bool found = false;
                if (spots != null)
                {
                    pos = spots[i].position; lift = pos.y; turned = spots[i].rotation;
                    // a place in an arrangement is not given up to the copy before it, only to what stood there already
                    found = room != null || ((standing == null || Free(pos, claim)) && Suits(pos, where, def, hasWater, waterY, lowest, highest, sky));
                    if (!found) continue;
                }
                for (int attempt = 0; attempt < tries && !found; attempt++)
                {
                    if (line)
                    {
                        // evenly spaced down the line, each nudged a little so it is not ruled
                        float along = (i + 0.5f + (attempt == 0 ? 0f : ((float)rng.NextDouble() - 0.5f))) / count * length;
                        Vector3 on = OnRoute(route, along, out Vector3 heading);
                        Vector3 side = new Vector3(heading.z, 0f, -heading.x);
                        pos = on + side * (((float)rng.NextDouble() * 2f - 1f) * Mathf.Max(spread, 0f));
                        lift = on.y;
                        if (align && heading.sqrMagnitude > 0.0001f) yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
                    }
                    else if (clusters > 0)
                    {
                        // thick in the middle of the clump, thinning outwards
                        float turn = (float)rng.NextDouble() * Mathf.PI * 2f, reach = clump * Mathf.Pow((float)rng.NextDouble(), 0.8f);
                        pos = centres[i % clusters] + new Vector3(Mathf.Sin(turn) * reach, 0f, Mathf.Cos(turn) * reach);
                        Vector3 fromUser = pos - frame.position;
                        if (hMin < 0.5f && hMax < 0.5f && Flat(fromUser).magnitude < safe) continue;   // never on top of the user
                    }
                    else
                    {
                        pos = Offer(i);
                        // crowded out near the middle, a big thing looks further off before it gives up
                        if (sizeable && attempt >= tries / 2)
                        {
                            Vector3 outward = pos - frame.position;
                            pos = frame.position + outward * Mathf.Min(1f + 0.06f * (attempt - tries / 2 + 1), 108f / Mathf.Max(outward.magnitude, 1f));
                        }
                    }
                    // down a line they are spaced by the line itself, and only kept off what else stands there
                    found = Suits(pos, where, def, hasWater, waterY, lowest, highest, sky) && (cover || sky || Free(pos, line ? Mathf.Min(claim, alongLine * scale * 0.5f) : claim, !line));
                }
                // with nowhere suitable found, a thing asked to keep to one kind of ground is left out,
                // and so is anything of a size to be seen standing inside its neighbour: fewer, never overlapping
                if (!found && (where != "any" || sizeable)) continue;
                if (taken.Count >= wanted) break;
                taken.Add(new Vector4(pos.x, pos.z, claim, 0f));
                pos.y = lift + (spots != null ? 0f : Mathf.Lerp(hMin, hMax, (float)rng.NextDouble()));
                if (sky)
                {
                    // Overhead, something too big for how close it is stops being a shape and becomes
                    // the ceiling: it is kept to a size the eye can take in, and clear of the user's head.
                    Vector3 reach = def.Bounds.size;
                    float span = Mathf.Max(reach.x, Mathf.Max(reach.y, reach.z), 0.01f);
                    Vector3 off = pos - frame.position;
                    float away = Mathf.Sqrt(off.x * off.x + off.z * off.z + (pos.y - 1.6f) * (pos.y - 1.6f));
                    scale = Mathf.Min(scale, Mathf.Max(away, 3f) * 0.8f / span);
                    pos.y = Mathf.Max(pos.y, 2.6f + def.Bounds.extents.y * scale);
                }
                if (Enliven(g, def, pos, scale, s.Str("behavior"))) continue;
                var p = new Placement(def.Mesh, pos, yaw, scale, Color.white) { OnWater = def.OnWater };
                if (turned.HasValue) p.Rotation = turned.Value;
                // no two of a kind are quite the same height: a skyline, a wood, a crowd
                if (def.Stretch > 0f) p.Scale = new Vector3(scale, scale * (1f + def.Stretch * ((float)rng.NextDouble() * 2f - 1f)), scale);
                if (s.Flag("solid") && !sky) Solidify(g, def, pos, p.Rotation, p.Scale);
                g.Items.Add(p);
            }
            g.Dirty = true;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>The point a distance down a line of points, and which way the line runs there. Heights carry through in y.</summary>
        static Vector3 OnRoute(List<Vector3> route, float distance, out Vector3 heading)
        {
            heading = Vector3.forward;
            for (int k = 1; k < route.Count; k++)
            {
                Vector3 leg = route[k] - route[k - 1];
                float run = Flat(leg).magnitude;
                if (run < 0.0001f) continue;
                heading = Flat(leg) / run;
                if (distance <= run || k == route.Count - 1) return route[k - 1] + leg * Mathf.Clamp01(distance / run);
                distance -= run;
            }
            return route[route.Count - 1];
        }

        /// <summary>
        /// Is this a place the thing would stand? Floating things want water and everything else
        /// wants land, as always; `where` narrows it to a kind of ground: `flat`, `steep`, `high`,
        /// `low`, or the `shore` (the strip of land along the water's edge).
        /// </summary>
        bool Suits(Vector3 pos, string where, AssetDef def, bool hasWater, float waterY, float lowest, float highest, bool sky)
        {
            if (sky) return true;
            TerrainSystem ground = _world.Terrain;
            float h = ground.HeightAt(pos.x, pos.z);
            if (hasWater && (h < waterY + 0.15f) != def.OnWater) return false;
            if (where == "any" || def.OnWater) return true;
            switch (where)
            {
                case "shore": return h < waterY + 1.3f;
                case "high": return h > Mathf.Lerp(lowest, highest, 0.55f) && highest - lowest > 2f;
                case "low": return h < Mathf.Lerp(Mathf.Max(lowest, hasWater ? waterY : lowest), highest, 0.3f) || highest - lowest <= 2f;
            }
            const float e = 1.5f;
            float dx = ground.HeightAt(pos.x + e, pos.z) - ground.HeightAt(pos.x - e, pos.z), dz = ground.HeightAt(pos.x, pos.z + e) - ground.HeightAt(pos.x, pos.z - e);
            float slope = Mathf.Sqrt(dx * dx + dz * dz) / (2f * e);
            return where == "steep" ? slope > 0.3f : where != "flat" || slope < 0.12f;
        }

        public void Modify(Statement s)
        {
            foreach (Group g in Matching(s.Str("id")))
            {
                if (s.TryColor("tint", out Color tint)) g.Tint = tint;
                if (s.Has("scale")) { g.Scale = Mathf.Clamp(s.Num("scale", 1f), 0.05f, 60f); g.Dirty = true; }
                if (s.Has("motion")) g.Motion = MotionFor(s.Str("motion"), null);
                if (s.Has("behavior")) foreach (GameObject extra in g.Extras) { Creature c = extra != null ? extra.GetComponent<Creature>() : null; if (c != null) c.SetBehavior(s.Str("behavior")); }
                if (s.Has("tint")) foreach (GameObject extra in g.Extras) { Creature c = extra != null ? extra.GetComponent<Creature>() : null; if (c != null) c.SetTint(g.Tint); }
                if (s.Has("count") && g.Kind == "scatter" && g.Source != null)
                {
                    AssetDef def = Library.FindAsset(g.Source.Str("asset", "cube")) ?? Library.Asset("cube");
                    int room = MaxScatterTotal - ScatterTotal() + g.Items.Count;
                    int count = Mathf.Clamp(s.Int("count"), 0, Mathf.Min(MaxScatterCount, room));
                    if (count == 0) { Retire(g, s.T(0.8f)); continue; }
                    Populate(g, g.Source, def, count);
                }
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
            _rowPitch = 0f;
            foreach (Group g in new List<Group>(_order)) Retire(g, seconds);
        }

        /// <summary>The ground changed: everything standing on it settles onto the new heights.</summary>
        public void Resettle()
        {
            foreach (Group g in _order) g.Dirty = true;
        }

        // ------------------------------------------------------------------ drawing
        //
        // Still scenery is combined, a group at a time, into ordinary meshes on ordinary
        // MeshRenderers: one for each quarter of the world the group reaches into, so that what is
        // behind the user is culled whole. A 300-tree forest is four draw calls.
        //
        // It was GPU-instanced once (Graphics.RenderMeshInstanced). That drew in the editor and
        // drew NOTHING in the headset build, for the whole of the first day: the build strips the
        // instancing variants of shaders that only runtime-made materials use. Creatures and props,
        // which were MeshRenderers, were all anyone ever saw. Do not go back to instancing without
        // checking a real build (see README, "Check a build, not the editor").

        sealed class Bucket
        {
            public readonly List<CombineInstance> Parts = new List<CombineInstance>();
            public readonly List<Vector4> Pivots = new List<Vector4>();
            public int Vertices, Triangles;
        }

        static readonly Dictionary<long, Mesh> Singles = new Dictionary<long, Mesh>();
        readonly Dictionary<int, Bucket> _buckets = new Dictionary<int, Bucket>();
        readonly List<Matrix4x4> _shadowScratch = new List<Matrix4x4>();

        /// <summary>One submesh of a library mesh as a mesh of its own, so that combining copies exactly its vertices.</summary>
        static Mesh Single(Mesh mesh, int sub)
        {
            if (mesh.subMeshCount == 1) return mesh;
            long key = ((long)mesh.GetInstanceID() << 4) | (uint)sub;
            if (Singles.TryGetValue(key, out Mesh single) && single != null) return single;
            int[] indices = mesh.GetIndices(sub);
            Vector3[] verts = mesh.vertices, normals = mesh.normals;
            Color[] colors = mesh.colors;
            var uv0 = new List<Vector2>(); var uv1 = new List<Vector2>();
            mesh.GetUVs(0, uv0); mesh.GetUVs(1, uv1);
            var map = new Dictionary<int, int>();
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var a = new List<Vector2>(); var b = new List<Vector2>();
            var tris = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int old = indices[i];
                if (!map.TryGetValue(old, out int now))
                {
                    now = v.Count; map[old] = now;
                    v.Add(verts[old]); n.Add(old < normals.Length ? normals[old] : Vector3.up); c.Add(old < colors.Length ? colors[old] : Color.white);
                    a.Add(old < uv0.Count ? uv0[old] : Vector2.zero); b.Add(old < uv1.Count ? uv1[old] : Vector2.zero);
                }
                tris[i] = now;
            }
            single = new Mesh { name = mesh.name + "_" + sub };
            if (v.Count > 65000) single.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            single.SetVertices(v); single.SetNormals(n); single.SetColors(c); single.SetUVs(0, a); single.SetUVs(1, b);
            single.SetTriangles(tris, 0, true);
            Singles[key] = single;
            return single;
        }

        void Discard(Group g)
        {
            foreach (MeshRenderer r in g.Renderers) if (r != null) UnityEngine.Object.Destroy(r.gameObject);
            if (g.Shadow != null) UnityEngine.Object.Destroy(g.Shadow.gameObject);
            foreach (Mesh m in g.Combined) if (m != null) UnityEngine.Object.Destroy(m);
            g.Renderers.Clear();
            g.Combined.Clear();
            g.Shadow = null;
            g.Drawn = g.Triangles = 0;
            g.ShownAppear = -1f;
        }

        MeshRenderer Chunk(Group g, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(g.Root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            g.Combined.Add(mesh);
            return r;
        }

        void Rebuild(Group g)
        {
            g.Dirty = false;
            Discard(g);
            if (g.Items.Count == 0) return;
            if (g.Root == null)
            {
                g.Root = new GameObject("Group_" + g.Id);
                g.Root.transform.SetParent(_root, false);
            }
            _buckets.Clear();
            _shadowScratch.Clear();
            bool moving = g.Motion.x != 0f;
            float waterY = _world.Terrain.WaterY;
            bool wild = g.Id.StartsWith(World.Cover, StringComparison.Ordinal);
            foreach (Placement p in g.Items)
            {
                if (p.Mesh == null) continue;
                Vector3 pos = p.Position;
                // what grows on the ground by itself does not grow up through the floor of an interior
                if (wild && !float.IsNaN(_world.Structures.FloorAt(pos))) continue;
                if (p.Grounded)
                {
                    float ground = _world.SurfaceHeight(pos.x, pos.z);
                    if (p.OnWater && !float.IsNaN(waterY) && ground < waterY) ground = waterY;
                    pos.y += ground;
                }
                Vector3 scale = p.Scale * g.Scale;
                Matrix4x4 matrix = Matrix4x4.TRS(pos, p.Rotation, scale);
                // split by quarter so the part of a group behind the user is culled as a whole
                int sector = moving ? 0 : (pos.x >= 0f ? 1 : 0) | (pos.z >= 0f ? 2 : 0);
                var pivot = new Vector4(pos.x, pos.y, pos.z, Mathf.Max(scale.x, scale.y, scale.z));
                for (int sub = 0; sub < p.Mesh.subMeshCount && sub < 2; sub++)
                {
                    int key = sector * 2 + sub;
                    if (!_buckets.TryGetValue(key, out Bucket bucket)) _buckets[key] = bucket = new Bucket();
                    Mesh single = Single(p.Mesh, sub);
                    int count = single.vertexCount;
                    if (count == 0) continue;
                    bucket.Parts.Add(new CombineInstance { mesh = single, subMeshIndex = 0, transform = matrix });
                    for (int i = 0; i < count; i++) bucket.Pivots.Add(pivot);
                    bucket.Vertices += count;
                    bucket.Triangles += (int)(single.GetIndexCount(0) / 3);
                }
                g.Drawn++;

                // whatever stands on the ground and stays put gets a shadow there
                if (p.Grounded && !moving && g.Motion.y == 0f && Shadows.Wanted(p.Mesh.bounds, scale, p.Position.y, out Vector2 half))
                {
                    float floor = p.OnWater && !float.IsNaN(waterY) ? waterY : float.NegativeInfinity;
                    var yaw = Quaternion.Euler(0f, p.Rotation.eulerAngles.y, 0f);
                    Vector3 centre = pos + p.Rotation * Vector3.Scale(p.Mesh.bounds.center, scale);
                    _shadowScratch.Add(Shadows.Matrix(_world.Terrain, centre.x, centre.z, floor, yaw, half, 1f));
                }
            }

            foreach (KeyValuePair<int, Bucket> pair in _buckets)
            {
                Bucket bucket = pair.Value;
                if (bucket.Parts.Count == 0) continue;
                var mesh = new Mesh { name = g.Id + "_" + pair.Key };
                if (bucket.Vertices > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(bucket.Parts.ToArray(), true, true);
                if (mesh.vertexCount != bucket.Pivots.Count)
                {
                    // must not happen; if it ever does, say so loudly and draw it unanimated rather than not at all
                    Debug.LogError("ImagineVR: combined " + g.Id + " has " + mesh.vertexCount + " vertices, expected " + bucket.Pivots.Count);
                    bucket.Pivots.Clear();
                    Vector3[] verts = mesh.vertices;
                    foreach (Vector3 vert in verts) bucket.Pivots.Add(new Vector4(vert.x, vert.y, vert.z, 1f));
                }
                mesh.SetUVs(2, bucket.Pivots);
                Bounds bounds = mesh.bounds;
                bounds.Expand(pair.Key % 2 == 1 ? 4f : 2f);   // wind sway, and halos wider than their points
                mesh.bounds = moving || g.Motion.w != 0f ? new Bounds(Vector3.zero, Vector3.one * 2000f) : bounds;
                mesh.UploadMeshData(!Application.isEditor);   // the headset keeps no second copy; the editor does, for the tests to read
                g.Renderers.Add(Chunk(g, pair.Key % 2 == 0 ? "Solid" : "Glow", mesh, pair.Key % 2 == 0 ? _litBaked : _glowBaked));
                g.Triangles += bucket.Triangles;
            }
            if (_shadowScratch.Count > 0)
            {
                Mesh shadows = Shadows.Bake(_shadowScratch, null);
                g.Shadow = Chunk(g, "Shadows", shadows, Shadows.Material);
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
                    Discard(g);
                    if (g.Root != null) UnityEngine.Object.Destroy(g.Root);
                    _dying.RemoveAt(i);
                }
            }
        }

        static void Step(Group g, float dt)
        {
            g.Appear = Mathf.MoveTowards(g.Appear, g.AppearTarget, dt / g.AppearSeconds);
        }

        /// <summary>How many renderers, items and triangles are being drawn right now: for the session log.</summary>
        public void Census(out int renderers, out int items, out int triangles)
        {
            renderers = items = triangles = 0;
            foreach (Group g in _order) { renderers += g.Renderers.Count; items += g.Drawn; triangles += g.Triangles; }
        }

        public void Render()
        {
            // a change of ground re-settles everything; spread that over frames so it is never one long stall
            int budget = 600;   // items combined this frame; the first group always goes, however big
            foreach (Group g in _order)
            {
                if (g.Dirty && budget > 0) { budget -= Mathf.Max(g.Items.Count, 1); Rebuild(g); }
                Show(g);
            }
            foreach (Group g in _dying) Show(g);
        }

        static readonly int ShadowStrengthId = Shader.PropertyToID("_Strength");

        void Show(Group g)
        {
            if (g.Renderers.Count == 0 && g.Shadow == null) return;
            if (g.Appear == g.ShownAppear && g.Tint == g.ShownTint && g.Motion == g.ShownMotion) return;
            g.ShownAppear = g.Appear; g.ShownTint = g.Tint; g.ShownMotion = g.Motion;
            bool visible = g.Appear > 0.0001f;
            g.Props.SetFloat(AppearId, g.Appear);
            g.Props.SetColor(TintId, g.Tint);
            g.Props.SetVector(MotionId, g.Motion);
            g.Props.SetFloat(SwayId, g.Sway);
            foreach (MeshRenderer r in g.Renderers)
            {
                r.enabled = visible;
                r.SetPropertyBlock(g.Props);
            }
            if (g.Shadow != null)
            {
                // shadows arrive last, once the things casting them have finished growing
                float shade = Mathf.Clamp01(g.Appear * 3f - 2f);
                g.Shadow.enabled = shade > 0.001f;
                g.Props.SetFloat(ShadowStrengthId, shade);
                g.Shadow.SetPropertyBlock(g.Props);
            }
        }
    }
}
