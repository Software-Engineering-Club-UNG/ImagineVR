using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// `when`: the world reacting. A rule is an event, what it is about, and a list of things to do.
    /// Rules live here in the headset and nowhere else: the server remembers the statement that
    /// made each one, and that is all. (Their predecessor mirrored its whole state to the server
    /// before every turn, and a server restart then refused every turn until the user reset.)
    /// </summary>
    public sealed class Rules
    {
        sealed class Rule
        {
            public string Id, Event, Target;
            public float Seconds, Radius, Next;
            public bool Once, Spent, Inside;
            public List<Dictionary<string, object>> Do;
        }

        struct Pending { public float At; public Dictionary<string, object> Action; public Subject Subject; }

        /// <summary>What an event happened to. Any of the three may be missing.</summary>
        public struct Subject
        {
            public Creature Creature;
            public Prop Prop;
            public string Asset, Group;
            public Vector3 Position;
            public GameObject Object => Creature != null ? Creature.gameObject : Prop != null ? Prop.gameObject : null;
        }

        readonly World _world;
        readonly List<Rule> _rules = new List<Rule>();
        readonly List<Pending> _pending = new List<Pending>();
        int _spawned;

        public int Score { get; private set; }
        public int Count => _rules.Count;

        public Rules(World world) { _world = world; }

        public void Install(Statement s)
        {
            string id = s.Str("id") ?? "rule" + _rules.Count;
            Remove(id);
            var rule = new Rule
            {
                Id = id, Event = s.Str("event", "hit"), Target = s.Str("target", "any"),
                Seconds = Mathf.Max(s.Num("seconds", 5f), 0.2f), Radius = Mathf.Max(s.Num("radius", 3f), 0.3f),
                Once = s.Flag("once"), Do = s.ObjList("do"),
            };
            rule.Next = Time.time + rule.Seconds;
            _rules.Add(rule);
            SessionLog.Write("rule", "id", id, "event", rule.Event, "target", rule.Target, "actions", rule.Do.Count);
            if (rule.Event == "start") { Run(rule, Nearest(rule.Target)); rule.Spent = true; }
        }

        public bool Remove(string id) => _rules.RemoveAll(r => r.Id == id) > 0;

        public void Clear(bool keepScore = false)
        {
            _rules.Clear();
            _pending.Clear();
            if (!keepScore) Score = 0;
        }

        static bool Matches(Rule rule, in Subject subject)
        {
            string t = rule.Target;
            if (string.IsNullOrEmpty(t) || t == "any") return true;
            if (t == subject.Group || t == subject.Asset) return true;
            if (subject.Group != null && subject.Group.StartsWith(t + ".", System.StringComparison.Ordinal)) return true;
            // "zombies" for zombie, "sheep" for a made "fluffy_sheep"
            AssetDef named = Library.FindAsset(t);
            return named != null && named.Id == subject.Asset || subject.Asset != null && subject.Asset.Contains(t);
        }

        public static Subject Of(Creature c) => new Subject { Creature = c, Asset = c.Def.Id, Group = c.GroupId, Position = c.transform.position + Vector3.up * (c.Height * 0.6f) };
        public static Subject Of(Prop p) => new Subject { Prop = p, Asset = p.Def.Id, Group = p.GroupId, Position = p.transform.position };

        /// <summary>
        /// Something happened to something. Runs every rule about it, and says whether one of them
        /// took the thing over (removed, respawned, exploded or launched it), in which case whatever
        /// would ordinarily have happened to it should not.
        /// </summary>
        public bool Fire(string ev, in Subject subject)
        {
            bool took = false;
            for (int i = 0; i < _rules.Count; i++)
            {
                Rule rule = _rules[i];
                if (rule.Spent || rule.Event != ev || !Matches(rule, subject)) continue;
                took |= Run(rule, subject);
                if (rule.Once) rule.Spent = true;
            }
            return took;
        }

        bool Run(Rule rule, in Subject subject)
        {
            bool took = false;
            SessionLog.Write("rule_fired", "id", rule.Id, "event", rule.Event, "asset", subject.Asset, "group", subject.Group, "at", subject.Position);
            foreach (Dictionary<string, object> action in rule.Do)
            {
                Statement a = Statement.Wrap(action);
                string act = a.Str("act", "");
                took |= act == "remove" || act == "respawn" || act == "explode" || act == "launch";
                float delay = a.Num("delay", 0f);
                if (delay > 0.01f) _pending.Add(new Pending { At = Time.time + delay, Action = action, Subject = subject });
                else Act(a, subject);
            }
            return took;
        }

        public void Tick(float dt)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pending[i].At) continue;
                Pending p = _pending[i];
                _pending.RemoveAt(i);
                Act(Statement.Wrap(p.Action), p.Subject);
            }
            if (_rules.Count == 0) return;
            Vector3 user = _world.Head != null ? _world.Head.position : Vector3.zero;
            for (int i = 0; i < _rules.Count; i++)
            {
                Rule rule = _rules[i];
                if (rule.Spent) continue;
                if (rule.Event == "every" && Time.time >= rule.Next)
                {
                    rule.Next = Time.time + rule.Seconds;
                    Run(rule, Nearest(rule.Target));
                    if (rule.Once) rule.Spent = true;
                }
                else if (rule.Event == "near" && Time.frameCount % 6 == i % 6)
                {
                    Subject nearest = Nearest(rule.Target, out bool found);
                    Vector3 to = nearest.Position - user;
                    to.y = 0f;
                    bool inside = found && to.magnitude < rule.Radius;
                    if (inside && !rule.Inside)
                    {
                        Run(rule, nearest);
                        if (rule.Once) rule.Spent = true;
                    }
                    rule.Inside = inside;
                }
            }
        }

        Subject Nearest(string target) => Nearest(target, out _);

        /// <summary>The matching thing nearest the user; with none, a spot a few metres in front of them.</summary>
        Subject Nearest(string target, out bool found)
        {
            Transform head = _world.Head;
            Vector3 user = head != null ? head.position : Vector3.zero;
            var probe = new Rule { Target = target };
            Subject best = default;
            float bestD = float.MaxValue;
            found = false;
            void Offer(in Subject s)
            {
                if (!Matches(probe, s)) return;
                Vector3 to = s.Position - user;
                to.y = 0f;
                if (to.sqrMagnitude < bestD) { bestD = to.sqrMagnitude; best = s; }
            }
            if (target != "any" && !string.IsNullOrEmpty(target))
            {
                foreach (Creature c in Creature.All) if (c != null && c.Up) Offer(Of(c));
                foreach (Prop p in _world.Props.All) if (p != null && p.GroupId != "shot") Offer(Of(p));
                foreach (Group g in _world.Objects.Groups)
                    foreach (Placement p in g.Items)
                    {
                        var at = new Vector3(p.Position.x, _world.Terrain.HeightAt(p.Position.x, p.Position.z) + p.Position.y, p.Position.z);
                        Offer(new Subject { Asset = g.Asset, Group = g.Id, Position = at });
                    }
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

        // ------------------------------------------------------------------ actions

        void Act(Statement a, in Subject subject)
        {
            Vector3 at = subject.Object != null ? (subject.Creature != null ? Of(subject.Creature).Position : subject.Object.transform.position) : subject.Position;
            switch (a.Str("act", ""))
            {
                case "burst": Burst(a.Str("kind", "confetti"), at, a.TryColor("color", out Color bc) ? bc : (Color?)null); break;
                case "sound": if (a.Str("name", "pop") != "none") Sfx.Play(a.Str("name", "pop"), at, 0.9f); break;
                case "remove":
                    if (subject.Creature != null) Object.Destroy(subject.Creature.gameObject);
                    else if (subject.Prop != null) _world.Props.Destroy(subject.Prop);
                    else if (subject.Group != null) _world.Objects.Remove(subject.Group, 0.4f);
                    break;
                case "respawn":
                    if (subject.Creature != null) subject.Creature.Vanish();
                    else if (subject.Prop != null) _world.Props.Destroy(subject.Prop);
                    break;
                case "spawn": Spawn(a, at); break;
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
                case "explode": Explode(at, Mathf.Clamp(a.Num("force", 12f), 0f, 40f), a.TryColor("color", out Color ec) ? ec : new Color(1f, 0.6f, 0.2f), subject); break;
                case "launch":
                    if (subject.Prop != null && subject.Prop.Body != null && !subject.Prop.Body.isKinematic)
                        subject.Prop.Body.AddForce(Vector3.up * Mathf.Clamp(a.Num("force", 8f), 0f, 40f) + Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
                    else if (subject.Creature != null) subject.Creature.Knock(Random.insideUnitSphere);
                    break;
                case "score":
                    Score += Mathf.RoundToInt(a.Num("add", 1f));
                    Sfx.Play("score", at, 0.8f, Mathf.Clamp(1f + Score * 0.01f, 1f, 1.6f));
                    _world.Emit("score " + Score);
                    break;
                case "say":
                    if (!string.IsNullOrEmpty(a.Str("text"))) _world.Emit(a.Str("text"));
                    break;
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

        void Explode(Vector3 at, float force, Color color, in Subject source)
        {
            Fx.Burst(at, color, new Color(1f, 0.95f, 0.6f), 46, 7f, 0.16f, 0.7f, 0.4f, true);
            Burst("smoke", at);
            Sfx.Play("boom", at, 1f, Random.Range(0.9f, 1.1f));
            const float radius = 6f;
            foreach (Prop p in _world.Props.All)
            {
                if (p == null || p.Body == null || p.Held || p.Body.isKinematic) continue;
                if ((p.transform.position - at).magnitude < radius) p.Body.AddExplosionForce(force, at, radius, 0.6f, ForceMode.VelocityChange);
            }
            foreach (Creature c in new List<Creature>(Creature.All))
            {
                if (c == null || c == source.Creature) continue;
                Vector3 away = c.transform.position - at;
                if (away.magnitude < radius) c.Knock(away);
            }
            if (source.Creature != null) source.Creature.Vanish();
            else if (source.Prop != null) _world.Props.Destroy(source.Prop);
            ImagineApp app = ImagineApp.Instance;
            if (app != null && app.Rig != null && _world.Head != null && (_world.Head.position - at).magnitude < 25f)
            {
                app.Rig.Haptic(true, 1f, 0.18f);
                app.Rig.Haptic(false, 1f, 0.18f);
            }
        }

        /// <summary>New things where it happened. They take their turn in a ring of ids, so a rule that spawns for ever does not fill the world.</summary>
        void Spawn(Statement a, Vector3 at)
        {
            int count = Mathf.Clamp(a.Int("count", 1), 1, 8);
            float ground = _world.Terrain.HeightAt(at.x, at.z);
            for (int i = 0; i < count; i++)
            {
                Vector2 off = count > 1 ? Random.insideUnitCircle * 1.2f : Vector2.zero;
                bool loose = a.Flag("physics");
                Statement s = Statement.Make("spawn", "id", "rule_spawn_" + (_spawned++ % 24), "asset", a.Str("asset", "cube"),
                    "at", new List<object> { (double)(at.x + off.x), (double)(loose ? Mathf.Max(at.y - ground, 0.2f) : 0f), (double)(at.z + off.y) });
                if (a.Has("scale")) s.Fields["scale"] = (double)a.Num("scale", 1f);
                if (a.Has("behavior")) s.Fields["behavior"] = a.Str("behavior");
                if (a.Has("tint")) s.Fields["tint"] = a.Str("tint");
                if (loose) s.Fields["physics"] = true;
                _world.Objects.Spawn(s);
            }
        }
    }
}
