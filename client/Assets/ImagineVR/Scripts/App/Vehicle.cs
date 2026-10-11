using UnityEngine;
using UnityEngine.InputSystem;

namespace ImagineVR
{
    /// <summary>
    /// Moves the user through the world they built: on foot, or in or on a car, a horse, a dragon.
    /// The whole rig moves, with any vehicle drawn around it. The right thumbstick (or W A S D on the
    /// desktop) is forward, back and turning. Things that fly cruise above the ground.
    ///
    /// On foot in the headset the user walks where they are looking and turns in steps, which is
    /// the gentlest way to turn; clicking the thumbstick switches to smooth turning. Whenever the
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

        /// <summary>Turn in 30 degree steps on foot (the comfortable default in a headset) rather than smoothly.</summary>
        public bool SnapTurn = true;

        /// <summary>Stick input from code rather than a hand: x steers, y is throttle. For demos and checks.</summary>
        public Vector2 Autopilot;

        public bool Riding => _visual != null;
        public string Asset => _def != null ? _def.Id : null;
        public float Speed => _speed;
        /// <summary>The user got in or on something. True when it flies.</summary>
        public event System.Action<bool> Mounted;

        public void Init(Rig rig, World world, Terminal terminal)
        {
            _rig = rig;
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
            if (def == null) { Dismount(); return; }
            _world.Placed(s, name, def, false, _rig.transform.position, null);
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
            // their eyes have just moved a long way up or down: the terminal comes to where they now are
            // (made a giant in the 20:45 session, the user found it somewhere below their knees)
            if (_terminal != null) _terminal.Recenter(false);
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
            _top = (def.FliesRidden ? 16f : enclosed ? 11f : 7f) * speed;
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
            _body = mr;
            _bodyProps = props;
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

        /// <summary>Back on foot, standing wherever the ride stopped.</summary>
        public void Dismount()
        {
            if (_visual != null) Destroy(_visual);
            _visual = null;
            _body = null;
            _def = null;
            _route = null;
            _speed = 0f;
        }

        /// <summary>
        /// Back to the middle of the world, where new places are built, and facing along it:
        /// places are laid out in front of +Z, so the user is turned to look that way from wherever
        /// in their room they are standing and whichever way they had turned. Whatever is being
        /// ridden comes too, and so does the terminal.
        /// </summary>
        public void Home()
        {
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
            SessionLog.Write("home", "moved", (rig.position - was).magnitude, "turned", turn);
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
            Vector3 centre = p;      // what a turn pivots on
            Vector3 turned = p;      // where the rig is once it has turned, before it travels
            float turn = 0f, dt = Time.deltaTime;
            bool xr = _rig.XrActive, snapped = false;

            if (_rig.StickClicked)
            {
                SnapTurn = !SnapTurn;
                _world.Note(SnapTurn ? "turning in steps" : "turning smoothly");
            }

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
                if (kb != null && !xr && !ImagineApp.Typing)
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

                Vector3 forward = rig.forward;
                if (Riding)
                {
                    // steers like a car once it is rolling, and can still be turned on the spot
                    turn = stick.x * 70f * Mathf.Clamp(Mathf.Abs(_speed) / 3f, 0.4f, 1f) * (_speed < -0.1f ? -1f : 1f) * dt;
                    rig.Rotate(0f, turn, 0f, Space.World);
                    forward = rig.forward;
                }
                else
                {
                    // On foot the turn pivots on the user, wherever in their room they are standing.
                    Vector3 head = _rig.Head.position;
                    centre = new Vector3(head.x, p.y, head.z);
                    if (xr && SnapTurn)
                    {
                        if (_snapArmed && Mathf.Abs(stick.x) > 0.7f && Mathf.Abs(stick.x) > Mathf.Abs(stick.y))
                        {
                            turn = Mathf.Sign(stick.x) * 30f;
                            _snapArmed = false;
                            snapped = true;
                        }
                        else if (Mathf.Abs(stick.x) < 0.35f) _snapArmed = true;
                    }
                    else
                    {
                        turn = stick.x * 70f * Mathf.Clamp(Mathf.Abs(_speed) / 3f, 0.4f, 1f) * (_speed < -0.1f ? -1f : 1f) * dt;
                    }
                    if (turn != 0f)
                    {
                        rig.RotateAround(centre, Vector3.up, turn);
                        p = rig.position;
                    }
                    turned = p;
                    // in the headset, forward is where the user is looking
                    if (xr)
                    {
                        Vector3 look = _rig.Head.forward;
                        look.y = 0f;
                        if (look.sqrMagnitude > 0.01f) forward = look.normalized;
                    }
                    else forward = rig.forward;
                }

                Vector3 step = Blocked(p, forward * (_speed * dt));
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
            // Flying is over things. Twelve metres clears a wood, but asked to "fly over New York" in the
            // 20:45 session it put the user in the street between the towers, looking up at them. So the
            // cruising height rises with what stands there, checked now and then: above the rooftops, with
            // the tallest towers still reaching up past the user, and not so high the streets are lost in haze.
            if (Time.time >= _skylineAt) { _skylineAt = Time.time + 1.5f; _cruise = Mathf.Clamp(_world.Objects.Skyline() * 0.6f + 8f, Cruise, 60f); }
            _altitude = Mathf.MoveTowards(_altitude, Riding && _def.FliesRidden ? _cruise : 0f, Mathf.Max(4f, _cruise / 8f) * dt);
            if (_route == null) _railHeight = Mathf.MoveTowards(_railHeight, 0f, 6f * dt);
            target += _altitude + _railHeight + (Riding ? _seat : 0f);
            _lastYaw = rig.eulerAngles.y;
            _height = Mathf.Lerp(_height, target, 1f - Mathf.Exp(-dt * 8f));
            p.y = _height;
            rig.position = p;

            if (_terminal != null && (p != before || turn != 0f)) _terminal.Carry(p - turned, turn, centre + (p - turned));

            if (dt > 0f) Liven(dt, snapped ? 0f : turn / dt, xr);
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
            bool grounded = _altitude < 0.5f && _railHeight < 0.5f && _world.Terrain.HasGround;

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
            Play(_engine, machine ? 0.14f + 0.2f * pace : 0f, 0.75f + 1.05f * pace, dt);

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
            _visual.transform.localPosition = _visualHome + Vector3.up * bob;
            _visual.transform.localRotation = Quaternion.Euler(_tilt, 0f, _roll);

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
