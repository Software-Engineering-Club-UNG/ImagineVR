using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImagineVR
{
    /// <summary>One bounded, single-floor interior. Static room meshes and exact wall colliders.</summary>
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
                Pose frame = s.Frame();
                Vector3 origin = Statement.InWorld(frame, s.Vec("at", Vector3.zero));
                Quaternion rotation = frame.rotation * Quaternion.Euler(0, s.Num("rot_y"), 0);
                float floor = world.Terrain.HeightAt(origin.x, origin.z);
                foreach (var raw in source)
                {
                    Statement z = Statement.Wrap(raw);
                    Vector3 size = z.Vec("size", Vector3.zero), centre = z.Vec("center", Vector3.zero);
                    if (!Finite(size) || !Finite(centre) || size.x < 1.2f || size.z < 1.2f || size.x > 16 || size.z > 16 || size.y < 2.5f || size.y > 6)
                        throw new ArgumentException("Invalid room dimensions.");
                    if (Mathf.Abs(centre.y) > 0.001f) throw new ArgumentException("Rooms must share one floor.");
                    if (newZones.Exists(r => r.Id == z.Str("id"))) throw new ArgumentException("Duplicate room id.");
                    foreach (Room existing in newZones)
                        if (Mathf.Abs(existing.Center.x - centre.x) < (existing.Size.x + size.x) / 2 - 0.01f && Mathf.Abs(existing.Center.z - centre.z) < (existing.Size.z + size.z) / 2 - 0.01f)
                            throw new ArgumentException("Rooms overlap.");
                    var room = new Room { Id = z.Str("id"), Center = centre, Size = size };
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
                floor += 0.15f;
                candidate = new GameObject("Structure_" + s.Str("id", "interior"));
                candidate.SetActive(false);
                candidate.transform.SetParent(world.transform, false);
                candidate.transform.SetPositionAndRotation(new Vector3(origin.x, floor, origin.z), rotation);
                if (material == null) material = new Material(world.Objects.LitMaterial) { name = "Interior", enableInstancing = false };
                for (int i = 0; i < source.Count; i++)
                    BuildRoom(candidate.transform, newZones[i], Statement.Wrap(source[i]), newTheme, s.Int("seed"), newMeshes, newDoors);
                foreach (Room room in newZones) AddLampVisuals(room, candidate.transform, newTheme, newMeshes);
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
                zones.AddRange(newZones); meshes.AddRange(newMeshes);
                foreach (var d in newDoors) doors.Add(d.Key, d.Value);
                root.SetActive(true);
                colliders = root.GetComponentsInChildren<Collider>();
                PublishLighting();
                Physics.SyncTransforms();
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

        void BuildRoom(Transform parent, Room r, Statement plan, string style, int seed, List<Mesh> owned, Dictionary<string, StructureDoor> builtDoors)
        {
            var go = new GameObject("Room_" + r.Id); go.transform.SetParent(parent, false); go.transform.localPosition = r.Center;
            var mb = new MeshBuilder();
            Color wall = Pal.Hex(style == "dungeon" ? "#77756C" : style == "house" ? "#C6B79B" : "#738899");
            Color ground = Pal.Hex(style == "dungeon" ? "#55534B" : style == "house" ? "#84603C" : "#495E70");
            // Floor/ceiling subdivisions keep vertex lighting useful at room scale.
            Tile(mb, r.Size.x, r.Size.z, -0.10f, ground);
            Tile(mb, r.Size.x, r.Size.z, r.Size.y + 0.10f, wall * 0.75f);
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
                var parts = new List<Vector2>();
                if (opening == null) parts.Add(new Vector2(0, width));
                else { parts.Add(new Vector2(-(width + 1.2f) / 4, (width - 1.2f) / 2)); parts.Add(new Vector2((width + 1.2f) / 4, (width - 1.2f) / 2)); }
                foreach (Vector2 part in parts)
                {
                    mb.Push(wallOrigin, new Vector3(0, angle, 0));
                    Wall(mb, part.x, part.y, r.Size.y, wall, style, seed);
                    mb.Pop();
                    ColliderBox(go.transform, "Wall", wallOrigin + q * new Vector3(part.x, r.Size.y / 2, 0), new Vector3(part.y, r.Size.y, 0.20f), q);
                }
                if (opening != null)
                {
                    mb.Push(wallOrigin, new Vector3(0, angle, 0)).Col(wall).Box(new Vector3(0, (r.Size.y + 2.2f) / 2, 0), new Vector3(1.2f, r.Size.y - 2.2f, 0.2f)).Pop();
                    ColliderBox(go.transform, "Lintel", wallOrigin + Vector3.up * ((r.Size.y + 2.2f) / 2), new Vector3(1.2f, r.Size.y - 2.2f, 0.2f), q);
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

        static void Wall(MeshBuilder mb, float centre, float width, float height, Color color, string style, int seed)
        {
            mb.Col(color * 0.55f).Box(new Vector3(centre, height / 2, 0), new Vector3(width, height, 0.20f));
            float rowHeight = style == "dungeon" ? 0.4f : 1f;
            for (float y = 0; y < height - 0.001f; y += rowHeight)
            {
                int row = Mathf.RoundToInt(y / rowHeight);
                float start = centre - width / 2, end = centre + width / 2;
                for (float x = start - (row % 2) * 0.4f; x < end; x += 0.8f)
                {
                    float left = Mathf.Max(start, x), right = Mathf.Min(end, x + 0.8f);
                    if (right - left < 0.03f) continue;
                    float shade = 0.88f + 0.12f * Mathf.Abs(Mathf.Sin((row * 17 + x * 37 + seed) * 1.7f));
                    mb.Col(color * shade).Box(new Vector3((left + right) / 2, y + Mathf.Min(rowHeight, height - y) / 2, 0),
                        new Vector3(right - left - 0.016f, Mathf.Min(rowHeight, height - y) - 0.016f, 0.22f));
                }
            }
        }

        static BoxCollider ColliderBox(Transform parent, string name, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localRotation = rotation ?? Quaternion.identity;
            return go.AddComponent<BoxCollider>().WithSize(size);
        }

        void AddLampVisuals(Room r, Transform parent, string style, List<Mesh> owned)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = r.Center + new Vector3((i == 0 ? -1 : 1) * (r.Size.x / 2 - 0.25f), Mathf.Min(1.9f, r.Size.y - 0.5f), 0);
                var mb = new MeshBuilder();
                mb.Col(style == "dungeon" ? "#67482E" : "#455262").Box(p - Vector3.up * 0.16f, new Vector3(0.12f, 0.4f, 0.12f));
                mb.Col(style == "dungeon" ? "#FFB14C" : "#CBE8FF").Emit(1).Box(p, new Vector3(0.16f, 0.20f, 0.16f));
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
                Room r = zones[i]; centres[i] = new Vector4(r.Center.x, r.Size.y / 2, r.Center.z, theme == "dungeon" ? 0.035f : 0.22f);
                bounds.Encapsulate(new Bounds(r.Center + Vector3.up * (r.Size.y / 2), r.Size));
                halves[i] = new Vector4(r.Size.x / 2 + 0.13f, r.Size.y / 2 + 0.13f, r.Size.z / 2 + 0.13f, 0);
                for (int j = 0; j < 2; j++) lights[i * 2 + j] = new Vector4(r.Center.x + (j == 0 ? -1 : 1) * (r.Size.x / 2 - 0.25f), Mathf.Min(1.9f, r.Size.y - 0.5f), r.Center.z, 4.5f);
            }
            Shader.SetGlobalMatrix("_ImInteriorInverse", root.transform.worldToLocalMatrix);
            Shader.SetGlobalVector("_ImInteriorCenter", bounds.center);
            Shader.SetGlobalVector("_ImInteriorHalf", bounds.extents + Vector3.one * 0.15f);
            Shader.SetGlobalInt("_ImRoomCount", zones.Count);
            Shader.SetGlobalVectorArray("_ImRoomCenters", centres); Shader.SetGlobalVectorArray("_ImRoomHalves", halves);
            Shader.SetGlobalVectorArray("_ImRoomLights", lights);
            Shader.SetGlobalVector("_ImRoomLightColor", theme == "dungeon" ? new Vector4(1.1f, 0.55f, 0.16f, 1) : new Vector4(0.65f, 0.85f, 1f, 0));
        }

        public void Remove(string id) { if (id == Id) Clear(); }
        public void TrackFurnishing(Statement s) { if (Active && s.Has("room") && s.Has("id")) furnishingIds.Add(s.Str("id")); }
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
            root = null; Id = null; meshes.Clear(); zones.Clear(); doors.Clear(); colliders = new Collider[0]; Shader.SetGlobalInt("_ImRoomCount", 0);
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
