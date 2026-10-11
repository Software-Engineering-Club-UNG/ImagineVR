using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// A small label that floats beside something and says how to use it, with an optional
    /// pulsing ring marking the exact spot (a button). It follows its anchor, faces the user,
    /// and goes away when dismissed, when its time is up, or when its anchor does.
    /// </summary>
    public sealed class Callout : MonoBehaviour
    {
        Transform _anchor, _head;
        Vector3 _offset;
        float _lift, _until, _shown;
        bool _leaving;
        GameObject _ring;
        Material _panel;

        /// <param name="offset">Where on the anchor, in the anchor's own space.</param>
        /// <param name="lift">Metres straight up from there (negative hangs it below).</param>
        /// <param name="life">Seconds before it leaves on its own; 0 stays until dismissed.</param>
        public static Callout Show(Transform anchor, Vector3 offset, float lift, string rich, float fontSize, float life)
        {
            World world = World.Instance;
            if (anchor == null || world == null || world.Font == null) return null;
            var go = new GameObject("Callout");
            go.transform.SetParent(world.transform.parent, false);
            Callout c = go.AddComponent<Callout>();
            c._anchor = anchor;
            c._head = world.Head;
            c._offset = offset;
            c._lift = lift;
            c._until = life > 0f ? Time.time + life : float.MaxValue;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            TextMeshPro tmp = textGo.AddComponent<TextMeshPro>();
            tmp.font = world.Font;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = Color.white;
            tmp.text = Terminal.Tag(Terminal.Text, rich);
            tmp.ForceMeshUpdate();
            Vector2 size = tmp.GetPreferredValues();
            tmp.rectTransform.sizeDelta = size;
            var tr = textGo.GetComponent<MeshRenderer>();
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;

            float pad = size.y * 0.55f;
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "Back";
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(go.transform, false);
            back.transform.localScale = new Vector3(size.x + pad * 2f, size.y + pad, 1f);
            c._panel = new Material(Shader.Find("ImagineVR/Panel")) { name = "Callout" };
            c._panel.SetVector("_Size", new Vector4(size.x + pad * 2f, size.y + pad, 0f, 0f));
            c._panel.SetFloat("_TitleHeight", 0f);
            c._panel.SetFloat("_Radius", (size.y + pad) * 0.5f);
            c._panel.SetFloat("_Border", Mathf.Max(0.0012f, size.y * 0.06f));
            c._panel.SetColor("_GlowColor", Pal.Hex(Terminal.Accent));
            var br = back.GetComponent<MeshRenderer>();
            br.sharedMaterial = c._panel;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.transform.localScale = Vector3.zero;
            c.Place();
            return c;
        }

        /// <summary>Adds a pulsing ring lying on the anchor at the callout's spot, to mark a button.</summary>
        public Callout WithRing(float radius)
        {
            _ring = new GameObject("HintRing");
            _ring.transform.SetParent(_anchor, false);
            _ring.transform.localPosition = _offset;
            var mb = new MeshBuilder();
            mb.Col(Color.white).Torus(Vector3.zero, radius, radius * 0.22f, 20, 6);
            _ring.AddComponent<MeshFilter>().sharedMesh = mb.Build("HintRing");
            MeshRenderer mr = _ring.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("ImagineVR/Unlit")) { name = "HintRing" };
            material.SetColor("_Color", Pal.Hex(Terminal.Accent));
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return this;
        }

        public void Dismiss() { _leaving = true; }

        void Place()
        {
            Vector3 p = _anchor.TransformPoint(_offset) + Vector3.up * _lift;
            transform.position = p;
            if (_head == null) return;
            Vector3 away = p - _head.position;
            if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        void LateUpdate()
        {
            if (_anchor == null || !_anchor.gameObject.activeInHierarchy || Time.time > _until) _leaving = true;
            _shown = Mathf.MoveTowards(_shown, _leaving ? 0f : 1f, Time.deltaTime / 0.25f);
            if (_leaving && _shown <= 0f) { Destroy(gameObject); return; }
            if (_anchor != null) Place();
            transform.localScale = Vector3.one * (_leaving ? Ease.Smooth(_shown) : Ease.OutBack(_shown));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            _panel.SetFloat("_Glow", 0.45f + 0.4f * pulse);
            if (_ring != null) _ring.transform.localScale = Vector3.one * (1f + 0.3f * pulse) * Mathf.Clamp01(_shown * 2f);
        }

        void OnDestroy()
        {
            if (_ring != null) Destroy(_ring);
            if (_panel != null) Destroy(_panel);
        }
    }

    /// <summary>
    /// Tells a newcomer the few things they cannot guess, each at the moment it matters and
    /// only until they have done it:
    ///   - how to talk, once, when they arrive (a ring on the A button);
    ///   - how to take something they asked for, beside the thing itself;
    ///   - what the thing in their hand does, the first couple of times;
    ///   - how to put the terminal away, once a world is built and it is in the way.
    /// Nothing here stays on screen. <see cref="Arm"/> starts over, for the next person.
    /// </summary>
    public sealed class Hints : MonoBehaviour
    {
        public Rig Rig;
        public Terminal Terminal;
        public World World;
        public Conductor Conductor;

        const float TalkLife = 45f, HandText = 0.2f, TerminalText = 0.34f;
        const int TeachTakes = 3, TeachUses = 2, TeachFolds = 2, TeachRides = 3;

        Callout _talk, _fold, _use, _ride;
        readonly Dictionary<Prop, Callout> _take = new Dictionary<Prop, Callout>();
        bool _talkOwed;
        float _talkAt;
        int _takes, _uses, _folds, _rides;

        void Start()
        {
            World.Given += OnGiven;
            Rig.Grabbed += OnGrabbed;
            Rig.Released += (prop, right) => { if (_use != null) { _use.Dismiss(); _use = null; } };
            if (World.Vehicle != null) World.Vehicle.Mounted += OnMounted;
        }

        /// <summary>Starts the hints over, as for someone who has just put the headset on.</summary>
        public void Arm()
        {
            foreach (Callout c in new[] { _talk, _fold, _use, _ride }) if (c != null) c.Dismiss();
            foreach (Callout c in _take.Values) if (c != null) c.Dismiss();
            _take.Clear();
            _talk = _fold = _use = _ride = null;
            _takes = _uses = _folds = _rides = 0;
            _talkOwed = true;
            _talkAt = Time.time + 2f;   // after the welcome has typed itself out
        }

        bool Xr => Rig.XrActive;

        void Update()
        {
            // how to talk: shown once on arrival, gone the moment they do it (or after a while regardless)
            if (_talkOwed && Time.time >= _talkAt && !ImagineApp.Quiet)
            {
                _talkOwed = false;
                string text = Xr ? "hold <b>A</b> and say what you imagine" : "hold <b>Space</b> and speak, or <b>Enter</b> to type";
                _talk = Callout.Show(Rig.RightHand, Rig.AButton, 0.09f, text, HandText, TalkLife);
                if (_talk != null && Xr) _talk.WithRing(0.011f);
                SessionLog.Write("hint", "what", "talk");
            }
            if (Rig.TalkPressed || Conductor.Busy)   // they have asked for something, one way or another
            {
                _talkOwed = false;
                if (_talk != null) { _talk.Dismiss(); _talk = null; }
            }

            // how to make a ride go: it stays until they push the stick, or get off
            if (_ride != null && (Moving || !World.Vehicle.Riding)) { _ride.Dismiss(); _ride = null; }
            if (Moving && World.Vehicle != null && World.Vehicle.Riding) _rides = TeachRides;   // they have found it

            if (Terminal.Collapsed)
            {
                _folds = TeachFolds;   // they have found it
                if (_fold != null) { _fold.Dismiss(); _fold = null; }
            }
            else if (!World.IsDark && !Conductor.Busy && Mathf.Max(Rig.Stick.magnitude, Rig.MoveStick.magnitude) > 0.6f) OfferFold();   // walking with the terminal in the way
        }

        bool Moving => World.Vehicle != null && Mathf.Abs(World.Vehicle.Speed) > 0.5f;

        /// <summary>
        /// A ride that is not moving looks broken: in testing a dragon and a flying saucer were
        /// each sat on for a minute without the stick being touched. So the stick is pointed out.
        /// </summary>
        void OnMounted(bool flies)
        {
            if (_rides >= TeachRides || ImagineApp.Quiet) return;
            _rides++;
            if (_ride != null) _ride.Dismiss();
            string go = flies ? "fly" : "drive";
            string text = Xr ? "<b>Left stick</b> to " + go + " and strafe · <b>right stick</b> turns 45° · click it to get off" : "<b>W A S D</b> to " + go + " and strafe · mouse to look · hold <b>Q</b> to get off";
            _ride = Callout.Show(Rig.LeftHand, Vector3.zero, 0.2f, text, HandText, 0f);
            SessionLog.Write("hint", "what", "ride", "asset", World.Vehicle.Asset);
        }

        void OfferFold()
        {
            if (_folds >= TeachFolds || _fold != null || Terminal.Collapsed || ImagineApp.Quiet) return;
            _folds++;
            _fold = Callout.Show(Terminal.transform, Terminal.FoldHintAnchor, 0f,
                "<b>" + (Xr ? "Y" : "C") + "</b> tucks the terminal away", TerminalText, 8f);
            SessionLog.Write("hint", "what", "fold");
        }

        void OnGiven(Prop prop)
        {
            if (_takes >= TeachTakes || prop == null || ImagineApp.Quiet) return;
            string text = Xr ? "hold the <b>grip</b> to take it · release to drop or throw" : "hold <b>E / Q</b> to take it · release to drop or throw";
            _take[prop] = Callout.Show(prop.transform, Vector3.zero, -0.13f, text, HandText, 0f);
            SessionLog.Write("hint", "what", "take", "asset", prop.Def.Id);
        }

        void OnGrabbed(Prop prop, bool right)
        {
            if (_take.TryGetValue(prop, out Callout waiting))
            {
                if (waiting != null) waiting.Dismiss();
                _take.Remove(prop);
            }
            if (!prop.Given) return;
            _takes++;
            if (_uses >= TeachUses || ImagineApp.Quiet) return;
            _uses++;
            if (_use != null) _use.Dismiss();
            string text;
            string key = right ? "E" : "Q";
            if (prop.Def.Tool) text = Xr ? "keep <b>grip</b> held · <b>trigger</b> uses · release grip to drop or throw" : "keep <b>" + key + "</b> held · click uses · release " + key + " to drop or throw";
            else text = Xr ? "keep <b>grip</b> held · release to drop or throw" : "keep <b>" + key + "</b> held · release to drop or throw";
            _use = Callout.Show(right ? Rig.RightHand : Rig.LeftHand, Vector3.zero, 0.13f, text, HandText, 7f);
            SessionLog.Write("hint", "what", "use", "asset", prop.Def.Id);
        }
    }
}
