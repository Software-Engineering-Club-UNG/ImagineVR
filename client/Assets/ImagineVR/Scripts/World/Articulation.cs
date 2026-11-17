using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>One piece of an asset that moves on its own: a wheel, a rotor, a steering wheel, a sail, a wing.</summary>
    public sealed class PartDef
    {
        public string Role = "";
        public Vector3 Pivot;            // in the asset's own space
        public Vector3 Axis = Vector3.up;   // the piece's own up: a rotor's shaft, a cylinder's length
        public Mesh Mesh;                // positions relative to the pivot
        public float Radius = 0.3f;      // for a wheel: how far it rolls in one turn is 2πr
        public Vector3 Spin, Swing;      // `anim`: degrees a second about X, Y, Z; degrees either way
        public float Every = 2f, Bob;
        public string When = "always";   // always, moving, alive, throttle
    }

    /// <summary>
    /// Draws an asset as a body and its moving pieces, and moves them: wheels roll with the speed and
    /// the front pair steer, rotors spin with the throttle, a steering wheel turns with the steering,
    /// wings beat, and anything with an `anim` turns or rocks as it says.
    ///
    /// Whatever carries it (the ride, a car in traffic, a windmill standing in a field) sets Speed,
    /// Steer and Throttle each frame; nothing else needs to know the pieces are there.
    /// </summary>
    public sealed class Articulated : MonoBehaviour
    {
        public AssetDef Def;
        /// <summary>Metres a second, forwards positive. Rolls the wheels.</summary>
        public float Speed;
        /// <summary>-1 full left .. 1 full right.</summary>
        public float Steer;
        /// <summary>0..1: how hard the engine is working. Spins rotors and propellers.</summary>
        public float Throttle = 1f;
        public bool Moving => Mathf.Abs(Speed) > 0.2f;

        Transform[] _pieces;
        PartDef[] _defs;
        float[] _angles;
        float _clock, _frontZ;
        MeshRenderer _body;

        /// <summary>
        /// Gives a host object the asset's look: one renderer when nothing on it moves, or a body and
        /// a child for every moving piece. With `body` false only the pieces are drawn (the body is
        /// already drawn elsewhere, combined with the scenery). Returns null when there are no pieces.
        /// </summary>
        public static Articulated Attach(GameObject host, AssetDef def, Color tint, bool body = true)
        {
            Mesh full = def.Mesh;      // builds the parts too
            World world = World.Instance;
            Material lit = world != null ? world.Objects.LitMaterial : new Material(Shader.Find("ImagineVR/Lit"));
            Material glow = world != null ? world.Objects.GlowMaterial : lit;
            var props = new MaterialPropertyBlock();
            props.SetColor("_Tint", tint);
            props.SetFloat("_Appear", 1f);
            if (def.Parts.Count == 0)
            {
                if (body) Renderer(host, full, lit, glow, props);
                return null;
            }
            Articulated a = host.AddComponent<Articulated>();
            a.Def = def;
            if (body) a._body = Renderer(host, def.BodyMesh ?? full, lit, glow, props);
            a._pieces = new Transform[def.Parts.Count];
            a._defs = def.Parts.ToArray();
            a._angles = new float[def.Parts.Count];
            Bounds b = def.Bounds;
            a._frontZ = b.center.z;
            for (int i = 0; i < def.Parts.Count; i++)
            {
                PartDef part = def.Parts[i];
                var go = new GameObject("Part_" + part.Role + i);
                go.transform.SetParent(host.transform, false);
                go.transform.localPosition = part.Pivot;
                Renderer(go, part.Mesh, lit, glow, props);
                a._pieces[i] = go.transform;
            }
            return a;
        }

        static MeshRenderer Renderer(GameObject go, Mesh mesh, Material lit, Material glow, MaterialPropertyBlock props)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>() ?? go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.GetComponent<MeshRenderer>() ?? go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mesh.subMeshCount > 1 ? new[] { lit, glow } : new[] { lit };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.SetPropertyBlock(props);
            return mr;
        }

        public void SetTint(Color tint)
        {
            var props = new MaterialPropertyBlock();
            props.SetColor("_Tint", tint);
            props.SetFloat("_Appear", 1f);
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>()) r.SetPropertyBlock(props);
        }

        void LateUpdate()
        {
            if (_pieces == null) return;
            float dt = Time.deltaTime;
            _clock += dt;
            for (int i = 0; i < _pieces.Length; i++)
            {
                Transform t = _pieces[i];
                if (t == null) continue;
                PartDef p = _defs[i];
                Quaternion turn = Quaternion.identity;
                switch (p.Role)
                {
                    case "wheel":
                    {
                        // rolls about its axle (across the vehicle), and the front ones turn with the steering
                        _angles[i] += Speed / Mathf.Max(p.Radius, 0.05f) * Mathf.Rad2Deg * dt;
                        float yaw = p.Pivot.z > _frontZ + 0.1f ? Steer * 28f : 0f;
                        turn = Quaternion.Euler(0f, yaw, 0f) * Quaternion.AngleAxis(_angles[i], Vector3.right);
                        break;
                    }
                    case "spinner":
                        _angles[i] += Mathf.Lerp(120f, 1400f, Mathf.Clamp01(Throttle)) * dt;
                        turn = Quaternion.AngleAxis(_angles[i], p.Axis);
                        break;
                    case "steer":
                        turn = Quaternion.AngleAxis(Steer * 120f, p.Axis);
                        break;
                    case "flap":
                    {
                        // wings beat about the line from the body outwards; a mirrored twin beats the other way
                        float side = p.Pivot.x >= 0f ? 1f : -1f;
                        float rate = Moving ? 2.2f : 0.6f;
                        turn = Quaternion.AngleAxis(Mathf.Sin(_clock * rate * Mathf.PI * 2f) * 28f * side, Vector3.forward);
                        break;
                    }
                    case "door":
                        turn = Quaternion.identity;
                        break;
                }
                bool on = p.When == "always" || p.When == "alive" || (p.When == "moving" && Moving) || (p.When == "throttle" && Throttle > 0.05f);
                if (on && p.Spin != Vector3.zero) turn = turn * Quaternion.Euler(p.Spin * _clock);
                if (on && p.Swing != Vector3.zero) turn = turn * Quaternion.Euler(p.Swing * Mathf.Sin(_clock * Mathf.PI * 2f / Mathf.Max(p.Every, 0.05f)));
                t.localRotation = turn;
                t.localPosition = p.Pivot + (on && p.Bob > 0f ? Vector3.up * Mathf.Sin(_clock * Mathf.PI * 2f / Mathf.Max(p.Every, 0.05f)) * p.Bob : Vector3.zero);
            }
        }
    }
}
