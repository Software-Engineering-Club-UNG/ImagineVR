using UnityEngine;
using UnityEngine.InputSystem;

namespace ImagineVR
{
    /// <summary>
    /// Lets the user ride or drive through the world they built: a car, a horse, a dragon. The whole
    /// rig moves, with the vehicle drawn around it. The right thumbstick (or W A S D on the desktop)
    /// is throttle and steering. Things that fly cruise above the ground.
    /// </summary>
    public sealed class Vehicle : MonoBehaviour
    {
        const float Cruise = 12f;   // metres above the ground, for things that fly

        Rig _rig;
        World _world;
        Terminal _terminal;
        GameObject _visual;
        AssetDef _def;
        float _speed, _top, _seat, _height, _altitude;

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

        void Update()
        {
            if (_rig == null) return;
            Transform rig = _rig.transform;
            Vector3 p = rig.position;
            Vector3 before = p;
            float turn = 0f, dt = Time.deltaTime;

            if (Riding)
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

                float wanted = stick.y >= 0f ? stick.y * _top : stick.y * _top * 0.4f;
                _speed = Mathf.MoveTowards(_speed, wanted, (Mathf.Abs(wanted) > Mathf.Abs(_speed) ? 6f : 10f) * dt);
                // steers like a car once it is rolling, and can still be turned on the spot
                turn = stick.x * 70f * Mathf.Clamp(Mathf.Abs(_speed) / 3f, 0.4f, 1f) * (_speed < -0.1f ? -1f : 1f) * dt;
                rig.Rotate(0f, turn, 0f, Space.World);
                p += rig.forward * (_speed * dt);
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
            target += _altitude + (Riding ? _seat : 0f);
            _height = Mathf.Lerp(_height, target, 1f - Mathf.Exp(-dt * 8f));
            p.y = _height;
            rig.position = p;

            if (_terminal != null && (p != before || turn != 0f)) _terminal.Carry(p - before, turn, p);
        }
    }
}
