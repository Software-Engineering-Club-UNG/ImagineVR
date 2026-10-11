using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// `make`: a new asset built from parts, for whatever the library lacks. What comes out is an
    /// AssetDef like any baked one, so everything that works for a library asset (spawn, scatter,
    /// give, ride, behavior, physics, rules) works for a made one with no code of its own.
    ///
    /// Nothing here refuses anything. A part that is wrong in some way is built as the nearest
    /// sensible shape; a part that cannot be built at all is skipped and the rest still stand.
    /// (Its predecessor validated strictly and rejected whole things for one bad field.)
    /// </summary>
    public static class Maker
    {
        public const int MaxParts = 48;

        public static AssetDef Make(Statement s)
        {
            string name = s.Str("name") ?? s.Str("id") ?? "thing";
            string kind = s.Str("kind", "thing");
            var parts = new List<Dictionary<string, object>>();
            if (s.Fields.TryGetValue("parts", out object raw) && raw is List<object> list)
                foreach (object item in list) if (item is Dictionary<string, object> part && parts.Count < MaxParts) parts.Add(part);
            Dictionary<string, object> fires = s.Obj("fires");
            bool tool = kind == "tool" || fires != null;
            s.TryVec3("grip", out Vector3 grip);

            var def = new AssetDef
            {
                Id = name, Desc = "Made: " + name.Replace('_', ' '), Made = true,
                Category = kind == "animal" ? "animals" : kind == "person" ? "people" : kind == "vehicle" || kind == "flyer" ? "vehicles" : "made",
                Recipe = mb =>
                {
                    mb.Shade(0.045f);
                    // a tool is drawn barrel-forward and held barrel-up: "up" is where the controller points
                    if (tool) mb.Push(Vector3.zero, new Vector3(-90f, 0f, 0f)).Push(-grip);
                    Build(mb, parts);
                    if (tool) mb.Pop().Pop();
                },
            };
            if (kind == "flyer") def.Flies();

            Bounds b = def.Bounds;   // builds the mesh
            if (def.Mesh.vertexCount == 0)
            {
                def.Recipe = mb => mb.Col(new Color(0.8f, 0.8f, 0.85f)).BoxOn(Vector3.zero, Vector3.one * 0.4f);
                def.Rebuild();
                b = def.Bounds;
            }
            def.Size = Mathf.Max(b.size.y, 0.01f);
            float volume = Mathf.Max(b.size.x * b.size.y * b.size.z, 0.0001f);
            def.Solid(Mathf.Clamp(volume * 60f, 0.2f, 300f), 0.2f);

            if (tool)
            {
                def.AsTool();
                if (fires != null)
                {
                    Statement f = Statement.Wrap(fires);
                    Vector3 muzzle;
                    if (f.TryVec3("muzzle", out Vector3 given)) muzzle = new Vector3(given.x - grip.x, given.z - grip.z, -(given.y - grip.y));
                    else muzzle = Tip(def.Mesh);
                    def.Muzzle = muzzle;
                    def.HasMuzzle = true;
                    def.ShotSound = f.Str("sound", "zap");
                    def.FireEvery = Mathf.Clamp(f.Num("every", 0.25f), 0.05f, 5f);
                    def.Automatic = f.Flag("auto");
                    def.ShotSpeed = Mathf.Clamp(f.Num("speed", 22f), 2f, 80f);
                    def.ShotSize = Mathf.Clamp(f.Num("size", 1f), 0.2f, 6f);
                    def.Pellets = Mathf.Clamp(f.Int("pellets", 1), 1, 12);
                    def.Spread = Mathf.Clamp(f.Num("spread", def.Pellets > 1 ? 5f : 0f), 0f, 30f);
                    def.ShotLife = def.Automatic ? 2.5f : 5f;
                    if (f.TryColor("color", out Color shot)) def.Shoots(shot);
                    def.Behavior = "blaster";
                }
            }
            Library.Register(def);
            SessionLog.Write("made", "name", name, "kind", kind, "parts", parts.Count, "triangles", def.Mesh.triangles.Length / 3,
                "size", b.size, "fires", fires != null);
            return def;
        }

        /// <summary>The front of the barrel: the middle of whatever reaches furthest along the tool's length.</summary>
        static Vector3 Tip(Mesh mesh)
        {
            Vector3[] verts = mesh.vertices;
            float top = float.NegativeInfinity;
            foreach (Vector3 v in verts) top = Mathf.Max(top, v.y);
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Vector3 v in verts) if (v.y > top - 0.015f) { sum += v; n++; }
            return n > 0 ? sum / n : new Vector3(0f, top, 0f);
        }

        // ------------------------------------------------------------------ parts

        static void Build(MeshBuilder mb, List<Dictionary<string, object>> parts)
        {
            var byId = new Dictionary<string, Dictionary<string, object>>();
            foreach (Dictionary<string, object> part in parts)
                if (part.TryGetValue("id", out object id) && id is string key && !byId.ContainsKey(key)) byId[key] = part;

            foreach (Dictionary<string, object> part in parts)
            {
                // what it hangs from, outermost first; a loop or a missing parent just ends the chain
                var chain = new List<Dictionary<string, object>> { part };
                Dictionary<string, object> at = part;
                bool mirror = Statement.Wrap(part).Has("mirror");
                while (chain.Count < 8 && at.TryGetValue("parent", out object p) && p is string parent && byId.TryGetValue(parent, out Dictionary<string, object> up) && !chain.Contains(up))
                {
                    chain.Insert(0, up);
                    mirror |= Statement.Wrap(up).Has("mirror");
                    at = up;
                }
                for (int side = 0; side < (mirror ? 2 : 1); side++)
                {
                    int before = mb.TriangleCount, depth = mb.Depth;
                    try { One(mb, chain, side == 1); }
                    catch (System.Exception e)
                    {
                        // one bad part must not cost the user the whole thing
                        mb.PopTo(depth);
                        mb.Emit(0f);
                        SessionLog.Write("made_part_skipped", "shape", Statement.Wrap(part).Str("shape"), "why", e.GetType().Name, "kept_triangles", before);
                    }
                }
            }
        }

        static void One(MeshBuilder mb, List<Dictionary<string, object>> chain, bool mirrored)
        {
            int pushed = 0;
            foreach (Dictionary<string, object> link in chain)
            {
                Statement l = Statement.Wrap(link);
                Vector3 position = l.Vec("position", l.Vec("at", Vector3.zero)), rotation = l.Vec("rotation", l.Vec("rot", Vector3.zero));
                if (mirrored) { position.x = -position.x; rotation = new Vector3(rotation.x, -rotation.y, -rotation.z); }
                mb.Push(position, rotation);
                pushed++;
            }
            Dictionary<string, object> part = chain[chain.Count - 1];
            Statement p = Statement.Wrap(part);
            string shape = p.Str("shape", "cube");
            // a shape that needs a drawing and has none is the nearest plain shape, at a plain shape's size
            if (shape == "lathe" && Pairs(part, "profile").Count < 2) shape = "cylinder";
            else if (shape == "slab" && Pairs(part, "outline").Count < 3) shape = "cube";
            else if (shape == "tube" && Points(part, "points").Count < 2) shape = "capsule";
            bool drawn = shape == "lathe" || shape == "slab" || shape == "tube";
            Vector3 s = Vector3.one * (drawn ? 1f : 0.3f);
            foreach (string key in new[] { "size", "scale" })
            {
                if (p.TryVec3(key, out Vector3 three)) s = three;
                else if (part.TryGetValue(key, out object one) && one is double d) s = Vector3.one * (float)d;
            }
            s = new Vector3(Mathf.Max(Mathf.Abs(s.x), 0.001f), Mathf.Max(Mathf.Abs(s.y), 0.001f), Mathf.Max(Mathf.Abs(s.z), 0.001f));

            mb.Emit(Mathf.Clamp(p.Num("emission"), 0f, 3f));
            Color color = p.TryColor("color", out Color given) ? given : new Color(0.78f, 0.78f, 0.82f);
            if (p.TryColor("color_top", out Color top))
            {
                float y0 = -s.y * 0.5f, y1 = s.y * 0.5f;
                if (shape == "dome") { y0 = 0f; y1 = s.y; }
                else if (drawn) Span(part, shape, s.y, ref y0, ref y1);
                mb.Grad(color, top, y0, y1);
            }
            else mb.Col(color);

            int copies = Mathf.Clamp(p.Int("repeat", 1), 1, 16);
            Vector3 step = p.Vec("step", Vector3.zero), turn = p.Vec("rotation", p.Vec("rot", Vector3.zero));
            if (mirrored) { step.x = -step.x; turn = new Vector3(turn.x, -turn.y, -turn.z); }
            step = Quaternion.Inverse(Quaternion.Euler(turn)) * step;   // `step` is measured like `position`, not along the turned part
            bool centred = shape != "dome" && !drawn;
            Vector3 anchor = !centred ? Vector3.zero : p.Str("anchor") == "top" ? Vector3.down * s.y * 0.5f : p.Str("anchor") == "bottom" ? Vector3.up * s.y * 0.5f : Vector3.zero;
            for (int k = 0; k < copies; k++)
            {
                mb.Push(step * k + anchor, Vector3.zero, mirrored ? new Vector3(-1f, 1f, 1f) : Vector3.one);
                Shape(mb, p, part, shape, s);
                mb.Pop();
            }
            mb.Emit(0f);
            for (int i = 0; i < pushed; i++) mb.Pop();
        }

        static void Span(Dictionary<string, object> part, string shape, float scaleY, ref float y0, ref float y1)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            if (shape == "tube") foreach (Vector3 v in Points(part, "points")) { lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y); }
            else foreach (Vector2 v in Pairs(part, shape == "lathe" ? "profile" : "outline")) { lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y); }
            if (lo < hi) { y0 = lo * scaleY; y1 = hi * scaleY; }
        }

        static List<Vector3> Points(Dictionary<string, object> part, string key)
        {
            var result = new List<Vector3>();
            if (part.TryGetValue(key, out object v) && v is List<object> list)
                foreach (object item in list)
                    if (item is List<object> p && p.Count == 3 && p[0] is double x && p[1] is double y && p[2] is double z) result.Add(new Vector3((float)x, (float)y, (float)z));
            return result;
        }

        static List<Vector2> Pairs(Dictionary<string, object> part, string key)
        {
            var result = new List<Vector2>();
            if (part.TryGetValue(key, out object v) && v is List<object> list)
                foreach (object item in list)
                    if (item is List<object> p && p.Count == 2 && p[0] is double x && p[1] is double y) result.Add(new Vector2((float)x, (float)y));
            return result;
        }

        static void Taper(Dictionary<string, object> part, out float x, out float z)
        {
            x = z = 1f;
            if (!part.TryGetValue("taper", out object t)) return;
            if (t is double n) x = z = (float)n;
            else if (t is List<object> l && l.Count == 2 && l[0] is double a && l[1] is double b) { x = (float)a; z = (float)b; }
            x = Mathf.Clamp(x, 0f, 3f);
            z = Mathf.Clamp(z, 0f, 3f);
        }

        static void Shape(MeshBuilder mb, Statement p, Dictionary<string, object> part, string shape, Vector3 s)
        {
            int sides = Mathf.Clamp(p.Int("sides", 12), 3, 24);
            Taper(part, out float tx, out float tz);
            float least = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            switch (shape)
            {
                case "sphere": mb.Sphere(Vector3.zero, s * 0.5f, Mathf.Max(s.x, Mathf.Max(s.y, s.z)) > 0.5f ? 2 : 1); break;
                case "blob": mb.Blob(Vector3.zero, s * 0.5f, Mathf.Clamp(p.Num("rough", 0.25f), 0f, 0.6f), p.Int("seed", 1), 1); break;
                case "cylinder": mb.Push(Vector3.zero, Vector3.zero, new Vector3(s.x, 1f, s.z)).Cyl(Vector3.down * s.y * 0.5f, 0.5f, 0.5f * tx, s.y, sides).Pop(); break;
                case "cone": mb.Push(Vector3.zero, Vector3.zero, new Vector3(s.x, 1f, s.z)).Cone(Vector3.down * s.y * 0.5f, 0.5f, s.y, sides).Pop(); break;
                case "capsule":
                    float height = Mathf.Max(s.y, s.x);
                    mb.Push(Vector3.zero, Vector3.zero, new Vector3(1f, 1f, s.z / s.x)).Capsule(Vector3.down * height * 0.5f, s.x * 0.5f, height, Mathf.Max(sides, 8)).Pop(); break;
                case "pyramid": mb.Push(Vector3.zero, Vector3.zero, s).Pyramid(Vector3.down * 0.5f, 1f, 1f).Pop(); break;
                case "torus":
                    float tube = Mathf.Clamp(p.Num("radius", 0.15f), 0.02f, 0.45f);
                    mb.Push(Vector3.zero, Vector3.zero, s).Torus(Vector3.zero, 0.5f - tube, tube, Mathf.Max(sides, 12), 6).Pop(); break;
                case "disc": mb.Push(Vector3.zero, Vector3.zero, new Vector3(s.x, 1f, s.z)).Cyl(Vector3.down * s.y * 0.5f, 0.5f, 0.5f, s.y, Mathf.Max(sides, 16)).Pop(); break;
                case "wedge": mb.Push(Vector3.zero, Vector3.zero, s).Wedge(Vector3.down * 0.5f, Vector3.one).Pop(); break;
                case "dome": mb.Push(Vector3.zero, Vector3.zero, new Vector3(s.x, 1f, s.z)).Dome(Vector3.zero, 0.5f, s.y, sides).Pop(); break;
                case "lathe":
                    List<Vector2> profile = Pairs(part, "profile");
                    if (profile.Count >= 2) mb.Push(Vector3.zero, Vector3.zero, s).Lathe(Vector3.zero, profile, sides).Pop();
                    else mb.Push(Vector3.zero, Vector3.zero, new Vector3(s.x, 1f, s.z)).Cyl(Vector3.zero, 0.5f, 0.5f, s.y, sides).Pop();
                    break;
                case "slab":
                    List<Vector2> outline = Pairs(part, "outline");
                    if (outline.Count >= 3) mb.Push(Vector3.zero, Vector3.zero, s).Slab(outline, Mathf.Max(p.Num("thickness", 0.05f), 0.002f)).Pop();
                    else mb.BevelBox(Vector3.zero, s, Mathf.Min(0.1f * least, 0.03f));
                    break;
                case "tube":
                    List<Vector3> points = Points(part, "points");
                    if (points.Count >= 2) mb.Push(Vector3.zero, Vector3.zero, s).Tube(points, Mathf.Max(p.Num("radius", 0.05f), 0.002f), Mathf.Clamp(p.Int("sides", 6), 3, 12)).Pop();
                    else mb.Capsule(Vector3.down * s.y * 0.5f, Mathf.Min(s.x, s.y) * 0.5f, s.y, 8);
                    break;
                default:
                    // a cube, and anything not recognised: edges worn a little, since a box with knife edges reads as a placeholder
                    mb.BevelBox(Vector3.zero, s, p.Has("bevel") ? Mathf.Clamp01(p.Num("bevel")) * 0.5f * least : Mathf.Min(0.1f * least, 0.03f), tx, tz);
                    break;
            }
        }
    }
}
