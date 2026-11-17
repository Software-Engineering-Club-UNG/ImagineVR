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
        float _speed, _distance, _now, _roll, _lastYaw;
        bool _flies;
        Articulated _parts;
        static readonly Collider[] Ahead = new Collider[12];

        /// <summary>How fast it goes when nothing is in its way, in metres a second.</summary>
        public float Speed { get => _speed; set => _speed = Mathf.Clamp(value, 0f, 80f); }

        public static GameObject Create(World world, AssetDef def, Route route, float speed, float start, Vector3 scale, Color tint, string groupId = null)
        {
            var go = new GameObject("Mover_" + def.Id);
            go.transform.SetParent(world.transform, false);
            go.transform.localScale = scale;
            Mover m = go.AddComponent<Mover>();
            // wheels roll, propellers spin, rotors turn: whatever moves on it moves
            m._parts = Articulated.Attach(go, def, tint);
            Shadows.Cast(go.transform, def.Bounds);
            m._world = world;
            m._route = route;
            m._speed = m._now = speed;
            m._flies = def.FliesRidden;
            m._distance = Mathf.Clamp01(start) * route.Length;
            m.Place(1f, 0f);
            return go;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // traffic stops for what is in front of it: the car ahead, the user, a pedestrian. Flyers do not.
            float want = _speed;
            if (!_flies && _speed > 0.5f && Time.frameCount % 4 == (GetInstanceID() & 3)) _blocked = Blocked();
            if (_blocked) want = 0f;
            _now = Mathf.MoveTowards(_now, want, (want < _now ? 8f : 3f) * dt);
            _distance += _now * dt;
            Place(1f - Mathf.Exp(-dt * 6f), dt);
        }
        bool _blocked;

        bool Blocked()
        {
            Vector3 forward = transform.forward;
            Vector3 probe = transform.position + forward * (4f + _now * 0.4f) + Vector3.up;
            int n = Physics.OverlapSphereNonAlloc(probe, 2f, Ahead, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = Ahead[i];
                if (c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<Mover>() != null || c.GetComponentInParent<Creature>() != null || c.GetComponentInParent<Rig>() != null || c is CharacterController) return true;
            }
            // the user on foot, or what they ride, standing in the road
            Transform head = _world.Head;
            if (head != null)
            {
                Vector3 to = head.position - transform.position;
                to.y = 0f;
                if (to.magnitude < 7f && Vector3.Dot(to.normalized, forward) > 0.8f) return true;
            }
            return false;
        }

        void Place(float turn, float dt)
        {
            Vector3 p = _route.At(_distance, out Vector3 heading);
            p.y += _world.Terrain.HeightAt(p.x, p.z);
            transform.position = p;
            if (heading.sqrMagnitude > 0.0001f && (_route.Loop || _distance < _route.Length))
            {
                Quaternion look = Quaternion.LookRotation(_flies ? heading : new Vector3(heading.x, 0f, heading.z));
                float yaw = look.eulerAngles.y;
                float rate = dt > 0f ? Mathf.DeltaAngle(_lastYaw, yaw) / dt : 0f;
                _lastYaw = yaw;
                // what flies banks into its turns; what drives leans out of them a little
                float roll = _flies ? Mathf.Clamp(-rate * 0.6f, -50f, 50f) : Mathf.Clamp(rate * 0.03f, -3f, 3f);
                _roll = Mathf.Lerp(_roll, roll, 1f - Mathf.Exp(-dt * 3f));
                transform.rotation = Quaternion.Slerp(transform.rotation, look * Quaternion.Euler(0f, 0f, _roll), turn);
                if (_parts != null) { _parts.Steer = Mathf.Clamp(rate / 40f, -1f, 1f); }
            }
            if (_parts != null) { _parts.Speed = _now; _parts.Throttle = Mathf.Clamp01(0.3f + _now / 30f); }
        }
    }
}
