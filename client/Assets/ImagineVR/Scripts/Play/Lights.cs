using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// The lights the custom shaders know: twelve, published as global arrays. The first four are
    /// held (flashlights, lanterns: HeldLights); the other eight are the nearest lit things in the
    /// world (a `light` on a lamp, a campfire, a beacon) and the headlights of what the user drives.
    /// They count for more after dark. There are no Unity lights anywhere in this project.
    /// </summary>
    public static class Lights
    {
        const int Count = 12, HeldSlots = 4;
        static readonly Vector4[] Positions = new Vector4[Count], Directions = new Vector4[Count], Colors = new Vector4[Count];
        static readonly List<LightSource> Nearest = new List<LightSource>();
        static float _nextSort;
        static bool _dirty = true;

        /// <summary>The held lights changed: copied into the first four slots.</summary>
        public static void Held(Vector4[] positions, Vector4[] directions, Vector4[] colors)
        {
            for (int i = 0; i < HeldSlots && i < positions.Length; i++) { Positions[i] = positions[i]; Directions[i] = directions[i]; Colors[i] = colors[i]; }
            _dirty = true;
            Upload();
        }

        public static void Publish(World world)
        {
            if (world == null || world.Head == null) return;
            Vector3 head = world.Head.position;
            if (Time.time >= _nextSort)
            {
                _nextSort = Time.time + 0.5f;
                Nearest.Clear();
                foreach (LightSource l in LightSource.All) if (l != null && l.isActiveAndEnabled && (l.transform.position - head).sqrMagnitude < (l.Range * 6f) * (l.Range * 6f)) Nearest.Add(l);
                Nearest.Sort((a, b) => (a.transform.position - head).sqrMagnitude.CompareTo((b.transform.position - head).sqrMagnitude));
            }
            float night = Shader.GetGlobalFloat("_ImNight");
            float strength = 0.35f + 0.65f * night;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            int slot = HeldSlots;
            // headlights first: what the user drives lights the road ahead of them
            Vehicle v = world.Vehicle;
            if (v != null && v.Riding && night > 0.3f && v.Lamps(out Vector3 lamp, out Vector3 ahead))
            {
                Positions[slot] = new Vector4(lamp.x, lamp.y, lamp.z, 28f);
                Directions[slot] = new Vector4(ahead.x, ahead.y, ahead.z, 0.82f);
                Color c = linear ? new Color(1f, 0.95f, 0.85f).linear : new Color(1f, 0.95f, 0.85f);
                Colors[slot] = new Vector4(c.r, c.g, c.b, 2.2f * night);
                slot++;
            }
            for (int i = 0; i < Nearest.Count && slot < Count; i++)
            {
                LightSource l = Nearest[i];
                if (l == null) continue;
                Vector3 at = l.transform.position + l.transform.rotation * l.Offset;
                Color c = linear ? l.Color.linear : l.Color;
                float flicker = l.name.Contains("fire") || l.name.Contains("torch") ? 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, i) : 1f;
                Positions[slot] = new Vector4(at.x, at.y, at.z, l.Range);
                Directions[slot] = new Vector4(0f, -1f, 0f, -1f);     // all round
                Colors[slot] = new Vector4(c.r, c.g, c.b, 1.6f * l.Intensity * strength * flicker);
                slot++;
            }
            for (; slot < Count; slot++) { Positions[slot] = Vector4.zero; Colors[slot] = Vector4.zero; }
            _dirty = true;
            Upload();
        }

        static void Upload()
        {
            if (!_dirty) return;
            _dirty = false;
            // always all twelve: a global array's length is fixed the first time it is set
            Shader.SetGlobalVectorArray("_ImHeldLightPositions", Positions);
            Shader.SetGlobalVectorArray("_ImHeldLightDirections", Directions);
            Shader.SetGlobalVectorArray("_ImHeldLightColors", Colors);
        }
    }
}
