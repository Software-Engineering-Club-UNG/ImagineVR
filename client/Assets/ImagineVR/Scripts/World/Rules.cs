using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// `when`: the world reacting. A rule is an event, what it is about, and a list of things to do.
    /// Rules live here in the headset and nowhere else: the server remembers the statement that
    /// made each one, and that is all. (Their predecessor mirrored its whole state to the server
    /// before every turn, and a server restart then refused every turn until the user reset.)
    ///
    /// The third version of the language adds: events for places (`enter`, `exit`), for things
    /// falling and settling (`tip`, `rest`), for health (`hurt`, `die`), `press`, `strike`,
    /// `collide`, `drop`, `lap`, `stage`, and `becomes` (an expression turning true); a condition
    /// (`if`) on any rule and on any action; rules scoped to a stage; and the actions that keep
    /// score, move the story on and win or lose. See openrouter-api/imagine/v3.py for every field.
    /// </summary>
    public sealed class Rules
    {
        sealed class Rule
        {
            public string Id, Event, Target, Zone, With, Stage;
            public Expr Test, If;
            public float Seconds, Radius, Next, Cooldown, LastFired = -999f;
            public int Times, Fired;
            public bool Once, Spent, Inside, WasTrue, Disabled;
            public Pose Frame = Pose.identity;
            public List<Dictionary<string, object>> Do;
        }

        struct Pending { public float At; public Dictionary<string, object> Action; public Subject Subject; public Pose Frame; }

        /// <summary>What an event happened to. Any of these may be missing.</summary>
        public struct Subject
        {
            public Creature Creature;
            public Prop Prop;
            public Hittable Hit;
            public string Asset, Group;
            public Vector3 Position;
            /// <summary>How hard it was hit: damage for `health`. 0 means one hit.</summary>
            public float Amount;
            public bool Player;
            public GameObject Object => Creature != null ? Creature.gameObject : Prop != null ? Prop.gameObject : Hit != null ? Hit.gameObject : null;
        }

        readonly World _world;
        readonly List<Rule> _rules = new List<Rule>();
        readonly List<Pending> _pending = new List<Pending>();
        int _spawned;
        float _testAt;
        bool _changed;

        /// <summary>The old running score: the counter `score`, kept for headsets and rules from before counters.</summary>
        public int Score => Mathf.RoundToInt((float)_world.Play.Number("score"));
        public int Count => _rules.Count;

        public Rules(World world) { _world = world; }

        public void Install(Statement s)
        {
            string id = s.Str("id") ?? "rule" + _rules.Count;
            Remove(id);
            var rule = new Rule
            {
                Id = id, Event = s.Str("event", "hit"), Target = s.Str("target", "any"), Zone = s.Str("zone"), With = s.Str("with"), Stage = s.Str("stage"),
                Seconds = Mathf.Max(s.Num("seconds", 5f), 0.2f), Radius = Mathf.Max(s.Num("radius", 3f), 0.3f),
                Once = s.Flag("once"), Do = s.ObjList("do"), Cooldown = s.Num("cooldown", 0f), Times = s.Int("times", 0), Frame = s.Frame(),
                Test = s.Has("test") ? Expr.Of(s.Str("test")) : null, If = s.Has("if") ? Expr.Of(s.Str("if")) : null,
            };
            rule.Next = Time.time + rule.Seconds;
            // a test already true when the rule arrives fires only once it has been false: "when all pins are down" must not fire on an empty lane
            if (rule.Test != null) rule.WasTrue = rule.Test.Truth(_world.Play);
            _rules.Add(rule);
            SessionLog.Write("rule", "id", id, "event", rule.Event, "target", rule.Target, "zone", rule.Zone, "actions", rule.Do.Count,
                "test", rule.Test != null ? rule.Test.Source : null, "valid", rule.Test == null || rule.Test.Valid);
            if (rule.Event == "start") { Run(rule, Nearest(rule.Target)); rule.Spent = true; }
            // something a rule is about can be struck, pressed and counted even when it is plain scenery
            if (rule.Event == "hit" || rule.Event == "press" || rule.Event == "die" || rule.Event == "hurt") _world.Play.MakeHittable(rule.Target);
        }

        public bool Remove(string id) => _rules.RemoveAll(r => r.Id == id) > 0;

        public void Clear(bool keepScore = false)
        {
            _rules.Clear();
            _pending.Clear();
            if (!keepScore) _world.Play.Set("score", 0.0);
        }

        public void Enable(string id, bool on)
        {
            foreach (Rule r in _rules) if (r.Id == id || r.Id.StartsWith(id + "_", System.StringComparison.Ordinal)) r.Disabled = !on;
        }

        /// <summary>Would anything happen if this event happened to this thing? (A press is only taken when it would.)</summary>
        public bool Handles(string ev, in Subject subject)
        {
            foreach (Rule r in _rules)
                if (!r.Spent && !r.Disabled && r.Event == ev && Names(r.Target, subject) && (r.Stage == null || r.Stage == _world.Play.Stage)) return true;
            return false;
        }

        /// <summary>Something counted changed: tests are looked at again on the next frame.</summary>
        public void Changed() { _changed = true; }

        /// <summary>Is `target` (an id, an asset name, `any`, or `player`) a name for this thing?</summary>
        public static bool Names(string target, in Subject subject)
        {
            if (string.IsNullOrEmpty(target) || target == "any") return true;
            if (target == "player") return subject.Player;
            if (subject.Player) return false;
            if (target == subject.Group || target == subject.Asset) return true;
            if (subject.Group != null && subject.Group.Length > target.Length && subject.Group.StartsWith(target, System.StringComparison.Ordinal))
            {
                char after = subject.Group[target.Length];
                if (after == '.' || after == '_') return true;      // `pins` names pins_1 and pins_2; `robot` names robot.head
            }
            // "zombies" for zombie, "sheep" for a made "fluffy_sheep"
            AssetDef named = Named(target);
            return named != null && named.Id == subject.Asset || subject.Asset != null && subject.Asset.Contains(target);
        }

        // What asset a rule's target names, remembered for a few seconds. Looking a name up is a
        // search of the whole library by spelling, and a `near` rule asks it of everything in the
        // world several times a second: with one such rule about a thing's id ("van_gogh") the
        // 01:15 session ran at 11 frames a second until the place was cleared.
        static readonly Dictionary<string, (AssetDef def, float until)> NamesCache = new Dictionary<string, (AssetDef, float)>();

        static AssetDef Named(string target)
        {
            if (NamesCache.TryGetValue(target, out var known) && Time.realtimeSinceStartup < known.until) return known.def;
            if (NamesCache.Count > 64) NamesCache.Clear();
            AssetDef def = Library.FindAsset(target);
            NamesCache[target] = (def, Time.realtimeSinceStartup + 3f);
            return def;
        }

        public static Subject Of(Creature c) => new Subject { Creature = c, Asset = c.Def.Id, Group = c.GroupId, Position = c.transform.position + Vector3.up * (c.Height * 0.6f) };
        public static Subject Of(Prop p) => new Subject { Prop = p, Asset = p.Def.Id, Group = p.GroupId, Position = p.transform.position };
        public static Subject Of(Hittable h) => new Subject { Hit = h, Asset = h.Asset, Group = h.Group, Position = h.transform.position };
        public Subject OfPlayer()
        {
            Vector3 at = _world.Head != null ? _world.Head.position : Vector3.zero;
            return new Subject { Player = true, Asset = "player", Group = "player", Position = at };
        }

        /// <summary>
        /// Something happened to something. Runs every rule about it, and says whether one of them
        /// took the thing over (removed, respawned, exploded or launched it, or its health took the
        /// blow), in which case whatever would ordinarily have happened to it should not.
        /// `zone` is the place entered or left; `with` is what it collided with.
        /// </summary>
        public bool Fire(string ev, in Subject subject, string zone = null, string with = null)
        {
            bool took = false;
            // a blow to something with health is damage before it is anything else
            if (ev == "hit" && !subject.Player) took |= Health.Strike(_world, subject);
            for (int i = 0; i < _rules.Count; i++)
            {
                Rule rule = _rules[i];
                if (rule.Spent || rule.Disabled || rule.Event != ev) continue;
                if (rule.Zone != null && rule.Zone != zone) continue;
                if (rule.With != null && !NamesThing(rule.With, with)) continue;
                if (!Names(rule.Target, subject)) continue;
                if (!Ready(rule)) continue;
                took |= Run(rule, subject);
                if (rule.Once) rule.Spent = true;
            }
            return took;
        }

        static bool NamesThing(string target, string other)
        {
            if (other == null) return false;
            int bar = other.IndexOf('|');
            if (bar >= 0) return NamesThing(target, other.Substring(0, bar)) || NamesThing(target, other.Substring(bar + 1));
            if (other == target || other.StartsWith(target + "_", System.StringComparison.Ordinal) || other.StartsWith(target + ".", System.StringComparison.Ordinal)) return true;
            AssetDef named = Named(target);
            return named != null && named.Id == other || other.Contains(target);
        }

        /// <summary>The checks every rule passes before it runs: its stage, its cooldown, how often, its condition.</summary>
        bool Ready(Rule rule)
        {
            if (rule.Stage != null && rule.Stage != _world.Play.Stage) return false;
            if (rule.Cooldown > 0f && Time.time - rule.LastFired < rule.Cooldown) return false;
            if (rule.Times > 0 && rule.Fired >= rule.Times) return false;
            if (rule.If != null && !rule.If.Truth(_world.Play)) return false;
            return true;
        }

        bool Run(Rule rule, in Subject subject)
        {
            bool took = false;
            rule.LastFired = Time.time;
            rule.Fired++;
            SessionLog.Write("rule_fired", "id", rule.Id, "event", rule.Event, "asset", subject.Asset, "group", subject.Group, "at", subject.Position);
            // every condition is asked first, of the world as it was when the rule fired: a "STRIKE!" banner
            // conditioned on the throw count must not see the count the same rule then resets
            var allowed = new bool[rule.Do.Count];
            for (int i = 0; i < rule.Do.Count; i++)
            {
                Statement a = Statement.Wrap(rule.Do[i]);
                allowed[i] = !a.Has("if") || Expr.Of(a.Str("if")).Truth(_world.Play);
            }
            for (int i = 0; i < rule.Do.Count; i++)
            {
                if (!allowed[i]) continue;
                took |= Schedule(rule.Do[i], subject, rule.Frame);
            }
            return took;
        }

        /// <summary>Runs a list of actions that is not a rule's: a stage's `enter`.</summary>
        public void RunActions(List<Dictionary<string, object>> actions, Pose frame)
        {
            Subject here = OfPlayer();
            foreach (Dictionary<string, object> action in actions)
            {
                Statement a = Statement.Wrap(action);
                if (a.Has("if") && !Expr.Of(a.Str("if")).Truth(_world.Play)) continue;
                Schedule(action, here, frame);
            }
        }

        bool Schedule(Dictionary<string, object> action, in Subject subject, Pose frame)
        {
            Statement a = Statement.Wrap(action);
            string act = a.Str("act", "");
            bool took = act == "remove" || act == "respawn" || act == "explode" || act == "launch";
            float delay = a.Num("delay", 0f);
            if (delay > 0.01f) _pending.Add(new Pending { At = Time.time + delay, Action = action, Subject = subject, Frame = frame });
            else Act(a, subject, frame);
            return took && !a.Has("target");
        }

        public void Tick(float dt)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pending[i].At) continue;
                Pending p = _pending[i];
                _pending.RemoveAt(i);
                Act(Statement.Wrap(p.Action), p.Subject, p.Frame);
            }
            if (_rules.Count == 0) return;
            Vector3 user = _world.Head != null ? _world.Head.position : Vector3.zero;
            bool testNow = _changed || Time.time >= _testAt;
            if (testNow) { _changed = false; _testAt = Time.time + 0.2f; }
            for (int i = 0; i < _rules.Count; i++)
            {
                Rule rule = _rules[i];
                if (rule.Spent || rule.Disabled) continue;
                if (rule.Event == "every" && Time.time >= rule.Next)
                {
                    rule.Next = Time.time + rule.Seconds;
                    if (!Ready(rule)) continue;
                    Run(rule, Nearest(rule.Target));
                    if (rule.Once) rule.Spent = true;
                }
                else if (rule.Event == "near" && Time.frameCount % 6 == i % 6)
                {
                    Subject nearest = Nearest(rule.Target, out bool found);
                    Vector3 to = nearest.Position - user;
                    to.y = 0f;
                    bool inside = found && to.magnitude < rule.Radius;
                    if (inside && !rule.Inside && Ready(rule))
                    {
                        Run(rule, nearest);
                        if (rule.Once) rule.Spent = true;
                    }
                    rule.Inside = inside;
                }
                else if (rule.Event == "becomes" && testNow && rule.Test != null)
                {
                    // on the rising edge only: it fires when it turns true, and again only after it has been false
                    bool now = (rule.Stage == null || rule.Stage == _world.Play.Stage) && rule.Test.Truth(_world.Play);
                    if (now && !rule.WasTrue && Ready(rule))
                    {
                        rule.WasTrue = true;
                        Run(rule, OfPlayer());
                        if (rule.Once) rule.Spent = true;
                    }
                    else rule.WasTrue = now;
                }
            }
        }

        public Subject Nearest(string target) => Nearest(target, out _);

        /// <summary>The matching thing nearest the user; with none, a spot a few metres in front of them.</summary>
        public Subject Nearest(string target, out bool found)
        {
            Transform head = _world.Head;
            Vector3 user = head != null ? head.position : Vector3.zero;
            Subject best = default;
            float bestD = float.MaxValue;
            found = false;
            if (target == "player") { found = true; return OfPlayer(); }
            void Offer(in Subject s)
            {
                if (!Names(target, s)) return;
                Vector3 to = s.Position - user;
                to.y = 0f;
                if (to.sqrMagnitude < bestD) { bestD = to.sqrMagnitude; best = s; }
            }
            if (target != "any" && !string.IsNullOrEmpty(target))
            {
                foreach (Creature c in Creature.All) if (c != null && c.Up) Offer(Of(c));
                foreach (Prop p in _world.Props.All) if (p != null && p.GroupId != "shot") Offer(Of(p));
                foreach (Hittable h in Hittable.All) if (h != null) Offer(Of(h));
                foreach (Group g in _world.Objects.Groups)
                    foreach (Placement p in g.Items)
                    {
                        var at = new Vector3(p.Position.x, _world.Terrain.HeightAt(p.Position.x, p.Position.z) + p.Position.y, p.Position.z);
                        Offer(new Subject { Asset = g.Asset, Group = g.Id, Position = at });
                    }
                if (_world.Play.ZoneCentre(target, out Vector3 zoneAt)) Offer(new Subject { Asset = target, Group = target, Position = zoneAt });
            }
            found = bestD < float.MaxValue;
            if (!found)
            {
                Vector3 ahead = head != null ? head.forward : Vector3.forward;
                ahead.y = 0f;
                ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;
                Vector3 spot = user + ahead * 7f;
                spot.y = _world.Terrain.HeightAt(spot.x, spot.z) + 1f;
                best = new Subject { Position = spot };
            }
            return best;
        }

        /// <summary>Where an action names a place: a position written as the user saw it, or the id of something.</summary>
        bool Where(Statement a, string key, Pose frame, out Vector3 at)
        {
            if (a.TryVec3(key, out Vector3 local))
            {
                at = Statement.InWorld(frame, local);
                at.y += _world.SurfaceHeight(at.x, at.z);
                return true;
            }
            string name = a.Str(key);
            if (!string.IsNullOrEmpty(name))
            {
                Subject s = Nearest(name, out bool found);
                at = s.Position;
                if (found) return true;
                if (_world.Play.ZoneCentre(name, out at)) return true;
            }
            at = default;
            return false;
        }

        // ------------------------------------------------------------------ actions

        void Act(Statement a, Subject subject, Pose frame)
        {
            // `target`: done to that thing in place of the one the event happened to
            if (a.Has("target") && a.Str("act") != "reset" && a.Str("act") != "modify" && a.Str("act") != "teleport")
            {
                Subject named = Nearest(a.Str("target"), out bool found);
                if (!found) return;
                subject = named;
            }
            Vector3 at = subject.Object != null ? (subject.Creature != null ? Of(subject.Creature).Position : subject.Object.transform.position) : subject.Position;
            PlaySystem play = _world.Play;
            switch (a.Str("act", ""))
            {
                case "burst": Burst(a.Str("kind", "confetti"), at, a.TryColor("color", out Color bc) ? bc : (Color?)null); break;
                case "sound": if (a.Str("name", "pop") != "none") Sfx.Play(a.Str("name", "pop"), at, 0.9f); break;
                case "remove":
                    if (subject.Creature != null) Object.Destroy(subject.Creature.gameObject);
                    else if (subject.Prop != null) _world.Props.Destroy(subject.Prop);
                    else if (subject.Hit != null) subject.Hit.Remove();
                    else if (subject.Group != null && !subject.Player) _world.Objects.Remove(subject.Group, 0.4f);
                    break;
                case "respawn":
                    if (subject.Creature != null) subject.Creature.Vanish();
                    else if (subject.Prop != null) { string id = subject.Prop.GroupId; _world.Props.Destroy(subject.Prop); play.Reset(id, 1.5f); }
                    break;
                case "spawn": Spawn(a, at, frame); break;
                case "tint":
                    if (a.TryColor("color", out Color tint))
                    {
                        if (subject.Creature != null) subject.Creature.SetTint(tint);
                        else if (subject.Prop != null) subject.Prop.SetTint(tint);
                        else if (subject.Group != null) { Group g = _world.Objects.Get(subject.Group); if (g != null) g.Tint = tint; }
                    }
                    break;
                case "scale":
                    if (subject.Object != null)
                    {
                        Vector3 s = subject.Object.transform.localScale * Mathf.Clamp(a.Num("by", 1.5f), 0.1f, 10f);
                        float biggest = Mathf.Max(s.x, s.y, s.z);
                        if (biggest > 0.02f && biggest < 40f) subject.Object.transform.localScale = s;
                    }
                    break;
                case "explode":
                    Explode(at, Mathf.Clamp(a.Num("force", 12f), 0f, 40f), a.TryColor("color", out Color ec) ? ec : new Color(1f, 0.6f, 0.2f), subject,
                        a.Num("damage", 0f), Mathf.Clamp(a.Num("radius", 6f), 0.5f, 40f));
                    break;
                case "launch":
                    if (subject.Prop != null && subject.Prop.Body != null && !subject.Prop.Body.isKinematic)
                        subject.Prop.Body.AddForce(Vector3.up * Mathf.Clamp(a.Num("force", 8f), 0f, 40f) + Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
                    else if (subject.Creature != null) subject.Creature.Knock(Random.insideUnitSphere);
                    break;
                case "score":
                    play.Add("score", Mathf.RoundToInt(a.Num("add", 1f)));
                    Sfx.Play("score", at, 0.8f, Mathf.Clamp(1f + Score * 0.01f, 1f, 1.6f));
                    if (!play.Labelled("score")) _world.Emit("score " + Score);
                    break;
                case "say":
                    if (!string.IsNullOrEmpty(a.Str("text"))) _world.Emit(Expr.Fill(a.Str("text"), play));
                    break;

                // ---- the third version
                case "set":
                    if (a.Has("text")) play.Set(a.Str("var"), Expr.Fill(a.Str("text"), play));
                    else play.Set(a.Str("var"), Expr.Value(a.Fields.TryGetValue("to", out object to) ? to : 0.0, play));
                    break;
                case "add":
                    play.Add(a.Str("var"), Expr.Value(a.Fields.TryGetValue("by", out object by) ? by : 1.0, play, 1.0));
                    break;
                case "banner":
                    play.Banner(Expr.Fill(a.Str("text", ""), play), a.Num("seconds", 2.5f), a.TryColor("color", out Color banner) ? banner : (Color?)null);
                    if (a.Has("sound")) Sfx.Play(a.Str("sound"), _world.Head != null ? _world.Head.position : at, 0.8f);
                    break;
                case "objective": play.SetObjective(Expr.Fill(a.Str("text", ""), play)); break;
                case "win": play.Finish(true, Expr.Fill(a.Str("text", "You win!"), play), a.Has("sound") ? a.Str("sound") : "fanfare"); break;
                case "lose": play.Finish(false, Expr.Fill(a.Str("text", "Game over"), play), a.Has("sound") ? a.Str("sound") : "lose"); break;
                case "reset": play.Reset(a.Str("target") ?? subject.Group, 0f); break;
                case "stage": play.GoStage(a.Str("go", "next")); break;
                case "checkpoint":
                    play.Checkpoint = a.TryVec3("at", out Vector3 cp) ? Statement.InWorld(frame, cp) : _world.Head != null ? _world.Head.position : at;
                    break;
                case "teleport":
                {
                    if (!Where(a, "to", frame, out Vector3 dest)) break;
                    string who = a.Str("who", subject.Player ? "player" : null);
                    float face = a.Has("face") ? a.Num("face") + frame.rotation.eulerAngles.y : float.NaN;   // written as the user faced when the rule was made
                    if (who == "player" || (who == null && subject.Player)) play.MovePlayer(dest, face);
                    else
                    {
                        Subject thing = who != null ? Nearest(who, out bool found) : subject;
                        if (who != null && !found) break;
                        Move(thing, dest);
                    }
                    break;
                }
                case "give":
                {
                    Statement give = Statement.Make("give", "asset", a.Str("asset", "cube"), "id", a.Str("asset", "cube") + "_given" + (_spawned++ % 8));
                    if (a.Has("tint")) give.Fields["tint"] = a.Str("tint");
                    _world.Props.Give(give);
                    break;
                }
                case "take": play.Take(a.Str("asset", "all")); break;
                case "damage":
                case "heal":
                {
                    float amount = a.Num("amount", a.Str("act") == "damage" ? 1f : 25f) * (a.Str("act") == "heal" ? -1f : 1f);
                    float radius = a.Num("radius", 0f);
                    string who = a.Str("who");
                    if (radius > 0f) Health.Area(_world, at, radius, amount);
                    else if (who == "player" || (who == null && subject.Player)) play.HurtPlayer(amount, at);
                    else
                    {
                        Subject thing = who != null ? Nearest(who, out bool found) : subject;
                        if (who != null && !found) break;
                        thing.Amount = amount;
                        if (amount > 0f) Health.Strike(_world, thing); else Health.Heal(thing, -amount);
                    }
                    break;
                }
                case "enable": Enable(a.Str("rule"), true); break;
                case "disable": Enable(a.Str("rule"), false); break;
                case "speak": play.Speak(a.Str("id"), Expr.Fill(a.Str("text", ""), play), a.Str("voice")); break;
                case "narrate": play.Speak(null, Expr.Fill(a.Str("text", ""), play), a.Str("voice", "narrator")); break;
                case "music":
                {
                    var music = new Dictionary<string, object>(a.Fields) { ["op"] = "music" };
                    music.Remove("act");
                    _world.Apply(Statement.Wrap(music).WithOp("music"));
                    break;
                }
                case "fade": play.Fade(a.Num("seconds", 1f), a.TryColor("color", out Color fc) ? fc : Color.black); break;
                case "slowmo": play.SlowMotion(a.Num("scale", 0.3f), a.Num("seconds", 2f)); break;
                case "shake": play.Shake(a.Num("amount", 0.5f)); break;
                case "door":
                    _world.Structures.Door(Statement.Make("door", "id", a.Str("id"), "state", a.Str("state", "toggle")));
                    break;
                case "modify":
                {
                    var change = new Dictionary<string, object>(a.Fields) { ["op"] = "modify", ["id"] = a.Str("target") ?? subject.Group };
                    change.Remove("act");
                    change.Remove("target");
                    change.Remove("delay");
                    change.Remove("if");
                    if (change["id"] != null) _world.Apply(Statement.Wrap(change).WithOp("modify"));
                    break;
                }
                case "direct": play.Direct(Expr.Fill(a.Str("note", ""), play)); break;
                case "fx": Emitters.Timed(a.Str("kind", "sparks"), at, a.Num("seconds", 6f), a.Num("size", 1f), a.TryColor("color", out Color xc) ? xc : (Color?)null); break;
                case "ride":
                {
                    var ride = new Dictionary<string, object>(a.Fields) { ["op"] = "ride" };
                    ride.Remove("act");
                    _world.Apply(Statement.Wrap(ride).WithOp("ride"));
                    break;
                }
            }
        }

        void Move(Subject thing, Vector3 to)
        {
            if (thing.Creature != null) thing.Creature.transform.position = to;
            else if (thing.Prop != null && thing.Prop.Body != null)
            {
                if (thing.Prop.Holder != null) thing.Prop.Holder.ForceRelease();
                thing.Prop.Body.linearVelocity = Vector3.zero;
                thing.Prop.Body.angularVelocity = Vector3.zero;
                thing.Prop.Body.position = to + Vector3.up * 0.1f;
                thing.Prop.transform.position = to + Vector3.up * 0.1f;
            }
        }

        public static bool IsBurst(string kind)
        {
            switch (kind)
            {
                case "confetti": case "sparks": case "smoke": case "sparkles": case "dust": case "splash": case "fire": return true;
                default: return false;
            }
        }

        public static void Burst(string kind, Vector3 at, Color? color = null)
        {
            switch (kind)
            {
                case "sparks": Fx.Burst(at, color ?? new Color(1f, 0.85f, 0.4f), Color.white, 28, 5f, 0.03f, 0.5f, 1f, true); break;
                case "smoke": Fx.Burst(at, color ?? new Color(0.35f, 0.35f, 0.38f, 0.7f), new Color(0.6f, 0.6f, 0.62f, 0.4f), 18, 1.2f, 0.35f, 1.6f, -0.15f, false); break;
                case "sparkles": Fx.Burst(at, color ?? new Color(1f, 0.9f, 0.6f), Color.white, 26, 1.6f, 0.035f, 0.9f, 0f, true); break;
                case "dust": Fx.Burst(at, color ?? new Color(0.7f, 0.62f, 0.5f, 0.7f), new Color(0.85f, 0.8f, 0.7f, 0.4f), 16, 1.5f, 0.2f, 1.1f, 0.1f, false); break;
                case "splash": Fx.Burst(at, color ?? new Color(0.55f, 0.8f, 1f, 0.8f), Color.white, 30, 4f, 0.05f, 0.8f, 1f, false); break;
                case "fire": Fx.Burst(at, color ?? new Color(1f, 0.55f, 0.15f), new Color(1f, 0.9f, 0.4f), 30, 2.2f, 0.12f, 0.7f, -0.6f, true); break;
                default: Fx.Confetti(at, 80, 2.6f, 0.06f, color); break;
            }
        }

        void Explode(Vector3 at, float force, Color color, in Subject source, float damage, float radius)
        {
            Fx.Burst(at, color, new Color(1f, 0.95f, 0.6f), 46, 7f, 0.16f, 0.7f, 0.4f, true);
            Burst("smoke", at);
            Sfx.Play("boom", at, 1f, Random.Range(0.9f, 1.1f));
            foreach (Prop p in _world.Props.All)
            {
                if (p == null || p.Body == null || p.Held || p.Body.isKinematic) continue;
                if ((p.transform.position - at).magnitude < radius) p.Body.AddExplosionForce(force, at, radius, 0.6f, ForceMode.VelocityChange);
            }
            foreach (Creature c in new List<Creature>(Creature.All))
            {
                if (c == null || c == source.Creature) continue;
                Vector3 away = c.transform.position - at;
                if (away.magnitude < radius && (damage <= 0f || c.GetComponent<Health>() == null)) c.Knock(away);
            }
            if (damage > 0f) Health.Area(_world, at, radius, damage, source.Object);
            if (source.Creature != null) source.Creature.Vanish();
            else if (source.Prop != null) _world.Props.Destroy(source.Prop);
            ImagineApp app = ImagineApp.Instance;
            if (app != null && app.Rig != null && _world.Head != null && (_world.Head.position - at).magnitude < 25f)
            {
                app.Rig.Haptic(true, 1f, 0.18f);
                app.Rig.Haptic(false, 1f, 0.18f);
            }
        }

        /// <summary>New things where it happened, or `at` a place, or round the user (`near`). They take their turn in a ring of ids, so a rule that spawns for ever does not fill the world.</summary>
        void Spawn(Statement a, Vector3 at, Pose frame)
        {
            int count = Mathf.Clamp(a.Int("count", 1), 1, 12);
            bool aroundUser = a.Str("near") == "player";
            float ring = a.Num("radius", aroundUser ? 18f : 1.2f);
            if (a.Has("at")) Where(a, "at", frame, out at);
            Vector3 user = _world.Head != null ? _world.Head.position : Vector3.zero;
            string id = a.Str("id");
            int most = a.Int("max", 40);
            if (Creature.All.Count >= most && a.Has("behavior")) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = at;
                if (aroundUser)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    p = user + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * ring * Random.Range(0.8f, 1.1f);
                }
                else if (count > 1) { Vector2 off = Random.insideUnitCircle * ring; p += new Vector3(off.x, 0f, off.y); }
                bool loose = a.Flag("physics");
                float ground = _world.SurfaceHeight(p.x, p.z);
                string name = id != null && count == 1 ? id : (id ?? "rule_spawn") + "_" + (_spawned++ % 24);
                Statement s = Statement.Make("spawn", "id", name, "asset", a.Str("asset", "cube"),
                    "at", new List<object> { (double)p.x, (double)(loose ? Mathf.Max(p.y - ground, 0.2f) : 0f), (double)p.z });
                foreach (string key in new[] { "scale", "behavior", "tint", "health", "team", "speed", "damage", "sight" })
                    if (a.Fields.TryGetValue(key, out object v)) s.Fields[key] = v;
                if (loose) s.Fields["physics"] = true;
                _world.Apply(s);
            }
        }
    }
}
