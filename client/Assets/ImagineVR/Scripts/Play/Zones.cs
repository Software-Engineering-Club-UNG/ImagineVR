using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// `zone`: a place that notices what goes into it. A cup on a green, the pit behind the pins, a
    /// finish line, the mouth of a goal, a ring in the sky, lava. Unseen unless `show` marks it.
    /// What crosses its edge raises `enter` and `exit` for the rules; an `effect` works on what stays.
    /// </summary>
    public sealed class Zones
    {
        sealed class Zone
        {
            public string Id, On, Show;
            public bool Sphere;
            public Vector3 At, Offset, Size;
            public float Radius, Yaw;
            public Color Color;
            public float Damage, Heal, Push, Kill;
            public Vector3 PushDir;
            public readonly HashSet<int> Inside = new HashSet<int>();
            public bool PlayerInside;
            public float NextEffect;
            public GameObject Marker;
            public Transform Anchor;      // what an `on` zone is fixed to, once found
        }

        readonly World _world;
        readonly PlaySystem _play;
        readonly Dictionary<string, Zone> _zones = new Dictionary<string, Zone>();
        readonly HashSet<int> _seen = new HashSet<int>();

        public int Count => _zones.Count;

        public Zones(World world, PlaySystem play) { _world = world; _play = play; }

        public void Define(Statement s)
        {
            string id = s.Str("id");
            if (string.IsNullOrEmpty(id)) return;
            Remove(id);
            var z = new Zone
            {
                Id = id, On = s.Str("on"), Show = s.Str("show", "none"), Sphere = s.Str("shape", "box") == "sphere",
                Size = s.Vec("size", Vector3.one), Radius = s.Num("radius", 1.5f), Color = s.TryColor("color", out Color c) ? c : new Color(1f, 0.85f, 0.3f),
                Offset = s.Vec("offset", Vector3.zero),
            };
            if (!s.Has("size") && !s.Has("shape")) z.Sphere = true;
            Pose frame = s.Frame();
            z.Yaw = frame.rotation.eulerAngles.y + s.Num("rot_y", 0f);
            if (z.On == null)
            {
                z.At = Statement.InWorld(frame, s.Vec("at", new Vector3(0f, 0f, 4f)));
                z.At.y += _world.SurfaceHeight(z.At.x, z.At.z);
            }
            Dictionary<string, object> effect = s.Obj("effect");
            if (effect != null)
            {
                Statement e = Statement.Wrap(effect);
                z.Damage = e.Num("damage", 0f);
                z.Heal = e.Num("heal", 0f);
                z.Kill = e.Flag("kill") ? 1f : 0f;
                if (e.TryVec3("push", out Vector3 push)) { z.PushDir = frame.rotation * push; z.Push = 1f; }
            }
            _zones[id] = z;
            if (z.Show != "none") z.Marker = Marker(z);
            SessionLog.Write("zone", "id", id, "on", z.On, "at", z.At, "sphere", z.Sphere, "radius", z.Radius, "size", z.Size);
        }

        public void Remove(string id)
        {
            if (!_zones.TryGetValue(id, out Zone z)) return;
            if (z.Marker != null) Object.Destroy(z.Marker);
            _zones.Remove(id);
        }

        public void Clear()
        {
            foreach (Zone z in _zones.Values) if (z.Marker != null) Object.Destroy(z.Marker);
            _zones.Clear();
        }

        Vector3 CentreOf(Zone z)
        {
            if (z.On == null) return z.At;
            if (z.Anchor == null || Time.frameCount % 30 == 0)
            {
                Rules.Subject s = _world.Rules.Nearest(z.On, out bool found);
                if (!found) return new Vector3(0f, -9999f, 0f);
                if (s.Object != null) z.Anchor = s.Object.transform;
                else return s.Position + z.Offset;
            }
            return z.Anchor != null ? z.Anchor.position + z.Anchor.rotation * z.Offset : new Vector3(0f, -9999f, 0f);
        }

        public bool Centre(string id, out Vector3 at)
        {
            at = default;
            if (id == null || !_zones.TryGetValue(id, out Zone z)) return false;
            at = CentreOf(z);
            return true;
        }

        bool Contains(Zone z, Vector3 centre, Vector3 p)
        {
            Vector3 d = p - centre;
            if (z.Sphere) return d.magnitude <= z.Radius;
            d = Quaternion.Euler(0f, -z.Yaw, 0f) * d;
            Vector3 h = z.Size * 0.5f;
            return Mathf.Abs(d.x) <= h.x && Mathf.Abs(d.y) <= h.y && Mathf.Abs(d.z) <= h.z;
        }

        /// <summary>For `inside(zone, thing)`.</summary>
        public bool Inside(string zone, string who)
        {
            if (zone == null || !_zones.TryGetValue(zone, out Zone z)) return false;
            if (string.IsNullOrEmpty(who) || who == "player") return z.PlayerInside;
            Vector3 centre = CentreOf(z);
            foreach (Prop p in _world.Props.All) if (p != null && Rules.Names(who, Rules.Of(p)) && Contains(z, centre, p.transform.position)) return true;
            foreach (Creature c in Creature.All) if (c != null && c.Up && Rules.Names(who, Rules.Of(c)) && Contains(z, centre, c.transform.position + Vector3.up * c.Height * 0.5f)) return true;
            return false;
        }

        public void Tick(float dt, int frame)
        {
            if (_zones.Count == 0) return;
            bool effects = false;
            foreach (Zone z in _zones.Values)
            {
                Vector3 centre = CentreOf(z);
                if (z.Marker != null) z.Marker.transform.position = centre;
                // the user: their head on foot; what they ride carries them in
                Vector3 user = _world.Head != null ? _world.Head.position : Vector3.zero;
                bool userIn = Contains(z, centre, user) || (_world.Vehicle != null && _world.Vehicle.Riding && Contains(z, centre, _world.Vehicle.transform.position));
                if (userIn != z.PlayerInside)
                {
                    z.PlayerInside = userIn;
                    _world.Rules.Fire(userIn ? "enter" : "exit", _world.Rules.OfPlayer(), z.Id);
                    if (_world.Vehicle != null && _world.Vehicle.Riding)
                        _world.Rules.Fire(userIn ? "enter" : "exit", new Rules.Subject { Asset = _world.Vehicle.Asset, Group = _world.Vehicle.Asset, Position = user }, z.Id);
                    _world.Rules.Changed();
                }
                if (frame % 3 != 0) continue;
                _seen.Clear();
                foreach (Prop p in new List<Prop>(_world.Props.All))
                {
                    if (p == null || p.GroupId == "shot") continue;
                    Check(z, centre, p.gameObject.GetInstanceID(), Contains(z, centre, p.transform.position), Rules.Of(p));
                }
                foreach (Creature c in new List<Creature>(Creature.All))
                {
                    if (c == null || !c.Up) continue;
                    Check(z, centre, c.gameObject.GetInstanceID(), Contains(z, centre, c.transform.position + Vector3.up * (c.Height * 0.5f)), Rules.Of(c));
                }
                z.Inside.RemoveWhere(id => !_seen.Contains(id));      // what was destroyed inside has left
                if (Time.time >= z.NextEffect && (z.Damage > 0f || z.Heal > 0f || z.Kill > 0f || z.Push > 0f)) effects = true;
            }
            if (effects) Effects(dt);
        }

        void Check(Zone z, Vector3 centre, int id, bool inside, in Rules.Subject subject)
        {
            _seen.Add(id);
            bool was = z.Inside.Contains(id);
            if (inside == was) return;
            if (inside) z.Inside.Add(id); else z.Inside.Remove(id);
            _world.Rules.Fire(inside ? "enter" : "exit", subject, z.Id);
            _world.Rules.Changed();
        }

        void Effects(float dt)
        {
            foreach (Zone z in _zones.Values)
            {
                if (Time.time < z.NextEffect) continue;
                if (z.Damage <= 0f && z.Heal <= 0f && z.Kill <= 0f && z.Push <= 0f) continue;
                z.NextEffect = Time.time + (z.Push > 0f ? 0.1f : 1f);
                Vector3 centre = CentreOf(z);
                if (z.PlayerInside)
                {
                    if (z.Kill > 0f) _play.HurtPlayer(100000f, centre);
                    else if (z.Damage > 0f) _play.HurtPlayer(z.Damage, centre);
                    if (z.Heal > 0f) _play.HurtPlayer(-z.Heal, centre);
                }
                foreach (Prop p in _world.Props.All)
                {
                    if (p == null || p.Body == null || p.Held || p.Body.isKinematic || !z.Inside.Contains(p.gameObject.GetInstanceID())) continue;
                    if (z.Push > 0f) p.Body.AddForce(z.PushDir * 0.1f, ForceMode.VelocityChange);
                }
                if (z.Damage > 0f || z.Kill > 0f)
                    foreach (Creature c in new List<Creature>(Creature.All))
                    {
                        if (c == null || !z.Inside.Contains(c.gameObject.GetInstanceID())) continue;
                        Rules.Subject s = Rules.Of(c);
                        s.Amount = z.Kill > 0f ? 100000f : z.Damage;
                        Health.Strike(_world, s);
                    }
            }
        }

        GameObject Marker(Zone z)
        {
            var mb = new MeshBuilder();
            float r = z.Sphere ? z.Radius : Mathf.Max(z.Size.x, z.Size.z) * 0.5f;
            Color c = z.Color;
            switch (z.Show)
            {
                case "beam":
                    // a column of light seen from far off: where to go
                    mb.Col(c).Emit(1.6f).Cyl(new Vector3(0f, -1f, 0f), r * 0.9f, r * 0.9f, 0.08f, 24);
                    mb.Col(Color.Lerp(c, Color.white, 0.35f)).Emit(2f).Cyl(new Vector3(0f, -1f, 0f), 0.12f, 0.12f, 60f, 6);
                    mb.Glow(new Vector3(0f, 1.5f, 0f), r * 1.4f, c);
                    break;
                case "flag":
                    mb.Col(new Color(0.9f, 0.9f, 0.9f)).Cyl(new Vector3(0f, -0.2f, 0f), 0.025f, 0.025f, 2.6f, 6);
                    mb.Col(c).Emit(0.8f).Push(new Vector3(0f, 2.05f, 0f)).TriTwoSided(Vector3.zero, new Vector3(0.6f, 0.15f, 0f), new Vector3(0f, 0.3f, 0f)).Pop();
                    break;
                case "glow":
                    mb.Col(c).Emit(2f).Disc(new Vector3(0f, 0.01f, 0f), Mathf.Max(r, 0.15f), 24, true);
                    mb.Glow(new Vector3(0f, 0.15f, 0f), Mathf.Max(r * 2f, 0.4f), c);
                    break;
                case "box":
                    Vector3 h = z.Sphere ? Vector3.one * z.Radius : z.Size * 0.5f;
                    foreach (Vector3 corner in new[] { new Vector3(-h.x, 0f, -h.z), new Vector3(h.x, 0f, -h.z), new Vector3(-h.x, 0f, h.z), new Vector3(h.x, 0f, h.z) })
                        mb.Col(c).Emit(1.5f).Cyl(corner + Vector3.down * h.y, 0.03f, 0.03f, h.y * 2f, 5);
                    break;
                default: // ring
                    mb.Col(c).Emit(1.5f).Push(Vector3.zero, new Vector3(z.Sphere ? 0f : 0f, 0f, 0f)).Torus(Vector3.zero, Mathf.Max(r, 0.2f), Mathf.Clamp(r * 0.04f, 0.02f, 0.25f), 32, 6).Pop();
                    break;
            }
            var go = new GameObject("Zone_" + z.Id);
            go.transform.SetParent(_world.transform, false);
            go.transform.rotation = Quaternion.Euler(0f, z.Yaw, 0f);
            Mesh mesh = mb.Build("Zone_" + z.Id);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mesh.subMeshCount > 1 ? new[] { _world.Objects.LitMaterial, _world.Objects.GlowMaterial } : new[] { _world.Objects.LitMaterial };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var props = new MaterialPropertyBlock();
            props.SetColor("_Tint", Color.white);
            props.SetFloat("_Appear", 1f);
            mr.SetPropertyBlock(props);
            return go;
        }
    }
}
