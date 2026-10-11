using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace ImagineVR
{
    /// <summary>
    /// The user: a head (camera) and two hands. In the headset they follow tracking. With no
    /// headset (the editor, or a desktop build) the mouse looks around and the keyboard stands in
    /// for the controllers, so everything can be tried without putting the headset on.
    ///
    /// Controls:            headset                     desktop
    ///   talk               hold A (right)              hold Space
    ///   take / let go      touch it, or grip           E (right hand), Q (left hand)
    ///   use held thing     trigger                     left mouse button
    ///   next colour / demo B (right)                   B
    ///   recentre terminal  X (left)                    R
    ///   reset everything   hold X for two seconds      hold R (or F12 at once)
    ///   tuck terminal away Y (left)                    C
    ///   move               right thumbstick            W A S D
    ///   step / smooth turn click right thumbstick      T
    ///   look                                           hold right mouse button
    /// </summary>
    public sealed class Rig : MonoBehaviour
    {
        public Transform Head { get; private set; }
        public Camera Cam { get; private set; }
        public Transform LeftHand { get; private set; }
        public Transform RightHand { get; private set; }
        public Grabber LeftGrabber { get; private set; }
        public Grabber RightGrabber { get; private set; }
        public bool XrActive { get; private set; }

        /// <summary>
        /// Roughly where the A button is on the right controller, in the hand's own space, for the
        /// hint that points at it. An estimate from the shape of a Touch controller: nudge it on the headset.
        /// </summary>
        public static readonly Vector3 AButton = new Vector3(0.012f, 0.022f, 0.03f);

        /// <summary>Something was taken into a hand (true for the right one), or let go of.</summary>
        public event System.Action<Prop, bool> Grabbed, Released;
        internal void OnGrabbed(Prop prop, bool right) { Grabbed?.Invoke(prop, right); }
        internal void OnReleased(Prop prop, bool right) { Released?.Invoke(prop, right); }

        InputAction _talk, _alt, _menu, _fold, _gripL, _gripR, _triggerL, _triggerR, _stickR, _stickClick;
        InputAction _posL, _rotL, _posR, _rotR, _look, _lookHold;
        TrackedPoseDriver _headDriver;
        float _yaw, _pitch;
        bool _floorSet;
        GameObject _handVisualL, _handVisualR;

        // On the desktop, keys typed into the prompt box must not also act as controller buttons.
        bool Blocked => !XrActive && ImagineApp.Typing;

        public bool TalkHeld => !Blocked && _talk.IsPressed();
        public bool TalkPressed => !Blocked && _talk.WasPressedThisFrame();
        public bool TalkReleased => _talk.WasReleasedThisFrame();
        public bool AltPressed => !Blocked && _alt.WasPressedThisFrame();
        public bool MenuPressed => !Blocked && _menu.WasPressedThisFrame();
        public bool MenuHeld => !Blocked && _menu.IsPressed();
        public bool FoldPressed => !Blocked && _fold.WasPressedThisFrame();
        public Vector2 Stick => _stickR.ReadValue<Vector2>();
        public bool StickClicked => !Blocked && _stickClick.WasPressedThisFrame();

        public float Grip(bool right) => Blocked ? 0f : (right ? _gripR : _gripL).ReadValue<float>();
        public float Trigger(bool right) => Blocked ? 0f : (right ? _triggerR : _triggerL).ReadValue<float>();

        public void Build()
        {
            var offset = new GameObject("Offset");
            offset.transform.SetParent(transform, false);

            var headGo = new GameObject("Head");
            headGo.tag = "MainCamera";
            headGo.transform.SetParent(offset.transform, false);
            headGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            Head = headGo.transform;
            Cam = headGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 3000f;
            Cam.clearFlags = CameraClearFlags.Skybox;
            Cam.backgroundColor = Color.black;
            Cam.allowHDR = false;
            Cam.fieldOfView = 75f;
            headGo.AddComponent<AudioListener>();

            _headDriver = headGo.AddComponent<TrackedPoseDriver>();
            var headPos = new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            var headRot = new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            _headDriver.positionInput = new InputActionProperty(headPos);
            _headDriver.rotationInput = new InputActionProperty(headRot);
            _headDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            _headDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            headPos.Enable();
            headRot.Enable();
            _headDriver.enabled = false;   // switched on once a headset is actually running

            LeftHand = MakeHand(offset.transform, "LeftHand", false, out _handVisualL);
            RightHand = MakeHand(offset.transform, "RightHand", true, out _handVisualR);

            _talk = Button("Talk", "<XRController>{RightHand}/primaryButton", "<Keyboard>/space");
            _alt = Button("Alt", "<XRController>{RightHand}/secondaryButton", "<Keyboard>/b");
            _menu = Button("Menu", "<XRController>{LeftHand}/primaryButton", "<Keyboard>/r");
            _fold = Button("Fold", "<XRController>{LeftHand}/secondaryButton", "<Keyboard>/c");
            _gripR = Axis("GripR", "<XRController>{RightHand}/grip", "<Keyboard>/e");
            _gripL = Axis("GripL", "<XRController>{LeftHand}/grip", "<Keyboard>/q");
            _triggerR = Axis("TriggerR", "<XRController>{RightHand}/trigger", "<Mouse>/leftButton");
            _triggerL = Axis("TriggerL", "<XRController>{LeftHand}/trigger", "<Keyboard>/z");
            _stickR = new InputAction("StickR", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
            _stickR.Enable();
            _stickClick = Button("StickClick", "<XRController>{RightHand}/thumbstickClicked", "<Keyboard>/t");
            _posL = Pose("PosL", "<XRController>{LeftHand}/devicePosition", "Vector3");
            _rotL = Pose("RotL", "<XRController>{LeftHand}/deviceRotation", "Quaternion");
            _posR = Pose("PosR", "<XRController>{RightHand}/devicePosition", "Vector3");
            _rotR = Pose("RotR", "<XRController>{RightHand}/deviceRotation", "Quaternion");
            _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
            _look.Enable();
            _lookHold = Button("LookHold", "<Mouse>/rightButton", null);

            LeftGrabber = LeftHand.gameObject.AddComponent<Grabber>();
            LeftGrabber.Init(this, false);
            RightGrabber = RightHand.gameObject.AddComponent<Grabber>();
            RightGrabber.Init(this, true);

            Application.onBeforeRender += UpdateHandPoses;
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= UpdateHandPoses;
        }

        static InputAction Button(string name, string binding, string desktop)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding(binding);
            if (desktop != null) action.AddBinding(desktop);
            action.Enable();
            return action;
        }

        static InputAction Axis(string name, string binding, string desktop)
        {
            var action = new InputAction(name, InputActionType.Value, expectedControlType: "Axis");
            action.AddBinding(binding);
            if (desktop != null) action.AddBinding(desktop);
            action.Enable();
            return action;
        }

        static InputAction Pose(string name, string binding, string type)
        {
            var action = new InputAction(name, InputActionType.Value, binding, expectedControlType: type);
            action.Enable();
            return action;
        }

        Transform MakeHand(Transform parent, string name, bool right, out GameObject visual)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(right ? 0.22f : -0.22f, 1.25f, 0.4f);

            // a small solid sphere so hands can push things around
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.05f;
            col.sharedMaterial = PropSystem.Bouncy(0.1f);

            visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var mb = new MeshBuilder();
            Color shell = Pal.Hex("#2A2D34"), accent = right ? Pal.Hex("#FF8A4C") : Pal.Hex("#6CB6FF");
            mb.Col(shell).Capsule(new Vector3(0f, -0.035f, -0.06f), 0.022f, 0.1f, 8);
            mb.Push(new Vector3(0f, 0f, 0.012f), new Vector3(90f, 0f, 0f));
            mb.Col(accent).Emit(1f).Torus(Vector3.zero, 0.042f, 0.006f, 14, 5);
            mb.Pop();
            mb.Emit(1f).Sphere(new Vector3(0f, 0f, 0.012f), 0.008f, 0);
            visual.AddComponent<MeshFilter>().sharedMesh = mb.Build("Hand");
            var mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("ImagineVR/Lit")) { name = "Hand" };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        public void ShowHand(bool right, bool show)
        {
            GameObject v = right ? _handVisualR : _handVisualL;
            if (v != null && v.activeSelf != show) v.SetActive(show);
        }

        void Update()
        {
            bool xr = XRSettings.isDeviceActive;
            if (xr != XrActive)
            {
                XrActive = xr;
                _headDriver.enabled = xr;
                _floorSet = false;
            }
            if (XrActive)
            {
                if (!_floorSet) _floorSet = TrySetFloorOrigin();
            }
            else
            {
                DesktopLook();
            }
            UpdateHandPoses();

            // every press goes in the session log, so a test can be replayed from it
            if (TalkPressed) SessionLog.Write("press", "button", "talk");
            if (AltPressed) SessionLog.Write("press", "button", "alt");
            if (MenuPressed) SessionLog.Write("press", "button", "menu");
            if (FoldPressed) SessionLog.Write("press", "button", "fold");
            if (StickClicked) SessionLog.Write("press", "button", "stick");
        }

        static readonly List<XRInputSubsystem> Subsystems = new List<XRInputSubsystem>();

        static bool TrySetFloorOrigin()
        {
            SubsystemManager.GetSubsystems(Subsystems);
            bool ok = false;
            foreach (XRInputSubsystem s in Subsystems) ok |= s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            return ok || Subsystems.Count == 0;
        }

        void DesktopLook()
        {
            if (_lookHold.IsPressed())
            {
                Vector2 d = _look.ReadValue<Vector2>();
                _yaw += d.x * 0.12f;
                _pitch = Mathf.Clamp(_pitch - d.y * 0.12f, -85f, 85f);
            }
            Head.localPosition = new Vector3(0f, 1.6f, 0f);
            Head.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        /// <summary>Points the desktop camera somewhere. Used by the snapshot tool.</summary>
        public void DesktopLookAt(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = pitch;
            Head.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        void UpdateHandPoses()
        {
            if (LeftHand == null) return;
            if (XrActive)
            {
                LeftHand.localPosition = _posL.ReadValue<Vector3>();
                LeftHand.localRotation = Safe(_rotL.ReadValue<Quaternion>());
                RightHand.localPosition = _posR.ReadValue<Vector3>();
                RightHand.localRotation = Safe(_rotR.ReadValue<Quaternion>());
            }
            else
            {
                // hands hover in front of the camera
                LeftHand.position = Head.TransformPoint(new Vector3(-0.22f, -0.28f, 0.45f));
                LeftHand.rotation = Head.rotation;
                RightHand.position = Head.TransformPoint(new Vector3(0.22f, -0.28f, 0.45f));
                RightHand.rotation = Head.rotation;
            }
        }

        static Quaternion Safe(Quaternion q)
        {
            return q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f ? Quaternion.identity : q;
        }

        public void Haptic(bool right, float amplitude, float seconds)
        {
            if (!XrActive) return;
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(right ? XRNode.RightHand : XRNode.LeftHand);
            if (device.isValid) device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
        }
    }

    /// <summary>
    /// One hand's ability to pick things up. Grip grabs what the hand is touching, or failing
    /// that, what it is pointing at (which then flies to the hand). Trigger uses the held thing.
    ///
    /// Something the user asked for and was given is easier still, because a newcomer does not
    /// know what a grip button is: touching it, or pulling the trigger at it, takes it, and it
    /// then stays in the hand on its own. Squeezing the grip lets a tool go; anything else is
    /// then held by the grip in the usual way, and thrown by letting go.
    /// </summary>
    public sealed class Grabber : MonoBehaviour
    {
        Rig _rig;
        bool _right;
        Prop _held, _near;
        bool _gripDown, _triggerDown;
        bool _latched;        // stays in the hand without the grip being held
        bool _using, _replaceUse;
        bool _waitTrigger;    // the trigger pull that took it must not also fire it
        Vector3 _lastPos, _velocity, _angular;
        Quaternion _lastRot = Quaternion.identity;
        Vector3 _gripOffset;
        Quaternion _gripRotation = Quaternion.identity;
        float _fly = 1f;
        TrailRenderer _stroke;
        Transform _tip;
        int _paintIndex;
        float _lastShot;
        static readonly Collider[] Hits = new Collider[16];

        public Prop Held => _held;
        public Vector3 Velocity => _velocity;

        public void Init(Rig rig, bool right)
        {
            _rig = rig;
            _right = right;
            _lastPos = transform.position;
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 v = (transform.position - _lastPos) / dt;
            _velocity = Vector3.Lerp(_velocity, v, 0.5f);
            Quaternion dq = transform.rotation * Quaternion.Inverse(_lastRot);
            dq.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (!float.IsNaN(axis.x)) _angular = Vector3.Lerp(_angular, axis * (angle * Mathf.Deg2Rad / dt), 0.5f);
            _lastPos = transform.position;
            _lastRot = transform.rotation;

            float trigger = _rig.Trigger(_right);
            bool trig = trigger > 0.4f;

            bool grip = _rig.Grip(_right) > 0.55f;
            if (grip && !_gripDown)
            {
                SessionLog.Write("press", "button", _right ? "grip_right" : "grip_left", "holding", _held != null ? _held.Def.Id : null);
                if (_held == null) TryGrab(false);
                else if (_latched && _held.Def.Tool) Release(true);
                else _latched = false;   // now held by the grip: letting go throws it
            }
            else if (!grip && _gripDown) { if (_held != null && !_latched) Release(true); }
            _gripDown = grip;

            if (_held == null)
            {
                if (trig && !_triggerDown) TryGrab(true);
                if (_held == null) Reach();
            }
            if (!trig) _waitTrigger = false;
            if (_held != null && !_waitTrigger) Use(trig, trigger);
            if (_held != null && !trig) EndUse(_held);
            _triggerDown = trig;

            if (_held != null && _held.Def.Behavior == "paint" && _rig.AltPressed && _right)
            {
                _paintIndex++;
                _held.SetTint(PropSystem.PaintColor(_paintIndex));
                Sfx.Play("blip", transform.position, 0.4f, 1.3f);
            }
        }

        /// <summary>What the hand is touching and could pick up, nearest first; null when nothing.</summary>
        Prop Touching(float radius, bool waitingOnly)
        {
            Prop best = null;
            float bestScore = float.MaxValue;
            int n = Physics.OverlapSphereNonAlloc(transform.position, radius, Hits);
            for (int i = 0; i < n; i++)
            {
                Prop p = Hits[i].GetComponentInParent<Prop>();
                if (p == null || p.Held || (waitingOnly && !p.Floating)) continue;
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d < bestScore) { best = p; bestScore = d; }
            }
            return best;
        }

        /// <summary>
        /// Grip takes anything in reach or pointed at. The trigger takes only something that is
        /// waiting to be taken (so it never grabs by accident), and that pull does not also fire it.
        /// </summary>
        void TryGrab(bool byTrigger)
        {
            Prop best = Touching(0.16f, byTrigger);
            if (best == null && Physics.SphereCast(transform.position, 0.14f, transform.forward, out RaycastHit hit, byTrigger ? 2.5f : 9f))
            {
                Prop p = hit.collider.GetComponentInParent<Prop>();
                if (p != null && !p.Held && (!byTrigger || p.Floating)) best = p;
            }
            if (best == null)
            {
                if (!byTrigger) SessionLog.Write("grab", "hand", _right ? "right" : "left", "result", "nothing in reach");
                return;
            }
            Grab(best, byTrigger ? "trigger" : "grip");
            _latched = byTrigger || best.Def.Tool;   // a tool stays in the hand; the grip is squeezed again to put it down
            _waitTrigger = byTrigger;
        }

        /// <summary>
        /// An empty hand near things: something waiting to be taken jumps into the hand that
        /// touches it, and anything else in reach answers with a small tick and swells a little,
        /// so the user can feel and see what a squeeze of the grip would pick up.
        /// </summary>
        void Reach()
        {
            Prop waiting = Touching(0.11f, true);
            if (waiting != null)
            {
                Near(null);
                Grab(waiting, "touch");
                _latched = true;
                _waitTrigger = _triggerDown;
                return;
            }
            Near(Touching(0.16f, false));
        }

        void Near(Prop prop)
        {
            if (prop == _near) return;
            if (_near != null && _near.Visual != null) _near.Visual.localScale = Vector3.one;
            _near = prop;
            if (prop == null) return;
            prop.Visual.localScale = Vector3.one * 1.1f;
            _rig.Haptic(_right, 0.18f, 0.02f);
        }

        void Grab(Prop prop, string how)
        {
            Near(null);
            SessionLog.Write("grab", "hand", _right ? "right" : "left", "result", "took", "asset", prop.Def.Id, "id", prop.GroupId, "how", how, "given", prop.Given);
            RuleBody rule = prop.GetComponent<RuleBody>();
            if (rule != null) rule.Engine.Emit("grab", rule.Entity);
            _held = prop;
            prop.Holder = this;
            prop.Floating = false;
            prop.Body.isKinematic = true;
            var blueprint=prop.GetComponent<BlueprintActor>();
            var grip=blueprint!=null?BlueprintData.Wrap(blueprint.Recipe).Obj("capabilities")?.TryGetValue("grip",out object gripData)==true?gripData as Dictionary<string,object>:null:null;
            if(grip!=null)
            {
                _gripRotation=Quaternion.Euler(BlueprintData.Vec(grip,"rotation",Vector3.zero));
                _gripOffset=-(_gripRotation*Vector3.Scale(BlueprintData.Vec(grip,"position",Vector3.zero),prop.transform.localScale));
            }
            else if (prop.Def.Tool)
            {
                // a tool's handle sits in the fist and its length runs along the controller's forward axis
                _gripOffset = Vector3.zero;
                _gripRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else if (prop.Def.Holdable)
            {
                // sits in the palm by its centre, keeping whatever way up it was
                float r = prop.Def.Bounds.extents.magnitude * prop.transform.localScale.x;
                _gripRotation = Quaternion.Inverse(transform.rotation) * prop.transform.rotation;
                Vector3 centre = Vector3.Scale(prop.Def.Bounds.center, prop.transform.localScale);
                _gripOffset = new Vector3(0f, 0f, Mathf.Min(0.04f + r * 0.6f, 0.2f)) - _gripRotation * centre;
            }
            else
            {
                _gripOffset = transform.InverseTransformPoint(prop.transform.position);
                if (_gripOffset.magnitude > 0.35f) _gripOffset = _gripOffset.normalized * 0.3f;
                _gripRotation = Quaternion.Inverse(transform.rotation) * prop.transform.rotation;
            }
            _fly = 0f;
            _latched = false;
            _rig.ShowHand(_right, false);
            _rig.Haptic(_right, 0.5f, 0.05f);
            Sfx.Play("grab", transform.position, 0.6f);
            _rig.OnGrabbed(prop, _right);
        }


        void Release(bool thrown)
        {
            Prop prop = _held;
            _held = null;
            _latched = false;
            EndUse(prop);
            if (prop == null) return;
            SessionLog.Write("release", "hand", _right ? "right" : "left", "asset", prop.Def.Id, "thrown", thrown, "speed", _velocity.magnitude);
            _rig.OnReleased(prop, _right);
            prop.Holder = null;
            prop.Visual.localPosition = Vector3.zero;
            prop.Visual.localRotation = Quaternion.identity;
            prop.Body.isKinematic = false;
            prop.Body.position = prop.transform.position;
            if (thrown)
            {
                prop.Body.linearVelocity = _velocity * 1.25f;
                prop.Body.angularVelocity = _angular;
            }
            RuleBody rule = prop.GetComponent<RuleBody>();
            if (rule != null) rule.Released(thrown);
            _rig.ShowHand(_right, true);
            Sfx.Play("release", transform.position, 0.4f);
        }

        public void ForceRelease()
        {
            if (_held != null) Release(false);
        }

        Vector3 TargetPosition => transform.TransformPoint(_gripOffset);
        Quaternion TargetRotation => transform.rotation * _gripRotation;

        void FixedUpdate()
        {
            if (_held == null) return;
            if (_held.Body == null) { _held = null; _latched = false; _rig.ShowHand(_right, true); return; }
            _fly = Mathf.MoveTowards(_fly, 1f, Time.fixedDeltaTime / 0.14f);
            Vector3 target = Vector3.Lerp(_held.Body.position, TargetPosition, Mathf.Lerp(0.25f, 1f, _fly));
            _held.Body.MovePosition(target);
            _held.Body.MoveRotation(Quaternion.Slerp(_held.Body.rotation, TargetRotation, Mathf.Lerp(0.3f, 1f, _fly)));
        }

        void LateUpdate()
        {
            // the mesh tracks the hand exactly; the body catches up through physics
            if (_held == null || _held.Visual == null || _fly < 1f) return;
            _held.Visual.SetPositionAndRotation(TargetPosition, TargetRotation);
        }

        // ------------------------------------------------------------------ using what is held

        void Use(bool pressed, float amount)
        {
            Prop prop = _held;
            if (pressed && !_using)
            {
                _using = true;
                RuleBody rule = prop.GetComponent<RuleBody>();
                _replaceUse = rule != null && rule.Engine.Emit("use_start", rule.Entity);
            }
            if (_replaceUse) return;
            Vector3 tipPosition = TargetPosition + (TargetRotation * Vector3.up) * prop.Length;
            Vector3 aim = TargetRotation * Vector3.up;
            switch (prop.Def.Behavior)
            {
                case "paint":
                    if (pressed && _stroke == null)
                    {
                        if (_tip == null)
                        {
                            _tip = new GameObject("Tip").transform;
                            _tip.SetParent(transform, false);
                        }
                        _tip.position = tipPosition;
                        Color c = prop.Tint == Color.white ? PropSystem.PaintColor(_paintIndex) : prop.Tint;
                        _stroke = prop.System.BeginStroke(_tip, c, Mathf.Lerp(0.008f, 0.035f, amount));
                    }
                    else if (!pressed && _stroke != null)
                    {
                        prop.System.EndStroke(_stroke);
                        _stroke = null;
                    }
                    if (_tip != null) _tip.position = tipPosition;
                    if (pressed && Time.frameCount % 9 == 0) _rig.Haptic(_right, 0.12f, 0.02f);
                    break;
                case "blaster":
                    if (pressed && !_triggerDown && Time.time - _lastShot > 0.15f)
                    {
                        _lastShot = Time.time;
                        Color c = prop.Tint == Color.white ? Pal.Neon : prop.Tint;
                        prop.System.Fire(tipPosition + aim * 0.1f, aim, c);
                        _rig.Haptic(_right, 0.8f, 0.06f);
                    }
                    break;
                case "wand":
                    if (pressed)
                    {
                        Telekinesis(tipPosition, aim);
                        if (!_triggerDown) Sfx.Play("sparkle", tipPosition, 0.5f);
                        if (Time.frameCount % 5 == 0) Fx.Burst(tipPosition, new Color(1f, 0.9f, 0.5f), new Color(0.8f, 0.6f, 1f), 2, 0.6f, 0.025f, 0.6f, -0.1f, true);
                    }
                    break;
                case "bubbles":
                    if (pressed && Time.frameCount % 4 == 0)
                    {
                        Fx.Burst(tipPosition, new Color(0.75f, 0.95f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.5f), 1, 0.7f, 0.07f, 4f, -0.05f, false);
                    }
                    break;
                case "confetti":
                    if (pressed && !_triggerDown)
                    {
                        Fx.Confetti(tipPosition);
                        Sfx.Play("confetti", tipPosition, 0.9f);
                        _rig.Haptic(_right, 1f, 0.08f);
                    }
                    break;
            }
        }

        void EndUse(Prop prop)
        {
            RuleBody rule = prop != null ? prop.GetComponent<RuleBody>() : null;
            if (_using && rule != null) rule.Engine.Emit("use_end", rule.Entity);
            _using = _replaceUse = false;
            if (_stroke != null && prop != null) prop.System.EndStroke(_stroke);
            _stroke = null;
        }

        /// <summary>The wand: whatever loose thing it points at is drawn to float in front of it.</summary>
        void Telekinesis(Vector3 tip, Vector3 aim)
        {
            Prop target = null;
            float best = 0.8f;
            foreach (Prop p in _held.System.All)
            {
                if (p == null || p == _held || p.Held || p.Body.isKinematic) continue;
                Vector3 to = p.transform.position - tip;
                if (to.magnitude > 12f) continue;
                float dot = Vector3.Dot(to.normalized, aim);
                if (dot > best) { best = dot; target = p; }
            }
            if (target == null) return;
            Vector3 hover = tip + aim * 1.4f;
            Vector3 force = (hover - target.transform.position) * 30f - target.Body.linearVelocity * 6f;
            target.Body.AddForce(Vector3.ClampMagnitude(force, 80f) * target.Body.mass - Physics.gravity * target.Body.mass);
        }
    }
}
