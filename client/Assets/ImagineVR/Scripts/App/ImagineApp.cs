using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace ImagineVR
{
    /// <summary>
    /// The whole application. The scene contains one object with this component; everything else
    /// (the user's rig, the world, the terminal, networking, voice) is built from code at startup.
    /// </summary>
    public sealed class ImagineApp : MonoBehaviour
    {
        public static ImagineApp Instance { get; private set; }

        public Rig Rig { get; private set; }
        public World World { get; private set; }
        public Terminal Terminal { get; private set; }
        public Conductor Conductor { get; private set; }
        public TurnClient Client { get; private set; }
        public Voice Voice { get; private set; }
        public Hints Hints { get; private set; }
        public bool Online { get; private set; }

        /// <summary>How long X (or R) is held before everything is reset, and how long before the countdown shows.</summary>
        const float ResetHold = 2f, ResetGrace = 0.45f;

        /// <summary>Set by tools that drive the app themselves (snapshots): skips the welcome text and server check.</summary>
        public static bool Quiet;
        /// <summary>True while the desktop text box has the keyboard, so letter keys do not also act as controller buttons.</summary>
        public static bool Typing;

        ImagineSettings _settings;
        TMP_FontAsset _font;
        string _typed = "";
        bool _focusField;
        bool _foveationSet;
        float _holdSince = -1f, _nextSample, _stickPeak;
        int _frames;

        void Awake()
        {
            Instance = this;
            Typing = false;
            SessionLog.Open();
            // The headset sets the pace (90 Hz when it agrees to: see QuestFeature); physics keeps up with it.
            Time.fixedDeltaTime = 1f / 90f;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;

            _settings = ImagineSettings.Load();
            SessionLog.Write("start", "version", Application.version, "unity", Application.unityVersion,
                "device", SystemInfo.deviceModel, "os", SystemInfo.operatingSystem,
                "server", _settings.ServerUrl, "settings_from", _settings.Source, "log", SessionLog.Path);
            _font = Resources.Load<TMP_FontAsset>("Fonts/ImagineMono");
            if (_font == null) _font = TMP_Settings.defaultFontAsset;

            Library.EnsureLoaded();
            Sfx.Init(transform);

            Rig = new GameObject("Rig").AddComponent<Rig>();
            Rig.transform.SetParent(transform, false);
            Rig.Build();

            World = new GameObject("World").AddComponent<World>();
            World.transform.SetParent(transform, false);
            World.Init(Rig.Head, _font);

            Terminal = new GameObject("Terminal").AddComponent<Terminal>();
            Terminal.transform.SetParent(transform, false);
            Terminal.Build(_font, Rig.Head);

            World.Vehicle = Rig.gameObject.AddComponent<Vehicle>();
            World.Vehicle.Init(Rig, World, Terminal);

            Client = gameObject.AddComponent<TurnClient>();
            Client.Settings = _settings;
            Client.Pose = UserPose;
            Client.UserState = UserState;
            Client.World = World;
            TextAsset version = Resources.Load<TextAsset>("CatalogVersion");
            if (version != null) Client.CatalogVersion = version.text.Trim();

            Conductor = gameObject.AddComponent<Conductor>();
            Conductor.World = World;
            Conductor.Terminal = Terminal;
            Conductor.Client = Client;
            Conductor.Rig = Rig;

            Voice = gameObject.AddComponent<Voice>();
            Voice.Rig = Rig;
            Voice.Terminal = Terminal;
            Voice.Conductor = Conductor;

            Hints = gameObject.AddComponent<Hints>();
            Hints.Rig = Rig;
            Hints.Terminal = Terminal;
            Hints.World = World;
            Hints.Conductor = Conductor;

            World.Noted += Terminal.Note;
            World.Happened += OnWorldEvent;
            World.Emptied += Conductor.ResetFirstLight;
            Conductor.TurnEnded += _ => NextIdea();
        }

        /// <summary>
        /// Where the user's head is in the world and which way it faces, as "x,z,yaw" for the
        /// server; "home" in the dark room, where the next place is built around them anyway.
        /// </summary>
        string UserPose()
        {
            if (World.Unbuilt) return "home";
            Vector3 p = Rig.Head.position, look = Rig.Head.forward;
            look.y = 0f;
            float yaw = look.sqrMagnitude > 0.01f ? Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg : Rig.transform.eulerAngles.y;
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00},{1:0.00},{2:0.0}", p.x, p.z, yaw);
        }

        /// <summary>The headset's own account of the user, which the model is told to trust over its memory of what it gave them.</summary>
        string UserState()
        {
            var parts = new List<string>();
            Prop left = Rig.LeftGrabber != null ? Rig.LeftGrabber.Held : null, right = Rig.RightGrabber != null ? Rig.RightGrabber.Held : null;
            if (right != null) parts.Add("holding " + right.Def.Id + " in the right hand");
            if (left != null) parts.Add("holding " + left.Def.Id + " in the left hand");
            if (left == null && right == null) parts.Add("hands empty");
            var waiting = new List<string>();
            foreach (Prop p in World.Props.All) if (p != null && p.Given && !p.Held && !waiting.Contains(p.Def.Id)) waiting.Add(p.Def.Id);
            if (waiting.Count > 0) parts.Add("within reach, not yet taken: " + string.Join(", ", waiting));
            parts.Add(World.Vehicle != null && World.Vehicle.Riding ? "riding " + World.Vehicle.Asset : "on foot");
            if (World.Rules.Score != 0) parts.Add("score " + World.Rules.Score);
            return string.Join("; ", parts);
        }

        // ------------------------------------------------------------------ what to try next
        //
        // An empty prompt is the hardest part of a sandbox: the user has to think of something.
        // So there is always one idea beside the talk hint, chosen from what the world is missing
        // right now, and never the same one twice running. None of them is a feature: each is just
        // a sentence the general machinery can already do.

        static readonly string[] Places =
        {
            "a pirate island at sunset", "a neon city in the rain", "a snowy village at night", "the moon, with a golf club",
            "a canyon to fly a dragon through", "a jungle temple", "a haunted graveyard", "an alien planet with two suns",
            "a medieval castle on a cliff", "the bottom of the ocean",
        };
        static readonly string[] ToHold = { "give me a flamethrower", "give me a lightsaber", "give me a bow", "give me a rocket launcher", "give me a giant hammer", "give me a paintbrush" };
        static readonly string[] ToRide = { "let me ride a dragon", "put me in a monster truck", "let me fly a jet", "let me ride a giant snail", "put me on a hoverboard", "let me drive a tank" };
        static readonly string[] Alive = { "fill it with dinosaurs", "send robots after me", "add a herd of deer", "make it rain frogs", "put whales in the sky" };
        static readonly string[] React = { "make them explode when hit", "make them turn gold when hit", "when I hit one, spawn two more", "make them burst into confetti" };
        static readonly string[] Twist = { "turn off gravity", "shrink me to mouse size", "make me a giant", "make it night", "make everything huge", "set up bowling", "add a storm", "build me a racetrack" };
        string _lastIdea;

        void NextIdea()
        {
            var pool = new List<string>();
            if (World.Unbuilt) pool.AddRange(Places);
            else
            {
                bool holding = Rig.LeftGrabber.Held != null || Rig.RightGrabber.Held != null;
                if (!holding) pool.AddRange(ToHold);
                if (World.Vehicle != null && !World.Vehicle.Riding) pool.AddRange(ToRide);
                if (Creature.All.Count == 0) pool.AddRange(Alive);
                else if (World.Rules.Count == 0) { pool.AddRange(React); pool.AddRange(React); }   // the most fun, so the likeliest
                pool.AddRange(Twist);
            }
            pool.Remove(_lastIdea);
            _lastIdea = pool[Random.Range(0, pool.Count)];
            Voice.Idea = _lastIdea;
            Terminal.SetHint(Voice.HintText());
        }

        /// <summary>
        /// The dark room is not silent: a low pad hangs in it until the first place is built, and
        /// comes back when everything is cleared away. A place that names no sound of its own goes quiet.
        /// </summary>
        void DarkRoomSound()
        {
            if (Quiet) return;
            bool dark = World.IsDark && World.Objects.Count == 0;
            if (dark && !_darkHum && !World.Ambience.IsPlaying)
            {
                World.Ambience.Apply(Statement.Make("ambience", "loop", "synth_pad", "volume", 0.3));
                _darkHum = true;
            }
            else if (!dark && _darkHum)
            {
                if (World.Ambience.Current == "synth_pad") World.Ambience.Stop(4f);
                _darkHum = false;
            }
        }
        bool _darkHum;

        void OnWorldEvent(string text)
        {
            SessionLog.Write("event", "text", text);
            Terminal.Event(text);
            Rig.Haptic(true, 0.7f, 0.12f);
            Rig.Haptic(false, 0.7f, 0.12f);
        }

        IEnumerator Start()
        {
            yield return null;
            Terminal.Recenter(true);
            if (Quiet) yield break;
            Sfx.Play("boot", Terminal.transform.position, 0.9f);
            Rig.Haptic(true, 0.3f, 0.5f);
            Rig.Haptic(false, 0.3f, 0.5f);
            Welcome();
            NextIdea();
            Hints.Arm();
            yield return new WaitForSeconds(0.6f);
            yield return CheckServer();
        }

        // ------------------------------------------------------------------ welcome

        void Welcome()
        {
            int width = Mathf.Min(Terminal.Columns - 2, 52);
            bool box = _font != null && _font.HasCharacter('╭', false, true) && _font.HasCharacter('─', false, true) && _font.HasCharacter('│', false, true);
            string tl = box ? "╭" : "+", tr = box ? "╮" : "+", bl = box ? "╰" : "+", br = box ? "╯" : "+";
            string h = box ? "─" : "-", v = box ? "│" : "|";
            string bar = Repeat(h, width - 2);
            string a = Terminal.Accent, d = Terminal.Dim;

            Terminal.Raw(Terminal.Tag(a, tl + bar + tr), width);
            BoxLine(v, width, Terminal.Tag(a, Terminal.Spark) + " " + Terminal.Tag(Terminal.Bright, "Welcome to ") + Terminal.Tag(a, "<b>ImagineVR</b>"), 22);
            BoxLine(v, width, "", 0);
            BoxLine(v, width, Terminal.Tag(d, "  Say what you imagine. Watch it get built."), 43);
            Terminal.Raw(Terminal.Tag(a, bl + bar + br), width);
            Terminal.Blank();
            if (Rig.XrActive || XRSettings.isDeviceActive)
            {
                Terminal.Raw("  " + Terminal.Tag(Terminal.Text, "hold A") + Terminal.Tag(d, " and speak") + "    " +
                             Terminal.Tag(Terminal.Text, "touch") + Terminal.Tag(d, " things to take them"), 46);
                Terminal.Raw("  " + Terminal.Tag(Terminal.Text, "Y") + Terminal.Tag(d, " tucks this away") + "    " +
                             Terminal.Tag(Terminal.Text, "X") + Terminal.Tag(d, " recentres") + "    " +
                             Terminal.Tag(Terminal.Text, "hold X") + Terminal.Tag(d, " starts over"), 56);
            }
            else
            {
                Terminal.Raw("  " + Terminal.Tag(Terminal.Text, "Enter") + Terminal.Tag(d, " to type") + "   " +
                             Terminal.Tag(Terminal.Text, "Space") + Terminal.Tag(d, " to speak") + "   " +
                             Terminal.Tag(Terminal.Text, "right mouse") + Terminal.Tag(d, " to look"), 56);
                Terminal.Raw("  " + Terminal.Tag(Terminal.Text, "E") + Terminal.Tag(d, " grab") + "   " +
                             Terminal.Tag(Terminal.Text, "click") + Terminal.Tag(d, " use") + "   " +
                             Terminal.Tag(Terminal.Text, "C") + Terminal.Tag(d, " tuck away") + "   " +
                             Terminal.Tag(Terminal.Text, "F12") + Terminal.Tag(d, " start over"), 56);
            }
            Terminal.SetHint(Voice.HintText());
        }

        void BoxLine(string v, int width, string rich, int length)
        {
            string a = Terminal.Accent;
            int pad = Mathf.Max(0, width - 3 - length);
            Terminal.Raw(Terminal.Tag(a, v) + " " + rich + new string(' ', pad) + Terminal.Tag(a, v), width);
        }

        static string Repeat(string s, int n)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++) sb.Append(s);
            return sb.ToString();
        }

        IEnumerator CheckServer()
        {
            Terminal.Blank();
            Terminal.Raw(Terminal.Tag(Terminal.Blue, Terminal.Dot) + " " + Terminal.Tag(Terminal.Text, "<b>connect</b>") + Terminal.Tag(Terminal.Dim, "(" + Terminal.Plain(_settings.ServerUrl) + ")"), 12 + _settings.ServerUrl.Length);
            Dictionary<string, object> health = null;
            yield return Client.Health(h => health = h);
            Online = health != null;
            string library = Library.AssetList.Count + " things, and anything else gets made";
            SessionLog.Write("server", "online", Online, "health", health != null ? MiniJson.Serialize(health) : null,
                "app_catalog", Client.CatalogVersion, "objects", Library.AssetList.Count);
            if (Online)
            {
                string model = health.TryGetValue("model", out object m) && m is string ms ? ms : "?";
                int slash = model.LastIndexOf('/');
                if (slash >= 0) model = model.Substring(slash + 1);
                Terminal.Note("online. model " + model + ". library: " + library);
                Terminal.SetTitle(null, Terminal.Tag(Terminal.Green, Terminal.Dot) + Terminal.Tag(Terminal.Dim, " " + model));
                yield return Client.ResetSession();
            }
            else
            {
                Terminal.Note("no server at that address. library: " + library);
                Terminal.Note("nothing can be built until it is reachable: check the wifi, then hold X to try again");
                Terminal.SetTitle(null, Terminal.Tag(Terminal.Red, Terminal.Dot) + Terminal.Tag(Terminal.Dim, " offline"));
            }
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (Rig.XrActive && !_foveationSet) _foveationSet = TryFoveation();
            Terminal.SetFoldKey(Rig.XrActive ? "Y" : "C");
            Sample();
            DarkRoomSound();

            if (Rig.FoldPressed && !Typing) Terminal.Toggle();

            // X (or R): a tap recentres the terminal, and holding it down resets everything
            if (Rig.MenuPressed && !Typing)
            {
                Terminal.Recenter(false);
                _holdSince = Time.unscaledTime;
            }
            if (_holdSince >= 0f)
            {
                float held = Time.unscaledTime - _holdSince;
                if (!Rig.MenuHeld)
                {
                    _holdSince = -1f;
                    Terminal.SetOverride(null);
                }
                else if (held >= ResetHold)
                {
                    _holdSince = -1f;
                    ResetEverything("held " + (Rig.XrActive ? "X" : "R"));
                }
                else if (held >= ResetGrace)
                {
                    // a countdown they can see and feel, so nobody resets the world by leaning on a button
                    const int dots = 10;
                    int lit = Mathf.Clamp(Mathf.CeilToInt((held - ResetGrace) / (ResetHold - ResetGrace) * dots), 0, dots);
                    Terminal.SetOverride(Terminal.Tag(Terminal.Yellow, "Keep holding to start over ") +
                        Terminal.Tag(Terminal.Accent, Repeat(Terminal.Dot, lit)) + Terminal.Tag(Terminal.Faint, Repeat(Terminal.Dot, dots - lit)));
                    if (Time.frameCount % 6 == 0) Rig.Haptic(false, 0.15f + 0.5f * lit / dots, 0.03f);
                }
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && !Rig.XrActive && !Typing)
            {
                if (kb.f12Key.wasPressedThisFrame) ResetEverything("F12");
                if (kb.enterKey.wasPressedThisFrame) _focusField = true;
            }
        }

        static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        static bool TryFoveation()
        {
            SubsystemManager.GetSubsystems(Displays);
            foreach (XRDisplaySubsystem display in Displays)
            {
                display.foveatedRenderingLevel = 1f;
                display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
            }
            return Displays.Count > 0;
        }

        public void BackToDark() { ResetEverything("editor"); }

        /// <summary>
        /// Everything back to how it was when the app started, ready for the next person: the dark
        /// room (their own room, in a headset that shows it), nothing built, nothing held, nothing
        /// being said or on its way, an empty terminal with its welcome, a server that remembers
        /// nothing, and the newcomer's hints again. This is what holding X does.
        /// </summary>
        public void ResetEverything(string why)
        {
            _holdSince = -1f;
            SessionLog.Write("reset", "what", "everything", "why", why);
            Voice.Cancel();
            Conductor.Abort();
            string old = Client.NewSession();
            if (Online) StartCoroutine(Client.ResetSession(old));

            World.ResetEverything(1.5f);   // also lets go of whatever is in the hands

            Terminal.Clear();
            Terminal.SetCollapsed(false);
            Terminal.SetState(TerminalState.Ready);
            Terminal.SetMic(0f);
            Terminal.Recenter(false);
            Welcome();
            Terminal.Blank();
            Terminal.Note("started over: the world, what you were holding and the conversation are all cleared");
            Hints.Arm();
            NextIdea();

            Sfx.Play("whoosh", Terminal.transform.position, 0.7f, 0.8f);
            Rig.Haptic(true, 0.8f, 0.2f);
            Rig.Haptic(false, 0.8f, 0.2f);
        }

        /// <summary>Every two seconds, where the user is and how the app is doing, for the session log.</summary>
        void Sample()
        {
            _frames++;
            _stickPeak = Mathf.Max(_stickPeak, Rig.Stick.magnitude);
            if (Time.unscaledTime < _nextSample) return;
            float fps = _frames / Mathf.Max(0.001f, Time.unscaledTime - (_nextSample - 2f));
            _nextSample = Time.unscaledTime + 2f;
            _frames = 0;
            Vector3 look = Rig.Head.forward;
            Prop left = Rig.LeftGrabber.Held, right = Rig.RightGrabber.Held;
            // what is really being drawn, not what was asked for: the first day's logs said "placed" for scenery nobody could see
            World.Objects.Census(out int renderers, out int drawn, out int triangles);
            SessionLog.Write("state", "head", Rig.Head.position, "yaw", Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg,
                "rig", Rig.transform.position, "fps", Mathf.Round(fps), "headset", Rig.XrActive,
                "dark", World.IsDark, "passthrough", QuestFeature.Passthrough,
                "terminal", Terminal.State.ToString(), "collapsed", Terminal.Collapsed, "busy", Conductor.Busy,
                "riding", World.Vehicle != null ? World.Vehicle.Asset : null,
                "stick", System.Math.Round(_stickPeak, 2), "speed", World.Vehicle != null ? System.Math.Round(World.Vehicle.Speed, 1) : 0.0,
                "left", left != null ? left.Def.Id : null, "right", right != null ? right.Def.Id : null,
                "objects", World.Objects.Count, "props", World.Props.All.Count, "alive", Creature.All.Count,
                "renderers", renderers, "drawn", drawn, "triangles", triangles, "rules", World.Rules.Count, "score", World.Rules.Score);
            _stickPeak = 0f;
        }

        void OnApplicationPause(bool paused)
        {
            SessionLog.Write("app", "what", paused ? "paused (headset off or app in the background)" : "resumed");
        }

        // ------------------------------------------------------------------ desktop typing

        void OnGUI()
        {
            if (Rig == null || Rig.XrActive || Quiet) return;
            const string fieldName = "ImaginePrompt";
            float w = Mathf.Min(Screen.width - 40f, 720f), h = 30f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 16f, w, h);

            Event e = Event.current;
            bool submit = false;
            if (Typing && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { submit = true; e.Use(); }
                else if (e.keyCode == KeyCode.Escape) { GUI.FocusControl(null); e.Use(); }
            }

            GUI.SetNextControlName(fieldName);
            GUI.skin.textField.fontSize = 16;
            _typed = GUI.TextField(rect, _typed, 300);
            if (_typed.Length == 0 && !Typing)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                GUI.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width, rect.height), "Press Enter and type what you imagine...");
                GUI.color = Color.white;
            }
            if (_focusField) { GUI.FocusControl(fieldName); _focusField = false; }
            Typing = GUI.GetNameOfFocusedControl() == fieldName;

            if (submit)
            {
                string text = _typed.Trim();
                _typed = "";
                GUI.FocusControl(null);
                Typing = false;
                if (text.Length > 0) Conductor.SubmitText(text);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SessionLog.Write("app", "what", "closed");
            SessionLog.Close();
        }
    }
}
