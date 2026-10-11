using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImagineVR
{
    /// <summary>
    /// One bounded, single-floor interior: the inside of anything with walls round it. Rooms are the
    /// size the thing really is (a cockpit, a classroom, a hangar), may have windows to see out of,
    /// an arched roof, and a floor high in the air. Static room meshes and exact wall colliders.
    /// </summary>
    public sealed class StructureSystem
    {
        public const int MaxZones = 15;
        readonly World world;
        GameObject root;
        readonly List<Room> zones = new List<Room>();
        readonly Dictionary<string, StructureDoor> doors = new Dictionary<string, StructureDoor>();
        readonly HashSet<string> furnishingIds = new HashSet<string>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly Vector4[] centres = new Vector4[MaxZones], halves = new Vector4[MaxZones];
        readonly Vector4[] lights = new Vector4[MaxZones * 2];
        Material material;
        Collider[] colliders = new Collider[0];
        string theme, entry;
        // How it looks. A theme is only a starting point: any of these may be given outright, so an
        // interior can be a school, a castle, a cave or a sweet shop and not just the three themes.
        Color wallColor, floorColor, lampColor;
        float ambient, flicker;
        public string Id { get; private set; }
        public bool Active => root != null;
        public int ZoneCount => zones.Count;
        public int DoorCount => doors.Count;
        public IEnumerable<StructureDoor> Doors => doors.Values;
        public sealed class Room
        {
            public string Id;
            public Vector3 Center, Size;
            public readonly HashSet<string> OpenSides = new HashSet<string>();
            public readonly HashSet<string> Windows = new HashSet<string>();
            public bool Corridor;   // a way between rooms, not a room: nothing is furnished into it
            public bool Vault;      // an arched roof along its length
            public bool VaultAlongX;   // which way the arch runs: with the line of rooms, so a cabin and its cockpit are one tube
            /// <summary>How high the side walls stand before an arched roof springs from them.</summary>
            public float Springing => Vault ? Size.y - Mathf.Min((VaultAlongX ? Size.z : Size.x) / 2, Size.y * 0.4f) : Size.y;
            public float WallHeight(string side) => Vault && ((side == "east" || side == "west") != VaultAlongX) ? Springing : Size.y;
            /// <summary>Where its two lamps hang, in the structure's own space, measured from the floor.</summary>
            public Vector3 Lamp(int which, bool onWalls)
            {
                float sign = which == 0 ? -1 : 1;
                if (Vault) return Center + new Vector3(VaultAlongX ? sign * Size.x / 4 : 0, Size.y - 0.14f, VaultAlongX ? 0 : sign * Size.z / 4);
                if (onWalls && !Windows.Contains("east") && !Windows.Contains("west"))
                    return Center + new Vector3(sign * (Size.x / 2 - 0.25f), Mathf.Min(1.9f, Size.y - 0.5f), 0);
                // overhead, a quarter of the way in from each end of its longer side
                return Center + new Vector3(Size.x >= Size.z ? sign * Size.x / 4 : 0, Size.y - 0.08f, Size.x >= Size.z ? 0 : sign * Size.z / 4);
            }
            public float LampReach => Mathf.Clamp(Mathf.Max(Size.x, Size.z) * 0.7f, 4.5f, 16f);
            public bool Contains(Vector3 p, float margin = 0) => Mathf.Abs(p.x - Center.x) <= Size.x / 2 + margin && Mathf.Abs(p.z - Center.z) <= Size.z / 2 + margin;
        }

        public StructureSystem(World owner) { world = owner; Shader.SetGlobalInt("_ImRoomCount", 0); }

        public bool Build(Statement s)
        {
            GameObject candidate = null;
            var newMeshes = new List<Mesh>();
            var newZones = new List<Room>();
            var newDoors = new Dictionary<string, StructureDoor>();
            try
            {
                var source = s.ObjList("zones");
                if (source.Count == 0 || source.Count > MaxZones) throw new ArgumentException("Invalid room layout; expected 1..15 zones.");
                string newTheme = s.Str("theme", "dungeon");
                bool dungeon = newTheme == "dungeon", house = newTheme == "house", plain = newTheme == "plain";
                Color newWall = s.TryColor("wall", out Color givenWall) ? givenWall : Pal.Hex(dungeon ? "#77756C" : house ? "#C6B79B" : plain ? "#D6D2C8" : "#738899");
                Color newFloor = s.TryColor("floor", out Color givenFloor) ? givenFloor : Pal.Hex(dungeon ? "#55534B" : house ? "#84603C" : plain ? "#8A8478" : "#495E70");
                Color newLamp = s.TryColor("light", out Color givenLamp) ? givenLamp : dungeon ? new Color(1.1f, 0.55f, 0.16f) : plain || house ? new Color(1f, 0.93f, 0.8f) : new Color(0.65f, 0.85f, 1f);
                float newAmbient = s.Has("brightness") ? Mathf.Lerp(0.02f, 0.6f, Mathf.Clamp01(s.Num("brightness"))) : dungeon ? 0.035f : plain ? 0.3f : 0.22f;
                bool vault = s.Str("ceiling", "flat") == "vault";
                float altitude = Mathf.Clamp(s.Num("altitude"), 0, 80);
                var roomIds = new HashSet<string>();
                foreach (var listed in s.ObjList("rooms")) roomIds.Add(Statement.Wrap(listed).Str("id", ""));
                Pose frame = s.Frame();
                Vector3 origin = Statement.InWorld(frame, s.Vec("at", Vector3.zero));
                Quaternion rotation = frame.rotation * Quaternion.Euler(0, s.Num("rot_y"), 0);
                float floor = world.Terrain.HeightAt(origin.x, origin.z);
                foreach (var raw in source)
                {
                    Statement z = Statement.Wrap(raw);
                    Vector3 size = z.Vec("size", Vector3.zero), centre = z.Vec("center", Vector3.zero);
                    if (!Finite(size) || !Finite(centre)) throw new ArgumentException("Invalid room dimensions.");
                    // repaired, not refused: a size out of range is the nearest one that can be built
                    size = new Vector3(Mathf.Clamp(size.x, 0.3f, 30), Mathf.Clamp(size.y, 2.2f, 12), Mathf.Clamp(size.z, 0.3f, 30));
                    if (Mathf.Abs(centre.y) > 0.001f) throw new ArgumentException("Rooms must share one floor.");
                    if (newZones.Exists(r => r.Id == z.Str("id"))) throw new ArgumentException("Duplicate room id.");
                    foreach (Room existing in newZones)
                        if (Mathf.Abs(existing.Center.x - centre.x) < (existing.Size.x + size.x) / 2 - 0.01f && Mathf.Abs(existing.Center.z - centre.z) < (existing.Size.z + size.z) / 2 - 0.01f)
                            throw new ArgumentException("Rooms overlap.");
                    var room = new Room { Id = z.Str("id"), Center = centre, Size = size, Corridor = roomIds.Count > 0 && !roomIds.Contains(z.Str("id", "")) };
                    room.Vault = vault && Mathf.Min(size.x, size.z) >= 1.2f;
                    room.VaultAlongX = s.Str("layout", "chain") == "hub" && size.x > size.z;
                    foreach (string side in z.StrList("windows")) room.Windows.Add(side);
                    foreach (var opening in z.ObjList("openings"))
                    {
                        string side = Statement.Wrap(opening).Str("side");
                        if (side != "north" && side != "east" && side != "south" && side != "west") throw new ArgumentException("Invalid doorway side.");
                        if (!room.OpenSides.Add(side)) throw new ArgumentException("Duplicate doorway side.");
                    }
                    newZones.Add(room);
                    // Sample the entire footprint, not just corners: a hill must not protrude through the floor.
                    for (float x = -size.x / 2; x <= size.x / 2 + 0.01f; x += 1)
                        for (float zz = -size.z / 2; zz <= size.z / 2 + 0.01f; zz += 1)
                        {
                            Vector3 point = origin + rotation * (centre + new Vector3(x, 0, zz));
                            if (new Vector2(point.x, point.z).magnitude > World.Radius - 8) throw new ArgumentException("Structure exceeds world bounds.");
                            floor = Mathf.Max(floor, world.Terrain.HeightAt(point.x, point.z));
                        }
                }
                floor += 0.15f + altitude;
                candidate = new GameObject("Structure_" + s.Str("id", "interior"));
                candidate.SetActive(false);
                candidate.transform.SetParent(world.transform, false);
                candidate.transform.SetPositionAndRotation(new Vector3(origin.x, floor, origin.z), rotation);
                if (material == null) material = new Material(world.Objects.LitMaterial) { name = "Interior", enableInstancing = false };
                for (int i = 0; i < source.Count; i++)
                    BuildRoom(candidate.transform, newZones[i], Statement.Wrap(source[i]), newTheme, newWall, newFloor, s.Int("seed"), newMeshes, newDoors, altitude > 0.5f);
                foreach (Room room in newZones) if (Mathf.Min(room.Size.x, room.Size.z) >= 1.2f) AddLampVisuals(room, candidate.transform, newTheme, newLamp, newMeshes);
                if (!newZones.Exists(r => r.Id == s.Str("entry"))) throw new ArgumentException("Missing entry room.");
                if (newDoors.Count != s.ObjList("doors").Count) throw new ArgumentException("Door plan does not match openings.");
                var checkedDoors = new HashSet<string>();
                foreach (var d in s.ObjList("doors"))
                {
                    string id = Statement.Wrap(d).Str("id", "");
                    if (!newDoors.ContainsKey(id) || !checkedDoors.Add(id)) throw new ArgumentException("Invalid door reference.");
                }
                int triangles = 0; foreach (Mesh mesh in newMeshes) triangles += mesh.triangles.Length / 3;
                if (triangles > 200000) throw new ArgumentException("Structure exceeds geometry budget.");
                // Every zone must be connected through an actual opening.
                var reached = new HashSet<int> { 0 };
                for (int pass = 0; pass < newZones.Count; pass++)
                    for (int i = 0; i < newZones.Count; i++) if (reached.Contains(i))
                        for (int j = 0; j < newZones.Count; j++) if (!reached.Contains(j) && Connected(newZones[i], newZones[j])) reached.Add(j);
                if (reached.Count != newZones.Count) throw new ArgumentException("Disconnected rooms.");
                // An entry must fit the current tracked player before replacing anything.
                Room entryRoom = newZones.Find(r => r.Id == s.Str("entry"));
                if (s.Flag("enter") && world.Vehicle != null && (world.Head.position.y - world.Vehicle.transform.position.y > entryRoom.Size.y - 0.15f))
                    throw new ArgumentException("The player is too tall for this ceiling; resize the player or increase room height.");
                Clear();
                root = candidate; Id = s.Str("id", "interior"); theme = newTheme; entry = s.Str("entry");
                wallColor = newWall; floorColor = newFloor; lampColor = newLamp; ambient = newAmbient; flicker = dungeon ? 1f : 0f;
                zones.AddRange(newZones); meshes.AddRange(newMeshes);
                foreach (var d in newDoors) doors.Add(d.Key, d.Value);
                root.SetActive(true);
                colliders = root.GetComponentsInChildren<Collider>();
                PublishLighting();
                Physics.SyncTransforms();
                world.Objects.Resettle();
                if (s.Flag("enter") && world.Vehicle != null) world.Vehicle.PlaceAt(RoomPosition(entry, Vector3.zero), root.transform.eulerAngles.y);
                SessionLog.Write("structure", "id", Id, "rooms", zones.Count, "doors", doors.Count, "theme", theme);
                return true;
            }
            catch (Exception ex)
            {
                if (candidate != null && candidate != root) UnityEngine.Object.Destroy(candidate);
                foreach (Mesh mesh in newMeshes) if (!meshes.Contains(mesh)) UnityEngine.Object.Destroy(mesh);
                world.Note("Interior was not built: " + ex.Message + " The previous place is unchanged.");
                return false;
            }
        }

        static bool Finite(Vector3 p) => !float.IsNaN(p.x + p.y + p.z) && !float.IsInfinity(p.x + p.y + p.z);
        static bool Touches(Room a, Room b) =>
            (Mathf.Abs(Mathf.Abs(a.Center.x - b.Center.x) - (a.Size.x + b.Size.x) / 2) < 0.02f && Mathf.Abs(a.Center.z - b.Center.z) < (a.Size.z + b.Size.z) / 2) ||
            (Mathf.Abs(Mathf.Abs(a.Center.z - b.Center.z) - (a.Size.z + b.Size.z) / 2) < 0.02f && Mathf.Abs(a.Center.x - b.Center.x) < (a.Size.x + b.Size.x) / 2);
        static bool Connected(Room a, Room b)
        {
            if (!Touches(a, b)) return false;
            Vector3 delta = b.Center - a.Center;
            bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
            if (Mathf.Abs(horizontal ? delta.z : delta.x) > 0.01f) return false;
            string side = horizontal ? (delta.x > 0 ? "east" : "west") : (delta.z > 0 ? "north" : "south");
            string opposite = side == "north" ? "south" : side == "south" ? "north" : side == "east" ? "west" : "east";
            return a.OpenSides.Contains(side) && b.OpenSides.Contains(opposite);
        }

        void BuildRoom(Transform parent, Room r, Statement plan, string style, Color wall, Color ground, int seed, List<Mesh> owned, Dictionary<string, StructureDoor> builtDoors, bool aloft)
        {
            var go = new GameObject("Room_" + r.Id); go.transform.SetParent(parent, false); go.transform.localPosition = r.Center;
            var mb = new MeshBuilder();
            // Floor/ceiling subdivisions keep vertex lighting useful at room scale.
            Tile(mb, r.Size.x, r.Size.z, -0.10f, ground);
            if (r.Vault) Arch(mb, r, wall * 0.75f);
            else Tile(mb, r.Size.x, r.Size.z, r.Size.y + 0.10f, wall * 0.75f);
            ColliderBox(go.transform, "Floor", new Vector3(0, -0.10f, 0), new Vector3(r.Size.x, 0.20f, r.Size.z));
            ColliderBox(go.transform, "Ceiling", new Vector3(0, r.Size.y + 0.10f, 0), new Vector3(r.Size.x, 0.20f, r.Size.z));
            foreach (string side in new[] { "north", "east", "south", "west" })
            {
                bool alongX = side == "north" || side == "south";
                float width = alongX ? r.Size.x : r.Size.z;
                float angle = side == "north" ? 0 : side == "east" ? 90 : side == "south" ? 180 : 270;
                Quaternion q = Quaternion.Euler(0, angle, 0);
                Vector3 wallOrigin = q * new Vector3(0, 0, (alongX ? r.Size.z : r.Size.x) / 2);
                var opening = plan.ObjList("openings").Find(o => Statement.Wrap(o).Str("side") == side);
                float tall = r.WallHeight(side);   // the side walls under an arched roof stop where it springs
                var parts = new List<Vector2>();
                if (opening == null) parts.Add(new Vector2(0, width));
                else { parts.Add(new Vector2(-(width + 1.2f) / 4, (width - 1.2f) / 2)); parts.Add(new Vector2((width + 1.2f) / 4, (width - 1.2f) / 2)); }
                bool windowed = opening == null && r.Windows.Contains(side) && width >= 1.4f;
                foreach (Vector2 part in parts)
                {
                    mb.Push(wallOrigin, new Vector3(0, angle, 0));
                    if (windowed) WindowWall(mb, part.y, tall, wall, style, seed);
                    else if (part.y > 0.02f) Wall(mb, part.x, part.y, tall, wall, style, seed);
                    mb.Pop();
                    // glass or wall, nothing passes: the collider is the whole wall either way
                    if (part.y > 0.02f) ColliderBox(go.transform, "Wall", wallOrigin + q * new Vector3(part.x, r.Size.y / 2, 0), new Vector3(part.y, r.Size.y, 0.20f), q);
                }
                if (opening != null)
                {
                    mb.Push(wallOrigin, new Vector3(0, angle, 0)).Col(wall).Box(new Vector3(0, (tall + 2.2f) / 2, 0), new Vector3(1.2f, Mathf.Max(tall - 2.2f, 0.02f), 0.2f)).Pop();
                    ColliderBox(go.transform, "Lintel", wallOrigin + Vector3.up * ((r.Size.y + 2.2f) / 2), new Vector3(1.2f, Mathf.Max(r.Size.y - 2.2f, 0.02f), 0.2f), q);
                    string doorId = Statement.Wrap(opening).Str("door");
                    if (!string.IsNullOrEmpty(doorId))
                    {
                        if (builtDoors.ContainsKey(doorId)) throw new ArgumentException("Duplicate door id.");
                        var hinge = new GameObject("Door_" + doorId); hinge.transform.SetParent(go.transform, false);
                        hinge.transform.localPosition = wallOrigin + q * new Vector3(-0.6f, 0, 0);
                        hinge.transform.localRotation = q;
                        StructureDoor door = hinge.AddComponent<StructureDoor>();
                        door.Init(doorId, material, style == "sci_fi" ? Pal.Hex("#50677A") : Pal.Hex("#65492F"));
                        builtDoors.Add(doorId, door);
                    }
                }
                if (aloft) continue;   // high in the air it stands on nothing
                // Hide raised foundations along the outer shell; no collider covers the room itself.
                Vector3 wp = parent.TransformPoint(r.Center + wallOrigin);
                float foundation = Mathf.Clamp(parent.position.y - world.Terrain.HeightAt(wp.x, wp.z), 0.15f, 50);
                mb.Push(wallOrigin, new Vector3(0, angle, 0)).Col(ground * 0.7f).Box(new Vector3(0, -foundation / 2 - 0.15f, 0), new Vector3(width, foundation, 0.20f)).Pop();
                ColliderBox(go.transform, "Foundation", wallOrigin - Vector3.up * (foundation / 2 + 0.15f), new Vector3(width, foundation, 0.20f), q);
            }
            Mesh mesh = mb.Build("Interior_" + r.Id); owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void Tile(MeshBuilder mb, float width, float depth, float y, Color color)
        {
            for (float x = -width / 2; x < width / 2 - 0.001f; x += 1)
                for (float z = -depth / 2; z < depth / 2 - 0.001f; z += 1)
                { float w = Mathf.Min(1, width / 2 - x), d = Mathf.Min(1, depth / 2 - z); mb.Col(color).Box(new Vector3(x + w / 2, y, z + d / 2), new Vector3(w, 0.20f, d)); }
        }

        /// <summary>A stretch of wall from `bottom` to `height`, in the theme's texture: rough courses of brick, panels, or smooth.</summary>
        static void Wall(MeshBuilder mb, float centre, float width, float height, Color color, string style, int seed, float bottom = 0)
        {
            if (height - bottom < 0.02f || width < 0.02f) return;
            if (style == "plain")
            {
                // smooth: one face, with a darker skirting where it meets the floor
                mb.Col(color).Box(new Vector3(centre, (bottom + height) / 2, 0), new Vector3(width, height - bottom, 0.22f));
                if (bottom < 0.01f) mb.Col(color * 0.7f).Box(new Vector3(centre, 0.06f, 0), new Vector3(width, 0.12f, 0.25f));
                return;
            }
            mb.Col(color * 0.55f).Box(new Vector3(centre, (bottom + height) / 2, 0), new Vector3(width, height - bottom, 0.20f));
            // big walls are laid in bigger courses, so a hangar costs no more than a cell
            float rowHeight = (style == "dungeon" ? 0.4f : 1f) * (height > 6.5f ? 2f : 1f), course = height > 6.5f ? 1.6f : 0.8f;
            for (float y = bottom; y < height - 0.001f; y += rowHeight)
            {
                int row = Mathf.RoundToInt(y / rowHeight);
                float start = centre - width / 2, end = centre + width / 2;
                for (float x = start - (row % 2) * course / 2; x < end; x += course)
                {
                    float left = Mathf.Max(start, x), right = Mathf.Min(end, x + course);
                    if (right - left < 0.03f) continue;
                    float shade = 0.88f + 0.12f * Mathf.Abs(Mathf.Sin((row * 17 + x * 37 + seed) * 1.7f));
                    mb.Col(color * shade).Box(new Vector3((left + right) / 2, y + Mathf.Min(rowHeight, height - y) / 2, 0),
                        new Vector3(right - left - 0.016f, Mathf.Min(rowHeight, height - y) - 0.016f, 0.22f));
                }
            }
        }

        /// <summary>
        /// A wall with windows in it: a sill, a head, piers between the openings and a thin frame in
        /// each. There is no pane: what is outside (the sky, clouds, the land below) is simply seen.
        /// </summary>
        static void WindowWall(MeshBuilder mb, float width, float height, Color color, string style, int seed)
        {
            const float pier = 0.5f;
            float sill = Mathf.Min(0.9f, height * 0.35f), head = height - Mathf.Max(0.35f, height * 0.18f);
            int n = Mathf.Max(1, Mathf.FloorToInt((width - pier) / 2.4f));
            float open = (width - pier * (n + 1)) / n;
            if (open < 0.4f || head - sill < 0.4f) { Wall(mb, 0, width, height, color, style, seed); return; }
            Wall(mb, 0, width, sill, color, style, seed);
            Wall(mb, 0, width, height, color, style, seed, head);
            for (int i = 0; i <= n; i++) Wall(mb, -width / 2 + pier / 2 + i * (open + pier), pier, head, color, style, seed + i, sill);
            Color frame = color * 0.45f;
            for (int i = 0; i < n; i++)
            {
                float x = -width / 2 + pier + open / 2 + i * (open + pier), mid = (sill + head) / 2;
                mb.Col(frame).Box(new Vector3(x, sill + 0.03f, 0), new Vector3(open, 0.06f, 0.26f)).Box(new Vector3(x, head - 0.03f, 0), new Vector3(open, 0.06f, 0.12f));
                mb.Box(new Vector3(x, mid, 0), new Vector3(0.04f, head - sill, 0.06f));
                if (head - sill > 1.6f) mb.Box(new Vector3(x, mid, 0), new Vector3(open, 0.04f, 0.06f));
            }
        }

        /// <summary>An arched roof along the room's length, springing from its side walls: flat strips, enough of them to read as a curve.</summary>
        static void Arch(MeshBuilder mb, Room r, Color color)
        {
            float span = r.VaultAlongX ? r.Size.z : r.Size.x, length = r.VaultAlongX ? r.Size.x : r.Size.z, from = r.Springing, rise = r.Size.y - from;
            const int strips = 10;
            mb.Push(Vector3.zero, new Vector3(0, r.VaultAlongX ? 90 : 0, 0));
            for (int i = 0; i < strips; i++)
            {
                float a0 = Mathf.PI * i / strips, a1 = Mathf.PI * (i + 1) / strips;
                var p0 = new Vector2(Mathf.Cos(a0) * span / 2, from + Mathf.Sin(a0) * rise);
                var p1 = new Vector2(Mathf.Cos(a1) * span / 2, from + Mathf.Sin(a1) * rise);
                Vector2 mid = (p0 + p1) / 2, run = p1 - p0;
                mb.Push(new Vector3(mid.x, mid.y + 0.1f, 0), new Vector3(0, 0, Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg));
                mb.Col(color * (0.9f + 0.1f * (i % 2))).Box(Vector3.zero, new Vector3(run.magnitude + 0.06f, 0.2f, length));
                mb.Pop();
            }
            mb.Pop();
        }

        static BoxCollider ColliderBox(Transform parent, string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localRotation = rotation ?? Quaternion.identity;
            return go.AddComponent<BoxCollider>().WithSize(size);
        }

        void AddLampVisuals(Room r, Transform parent, string style, Color glow, List<Mesh> owned)
        {
            bool torch = style == "dungeon";
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = r.Lamp(i, torch);
                bool onWall = p.y < r.Size.y - 0.4f && !r.Vault;
                var mb = new MeshBuilder();
                float brightest = Mathf.Max(glow.r, Mathf.Max(glow.g, glow.b), 0.01f);
                if (onWall)
                {
                    mb.Col(torch ? "#67482E" : "#455262").Box(p - Vector3.up * 0.16f, new Vector3(0.12f, 0.4f, 0.12f));
                    mb.Col(Color.Lerp(glow / brightest, Color.white, 0.35f)).Emit(1).Box(p, new Vector3(0.16f, 0.20f, 0.16f));
                }
                else
                {
                    // a light overhead: a flat panel, set along the ridge under an arch
                    mb.Col(Color.Lerp(glow / brightest, Color.white, 0.35f)).Emit(1).Box(p, r.Vault && !r.VaultAlongX ? new Vector3(0.3f, 0.05f, 0.9f) : new Vector3(0.9f, 0.05f, r.Vault ? 0.3f : 0.5f));
                }
                Mesh mesh = mb.Build("Lamp"); owned.Add(mesh);
                var lamp = new GameObject("Lamp"); lamp.transform.SetParent(parent, false);
                lamp.AddComponent<MeshFilter>().sharedMesh = mesh; lamp.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        public bool TryRoomPose(string id, out Pose pose)
        {
            Room r = zones.Find(z => z.Id == id);
            pose = r != null && Active ? new Pose(root.transform.TransformPoint(r.Center), root.transform.rotation) : Pose.identity;
            return r != null && Active;
        }
        /// <summary>A room by name, with where its middle is in the world. Corridors are not rooms.</summary>
        public bool TryRoom(string id, out Room room, out Pose pose)
        {
            room = Active ? zones.Find(z => z.Id == id && !z.Corridor) : null;
            pose = room != null ? new Pose(root.transform.TransformPoint(room.Center), root.transform.rotation) : Pose.identity;
            return room != null;
        }
        /// <summary>The room this point of the world is over or in, whatever its height, or null: for things asked for "here" by someone standing indoors.</summary>
        public Room RoomAt(Vector3 p)
        {
            if (!Active) return null;
            Vector3 local = root.transform.InverseTransformPoint(p);
            return zones.Find(r => !r.Corridor && r.Contains(local));
        }
        public Vector3 RoomPosition(string id, Vector3 local) => TryRoomPose(id, out Pose pose) ? Statement.InWorld(pose, local) : local;
        public bool Contains(Vector3 p)
        {
            if (!Active) return false;
            Vector3 local = root.transform.InverseTransformPoint(p);
            return zones.Exists(r => r.Contains(local) && local.y >= -0.15f && local.y <= r.Size.y + 0.15f);
        }
        public float FloorAt(Vector3 p)
        {
            if (!Active) return float.NaN;
            Vector3 local = root.transform.InverseTransformPoint(p);
            return zones.Exists(r => r.Contains(local)) ? root.transform.position.y : float.NaN;
        }
        public string CurrentRoom(Vector3 p)
        {
            if (!Active) return null; Vector3 local = root.transform.InverseTransformPoint(p);
            return zones.Find(r => r.Contains(local) && local.y >= -0.15f && local.y <= r.Size.y + 0.15f)?.Id;
        }
        public string Context(Vector3 head)
        {
            if (!Active) return "structure none";
            var states = new List<string>(); foreach (var d in doors) states.Add(d.Key + ":" + (d.Value.Open ? "open" : "closed"));
            return "structure " + Id + "; room " + (CurrentRoom(head) ?? "outside") + "; doors " + string.Join(",", states);
        }
        public void Door(Statement s)
        {
            if (doors.TryGetValue(s.Str("id", ""), out StructureDoor door)) door.Set(s.Str("state", "toggle"));
            else world.Note("That door does not exist in this interior.");
        }
        public bool TryInteract(Transform hand, bool pressed)
        {
            if (!Active || hand == null) return false;
            Physics.SyncTransforms();
            if (!Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore)) return false;
            StructureDoor door = hit.collider.GetComponentInParent<StructureDoor>();
            if (door == null) return false;
            door.Highlight(); if (pressed) door.Set("toggle"); return pressed;
        }

        public bool HeadBlocked(Vector3 head)
        {
            if (!Active) return false;
            foreach (Collider c in colliders)
                if (c.enabled && (c.ClosestPoint(head) - head).sqrMagnitude < 0.0025f) return true;
            return false;
        }
        public void Tick()
        {
            if (!Active) return;
            Rig rig = world.Head != null ? world.Head.GetComponentInParent<Rig>() : null;
            if (rig != null && !rig.XrActive && !ImagineApp.Typing && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                TryInteract(rig.Head, true);
            PublishLighting();
        }
        void PublishLighting()
        {
            if (!Active) return;
            Bounds bounds = new Bounds(zones[0].Center + Vector3.up * (zones[0].Size.y / 2), zones[0].Size);
            for (int i = 0; i < zones.Count; i++)
            {
                // a room with windows has daylight in it as well as its lamps
                Room r = zones[i]; centres[i] = new Vector4(r.Center.x, r.Size.y / 2, r.Center.z, ambient + (r.Windows.Count > 0 ? 0.12f : 0f));
                bounds.Encapsulate(new Bounds(r.Center + Vector3.up * (r.Size.y / 2), r.Size));
                halves[i] = new Vector4(r.Size.x / 2 + 0.13f, r.Size.y / 2 + 0.13f, r.Size.z / 2 + 0.13f, 0);
                for (int j = 0; j < 2; j++) { Vector3 lamp = r.Lamp(j, theme == "dungeon"); lights[i * 2 + j] = new Vector4(lamp.x, lamp.y, lamp.z, r.LampReach); }
            }
            Shader.SetGlobalMatrix("_ImInteriorInverse", root.transform.worldToLocalMatrix);
            Shader.SetGlobalVector("_ImInteriorCenter", bounds.center);
            Shader.SetGlobalVector("_ImInteriorHalf", bounds.extents + Vector3.one * 0.15f);
            Shader.SetGlobalInt("_ImRoomCount", zones.Count);
            Shader.SetGlobalVectorArray("_ImRoomCenters", centres); Shader.SetGlobalVectorArray("_ImRoomHalves", halves);
            Shader.SetGlobalVectorArray("_ImRoomLights", lights);
            Shader.SetGlobalVector("_ImRoomLightColor", new Vector4(lampColor.r, lampColor.g, lampColor.b, flicker));
        }

        public void Remove(string id) { if (id == Id) Clear(); }
        public void TrackFurnishing(Statement s) { if (Active && s.Has("room") && s.Has("id")) furnishingIds.Add(s.Str("id")); }
        public void TrackFurnishing(string id) { if (Active && !string.IsNullOrEmpty(id)) furnishingIds.Add(id); }
        public void Clear()
        {
            if (Active && world.Vehicle != null && Contains(world.Head.position))
            {
                Vector3 local = zones[0].Center - Vector3.forward * (zones[0].Size.z / 2 + 2);
                Vector3 outside = root.transform.TransformPoint(local); outside.y = world.Terrain.HeightAt(outside.x, outside.z);
                world.Vehicle.PlaceAt(outside, root.transform.eulerAngles.y);
            }
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            foreach (string id in furnishingIds) { world.Objects.Remove(id, 0.2f); world.Props.Remove(id); }
            furnishingIds.Clear();
            foreach (Mesh mesh in meshes) if (mesh != null) UnityEngine.Object.Destroy(mesh);
            bool stood = root != null;
            root = null; Id = null; meshes.Clear(); zones.Clear(); doors.Clear(); colliders = new Collider[0]; Shader.SetGlobalInt("_ImRoomCount", 0);
            if (stood && world.Objects != null) world.Objects.Resettle();
        }
        public void Dispose() { Clear(); if (material != null) UnityEngine.Object.Destroy(material); }
        public void Census(out int renderers, out int triangles)
        {
            renderers = triangles = 0;
            if (!Active) return;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                renderers++;
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)(mesh.GetIndexCount(i) / 3);
            }
        }
    }

    internal static class StructureColliderExtensions
    {
        internal static BoxCollider WithSize(this BoxCollider box, Vector3 size) { box.size = size; return box; }
    }
}
