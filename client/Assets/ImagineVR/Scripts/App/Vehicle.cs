using UnityEngine;
using UnityEngine.InputSystem;

namespace ImagineVR
{
    /// <summary>
    /// Moves the user through the world they built: on foot, or in or on a car, a horse, a dragon.
    /// The whole rig moves, with any vehicle drawn around it. The right thumbstick (or W A S D on the
    /// desktop) is forward, back and turning. Things that fly cruise above the ground.
    /// </summary>
    public sealed class Vehicle : MonoBehaviour
    {
        const float Cruise = 12f;   // metres above the ground, for things that fly
        const float WalkSpeed = 3f;

        Rig _rig;
        World _world;
        Terminal _terminal;
        GameObject _visual;
        AssetDef _def;
        float _speed, _top, _seat, _height, _altitude, _size = 1f, _travelled;
        Route _route;
        float _railHeight, _lastYaw;

        /// <summary>Stick input from code rather than a hand: x steers, y is throttle. For demos and checks.</summary>
        public Vector2 Autopilot;

        public bool Riding => _visual != null;
        public string Asset => _def != null ? _def.Id : null;

        public void Init(Rig rig, World world, Terminal terminal)
        {
            _rig = rig;
            _world = world;
            _terminal = terminal;
        }

        public void Apply(Statement s)
        {
            string name = s.Str("asset", "none");
            AssetDef def = name == "none" ? null : Library.FindAsset(name);
            if (def == null) { Dismount(); return; }
            Mount(def, s.TryColor("tint", out Color tint) ? tint : Color.white, Mathf.Clamp(s.Num("speed", 1f), 0.2f, 3f));
            // on rails: a roller coaster, a guided tour, a boat ride. The route does the steering.
            _route = s.TryPath("path", out System.Collections.Generic.List<Vector3> points) ? new Route(points, s.Flag("loop", true)) : null;
            _travelled = 0f;
        }

        /// <summary>How big the user is. 1 is life size; 0.1 makes a kitchen a canyon, 10 makes a city a model.</summary>
        public void Resize(float scale)
        {
            _size = Mathf.Clamp(scale, 0.05f, 20f);
            _rig.transform.GetChild(0).localScale = Vector3.one * _size;
            _rig.Cam.nearClipPlane = Mathf.Clamp(0.05f * _size, 0.004f, 0.5f);
        }

        void Mount(AssetDef def, Color tint, float speed)
        {
            Dismount();
            _def = def;
            Bounds b = def.Bounds;
            float top = b.max.y;
            // sit inside a vehicle, just under its roof; sit on top of an animal; stand in a balloon's basket
            bool enclosed = def.Category == "vehicles";
            _seat = top > 6f ? 0.3f : (enclosed ? top - 0.25f : top + 0.45f) - 1.6f;
            _top = (def.Airborne ? 16f : enclosed ? 11f : 7f) * speed;
            _speed = 0f;

            _visual = new GameObject("Vehicle_" + def.Id);
            _visual.transform.SetParent(_rig.transform, false);
            // the user sits a little behind the middle, with the nose out in front
            _visual.transform.localPosition = new Vector3(-b.center.x, -_seat, -b.center.z + b.extents.z * (enclosed ? 0.12f : 0.2f));
            Mesh mesh = def.Mesh;
            _visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = _visual.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterials = mesh.subMeshCount > 1
                ? new[] { _world.Objects.LitMaterial, _world.Objects.GlowMaterial }
                : new[] { _world.Objects.LitMaterial };
            var props = new MaterialPropertyBlock();
            props.SetColor("_Tint", tint);
            props.SetFloat("_Appear", 1f);
            mr.SetPropertyBlock(props);
            Sfx.Play("whoosh", _rig.transform.position, 0.6f);
        }

        /// <summary>Back on foot, standing wherever the ride stopped.</summary>
        public void Dismount()
        {
            if (_visual != null) Destroy(_visual);
            _visual = null;
            _def = null;
            _route = null;
            _speed = 0f;
        }

        /// <summary>Back to the middle of the world, where new places are built. Whatever is being ridden comes too.</summary>
        public void Home()
        {
            Transform rig = _rig.transform;
            Vector3 was = rig.position;
            float yaw = rig.eulerAngles.y;
            rig.SetPositionAndRotation(new Vector3(0f, _height, 0f), Quaternion.identity);
            _terminal.Carry(rig.position - was, -yaw, rig.position);
            _speed = 0f;
        }

        static readonly Collider[] Touching = new Collider[24];

        /// <summary>
        /// The part of a step that solid scenery allows: all of it, a slide along a wall, or nothing.
        /// Only still, solid things count (walls, things made solid); creatures, props and the ground do not.
        /// </summary>
        Vector3 Blocked(Vector3 rigPosition, Vector3 step)
        {
            if (step.sqrMagnitude < 1e-8f || _altitude > 3f) return step;
            float radius = Riding ? Mathf.Clamp(_def.Bounds.extents.x, 0.5f, 1.4f) : 0.3f;
            Vector3 body = Riding ? rigPosition : new Vector3(_rig.Head.position.x, rigPosition.y, _rig.Head.position.z);
            body.y = _world.Terrain.HeightAt(body.x, body.z) + radius + 0.45f;
            if (!Hit(body, radius, step, out Vector3 normal)) return step;
            normal.y = 0f;
            if (normal.sqrMagnitude < 1e-6f) return Vector3.zero;
            Vector3 slide = step - normal.normalized * Vector3.Dot(step, normal.normalized);
            return slide.sqrMagnitude > 1e-8f && !Hit(body, radius, slide, out _) ? slide : Vector3.zero;
        }

        /// <summary>
        /// Would this step push the body into something solid? A body that is already inside one (the
        /// world was built around it) may always move out, never further in.
        /// </summary>
        static bool Hit(Vector3 from, float radius, Vector3 step, out Vector3 normal)
        {
            normal = Vector3.zero;
            Vector3 to = from + step;
            int n = Physics.OverlapSphereNonAlloc(to, radius, Touching, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = Touching[i];
                if (c.attachedRigidbody != null || c is MeshCollider) continue;
                Vector3 nearNow = c.ClosestPoint(from), nearThen = c.ClosestPoint(to);
                float now = Vector3.Distance(from, nearNow), then = Vector3.Distance(to, nearThen);
                if (now < radius && then >= now - 1e-4f) continue;   // inside already, and not going deeper
                normal = then > 1e-4f ? (to - nearThen) / then : -step.normalized;
                return true;
            }
            return false;
        }

        void Update()
        {
            if (_rig == null) return;
            Transform rig = _rig.transform;
            Vector3 p = rig.position;
            Vector3 before = p;
            float turn = 0f, dt = Time.deltaTime;

            if (Riding && _route != null)
            {
                _speed = Mathf.MoveTowards(_speed, _top * 0.7f, 5f * dt);
                _travelled += _speed * dt;
                Vector3 at = _route.At(_travelled, out Vector3 heading);
                heading.y = 0f;
                if (heading.sqrMagnitude > 0.0001f) rig.rotation = Quaternion.Slerp(rig.rotation, Quaternion.LookRotation(heading), 1f - Mathf.Exp(-dt * 4f));
                turn = Mathf.DeltaAngle(0f, rig.eulerAngles.y - _lastYaw);
                p = new Vector3(at.x, p.y, at.z);
                _railHeight = at.y;
                if (!_route.Loop && _travelled >= _route.Length) _route = null;   // end of the line: the controls come back
            }
            // on foot the same controls walk, so the world can be explored without asking for a ride
            else if (Riding || !_world.IsDark)
            {
                Vector2 stick = _rig.Stick + Autopilot;
                Keyboard kb = Keyboard.current;
                if (kb != null && !_rig.XrActive && !ImagineApp.Typing)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) stick.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) stick.y -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) stick.x += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) stick.x -= 1f;
                }
                stick = Vector2.ClampMagnitude(stick, 1f);
                if (stick.magnitude < 0.15f) stick = Vector2.zero;

                float top = Riding ? _top : WalkSpeed * Mathf.Clamp(_size, 0.25f, 6f);
                float wanted = stick.y >= 0f ? stick.y * top : stick.y * top * (Riding ? 0.4f : 1f);
                _speed = Mathf.MoveTowards(_speed, wanted, (Mathf.Abs(wanted) > Mathf.Abs(_speed) ? 6f : 10f) * dt);
                // steers like a car once it is rolling, and can still be turned on the spot
                turn = stick.x * 70f * Mathf.Clamp(Mathf.Abs(_speed) / 3f, 0.4f, 1f) * (_speed < -0.1f ? -1f : 1f) * dt;
                rig.Rotate(0f, turn, 0f, Space.World);
                Vector3 step = Blocked(p, rig.forward * (_speed * dt));
                if (step == Vector3.zero && Riding) _speed *= 0.5f;
                p += step;
                float edge = World.Radius - 8f;
                Vector2 flat = new Vector2(p.x, p.z);
                if (flat.magnitude > edge) { flat = flat.normalized * edge; p.x = flat.x; p.z = flat.y; }
            }

            // stand (or sit) on whatever ground is here; away from the middle that is not y = 0
            bool moved = Riding || new Vector2(p.x, p.z).sqrMagnitude > 0.01f;
            float target = 0f;
            if (moved)
            {
                target = _world.Terrain.HeightAt(p.x, p.z);
                float water = _world.Terrain.WaterY;
                if (!float.IsNaN(water)) target = Mathf.Max(target, water);
            }
            _altitude = Mathf.MoveTowards(_altitude, Riding && _def.Airborne ? Cruise : 0f, 4f * dt);
            if (_route == null) _railHeight = Mathf.MoveTowards(_railHeight, 0f, 6f * dt);
            target += _altitude + _railHeight + (Riding ? _seat : 0f);
            _lastYaw = rig.eulerAngles.y;
            _height = Mathf.Lerp(_height, target, 1f - Mathf.Exp(-dt * 8f));
            p.y = _height;
            rig.position = p;

            if (_terminal != null && (p != before || turn != 0f)) _terminal.Carry(p - before, turn, p);
        }
    }
}
