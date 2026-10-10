using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>Something that moves and should have a shadow under it. Made by <see cref="Shadows.Cast"/>.</summary>
    public sealed class ShadowCaster
    {
        public Transform Target;
        public Renderer Body;       // when set, the shadow follows this renderer's box, so it can tumble
        public Vector2 Half;        // half width and half length on the ground, before the target's scale
        public float Fade = 1f;     // 0 hides it
    }

    /// <summary>
    /// Soft shadows on the ground under everything that stands on it. Nothing here uses real
    /// shadow maps: each shadow is one flat instanced quad, which is what a standalone headset
    /// can afford for a forest. Still scenery gets its shadows when it is placed; things that
    /// move (creatures, props, vehicles) are followed every frame.
    /// </summary>
    public static class Shadows
    {
        const float Lift = 0.035f;
        static readonly int StrengthId = Shader.PropertyToID("_Strength"), GlobalId = Shader.PropertyToID("_ImShadow");
        static readonly List<ShadowCaster> Casters = new List<ShadowCaster>();
        static readonly Matrix4x4[] Scratch = new Matrix4x4[256];
        static readonly Bounds Everywhere = new Bounds(Vector3.zero, Vector3.one * 2000f);
        static Mesh _quad;
        static Material _material;
        static MaterialPropertyBlock _props;
        static float _strength;

        public static Material Material
        {
            get
            {
                if (_material == null) _material = new Material(Shader.Find("ImagineVR/Shadow")) { name = "ImagineShadow", enableInstancing = true };
                return _material;
            }
        }

        public static Mesh Quad
        {
            get
            {
                if (_quad == null)
                {
                    _quad = new Mesh { name = "Shadow" };
                    _quad.vertices = new[] { new Vector3(-1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, -1f) };
                    _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                    _quad.bounds = new Bounds(Vector3.zero, new Vector3(2f, 0.2f, 2f));
                }
                return _quad;
            }
        }

        /// <summary>Follows a moving thing with a shadow until it is destroyed.</summary>
        public static ShadowCaster Cast(Transform target, Bounds local, Renderer body = null)
        {
            var caster = new ShadowCaster { Target = target, Body = body, Half = new Vector2(local.extents.x, local.extents.z) };
            Casters.Add(caster);
            return caster;
        }

        /// <summary>
        /// Whether a piece of still scenery should have a shadow, and how big. Not the tiny (grass),
        /// the flat (roads, rugs, ponds), the enormous (a mountain), or anything hanging in the air.
        /// </summary>
        public static bool Wanted(Bounds mesh, Vector3 scale, float lift, out Vector2 half)
        {
            half = new Vector2(mesh.extents.x * scale.x, mesh.extents.z * scale.z);
            float wide = Mathf.Max(half.x, half.y), tall = mesh.size.y * scale.y;
            if (wide < 0.2f || wide > 9f) return false;
            if (tall < 0.15f || tall < wide * 0.15f) return false;
            return lift + mesh.min.y * scale.y < 0.8f;
        }

        /// <summary>A shadow lying on the ground at (x, z), tilted to the slope there.</summary>
        public static Matrix4x4 Matrix(TerrainSystem terrain, float x, float z, float floor, Quaternion yaw, Vector2 half, float fade)
        {
            const float d = 0.6f;
            var normal = new Vector3(terrain.HeightAt(x - d, z) - terrain.HeightAt(x + d, z), 2f * d, terrain.HeightAt(x, z - d) - terrain.HeightAt(x, z + d));
            float ground = terrain.HeightAt(x, z);
            if (floor > ground) { ground = floor; normal = Vector3.up; }   // on water, or a floor
            normal.Normalize();
            float wide = Mathf.Max(half.x, half.y);
            var position = new Vector3(x, ground, z) + normal * (Lift + wide * 0.02f);
            return Matrix4x4.TRS(position, Quaternion.FromToRotation(Vector3.up, normal) * yaw, new Vector3(half.x * 1.2f, Mathf.Clamp01(fade), half.y * 1.2f));
        }

        public static void Draw(Matrix4x4[] matrices, int count, Bounds bounds, float strength)
        {
            if (count <= 0 || strength <= 0.001f || _strength <= 0.001f) return;
            if (_props == null) _props = new MaterialPropertyBlock();
            _props.SetFloat(StrengthId, strength);
            var rp = new RenderParams(Material)
            {
                matProps = _props,
                worldBounds = bounds,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMeshInstanced(rp, Quad, 0, matrices, count);
        }

        /// <summary>Once a frame: sets how dark shadows are, and draws the ones under moving things.</summary>
        public static void Render(World world, float dt)
        {
            // Shadows need ground to lie on and light to be cast by. Night keeps a faint one: it is
            // what makes a thing look like it is standing there rather than pasted on.
            float wanted = 0f;
            if (!world.IsDark && world.Terrain.HasGround)
            {
                SkyState sky = world.Sky.Current;
                wanted = Mathf.Lerp(0.4f, 0.78f, Mathf.Clamp01(sky.LightIntensity)) * (1f - 0.3f * Mathf.Clamp01(sky.Night));
            }
            _strength = Mathf.MoveTowards(_strength, wanted, dt * 0.4f);
            Shader.SetGlobalFloat(GlobalId, _strength);
            if (_strength <= 0.001f) { Casters.RemoveAll(c => c.Target == null); return; }

            TerrainSystem terrain = world.Terrain;
            float water = terrain.WaterY;
            int n = 0;
            for (int i = Casters.Count - 1; i >= 0; i--)
            {
                ShadowCaster c = Casters[i];
                if (c.Target == null) { Casters.RemoveAt(i); continue; }
                if (c.Fade <= 0.001f || !c.Target.gameObject.activeInHierarchy) continue;
                if (c.Body != null && !c.Body.enabled) continue;

                Vector3 at;
                Vector2 half;
                Quaternion yaw;
                float bottom;
                if (c.Body != null)
                {
                    Bounds b = c.Body.bounds;
                    at = b.center;
                    half = new Vector2(b.extents.x, b.extents.z);
                    yaw = Quaternion.identity;
                    bottom = b.min.y;
                }
                else
                {
                    at = c.Target.position;
                    Vector3 s = c.Target.lossyScale;
                    half = new Vector2(c.Half.x * s.x, c.Half.y * s.z);
                    yaw = Quaternion.Euler(0f, c.Target.eulerAngles.y, 0f);
                    bottom = at.y;
                }
                float wide = Mathf.Max(half.x, half.y);
                if (wide < 0.02f) continue;
                float floor = float.IsNaN(water) ? float.NegativeInfinity : water;
                float ground = Mathf.Max(terrain.HeightAt(at.x, at.z), floor);
                // the higher it is, the wider and fainter its shadow, until it is gone
                float height = Mathf.Max(0f, bottom - ground);
                float fade = c.Fade * (1f - Mathf.Clamp01(height / (4f + wide * 6f)));
                if (fade <= 0.01f) continue;
                Scratch[n++] = Matrix(terrain, at.x, at.z, floor, yaw, half * (1f + height * 0.12f), fade);
                if (n == Scratch.Length) { Draw(Scratch, n, Everywhere, 1f); n = 0; }
            }
            Draw(Scratch, n, Everywhere, 1f);
        }
    }
}
