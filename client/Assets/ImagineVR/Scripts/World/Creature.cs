using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// Something that is alive. Any asset can be: a zombie that shambles at the user, sheep that
    /// wander a field, deer that bolt when approached. Whatever it is, anything fired, thrown or
    /// swung at it knocks it over, and a moment later another one turns up.
    ///   chase   walks to the user and stops just out of reach
    ///   wander  strolls about near where it was put
    ///   flee    wanders, and runs when the user comes close
    /// </summary>
    public sealed class Creature : MonoBehaviour
    {
        public const int MaxLive = 24;
        const float RiseSeconds = 1.6f, FallSeconds = 0.4f, LieSeconds = 1.1f, SinkSeconds = 0.8f;

        static int _alive, _downed;
        /// <summary>Every living thing in the world, for rules that are about them.</summary>
        public static readonly List<Creature> All = new List<Creature>();
        public AssetDef Def => _def;
        public string GroupId;
        public float Height => _height;
        /// <summary>On its feet: not fallen, and not away waiting to come back.</summary>
        public bool Up => _state == State.Arriving || _state == State.Walking;

        enum Mode { Chase, Wander, Flee }
        enum State { Arriving, Walking, Falling, Down, Gone }

        World _world;
        AssetDef _def;
        Transform _visual;
        Renderer _renderer;
        CapsuleCollider _collider;
        Mode _mode;
        State _state;
        bool _rises;                 // climbs out of the ground, as the undead do
        bool _still;

        public void SetBehavior(string behavior)
        {
            _still = behavior == "still";
            _mode = behavior == "wander" ? Mode.Wander : behavior == "flee" ? Mode.Flee : Mode.Chase;
        }

        public void SetTint(Color tint)
        {
            _props.SetColor("_Tint", tint);
            _renderer.SetPropertyBlock(_props);
        }

        /// <summary>Gone at once, to come back in a moment as a fresh one: what `respawn` and an explosion do to it.</summary>
        public void Vanish()
        {
            if (_state == State.Gone) return;
            _state = State.Gone;
            _t = 0f;
            _wait = Random.Range(2f, 4f);
            _renderer.enabled = false;
            _collider.enabled = false;
            _shadow.Fade = 0f;
        }
        float _t, _wait, _height, _radius, _speed, _stop, _home, _phase, _nextSound, _lift, _pause, _size;
        Vector3 _centre, _goal;
        Vector3 _fallAxis = Vector3.right;
        static readonly int GaitId = Shader.PropertyToID("_Gait");
        MaterialPropertyBlock _props;
        ShadowCaster _shadow;
        float _stride, _legs, _fourLegs, _gait, _pace;   // the walk cycle: see the Lit shader

        /// <summary>The behaviour an asset has when a statement does not name one, or null for none.</summary>
        public static string DefaultFor(AssetDef def) => def.Behavior == "zombie" ? "chase" : null;

        /// <summary>Makes one creature standing at (position.x, position.z). The caller owns the returned object.</summary>
        public static GameObject Create(World world, AssetDef def, Vector3 position, float scale, Color tint, string behavior, string groupId = null)
        {
            if (_alive == 0) _downed = 0;
            scale = Mathf.Clamp(scale, 0.3f, 4f);
            var go = new GameObject("Creature_" + def.Id);
            go.transform.SetParent(world.transform, false);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * scale;
            Mesh mesh = def.Mesh;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = visual.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterials = mesh.subMeshCount > 1
                ? new[] { world.Objects.LitMaterial, world.Objects.GlowMaterial }
                : new[] { world.Objects.LitMaterial };
            var props = new MaterialPropertyBlock();
            props.SetColor("_Tint", tint);
            props.SetFloat("_Appear", 1f);
            mr.SetPropertyBlock(props);

            Bounds b = def.Bounds;
            float height = Mathf.Max(0.3f, b.max.y * scale);
            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * scale * 0.8f, 0.25f, height * 0.5f);
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            go.AddComponent<Rigidbody>().isKinematic = true;

            Creature c = go.AddComponent<Creature>();
            c._world = world;
            c._def = def;
            c.GroupId = groupId;
            c._still = behavior == "still";
            c._visual = visual.transform;
            c._renderer = mr;
            c._collider = capsule;
            c._height = height;
            c._radius = capsule.radius;
            c._size = scale;
            c._mode = behavior == "wander" ? Mode.Wander : behavior == "flee" ? Mode.Flee : Mode.Chase;
            c._rises = def.Behavior == "zombie";
            // big things stride, small things scurry: roughly a body length every two seconds
            c._speed = Random.Range(0.55f, 0.95f) * Mathf.Clamp(height / 1.75f, 0.6f, 2.5f);
            c._stop = capsule.radius + Random.Range(2f, 2.9f);   // beyond arm's length, and beyond the terminal
            c._phase = Random.value * 10f;
            c._lift = def.Airborne ? Mathf.Clamp(position.y, 1.5f, 6f) : 0f;
            c._props = props;
            c._shadow = Shadows.Cast(go.transform, new Bounds(Vector3.zero, new Vector3(b.size.x, 0f, b.size.z) * scale));
            // People and animals have legs to walk on; four of them when the body is longer than it is
            // wide. Anything else that has been brought to life (a car, a ghost, a ball) just glides.
            bool legged = !def.Airborne && (def.Category == "people" || def.Category == "animals");
            c._fourLegs = legged && def.Category == "animals" && b.extents.z > b.extents.x * 1.25f ? 1f : 0f;
            c._legs = legged ? b.max.y * (c._fourLegs > 0f ? 0.42f : 0.47f) : 0f;
            c._stride = c._legs * 0.2f;

            Vector2 flat = new Vector2(position.x, position.z);
            if (flat.sqrMagnitude < 0.01f) flat = Vector2.up;
            if (c._mode == Mode.Chase)
            {
                // wherever it was put, it starts close enough to arrive while the user is still watching
                c._home = Mathf.Clamp(flat.magnitude, 6f, 22f);
                flat = flat.normalized * Mathf.Clamp(flat.magnitude, World.SafeRadius + 1f + capsule.radius, 24f);
            }
            c._centre = new Vector3(flat.x, 0f, flat.y);
            go.transform.position = c._centre;
            go.transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
            c.Arrive();
            return go;
        }

        void Awake() { _alive++; All.Add(this); }
        void OnDestroy() { _alive--; All.Remove(this); }

        Vector3 User
        {
            get
            {
                Transform head = _world.Head;
                return head != null ? head.position : Vector3.zero;
            }
        }

        void Arrive()
        {
            _state = State.Arriving;
            _t = 0f;
            _nextSound = Random.Range(2f, 9f);
            _renderer.enabled = true;
            _collider.enabled = true;
            _visual.localRotation = Quaternion.identity;
            _visual.localPosition = _rises ? Vector3.down * _height : Vector3.zero;
            _visual.localScale = _rises ? Vector3.one * _size : Vector3.zero;
            _goal = transform.position;
            _pause = Random.Range(0f, 2f);
        }

        void Update()
        {
            if (Time.time < stasisUntil && _state == State.Walking) { Stride(0, 0); return; }
            float dt = Time.deltaTime;
            _t += dt;
            Vector3 p = transform.position;
            p.y = 0f;
            Vector3 user = User;
            Vector3 toUser = new Vector3(user.x - p.x, 0f, user.z - p.z);
            Vector3 heading = Vector3.zero;

            switch (_state)
            {
                case State.Arriving:
                {
                    float f = Mathf.SmoothStep(0f, 1f, _t / RiseSeconds);
                    if (_rises) _visual.localPosition = Vector3.down * (_height * (1f - f));
                    else _visual.localScale = Vector3.one * (_size * Mathf.Clamp01(_t / 0.5f));
                    _shadow.Fade = _rises ? f : Mathf.Clamp01(_t / 0.5f);
                    if (_t >= (_rises ? RiseSeconds : 0.5f)) { _state = State.Walking; _t = 0f; _visual.localScale = Vector3.one * _size; _shadow.Fade = 1f; }
                    if (_mode == Mode.Chase) heading = toUser;
                    break;
                }
                case State.Walking:
                {
                    float pace = 0f;
                    if (_still) { }
                    else if (_mode == Mode.Chase)
                    {
                        heading = toUser;
                        if (toUser.magnitude > _stop) pace = _speed;
                    }
                    else if (_mode == Mode.Flee && toUser.magnitude < 7f + _radius)
                    {
                        heading = -toUser;
                        pace = _speed * 2.4f;
                        _goal = p;
                        _pause = 0.5f;
                    }
                    else
                    {
                        Vector3 toGoal = _goal - p;
                        if (toGoal.magnitude < 0.3f)
                        {
                            // stand a while, then pick somewhere new nearby
                            _pause -= dt;
                            if (_pause <= 0f)
                            {
                                Vector2 r = Random.insideUnitCircle * 9f;
                                Vector3 next = _centre + new Vector3(r.x, 0f, r.y);
                                Vector3 fromUser = next - new Vector3(user.x, 0f, user.z);
                                if (fromUser.magnitude > World.SafeRadius + _radius && next.magnitude < World.Radius - 6f) _goal = next;
                                _pause = Random.Range(1f, 5f);
                            }
                        }
                        else
                        {
                            heading = toGoal;
                            pace = _speed * 0.7f;
                        }
                    }
                    if (pace > 0f && heading.sqrMagnitude > 0.0001f) p += heading.normalized * (pace * dt);
                    if (p.magnitude > World.Radius - 4f) p = p.normalized * (World.Radius - 4f);
                    if (_mode == Mode.Flee) _centre = Vector3.Lerp(_centre, p, dt * 0.5f);

                    // Legs step in time with the ground covered, so feet do not slide; the body rises on
                    // each step and rocks a little. Standing still, the legs settle and it only sways.
                    _pace = Mathf.MoveTowards(_pace, pace > 0f ? 1f : 0f, dt * 4f);
                    if (_stride > 0f) _gait += dt * Mathf.PI * pace / (2f * _stride * _size);
                    else _gait += dt * _speed * 5f;
                    float step = _gait + _phase, moving = Mathf.Lerp(0.25f, 1f, _pace);
                    _visual.localPosition = Vector3.up * (Mathf.Abs(Mathf.Cos(step)) * 0.035f * _pace * _size);
                    _visual.localRotation = Quaternion.Euler((_rises ? 8f : 2f) + Mathf.Sin(_t * 1.3f + _phase) * 3f * moving, 0f, Mathf.Sin(step) * (_rises ? 5f : 2.5f) * moving);
                    Stride(step, _pace * (pace > _speed * 1.5f ? 1.5f : 1f));
                    if (_rises && _t > _nextSound)
                    {
                        _nextSound = _t + Random.Range(5f, 12f);
                        Sfx.Play("groan", p + Vector3.up * (_height * 0.9f), 0.5f, Random.Range(0.8f, 1.2f));
                    }
                    Struck();
                    break;
                }
                case State.Falling:
                {
                    float f = Mathf.Clamp01(_t / FallSeconds);
                    _visual.localRotation = Quaternion.AngleAxis(86f * f * f, _fallAxis);
                    if (_t >= FallSeconds)
                    {
                        _state = State.Down;
                        _t = 0f;
                        Sfx.Play("thud", transform.position, 0.7f, Mathf.Clamp(1.4f / Mathf.Max(_height, 0.5f), 0.5f, 1.4f));
                    }
                    break;
                }
                case State.Down:
                    if (_t > LieSeconds) _visual.localPosition = Vector3.down * ((0.4f + _radius) * (_t - LieSeconds) / SinkSeconds);
                    _shadow.Fade = 1f - Mathf.Clamp01((_t - LieSeconds) / SinkSeconds);
                    if (_t >= LieSeconds + SinkSeconds)
                    {
                        _state = State.Gone;
                        _t = 0f;
                        _wait = Random.Range(1.5f, 4f);
                        _renderer.enabled = false;
                        _shadow.Fade = 0f;
                    }
                    break;
                case State.Gone:
                    if (_t >= _wait)
                    {
                        if (_mode == Mode.Chase)
                        {
                            // comes back from a new direction, mostly from where the user is looking
                            Transform head = _world.Head;
                            float facing = head != null ? Mathf.Atan2(head.forward.x, head.forward.z) : 0f;
                            float angle = facing + Random.Range(-1.9f, 1.9f);
                            p = new Vector3(user.x + Mathf.Sin(angle) * _home, 0f, user.z + Mathf.Cos(angle) * _home);
                            if (p.magnitude > World.Radius - 4f) p = p.normalized * (World.Radius - 4f);
                            heading = new Vector3(user.x - p.x, 0f, user.z - p.z);
                        }
                        else
                        {
                            p = _centre;
                        }
                        transform.position = p;
                        Arrive();
                    }
                    break;
            }

            float ground = _world.SurfaceHeight(p.x, p.z);
            float water = _world.Terrain.WaterY;
            if (!float.IsNaN(water)) ground = Mathf.Max(ground, water - _height * 0.4f);   // wades, but does not walk the sea bed
            p.y = ground + _lift;
            transform.position = p;
            if ((_state == State.Arriving || _state == State.Walking) && heading.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), 1f - Mathf.Exp(-dt * 5f));
        }

        float stasisUntil;
        public void Stasis(float seconds) { stasisUntil = Mathf.Max(stasisUntil, Time.time + Mathf.Clamp(seconds, 0, 10)); }

        /// <summary>Poses the legs: where in the step they are, and how far they reach (0 stands still).</summary>
        void Stride(float phase, float reach)
        {
            if (_stride <= 0f) return;
            _props.SetVector(GaitId, new Vector4(phase, _stride * reach, _legs, _fourLegs));
            _renderer.SetPropertyBlock(_props);
        }

        /// <summary>Hit by something the user is swinging: held things are moved by hand, so physics never reports them.</summary>
        void Struck()
        {
            foreach (Prop prop in _world.Props.All)
            {
                if (prop == null || prop.Holder == null || prop.Visual == null) continue;
                Vector3 swing = prop.Holder.Velocity;
                if (swing.magnitude < 1.3f) continue;
                Vector3 grip = prop.Visual.position, tip = grip + prop.Visual.up * prop.Length;
                if (Touches(tip) || Touches((grip + tip) * 0.5f) || Touches(grip))
                {
                    if (!_world.Rules.Fire("hit", Rules.Of(this))) Knock(swing);
                    return;
                }
            }
        }

        bool Touches(Vector3 point)
        {
            Vector3 local = point - transform.position;
            return local.y > -0.1f && local.y < _height + 0.1f && new Vector2(local.x, local.z).magnitude < _radius + 0.12f;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_state != State.Arriving && _state != State.Walking) return;
            Prop prop = collision.collider.GetComponentInParent<Prop>();
            if (prop == null || collision.relativeVelocity.magnitude < 2f) return;
            HeldItemRuntime payload = prop.GetComponent<HeldItemRuntime>();
            if (payload != null && payload.Profile.Action == "payload" && payload.Armed) { payload.Detonate(); return; }
            Vector3 away = transform.position - prop.transform.position;
            bool taken = _world.Rules.Fire("hit", Rules.Of(this));
            if (prop.GroupId == "shot") Destroy(prop.gameObject);
            if (!taken) Knock(away);
        }

        /// <summary>Something ran or crashed into it: a rule for being hit has its say, and otherwise it goes down.</summary>
        public void Struck(Vector3 away)
        {
            if (_state != State.Arriving && _state != State.Walking) return;
            if (!_world.Rules.Fire("hit", Rules.Of(this))) Knock(away);
        }

        /// <summary>Knocks it flat, falling in the given direction.</summary>
        public void Knock(Vector3 direction)
        {
            if (_state != State.Arriving && _state != State.Walking) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;
            _fallAxis = Quaternion.Inverse(transform.rotation) * Vector3.Cross(Vector3.up, direction.normalized);
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one * _size;
            _state = State.Falling;
            _t = 0f;
            _collider.enabled = false;
            _pace = 0f;
            Stride(0f, 0f);
            Vector3 chest = transform.position + Vector3.up * (_height * 0.7f);
            Color a = _rises ? new Color(0.55f, 0.85f, 0.3f) : new Color(1f, 0.9f, 0.5f), b = _rises ? new Color(0.25f, 0.4f, 0.15f) : Color.white;
            Fx.Burst(chest, a, b, 22, 3f, 0.06f * Mathf.Clamp(_height, 0.6f, 3f), 0.6f, 0.8f, false);
            Sfx.Play("pop", chest, 0.8f, 0.7f);
            _downed++;
            _world.Emit(_downed == 1 ? _def.Id.Replace('_', ' ') + " down" : _downed + " down");
        }
    }
}
