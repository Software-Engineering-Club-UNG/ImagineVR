using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// A small modelling toolkit for building low-poly, flat-shaded, vertex-coloured meshes in code.
    /// Every library asset is a short recipe written against this class.
    ///
    /// Conventions: +Y is up, the pivot is at the origin (usually the base of the object), units
    /// are metres. Winding is fixed automatically: every face is given an "outside" hint and is
    /// flipped if it faces the wrong way, so recipes never have to think about triangle order.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _verts = new List<Vector3>(512);
        readonly List<Vector3> _normals = new List<Vector3>(512);
        readonly List<Color> _colors = new List<Color>(512);
        readonly List<Vector2> _uv0 = new List<Vector2>(512);   // x: always-on emission, y: night emission
        readonly List<Vector2> _uv1 = new List<Vector2>(512);   // x: sway weight (or halo radius), y: halo night-only
        readonly List<int> _tris = new List<int>(1024);
        readonly List<int> _glowTris = new List<int>();

        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
        Matrix4x4 _xf = Matrix4x4.identity;

        Color _color = Color.white;
        bool _gradient;
        Color _gradBottom, _gradTop;
        float _gradY0, _gradY1;
        float _emitAlways, _emitNight;
        float _swayY0 = float.MaxValue, _swayY1;
        float _shade;      // per-face brightness variation
        float _jitter;     // per-vertex position noise, metres

        public int TriangleCount => (_tris.Count + _glowTris.Count) / 3;

        /// <summary>Size of the solid geometry from the last Build, ignoring halos.</summary>
        public Bounds SolidBounds { get; private set; }

        // ------------------------------------------------------------------ state

        public MeshBuilder Col(Color c) { _color = c; _gradient = false; return this; }
        public MeshBuilder Col(string hex) { return Col(Pal.Hex(hex)); }

        /// <summary>Colour runs from bottom to top between two heights (model space).</summary>
        public MeshBuilder Grad(Color bottom, Color top, float y0, float y1)
        {
            _gradient = true; _gradBottom = bottom; _gradTop = top; _gradY0 = y0; _gradY1 = Mathf.Max(y1, y0 + 0.0001f);
            return this;
        }

        /// <summary>Emission: `always` glows all the time, `night` only when the world is dark.</summary>
        public MeshBuilder Emit(float always = 0f, float night = 0f) { _emitAlways = always; _emitNight = night; return this; }

        /// <summary>Lit-window emission: its own colour by day, a warm glow once the world is dark.</summary>
        public MeshBuilder Window() { _emitAlways = 0f; _emitNight = 2f; return this; }

        /// <summary>Vertices between these heights sway in the wind, more towards the top.</summary>
        public MeshBuilder Sway(float fromY, float toY) { _swayY0 = fromY; _swayY1 = Mathf.Max(toY, fromY + 0.0001f); return this; }
        public MeshBuilder NoSway() { _swayY0 = float.MaxValue; return this; }

        /// <summary>Random brightness per face, for a hand-cut look. 0.08 is subtle.</summary>
        public MeshBuilder Shade(float amount) { _shade = amount; return this; }

        /// <summary>Random offset per vertex in metres. Shared corners move together, so no cracks.</summary>
        public MeshBuilder Jitter(float metres) { _jitter = metres; return this; }

        public MeshBuilder Push(Vector3 position, Vector3 eulerDegrees, Vector3 scale)
        {
            _stack.Push(_xf);
            _xf = _xf * Matrix4x4.TRS(position, Quaternion.Euler(eulerDegrees), scale);
            return this;
        }
        public MeshBuilder Push(Vector3 position) { return Push(position, Vector3.zero, Vector3.one); }
        public MeshBuilder Push(Vector3 position, Vector3 eulerDegrees) { return Push(position, eulerDegrees, Vector3.one); }
        public MeshBuilder Pop() { _xf = _stack.Pop(); return this; }
        public int Depth => _stack.Count;
        /// <summary>Back out to an earlier depth, after something failed part-way through a nest of pushes.</summary>
        public void PopTo(int depth) { while (_stack.Count > depth) Pop(); }

        // ------------------------------------------------------------------ core

        static float Hash(Vector3 p)
        {
            float h = Mathf.Sin(p.x * 127.1f + p.y * 311.7f + p.z * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        Vector3 Place(Vector3 local)
        {
            Vector3 p = _xf.MultiplyPoint3x4(local);
            if (_jitter > 0f)
            {
                var q = new Vector3(Mathf.Round(p.x * 200f), Mathf.Round(p.y * 200f), Mathf.Round(p.z * 200f));
                p += new Vector3(Hash(q) - 0.5f, Hash(q + new Vector3(7.3f, 1.1f, 3.7f)) - 0.5f, Hash(q + new Vector3(2.9f, 8.3f, 5.1f)) - 0.5f) * (2f * _jitter);
            }
            return p;
        }

        Color ColorAt(Vector3 p, float faceShade)
        {
            Color c = _gradient ? Color.Lerp(_gradBottom, _gradTop, Mathf.Clamp01((p.y - _gradY0) / (_gradY1 - _gradY0))) : _color;
            c.r *= faceShade; c.g *= faceShade; c.b *= faceShade; c.a = 1f;
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
        }

        /// <summary>
        /// Adds one triangle. `outside` is any direction (local space) that points away from the
        /// solid at this face; the triangle is wound so its normal agrees with it.
        /// </summary>
        public MeshBuilder Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outside)
        {
            Vector3 centroidLocal = (a + b + c) / 3f;
            Vector3 pa = Place(a), pb = Place(b), pc = Place(c);
            Vector3 n = Vector3.Cross(pb - pa, pc - pa);
            if (n.sqrMagnitude < 1e-12f) return this;
            Vector3 centroid = _xf.MultiplyPoint3x4(centroidLocal);
            Vector3 outsidePoint = _xf.MultiplyPoint3x4(centroidLocal + outside.normalized * 0.01f);
            if (Vector3.Dot(n, outsidePoint - centroid) < 0f)
            {
                Vector3 swap = pb; pb = pc; pc = swap;
                n = -n;
            }
            n.Normalize();
            float shade = _shade > 0f ? 1f + (Hash(centroid * 3.1f) - 0.5f) * 2f * _shade : 1f;
            int start = _verts.Count;
            AddVertex(pa, n, shade);
            AddVertex(pb, n, shade);
            AddVertex(pc, n, shade);
            _tris.Add(start); _tris.Add(start + 1); _tris.Add(start + 2);
            return this;
        }

        void AddVertex(Vector3 p, Vector3 n, float shade)
        {
            _verts.Add(p);
            _normals.Add(n);
            _colors.Add(ColorAt(p, shade));
            _uv0.Add(new Vector2(_emitAlways, _emitNight));
            float sway = _swayY0 == float.MaxValue ? 0f : Mathf.Clamp01((p.y - _swayY0) / (_swayY1 - _swayY0));
            _uv1.Add(new Vector2(sway * sway, 0f));
        }

        public MeshBuilder Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outside)
        {
            Tri(a, b, c, outside);
            Tri(a, c, d, outside);
            return this;
        }

        /// <summary>A flat quad visible from both sides (leaves, flags, signs).</summary>
        public MeshBuilder QuadTwoSided(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            Quad(a, b, c, d, n);
            Quad(a, b, c, d, -n);
            return this;
        }

        public MeshBuilder TriTwoSided(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            Tri(a, b, c, n);
            Tri(a, b, c, -n);
            return this;
        }

        // ------------------------------------------------------------------ primitives

        public MeshBuilder Box(Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z), p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z), p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z), p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z), p111 = center + new Vector3(h.x, h.y, h.z);
            Quad(p000, p100, p110, p010, Vector3.back);
            Quad(p001, p101, p111, p011, Vector3.forward);
            Quad(p000, p001, p011, p010, Vector3.left);
            Quad(p100, p101, p111, p110, Vector3.right);
            Quad(p010, p110, p111, p011, Vector3.up);
            Quad(p000, p100, p101, p001, Vector3.down);
            return this;
        }

        /// <summary>A box sitting on the ground plane of its local space: base centre, then size.</summary>
        public MeshBuilder BoxOn(Vector3 baseCenter, Vector3 size)
        {
            return Box(baseCenter + new Vector3(0f, size.y * 0.5f, 0f), size);
        }

        /// <summary>
        /// A surface of revolution around +Y. `profile` lists (radius, height) pairs from bottom
        /// to top. Radius 0 at an end closes it to a point; otherwise the end is left open, so add
        /// a (0, y) point for a cap.
        /// </summary>
        public MeshBuilder Lathe(Vector3 origin, IList<Vector2> profile, int sides = 8, float startAngle = 0f)
        {
            sides = Mathf.Max(3, sides);
            for (int i = 0; i + 1 < profile.Count; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[i + 1];
                if (p0.x <= 0f && p1.x <= 0f) continue;
                Vector2 dir = p1 - p0;
                var n2 = new Vector2(dir.y, -dir.x);   // outward in the (radius, height) plane
                for (int j = 0; j < sides; j++)
                {
                    float a0 = startAngle + (j / (float)sides) * Mathf.PI * 2f;
                    float a1 = startAngle + ((j + 1) / (float)sides) * Mathf.PI * 2f;
                    float am = (a0 + a1) * 0.5f;
                    var c0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var c1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    var outside = new Vector3(Mathf.Cos(am) * n2.x, n2.y, Mathf.Sin(am) * n2.x);
                    Vector3 b0 = origin + c0 * p0.x + Vector3.up * p0.y;
                    Vector3 b1 = origin + c1 * p0.x + Vector3.up * p0.y;
                    Vector3 t0 = origin + c0 * p1.x + Vector3.up * p1.y;
                    Vector3 t1 = origin + c1 * p1.x + Vector3.up * p1.y;
                    if (p0.x <= 0f) Tri(b0, t0, t1, outside);
                    else if (p1.x <= 0f) Tri(b0, b1, t0, outside);
                    else Quad(b0, b1, t1, t0, outside);
                }
            }
            return this;
        }

        static readonly List<Vector2> Scratch = new List<Vector2>(8);

        /// <summary>Cylinder or cone standing on `baseCenter`. Set a radius to 0 for a point.</summary>
        public MeshBuilder Cyl(Vector3 baseCenter, float radiusBottom, float radiusTop, float height, int sides = 8, bool caps = true)
        {
            if (height < 0f)
            {
                // hanging downward: build it upright and mirror it (winding is fixed automatically)
                Push(baseCenter, Vector3.zero, new Vector3(1f, -1f, 1f));
                Cyl(Vector3.zero, radiusBottom, radiusTop, -height, sides, caps);
                Pop();
                return this;
            }
            Scratch.Clear();
            if (caps && radiusBottom > 0f) Scratch.Add(new Vector2(0f, 0f));
            Scratch.Add(new Vector2(radiusBottom, 0f));
            Scratch.Add(new Vector2(radiusTop, height));
            if (caps && radiusTop > 0f) Scratch.Add(new Vector2(0f, height));
            return Lathe(baseCenter, new List<Vector2>(Scratch), sides, Mathf.PI / sides);
        }

        public MeshBuilder Cone(Vector3 baseCenter, float radius, float height, int sides = 8)
        {
            return Cyl(baseCenter, radius, 0f, height, sides, true);
        }

        /// <summary>A cylinder between two arbitrary points: limbs, branches, pipes, rails.</summary>
        public MeshBuilder Rod(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int sides = 6, bool caps = true)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 1e-5f) return this;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis / length);
            Push(from, rot.eulerAngles, Vector3.one);
            Cyl(Vector3.zero, radiusFrom, radiusTo, length, sides, caps);
            Pop();
            return this;
        }

        public MeshBuilder Rod(Vector3 from, Vector3 to, float radius, int sides = 6) { return Rod(from, to, radius, radius, sides, true); }

        // Icosphere cache: unit-sphere triangles per detail level.
        static readonly Dictionary<int, Vector3[]> SphereCache = new Dictionary<int, Vector3[]>();

        static Vector3[] UnitSphere(int detail)
        {
            detail = Mathf.Clamp(detail, 0, 3);
            if (SphereCache.TryGetValue(detail, out Vector3[] cached)) return cached;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            var tris = new List<Vector3>();
            for (int i = 0; i < f.Length; i++) tris.Add(v[f[i]].normalized);
            for (int d = 0; d < detail; d++)
            {
                var next = new List<Vector3>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + c) * 0.5f).normalized, ca = ((c + a) * 0.5f).normalized;
                    next.Add(a); next.Add(ab); next.Add(ca);
                    next.Add(b); next.Add(bc); next.Add(ab);
                    next.Add(c); next.Add(ca); next.Add(bc);
                    next.Add(ab); next.Add(bc); next.Add(ca);
                }
                tris = next;
            }
            Vector3[] result = tris.ToArray();
            SphereCache[detail] = result;
            return result;
        }

        /// <summary>Sphere or ellipsoid. detail 0 = 20 faces, 1 = 80, 2 = 320.</summary>
        public MeshBuilder Sphere(Vector3 center, Vector3 radii, int detail = 1)
        {
            Vector3[] unit = UnitSphere(detail);
            for (int i = 0; i < unit.Length; i += 3)
            {
                Vector3 a = unit[i], b = unit[i + 1], c = unit[i + 2];
                Tri(center + Vector3.Scale(a, radii), center + Vector3.Scale(b, radii), center + Vector3.Scale(c, radii), a + b + c);
            }
            return this;
        }

        public MeshBuilder Sphere(Vector3 center, float radius, int detail = 1) { return Sphere(center, new Vector3(radius, radius, radius), detail); }

        /// <summary>A lumpy sphere: rocks, foliage, clouds, bushes. `rough` 0.2 is gentle, 0.5 is craggy.</summary>
        public MeshBuilder Blob(Vector3 center, Vector3 radii, float rough, int seed, int detail = 1)
        {
            Vector3[] unit = UnitSphere(detail);
            for (int i = 0; i < unit.Length; i += 3)
            {
                Vector3 a = unit[i], b = unit[i + 1], c = unit[i + 2];
                Tri(center + Lump(a, radii, rough, seed), center + Lump(b, radii, rough, seed), center + Lump(c, radii, rough, seed), a + b + c);
            }
            return this;
        }

        public MeshBuilder Blob(Vector3 center, float radius, float rough, int seed, int detail = 1)
        {
            return Blob(center, new Vector3(radius, radius, radius), rough, seed, detail);
        }

        static Vector3 Lump(Vector3 dir, Vector3 radii, float rough, int seed)
        {
            float h = Hash(dir * 5.7f + new Vector3(seed * 1.31f, seed * 0.73f, seed * 2.17f));
            return Vector3.Scale(dir, radii) * (1f + (h - 0.5f) * 2f * rough);
        }

        /// <summary>A convex outline on the ground plane, extruded from y0 to y1.</summary>
        public MeshBuilder Prism(IList<Vector2> outline, float y0, float y1, bool caps = true)
        {
            int n = outline.Count;
            if (n < 3) return this;
            Vector2 mid = Vector2.zero;
            for (int i = 0; i < n; i++) mid += outline[i];
            mid /= n;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                Vector2 edgeMid = (a + b) * 0.5f - mid;
                var outside = new Vector3(edgeMid.x, 0f, edgeMid.y);
                Quad(new Vector3(a.x, y0, a.y), new Vector3(b.x, y0, b.y), new Vector3(b.x, y1, b.y), new Vector3(a.x, y1, a.y), outside);
                if (caps)
                {
                    Tri(new Vector3(mid.x, y1, mid.y), new Vector3(a.x, y1, a.y), new Vector3(b.x, y1, b.y), Vector3.up);
                    Tri(new Vector3(mid.x, y0, mid.y), new Vector3(a.x, y0, a.y), new Vector3(b.x, y0, b.y), Vector3.down);
                }
            }
            return this;
        }

        /// <summary>A flat disc facing up (or down), for pads, puddles, lily pads, greens.</summary>
        public MeshBuilder Disc(Vector3 center, float radius, int sides = 12, bool twoSided = false)
        {
            for (int j = 0; j < sides; j++)
            {
                float a0 = (j / (float)sides) * Mathf.PI * 2f, a1 = ((j + 1) / (float)sides) * Mathf.PI * 2f;
                Vector3 p0 = center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Tri(center, p0, p1, Vector3.up);
                if (twoSided) Tri(center, p0, p1, Vector3.down);
            }
            return this;
        }

        /// <summary>A doughnut lying flat: rings, tyres, hoops, wreaths.</summary>
        public MeshBuilder Torus(Vector3 center, float ringRadius, float tubeRadius, int segments = 12, int sides = 6)
        {
            for (int i = 0; i < segments; i++)
            {
                float u0 = (i / (float)segments) * Mathf.PI * 2f, u1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                for (int j = 0; j < sides; j++)
                {
                    float v0 = (j / (float)sides) * Mathf.PI * 2f, v1 = ((j + 1) / (float)sides) * Mathf.PI * 2f;
                    Vector3 a = TorusPoint(center, ringRadius, tubeRadius, u0, v0);
                    Vector3 b = TorusPoint(center, ringRadius, tubeRadius, u1, v0);
                    Vector3 c = TorusPoint(center, ringRadius, tubeRadius, u1, v1);
                    Vector3 d = TorusPoint(center, ringRadius, tubeRadius, u0, v1);
                    float um = (u0 + u1) * 0.5f;
                    Vector3 ringPoint = center + new Vector3(Mathf.Cos(um), 0f, Mathf.Sin(um)) * ringRadius;
                    Quad(a, b, c, d, (a + b + c + d) * 0.25f - ringPoint);
                }
            }
            return this;
        }

        static Vector3 TorusPoint(Vector3 center, float ringRadius, float tubeRadius, float u, float v)
        {
            float r = ringRadius + Mathf.Cos(v) * tubeRadius;
            return center + new Vector3(Mathf.Cos(u) * r, Mathf.Sin(v) * tubeRadius, Mathf.Sin(u) * r);
        }

        /// <summary>A ramp: full height at +Z, zero at -Z. Sits on its base centre.</summary>
        public MeshBuilder Wedge(Vector3 baseCenter, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 a = baseCenter + new Vector3(-h.x, 0, -h.z), b = baseCenter + new Vector3(h.x, 0, -h.z);
            Vector3 c = baseCenter + new Vector3(h.x, 0, h.z), d = baseCenter + new Vector3(-h.x, 0, h.z);
            Vector3 e = baseCenter + new Vector3(h.x, size.y, h.z), f = baseCenter + new Vector3(-h.x, size.y, h.z);
            Quad(a, b, c, d, Vector3.down);
            Quad(d, c, e, f, Vector3.forward);
            Quad(a, b, e, f, new Vector3(0, size.z, -size.y));
            Tri(a, d, f, Vector3.left);
            Tri(b, c, e, Vector3.right);
            return this;
        }

        /// <summary>A four-sided pyramid on its base centre.</summary>
        public MeshBuilder Pyramid(Vector3 baseCenter, float width, float height)
        {
            return Lathe(baseCenter, new List<Vector2> { new Vector2(0, 0), new Vector2(width * 0.7071f, 0), new Vector2(0, height) }, 4, Mathf.PI / 4f);
        }

        /// <summary>A pill standing upright on its base centre.</summary>
        public MeshBuilder Capsule(Vector3 baseCenter, float radius, float height, int sides = 8)
        {
            float body = Mathf.Max(0f, height - radius * 2f);
            var profile = new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(radius * 0.7f, radius * 0.3f), new Vector2(radius, radius),
                new Vector2(radius, radius + body), new Vector2(radius * 0.7f, radius * 1.7f + body), new Vector2(0, radius * 2f + body),
            };
            return Lathe(baseCenter, profile, sides);
        }

        /// <summary>
        /// A box whose edges are cut off by `bevel` metres, and whose top may be narrower or wider
        /// than its base (`taperX`, `taperZ`: 1 is straight, 0 comes to a ridge). Made things have
        /// worn edges; a box with none reads as a placeholder.
        /// </summary>
        public MeshBuilder BevelBox(Vector3 center, Vector3 size, float bevel, float taperX = 1f, float taperZ = 1f)
        {
            Vector3 h = size * 0.5f;
            float narrow = Mathf.Min(1f, Mathf.Min(Mathf.Max(taperX, 0.02f), Mathf.Max(taperZ, 0.02f)));
            float b = Mathf.Clamp(bevel, 0f, Mathf.Min(h.x * narrow, h.y, h.z * narrow) * 0.98f);
            // every corner has three points, one on each face that meets there
            var onX = new Vector3[8]; var onY = new Vector3[8]; var onZ = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float sx = (i & 1) != 0 ? 1f : -1f, sy = (i & 2) != 0 ? 1f : -1f, sz = (i & 4) != 0 ? 1f : -1f;
                float tx = sy > 0f ? Mathf.Max(taperX, 0.02f) : 1f, tz = sy > 0f ? Mathf.Max(taperZ, 0.02f) : 1f;
                Vector3 c = center + new Vector3(sx * h.x * tx, sy * h.y, sz * h.z * tz);
                onX[i] = c - new Vector3(0f, sy * b, sz * b);
                onY[i] = c - new Vector3(sx * b, 0f, sz * b);
                onZ[i] = c - new Vector3(sx * b, sy * b, 0f);
            }
            Vector3[][] on = { onX, onY, onZ };
            int[] bit = { 1, 2, 4 };
            for (int axis = 0; axis < 3; axis++)
            {
                int u = bit[(axis + 1) % 3], v = bit[(axis + 2) % 3];
                foreach (int side in new[] { 0, bit[axis] })
                {
                    Vector3 outside = Vector3.zero;
                    outside[axis] = side != 0 ? 1f : -1f;
                    Quad(on[axis][side], on[axis][side | u], on[axis][side | u | v], on[axis][side | v], outside);
                }
            }
            if (b <= 0.00001f) return this;
            for (int axis = 0; axis < 3; axis++)
            {
                // the four edges that run along this axis, each a strip between its two faces
                int f1 = (axis + 1) % 3, f2 = (axis + 2) % 3;
                for (int k = 0; k < 4; k++)
                {
                    int c0 = ((k & 1) != 0 ? bit[f1] : 0) | ((k & 2) != 0 ? bit[f2] : 0), c1 = c0 | bit[axis];
                    Vector3 outside = Vector3.zero;
                    outside[f1] = (k & 1) != 0 ? 1f : -1f;
                    outside[f2] = (k & 2) != 0 ? 1f : -1f;
                    Quad(on[f1][c0], on[f1][c1], on[f2][c1], on[f2][c0], outside);
                }
            }
            for (int i = 0; i < 8; i++)
                Tri(onX[i], onY[i], onZ[i], new Vector3((i & 1) != 0 ? 1f : -1f, (i & 2) != 0 ? 1f : -1f, (i & 4) != 0 ? 1f : -1f));
            return this;
        }

        /// <summary>Half a ball, flat side down on `baseCenter`: helmets, shells, caps, igloos, mounds.</summary>
        public MeshBuilder Dome(Vector3 baseCenter, float radius, float height, int sides = 10, int rings = 4)
        {
            var profile = new List<Vector2> { new Vector2(0f, 0f) };
            for (int k = 0; k <= rings; k++)
            {
                float a = k / (float)rings * Mathf.PI * 0.5f;
                profile.Add(new Vector2(k == rings ? 0f : Mathf.Cos(a) * radius, Mathf.Sin(a) * height));
            }
            return Lathe(baseCenter, profile, sides);
        }

        /// <summary>
        /// A silhouette cut out of a board. `outline` is the side view, (forward, up) pairs going
        /// round the shape once; the board is `thickness` across, centred on x = 0. It need not be
        /// convex: a rifle stock, a fish, a key, a fin, a crescent.
        /// </summary>
        public MeshBuilder Slab(IList<Vector2> outline, float thickness)
        {
            int n = outline.Count;
            if (n < 3) return this;
            float area = 0f;
            for (int i = 0; i < n; i++) { Vector2 a = outline[i], b = outline[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            var pts = new List<Vector2>(outline);
            if (area < 0f) pts.Reverse();   // counter-clockwise from here on
            float hx = thickness * 0.5f;
            Vector3 At(Vector2 p, float x) => new Vector3(x, p.y, p.x);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n], d = b - a;
                Quad(At(a, -hx), At(b, -hx), At(b, hx), At(a, hx), new Vector3(0f, -d.x, d.y));
            }
            List<int> tris = Triangulate(pts);
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector2 a = pts[tris[i]], b = pts[tris[i + 1]], c = pts[tris[i + 2]];
                Tri(At(a, hx), At(b, hx), At(c, hx), Vector3.right);
                Tri(At(a, -hx), At(b, -hx), At(c, -hx), Vector3.left);
            }
            return this;
        }

        /// <summary>Ear clipping for a small counter-clockwise outline. Whatever it cannot cut is fanned, so it always returns.</summary>
        static List<int> Triangulate(List<Vector2> p)
        {
            var left = new List<int>(p.Count);
            for (int i = 0; i < p.Count; i++) left.Add(i);
            var tris = new List<int>((p.Count - 2) * 3);
            float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            int guard = 0;
            while (left.Count > 3 && guard++ < 400)
            {
                bool cut = false;
                for (int i = 0; i < left.Count && !cut; i++)
                {
                    int ia = left[(i + left.Count - 1) % left.Count], ib = left[i], ic = left[(i + 1) % left.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if (Cross(a, b, c) <= 1e-9f) continue;   // a corner that turns inward is not an ear
                    bool holds = false;
                    foreach (int k in left)
                    {
                        if (k == ia || k == ib || k == ic) continue;
                        Vector2 q = p[k];
                        if (Cross(a, b, q) >= 0f && Cross(b, c, q) >= 0f && Cross(c, a, q) >= 0f) { holds = true; break; }
                    }
                    if (holds) continue;
                    tris.Add(ia); tris.Add(ib); tris.Add(ic);
                    left.RemoveAt(i);
                    cut = true;
                }
                if (!cut) break;
            }
            for (int i = 1; i + 1 < left.Count; i++) { tris.Add(left[0]); tris.Add(left[i]); tris.Add(left[i + 1]); }
            return tris;
        }

        /// <summary>A tube following a path of points: arches, vines, handles, rails.</summary>
        public MeshBuilder Tube(IList<Vector3> path, float radius, int sides = 5)
        {
            for (int i = 0; i + 1 < path.Count; i++) Rod(path[i], path[i + 1], radius, radius, sides, i == 0 || i == path.Count - 2);
            return this;
        }

        /// <summary>
        /// A soft halo that always faces the viewer, drawn additively. `nightOnly` halos fade in
        /// as the world gets dark (street lamps, windows).
        /// </summary>
        public MeshBuilder Glow(Vector3 position, float radius, Color color, bool nightOnly = false)
        {
            Vector3 p = _xf.MultiplyPoint3x4(position);
            float scale = _xf.lossyScale.x;
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            int start = _verts.Count;
            for (int k = 0; k < 4; k++)
            {
                _verts.Add(p);
                _normals.Add(Vector3.up);
                _colors.Add(c);
                _uv0.Add(new Vector2((k == 0 || k == 3) ? -1f : 1f, k < 2 ? -1f : 1f));
                _uv1.Add(new Vector2(radius * scale, nightOnly ? 1f : 0f));
            }
            _glowTris.Add(start); _glowTris.Add(start + 1); _glowTris.Add(start + 2);
            _glowTris.Add(start); _glowTris.Add(start + 2); _glowTris.Add(start + 3);
            return this;
        }

        // ------------------------------------------------------------------ output

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (_verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, _uv0);
            mesh.SetUVs(1, _uv1);
            bool hasGlow = _glowTris.Count > 0;
            mesh.subMeshCount = hasGlow ? 2 : 1;
            mesh.SetTriangles(_tris, 0, false);
            if (hasGlow) mesh.SetTriangles(_glowTris, 1, false);
            mesh.RecalculateBounds();
            SolidBounds = mesh.bounds;
            if (hasGlow && _tris.Count > 0)
            {
                // the solid part alone: halos are single points and must not count towards size
                var solid = new Bounds(_verts[_tris[0]], Vector3.zero);
                for (int i = 0; i < _tris.Count; i++) solid.Encapsulate(_verts[_tris[i]]);
                SolidBounds = solid;
            }
            if (hasGlow)
            {
                // halos extend past their single-point geometry
                Bounds b = mesh.bounds;
                b.Expand(1.5f);
                mesh.bounds = b;
            }
            return mesh;
        }
    }
}
