using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>What a surface is made of, for how things slide and roll on it.</summary>
    public sealed class SurfaceTag : MonoBehaviour { public string Name; }

    /// <summary>Something that lights what is round it after dark: a lamp, a campfire, a beacon. Published to the shaders by Lights.</summary>
    public sealed class LightSource : MonoBehaviour
    {
        public static readonly List<LightSource> All = new List<LightSource>();
        public Color Color = new Color(1f, 0.85f, 0.63f);
        public float Range = 8f, Intensity = 1f;
        public Vector3 Offset = Vector3.up;
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }

    /// <summary>Lays what a spawn or scatter of the third version says onto the things it made.</summary>
    public static class ThingSettings
    {
        static bool Mine(string group, string id) => group == id || (group != null && group.StartsWith(id + ".", System.StringComparison.Ordinal));

        public static void Apply(World world, Statement s, string id, bool hittable)
        {
            bool solid = s.Flag("solid");
            bool wantsHealth = s.Has("health") || s.Has("breaks");
            string team = s.Str("team");
            string surface = s.Str("surface");

            // ---- loose things
            foreach (Prop p in world.Props.All)
            {
                if (p == null || !Mine(p.GroupId, id)) continue;
                if (p.Body != null && s.Has("mass")) p.Body.mass = Mathf.Clamp(s.Num("mass"), 0.01f, 5000f);
                if (s.Has("bounce") || s.Has("friction") || surface != null)
                {
                    PhysicsMaterial m = Material(s.Num("bounce", p.Def.Bounce), s.Has("friction") ? s.Num("friction") : Surfaces.Friction(surface, 0.5f));
                    foreach (Collider c in p.GetComponentsInChildren<Collider>()) c.sharedMaterial = m;
                }
                if (surface != null) (p.GetComponent<SurfaceTag>() ?? p.gameObject.AddComponent<SurfaceTag>()).Name = surface;
                if (wantsHealth) Health.Give(p.gameObject, s.Num("health", 1f), team).Breaks = BreakKind(s);
                Look(world, s, p.gameObject, p.Def.Bounds.size.y * p.transform.localScale.y);
            }

            // ---- living things
            foreach (Creature c in Creature.All)
            {
                if (c == null || !Mine(c.GroupId, id)) continue;
                if (s.Has("health")) Health.Give(c.gameObject, s.Num("health", 1f), team);
                else if (team != null) Health.Give(c.gameObject, 1f, team).Hp = 1f;
                c.Configure(s);
                Look(world, s, c.gameObject, c.Height);
            }

            // ---- standing things: scenery, and things that travel a path
            Group g = world.Objects.Get(id);
            if (g == null) return;
            AssetDef def = g.Asset != null ? Library.FindAsset(g.Asset) : null;
            foreach (GameObject extra in g.Extras)
            {
                if (extra == null || extra.GetComponent<Mover>() == null || def == null) continue;
                // traffic, rivals, a goalie, targets on a track: they can be hit, and things bounce off them
                Bounds b = def.Bounds;
                BoxCollider box = extra.GetComponent<BoxCollider>() ?? extra.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = Vector3.Max(b.size, Vector3.one * 0.1f);
                Rigidbody body = extra.GetComponent<Rigidbody>() ?? extra.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                Hittable h = extra.GetComponent<Hittable>() ?? extra.AddComponent<Hittable>();
                h.Group = g.Id;
                h.Asset = g.Asset;
                if (wantsHealth) Health.Give(extra, s.Num("health", 1f), team).Breaks = BreakKind(s);
                Look(world, s, extra, b.size.y);
            }
            if (g.Items.Count == 0) return;
            // pieces that turn on their own (a windmill's sails) are drawn apart from the combined scenery, turning
            if (def != null && def.Animated && g.Items.Count <= 24)
                foreach (Placement p in g.Items)
                {
                    var spinner = new GameObject("Turning_" + g.Id);
                    spinner.transform.SetParent(world.transform, false);
                    spinner.transform.SetPositionAndRotation(new Vector3(p.Position.x, world.SurfaceHeight(p.Position.x, p.Position.z) + p.Position.y, p.Position.z), p.Rotation);
                    spinner.transform.localScale = p.Scale;
                    Articulated.Attach(spinner, def, g.Tint, false);
                    g.Extras.Add(spinner);
                }
            if (solid || wantsHealth || hittable || surface != null) Hittables(world, g);
            foreach (GameObject extra in g.Extras)
            {
                if (extra == null || extra.name != "Solid") continue;
                if (surface != null) (extra.GetComponent<SurfaceTag>() ?? extra.AddComponent<SurfaceTag>()).Name = surface;
                if (surface != null || s.Has("bounce") || s.Has("friction"))
                {
                    PhysicsMaterial m = Material(s.Num("bounce", 0.2f), s.Has("friction") ? s.Num("friction") : Surfaces.Friction(surface, 0.5f));
                    foreach (Collider c in extra.GetComponents<Collider>()) c.sharedMaterial = m;
                }
                if (wantsHealth) Health.Give(extra, s.Num("health", 1f), team).Breaks = BreakKind(s);
            }
            if (def != null && (s.Has("label") || s.Has("sign") || s.Has("light")))
                foreach (Placement p in g.Items)
                {
                    var holder = new GameObject("Look_" + g.Id);
                    holder.transform.SetParent(world.transform, false);
                    holder.transform.SetPositionAndRotation(new Vector3(p.Position.x, world.SurfaceHeight(p.Position.x, p.Position.z) + p.Position.y, p.Position.z), p.Rotation);
                    g.Extras.Add(holder);
                    Look(world, s, holder, def.Bounds.size.y * p.Scale.y, def.Bounds, p.Scale);
                    if (g.Items.Count > 12) break;      // a label on every tree of a forest helps nobody
                }
        }

        static string BreakKind(Statement s) => s.Has("breaks") ? (s.Str("breaks") == "true" || s.Flag("breaks") ? "crate" : s.Str("breaks")) : null;

        static readonly Dictionary<long, PhysicsMaterial> Materials = new Dictionary<long, PhysicsMaterial>();

        static PhysicsMaterial Material(float bounce, float friction)
        {
            long key = Mathf.RoundToInt(bounce * 20f) * 1000L + Mathf.RoundToInt(friction * 50f);
            if (Materials.TryGetValue(key, out PhysicsMaterial m) && m != null) return m;
            m = new PhysicsMaterial("Surface")
            {
                bounciness = Mathf.Clamp01(bounce), dynamicFriction = Mathf.Clamp(friction, 0f, 2f), staticFriction = Mathf.Clamp(friction * 1.1f, 0f, 2f),
                bounceCombine = PhysicsMaterialCombine.Maximum,
                // the slipperier of two surfaces wins: a puck on ice slides however rough the puck
                frictionCombine = friction < 0.1f ? PhysicsMaterialCombine.Minimum : PhysicsMaterialCombine.Average,
            };
            return Materials[key] = m;
        }

        /// <summary>Every placement of a group given a body that knows which thing it is. Not for a forest: a hundred colliders cost too much.</summary>
        public static void Hittables(World world, Group g)
        {
            if (g.Items.Count > 60) return;
            AssetDef def = g.Asset != null ? Library.FindAsset(g.Asset) : null;
            if (def == null) return;
            foreach (Placement p in g.Items)
            {
                bool has = false;
                foreach (GameObject extra in g.Extras)
                    if (extra != null && extra.name == "Solid" && new Vector2(extra.transform.position.x - p.Position.x, extra.transform.position.z - p.Position.z).sqrMagnitude < 0.01f) { has = true; break; }
                if (!has) world.Objects.Solidify(g, def, p.Position, p.Rotation, p.Scale * g.Scale);
            }
            foreach (GameObject extra in g.Extras)
            {
                if (extra == null || extra.name != "Solid") continue;
                Hittable h = extra.GetComponent<Hittable>() ?? extra.AddComponent<Hittable>();
                h.Group = g.Id;
                h.Asset = g.Asset;
            }
        }

        /// <summary>A label over it, a sign on its front, a light: the look a statement may give anything.</summary>
        static void Look(World world, Statement s, GameObject host, float height, Bounds? bounds = null, Vector3? scale = null)
        {
            if (world.Font != null && s.Has("label"))
            {
                GameObject label = Text(world, host.transform, s.Str("label"), 1.6f, new Color(1f, 0.9f, 0.65f));
                label.transform.localPosition = Vector3.up * (height / Mathf.Max(host.transform.lossyScale.y, 0.01f) + 0.35f);
                label.AddComponent<FaceUser>();
            }
            if (world.Font != null && s.Has("sign"))
            {
                // written on its front, across most of its width, a third of the way down from its top
                Bounds b = bounds ?? new Bounds(Vector3.up * height * 0.5f, new Vector3(1f, height, 1f));
                Vector3 k = scale ?? Vector3.one;
                GameObject sign = Text(world, host.transform, s.Str("sign"), Mathf.Clamp(b.size.x * k.x * 0.9f, 0.4f, 6f), new Color(1f, 0.95f, 0.85f));
                sign.transform.localPosition = new Vector3(b.center.x * k.x, Mathf.Max(b.max.y * k.y * 0.7f, 0.3f), b.max.z * k.z + 0.03f);
                sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // TMP reads towards -Z
                var tmp = sign.GetComponent<TextMeshPro>();
                tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(b.size.x * k.x, 0.6f), 0.6f);
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 0.3f;
                tmp.fontSizeMax = 6f;
            }
            Dictionary<string, object> lightObj = s.Obj("light");
            if (lightObj != null || s.Has("light"))
            {
                LightSource l = host.GetComponent<LightSource>() ?? host.AddComponent<LightSource>();
                Statement light = lightObj != null ? Statement.Wrap(lightObj) : s;
                l.Color = light.TryColor(lightObj != null ? "color" : "light", out Color c) ? c : new Color(1f, 0.85f, 0.63f);
                l.Range = lightObj != null ? light.Num("range", 8f) : 8f;
                l.Intensity = lightObj != null ? light.Num("intensity", 1f) : 1f;
                l.Offset = Vector3.up * Mathf.Max(height * 0.8f, 0.3f);
            }
        }

        static GameObject Text(World world, Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = world.Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            return go;
        }
    }

    /// <summary>
    /// How far a ball rolls depends on what it rolls on: a long way on a lane or a putting green,
    /// nowhere in a bunker. Loose round things take their drag from the surface under them.
    /// </summary>
    public static class Surfaces
    {
        static readonly Dictionary<string, float> Drag = new Dictionary<string, float>
        {
            ["ice"] = 0.03f, ["glass"] = 0.05f, ["wood"] = 0.08f, ["metal"] = 0.1f, ["felt"] = 0.45f, ["green"] = 0.35f, ["rubber"] = 0.6f,
            ["carpet"] = 0.9f, ["fairway"] = 0.8f, ["stone"] = 0.3f, ["grass"] = 1.6f, ["dry_grass"] = 1.8f, ["sand"] = 4f, ["mud"] = 3f,
            ["asphalt"] = 0.25f, ["concrete"] = 0.25f, ["marble"] = 0.15f, ["snow"] = 1.5f, ["gravel"] = 1.2f, ["cobble"] = 0.5f,
            ["forest_floor"] = 2f, ["dirt"] = 1f, ["swamp"] = 3f,
        };
        static readonly Dictionary<string, float> Grip = new Dictionary<string, float>
        {
            ["ice"] = 0.02f, ["glass"] = 0.1f, ["wood"] = 0.25f, ["metal"] = 0.2f, ["felt"] = 0.35f, ["rubber"] = 0.9f, ["carpet"] = 0.7f,
            ["grass"] = 0.6f, ["sand"] = 0.8f, ["mud"] = 0.7f, ["stone"] = 0.5f,
        };

        public static float Friction(string surface, float fallback) => surface != null && Grip.TryGetValue(surface, out float f) ? f : fallback;

        /// <summary>The name of what is under a point: a tagged surface, a painted region of ground, or the ground's own material.</summary>
        public static string At(World world, Vector3 point, float reach = 0.6f)
        {
            if (Physics.Raycast(point + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                SurfaceTag tag = hit.collider.GetComponentInParent<SurfaceTag>();
                if (tag != null) return tag.Name;
            }
            return world.Terrain.MaterialAt(point.x, point.z);
        }

        public static void Tick(World world, int frame)
        {
            if (frame % 4 != 0) return;
            foreach (Prop p in world.Props.All)
            {
                if (p == null || p.Body == null || p.Held || p.Floating || p.Body.isKinematic || p.GroupId == "shot") continue;
                if (p.Def.Shape != ColliderShape.Sphere && p.GetComponent<SurfaceTag>() == null) continue;
                if (p.Body.linearVelocity.sqrMagnitude < 0.0004f) continue;
                float r = p.Def.Bounds.extents.magnitude * p.transform.localScale.x;
                string name = At(world, p.transform.position, r + 0.2f);
                if (name != null && Drag.TryGetValue(name, out float d)) { p.Body.linearDamping = d; p.Body.angularDamping = Mathf.Max(0.05f, d * 0.5f); }
                else p.Body.linearDamping = p.Def.Drag;
            }
        }
    }

    /// <summary>`fx`: something that keeps going. Smoke from a chimney, fire in a brazier, fireflies over a pond, a fountain, a portal.</summary>
    public sealed class Emitter : MonoBehaviour
    {
        public string Kind;
        public float Size = 1f, Rate = 0.6f, Until = -1f;
        public Color? Tint;
        public Transform Follow;
        public Vector3 Offset;
        ParticleSystem _ps;
        float _due;

        public void Begin()
        {
            bool additive = Kind == "fire" || Kind == "sparks" || Kind == "magic" || Kind == "fireflies" || Kind == "embers" || Kind == "portal";
            _ps = Fx.Stream("Fx_" + Kind, transform, 400);
            _ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = Fx.Material(additive);
            ParticleSystem.MainModule main = _ps.main;
            main.gravityModifier = Kind == "fountain" || Kind == "sparks" || Kind == "waterfall" || Kind == "drips" ? 1f : Kind == "leaves" || Kind == "snowfall" ? 0.05f : -0.02f;
        }

        void Update()
        {
            if (Until > 0f && Time.time > Until) { Destroy(gameObject); return; }
            if (Follow != null) transform.position = Follow.position + Follow.rotation * Offset;
            else if (Follow == null && Offset != Vector3.zero) { Destroy(gameObject); return; }   // what it was on is gone
            if (_ps == null) return;
            float every = Mathf.Lerp(0.2f, 0.015f, Mathf.Clamp01(Rate));
            _due -= Time.deltaTime;
            int guard = 0;
            while (_due <= 0f && guard++ < 6) { _due += every; Puff(); }
        }

        void Puff()
        {
            float s = Size;
            Vector3 at = transform.position, v = Vector3.zero;
            Color c = Color.white;
            float size = 0.2f * s, life = 1.5f;
            switch (Kind)
            {
                case "fire": at += Random.insideUnitSphere * 0.25f * s; v = Vector3.up * Random.Range(0.8f, 1.6f) * s; c = Color.Lerp(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.85f, 0.3f), Random.value); size = Random.Range(0.25f, 0.5f) * s; life = 0.7f; break;
                case "smoke": at += Random.insideUnitSphere * 0.3f * s; v = Vector3.up * Random.Range(0.4f, 0.9f) * s + Random.insideUnitSphere * 0.15f; c = new Color(0.35f, 0.35f, 0.37f, 0.5f); size = Random.Range(0.6f, 1.2f) * s; life = 4f; break;
                case "steam": v = Vector3.up * Random.Range(0.5f, 1f) * s; c = new Color(1f, 1f, 1f, 0.35f); size = Random.Range(0.3f, 0.7f) * s; life = 2.5f; break;
                case "sparks": v = (Vector3.up + Random.insideUnitSphere) * 3f * s; c = new Color(1f, 0.8f, 0.4f); size = 0.04f * s; life = 0.8f; break;
                case "fountain": v = Vector3.up * 5f * s + Random.insideUnitSphere * 0.6f; c = new Color(0.7f, 0.9f, 1f, 0.7f); size = 0.08f * s; life = 1.6f; break;
                case "waterfall": at += Vector3.right * Random.Range(-1f, 1f) * s; v = Vector3.down * 0.5f; c = new Color(0.85f, 0.95f, 1f, 0.6f); size = 0.35f * s; life = 2.5f; break;
                case "fireflies": at += Random.insideUnitSphere * 4f * s; v = Random.insideUnitSphere * 0.2f; c = new Color(0.9f, 1f, 0.4f); size = 0.05f; life = 3f; break;
                case "embers": at += Random.insideUnitSphere * 0.4f * s; v = Vector3.up * Random.Range(0.6f, 1.4f) + Random.insideUnitSphere * 0.3f; c = new Color(1f, 0.5f, 0.15f); size = 0.03f; life = 2.5f; break;
                case "leaves": at += Random.insideUnitSphere * 5f * s + Vector3.up * 4f; v = new Vector3(Random.Range(-0.4f, 0.4f), -0.4f, Random.Range(-0.4f, 0.4f)); c = Color.Lerp(new Color(0.8f, 0.4f, 0.1f), new Color(0.9f, 0.7f, 0.2f), Random.value); size = 0.12f; life = 6f; break;
                case "snowfall": at += new Vector3(Random.Range(-6f, 6f), 6f, Random.Range(-6f, 6f)) * s; v = Vector3.down * 0.6f; c = Color.white; size = 0.06f; life = 9f; break;
                case "bubbles": at += Random.insideUnitSphere * 0.3f * s; v = Vector3.up * 0.6f; c = new Color(0.8f, 0.95f, 1f, 0.45f); size = Random.Range(0.05f, 0.15f) * s; life = 3f; break;
                case "portal": { float a = Random.value * Mathf.PI * 2f; at += new Vector3(Mathf.Cos(a), Mathf.Sin(a) + 1.2f, 0f) * s; v = -new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.6f * s; c = Color.Lerp(new Color(0.5f, 0.3f, 1f), new Color(0.3f, 0.9f, 1f), Random.value); size = 0.08f * s; life = 1.2f; break; }
                case "magic": at += Random.insideUnitSphere * 0.6f * s; v = Vector3.up * 0.4f + Random.insideUnitSphere * 0.4f; c = Color.Lerp(new Color(1f, 0.85f, 0.4f), new Color(0.8f, 0.5f, 1f), Random.value); size = 0.05f * s; life = 1.5f; break;
                case "dust": at += Random.insideUnitSphere * 2f * s; v = Random.insideUnitSphere * 0.2f; c = new Color(0.8f, 0.72f, 0.6f, 0.3f); size = 0.4f * s; life = 4f; break;
                case "drips": at += new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f)) * s; v = Vector3.zero; c = new Color(0.6f, 0.8f, 1f, 0.8f); size = 0.03f; life = 1.5f; break;
            }
            if (Tint.HasValue) c = new Color(Tint.Value.r, Tint.Value.g, Tint.Value.b, c.a);
            _ps.Emit(new ParticleSystem.EmitParams { position = at, velocity = v, startColor = c, startSize = size, startLifetime = life }, 1);
        }
    }

    public static class Emitters
    {
        static readonly Dictionary<string, Emitter> ById = new Dictionary<string, Emitter>();

        /// <summary>The `fx` statement: one that keeps going until it is removed or the place is cleared.</summary>
        public static void Apply(World world, Statement s)
        {
            string id = s.Str("id", "fx");
            Remove(id);
            var go = new GameObject("Fx_" + id);
            go.transform.SetParent(world.transform, false);
            Emitter e = go.AddComponent<Emitter>();
            e.Kind = s.Str("kind", "smoke");
            e.Size = Mathf.Clamp(s.Num("size", 1f), 0.1f, 40f);
            e.Rate = Mathf.Clamp01(s.Num("rate", 0.6f));
            if (s.TryColor("color", out Color c)) e.Tint = c;
            if (s.Has("on"))
            {
                Rules.Subject on = world.Rules.Nearest(s.Str("on"), out bool found);
                if (found && on.Object != null) { e.Follow = on.Object.transform; e.Offset = s.Vec("offset", Vector3.up * 0.5f); }
                else go.transform.position = found ? on.Position : Vector3.zero;
            }
            else
            {
                Vector3 at = Statement.InWorld(s.Frame(), s.Vec("at", new Vector3(0f, 0f, 4f)));
                at.y += world.SurfaceHeight(at.x, at.z);
                go.transform.position = at;
            }
            e.Begin();
            ById[id] = e;
        }

        /// <summary>A burst that goes on for a while: smoke from a wreck, a fire left burning.</summary>
        public static void Timed(string kind, Vector3 at, float seconds, float size, Color? color)
        {
            var go = new GameObject("Fx_" + kind);
            go.transform.position = at;
            Emitter e = go.AddComponent<Emitter>();
            e.Kind = kind;
            e.Size = Mathf.Clamp(size, 0.1f, 40f);
            e.Tint = color;
            e.Until = Time.time + Mathf.Clamp(seconds, 0.5f, 120f);
            e.Begin();
        }

        public static void Remove(string id)
        {
            if (ById.TryGetValue(id, out Emitter e)) { if (e != null) Object.Destroy(e.gameObject); ById.Remove(id); }
        }

        public static void Clear()
        {
            foreach (Emitter e in ById.Values) if (e != null) Object.Destroy(e.gameObject);
            ById.Clear();
        }
    }
}
