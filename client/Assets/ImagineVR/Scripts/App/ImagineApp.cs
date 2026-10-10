using System.Collections;
using System.Collections.Generic;
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
        public bool Online { get; private set; }

        /// <summary>Set by tools that drive the app themselves (snapshots): skips the welcome text and server check.</summary>
        public static bool Quiet;
        /// <summary>True while the desktop text box has the keyboard, so letter keys do not also act as controller buttons.</summary>
        public static bool Typing;

        ImagineSettings _settings;
        TMP_FontAsset _font;
        TextAsset[] _examples;
        int _demo;
        string _typed = "";
        bool _focusField;
        bool _foveationSet;

        void Awake()
        {
            Instance = this;
            Typing = false;
            // The headset sets the pace (90 Hz when it agrees to: see QuestFeature); physics keeps up with it.
            Time.fixedDeltaTime = 1f / 90f;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;

            _settings = ImagineSettings.Load();
            _font = Resources.Load<TMP_FontAsset>("Fonts/ImagineMono");
            if (_font == null) _font = TMP_Settings.defaultFontAsset;
            _examples = Resources.LoadAll<TextAsset>("Examples");
            System.Array.Sort(_examples, (a, b) => string.CompareOrdinal(a.name, b.name));

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

            World.Noted += Terminal.Note;
            World.Happened += OnWorldEvent;
        }

        void OnWorldEvent(string text)
        {
            Terminal.Event(text);
            Rig.Haptic(true, 0.7f, 0.12f);
            Rig.Haptic(false, 0.7f, 0.12f);
        }

        IEnumerator Start()
        {
            yield return null;
            Terminal.Recenter(true);
            if (Quiet) yield break;
            Welcome();
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
                Terminal.Raw("  " + Terminal.Tag(Terminal.Text, "hold A") + Terminal.Tag(d, " and speak") + Terminal.Tag(d, "    ") +
                             Terminal.Tag(Terminal.Text, "grip") + Terminal.Tag(d, " to grab") + Terminal.Tag(d, "    ") +
                             Terminal.Tag(Terminal.Text, "X") + Terminal.Tag(d, " recentres"), 52);
            }
            else
            {
                Terminal.Raw("  " + Terminal.Tag(Terminal.Text, "Enter") + Terminal.Tag(d, " to type") + "   " +
                             Terminal.Tag(Terminal.Text, "Space") + Terminal.Tag(d, " to speak") + "   " +
                             Terminal.Tag(Terminal.Text, "right mouse") + Terminal.Tag(d, " to look"), 56);
                Terminal.Raw("  " + Terminal.Tag(Terminal.Text, "F1-F8") + Terminal.Tag(d, " demos") + "   " +
                             Terminal.Tag(Terminal.Text, "E") + Terminal.Tag(d, " grab") + "   " +
                             Terminal.Tag(Terminal.Text, "click") + Terminal.Tag(d, " use") + "   " +
                             Terminal.Tag(Terminal.Text, "F12") + Terminal.Tag(d, " back to dark"), 54);
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
            string library = Library.AssetList.Count + " objects, " + Library.Kits.Count + " kits";
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
                Terminal.Note(Rig.XrActive ? "press B to play a built-in demo" : "press F1 to F8 to play a built-in demo");
                Terminal.SetTitle(null, Terminal.Tag(Terminal.Red, Terminal.Dot) + Terminal.Tag(Terminal.Dim, " offline"));
            }
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (Rig.XrActive && !_foveationSet) _foveationSet = TryFoveation();

            if (Rig.MenuPressed && !Typing) Terminal.Recenter(false);

            // B cycles the built-in demos, unless the right hand is holding a paintbrush (B then changes colour)
            if (Rig.AltPressed && !Typing && _examples.Length > 0)
            {
                Prop held = Rig.RightGrabber != null ? Rig.RightGrabber.Held : null;
                if (held == null || held.Def.Behavior != "paint") PlayDemo(_demo++);
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && !Rig.XrActive && !Typing)
            {
                Key[] keys = { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8 };
                for (int i = 0; i < keys.Length; i++) if (kb[keys[i]].wasPressedThisFrame) PlayDemo(i);
                if (kb.f12Key.wasPressedThisFrame) BackToDark();
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

        public void PlayDemo(int index)
        {
            if (_examples.Length == 0) return;
            TextAsset example = _examples[((index % _examples.Length) + _examples.Length) % _examples.Length];
            string text = example.text;
            string label = example.name.Replace('-', ' ').Replace('_', ' ');
            // an optional first line "# what the user says" becomes the prompt shown in the terminal
            if (text.StartsWith("#"))
            {
                int end = text.IndexOf('\n');
                if (end > 0) { label = text.Substring(1, end - 1).Trim(); text = text.Substring(end + 1); }
            }
            Conductor.SubmitScript(text, label);
        }

        public void BackToDark()
        {
            World.ResetToDark(1.5f);
            Conductor.ResetFirstLight();
            Terminal.Note("back to the dark room");
            if (Online) StartCoroutine(Client.ResetSession());
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
        }
    }
}
