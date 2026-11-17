using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// `road`: streets, highways, lanes, dirt tracks, race tracks, runways, rails, paths. A centreline
    /// (drawn as points, or a shape: straight, loop, oval, ring, figure8, cross, grid) becomes a
    /// ribbon over the ground with its markings, kerbs and sidewalks; and on request it is lit, lined
    /// with buildings that face it, driven by traffic that stops for what is ahead, walked by people,
    /// and timed as a circuit (the counters lap, lap_time and best_lap, and the event `lap`).
    /// </summary>
    public sealed class RoadSystem
    {
        sealed class Road
        {
            public string Id, Kind;
            public float Width;
            public readonly List<List<Vector3>> Lines = new List<List<Vector3>>();
            public readonly List<bool> Loops = new List<bool>();
            public GameObject Root;
            public readonly List<string> Groups = new List<string>();
            // a circuit
            public List<Vector3> Gates;
            public int Next = 1;
            public float LapStarted = -1f, Best = -1f;
            // a grid of streets, for building its blocks
            public bool IsGrid;
            public Vector3 GridOrigin;
            public Quaternion GridTurn = Quaternion.identity;
            public float GridX0, GridZ0, GridBX, GridBZ;
            public int GridNX, GridNZ;
        }

        readonly World _world;
        readonly Dictionary<string, Road> _roads = new Dictionary<string, Road>();
        bool _hasStart;
        Vector3 _start;
        float _startYaw;

        public RoadSystem(World world) { _world = world; }

        static float WidthOf(string kind)
        {
            switch (kind)
            {
                case "highway": return 14f;
                case "lane": case "dirt": return 5f;
                case "track": return 12f;
                case "runway": return 22f;
                case "rail": return 2.2f;
                case "path": return 2.5f;
                default: return 8f;
            }
        }

        /// <summary>A ride written in the same turn as its road starts on the road; one written in a later turn does not.</summary>
        public void EndTurn() { _hasStart = false; }

        /// <summary>Where a ride begins after a road is laid: on it, facing along it. Taken once.</summary>
        public bool TakeStart(out Vector3 at, out float yaw)
        {
            at = _start;
            yaw = _startYaw;
            bool had = _hasStart;
            _hasStart = false;
            return had;
        }

        public void Build(Statement s)
        {
            string id = s.Str("id", "road");
            Remove(id);
            string kind = s.Str("kind", "street");
            var road = new Road { Id = id, Kind = kind, Width = Mathf.Clamp(s.Num("width", WidthOf(kind)), 1.5f, 40f) };
            Pose frame = s.Frame();
            Centrelines(s, frame, road);
            if (road.Lines.Count == 0) return;
            _roads[id] = road;
            road.Root = new GameObject("Road_" + id);
            road.Root.transform.SetParent(_world.transform, false);
            float raised = s.Num("height", 0f);
            Draw(road, s, raised);
            Color tint = s.TryColor("tint", out Color t) ? t : Color.white;

            // where a ride starts: the start of the first line, in its right-hand lane, facing along it
            List<Vector3> first = road.Lines[0];
            Vector3 along = (first[Mathf.Min(1, first.Count - 1)] - first[0]);
            along.y = 0f;
            if (along.sqrMagnitude > 0.001f)
            {
                along.Normalize();
                Vector3 rightSide = Vector3.Cross(Vector3.up, along);
                int nearest = 0;
                float best = float.MaxValue;
                Vector3 user = _world.Head != null ? _world.Head.position : Vector3.zero;
                for (int i = 0; i < first.Count; i++) { float d = (first[i] - user).sqrMagnitude; if (d < best) { best = d; nearest = i; } }
                if (kind == "track" || kind == "runway" || road.Loops[0]) nearest = 0;
                Vector3 a = first[nearest], b = first[Mathf.Min(nearest + 1, first.Count - 1)];
                Vector3 dir = b - a;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) dir = along;
                dir.Normalize();
                _start = a + Vector3.Cross(Vector3.up, dir) * (kind == "runway" || kind == "rail" ? 0f : road.Width * 0.25f);
                _start.y = _world.SurfaceHeight(_start.x, _start.z) + raised;
                _startYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                _hasStart = true;
            }

            if (s.Flag("lamps")) Lamps(road);
            Dictionary<string, object> buildings = s.Obj("buildings");
            if (buildings != null) Buildings(road, Statement.Wrap(buildings), s);
            Dictionary<string, object> traffic = s.Obj("traffic");
            if (traffic != null) Traffic(road, Statement.Wrap(traffic));
            Dictionary<string, object> walkers = s.Obj("walkers");
            if (walkers != null) Walkers(road, Statement.Wrap(walkers));
            int gates = s.Int("checkpoints", 0);
            if (gates > 0 && road.Loops[0]) Circuit(road, gates);
            SessionLog.Write("road", "id", id, "kind", kind, "lines", road.Lines.Count, "points", road.Lines[0].Count, "loop", road.Loops[0]);
        }

        // ------------------------------------------------------------------ the line it follows

        void Centrelines(Statement s, Pose frame, Road road)
        {
            if (s.TryPath("points", out List<Vector3> written))
            {
                bool loop = s.Flag("loop");
                road.Lines.Add(Smooth(written, loop));
                road.Loops.Add(loop);
                return;
            }
            string shape = s.Str("shape", "straight");
            Vector3 centre = Statement.InWorld(frame, s.Vec("at", Vector3.zero));
            Quaternion turn = frame.rotation * Quaternion.Euler(0f, s.Num("rot_y", 0f), 0f);
            Vector3 size = s.TryVec3("size", out Vector3 sz) ? sz : Vector3.zero;
            float w = size.x > 0f ? size.x : s.Num("radius", 60f) * 2f, d = size.y > 0f ? size.y : w;
            if (s.Fields.TryGetValue("size", out object raw) && raw is List<object> two && two.Count == 2) { w = System.Convert.ToSingle(two[0]); d = System.Convert.ToSingle(two[1]); }
            var pts = new List<Vector3>();
            switch (shape)
            {
                case "loop":
                case "oval":
                {
                    // a stadium: two straights joined by half circles; it passes the point it is placed at, along +X
                    float r = Mathf.Min(w, d) * 0.5f, straight = Mathf.Max(w, d) - 2f * r;
                    int arc = 24;
                    for (int i = 0; i <= arc; i++) { float a = Mathf.PI * i / arc - Mathf.PI * 0.5f; pts.Add(new Vector3(straight * 0.5f + Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)); }
                    for (int i = 0; i <= arc; i++) { float a = Mathf.PI * i / arc + Mathf.PI * 0.5f; pts.Add(new Vector3(-straight * 0.5f + Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)); }
                    // the start line, where the ride begins, is the middle of the near straight
                    int half = pts.Count / 4;
                    var rotated = new List<Vector3>(pts.GetRange(half, pts.Count - half));
                    rotated.AddRange(pts.GetRange(0, half));
                    pts = rotated;
                    AddLine(road, pts, centre + turn * new Vector3(0f, 0f, r), turn * Quaternion.Euler(0f, 180f, 0f), true, w >= d);
                    return;
                }
                case "ring":
                {
                    float r = s.Num("radius", w * 0.5f);
                    for (int i = 0; i < 48; i++) { float a = Mathf.PI * 2f * i / 48f; pts.Add(new Vector3(Mathf.Sin(a) * r, 0f, -Mathf.Cos(a) * r + r)); }
                    AddLine(road, pts, centre, turn, true, true);
                    return;
                }
                case "figure8":
                {
                    float a = Mathf.Max(w, 40f) * 0.5f;
                    for (int i = 0; i < 64; i++) { float t = Mathf.PI * 2f * i / 64f; float k = 1f + Mathf.Sin(t) * Mathf.Sin(t); pts.Add(new Vector3(a * Mathf.Cos(t) / k, 0f, a * Mathf.Sin(t) * Mathf.Cos(t) / k)); }
                    AddLine(road, pts, centre, turn, true, true);
                    return;
                }
                case "cross":
                {
                    float l = s.Num("length", Mathf.Max(w, 120f));
                    AddLine(road, Straight(l), centre, turn, false, true);
                    AddLine(road, Straight(l), centre, turn * Quaternion.Euler(0f, 90f, 0f), false, true);
                    return;
                }
                case "grid":
                {
                    int nx = 3, nz = 3;
                    float bx = 60f, bz = 45f;
                    if (s.Fields.TryGetValue("blocks", out object bl) && bl is List<object> bb && bb.Count == 2) { nx = Mathf.Clamp(System.Convert.ToInt32(bb[0]), 1, 8); nz = Mathf.Clamp(System.Convert.ToInt32(bb[1]), 1, 8); }
                    if (s.Fields.TryGetValue("block", out object bk) && bk is List<object> bs && bs.Count == 2) { bx = Mathf.Clamp(System.Convert.ToSingle(bs[0]), 16f, 200f); bz = Mathf.Clamp(System.Convert.ToSingle(bs[1]), 16f, 200f); }
                    // a street runs past the user's feet whichever way: the grid starts one block behind them
                    float x0 = -Mathf.Floor(nx * 0.5f) * bx, z0 = -bz;
                    float spanX = nx * bx, spanZ = nz * bz;
                    for (int i = 0; i <= nx; i++) AddLine(road, new List<Vector3> { new Vector3(x0 + i * bx, 0f, z0), new Vector3(x0 + i * bx, 0f, z0 + spanZ) }, centre, turn, false, false, 4f);
                    for (int j = 0; j <= nz; j++) AddLine(road, new List<Vector3> { new Vector3(x0, 0f, z0 + j * bz), new Vector3(x0 + spanX, 0f, z0 + j * bz) }, centre, turn, false, false, 4f);
                    road.GridOrigin = centre; road.GridTurn = turn; road.GridX0 = x0; road.GridZ0 = z0; road.GridBX = bx; road.GridBZ = bz; road.GridNX = nx; road.GridNZ = nz; road.IsGrid = true;
                    return;
                }
                default:
                {
                    float l = s.Num("length", 120f);
                    AddLine(road, Straight(l), centre, turn, false, true);
                    return;
                }
            }
        }

        static List<Vector3> Straight(float length)
        {
            var pts = new List<Vector3>();
            for (float z = -length * 0.5f; z <= length * 0.5f + 0.01f; z += Mathf.Max(length / 40f, 4f)) pts.Add(new Vector3(0f, 0f, z));
            return pts;
        }

        void AddLine(Road road, List<Vector3> local, Vector3 origin, Quaternion turn, bool loop, bool subdivide, float step = 4f)
        {
            var world = new List<Vector3>(local.Count);
            foreach (Vector3 p in local) world.Add(origin + turn * p);
            road.Lines.Add(subdivide ? world : Subdivide(world, step));
            road.Loops.Add(loop);
        }

        static List<Vector3> Subdivide(List<Vector3> line, float step)
        {
            var result = new List<Vector3>();
            for (int i = 0; i + 1 < line.Count; i++)
            {
                float d = Vector3.Distance(line[i], line[i + 1]);
                int n = Mathf.Max(1, Mathf.CeilToInt(d / step));
                for (int k = 0; k < n; k++) result.Add(Vector3.Lerp(line[i], line[i + 1], k / (float)n));
            }
            result.Add(line[line.Count - 1]);
            return result;
        }

        /// <summary>A line through the points the model wrote, rounded at the corners (Catmull-Rom).</summary>
        static List<Vector3> Smooth(List<Vector3> pts, bool loop)
        {
            var result = new List<Vector3>();
            int n = pts.Count;
            int segments = loop ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector3 p0 = pts[loop ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[loop ? (i + 2) % n : Mathf.Min(i + 2, n - 1)];
                int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(p1, p2) / 4f), 2, 30);
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            if (!loop) result.Add(pts[n - 1]);
            return result;
        }

        // ------------------------------------------------------------------ drawing it

        void Draw(Road road, Statement s, float raised)
        {
            var mb = new MeshBuilder();
            string kind = road.Kind;
            Color surface = kind == "dirt" ? new Color(0.55f, 0.42f, 0.3f) : kind == "path" ? new Color(0.72f, 0.66f, 0.55f) : kind == "runway" ? new Color(0.3f, 0.31f, 0.33f)
                : kind == "rail" ? new Color(0.42f, 0.36f, 0.3f) : kind == "bridge" ? Pal.Concrete : Pal.Asphalt;
            if (s.TryColor("tint", out Color tint)) surface = tint;
            bool markings = s.Has("markings") ? s.Flag("markings", true) : kind != "dirt" && kind != "path" && kind != "rail";
            bool walks = s.Has("sidewalks") ? s.Flag("sidewalks") : kind == "street";
            bool kerbs = kind == "track";
            bool barriers = s.Flag("barriers");
            float half = road.Width * 0.5f;
            for (int li = 0; li < road.Lines.Count; li++)
            {
                List<Vector3> line = road.Lines[li];
                bool loop = road.Loops[li];
                int count = loop ? line.Count + 1 : line.Count;
                Vector3 prevL = default, prevR = default, prevC = default;
                float dist = 0f;
                for (int k = 0; k < count; k++)
                {
                    Vector3 c = line[k % line.Count];
                    Vector3 next = line[loop ? (k + 1) % line.Count : Mathf.Min(k + 1, line.Count - 1)], prev = line[loop ? (k - 1 + line.Count) % line.Count : Mathf.Max(k - 1, 0)];
                    Vector3 dir = next - prev;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
                    dir.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    Vector3 L = c - side * half, R = c + side * half;
                    L.y = Ground(L) + 0.04f + raised; R.y = Ground(R) + 0.04f + raised; c.y = Ground(c) + 0.04f + raised;
                    if (k > 0)
                    {
                        float seg = Vector3.Distance(prevC, c);
                        mb.Col(surface).Quad(prevL, prevR, R, L, Vector3.up);
                        if (markings && kind != "runway")
                        {
                            // dashes down the middle: yellow on a highway, white elsewhere
                            float from = dist, to = dist + seg;
                            for (float m = Mathf.Ceil(from / 6f) * 6f; m < to; m += 6f)
                            {
                                float a = (m - from) / Mathf.Max(seg, 0.01f), b = Mathf.Min(1f, (m + 3f - from) / Mathf.Max(seg, 0.01f));
                                Mark(mb, Vector3.Lerp(prevC, c, a), Vector3.Lerp(prevC, c, b), side, 0.12f, kind == "highway" ? Pal.Yellow : Pal.White);
                            }
                            Mark(mb, prevL + side * 0.35f, L + side * 0.35f, side, 0.1f, Pal.White);
                            Mark(mb, prevR - side * 0.35f, R - side * 0.35f, side, 0.1f, Pal.White);
                        }
                        if (kind == "runway") { Mark(mb, prevC, c, side, 0.5f, Pal.White); Mark(mb, prevL + side * 0.5f, L + side * 0.5f, side, 0.4f, Pal.White); Mark(mb, prevR - side * 0.5f, R - side * 0.5f, side, 0.4f, Pal.White); }
                        if (kind == "rail")
                        {
                            Mark(mb, prevC - side * 0.72f + Vector3.up * 0.12f, c - side * 0.72f + Vector3.up * 0.12f, side, 0.08f, Pal.Steel);
                            Mark(mb, prevC + side * 0.72f + Vector3.up * 0.12f, c + side * 0.72f + Vector3.up * 0.12f, side, 0.08f, Pal.Steel);
                            mb.Col(Pal.WoodDark).Box(c + Vector3.up * 0.06f, new Vector3(2.4f, 0.1f, 0.25f));
                        }
                        if (walks)
                        {
                            // a kerb, then a raised pavement either side
                            Vector3 up = Vector3.up * 0.15f;
                            mb.Col(Pal.Concrete).Quad(prevL + up, L + up, L - side * 2.6f + up, prevL - side * 2.6f + up, Vector3.up);
                            mb.Col(Pal.Concrete).Quad(prevR + up, R + up, R + side * 2.6f + up, prevR + side * 2.6f + up, Vector3.up);
                            mb.Col(Pal.ConcreteDark).Quad(prevL, L, L + up, prevL + up, side);
                            mb.Col(Pal.ConcreteDark).Quad(prevR, R, R + up, prevR + up, -side);
                        }
                        if (kerbs)
                        {
                            bool red = Mathf.FloorToInt(dist / 3f) % 2 == 0;
                            Mark(mb, prevL - side * 0.5f + Vector3.up * 0.02f, L - side * 0.5f + Vector3.up * 0.02f, side, 1f, red ? Pal.Red : Pal.White);
                            Mark(mb, prevR + side * 0.5f + Vector3.up * 0.02f, R + side * 0.5f + Vector3.up * 0.02f, side, 1f, red ? Pal.White : Pal.Red);
                        }
                        if (barriers)
                        {
                            mb.Col(Pal.Steel).Box((prevL + L) * 0.5f - side * 1.4f + Vector3.up * 0.45f, Abs(Quaternion.LookRotation(dir) * new Vector3(0.12f, 0.5f, seg + 0.05f)));
                            mb.Col(Pal.Steel).Box((prevR + R) * 0.5f + side * 1.4f + Vector3.up * 0.45f, Abs(Quaternion.LookRotation(dir) * new Vector3(0.12f, 0.5f, seg + 0.05f)));
                        }
                        dist += seg;
                    }
                    prevL = L; prevR = R; prevC = c;
                }
            }
            if (road.Loops[0] && (kind == "track" || s.Int("checkpoints", 0) > 0))
            {
                // a chequered start line where the ride begins
                List<Vector3> line = road.Lines[0];
                Vector3 c = line[0], dir = line[1] - line[0];
                dir.y = 0f;
                dir.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                for (int i = 0; i < 12; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        Vector3 at = c + side * (-half + road.Width * (i + 0.5f) / 12f) + dir * (j * 0.8f);
                        at.y = Ground(at) + 0.06f;
                        mb.Col((i + j) % 2 == 0 ? Pal.White : Pal.Black).Box(at, new Vector3(road.Width / 12f, 0.02f, 0.8f));
                    }
            }
            Mesh mesh = mb.Build("Road_" + road.Id);
            road.Root.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = road.Root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _world.Objects.LitMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var props = new MaterialPropertyBlock();
            props.SetColor("_Tint", Color.white);
            props.SetFloat("_Appear", 1f);
            mr.SetPropertyBlock(props);
            road.Root.AddComponent<SurfaceTag>().Name = kind == "dirt" ? "dirt" : kind == "path" ? "gravel" : "asphalt";
        }

        float Ground(Vector3 p) => _world.Terrain.HeightAt(p.x, p.z);

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static void Mark(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 side, float width, Color color)
        {
            Vector3 up = Vector3.up * 0.012f, h = side * (width * 0.5f);
            mb.Col(color).Quad(a - h + up, a + h + up, b + h + up, b - h + up, Vector3.up);
        }

        // ------------------------------------------------------------------ what lines it

        void Lamps(Road road)
        {
            AssetDef lamp = Library.Asset("street_lamp");
            if (lamp == null) return;
            Group g = _world.Objects.Begin(road.Id + "_lamps", "spawn", Statement.Make("spawn", "id", road.Id + "_lamps", "asset", "street_lamp"), 0.8f);
            g.Asset = lamp.Id;
            road.Groups.Add(g.Id);
            float offset = road.Width * 0.5f + (road.Kind == "street" ? 2.2f : 1.2f);
            int placed = 0;
            foreach (List<Vector3> line in road.Lines)
            {
                float since = 0f;
                for (int i = 1; i < line.Count && placed < 120; i++)
                {
                    since += Vector3.Distance(line[i - 1], line[i]);
                    if (since < 28f) continue;
                    since = 0f;
                    Vector3 dir = line[i] - line[i - 1];
                    dir.y = 0f;
                    Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        if (placed % 2 == 1 && sign < 0f) continue;      // staggered: one side, then the other
                        Vector3 at = line[i] + side * offset * sign;
                        g.Add(new Placement { Mesh = lamp.SceneryMesh, Position = new Vector3(at.x, 0f, at.z), Rotation = Quaternion.LookRotation(-side * sign), Scale = Vector3.one, Tint = Color.white, Grounded = true });
                        var light = new GameObject("lamp_light");
                        light.transform.SetParent(road.Root.transform, false);
                        light.transform.position = new Vector3(at.x, Ground(at) + lamp.Bounds.size.y * 0.9f, at.z);
                        LightSource ls = light.AddComponent<LightSource>();
                        ls.Color = new Color(1f, 0.82f, 0.55f);
                        ls.Range = 11f;
                        ls.Offset = Vector3.zero;
                        placed++;
                    }
                }
            }
        }

        /// <summary>Buildings on lots along the road, facing it. In a grid, the blocks between the streets are built round.</summary>
        void Buildings(Road road, Statement b, Statement s)
        {
            var assets = new List<AssetDef>();
            foreach (string name in b.StrList("assets")) { AssetDef d = Library.FindAsset(name); if (d != null) assets.Add(d); }
            if (assets.Count == 0) foreach (string name in new[] { "shop", "apartment_block", "office_block", "house" }) { AssetDef d = Library.Asset(name); if (d != null) assets.Add(d); }
            if (assets.Count == 0) return;
            float density = Mathf.Clamp01(b.Num("density", 0.85f)), setback = b.Num("setback", 1f);
            Vector2 scale = b.TryRange("scale", out float lo, out float hi) ? new Vector2(lo, hi) : new Vector2(0.85f, 1.35f);
            bool solid = !b.Has("solid") || b.Flag("solid", true);
            var groups = new Dictionary<AssetDef, Group>();
            var rng = new System.Random(road.Id.GetHashCode());
            float sidewalk = road.Kind == "street" ? 2.6f : 0.5f;
            Vector3 user = _world.Head != null ? _world.Head.position : Vector3.zero;
            int total = 0;

            void Lot(Vector3 front, Vector3 facing, float roomAlong, float roomDeep)
            {
                if (total >= 180 || rng.NextDouble() > density) return;
                AssetDef def = assets[rng.Next(assets.Count)];
                Bounds bb = def.Bounds;
                float k = Mathf.Lerp(scale.x, scale.y, (float)rng.NextDouble());
                // never wider than the lot or deeper than the block allows
                k = Mathf.Min(k, roomAlong / Mathf.Max(bb.size.x, 0.5f), roomDeep / Mathf.Max(bb.size.z, 0.5f));
                if (k < 0.3f) return;
                Vector3 at = front + facing * -(bb.size.z * k * 0.5f + setback);
                if (new Vector2(at.x - user.x, at.z - user.z).magnitude < bb.extents.magnitude * k + 3f) return;
                if (OnAnyRoad(at, bb.extents.x * k)) return;
                if (!groups.TryGetValue(def, out Group g))
                {
                    string gid = road.Id + "_" + def.Id;
                    g = _world.Objects.Begin(gid, "spawn", Statement.Make("spawn", "id", gid, "asset", def.Id), 1.2f);
                    g.Asset = def.Id;
                    groups[def] = g;
                    road.Groups.Add(gid);
                }
                Quaternion rot = Quaternion.LookRotation(facing);
                var scaleV = Vector3.one * k;
                if (def.Stretch > 0f) scaleV.y *= 1f + ((float)rng.NextDouble() * 2f - 1f) * def.Stretch;
                g.Add(new Placement { Mesh = def.SceneryMesh, Position = new Vector3(at.x, 0f, at.z), Rotation = rot, Scale = scaleV, Tint = Color.white, Grounded = true });
                if (solid && Vector3.Distance(at, user) < 120f) _world.Objects.Solidify(g, def, new Vector3(at.x, 0f, at.z), rot, scaleV);
                total++;
            }

            if (road.IsGrid)
            {
                float edge = road.Width * 0.5f + sidewalk;
                for (int i = 0; i < road.GridNX; i++)
                    for (int j = 0; j < road.GridNZ; j++)
                    {
                        float x0 = road.GridX0 + i * road.GridBX + edge, x1 = road.GridX0 + (i + 1) * road.GridBX - edge;
                        float z0 = road.GridZ0 + j * road.GridBZ + edge, z1 = road.GridZ0 + (j + 1) * road.GridBZ - edge;
                        float deep = (z1 - z0) * 0.5f - 1f;
                        if (x1 - x0 < 6f || deep < 4f) continue;
                        int lots = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 14f));
                        float lotW = (x1 - x0) / lots;
                        for (int l = 0; l < lots; l++)
                        {
                            float x = x0 + lotW * (l + 0.5f);
                            Lot(road.GridOrigin + road.GridTurn * new Vector3(x, 0f, z0), road.GridTurn * Vector3.back, lotW - 1f, deep);
                            Lot(road.GridOrigin + road.GridTurn * new Vector3(x, 0f, z1), road.GridTurn * Vector3.forward, lotW - 1f, deep);
                        }
                    }
            }
            else
            {
                float edge = road.Width * 0.5f + sidewalk;
                foreach (List<Vector3> line in road.Lines)
                {
                    float since = 0f;
                    for (int i = 1; i < line.Count; i++)
                    {
                        since += Vector3.Distance(line[i - 1], line[i]);
                        if (since < 16f) continue;
                        since = 0f;
                        Vector3 dir = line[i] - line[i - 1];
                        dir.y = 0f;
                        Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
                        Lot(line[i] - side * edge, side, 14f, 20f);
                        Lot(line[i] + side * edge, -side, 14f, 20f);
                    }
                }
            }
        }

        bool OnAnyRoad(Vector3 p, float radius)
        {
            foreach (Road r in _roads.Values)
                foreach (List<Vector3> line in r.Lines)
                    for (int i = 0; i + 1 < line.Count; i++)
                    {
                        Vector3 a = line[i], b = line[i + 1];
                        Vector3 ab = b - a, ap = p - a;
                        ab.y = ap.y = 0f;
                        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
                        if ((ap - ab * t).magnitude < r.Width * 0.5f + radius) return true;
                    }
            return false;
        }

        /// <summary>Traffic: each car goes round its own loop of lanes (along one side, back along the other), and stops for what is in front.</summary>
        void Traffic(Road road, Statement t)
        {
            var assets = new List<AssetDef>();
            foreach (string name in t.StrList("assets")) { AssetDef d = Library.FindAsset(name); if (d != null) assets.Add(d); }
            if (assets.Count == 0) { AssetDef car = Library.Asset("car"); if (car != null) assets.Add(car); }
            if (assets.Count == 0) return;
            int count = Mathf.Clamp(t.Int("count", 8), 0, 24);
            float speed = t.Num("speed", road.Kind == "highway" ? 22f : road.Kind == "track" ? 26f : 11f);
            var rng = new System.Random(road.Id.GetHashCode() + 7);
            var groups = new Dictionary<AssetDef, Group>();
            for (int i = 0; i < count; i++)
            {
                List<Vector3> line = road.Lines[i % road.Lines.Count];
                bool loop = road.Loops[i % road.Lines.Count];
                if (line.Count < 2) continue;
                float lane = road.Kind == "track" ? road.Width * (0.15f + 0.2f * (i % 3)) - road.Width * 0.3f : road.Width * 0.25f;
                List<Vector3> route = loop ? Offset(line, lane, true) : Offset(line, lane, false);
                if (!loop) { List<Vector3> back = Offset(line, -lane, false); back.Reverse(); route.AddRange(back); }
                AssetDef def = assets[rng.Next(assets.Count)];
                if (!groups.TryGetValue(def, out Group g))
                {
                    string gid = road.Id + "_traffic_" + def.Id;
                    g = _world.Objects.Begin(gid, "spawn", Statement.Make("spawn", "id", gid, "asset", def.Id), 0.8f);
                    g.Asset = def.Id;
                    groups[def] = g;
                    road.Groups.Add(gid);
                }
                Color tint = Color.HSVToRGB((float)rng.NextDouble(), 0.5f, 0.85f);
                if (def.Id == "taxi" || def.Id == "police_car" || def.Id == "bus") tint = Color.white;
                g.Extras.Add(Mover.Create(_world, def, new Route(route, true), speed * Mathf.Lerp(0.8f, 1.15f, (float)rng.NextDouble()), (float)rng.NextDouble(), Vector3.one, tint, g.Id));
            }
            foreach (Group g in groups.Values) ThingSettings.Apply(_world, Statement.Make("spawn", "id", g.Id, "asset", g.Asset), g.Id, false);
        }

        static List<Vector3> Offset(List<Vector3> line, float by, bool loop)
        {
            var result = new List<Vector3>(line.Count);
            for (int k = 0; k < line.Count; k++)
            {
                Vector3 next = line[loop ? (k + 1) % line.Count : Mathf.Min(k + 1, line.Count - 1)], prev = line[loop ? (k - 1 + line.Count) % line.Count : Mathf.Max(k - 1, 0)];
                Vector3 dir = next - prev;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
                Vector3 p = line[k] + side * by;
                p.y = 0f;
                result.Add(p);
            }
            return result;
        }

        /// <summary>People on the pavements, wandering as people do.</summary>
        void Walkers(Road road, Statement w)
        {
            var names = w.StrList("assets");
            if (names.Count == 0) names = new List<string> { "person", "woman", "child" };
            int count = Mathf.Clamp(w.Int("count", 8), 0, 16);
            var rng = new System.Random(road.Id.GetHashCode() + 13);
            float edge = road.Width * 0.5f + 1.3f;
            for (int i = 0; i < count; i++)
            {
                List<Vector3> line = road.Lines[rng.Next(road.Lines.Count)];
                Vector3 p = line[rng.Next(line.Count)];
                int k = Mathf.Max(1, line.IndexOf(p));
                Vector3 dir = line[k] - line[k - 1];
                dir.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward);
                Vector3 at = p + side * edge * (rng.Next(2) == 0 ? -1f : 1f);
                string gid = road.Id + "_walker." + i;
                _world.Objects.Spawn(Statement.Make("spawn", "id", gid, "asset", names[rng.Next(names.Count)], "behavior", "wander",
                    "at", new List<object> { (double)at.x, 0.0, (double)at.z }));
                road.Groups.Add(gid);
            }
        }

        // ------------------------------------------------------------------ a circuit

        void Circuit(Road road, int gates)
        {
            List<Vector3> line = road.Lines[0];
            road.Gates = new List<Vector3>();
            for (int i = 0; i < gates; i++) road.Gates.Add(line[Mathf.RoundToInt(i * line.Count / (float)gates) % line.Count]);
            road.Next = 1;
            road.LapStarted = -1f;
            _world.Play.Set("lap", 0.0);
            _world.Play.Set("lap_time", 0.0);
            _world.Play.Set("best_lap", 0.0);
        }

        public void Tick(float dt)
        {
            if (_roads.Count == 0 || _world.Head == null) return;
            Vector3 p = _world.Vehicle != null && _world.Vehicle.Riding ? _world.Vehicle.transform.position : _world.Head.position;
            foreach (Road road in _roads.Values)
            {
                if (road.Gates == null) continue;
                float reach = road.Width * 0.8f + 4f;
                if (road.LapStarted < 0f)
                {
                    // the clock starts as they cross the line the first time
                    if (Flat(p - road.Gates[0]) < reach) { road.LapStarted = Time.time; road.Next = 1; }
                    continue;
                }
                _world.Play.Set("lap_time", System.Math.Round(Time.time - road.LapStarted, 1));
                Vector3 gate = road.Gates[road.Next % road.Gates.Count];
                if (Flat(p - gate) > reach) continue;
                if (road.Next % road.Gates.Count == 0)
                {
                    float lap = Time.time - road.LapStarted;
                    road.Best = road.Best < 0f ? lap : Mathf.Min(road.Best, lap);
                    _world.Play.Set("lap_time", System.Math.Round(lap, 1));
                    _world.Play.Set("best_lap", System.Math.Round(road.Best, 1));
                    _world.Play.Add("lap", 1);
                    road.LapStarted = Time.time;
                    road.Next = 1;
                    _world.Rules.Fire("lap", new Rules.Subject { Asset = road.Id, Group = road.Id, Position = p });
                    _world.Rules.Fire("lap", _world.Rules.OfPlayer());
                }
                else
                {
                    road.Next++;
                    Sfx.Play("beep", p, 0.3f, 1.4f);
                }
            }
        }

        static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        // ------------------------------------------------------------------ taking it away

        public void Remove(string id)
        {
            if (!_roads.TryGetValue(id, out Road road)) return;
            foreach (string gid in road.Groups) _world.Objects.Remove(gid, 0.3f);
            if (road.Root != null) Object.Destroy(road.Root);
            _roads.Remove(id);
        }

        public void Clear()
        {
            foreach (string id in new List<string>(_roads.Keys)) Remove(id);
            _hasStart = false;
        }
    }
}
