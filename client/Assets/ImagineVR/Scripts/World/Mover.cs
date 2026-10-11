using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>A route through the world: points joined by straight lines, each point's y a height above the ground.</summary>
    public sealed class Route
    {
        readonly List<Vector3> _points;
        readonly List<float> _at = new List<float>();   // distance along the route at each point
        public readonly bool Loop;
        public float Length { get; private set; }

        public Route(List<Vector3> points, bool loop)
        {
            _points = new List<Vector3>(points);
            Loop = loop;
            if (loop && _points.Count > 2) _points.Add(_points[0]);
            float d = 0f;
            for (int i = 0; i < _points.Count; i++)
            {
                if (i > 0) d += Vector3.Distance(Flat(_points[i - 1]), Flat(_points[i])) + 0.001f;
                _at.Add(d);
            }
            Length = Mathf.Max(d, 0.01f);
        }

        static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);

        /// <summary>Where the route is after `distance` metres, and which way it is heading there.</summary>
        public Vector3 At(float distance, out Vector3 heading)
        {
            distance = Loop ? Mathf.Repeat(distance, Length) : Mathf.Clamp(distance, 0f, Length);
            int i = 1;
            while (i < _points.Count - 1 && _at[i] < distance) i++;
            Vector3 a = _points[i - 1], b = _points[i];
            heading = b - a;
            return Vector3.Lerp(a, b, Mathf.InverseLerp(_at[i - 1], _at[i], distance));
        }
    }

    /// <summary>
    /// Something that travels a route by itself: traffic on a street, a rival on a race track,
    /// a ship crossing the bay, a UFO coming in to land.
    /// </summary>
    public sealed class Mover : MonoBehaviour
    {
        World _world;
        Route _route;
        float _speed, _distance;

        public static GameObject Create(World world, AssetDef def, Route route, float speed, float start, Vector3 scale, Color tint, string groupId = null)
        {
            var go = new GameObject("Mover_" + def.Id);
            go.transform.SetParent(world.transform, false);
            go.transform.localScale = scale;
            Mesh mesh = def.Mesh;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterials = mesh.subMeshCount > 1
                ? new[] { world.Objects.LitMaterial, world.Objects.GlowMaterial }
                : new[] { world.Objects.LitMaterial };
            var props = new MaterialPropertyBlock();
            props.SetColor("_Tint", tint);
            props.SetFloat("_Appear", 1f);
            mr.SetPropertyBlock(props);

            Shadows.Cast(go.transform, def.Bounds);
            Mover m = go.AddComponent<Mover>();
            m._world = world;
            m._route = route;
            m._speed = speed;
            m._distance = Mathf.Clamp01(start) * route.Length;
            m.Place(1f);
            return go;
        }

        void Update()
        {
            _distance += _speed * Time.deltaTime;
            Place(1f - Mathf.Exp(-Time.deltaTime * 6f));
        }

        void Place(float turn)
        {
            Vector3 p = _route.At(_distance, out Vector3 heading);
            p.y += _world.Terrain.HeightAt(p.x, p.z);
            transform.position = p;
            if (heading.sqrMagnitude > 0.0001f && (_route.Loop || _distance < _route.Length))
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), turn);
        }
    }
}
