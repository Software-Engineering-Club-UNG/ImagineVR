using UnityEngine;
using UnityEngine.InputSystem;

namespace ImagineVR
{
    /// <summary>
    /// Moves the user through the world they built: on foot, or in or on a car, a horse, a dragon.
    /// Walking and manual riding share head-relative left-stick movement and right-stick
    /// 45-degree snap turns (desktop: WASD and mouse look). Flying rides cruise above ground.
    /// Whenever the
    /// user is being moved the edge of their view darkens a little (see <see cref="Comfort"/>).
    /// </summary>
    public sealed class Vehicle : MonoBehaviour
    {
        const float Cruise = 12f;   // metres above the ground, for things that fly, when nothing stands taller
        float _cruise = Cruise, _skylineAt;
        const float WalkSpeed = 3f;

        Rig _rig;
        World _world;
        Terminal _terminal;
        GameObject _visual;
        AssetDef _def;
        float _speed, _top, _seat, _height, _altitude, _size = 1f, _travelled;
        Route _route;
        float _railHeight, _lastYaw;

        // what makes a ride feel like one: it leans, bobs, kicks up dust and can be heard
        static readonly int GaitId = Shader.PropertyToID("_Gait");
        Comfort _comfort;
        AudioSource _engine, _rush;
        ParticleSystem _dust;
        MeshRenderer _body;
        MaterialPropertyBlock _bodyProps;
        Vector3 _visualHome;
        float _roll, _tilt, _turnRate, _lastSpeed, _stepped, _gait, _beat, _dustDue, _legs, _fourLegs;
        bool _snapArmed = true;
        Color _tint = Color.white;
        float _offSince = -1f, _bumpedAt = -10f, _crashedAt = -10f, _rumbleDue;
        CharacterController walker;
        float falling;
        Vector3 rideVelocity;
        Vector3 grappleTarget;
        float grappleUntil;

        // ---- the third version: things that drive and fly as what they are (see Drive.cs)
        DriveModel _model;
        Articulated _parts;
        Transform _wheel;
        PartDef _wheelPart;
        float _wheelHeld = -999f, _wheelStart, _wheelSteer, _gaugeAt, _gunAt;
        bool _canJump, _canFly;
        float _walkScale = 1f, _jumpUntil;

        /// <summary>Where the headlights of what is ridden are, and which way they point. False when it has none.</summary>
        public bool Lamps(out Vector3 at, out Vector3 ahead)
        {
            at = ahead = Vector3.zero;
            if (_visual == null || _def == null) return false;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var mark in _def.Marks) if (mark.Key == "lamp") { sum += mark.Value; n++; }
            if (n == 0 && (_model == null || _model.Kind != "car" && _model.Kind != "bike" && _model.Kind != "tank")) return false;
            Bounds b = _def.Bounds;
            Vector3 local = n > 0 ? sum / n : new Vector3(b.center.x, b.min.y + b.size.y * 0.4f, b.max.z);
            at = _visual.transform.TransformPoint(local) + _visual.transform.forward * 0.3f;
            ahead = (_visual.transform.forward - _visual.transform.up * 0.12f).normalized;
            return true;
        }

        /// <summary>The drive model in use, or null for the old way of riding (`simple`, rails).</summary>
        public DriveModel Model => _model;

        /// <summary>`player`: jumping, flying, walking speed.</summary>
        public void Abilities(Statement s)
        {
            if (s.Has("jump")) _canJump = s.Flag("jump");
            if (s.Has("fly")) _canFly = s.Flag("fly");
            if (s.Has("speed")) _walkScale = Mathf.Clamp(s.Num("speed"), 0.3f, 4f);
        }

        /// <summary>`modify id:"ride"`.</summary>
        public void Tune(Statement s)
        {
            if (!Riding) return;
            if (s.Has("drive") && _def != null) { _model = NewModel(s.Str("drive"), _def, 1f); }
            if (_model != null) _model.Tune(s);
            else if (s.Has("speed")) _top = (_def.FliesRidden ? 16f : _def.Category == "vehicles" ? 11f : 7f) * Mathf.Clamp(s.Num("speed"), 0.2f, 3f);
        }

        static DriveModel NewModel(string kind, AssetDef def, float speed)
        {
            if (string.IsNullOrEmpty(kind) || kind == "auto") kind = DriveModel.KindFor(def);
            return kind == "simple" || kind == "rail" ? null : new DriveModel(kind, def, speed);
        }

        /// <summary>Pull across the ground through the walking capsule, never through solid walls.</summary>
        public void PullTo(Vector3 feet)
        {
            Dismount(); grappleTarget = feet; grappleUntil = Time.time + 3;
        }

        /// <summary>Movement input from code: x strafes, y moves forward/back. For demos and checks.</summary>
        public Vector2 Autopilot;

        public bool Riding => _visual != null;
        public string Asset => _def != null ? _def.Id : null;
        public float Speed => _speed;
        /// <summary>The user got in or on something. True when it flies.</summary>
        public event System.Action<bool> Mounted;

        public void Init(Rig rig, World world, Terminal terminal)
        {
            _rig = rig;
            walker = rig.gameObject.AddComponent<CharacterController>();
            walker.radius = 0.3f; walker.height = 1.6f; walker.center = Vector3.up * 0.8f;
            walker.skinWidth = 0.03f; walker.stepOffset = 0.15f; walker.slopeLimit = 50; walker.enabled = false;
            walker.minMoveDistance = 0f;   // a slow push of the stick at a high frame rate is still a step
            foreach (Collider own in rig.GetComponentsInChildren<Collider>(true)) PassThrough(own);
            _world = world;
            _terminal = terminal;
            _comfort = Comfort.Create(rig.Head);
            _engine = Sfx.Loop("engine", rig.transform, 0f);
            _rush = Sfx.Loop("rush", rig.Head, 0f);
            _dust = Fx.Stream("Dust", world.transform, 120);
        }

        public void Apply(Statement s)
        {
            string name = s.Str("asset", "none");
            AssetDef def = name == "none" ? null : Library.FindAsset(name);
            if (def == null) { Dismount(true); return; }
            _world.Placed(s, name, def, false, _rig.transform.position, null);
            if (s.TryVec3("seat", out Vector3 seat)) def.SeatAt(seat);
            // a ride written just after a road starts on it, facing along it
            if (!s.Has("path") && _world.Roads != null && _world.Roads.TakeStart(out Vector3 roadStart, out float roadYaw)) PlaceAt(roadStart, roadYaw);
            Mount(def, s.TryColor("tint", out Color tint) ? tint : Color.white, Mathf.Clamp(s.Num("speed", 1f), 0.2f, 4f), s.Str("drive"));
            // on rails: a roller coaster, a guided tour, a boat ride. The route does the steering.
            _route = s.TryPath("path", out System.Collections.Generic.List<Vector3> points) ? new Route(points, s.Flag("loop", true)) : null;
            if (_route != null) _model = null;
            if (_model != null && s.Has("grip")) _model.Tune(Statement.Make("modify", "grip", (double)s.Num("grip")));
            if (_model != null && s.Has("altitude")) _model.StartAloft(s.Num("altitude"));
            _travelled = 0f;
        }

        /// <summary>How big the user is. 1 is life size; 0.1 makes a kitchen a canyon, 10 makes a city a model.</summary>
        public void Resize(float scale)
        {
            _size = Mathf.Clamp(scale, 0.05f, 20f);
            _rig.transform.GetChild(0).localScale = Vector3.one * _size;
            _rig.Cam.nearClipPlane = Mathf.Clamp(0.05f * _size, 0.004f, 0.5f);
            // their eyes have just moved a long way up or down: the terminal comes to where they now are
            // (made a giant in the 20:45 session, the user found it somewhere below their knees)
            if (_terminal != null) _terminal.Recenter(false);
        }

        void Mount(AssetDef def, Color tint, float speed, string drive = null)
        {
            walker.enabled = false;
            Dismount();
            _def = def;
            _tint = tint;
            _offSince = -1f;
            Bounds b = def.Bounds;
            float top = b.max.y;
            // sit inside a vehicle, just under its roof; sit on top of an animal; stand in a balloon's basket
            bool enclosed = def.Category == "vehicles";
            // measured from where their eyes really are: the desktop camera is at 1.6 m, but someone
            // sitting down in a headset is half a metre lower and used to end up inside the animal
            float eyes = Mathf.Clamp(_rig.Head.localPosition.y, 0.7f, 2f);
            _seat = top > 6f ? 0.3f : (enclosed ? top - 0.25f : top + 0.45f) - eyes;
            _top = (def.FliesRidden ? 16f : enclosed ? 11f : 7f) * speed;
            _speed = 0f;
            _model = NewModel(drive, def, speed);
            // where the driver sits: the seat it says, else behind the wheel (left of centre in a car), else on top
            Vector3 seatAt = new Vector3(b.center.x, top - 0.25f, b.center.z - b.extents.z * (enclosed ? 0.12f : 0.2f));
            bool seated = def.HasSeat;
            if (seated) seatAt = def.Seat;
            else if (_model != null && (_model.Kind == "car" || _model.Kind == "tank") && b.size.x > 1.3f && top < 4f) { seatAt.x = b.center.x - b.extents.x * 0.32f; seatAt.y = Mathf.Min(top - 0.2f, 1.1f); seatAt.z = b.center.z - b.extents.z * 0.1f; seated = true; }
            if (seated) _seat = seatAt.y - eyes;

            _visual = new GameObject("Vehicle_" + def.Id);
            _visual.transform.SetParent(_rig.transform, false);
            // the user sits a little behind the middle, with the nose out in front
            _visual.transform.localPosition = seated ? new Vector3(-seatAt.x, -_seat, -seatAt.z) : new Vector3(-b.center.x, -_seat, -b.center.z + b.extents.z * (enclosed ? 0.12f : 0.2f));
            // the body, and whatever moves on it: wheels that roll and steer, rotors, a steering wheel to hold
            _parts = Articulated.Attach(_visual, def, tint);
            MeshRenderer mr = _visual.GetComponent<MeshRenderer>();
            var props = new MaterialPropertyBlock();
            props.SetColor("_Tint", tint);
            props.SetFloat("_Appear", 1f);
            _body = mr;
            _bodyProps = props;
            _wheel = null;
            _wheelPart = null;
            for (int i = 0; i < def.Parts.Count; i++)
                if (def.Parts[i].Role == "steer") { _wheelPart = def.Parts[i]; _wheel = _visual.transform.Find("Part_steer" + i); break; }
            SessionLog.Write("drive", "asset", def.Id, "model", _model != null ? _model.Kind : "simple", "seat", seatAt, "seated", seated, "parts", def.Parts.Count);
            _visualHome = _visual.transform.localPosition;
            _roll = _tilt = _turnRate = _gait = 0f;
            // an animal being ridden walks on its own legs, as one roaming free does
            bool legged = !def.FliesRidden && def.Category == "animals";
            _fourLegs = legged && b.extents.z > b.extents.x * 1.25f ? 1f : 0f;
            _legs = legged ? top * (_fourLegs > 0f ? 0.42f : 0.47f) : 0f;
            Shadows.Cast(_visual.transform, b);
            Sfx.Play("whoosh", _rig.transform.position, 0.6f);
            Mounted?.Invoke(def.FliesRidden);
            if (_terminal != null) _terminal.Recenter(false);
        }

        /// <summary>The user gets into or onto something standing in the world, where it stands, facing the way it faces.</summary>
        public void Board(AssetDef def, Color tint, Vector3 at, float yaw)
        {
            PlaceAt(at, yaw);
            Mount(def, tint, 1f);
            _route = null;
            SessionLog.Write("board", "asset", def.Id, "at", at);
        }

        /// <summary>
        /// Back on foot, standing wherever the ride stopped. With `park` the ride is left standing
        /// there too, and the user steps out beside it: it can be got back into by hand.
        /// </summary>
        public void Dismount(bool park = false)
        {
            if (park && _visual != null && _def != null && _world != null && _world.Objects != null)
            {
                Transform rig = _rig.transform;
                _world.Objects.Park(_def, rig.position, rig.eulerAngles.y, _tint);
                float aside = Mathf.Clamp(_def.Bounds.extents.x, 0.4f, 4f) + 0.7f;
                Vector3 old = rig.position;
                rig.position -= rig.right * aside;
                _terminal?.Carry(rig.position - old, 0f, rig.position);
                Sfx.Play("release", rig.position, 0.5f, 0.7f);
                SessionLog.Write("park", "asset", _def.Id, "at", old);
            }
            if (_visual != null) Destroy(_visual);
            _visual = null;
            _body = null;
            _def = null;
            _route = null;
            _model = null;
            _parts = null;
            _wheel = null;
            if (_world != null && _world.Play != null) _world.Play.Gauge = "";
            _speed = 0f;
            rideVelocity = Vector3.zero;
            _altitude = _railHeight = 0;
            grappleUntil = 0;
        }

        /// <summary>
        /// Back to the middle of the world, where new places are built, and facing along it:
        /// places are laid out in front of +Z, so the user is turned to look that way from wherever
        /// in their room they are standing and whichever way they had turned. Whatever is being
        /// ridden comes too, and so does the terminal.
        /// </summary>
        public void Home()
        {
            if (walker != null) walker.enabled = false;
            Transform rig = _rig.transform, head = _rig.Head;
            Vector3 was = rig.position;
            float wasYaw = rig.eulerAngles.y;

            // where the head is, and which way it looks, within the rig
            Vector3 offset = Quaternion.Inverse(rig.rotation) * (head.position - rig.position);
            Vector3 look = Quaternion.Inverse(rig.rotation) * head.forward;
            look.y = 0f;
            float lookYaw = Riding || look.sqrMagnitude < 0.01f ? 0f : Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            if (Riding) offset = Vector3.zero;   // a ride faces the way it drives, with its seat in the middle

            Quaternion turned = Quaternion.Euler(0f, -lookYaw, 0f);
            Vector3 stand = -(turned * offset);
            rig.SetPositionAndRotation(new Vector3(stand.x, _height, stand.z), turned);
            float turn = Mathf.DeltaAngle(wasYaw, -lookYaw);
            _terminal.Carry(rig.position - was, turn, rig.position);
            _world.Props.Carry(was, rig.position, Quaternion.Euler(0f, turn, 0f));
            _lastYaw = rig.eulerAngles.y;
            _speed = 0f;
            rideVelocity = Vector3.zero;
            SessionLog.Write("home", "moved", (rig.position - was).magnitude, "turned", turn);
            grappleUntil = 0;
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
            if (!Riding) { Walk(); return; }
            Transform rig = _rig.transform;
            Vector3 p = rig.position;
            Vector3 before = p;
            Vector3 centre = p;      // what a turn pivots on
            Vector3 turned = p;      // where the rig is once it has turned, before it travels
            float turn = 0f, dt = Time.deltaTime;
            bool xr = _rig.XrActive, snapped = false;

            if (_route != null)
            {
                _snapArmed = Mathf.Abs(_rig.TurnStick.x) < 0.35f;
                rideVelocity = Vector3.zero;
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
            else if (_model != null)
            {
                // a car, a plane, a helicopter: the drive model says how far to turn and go
                centre = new Vector3(p.x, p.y, p.z);
                DriveInput input = ReadDrive(xr);
                Vector3 fwd = rig.forward;
                float grip = 1f, slope = 0f;
                if (!_model.Flies)
                {
                    string surface = Surfaces.At(_world, p + Vector3.up * 0.5f - Vector3.up * _seat, 1.5f);
                    grip = GripOf(surface);
                    float ahead = _world.SurfaceHeight(p.x + fwd.x * 1.5f, p.z + fwd.z * 1.5f), behind = _world.SurfaceHeight(p.x - fwd.x * 1.5f, p.z - fwd.z * 1.5f);
                    slope = Mathf.Clamp((ahead - behind) / 3f, -1f, 1f);
                    if (_world.Weather.IsActive) grip *= 0.9f;      // wet or snowy air: a little less grip
                }
                _model.Step(dt, input, fwd, grip, slope, out float yawStep, out Vector3 travel);
                if (yawStep != 0f)
                {
                    rig.RotateAround(centre, Vector3.up, yawStep);
                    p = rig.position;
                    turn = yawStep;
                }
                turned = p;
                Vector3 step = _model.Altitude > 3f ? travel : Blocked(p, travel);
                if (step == Vector3.zero && travel.sqrMagnitude > 1e-6f)
                {
                    if (Mathf.Abs(_model.Speed) > 4f && Time.time - _crashedAt > 0.5f) Crash(p + rig.forward * _def.Bounds.extents.z);
                    _model.Collide();
                }
                if (_model.LastLanding == DriveModel.Landing.Hard) { Crash(p); _model.LastLanding = DriveModel.Landing.None; }
                else if (_model.LastLanding == DriveModel.Landing.Soft) { Sfx.Play("thud", p, 0.5f, 0.7f); _model.LastLanding = DriveModel.Landing.None; }
                p += step;
                _speed = Mathf.Abs(_model.Speed);
                Bump(p, _model.Speed >= 0f ? rig.forward : -rig.forward);
                FireMounted(input, xr);

                if (xr && _rig.StickClicked && _model.Altitude < 2f) { Dismount(true); return; }      // not out of a plane in the sky
                var kbd = Keyboard.current;
                bool leaving = !xr && kbd != null && !ImagineApp.Typing && kbd.qKey.isPressed && _speed < 2f;
                if (!leaving) _offSince = -1f;
                else if (_offSince < 0f) _offSince = Time.time;
                else if (Time.time - _offSince > 0.5f) { Dismount(true); return; }
                float rim = World.Radius - 8f;
                Vector2 level = new Vector2(p.x, p.z);
                if (level.magnitude > rim) { level = level.normalized * rim; p.x = level.x; p.z = level.y; _model.Collide(); }
                if (_parts != null) { _parts.Speed = _model.Speed; _parts.Steer = _model.SteerAngle / 35f; _parts.Throttle = Mathf.Max(input.Throttle, _model.ThrottleLever); }
                Gauge();
            }
            else
            {
                centre = new Vector3(_rig.Head.position.x, p.y, _rig.Head.position.z);
                turn = TurnInput();
                snapped = turn != 0;
                if (snapped)
                {
                    rig.RotateAround(centre, Vector3.up, turn);
                    p = rig.position;
                    rideVelocity = Quaternion.Euler(0, turn, 0) * rideVelocity;
                }
                turned = p;
                Vector3 wanted = WalkDirection(MovementForward(), MovementInput()) * _top;
                float acceleration = wanted.magnitude > rideVelocity.magnitude ? 6f : 10f;
                rideVelocity = Vector3.MoveTowards(rideVelocity, wanted, acceleration * dt);
                Vector3 forward = rideVelocity.sqrMagnitude > 0.01f ? rideVelocity.normalized : rig.forward;
                Vector3 step = Blocked(p, rideVelocity * dt);
                if (step == Vector3.zero && rideVelocity.sqrMagnitude > 0.01f)
                {
                    if (rideVelocity.magnitude > 4f && Time.time - _crashedAt > 0.5f) Crash(p + forward * _def.Bounds.extents.z);
                    rideVelocity *= rideVelocity.magnitude > 4f ? -0.25f : 0.5f;
                }
                else if (dt > 0) rideVelocity = step / dt;
                p += step;
                _speed = rideVelocity.magnitude;
                Bump(p, forward);
                if (rideVelocity.magnitude > 0.001f) rideVelocity *= _speed / rideVelocity.magnitude;

                // Backward is movement, not dismounting. Click the right stick, or hold Q on desktop.
                if (xr && _rig.StickClicked) { Dismount(true); return; }
                var kb = Keyboard.current;
                bool off = !xr && kb != null && !ImagineApp.Typing && kb.qKey.isPressed;
                if (!off) _offSince = -1f;
                else if (_offSince < 0f) _offSince = Time.time;
                else if (Time.time - _offSince > 0.5f) { Dismount(true); return; }
                float edge = World.Radius - 8f;
                Vector2 flat = new Vector2(p.x, p.z);
                if (flat.magnitude > edge) { flat = flat.normalized * edge; p.x = flat.x; p.z = flat.y; }
                _speed = dt > 0 ? new Vector2(p.x - turned.x, p.z - turned.z).magnitude / dt : 0;
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
            // Flying is over things. Twelve metres clears a wood, but asked to "fly over New York" in the
            // 20:45 session it put the user in the street between the towers, looking up at them. So the
            // cruising height rises with what stands there, checked now and then: above the rooftops, with
            // the tallest towers still reaching up past the user, and not so high the streets are lost in haze.
            if (Time.time >= _skylineAt) { _skylineAt = Time.time + 1.5f; _cruise = Mathf.Clamp(_world.Objects.Skyline() * 0.6f + 8f, Cruise, 60f); }
            if (_model != null && _route == null) _altitude = _model.Altitude;
            else _altitude = Mathf.MoveTowards(_altitude, Riding && _def.FliesRidden ? _cruise : 0f, Mathf.Max(4f, _cruise / 8f) * dt);
            if (_route == null) _railHeight = Mathf.MoveTowards(_railHeight, 0f, 6f * dt);
            target += _altitude + _railHeight + (Riding ? _seat : 0f);
            _lastYaw = rig.eulerAngles.y;
            _height = Mathf.Lerp(_height, target, 1f - Mathf.Exp(-dt * 8f));
            p.y = _height;
            rig.position = p;

            if (_terminal != null && (p != before || turn != 0f)) _terminal.Carry(p - turned, turn, centre + (p - turned));

            if (dt > 0f) Liven(dt, snapped ? 0f : turn / dt, xr);
        }

        static readonly Collider[] Struck = new Collider[32];

        /// <summary>
        /// Whatever is ridden shoves what is loose or alive out of its way: bins and cones fly, a
        /// stack of crates bursts, a zombie goes down, and a rule for being hit fires as it would for
        /// a shot. Solid scenery is not moved: that is a crash (see <see cref="Crash"/>).
        /// </summary>
        void Bump(Vector3 at, Vector3 heading)
        {
            float speed = Mathf.Abs(_speed);
            if (speed < 1.5f || _altitude > 3f) return;
            Bounds b = _def.Bounds;
            float radius = Mathf.Clamp(b.extents.x, 0.5f, 1.6f) + 0.25f;
            Vector3 nose = at + heading * Mathf.Min(b.extents.z, 3f) * 0.6f;
            nose.y = _rig.transform.position.y - _seat + Mathf.Min(radius, Mathf.Max(b.extents.y, 0.3f));
            int n = Physics.OverlapSphereNonAlloc(nose, radius, Struck, ~0, QueryTriggerInteraction.Ignore);
            bool hit = false;
            for (int i = 0; i < n; i++)
            {
                Prop prop = Struck[i].GetComponentInParent<Prop>();
                if (prop != null)
                {
                    if (prop.Held || prop.Floating || prop.Body == null || prop.Body.isKinematic || Time.time - prop.RuledAt < 0.3f) continue;
                    prop.RuledAt = Time.time;
                    Vector3 away = prop.transform.position - at;
                    away.y = 0f;
                    Vector3 thrown = (heading * 0.7f + away.normalized * 0.3f).normalized;
                    prop.Body.linearVelocity = thrown * (speed * 1.15f) + Vector3.up * (1.5f + speed * 0.2f);
                    prop.Body.angularVelocity = Random.insideUnitSphere * Mathf.Min(speed, 12f);
                    _world.Rules.Fire("hit", Rules.Of(prop));
                    hit = true;
                    continue;
                }
                Creature creature = Struck[i].GetComponentInParent<Creature>();
                if (creature != null && creature.Up) { creature.Struck(heading); hit = true; }
            }
            if (!hit || Time.time - _bumpedAt < 0.12f) return;
            _bumpedAt = Time.time;
            Sfx.Play("thud", nose, Mathf.Clamp01(0.35f + speed / 12f), Random.Range(0.8f, 1.1f));
            _rig.Haptic(true, 0.7f, 0.07f);
            _rig.Haptic(false, 0.7f, 0.07f);
            _speed *= 0.94f;
        }

        /// <summary>Ran into something that does not give: heard, felt in both hands, and seen.</summary>
        void Crash(Vector3 at)
        {
            _crashedAt = Time.time;
            at.y = _rig.transform.position.y - _seat + 0.5f;
            Sfx.Play("thud", at, 1f, 0.55f);
            Fx.Burst(at, new Color(1f, 0.85f, 0.5f), new Color(0.6f, 0.6f, 0.6f), 18, 3.5f, 0.05f, 0.5f, 0.6f, false);
            _rig.Haptic(true, 1f, 0.18f);
            _rig.Haptic(false, 1f, 0.18f);
            SessionLog.Write("crash", "asset", _def.Id, "speed", Mathf.Abs(_speed));
        }

        /// <summary>
        /// The walking capsule is for walls, floors and doors. The user's own hands are inside it most
        /// of the time, and whatever they hold or throw starts there: left to collide, a rifle carried
        /// across the chest threw the user 80 metres in a second. Those all pass through it.
        /// </summary>
        public void PassThrough(Collider other)
        {
            if (walker != null && other != null && other != walker) Physics.IgnoreCollision(walker, other);
        }

        public static float Snap(float horizontal, ref bool armed)
        {
            if (Mathf.Abs(horizontal) < 0.35f) armed = true;
            if (!armed || Mathf.Abs(horizontal) <= 0.7f) return 0;
            armed = false; return Mathf.Sign(horizontal) * 45;
        }

        public static Vector3 WalkDirection(Vector3 forward, Vector2 input)
        {
            forward.y = 0;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize(); input = Vector2.ClampMagnitude(input, 1);
            return forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x;
        }

        public void PlaceAt(Vector3 feet, float yaw)
        {
            grappleUntil = 0;
            if (walker != null) walker.enabled = false;
            Dismount();
            _comfort.Flash();
            Transform rig = _rig.transform;
            Vector3 old = rig.position;
            float turn = Mathf.DeltaAngle(rig.eulerAngles.y, yaw);
            rig.RotateAround(_rig.Head.position, Vector3.up, turn);
            Vector3 head = _rig.Head.position;
            rig.position += new Vector3(feet.x - head.x, feet.y - rig.position.y, feet.z - head.z);
            _height = rig.position.y; falling = 0; _speed = 0;
            _terminal?.Carry(rig.position - old, turn, feet);
            _world.Props.Carry(old, rig.position, Quaternion.Euler(0, turn, 0));
            Physics.SyncTransforms();
        }

        Vector2 MovementInput()
        {
            Vector2 input = _rig.XrActive ? _rig.MoveStick : Vector2.zero;
            if (!_rig.XrActive && Keyboard.current != null && !ImagineApp.Typing)
            {
                var kb = Keyboard.current;
                input.x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                input.y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
            }
            input = Vector2.ClampMagnitude(input + Autopilot, 1);
            return input.magnitude < 0.15f ? Vector2.zero : input;
        }

        Vector3 MovementForward()
        {
            Vector3 forward = _rig.Head.forward; forward.y = 0;
            return forward.sqrMagnitude < 0.01f ? _rig.transform.forward : forward.normalized;
        }

        float TurnInput() => _rig.XrActive ? Snap(_rig.TurnStick.x, ref _snapArmed) : 0;

        void Walk()
        {
            Transform rig = _rig.transform;
            float dt = Time.deltaTime;
            Vector3 before = rig.position;
            Vector3 pivot = new Vector3(_rig.Head.position.x, rig.position.y, _rig.Head.position.z);
            float turn = TurnInput();
            walker.enabled = false;
            if (turn != 0) rig.RotateAround(pivot, Vector3.up, turn);
            Vector3 turned = rig.position;
            Vector2 input = _world.IsDark ? Vector2.zero : MovementInput();
            Vector3 forward = MovementForward();
            Vector3 movement = WalkDirection(forward, input) * WalkSpeed * _walkScale * Mathf.Clamp(_size, 0.25f, 6) * dt;
            // `player.jump`: the right stick clicked (desktop: J); `player.fly`: the right hand points, the trigger carries them
            var keys = Keyboard.current;
            bool jump = _canJump && (_rig.XrActive ? _rig.StickClicked : keys != null && !ImagineApp.Typing && keys.jKey.wasPressedThisFrame);
            if (jump && rig.position.y <= _world.SurfaceHeight(_rig.Head.position.x, _rig.Head.position.z) + 0.1f) { falling = 5.2f; _jumpUntil = Time.time + 0.2f; Sfx.Play("whoosh", rig.position, 0.3f, 1.4f); }
            bool flyingNow = _canFly && (_rig.XrActive ? _rig.Trigger(true) > 0.5f && (_rig.RightGrabber == null || _rig.RightGrabber.Held == null) : keys != null && !ImagineApp.Typing && keys.leftShiftKey.isPressed);
            if (flyingNow)
            {
                Vector3 aim = _rig.XrActive ? _rig.RightHand.forward : _rig.Head.forward;
                movement += aim * 7f * dt;
                falling = Mathf.Max(falling, aim.y * 7f);
                _jumpUntil = Time.time + 0.1f;
            }
            if (Time.time < grappleUntil)
            {
                Vector3 delta = grappleTarget - new Vector3(_rig.Head.position.x, rig.position.y, _rig.Head.position.z);
                delta.y = 0;
                if (delta.magnitude < 0.15f || input.sqrMagnitude > 0) grappleUntil = 0;
                else movement = Vector3.ClampMagnitude(delta, 8 * dt);
            }
            Vector3 localHead = rig.InverseTransformPoint(_rig.Head.position);
            walker.height = Mathf.Clamp(localHead.y, 0.6f, 3f * Mathf.Max(1, _size));
            walker.radius = Mathf.Min(0.3f * Mathf.Clamp(_size, 0.25f, 6), walker.height / 2 - 0.02f);
            walker.center = new Vector3(localHead.x, walker.height / 2 + 0.01f, localHead.z);
            walker.stepOffset = Mathf.Min(0.15f, walker.height / 3);
            walker.enabled = true;
            float ground = _world.SurfaceHeight(_rig.Head.position.x, _rig.Head.position.z);
            float water = _world.Terrain.WaterY;
            if (!float.IsNaN(water)) ground = Mathf.Max(ground, water);
            if (Time.time >= _jumpUntil) falling = rig.position.y <= ground + 0.08f ? -0.5f : Mathf.Max(falling - 9.81f * dt * Mathf.Abs(Physics.gravity.y / 9.81f), -20);
            else falling -= 9.81f * dt;
            if (rig.position.y < ground - 0.05f) movement.y = ground - rig.position.y;
            else movement.y = falling * dt;
            Physics.SyncTransforms();
            walker.Move(movement);
            // Terrain is also an analytic surface when no terrain collider has finished generating.
            ground = _world.SurfaceHeight(_rig.Head.position.x, _rig.Head.position.z);
            if (!float.IsNaN(water)) ground = Mathf.Max(ground, water);
            if (rig.position.y < ground) { rig.position += Vector3.up * (ground - rig.position.y); falling = 0; }
            Vector3 headNow = _rig.Head.position;
            Vector2 edge = new Vector2(headNow.x, headNow.z);
            if (edge.magnitude > World.Radius - 8) { Vector2 safe = edge.normalized * (World.Radius - 8); rig.position += new Vector3(safe.x - headNow.x, 0, safe.y - headNow.z); }
            _height = rig.position.y;
            _speed = dt > 0 ? new Vector2(rig.position.x - turned.x, rig.position.z - turned.z).magnitude / dt : 0;
            if (_terminal != null && (rig.position != before || turn != 0)) _terminal.Carry(rig.position - turned, turn, pivot + rig.position - turned);
            _comfort.Fade(_world.Structures.HeadBlocked(_rig.Head.position) ? 1 : 0);
            if (dt > 0) Liven(dt, 0, _rig.XrActive);
        }

        /// <summary>Everything that tells the user they are moving, other than the world going past.</summary>
        void Liven(float dt, float turnRate, bool xr)
        {
            Transform rig = _rig.transform;
            float speed = Mathf.Abs(_speed);
            float top = Riding ? _top : WalkSpeed * Mathf.Clamp(_size, 0.25f, 6f);
            float pace = Mathf.Clamp01(speed / Mathf.Max(top, 0.1f));
            float accel = (_speed - _lastSpeed) / dt;
            _lastSpeed = _speed;
            _turnRate = Mathf.Lerp(_turnRate, turnRate, 1f - Mathf.Exp(-dt * 8f));
            bool rails = Riding && _route != null;
            bool flying = Riding && _def.FliesRidden;
            bool machine = Riding && _def.Category == "vehicles";
            bool grounded = _altitude < 0.5f && _railHeight < 0.5f && (_world.Terrain.HasGround || _world.Structures.Contains(_rig.Head.position));

            // comfort: the faster the user is carried or turned, the narrower their view
            float narrow = 0f;
            if (xr)
            {
                float turning = Mathf.Clamp01(Mathf.Abs(_turnRate) / 60f);
                if (rails) narrow = 0.35f + 0.3f * pace;
                else if (Riding) narrow = 0.3f * pace + 0.4f * turning;
                else narrow = 0.5f * pace + 0.45f * turning;
            }
            _comfort.Set(narrow, dt);

            // the air going past, once there is enough speed to hear it
            Play(_rush, Mathf.Clamp01((speed - 3.5f) / 12f) * 0.45f, 0.8f + 0.5f * pace, dt);
            // an engine climbs through its gears: the pitch rises within each one and drops at the change
            float rev = pace;
            if (_model != null && !_model.Flies && machine) { float gears = 5f, g = Mathf.Abs(_model.Speed) / Mathf.Max(_model.Top / gears, 0.5f); rev = Mathf.Clamp01(0.25f + 0.75f * (g - Mathf.Floor(g))) * 0.7f + pace * 0.3f; }
            Play(_engine, machine ? 0.14f + 0.2f * pace : 0f, 0.75f + 1.05f * rev, dt);
            if (_model != null && Mathf.Abs(_model.Side) > 3f && Time.time > _skidAt) { _skidAt = Time.time + 0.35f; Sfx.Play("screech", rig.position, Mathf.Clamp01(Mathf.Abs(_model.Side) / 10f), Random.Range(0.9f, 1.1f)); }

            if (!Riding)
            {
                // footsteps
                if (grounded && speed > 0.3f)
                {
                    _stepped += speed * dt;
                    if (_stepped > 0.8f * Mathf.Clamp(_size, 0.4f, 3f))
                    {
                        _stepped = 0f;
                        Vector3 head = _rig.Head.position;
                        Sfx.Play("step", new Vector3(head.x, rig.position.y, head.z), 0.28f, Random.Range(0.85f, 1.15f));
                    }
                }
                return;
            }

            // The ride leans and pitches around the user, who stays level: a car rolls out of a bend
            // and squats when it pulls away, an animal leans in, and anything that flies banks.
            float roll = flying ? Mathf.Clamp(-_turnRate * 0.35f, -22f, 22f)
                : machine ? Mathf.Clamp(_turnRate * 0.06f * pace, -3.5f, 3.5f)
                : Mathf.Clamp(-_turnRate * 0.1f * pace, -8f, 8f);
            float tilt = machine && !flying ? Mathf.Clamp(-accel * 0.35f, -2.5f, 3f) : 0f;
            _roll = Mathf.Lerp(_roll, roll, 1f - Mathf.Exp(-dt * 5f));
            _tilt = Mathf.Lerp(_tilt, tilt, 1f - Mathf.Exp(-dt * 5f));

            float bob = 0f;
            if (flying)
            {
                // wings beat (or a balloon drifts): a slow heave, with the sound of each beat
                float every = machine ? 4f : 0.95f;
                _beat += dt / every;
                if (_beat >= 1f)
                {
                    _beat -= 1f;
                    if (!machine) Sfx.Play("flap", rig.position, 0.5f, Random.Range(0.9f, 1.1f));
                }
                bob = -Mathf.Cos(_beat * 2f * Mathf.PI) * (machine ? 0.08f : 0.16f);
            }
            else if (machine)
            {
                bob = Mathf.Sin(Time.time * 31f) * 0.004f * (0.4f + pace);
            }
            else if (!rails)
            {
                // an animal: its legs step, its back rises and falls, and its feet can be heard
                float stride = Mathf.Max(_legs * 0.3f, 0.05f);
                _gait += dt * Mathf.Min(Mathf.PI * speed / (2f * stride), 13f);
                bob = Mathf.Abs(Mathf.Cos(_gait)) * 0.06f * pace;
                if (_legs > 0f && _body != null)
                {
                    _bodyProps.SetVector(GaitId, new Vector4(_gait, stride * Mathf.Clamp01(speed / 1.5f), _legs, _fourLegs));
                    _body.SetPropertyBlock(_bodyProps);
                }
                if (grounded && speed > 0.3f)
                {
                    _stepped += dt * Mathf.Min(Mathf.PI * speed / (2f * stride), 13f);
                    if (_stepped > Mathf.PI * 0.5f)
                    {
                        _stepped = 0f;
                        Sfx.Play("hoof", rig.position, 0.3f + 0.3f * pace, Random.Range(0.85f, 1.2f));
                    }
                }
            }
            // a machine under way is felt in the hand that steers it
            _rumbleDue -= dt;
            if (machine && grounded && speed > 1f && _rumbleDue <= 0f)
            {
                _rumbleDue = 0.11f;
                _rig.Haptic(true, 0.04f + 0.1f * pace, 0.03f);
            }
            _visual.transform.localPosition = _visualHome + Vector3.up * bob;
            if (_model != null && _route == null) _visual.transform.localRotation = Quaternion.Euler(_model.VisualPitch + _tilt * 0.3f, 0f, _model.VisualRoll);
            else _visual.transform.localRotation = Quaternion.Euler(_tilt, 0f, _roll);

            // dust from the ground, or spray from the water, behind anything moving along it
            _dustDue -= dt;
            if (grounded && !rails && !flying && speed > 2.5f && _dustDue <= 0f)
            {
                _dustDue = 0.06f;
                Bounds b = _def.Bounds;
                float wide = Mathf.Clamp(b.extents.x, 0.4f, 1.6f);
                float ground = _world.Terrain.HeightAt(rig.position.x, rig.position.z), water = _world.Terrain.WaterY;
                bool afloat = !float.IsNaN(water) && water > ground;
                Vector3 back = rig.position - rig.forward * (b.extents.z * 0.8f) + rig.right * Random.Range(-wide, wide) * 0.8f;
                back.y = (afloat ? water : ground) + 0.15f;
                var puff = new ParticleSystem.EmitParams
                {
                    position = back,
                    velocity = -rig.forward * (speed * 0.08f) + Vector3.up * Random.Range(0.3f, 0.9f) + Random.insideUnitSphere * 0.4f,
                    startSize = Random.Range(0.5f, 1.1f) * wide,
                    startLifetime = Random.Range(0.8f, 1.4f),
                    startColor = afloat ? new Color(0.9f, 0.96f, 1f, 0.4f) : new Color(0.72f, 0.66f, 0.56f, 0.22f + 0.12f * pace),
                };
                _dust.Emit(puff, 1);
            }
        }

        float _skidAt;

        static float GripOf(string surface)
        {
            switch (surface)
            {
                case "ice": return 0.15f;
                case "snow": return 0.35f;
                case "sand": case "mud": case "swamp": return 0.5f;
                case "grass": case "dry_grass": case "fairway": case "green": case "forest_floor": return 0.6f;
                case "dirt": case "gravel": return 0.7f;
                case "glass": case "metal": return 0.5f;
                default: return 1f;
            }
        }

        /// <summary>The controls, as the drive model wants them. Hands on the steering wheel steer, if there is one and they are on it.</summary>
        DriveInput ReadDrive(bool xr)
        {
            var input = new DriveInput();
            if (xr)
            {
                Vector2 left = _rig.MoveStick, right = _rig.TurnStick;
                bool rightFree = _rig.RightGrabber == null || _rig.RightGrabber.Held == null, leftFree = _rig.LeftGrabber == null || _rig.LeftGrabber.Held == null;
                input.Throttle = rightFree ? _rig.Trigger(true) : 0f;
                input.Brake = leftFree ? _rig.Trigger(false) : 0f;
                input.Steer = Dead(left.x);
                input.Pitch = Dead(right.y);
                input.Roll = Dead(right.x);
                input.Lift = Dead(right.y);
                input.Yaw = Dead(right.x);
                input.ThrottleAxis = Dead(left.y);
                input.Move = new Vector2(Dead(left.x), Dead(left.y));
            }
            var kb = Keyboard.current;
            if (!xr && kb != null && !ImagineApp.Typing)
            {
                float ws = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                float ad = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                float ud = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
                float lr = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
                bool flying = _model != null && (_model.Kind == "plane" || _model.Kind == "glider");
                bool hovering = _model != null && (_model.Kind == "heli" || _model.Kind == "hover" || _model.Kind == "space");
                if (flying) { input.Pitch = -ws; input.Roll = ad; input.ThrottleAxis = ud; input.Steer = lr; }
                else if (hovering) { input.Move = new Vector2(ad, ws); input.Lift = ud; input.Yaw = lr; }
                else { input.Throttle = Mathf.Max(0f, ws); input.Brake = Mathf.Max(0f, -ws); input.Steer = ad; input.Handbrake = kb.leftShiftKey.isPressed; }
            }
            if (Autopilot != Vector2.zero) { input.Throttle = Mathf.Max(0f, Autopilot.y); input.Brake = Mathf.Max(0f, -Autopilot.y); input.Steer = Autopilot.x; input.Move = Autopilot; }
            float hands = WheelHands();
            if (!float.IsNaN(hands)) input.Steer = hands;
            return input;
        }

        static float Dead(float v) => Mathf.Abs(v) < 0.12f ? 0f : v;

        /// <summary>
        /// Gripping the steering wheel with either hand and turning it steers: the angle the hands have
        /// gone round the wheel's axis since they took hold, as a fraction of a third of a turn.
        /// NaN when no hand is on the wheel.
        /// </summary>
        float WheelHands()
        {
            if (_wheel == null || !_rig.XrActive) return float.NaN;
            Vector3 axis = _wheel.parent != null ? _wheel.parent.TransformDirection(_wheelPart.Axis) : Vector3.up;
            float sum = 0f;
            int held = 0;
            foreach (bool right in new[] { false, true })
            {
                Transform hand = right ? _rig.RightHand : _rig.LeftHand;
                Grabber grabber = right ? _rig.RightGrabber : _rig.LeftGrabber;
                if (hand == null || _rig.Grip(right) < 0.55f || (grabber != null && grabber.Held != null)) continue;
                Vector3 off = hand.position - _wheel.position;
                if (off.magnitude > 0.35f) continue;
                Vector3 flat = Vector3.ProjectOnPlane(off, axis);
                Vector3 up = Vector3.ProjectOnPlane(_rig.transform.up, axis);
                if (flat.sqrMagnitude < 0.0004f || up.sqrMagnitude < 0.0004f) continue;
                sum += Vector3.SignedAngle(up, flat, axis);
                held++;
            }
            if (held == 0) { _wheelHeld = -999f; return float.NaN; }
            float angle = sum / held;
            if (_wheelHeld < -900f) { _wheelHeld = angle; _wheelStart = angle; _wheelSteer = _model != null ? _model.SteerAngle / 35f : 0f; }
            return Mathf.Clamp(_wheelSteer - Mathf.DeltaAngle(_wheelStart, angle) / 120f, -1f, 1f);
        }

        /// <summary>A vehicle that `fires` shoots from its guns: B on anything that drives, the right trigger on anything that flies.</summary>
        void FireMounted(in DriveInput input, bool xr)
        {
            if (_def == null || !_def.HasMuzzle) return;
            bool flying = _model != null && _model.Flies;
            var kb = Keyboard.current;
            bool pressed = xr ? (flying ? _rig.Trigger(true) > 0.6f : _rig.AltPressed)
                              : kb != null && !ImagineApp.Typing && (kb.fKey.isPressed || (Mouse.current != null && Mouse.current.leftButton.isPressed));
            if (!pressed || Time.time - _gunAt < Mathf.Max(_def.FireEvery, 0.05f)) return;
            _gunAt = Time.time;
            Transform v = _visual.transform;
            var guns = new System.Collections.Generic.List<Vector3>();
            foreach (var mark in _def.Marks) if (mark.Key == "gun") guns.Add(mark.Value);
            if (guns.Count == 0) guns.Add(new Vector3(0f, _def.Bounds.center.y, _def.Bounds.max.z + 0.3f));
            Vector3 aim = v.forward;
            foreach (Vector3 g in guns) _world.Props.Fire(v.TransformPoint(g) + aim * 0.4f, aim, _def.ShotColored ? _def.ShotColor : Pal.Neon, _def, null);
            _rig.Haptic(true, 0.6f, 0.05f);
        }

        /// <summary>The dashboard: speed and gear on the ground; airspeed, height and throttle in the air.</summary>
        void Gauge()
        {
            if (Time.time < _gaugeAt || _world.Play == null) return;
            _gaugeAt = Time.time + 0.2f;
            int kmh = Mathf.RoundToInt(Mathf.Abs(_model.Speed) * 3.6f);
            _world.Play.Gauge = _model.Flies
                ? kmh + " km/h   alt " + Mathf.RoundToInt(_model.Altitude) + " m" + (_model.Kind == "plane" ? "   thr " + Mathf.RoundToInt(_model.ThrottleLever * 100f) + "%" : "")
                : kmh + " km/h   " + _model.Gear;
        }

        /// <summary>Fades a looping sound towards a volume; silent loops are paused rather than left running.</summary>
        static void Play(AudioSource source, float volume, float pitch, float dt)
        {
            if (source == null || source.clip == null) return;
            source.volume = Mathf.MoveTowards(source.volume, volume, dt * 0.8f);
            source.pitch = pitch;
            bool audible = source.volume > 0.004f;
            if (audible && !source.isPlaying) source.Play();
            else if (!audible && source.isPlaying) source.Pause();
        }
    }
}
