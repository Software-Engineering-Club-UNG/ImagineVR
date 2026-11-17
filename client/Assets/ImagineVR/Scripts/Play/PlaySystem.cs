using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImagineVR
{
    /// <summary>
    /// What makes a place a game: counters (`var`), the line that says what to do, stages,
    /// places that trigger (`zone`), the user's own health, and what has fallen over or come to rest.
    /// None of it knows any game by name. Bowling is a slippery lane, heavy pins, a zone behind
    /// them and a rule about what is still standing; this class only keeps the numbers.
    ///
    /// It is also what expressions ask (`IExprHost`), and what the headset tells the server of
    /// the game each turn (`Header`, sent as X-Play-State).
    /// </summary>
    public sealed class PlaySystem : IExprHost
    {
        sealed class VarDef { public string Label, As; public double Rate, Min = double.MinValue, Max = double.MaxValue; }

        sealed class StageDef
        {
            public string Id, Goal, Next, Fail;
            public float Timeout;
            public Pose Frame;
            public List<Dictionary<string, object>> Enter;
        }

        sealed class Track { public bool Tipped, Moved; public float Still; }

        readonly World _world;
        readonly Dictionary<string, object> _vars = new Dictionary<string, object>();
        readonly Dictionary<string, VarDef> _defs = new Dictionary<string, VarDef>();
        readonly List<string> _order = new List<string>();
        readonly Dictionary<string, StageDef> _stages = new Dictionary<string, StageDef>();
        readonly Dictionary<string, Statement> _sources = new Dictionary<string, Statement>();
        readonly Dictionary<Prop, Track> _tracks = new Dictionary<Prop, Track>();
        readonly List<(float at, string target)> _resets = new List<(float, string)>();
        readonly HashSet<string> _hittable = new HashSet<string>();
        readonly Zones _zones;
        float _stageStarted, _deadUntil = -1f, _slowUntil = -1f, _baseFixed = 1f / 90f;
        int _frame;

        /// <summary>The headset's own view of the game, drawn in front of the user. Null in tests and without a rig.</summary>
        public Hud Hud;
        /// <summary>Asks the director what happens next in a story: the text of a `direct` note. Wired to the server by the app.</summary>
        public event Action<string> Directed;
        /// <summary>A character (`id`, null for the narrator) says a line: (id, text, voice). Wired to speech by the app.</summary>
        public event Action<string, string, string> Spoke;

        public string Stage { get; private set; }
        /// <summary>The dashboard of what they drive or fly, set by the Vehicle a few times a second. Empty on foot.</summary>
        public string Gauge
        {
            get => _gauge;
            set { if (value != _gauge) { _gauge = value ?? ""; if (Hud != null) Hud.Changed(); } }
        }
        string _gauge = "";
        public string Objective { get; private set; } = "";
        public float PlayerHealth { get; private set; }
        public float PlayerMax { get; private set; }
        public Vector3 Checkpoint = Vector3.zero;
        public bool Active => _order.Count > 0 || Stage != null || PlayerMax > 0f || Objective.Length > 0 || _zones.Count > 0;

        public PlaySystem(World world)
        {
            _world = world;
            _zones = new Zones(world, this);
        }

        // ------------------------------------------------------------------ counters

        public object Var(string name)
        {
            switch (name)
            {
                case "time": return (double)(Time.time - _stageStarted);
                case "stage": return Stage ?? "";
                case "health": return (double)PlayerHealth;
            }
            return _vars.TryGetValue(name, out object v) ? v : 0.0;
        }

        public double Number(string name)
        {
            object v = Var(name);
            return v is double d ? d : 0.0;
        }

        public bool Labelled(string name) => _defs.TryGetValue(name, out VarDef d) && !string.IsNullOrEmpty(d.Label);

        public void Set(string name, object value)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (value is float f) value = (double)f;
            else if (value is int i) value = (double)i;
            if (value is double d && _defs.TryGetValue(name, out VarDef def)) value = Math.Min(Math.Max(d, def.Min), def.Max);
            if (!_vars.ContainsKey(name)) _order.Add(name);
            object was = _vars.TryGetValue(name, out object old) ? old : null;
            _vars[name] = value;
            if (!Equals(was, value)) { _world.Rules.Changed(); if (Hud != null) Hud.Changed(); }
        }

        public void Add(string name, double by) => Set(name, Number(name) + by);

        void DefineVar(Statement s)
        {
            string name = s.Str("name");
            if (string.IsNullOrEmpty(name)) return;
            var def = new VarDef
            {
                Label = s.Str("label"), As = s.Str("as", "number"), Rate = s.Num("rate", 0f),
                Min = s.Has("min") ? s.Num("min") : double.MinValue, Max = s.Has("max") && s.Str("as") != "bar" ? s.Num("max") : double.MaxValue,
            };
            if (s.Has("max") && def.As == "bar") def.Max = s.Num("max");
            _defs[name] = def;
            if (s.Has("text")) Set(name, s.Str("text"));
            else Set(name, (double)s.Num("value", 0f));
            if (def.As == "bar" && !s.Has("max")) def.Max = Math.Max(1.0, Number(name));
        }

        /// <summary>The counters to show, in the order they were made, with their labels and values.</summary>
        public List<(string name, string label, string value, float fill)> Shown()
        {
            var list = new List<(string, string, string, float)>();
            foreach (string name in _order)
            {
                if (!_defs.TryGetValue(name, out VarDef def) || string.IsNullOrEmpty(def.Label)) continue;
                object v = Var(name);
                string text;
                float fill = -1f;
                if (v is string s) text = s;
                else
                {
                    double d = (double)v;
                    if (def.As == "time") text = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(d))).ToString(d >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
                    else text = Math.Abs(d - Math.Round(d)) < 0.01 ? ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture) : d.ToString("0.0", CultureInfo.InvariantCulture);
                    if (def.As == "bar" && def.Max > 0 && def.Max < double.MaxValue) fill = Mathf.Clamp01((float)(d / def.Max));
                    else if (def.Max < double.MaxValue && def.Max > 0 && def.As == "number") text += "/" + ((long)def.Max).ToString(CultureInfo.InvariantCulture);
                }
                list.Add((name, def.Label, text, fill));
            }
            return list;
        }

        /// <summary>What the headset tells the server of the game: "score 3; frame 2; stage wave_2; health 80".</summary>
        public string Header()
        {
            if (!Active) return "";
            var parts = new List<string>();
            foreach (string name in _order)
            {
                object v = _vars[name];
                parts.Add(name + " " + (v is double d ? (Math.Abs(d - Math.Round(d)) < 0.01 ? ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture) : d.ToString("0.#", CultureInfo.InvariantCulture)) : v.ToString()));
                if (parts.Count >= 16) break;
            }
            if (Stage != null) parts.Add("stage " + Stage);
            if (PlayerMax > 0f) parts.Add("health " + Mathf.RoundToInt(PlayerHealth) + "/" + Mathf.RoundToInt(PlayerMax));
            if (Objective.Length > 0) parts.Add("objective " + Objective);
            return string.Join("; ", parts);
        }

        // ------------------------------------------------------------------ statements

        /// <summary>Applies a statement of the third version that belongs to the game rather than to the scenery.</summary>
        public void Apply(Statement s)
        {
            switch (s.Op)
            {
                case "var": DefineVar(s); break;
                case "hud": ApplyHud(s); break;
                case "stage": DefineStage(s); break;
                case "zone": _zones.Define(s); break;
                case "story": if (s.Has("title")) Banner(s.Str("title"), 4f, null); break;
                case "speak": Speak(s.Str("id"), s.Str("text", ""), s.Str("voice"), s.Str("audio")); break;
                case "narrate": Speak(null, s.Str("text", ""), s.Str("voice", "narrator"), s.Str("audio")); break;
            }
        }

        void ApplyHud(Statement s)
        {
            if (s.Flag("hide")) { SetObjective(""); if (Hud != null) Hud.ShowOnly(null); }
            if (s.Has("objective")) SetObjective(s.Str("objective"));
            if (s.Has("banner")) Banner(Expr.Fill(s.Str("banner"), this), s.Num("seconds", 2.5f), s.TryColor("color", out Color c) ? c : (Color?)null);
            if (s.Has("show") && Hud != null) Hud.ShowOnly(s.StrList("show"));
            Dictionary<string, object> board = s.Obj("board");
            if (board != null && Hud != null)
            {
                Statement b = Statement.Wrap(board);
                Vector3 at = Statement.InWorld(s.Frame(), b.Vec("at", new Vector3(0f, 2.5f, 8f)));
                at.y += _world.SurfaceHeight(at.x, at.z);
                Quaternion facing = s.Frame().rotation * Quaternion.Euler(0f, b.Num("rot_y", 0f), 0f);
                Hud.Board(b.Str("id", "board"), at, facing, b.Num("size", 1.5f), b.Str("title", ""), b.StrList("show"));
            }
        }

        public void SetObjective(string text)
        {
            Objective = text ?? "";
            if (Hud != null) Hud.Changed();
            if (Objective.Length > 0) SessionLog.Write("objective", "text", Objective);
        }

        public void Banner(string text, float seconds, Color? color)
        {
            if (string.IsNullOrEmpty(text)) return;
            SessionLog.Write("banner", "text", text);
            if (Hud != null) Hud.Banner(text, seconds, color);
            else _world.Emit(text);
        }

        public void Finish(bool won, string text, string sound)
        {
            Banner(text, 5f, won ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.3f));
            Vector3 at = _world.Head != null ? _world.Head.position + _world.Head.forward * 2f : Vector3.zero;
            if (!string.IsNullOrEmpty(sound)) Sfx.Play(sound, at, 0.9f);
            if (won) { Fx.Confetti(at + Vector3.up, 140, 3f, 0.07f); Shake(0.4f); }
            Set(won ? "wins" : "losses", Number(won ? "wins" : "losses") + 1);
        }

        // ------------------------------------------------------------------ stages

        void DefineStage(Statement s)
        {
            if (s.Has("go") && !s.Has("id")) { GoStage(s.Str("go")); return; }
            string id = s.Str("id");
            if (string.IsNullOrEmpty(id)) return;
            _stages[id] = new StageDef
            {
                Id = id, Goal = s.Str("goal"), Next = s.Str("next"), Fail = s.Str("fail"), Timeout = s.Num("timeout", 0f),
                Enter = s.ObjList("enter"), Frame = s.Frame(),
            };
            if (s.Flag("first") || Stage == null) GoStage(id);
        }

        public void GoStage(string go)
        {
            if (string.IsNullOrEmpty(go)) return;
            if (go == "next")
            {
                if (Stage == null || !_stages.TryGetValue(Stage, out StageDef current) || string.IsNullOrEmpty(current.Next)) return;
                go = current.Next;
            }
            if (!_stages.TryGetValue(go, out StageDef stage)) { SessionLog.Write("stage_missing", "id", go); return; }
            Stage = stage.Id;
            _stageStarted = Time.time;
            SessionLog.Write("stage", "id", stage.Id);
            if (!string.IsNullOrEmpty(stage.Goal)) { SetObjective(stage.Goal); Banner(stage.Goal, 3f, null); }
            if (stage.Enter != null && stage.Enter.Count > 0) _world.Rules.RunActions(stage.Enter, stage.Frame);
            _world.Rules.Fire("stage", new Rules.Subject { Asset = stage.Id, Group = stage.Id, Position = _world.Head != null ? _world.Head.position : Vector3.zero });
            _world.Rules.Changed();
            if (Hud != null) Hud.Changed();
        }

        // ------------------------------------------------------------------ the user

        public void SetPlayer(Statement s)
        {
            if (s.Has("health"))
            {
                PlayerMax = Mathf.Max(0f, s.Num("health"));
                PlayerHealth = PlayerMax;
                if (Hud != null) Hud.Changed();
            }
            if (s.TryVec3("at", out Vector3 local))
            {
                Vector3 at = Statement.InWorld(s.Frame(), local);
                at.y += _world.SurfaceHeight(at.x, at.z);
                MovePlayer(at, s.Has("face") ? s.Num("face") + s.Frame().rotation.eulerAngles.y : float.NaN);
            }
        }

        public void HurtPlayer(float amount, Vector3 from)
        {
            if (PlayerMax <= 0f || amount == 0f || _deadUntil > 0f) return;
            PlayerHealth = Mathf.Clamp(PlayerHealth - amount, 0f, PlayerMax);
            if (Hud != null) { Hud.Changed(); if (amount > 0f) Hud.Hurt(from); }
            ImagineApp app = ImagineApp.Instance;
            if (amount > 0f && app != null && app.Rig != null) { app.Rig.Haptic(true, 0.8f, 0.12f); app.Rig.Haptic(false, 0.8f, 0.12f); }
            if (amount > 0f) { Sfx.Play("hurt", _world.Head != null ? _world.Head.position : from, 0.7f); _world.Rules.Fire("hurt", _world.Rules.OfPlayer()); }
            _world.Rules.Changed();
            if (PlayerHealth > 0f) return;
            // down: a rule may say what happens; if none brings them back, they wake at the last checkpoint
            _deadUntil = Time.time + 2.5f;
            Fade(2.4f, new Color(0.35f, 0f, 0f));
            _world.Rules.Fire("die", _world.Rules.OfPlayer());
        }

        public void MovePlayer(Vector3 feet, float yaw)
        {
            if (_world.Vehicle == null) return;
            Transform rig = _world.Vehicle.transform;
            _world.Vehicle.PlaceAt(feet, float.IsNaN(yaw) ? rig.eulerAngles.y : yaw);
        }

        /// <summary>Takes back what was given: an asset, or `all` for everything in the hands and floating in reach.</summary>
        public void Take(string asset)
        {
            ImagineApp app = ImagineApp.Instance;
            if (app != null && app.Rig != null)
                foreach (Grabber g in new[] { app.Rig.LeftGrabber, app.Rig.RightGrabber })
                    if (g != null && g.Held != null && (asset == "all" || Rules.Names(asset, Rules.Of(g.Held)))) g.ForceRelease();
            foreach (Prop p in new List<Prop>(_world.Props.All))
                if (p != null && p.Given && (asset == "all" || Rules.Names(asset, Rules.Of(p)))) _world.Props.Destroy(p);
        }

        // ------------------------------------------------------------------ moments

        public void Fade(float seconds, Color color) { if (Hud != null) Hud.Fade(seconds, color); }

        public void Shake(float amount)
        {
            // never a moving camera in a headset: it is felt in both hands instead
            ImagineApp app = ImagineApp.Instance;
            if (app == null || app.Rig == null) return;
            app.Rig.Haptic(true, Mathf.Clamp01(amount), 0.25f);
            app.Rig.Haptic(false, Mathf.Clamp01(amount), 0.25f);
        }

        public void SlowMotion(float scale, float seconds)
        {
            if (_slowUntil < 0f) _baseFixed = Time.fixedDeltaTime;
            Time.timeScale = Mathf.Clamp(scale, 0.1f, 1f);
            Time.fixedDeltaTime = _baseFixed * Time.timeScale;
            _slowUntil = Time.unscaledTime + Mathf.Clamp(seconds, 0.2f, 10f);
        }

        public void Speak(string id, string text, string voice, string audio = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            SessionLog.Write("speak", "id", id, "text", text);
            if (Hud != null) Hud.Speech(id, text, Speaker(id));
            Spoke?.Invoke(id, text, string.IsNullOrEmpty(audio) ? voice : "url:" + audio);
        }

        /// <summary>Who is speaking, for a bubble over them and their voice to come from. Null for the narrator, or someone not there.</summary>
        public Transform Speaker(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Rules.Subject s = _world.Rules.Nearest(id, out bool found);
            return found && s.Object != null ? s.Object.transform : null;
        }

        public void Direct(string note)
        {
            if (string.IsNullOrEmpty(note)) return;
            SessionLog.Write("direct", "note", note);
            Directed?.Invoke(note);
        }

        // ------------------------------------------------------------------ things in the world

        /// <summary>
        /// What a spawn or scatter of the third version says about the things it made, laid on them
        /// once they exist: health, team, how they bounce and slide, a label, a light. Also kept, so
        /// `reset` can stand them back where they began.
        /// </summary>
        public void AfterSpawn(Statement s)
        {
            string id = s.Str("id");
            if (string.IsNullOrEmpty(id) || id.StartsWith(World.Cover, StringComparison.Ordinal)) return;
            if (!id.StartsWith("rule_spawn", StringComparison.Ordinal)) _sources[id] = s;
            ThingSettings.Apply(_world, s, id, _hittable.Contains(id) || HittableByRule(id));
        }

        bool HittableByRule(string id)
        {
            var probe = new Rules.Subject { Group = id };
            foreach (string target in _hittable) if (Rules.Names(target, probe)) return true;
            return false;
        }

        /// <summary>Plain scenery a rule is about can be hit, pressed and counted: it is given a body that says what it is.</summary>
        public void MakeHittable(string target)
        {
            if (string.IsNullOrEmpty(target) || target == "any" || target == "player") return;
            _hittable.Add(target);
            foreach (Group g in _world.Objects.Groups)
                if (Rules.Names(target, new Rules.Subject { Group = g.Id, Asset = g.Asset })) ThingSettings.Hittables(_world, g);
        }

        /// <summary>Stands a group (or every group an id is the root of) back where it began, remaking whatever was lost.</summary>
        public void Reset(string target, float delay)
        {
            if (string.IsNullOrEmpty(target)) return;
            if (delay > 0.01f) { _resets.Add((Time.time + delay, target)); return; }
            var ids = new List<string>();
            var probe = new Rules.Subject();
            foreach (string id in _sources.Keys) { probe.Group = id; probe.Asset = _sources[id].Str("asset"); if (id == target || Rules.Names(target, probe) && id.StartsWith(target, StringComparison.Ordinal)) ids.Add(id); }
            foreach (string id in ids)
            {
                Statement source = _sources[id];
                _world.Props.Remove(id);
                _world.Objects.Remove(id, 0.15f);
                _world.Apply(Statement.Wrap(new Dictionary<string, object>(source.Fields)).WithOp(source.Op));
            }
            if (ids.Count > 0) SessionLog.Write("reset", "target", target, "groups", ids.Count);
        }

        public bool ZoneCentre(string id, out Vector3 at) => _zones.Centre(id, out at);

        /// <summary>`remove` of a zone, a stage, or a counter.</summary>
        public void Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _zones.Remove(id);
            _stages.Remove(id);
            _sources.Remove(id);
            if (_vars.Remove(id)) { _defs.Remove(id); _order.Remove(id); if (Hud != null) Hud.Changed(); }
        }

        // ------------------------------------------------------------------ the world, as expressions ask it

        public double World(string function, List<string> args)
        {
            string a = args.Count > 0 ? args[0] : null;
            switch (function)
            {
                case "count": return CountOf(a, false, false, false);
                case "alive": return CountOf(a, true, false, false);
                case "standing": return CountOf(a, false, true, false);
                case "tipped": return CountOf(a, false, false, true);
                case "moving": return Moving(a);
                case "dist":
                {
                    if (!PositionOf(a, out Vector3 p) || !PositionOf(args.Count > 1 ? args[1] : "player", out Vector3 q)) return 9999;
                    return Vector3.Distance(p, q);
                }
                case "inside": return _zones.Inside(a, args.Count > 1 ? args[1] : "player") ? 1 : 0;
                case "holding":
                {
                    ImagineApp app = ImagineApp.Instance;
                    if (app == null || app.Rig == null) return 0;
                    foreach (Grabber g in new[] { app.Rig.LeftGrabber, app.Rig.RightGrabber })
                        if (g != null && g.Held != null && (a == null || Rules.Names(a, Rules.Of(g.Held)))) return 1;
                    return 0;
                }
                case "riding":
                    return _world.Vehicle != null && _world.Vehicle.Riding && (a == null || Rules.Names(a, new Rules.Subject { Asset = _world.Vehicle.Asset, Group = _world.Vehicle.Asset })) ? 1 : 0;
                case "speed":
                    if (a == null || a == "player") return _world.Vehicle != null ? _world.Vehicle.Speed : 0;
                    return SpeedOf(a);
                case "height":
                {
                    Vector3 p;
                    if (a == null || a == "player") p = _world.Vehicle != null ? _world.Vehicle.transform.position : Vector3.zero;
                    else if (!PositionOf(a, out p)) return 0;
                    float ground = _world.SurfaceHeight(p.x, p.z), water = _world.Terrain.WaterY;
                    if (!float.IsNaN(water) && water > ground) ground = water;
                    return p.y - ground;
                }
                case "health":
                    if (a == null || a == "player") return PlayerHealth;
                    Rules.Subject s = _world.Rules.Nearest(a, out bool found);
                    Health h = found && s.Object != null ? s.Object.GetComponent<Health>() : null;
                    return h != null ? h.Hp : found ? 1 : 0;
            }
            return 0;
        }

        double CountOf(string name, bool alive, bool standing, bool tipped)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            int n = 0;
            if (!standing && !tipped)
                foreach (Creature c in Creature.All)
                    if (c != null && Rules.Names(name, Rules.Of(c)) && (!alive || (c.Up && !Health.IsDead(c.gameObject)))) n++;
            foreach (Prop p in _world.Props.All)
            {
                if (p == null || p.GroupId == "shot" || !Rules.Names(name, Rules.Of(p))) continue;
                if (alive) { if (p.GetComponent<Health>() != null && !Health.IsDead(p.gameObject)) n++; continue; }
                bool down = IsTipped(p);
                if (standing && down || tipped && !down) continue;
                n++;
            }
            if (!standing && !tipped)
                foreach (Hittable h in Hittable.All)
                    if (h != null && Rules.Names(name, Rules.Of(h)) && (!alive || h.GetComponent<Health>() != null && !Health.IsDead(h.gameObject))) n++;
            if (!alive && !standing && !tipped)
                foreach (Group g in _world.Objects.Groups)
                    if (g.Items.Count > 0 && g.Extras.Count == 0 && Rules.Names(name, new Rules.Subject { Group = g.Id, Asset = g.Asset })) n += g.Items.Count;
            return n;
        }

        double Moving(string name)
        {
            int n = 0;
            foreach (Prop p in _world.Props.All)
                if (p != null && p.Body != null && p.GroupId != "shot" && Rules.Names(name, Rules.Of(p)) && p.Body.linearVelocity.magnitude > 0.3f) n++;
            return n;
        }

        double SpeedOf(string name)
        {
            foreach (Prop p in _world.Props.All)
                if (p != null && p.Body != null && Rules.Names(name, Rules.Of(p))) return p.Body.linearVelocity.magnitude;
            return 0;
        }

        bool PositionOf(string name, out Vector3 at)
        {
            at = default;
            if (string.IsNullOrEmpty(name)) return false;
            if (name == "player") { at = _world.Head != null ? _world.Head.position : Vector3.zero; at.y = _world.Vehicle != null ? _world.Vehicle.transform.position.y : at.y; return true; }
            if (_zones.Centre(name, out at)) return true;
            Rules.Subject s = _world.Rules.Nearest(name, out bool found);
            at = s.Object != null ? s.Object.transform.position : s.Position;
            return found;
        }

        static bool IsTipped(Prop p) => Vector3.Angle(p.transform.up, Vector3.up) > 55f;

        // ------------------------------------------------------------------ every frame

        public void Tick(float dt)
        {
            _frame++;
            if (_slowUntil > 0f && Time.unscaledTime >= _slowUntil) { Time.timeScale = 1f; Time.fixedDeltaTime = _baseFixed; _slowUntil = -1f; }

            foreach (string name in _order)
                if (_defs.TryGetValue(name, out VarDef def) && def.Rate != 0 && _vars[name] is double v)
                {
                    double next = Math.Min(Math.Max(v + def.Rate * dt, def.Min), def.Max);
                    if (def.As == "time" && def.Rate < 0) next = Math.Max(next, 0);
                    _vars[name] = next;
                    // a clock is a change worth testing for only when its whole seconds change
                    if (Math.Floor(next) != Math.Floor(v)) { _world.Rules.Changed(); if (Hud != null) Hud.Changed(); }
                }

            if (Stage != null && _stages.TryGetValue(Stage, out StageDef stage) && stage.Timeout > 0f && Time.time - _stageStarted > stage.Timeout)
                GoStage(!string.IsNullOrEmpty(stage.Fail) ? stage.Fail : stage.Next ?? Stage);

            for (int i = _resets.Count - 1; i >= 0; i--)
                if (Time.time >= _resets[i].at) { string target = _resets[i].target; _resets.RemoveAt(i); Reset(target, 0f); }

            if (_deadUntil > 0f && Time.time >= _deadUntil)
            {
                _deadUntil = -1f;
                if (PlayerHealth <= 0f)
                {
                    PlayerHealth = PlayerMax;
                    MovePlayer(Checkpoint, float.NaN);
                    if (Hud != null) Hud.Changed();
                }
            }

            _zones.Tick(dt, _frame);
            Watch(dt);
            Surfaces.Tick(_world, _frame);
            DesktopPress();
        }

        /// <summary>What has fallen over, and what has come to rest after moving: `tip` and `rest`.</summary>
        void Watch(float dt)
        {
            Vector3 head = _world.Head != null ? _world.Head.position : Vector3.zero;
            Prop struck = null;
            foreach (Prop p in _world.Props.All)
            {
                // a shot fired at the user reaches them: they are hurt (only when they can be)
                if (p != null && p.Hostile && (p.transform.position - head).sqrMagnitude < 0.22f) { struck = p; continue; }
                if (p == null || p.Body == null || p.GroupId == "shot" || p.Held || p.Floating) continue;
                if (!_tracks.TryGetValue(p, out Track t)) { _tracks[p] = t = new Track { Tipped = IsTipped(p) }; continue; }
                bool tipped = IsTipped(p);
                if (tipped && !t.Tipped) { _world.Rules.Fire("tip", Rules.Of(p)); _world.Rules.Changed(); }
                else if (!tipped && t.Tipped) _world.Rules.Changed();
                t.Tipped = tipped;
                float speed = p.Body.linearVelocity.magnitude;
                if (speed > 0.3f) { t.Moved = true; t.Still = 0f; }
                else if (t.Moved && speed < 0.06f && p.Body.angularVelocity.magnitude < 0.5f)
                {
                    t.Still += dt;
                    if (t.Still > 0.5f) { t.Moved = false; t.Still = 0f; _world.Rules.Fire("rest", Rules.Of(p)); _world.Rules.Changed(); }
                }
            }
            if (struck != null)
            {
                HurtPlayer(struck.Damage, struck.transform.position);
                Fx.Burst(struck.transform.position, new Color(1f, 0.3f, 0.2f), Color.white, 10, 2f, 0.04f, 0.3f, 0f, true);
                _world.Props.Destroy(struck);
            }
            if (_frame % 120 == 0)
            {
                var gone = new List<Prop>();
                foreach (Prop p in _tracks.Keys) if (p == null) gone.Add(p);
                foreach (Prop p in gone) _tracks.Remove(p);
            }
        }

        void DesktopPress()
        {
            ImagineApp app = ImagineApp.Instance;
            if (app == null || app.Rig == null || app.Rig.XrActive || ImagineApp.Typing || Keyboard.current == null) return;
            if (Keyboard.current.fKey.wasPressedThisFrame) _world.TryPress(app.Rig.Head);
        }

        // ------------------------------------------------------------------ starting over

        /// <summary>A new place: no counters, stages, zones, objective or health left from the last one.</summary>
        public void Clear()
        {
            _vars.Clear();
            _defs.Clear();
            _order.Clear();
            _stages.Clear();
            _sources.Clear();
            _tracks.Clear();
            _resets.Clear();
            _hittable.Clear();
            _zones.Clear();
            Stage = null;
            Objective = "";
            PlayerMax = PlayerHealth = 0f;
            Checkpoint = Vector3.zero;
            _deadUntil = -1f;
            if (_slowUntil > 0f) { Time.timeScale = 1f; Time.fixedDeltaTime = _baseFixed; _slowUntil = -1f; }
            if (Hud != null) Hud.Clear();
        }
    }
}
