using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// A solid thing: it falls, rolls, can be pushed, grabbed, thrown, and may react when used or hit.
    /// The body (rigidbody + collider) and the visible mesh are separate objects, so that while a
    /// prop is held the mesh can track the hand exactly while the body sweeps through physics.
    /// </summary>
    public sealed class Prop : MonoBehaviour
    {
        public AssetDef Def;
        public string GroupId;
        public Rigidbody Body;
        public Transform Visual;
        public PropSystem System;
        public Color Tint = Color.white;
        public bool Given;          // made by `give`: stays with the user through `clear`
        public bool Floating;       // waiting in the air to be picked up
        public Grabber Holder;
        public float Length = 0.3f; // how far the business end is from the grip, for tools
        public float Born;
        public float LetGoAt;       // when a hand last let go of it
        public float AwaySince = -1f;   // when it was first found waiting out of the user's reach; negative while in reach

        Vector3 _floatAnchor;
        float _lastSound;
        MaterialPropertyBlock _props;
        Renderer _renderer;

        public bool Held => Holder != null;

        public void SetTint(Color c)
        {
            if(GetComponent<BlueprintActor>()!=null)
            {
                Tint=c; foreach(var renderer in Visual.GetComponentsInChildren<Renderer>()) { var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); block.SetColor("_Tint",c); block.SetFloat("_Appear",1); renderer.SetPropertyBlock(block); } return;
            }
            Tint = c;
            if (_renderer == null) _renderer = Visual.GetComponent<Renderer>();
            if (_props == null) _props = new MaterialPropertyBlock();
            _props.SetColor("_Tint", c);
            _props.SetFloat("_Appear", 1f);
            _renderer.SetPropertyBlock(_props);
        }

        public void FloatAt(Vector3 position)
        {
            Floating = true;
            _floatAnchor = position;
            Body.isKinematic = true;
            transform.position = position;
        }

        public void Drop()
        {
            if (!Floating) return;
            Floating = false;
            Body.isKinematic = false;
        }

        void FixedUpdate()
        {
            if (Floating && !Held)
            {
                float t = Time.time - Born;
                Body.MovePosition(_floatAnchor + Vector3.up * (Mathf.Sin(t * 1.6f) * 0.03f));
                // a gun hangs level, grip down, as it will sit in the hand; other tools lean back a little
                Body.MoveRotation(Quaternion.Euler(0f, t * 25f, 0f) * Quaternion.Euler(Def != null && Def.HasMuzzle ? 90f : Def != null && Def.Tool ? -20f : 0f, 0f, 0f));
                return;
            }
            if (Held || Body.isKinematic) return;
            if (Def != null && Def.Behavior == "balloon")
            {
                // lighter than air: push up against gravity, with a little drift
                Body.AddForce(-Physics.gravity * 1.25f * Body.mass + new Vector3(Mathf.Sin(Time.time * 0.7f + Born), 0f, Mathf.Cos(Time.time * 0.5f + Born)) * 0.02f);
                if (transform.position.y > 35f) System.Pop(this);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (System == null) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 0.5f || Time.time - _lastSound < 0.08f) return;
            _lastSound = Time.time;
            System.OnHit(this, collision, speed);
        }
    }

    /// <summary>A spot that reacts when the right kind of prop arrives: a golf hole, a hoop, a target.</summary>
    public sealed class ScoreZone : MonoBehaviour
    {
        public string Accepts;
        public string Message;
        World _world;
        float _cooldown;
        int _count;

        public static GameObject Create(World world, Vector3 position, float radius, string accepts, string message)
        {
            var go = new GameObject("ScoreZone");
            go.transform.SetParent(world.transform, false);
            go.transform.position = position;
            SphereCollider c = go.AddComponent<SphereCollider>();
            c.isTrigger = true;
            c.radius = radius;
            ScoreZone zone = go.AddComponent<ScoreZone>();
            zone.Accepts = accepts;
            zone.Message = message;
            zone._world = world;
            return go;
        }

        void OnTriggerEnter(Collider other)
        {
            if (Time.time < _cooldown) return;
            Prop prop = other.GetComponentInParent<Prop>();
            if (prop == null || prop.Def == null || prop.Held) return;
            if (!string.IsNullOrEmpty(Accepts) && !prop.Def.Id.Contains(Accepts) && prop.Def.Behavior != Accepts) return;
            _cooldown = Time.time + 1.5f;
            _count++;
            Fx.Confetti(transform.position + Vector3.up * 0.3f);
            Sfx.Play("confetti", transform.position, 0.9f);
            _world.Emit(_count > 1 ? Message + "  (" + _count + ")" : Message);
            _world.Rules.Broadcast("goal_reached", Message);
        }
    }

    /// <summary>A spot that reacts when the user themselves arrives: the way out of a maze, a finish line.</summary>
    public sealed class Goal : MonoBehaviour
    {
        public float Radius = 2f;
        public string Message;
        World _world;
        float _cooldown;

        public static GameObject Create(World world, Vector3 position, float radius, string message)
        {
            var go = new GameObject("Goal");
            go.transform.SetParent(world.transform, false);
            go.transform.position = position;
            Goal goal = go.AddComponent<Goal>();
            goal.Radius = radius;
            goal.Message = message;
            goal._world = world;
            return go;
        }

        void Update()
        {
            if (Time.time < _cooldown || _world.Head == null) return;
            Vector3 to = _world.Head.position - transform.position;
            to.y = 0f;
            if (to.magnitude > Radius) return;
            _cooldown = Time.time + 8f;
            Fx.Confetti(transform.position + Vector3.up * 1.5f);
            Sfx.Play("confetti", transform.position, 0.9f);
            _world.Emit(Message);
            _world.Rules.Broadcast("goal_reached", Message);
        }
    }

    /// <summary>Creates and looks after physics props and the things the user is given to hold.</summary>
    public sealed class PropSystem
    {
        public const int MaxPhysics = 40;
        const int MaxStrokes = 80;

        readonly World _world;
        readonly Transform _root;
        readonly List<Prop> _props = new List<Prop>();
        readonly List<GameObject> _strokes = new List<GameObject>();
        static readonly Dictionary<int, PhysicsMaterial> Materials = new Dictionary<int, PhysicsMaterial>();
        Material _stroke;
        float _gravity = 1f;
        int _auto;

        public IReadOnlyList<Prop> All => _props;
        public Prop AttachGenerated(GameObject root,AssetDef def,string id,Transform visual)
        {
            var p=root.GetComponent<Prop>(); if(p==null) p=root.AddComponent<Prop>(); p.Def=def; p.GroupId=id; p.Visual=visual; p.Body=root.GetComponent<Rigidbody>(); p.System=this; p.Born=Time.time; p.Length=Mathf.Max(.1f,def.Bounds.max.y);
            if(!_props.Contains(p)) _props.Add(p); return p;
        }
        public float Gravity => _gravity;

        public PropSystem(World world)
        {
            _world = world;
            _root = new GameObject("Props").transform;
            _root.SetParent(world.transform, false);
            SetGravity(1f);
        }

        public static PhysicsMaterial Bouncy(float bounce)
        {
            int key = Mathf.RoundToInt(Mathf.Clamp(bounce, 0f, 1f) * 20f);
            if (!Materials.TryGetValue(key, out PhysicsMaterial m))
            {
                m = new PhysicsMaterial("Bounce" + key)
                {
                    bounciness = key / 20f, dynamicFriction = 0.5f, staticFriction = 0.55f,
                    bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Average,
                };
                Materials[key] = m;
            }
            return m;
        }

        public void SetGravity(float g)
        {
            _gravity = Mathf.Clamp(g, -0.5f, 2f);
            Physics.gravity = new Vector3(0f, -9.81f * _gravity, 0f);
            foreach (Prop p in _props) if (p != null && !p.Body.isKinematic) p.Body.WakeUp();
        }

        // ------------------------------------------------------------------ creation

        Prop Create(AssetDef def, string groupId, Vector3 position, Quaternion rotation, Vector3 scale, Color tint, bool registerRules = true)
        {
            var go = new GameObject("Prop_" + def.Id);
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            Mesh mesh = def.Mesh;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = visual.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterials = mesh.subMeshCount > 1
                ? new[] { _world.Objects.LitMaterial, _world.Objects.GlowMaterial }
                : new[] { _world.Objects.LitMaterial };

            Bounds b = def.Bounds;
            switch (def.Shape)
            {
                case ColliderShape.Sphere:
                    SphereCollider sc = go.AddComponent<SphereCollider>();
                    sc.center = b.center;
                    sc.radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z);
                    sc.sharedMaterial = Bouncy(def.Bounce);
                    break;
                case ColliderShape.Capsule:
                    CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
                    cc.center = b.center;
                    cc.direction = 1;
                    cc.radius = Mathf.Max(b.extents.x, b.extents.z);
                    cc.height = b.size.y;
                    cc.sharedMaterial = Bouncy(def.Bounce);
                    break;
                default:
                    BoxCollider bc = go.AddComponent<BoxCollider>();
                    bc.center = b.center;
                    bc.size = Vector3.Max(b.size, Vector3.one * 0.02f);
                    bc.sharedMaterial = Bouncy(def.Bounce);
                    break;
            }

            Rigidbody body = go.AddComponent<Rigidbody>();
            float volumeScale = Mathf.Max(0.05f, scale.x * scale.y * scale.z);
            body.mass = Mathf.Clamp(def.Mass * Mathf.Pow(volumeScale, 0.7f), 0.02f, 400f);
            body.linearDamping = def.Drag;
            body.angularDamping = 0.4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = b.extents.magnitude * scale.x < 0.25f
                ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;

            if (groupId != "shot") Shadows.Cast(go.transform, b, mr);
            Prop prop = go.AddComponent<Prop>();
            prop.Def = def;
            prop.GroupId = groupId;
            prop.Body = body;
            prop.Visual = visual.transform;
            prop.System = this;
            prop.Born = Time.time;
            prop.Length = Mathf.Max(0.1f, b.max.y * scale.y);
            prop.SetTint(tint);
            _props.Add(prop);
            if (registerRules) _world.Rules?.RegisterProp(prop);
            Trim();
            return prop;
        }

        /// <summary>Keeps the number of loose physics props under the limit by retiring the oldest.</summary>
        void Trim()
        {
            int loose = 0;
            foreach (Prop p in _props) if (p != null && !p.Given) loose++;
            // spent shots go first: a burst from a rifle must not eat the apples the user was given to throw
            for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < _props.Count && loose > MaxPhysics; i++)
            {
                Prop p = _props[i];
                if (p == null || p.Given || p.Held || (pass == 0 && p.GroupId != "shot")) continue;
                Object.Destroy(p.gameObject);
                _props.RemoveAt(i--);
                loose--;
            }
        }

        string NextId(string kind) => kind + "_p" + (++_auto);

        public void SpawnPhysics(Statement s, AssetDef def, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            string id = s.Str("id") ?? NextId("spawn");
            Remove(id);
            float ground = _world.Terrain.HeightAt(position.x, position.z);
            Vector3 p = new Vector3(position.x, ground + position.y + 0.05f, position.z);
            Color tint = s.TryColor("tint", out Color c) ? c : Color.white;
            Create(def, id, p, rotation, scale, tint);
        }

        /// <summary>A physics prop made by a kit. It is removed with the kit.</summary>
        public Prop SpawnLoose(string groupId, AssetDef def, Vector3 position, Quaternion rotation, Vector3 scale, Color tint, bool registerRules = true)
        {
            return Create(def, groupId, position, rotation, scale, tint, registerRules);
        }

        public void RemoveQuietly(string id)
        {
            foreach (Prop p in Matching(id))
            {
                if (p.Given) continue;
                if (p.Holder != null) p.Holder.ForceRelease();
                _props.Remove(p);
                Object.Destroy(p.gameObject);
            }
        }

        public void ScatterPhysics(Statement s, AssetDef def, int count)
        {
            string id = s.Str("id") ?? NextId("scatter");
            Remove(id);
            count = Mathf.Min(count, 30);
            var rng = new System.Random(s.Has("seed") ? s.Int("seed") : id.GetHashCode());
            float rMin = 1.2f, rMax = 4f;
            if (s.TryRange("radius", out float a, out float b)) { rMin = Mathf.Max(0.6f, a); rMax = Mathf.Max(b, rMin + 0.2f); }
            float sMin = 1f, sMax = 1f;
            if (s.TryRange("scale", out a, out b)) { sMin = a; sMax = b; }
            string area = s.Str("area", "front");
            float arcCenter = 0f, arcHalf = area == "around" || area == "ring" || area == "everywhere" ? 180f : 55f;
            if (area == "left") arcCenter = -90f; else if (area == "right") arcCenter = 90f; else if (area == "behind") arcCenter = 180f;
            Color tint = s.TryColor("tint", out Color c) ? c : Color.white;
            bool hasTint = s.Has("tint");
            Pose frame = s.Frame();
            for (int i = 0; i < count; i++)
            {
                float angle = (arcCenter + ((float)rng.NextDouble() * 2f - 1f) * arcHalf) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(rMin, rMax, (float)rng.NextDouble());
                Vector3 spot = Statement.InWorld(frame, new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius));
                float x = spot.x, z = spot.z;
                // dropped from a little way up so they tumble into place
                float y = _world.Terrain.HeightAt(x, z) + 0.4f + i * 0.12f + (float)rng.NextDouble() * 0.5f;
                float scale = Mathf.Lerp(sMin, sMax, (float)rng.NextDouble());
                Color col = hasTint ? tint : Color.white;
                Create(def, id, new Vector3(x, y, z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * scale, col);
            }
        }

        /// <summary>Puts something within reach, floating, for the user to take.</summary>
        public void Give(Statement s)
        {
            string name = s.Str("asset", "cube");
            AssetDef def = Library.FindAsset(name);
            bool standIn = def == null;
            if (standIn) def = Library.Asset("cube");
            string id = s.Str("id") ?? NextId("give");
            Remove(id);

            // things that are not made to be held arrive as a hand-sized model
            float scale = 1f;
            if (!def.Holdable)
            {
                float biggest = Mathf.Max(def.Bounds.size.x, def.Bounds.size.y, def.Bounds.size.z);
                scale = Mathf.Min(1f, 0.32f / Mathf.Max(biggest, 0.01f));
            }

            Vector3 position = InReach();

            Color tint = s.TryColor("tint", out Color c) ? c : Color.white;
            Prop prop = Create(def, id, position, Quaternion.identity, Vector3.one * scale, tint);
            prop.Given = true;
            prop.FloatAt(position);
            Sfx.Play("sparkle", position, 0.5f);
            Fx.Burst(position, new Color(1f, 0.9f, 0.6f), Color.white, 16, 0.8f, 0.03f, 0.7f, 0f, true);
            if (standIn && _world.Font != null) AddLabel(prop, name.Replace('_', ' '));
            _world.Placed(s, name, def, standIn, position, null);
            _world.OnGiven(prop);
        }

        /// <summary>How far from the user's head something waiting to be taken still counts as within reach.</summary>
        public const float Reach = 1.5f;
        const float ComesBackAfter = 2.5f;

        /// <summary>A free spot in the air in front of the user, at the height of a hand: where things wait to be taken.</summary>
        Vector3 InReach()
        {
            Transform head = _world.Head;
            Vector3 forward = head != null ? head.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 origin = head != null ? head.position : new Vector3(0f, 1.6f, 0f);
            int waiting = 0;
            foreach (Prop p in _props) if (p != null && p.Floating && !p.Held && (p.transform.position - origin).magnitude < Reach) waiting++;
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (((waiting + 1) / 2) * 0.28f * (waiting % 2 == 0 ? 1f : -1f));
            Vector3 position = origin + forward * 0.5f + side + Vector3.down * 0.32f;
            position.y = Mathf.Max(position.y, 0.6f);
            return position;
        }

        /// <summary>True for something the user was given that is waiting in the air close enough to take.</summary>
        public bool WithinReach(Prop p)
        {
            return p != null && p.Floating && !p.Held && _world.Head != null && (p.transform.position - _world.Head.position).magnitude < Reach;
        }

        void AddLabel(Prop prop, string text)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(prop.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.font = _world.Font;
            tmp.text = text;
            tmp.fontSize = 0.8f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.85f, 0.6f);
            tmp.rectTransform.sizeDelta = new Vector2(2f, 0.3f);
            go.AddComponent<FaceUser>();
        }

        // ------------------------------------------------------------------ changes

        List<Prop> Matching(string id)
        {
            var result = new List<Prop>();
            if (string.IsNullOrEmpty(id)) return result;
            string prefix = id + ".";
            foreach (Prop p in _props)
            {
                if (p != null && (p.GroupId == id || p.GroupId.StartsWith(prefix, System.StringComparison.Ordinal))) result.Add(p);
            }
            return result;
        }

        public void Modify(Statement s)
        {
            foreach (Prop p in Matching(s.Str("id")))
            {
                if (s.TryColor("tint", out Color c)) p.SetTint(c);
                if (s.Has("scale")) p.transform.localScale = Vector3.one * Mathf.Clamp(s.Num("scale", 1f), 0.05f, 20f);
            }
        }

        public void Remove(string id)
        {
            foreach (Prop p in Matching(id))
            {
                if (p.Holder != null) p.Holder.ForceRelease();
                Fx.Burst(p.transform.position, p.Tint, Color.white, 10, 0.8f, 0.03f, 0.5f, 0f, true);
                _props.Remove(p);
                Object.Destroy(p.gameObject);
            }
        }

        /// <summary>The user was moved (brought back to the middle): what is waiting in the air for them comes along.</summary>
        public void Carry(Vector3 from, Vector3 to, Quaternion turn)
        {
            foreach (Prop p in _props)
                if (p != null && p.Floating && !p.Held) p.FloatAt(to + turn * (p.transform.position - from));
        }

        /// <summary>Removes every prop there is, loose, given or in a hand, and every stroke of paint.</summary>
        public void ClearAll()
        {
            foreach (Prop p in _props)
            {
                if (p == null) continue;
                if (p.Holder != null) p.Holder.ForceRelease();
                Object.Destroy(p.gameObject);
            }
            _props.Clear();
            ClearLoose();
        }

        /// <summary>Removes loose physics props. Things the user was given stay.</summary>
        public void ClearLoose()
        {
            for (int i = _props.Count - 1; i >= 0; i--)
            {
                Prop p = _props[i];
                if (p == null) { _props.RemoveAt(i); continue; }
                if (p.Given || p.Held) continue;
                Object.Destroy(p.gameObject);
                _props.RemoveAt(i);
            }
            foreach (GameObject stroke in _strokes) if (stroke != null) Object.Destroy(stroke);
            _strokes.Clear();
        }

        public void Tick(float dt)
        {
            for (int i = _props.Count - 1; i >= 0; i--)
            {
                Prop p = _props[i];
                if (p == null) { _props.RemoveAt(i); continue; }
                Vector3 pos = p.transform.position;
                // A tool the user was given and has let go of comes back to wait within reach: dropped
                // by accident it is otherwise lost at their feet or across the street, and they have
                // to ask for another. Thrown on purpose, it is back in a moment to throw again.
                // Something waiting to be taken waits where the user can take it. They may have been put
                // somewhere else since it was hung in the air: set down from a ride, shrunk from a giant
                // (in the 20:45 session the blaster was left floating overhead), or simply walked off.
                if (p.Given && p.Floating && !p.Held && _world.Head != null)
                {
                    bool away = (pos - _world.Head.position).magnitude > Reach;
                    if (!away) p.AwaySince = -1f;
                    else if (p.AwaySince < 0f) p.AwaySince = Time.time;
                    else if (Time.time - p.AwaySince > 1f)
                    {
                        Vector3 near = InReach();
                        p.FloatAt(near);
                        p.AwaySince = -1f;
                        SessionLog.Write("recover", "asset", p.Def.Id, "id", p.GroupId, "from", pos, "to", near, "why", "waiting out of reach, brought back");
                    }
                }
                if (p.Given && p.Def.Tool && !p.Held && !p.Floating && Time.time - p.LetGoAt > ComesBackAfter)
                {
                    Vector3 back = InReach();
                    p.Body.linearVelocity = Vector3.zero;
                    p.Body.angularVelocity = Vector3.zero;
                    p.FloatAt(back);
                    Sfx.Play("sparkle", back, 0.4f);
                    SessionLog.Write("recover", "asset", p.Def.Id, "id", p.GroupId, "from", pos, "to", back, "why", "dropped tool came back to reach");
                    _world.OnGiven(p);
                    continue;
                }
                if (pos.y < -30f || pos.sqrMagnitude > 250f * 250f)
                {
                    if (p.Given)
                    {
                        // fell out of the world: bring it back to the user
                        Transform head = _world.Head;
                        Vector3 back = (head != null ? head.position + head.forward * 0.5f : new Vector3(0f, 1.2f, 0.5f)) + Vector3.down * 0.3f;
                        p.Body.linearVelocity = Vector3.zero;
                        p.Body.angularVelocity = Vector3.zero;
                        p.FloatAt(back);
                        SessionLog.Write("recover", "asset", p.Def.Id, "id", p.GroupId, "from", pos, "to", back,
                            "why", "given item left world bounds");
                    }
                    else
                    {
                        _props.RemoveAt(i);
                        Object.Destroy(p.gameObject);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ reactions

        public void OnHit(Prop prop, Collision collision, float speed)
        {
            RuleBody rule = prop.GetComponent<RuleBody>();
            Prop source = collision.collider.GetComponentInParent<Prop>();
            if (rule != null && rule.Hit(source != null ? rule.Engine.Find(source.gameObject) : null,
                source != null && source.GroupId == "shot" ? "projectile" : source != null ? "thrown" : "collision", speed)) return;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : prop.transform.position;
            float volume = Mathf.Clamp01(speed / 8f);
            switch (prop.Def.Behavior)
            {
                case "balloon":
                    if (speed > 2.2f) { Pop(prop); return; }
                    Sfx.Play("tap", point, volume * 0.4f, 1.6f);
                    break;
                case "bell":
                    Sfx.Play("bell", point, Mathf.Clamp01(0.3f + volume), prop.Def.Pitch);
                    break;
                case "drum":
                    Sfx.Play("drum", point, Mathf.Clamp01(0.4f + volume), prop.Def.Pitch);
                    break;
                case "chime":
                    Sfx.Play("chime", point, Mathf.Clamp01(0.3f + volume), prop.Def.Pitch);
                    break;
                default:
                    bool light = prop.Body.mass < 0.6f;
                    Sfx.Play(light ? "tap" : "thud", point, volume * 0.8f, Mathf.Clamp(1.4f - prop.Body.mass * 0.1f, 0.6f, 1.6f) * prop.Def.Pitch);
                    break;
            }
        }

        public void Pop(Prop prop)
        {
            Vector3 p = prop.transform.position + prop.transform.up * 0.2f;
            Fx.Burst(p, prop.Tint == Color.white ? Pal.Red : prop.Tint, Color.white, 26, 3.5f, 0.05f, 0.7f, 0.6f, false);
            Sfx.Play("pop", p, 1f, 0.9f + Random.value * 0.3f);
            if (prop.Holder != null) prop.Holder.ForceRelease();
            _props.Remove(prop);
            Object.Destroy(prop.gameObject);
        }

        // ------------------------------------------------------------------ tools

        static readonly Color[] PaintColors =
        {
            new Color(1f, 0.35f, 0.3f), new Color(1f, 0.62f, 0.2f), new Color(1f, 0.88f, 0.3f), new Color(0.4f, 0.85f, 0.45f),
            new Color(0.3f, 0.8f, 0.95f), new Color(0.35f, 0.5f, 1f), new Color(0.7f, 0.45f, 1f), new Color(1f, 0.5f, 0.8f), Color.white,
        };

        public static Color PaintColor(int index) => PaintColors[((index % PaintColors.Length) + PaintColors.Length) % PaintColors.Length];

        /// <summary>Starts a ribbon of paint that follows `tip` until EndStroke is called.</summary>
        public TrailRenderer BeginStroke(Transform tip, Color color, float width)
        {
            if (_stroke == null)
            {
                _stroke = new Material(Shader.Find("ImagineVR/Unlit")) { name = "Stroke" };
                _stroke.SetColor("_Color", Color.white);
            }
            var go = new GameObject("Stroke");
            go.transform.SetParent(tip, false);
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _stroke;
            trail.time = 100000f;
            trail.minVertexDistance = 0.008f;
            trail.widthMultiplier = width;
            trail.numCapVertices = 3;
            trail.numCornerVertices = 2;
            trail.alignment = LineAlignment.View;
            Color linear = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            trail.startColor = trail.endColor = linear;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = true;
            _strokes.Add(go);
            while (_strokes.Count > MaxStrokes)
            {
                if (_strokes[0] != null) Object.Destroy(_strokes[0]);
                _strokes.RemoveAt(0);
            }
            return trail;
        }

        public void EndStroke(TrailRenderer trail)
        {
            if (trail == null) return;
            trail.transform.SetParent(_root, true);
            trail.emitting = false;
        }

        /// <summary>Glowing balls fired from a toy gun. They bounce and knock things over; each gun fires them its own way.</summary>
        public void Fire(Vector3 position, Vector3 direction, Color color, AssetDef gun = null)
        {
            AssetDef def = Library.Asset("energy_ball") ?? Library.Asset("sphere");
            int pellets = gun != null ? Mathf.Max(1, gun.Pellets) : 1;
            float speed = gun != null ? gun.ShotSpeed : 20f, size = gun != null ? gun.ShotSize : 1f, life = gun != null ? gun.ShotLife : 6f;
            direction = direction.normalized;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 aim = direction;
                if (gun != null && gun.Spread > 0f) aim = Quaternion.Euler(Random.Range(-gun.Spread, gun.Spread), Random.Range(-gun.Spread, gun.Spread), 0f) * aim;
                Prop shot = Create(def, "shot", position + aim * (0.02f * i), Quaternion.identity, Vector3.one * size, color);
                shot.Body.useGravity = true;
                shot.Body.linearVelocity = aim * speed;
                Object.Destroy(shot.gameObject, life);
            }
            Sfx.Play(gun != null ? gun.ShotSound : "zap", position, 0.75f, 0.92f + Random.value * 0.2f);
            Color flash = color; flash.a = 1f;
            Fx.Burst(position, flash, Color.white, pellets > 1 ? 14 : 6, pellets > 1 ? 3.5f : 2.2f, 0.035f, 0.09f, 0f, true);
        }
    }
}
