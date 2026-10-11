using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace ImagineVR
{
    public enum TerminalState { Ready, Listening, Thinking, Building, Problem }

    /// <summary>
    /// The floating terminal: a window that looks and behaves like an agentic command-line
    /// assistant. Prompts, tool calls and replies stream in as lines; a status line at the bottom
    /// always shows exactly one state; a tool strip on the left lights up the tool in use.
    ///
    /// Once a world is built the window is in the way, so it can be tucked away: it folds down
    /// to its status line, a small strip low in view that still shows listening, thinking and
    /// building, and unfolds again with the same button.
    /// </summary>
    public sealed class Terminal : MonoBehaviour
    {
        // palette
        public const string Accent = "#FF8A4C", Text = "#E6E8EC", Bright = "#FFFFFF", Dim = "#7C828C", Faint = "#4A4F58";
        public const string Green = "#6BD08B", Red = "#FF6B6B", Blue = "#6CB6FF", Purple = "#C39BFF", Yellow = "#F5D76E", Cyan = "#5FD7E0";

        const float Width = 1.62f, Height = 1.06f, TitleHeight = 0.066f, Margin = 0.05f, ToolStrip = 0.08f;
        const float FontSize = 0.36f;
        const float StripWidth = 0.86f, StripHeight = 0.082f, FoldSeconds = 0.28f, StripDrop = 0.34f;

        sealed class Line
        {
            public string Rich;
            public int Visible;        // characters to show once fully revealed
            public float Shown;        // characters revealed so far
            public float Speed;        // characters per second
            public bool Tick;          // make typing sounds
        }

        readonly List<Line> _lines = new List<Line>();
        readonly List<TextMeshPro> _rows = new List<TextMeshPro>();
        TextMeshPro _title, _titleRight, _status, _hint;
        Material _panel;
        TMP_FontAsset _font;
        Transform _head;
        TerminalToolbar _toolbar;
        int _cols = 70, _rowCount = 17;
        float _rowHeight = 0.044f, _charWidth = 0.018f, _scroll;
        int _revealing;                // index of the line currently being typed
        TerminalState _state = TerminalState.Ready;
        string _verb = "Ready", _hintText = "", _detail = "";
        int _verbStart;
        float _stateSince, _mic, _glow, _lastTick;
        readonly float[] _micHistory = new float[14];
        bool _dirty = true;

        // folding down to the status line
        Transform _panelQuad, _body;
        float _fold, _statusY, _left, _right;
        string _override;
        string _foldKey = "Y";

        /// <summary>True while the terminal is tucked away as a strip.</summary>
        public bool Collapsed { get; private set; }

        /// <summary>Where a hint about folding sits: just under the window's bottom right corner (local space).</summary>
        public Vector3 FoldHintAnchor => new Vector3(Width * 0.5f - 0.36f, -Height * 0.5f - 0.07f, -0.01f);

        // glyphs, chosen at startup from what the font actually contains
        public string Dot = "*", Elbow = "L", Spark = "*", Cross = "x", Star = "*", Ellipsis = "...", Times = "x";
        string[] _spinner = { "|", "/", "-", "\\" };
        string _bars = "";

        // placement
        Vector3 _targetPos;
        Quaternion _targetRot = Quaternion.identity;
        float _offAxisTime;
        bool _placed;

        public TerminalState State => _state;
        public int Columns => _cols;

        public void Build(TMP_FontAsset font, Transform head)
        {
            _font = font;
            _head = head;
            ChooseGlyphs();

            // window
            var panelGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panelGo.name = "Panel";
            Destroy(panelGo.GetComponent<Collider>());
            panelGo.transform.SetParent(transform, false);
            panelGo.transform.localScale = new Vector3(Width, Height, 1f);
            _panelQuad = panelGo.transform;
            _panel = new Material(Shader.Find("ImagineVR/Panel")) { name = "TerminalPanel" };
            _panel.SetVector("_Size", new Vector4(Width, Height, 0f, 0f));
            _panel.SetFloat("_TitleHeight", TitleHeight);
            var pr = panelGo.GetComponent<MeshRenderer>();
            pr.sharedMaterial = _panel;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float left = -Width * 0.5f + Margin + ToolStrip;
            float right = Width * 0.5f - Margin;
            float top = Height * 0.5f;

            _title = MakeText("Title", new Vector3(-Width * 0.5f + Margin, top - TitleHeight * 0.5f, 0f), FontSize * 0.92f, TextAlignmentOptions.MidlineLeft, 0f);
            _titleRight = MakeText("TitleRight", new Vector3(right, top - TitleHeight * 0.5f, 0f), FontSize * 0.8f, TextAlignmentOptions.MidlineRight, 1f);
            _title.text = Tag(Accent, Spark) + " " + Tag(Bright, "imagine") + Tag(Dim, "  ~/worlds/untitled");

            // measure the font so the layout does not depend on guessing its metrics
            TextMeshPro probe = MakeText("Probe", Vector3.zero, FontSize, TextAlignmentOptions.MidlineLeft, 0f);
            Vector2 size = probe.GetPreferredValues("MMMMMMMMMMMMMMMMMMMM");
            _charWidth = Mathf.Max(0.004f, size.x / 20f);
            _rowHeight = Mathf.Max(0.02f, size.y * 1.08f);
            Destroy(probe.gameObject);

            float statusY = -Height * 0.5f + Margin * 0.6f + _rowHeight * 0.5f;
            float bodyTop = top - TitleHeight - Margin * 0.55f;
            float bodyBottom = statusY + _rowHeight * 1.15f;
            _cols = Mathf.Clamp(Mathf.FloorToInt((right - left) / _charWidth), 30, 110);
            _rowCount = Mathf.Clamp(Mathf.FloorToInt((bodyTop - bodyBottom) / _rowHeight), 6, 30);

            for (int i = 0; i < _rowCount; i++)
            {
                TextMeshPro row = MakeText("Row" + i, new Vector3(left, bodyBottom + (i + 0.5f) * _rowHeight, 0f), FontSize, TextAlignmentOptions.MidlineLeft, 0f);
                _rows.Add(row);   // row 0 is the bottom line
            }
            _status = MakeText("Status", new Vector3(left, statusY, 0f), FontSize, TextAlignmentOptions.MidlineLeft, 0f);
            _hint = MakeText("Hint", new Vector3(right, statusY, 0f), FontSize * 0.85f, TextAlignmentOptions.MidlineRight, 1f);

            var bars = new StringBuilder();
            foreach (char c in "▁▂▃▄▅▆▇█") if (Has(c)) bars.Append(c);
            _bars = bars.Length >= 4 ? bars.ToString() : ".:|";

            var strip = new GameObject("Toolbar");
            strip.transform.SetParent(transform, false);
            strip.transform.localPosition = new Vector3(-Width * 0.5f + Margin * 0.5f + ToolStrip * 0.42f, (bodyTop + bodyBottom) * 0.5f, -0.004f);
            _toolbar = strip.AddComponent<TerminalToolbar>();
            _toolbar.Build(bodyTop - bodyBottom);

            // everything but the status line lives under one object, so folding can put it all away at once
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            foreach (TextMeshPro row in _rows) row.transform.SetParent(_body, false);
            _title.transform.SetParent(_body, false);
            _titleRight.transform.SetParent(_body, false);
            strip.transform.SetParent(_body, false);
            _statusY = statusY;
            _left = left;
            _right = right;

            SetState(TerminalState.Ready);
        }

        void ChooseGlyphs()
        {
            Dot = First("●", "•", "*");
            Elbow = First("⎿", "└", "L");
            Spark = First("✻", "◆", "*");
            Cross = First("✗", "×", "x");
            Star = First("★", "✓", "*");
            Ellipsis = First("…", "...");
            Times = First("×", "x");
            // the spinner: the first set of frames the font can draw
            foreach (string frames in new[] { "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏", "◐◓◑◒", "·•●•" })
            {
                bool all = true;
                foreach (char c in frames) all &= Has(c);
                if (!all) continue;
                _spinner = new string[frames.Length];
                for (int i = 0; i < frames.Length; i++) _spinner[i] = frames[i].ToString();
                break;
            }
        }

        /// <summary>The font asset is dynamic and starts empty, so asking means "can you draw this", not "have you already".</summary>
        bool Has(char c) => _font != null && _font.HasCharacter(c, false, true);

        string First(params string[] options)
        {
            foreach (string o in options)
            {
                bool ok = true;
                foreach (char c in o) ok &= Has(c);
                if (ok) return o;
            }
            return options[options.Length - 1];
        }

        TextMeshPro MakeText(string name, Vector3 localPosition, float size, TextAlignmentOptions align, float pivotX)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = _font;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = Color.white;
            tmp.text = "";
            RectTransform rt = tmp.rectTransform;
            rt.pivot = new Vector2(pivotX, 0.5f);
            rt.sizeDelta = new Vector2(Width, 0.05f);
            go.transform.localPosition = localPosition + new Vector3(0f, 0f, -0.003f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return tmp;
        }

        public static string Tag(string color, string text) => "<color=" + color + ">" + text + "</color>";

        /// <summary>Makes user or model text safe to place inside rich text.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace('<', '[').Replace('>', ']').Replace("\n", " ").Replace("\r", "");   // angle brackets would be read as formatting tags
        }

        // ------------------------------------------------------------------ printing

        Line Add(string rich, int visible, float speed, bool tick = false)
        {
            var line = new Line { Rich = rich, Visible = visible, Speed = speed, Tick = tick };
            _lines.Add(line);
            if (_lines.Count > 300)
            {
                _lines.RemoveAt(0);
                _revealing = Mathf.Max(0, _revealing - 1);
            }
            _scroll = -_rowHeight * 0.45f;
            _dirty = true;
            return line;
        }

        /// <summary>Splits text into lines no wider than `width` characters.</summary>
        public static List<string> Wrap(string text, int width)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) { result.Add(""); return result; }
            var current = new StringBuilder();
            foreach (string word in text.Split(' '))
            {
                string w = word;
                while (w.Length > width)
                {
                    if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); }
                    result.Add(w.Substring(0, width));
                    w = w.Substring(width);
                }
                if (current.Length > 0 && current.Length + 1 + w.Length > width)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                if (current.Length > 0) current.Append(' ');
                current.Append(w);
            }
            if (current.Length > 0 || result.Count == 0) result.Add(current.ToString());
            return result;
        }

        /// <summary>Prints text over as many lines as it needs, and returns the first of them.</summary>
        Line AddWrapped(string prefixRich, int prefixLength, string color, string text, float speed, bool tick = false)
        {
            List<string> parts = Wrap(Plain(text), _cols - prefixLength);
            Line first = null;
            for (int i = 0; i < parts.Count; i++)
            {
                string lead = i == 0 ? prefixRich : new string(' ', prefixLength);
                Line line = Add(lead + Tag(color, parts[i]), prefixLength + parts[i].Length, speed, tick);
                if (i == 0) first = line;
            }
            return first;
        }

        public void Blank() { Add("", 0, 1000f); }

        /// <summary>A pre-formatted line of rich text. `length` is its visible character count.</summary>
        public void Raw(string rich, int length, float speed = 400f) { Add(rich, length, speed); }

        /// <summary>The user's words. Returns a handle so a placeholder can be filled in later.</summary>
        public object Prompt(string text)
        {
            Blank();
            // (not found by its place in the list: once the list is full, adding a line drops the oldest)
            return AddWrapped(Tag(Accent, ">") + " ", 2, Bright, text, 120f);
        }

        /// <summary>Replaces the text of a prompt line created earlier (used once speech is transcribed).</summary>
        public void FillPrompt(object handle, string text)
        {
            int index = _lines.IndexOf(handle as Line);
            if (index < 0) { Prompt(text); return; }
            List<string> parts = Wrap(Plain(text), _cols - 2);
            Line line = _lines[index];
            line.Rich = Tag(Accent, ">") + " " + Tag(Bright, parts[0]);
            line.Visible = 2 + parts[0].Length;
            line.Shown = Mathf.Min(line.Shown, 2f);
            line.Speed = 120f;
            for (int i = 1; i < parts.Count; i++)
            {
                _lines.Insert(index + i, new Line { Rich = "  " + Tag(Bright, parts[i]), Visible = 2 + parts[i].Length, Speed = 120f });
            }
            _revealing = Mathf.Min(_revealing, index);
            _dirty = true;
        }

        public static string ColorFor(string op)
        {
            switch (op)
            {
                case "sky": case "light": case "fog": return Blue;
                case "terrain": return Green;
                case "kit": return Purple;
                case "spawn": case "scatter": return Accent;
                case "give": return Yellow;
                case "weather": case "ambience": case "physics": return Cyan;
                case "remove": case "clear": return Red;
                default: return Dim;
            }
        }

        /// <summary>A world statement, shown the way a coding assistant shows a tool call.</summary>
        public void Tool(Statement s)
        {
            string call = Plain(s.ToToolCall(_cols - 3));
            int paren = call.IndexOf('(');
            string name = paren > 0 ? call.Substring(0, paren) : call;
            string args = paren > 0 ? call.Substring(paren) : "";
            Add(Tag(ColorFor(s.Op), Dot) + " " + Tag(Text, "<b>" + name + "</b>") + Tag(Dim, args), 2 + call.Length, 260f);
            if (_toolbar != null) _toolbar.Highlight(s.Op);
        }

        public void Say(string text)
        {
            AddWrapped(Tag(Bright, Dot) + " ", 2, Text, text, 55f, true);
            if (_toolbar != null) _toolbar.Highlight("say");
        }

        /// <summary>The model's plan, said to itself before it starts building: dim, slanted, typed out.</summary>
        public void Think(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            List<string> parts = Wrap(Plain(text), _cols - 2);
            for (int i = 0; i < parts.Count; i++)
                Add((i == 0 ? Tag(Dim, Spark) + " " : "  ") + Tag(Dim, "<i>" + parts[i] + "</i>"), 2 + parts[i].Length, 70f, true);
        }

        public void Note(string text)
        {
            AddWrapped("  " + Tag(Faint, Elbow) + " ", 4, Dim, text, 300f);
        }

        public void Error(string text)
        {
            AddWrapped(Tag(Red, Cross) + " ", 2, Red, text, 200f);
        }

        public void Event(string text)
        {
            AddWrapped(Tag(Yellow, Star) + " ", 2, Yellow, text, 200f);
        }

        public void SetTitle(string world, string right)
        {
            if (world != null) _title.text = Tag(Accent, Spark) + " " + Tag(Bright, "imagine") + Tag(Dim, "  ~/worlds/" + Plain(world));
            if (right != null) _titleRight.text = right;
        }

        // ------------------------------------------------------------------ state

        static readonly string[] ThinkingVerbs = { "Imagining", "Dreaming", "Sketching", "Composing", "Envisioning", "Musing", "Conjuring" };
        static readonly string[] BuildingVerbs = { "Painting", "Sculpting", "Building", "Planting", "Raising", "Arranging", "Shaping" };

        public void SetState(TerminalState state, string detail = null)
        {
            if (state != _state || detail != null) _stateSince = Time.time;
            _state = state;
            _detail = detail ?? "";
            _verbStart = Random.Range(0, 7);
            switch (state)
            {
                case TerminalState.Thinking: _verb = ThinkingVerbs[Random.Range(0, ThinkingVerbs.Length)]; break;
                case TerminalState.Building: _verb = BuildingVerbs[Random.Range(0, BuildingVerbs.Length)]; break;
                case TerminalState.Listening: _verb = "Listening"; break;
                case TerminalState.Problem: _verb = "Problem"; break;
                default: _verb = "Ready"; break;
            }
        }

        public void SetHint(string hint) { _hintText = hint ?? ""; }

        /// <summary>The name of the button that folds the terminal, as the user knows it ("Y", "C").</summary>
        public void SetFoldKey(string key) { _foldKey = key; }

        /// <summary>Takes over the status line for a moment (a countdown, say). Null gives it back.</summary>
        public void SetOverride(string rich) { _override = rich; }

        public void SetCollapsed(bool collapsed)
        {
            if (collapsed == Collapsed) return;
            Collapsed = collapsed;
            if (_placed) Recenter(false);   // the strip sits higher than the window's own status line did
            Sfx.Play("whoosh", transform.position, 0.25f, collapsed ? 1.5f : 1.2f);
            SessionLog.Write("terminal", "collapsed", collapsed);
        }

        public void Toggle() { SetCollapsed(!Collapsed); }

        /// <summary>True while something printed is still being typed out.</summary>
        public bool Printing => _revealing < _lines.Count;

        /// <summary>Wipes everything printed so far, as when the app has just started.</summary>
        public void Clear()
        {
            _lines.Clear();
            _revealing = 0;
            _scroll = 0f;
            _override = null;
            _dirty = true;
            foreach (TextMeshPro row in _rows) row.text = "";
        }

        /// <summary>Microphone level 0..1 while listening.</summary>
        public void SetMic(float level) { _mic = Mathf.Clamp01(level); }

        void UpdateStatus()
        {
            float elapsed = Time.time - _stateSince;
            string spin = _spinner[(int)(Time.time * 12f) % _spinner.Length];
            string text;
            switch (_state)
            {
                case TerminalState.Listening:
                {
                    var bars = new StringBuilder();
                    for (int i = 0; i < _micHistory.Length; i++)
                    {
                        int k = Mathf.Clamp(Mathf.RoundToInt(_micHistory[i] * (_bars.Length - 1)), 0, _bars.Length - 1);
                        bars.Append(_bars[k]);
                    }
                    text = Tag(Red, Dot) + " " + Tag(Bright, "Listening") + "  " + Tag(Accent, bars.ToString());
                    break;
                }
                case TerminalState.Thinking:
                case TerminalState.Building:
                {
                    string[] verbs = _state == TerminalState.Thinking ? ThinkingVerbs : BuildingVerbs;
                    _verb = verbs[(_verbStart + Mathf.FloorToInt(elapsed / 3.5f)) % verbs.Length];
                    text = Tag(Accent, spin) + " " + Tag(Accent, _verb + Ellipsis) + Tag(Dim, " (" + Mathf.FloorToInt(elapsed) + "s)");
                    break;
                }
                case TerminalState.Problem:
                    text = Tag(Red, Cross) + " " + Tag(Red, _detail.Length > 0 ? Plain(_detail) : "Something went wrong");
                    break;
                default:
                    text = Tag(Green, Dot) + " " + Tag(Dim, "Ready");
                    break;
            }
            _status.text = _override ?? text;
            // the right of the status line always says how to fold or unfold, in few words
            if (_override != null) _hint.text = "";
            else if (Collapsed) _hint.text = _state == TerminalState.Listening ? "" : Tag(Dim, _foldKey) + Tag(Faint, " opens");
            else _hint.text = Tag(Faint, _hintText + (_hintText.Length > 0 ? "  ·  " : "") + _foldKey + " tucks away");
        }

        /// <summary>Folds the window down around its status line, or opens it out again.</summary>
        void Fold(float dt)
        {
            float target = Collapsed ? 1f : 0f;
            if (_fold == target && _body.gameObject.activeSelf != Collapsed) return;
            _fold = Mathf.MoveTowards(_fold, target, dt / FoldSeconds);
            float e = Ease.InOutCubic(_fold);
            float w = Mathf.Lerp(Width, StripWidth, e), h = Mathf.Lerp(Height, StripHeight, e);
            _panelQuad.localScale = new Vector3(w, h, 1f);
            _panelQuad.localPosition = new Vector3(0f, Mathf.Lerp(0f, _statusY, e), 0f);
            _panel.SetVector("_Size", new Vector4(w, h, 0f, 0f));
            _panel.SetFloat("_TitleHeight", TitleHeight * (1f - e));
            // the lines go first when folding and come back last when opening, so they never spill out of the window
            bool body = _fold < 0.05f;
            if (_body.gameObject.activeSelf != body) { _body.gameObject.SetActive(body); _dirty = true; }
            Vector3 p = _status.transform.localPosition;
            _status.transform.localPosition = new Vector3(Mathf.Lerp(_left, -StripWidth * 0.5f + 0.035f, e), p.y, p.z);
            p = _hint.transform.localPosition;
            _hint.transform.localPosition = new Vector3(Mathf.Lerp(_right, StripWidth * 0.5f - 0.035f, e), p.y, p.z);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            float dt = Time.deltaTime;

            // type out lines in order; hurry when there is a backlog
            int backlog = _lines.Count - _revealing;
            float hurry = backlog > 4 ? 4f : backlog > 2 ? 2f : 1f;
            float budget = dt;
            while (_revealing < _lines.Count && budget > 0f)
            {
                Line line = _lines[_revealing];
                float need = line.Visible - line.Shown;
                float can = line.Speed * hurry * budget;
                if (can >= need)
                {
                    line.Shown = line.Visible;
                    budget -= need / Mathf.Max(line.Speed * hurry, 1f);
                    _revealing++;
                }
                else
                {
                    line.Shown += can;
                    budget = 0f;
                    if (line.Tick && Time.time - _lastTick > 0.055f)
                    {
                        _lastTick = Time.time;
                        Sfx.Play("tick", transform.position, 0.25f, 0.9f + Random.value * 0.3f);
                    }
                }
                _dirty = true;
            }

            _scroll = Mathf.MoveTowards(_scroll, 0f, dt * _rowHeight * 6f);
            if (_dirty || _scroll != 0f) Layout();

            // microphone history scrolls left
            if (_state == TerminalState.Listening && Time.frameCount % 3 == 0)
            {
                for (int i = 0; i < _micHistory.Length - 1; i++) _micHistory[i] = _micHistory[i + 1];
                _micHistory[_micHistory.Length - 1] = _mic;
            }
            UpdateStatus();
            Fold(dt);

            float glowTarget = _state == TerminalState.Thinking || _state == TerminalState.Building ? 0.55f + 0.25f * Mathf.Sin(Time.time * 3f)
                : _state == TerminalState.Listening ? 0.35f + _mic * 0.6f : 0f;
            _glow = Mathf.MoveTowards(_glow, glowTarget, dt * 2.5f);
            _panel.SetFloat("_Glow", _glow);
            _panel.SetColor("_GlowColor", _state == TerminalState.Listening ? new Color(1f, 0.35f, 0.35f) : new Color(1f, 0.55f, 0.3f));

            Follow(dt);
        }

        void Layout()
        {
            _dirty = false;
            // show everything revealed so far plus the line being typed
            int last = Mathf.Min(_revealing, _lines.Count - 1);
            for (int i = 0; i < _rows.Count; i++)
            {
                TextMeshPro row = _rows[i];
                int index = last - i;
                if (index < 0 || index >= _lines.Count)
                {
                    if (row.text.Length > 0) row.text = "";
                    continue;
                }
                Line line = _lines[index];
                if (row.text != line.Rich) row.text = line.Rich;
                row.maxVisibleCharacters = Mathf.FloorToInt(line.Shown);
                float fade = i >= _rows.Count - 2 ? 0.45f : 1f;   // oldest visible lines dim before they leave
                row.alpha = fade;
                Vector3 p = row.transform.localPosition;
                float baseY = RowY(i);
                row.transform.localPosition = new Vector3(p.x, baseY + _scroll, p.z);
            }
        }

        float RowY(int i)
        {
            float statusY = -Height * 0.5f + Margin * 0.6f + _rowHeight * 0.5f;
            float bodyBottom = statusY + _rowHeight * 1.15f;
            return bodyBottom + (i + 0.5f) * _rowHeight;
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Puts the terminal directly in front of the user.</summary>
        public void Recenter(bool instant)
        {
            if (_head == null) return;
            Vector3 forward = _head.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            // Open, the window is centred just below eye level. Folded, the strip is all there is, and it
            // sits a little below the line of sight: low enough to be out of the way, high enough to
            // clear the bonnet of a car or the neck of a horse.
            float height = Collapsed ? _head.position.y - StripDrop - _statusY : Mathf.Max(_head.position.y - 0.12f, 0.9f);
            _targetPos = new Vector3(_head.position.x, height, _head.position.z) + forward * 1.55f;
            _targetRot = Quaternion.LookRotation(forward);
            _placed = true;
            _offAxisTime = 0f;
            if (instant) transform.SetPositionAndRotation(_targetPos, _targetRot);
        }

        /// <summary>Moves the terminal with the user when they are carried along, so it stays where they left it relative to themselves.</summary>
        public void Carry(Vector3 delta, float yaw, Vector3 pivot)
        {
            if (!_placed) return;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            transform.position = pivot + turn * (transform.position + delta - pivot);
            transform.rotation = turn * transform.rotation;
            _targetPos = pivot + turn * (_targetPos + delta - pivot);
            _targetRot = turn * _targetRot;
        }

        void Follow(float dt)
        {
            if (_head == null) return;
            if (!_placed) Recenter(true);
            Vector3 to = transform.position - _head.position;
            Vector3 flatTo = new Vector3(to.x, 0f, to.z);
            Vector3 flatForward = new Vector3(_head.forward.x, 0f, _head.forward.z);
            float angle = flatTo.sqrMagnitude > 0.001f && flatForward.sqrMagnitude > 0.001f ? Vector3.Angle(flatForward, flatTo) : 0f;
            bool away = angle > 60f || flatTo.magnitude > 2.4f || flatTo.magnitude < 0.7f;
            _offAxisTime = away ? _offAxisTime + dt : 0f;
            if (_offAxisTime > 1.3f) Recenter(false);
            transform.position = Vector3.Lerp(transform.position, _targetPos, 1f - Mathf.Exp(-dt * 4f));
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRot, 1f - Mathf.Exp(-dt * 4f));
        }
    }

    /// <summary>
    /// A strip of creative-tool icons down the left of the terminal. The icon for the tool the
    /// assistant is using lights up. A quiet nod to the tool palettes of creative software.
    /// </summary>
    public sealed class TerminalToolbar : MonoBehaviour
    {
        static readonly string[] Tools = { "say", "sky", "terrain", "kit", "spawn", "scatter", "give", "weather" };
        readonly List<Renderer> _icons = new List<Renderer>();
        readonly List<float> _heat = new List<float>();
        MaterialPropertyBlock _props;
        static readonly Color Idle = new Color(0.32f, 0.34f, 0.38f), Hot = new Color(1f, 0.54f, 0.3f);

        public void Build(float height)
        {
            _props = new MaterialPropertyBlock();
            var material = new Material(Shader.Find("ImagineVR/Unlit")) { name = "ToolIcon" };
            float step = Mathf.Min(0.072f, height / Tools.Length);
            float top = step * (Tools.Length - 1) * 0.5f;
            for (int i = 0; i < Tools.Length; i++)
            {
                var go = new GameObject("Tool_" + Tools[i]);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, top - i * step, 0f);
                var mb = new MeshBuilder();
                mb.Col(Color.white);
                Icon(mb, Tools[i]);
                go.AddComponent<MeshFilter>().sharedMesh = mb.Build("Icon_" + Tools[i]);
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _icons.Add(mr);
                _heat.Add(0f);
            }
        }

        /// <summary>Flat icons about 4 cm across, drawn in the XY plane facing the user (-Z).</summary>
        static void Icon(MeshBuilder mb, string tool)
        {
            const float s = 0.02f;
            switch (tool)
            {
                case "say":   // paintbrush
                    mb.Push(Vector3.zero, new Vector3(0f, 0f, -45f));
                    mb.Box(new Vector3(0f, 0.35f * s, 0f), new Vector3(0.22f * s, 1.1f * s, 0.1f * s));
                    mb.Box(new Vector3(0f, -0.3f * s, 0f), new Vector3(0.32f * s, 0.25f * s, 0.1f * s));
                    mb.Tri(new Vector3(-0.18f * s, -0.42f * s, 0f), new Vector3(0.18f * s, -0.42f * s, 0f), new Vector3(0f, -1.0f * s, 0f), Vector3.back);
                    mb.Pop();
                    break;
                case "sky":   // sun over a horizon
                    mb.Push(new Vector3(0f, 0.15f * s, 0f), new Vector3(-90f, 0f, 0f));
                    mb.Disc(Vector3.zero, 0.5f * s, 12, true);
                    mb.Pop();
                    mb.Box(new Vector3(0f, -0.7f * s, 0f), new Vector3(1.8f * s, 0.16f * s, 0.05f * s));
                    break;
                case "terrain":   // two peaks
                    mb.TriTwoSided(new Vector3(-1f * s, -0.7f * s, 0f), new Vector3(-0.2f * s, 0.8f * s, 0f), new Vector3(0.5f * s, -0.7f * s, 0f));
                    mb.TriTwoSided(new Vector3(0f, -0.7f * s, 0f), new Vector3(0.55f * s, 0.25f * s, 0f), new Vector3(1f * s, -0.7f * s, 0f));
                    break;
                case "kit":   // four blocks
                    for (int x = 0; x < 2; x++)
                        for (int y = 0; y < 2; y++)
                            mb.Box(new Vector3((x - 0.5f) * 0.9f * s, (y - 0.5f) * 0.9f * s, 0f), new Vector3(0.7f * s, 0.7f * s, 0.05f * s));
                    break;
                case "spawn":   // one diamond
                    mb.Push(Vector3.zero, new Vector3(0f, 0f, 45f));
                    mb.Box(Vector3.zero, new Vector3(1.1f * s, 1.1f * s, 0.05f * s));
                    mb.Pop();
                    break;
                case "scatter":   // spray of dots
                {
                    Vector2[] dots = { new Vector2(-0.7f, 0.5f), new Vector2(0.1f, 0.7f), new Vector2(0.7f, 0.2f), new Vector2(-0.3f, -0.1f), new Vector2(0.4f, -0.6f), new Vector2(-0.7f, -0.7f) };
                    foreach (Vector2 d in dots) mb.Box(new Vector3(d.x * s, d.y * s, 0f), new Vector3(0.3f * s, 0.3f * s, 0.05f * s));
                    break;
                }
                case "give":   // open ring: a hand's grip
                    mb.Push(Vector3.zero, new Vector3(90f, 0f, 0f));
                    mb.Torus(Vector3.zero, 0.65f * s, 0.14f * s, 14, 4);
                    mb.Pop();
                    break;
                default:   // weather: a drop
                    mb.TriTwoSided(new Vector3(-0.5f * s, -0.2f * s, 0f), new Vector3(0f, 0.95f * s, 0f), new Vector3(0.5f * s, -0.2f * s, 0f));
                    mb.Push(new Vector3(0f, -0.3f * s, 0f), new Vector3(-90f, 0f, 0f));
                    mb.Disc(Vector3.zero, 0.52f * s, 12, true);
                    mb.Pop();
                    break;
            }
        }

        public void Highlight(string op)
        {
            string tool = op;
            if (op == "light" || op == "fog") tool = "sky";
            else if (op == "ambience" || op == "physics") tool = "weather";
            else if (op == "modify" || op == "remove" || op == "clear" || op == "ride" || op == "goal" || op == "player") tool = "spawn";
            for (int i = 0; i < Tools.Length; i++) if (Tools[i] == tool) _heat[i] = 1f;
        }

        void Update()
        {
            for (int i = 0; i < _icons.Count; i++)
            {
                _heat[i] = Mathf.MoveTowards(_heat[i], 0f, Time.deltaTime * 0.9f);
                float h = Ease.Smooth(_heat[i]);
                Color c = Color.Lerp(Idle, Hot, h);
                _props.SetColor("_Color", c);
                _icons[i].SetPropertyBlock(_props);
                _icons[i].transform.localScale = Vector3.one * (1f + 0.35f * h);
            }
        }
    }
}
