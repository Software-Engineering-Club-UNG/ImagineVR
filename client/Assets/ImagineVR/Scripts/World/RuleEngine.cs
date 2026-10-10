using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ImagineVR
{
    // A data-only interpreter. All callbacks are queued; model output can never become code.
    public sealed class RuleEngine
    {
        public const string Capability = "runtime-rules-v1";
        public const int MaxRules = 64, MaxInteractive = 64;
        readonly World _world;
        readonly Dictionary<string, RuleEntity> _entities = new Dictionary<string, RuleEntity>();
        readonly List<Statement> _rules = new List<Statement>();
        readonly List<Pending> _pending = new List<Pending>();
        readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        readonly HashSet<string> _once = new HashSet<string>();
        readonly Dictionary<string, float> _timers = new Dictionary<string, float>();
        readonly Dictionary<string, float> _intervals = new Dictionary<string, float>();
        readonly HashSet<string> _inside = new HashSet<string>();
        readonly Dictionary<string, float> _counters = new Dictionary<string, float>();
        readonly Dictionary<string, object> _states = new Dictionary<string, object>();
        readonly List<string> _failures = new List<string>();
        readonly Dictionary<string, object> _contract;
        int _next, _depth, _generation, _sequence, _interactive, _frameActions;
        readonly List<ParticleSystem> _effects = new List<ParticleSystem>();
        Coroutine _playerRoutine;
        GameObject _fade;
        bool _playerTransition;
        public bool PlayerEffectsEnabled;

        sealed class Pending
        {
            public string RuleId;
            public Statement Action;
            public RuleEntity Self, Source;
            public Vector3 Position;
            public float Due;
            public int Depth;
        }

        public RuleEngine(World world)
        {
            _world = world;
            TextAsset contract = Resources.Load<TextAsset>("RuntimeRules");
            _contract = contract != null ? MiniJson.ParseObject(contract.text) : null;
            if (_contract == null) throw new InvalidOperationException("Missing RuntimeRules contract");
        }
        public void Fail(string text)
        {
            text = text.Length > 200 ? text.Substring(0, 200) : text;
            if (_failures.Contains(text)) return;
            if (_failures.Count == 32) _failures.RemoveAt(0);
            _failures.Add(text); _world.Note(text);
        }
        public RuleEntity Find(GameObject go) => go != null && go.GetComponent<RuleBody>() != null ? go.GetComponent<RuleBody>().Entity : null;
        public void RegisterProp(Prop prop) => RegisterObject(prop.gameObject, prop.GroupId, prop.Def.Id, new HashSet<string>());
        public void Reset(bool keepRules = false)
        {
            if (_playerRoutine != null) _world.StopCoroutine(_playerRoutine);
            if (_fade != null) UnityEngine.Object.Destroy(_fade);
            _playerTransition = false; _playerRoutine = null;
            foreach (ParticleSystem effect in _effects) if (effect != null) UnityEngine.Object.Destroy(effect.gameObject);
            _effects.Clear();
            _pending.Clear(); _last.Clear(); _once.Clear(); _timers.Clear(); _intervals.Clear(); _inside.Clear();
            _counters.Clear(); _states.Clear(); _failures.Clear();
            if (!keepRules) _rules.Clear();
        }
        public void NewSession() { Reset(); PlayerEffectsEnabled = false; SetGeneration(0); }
        public void SetGeneration(int generation) { _generation = generation; _sequence = 0; }
        public void CreatureRespawned(RuleEntity entity)
        {
            _entities.Remove(entity.Id); entity.Id = "e" + (++_next); _entities[entity.Id] = entity;
            if (entity.Health >= 0) entity.Health = 100;
            Emit("respawn", entity);
        }
        static Statement Wrap(Dictionary<string, object> data) => new Statement { Fields = data, Op = data.TryGetValue("action", out object a) ? (string)a : "rule" };
        bool Matches(Dictionary<string, object> selector, RuleEntity e)
        {
            if (e == null || selector == null || selector.Count != 1) return false;
            var pair = selector.First(); string value = pair.Value as string;
            switch (pair.Key)
            {
                case "instance": return e.Id == value;
                case "asset": return e.Asset == value;
                case "group": return e.GroupId == value || e.GroupId.StartsWith(value + ".", StringComparison.Ordinal);
                case "tag": return e.Tags.Contains(value);
                default: return false;
            }
        }
        public void SyncEntities()
        {
            var present = new HashSet<string>();
            var placements = new Dictionary<Group, Dictionary<int, RuleEntity>>();
            foreach (RuleEntity entity in _entities.Values)
                if (entity.Group != null && entity.Index >= 0)
                {
                    if (!placements.TryGetValue(entity.Group, out Dictionary<int, RuleEntity> indexes)) { indexes = new Dictionary<int, RuleEntity>(); placements.Add(entity.Group, indexes); }
                    indexes[entity.Index] = entity;
                }
            foreach (Group group in _world.Objects.Groups)
            {
                for (int i = 0; i < group.Items.Count; i++)
                {
                    RuleEntity existing = placements.TryGetValue(group, out Dictionary<int, RuleEntity> byIndex) && byIndex.TryGetValue(i, out RuleEntity entry) ? entry : null;
                    if (existing != null) { present.Add(existing.Id); continue; }
                    Placement p = group.Items[i];
                    if (p.Mesh == null) continue;
                    if (_entities.Count >= 1920) { Fail("Entity tracking budget reached; additional scenery cannot receive rules"); continue; }
                    string asset = group.Source != null && group.Source.Has("asset") ? group.Source.Str("asset") : Library.Asset(p.Mesh.name) != null ? p.Mesh.name : "cube";
                    var e = new RuleEntity { Id = "e" + (++_next), GroupId = group.Id, Group = group, Index = i,
                        Asset = asset, World = _world, Original = p, BirthPosition = p.Position + Vector3.up * _world.Terrain.HeightAt(p.Position.x,p.Position.z), BirthRotation = p.Rotation, BirthScale = p.Scale };
                    if (group.Source != null) foreach (string tag in group.Source.StrList("tags")) e.Tags.Add(tag);
                    // Registry positions are absolute; placements use a terrain-relative y.
                    _entities.Add(e.Id, e); present.Add(e.Id); PrepareForRules(e); Emit("spawn", e);
                }
                foreach (GameObject extra in group.Extras)
                    if (extra != null && (extra.GetComponent<Creature>() != null || extra.GetComponent<Mover>() != null))
                        RegisterObject(extra, group.Id, extra.name.StartsWith("Creature_") ? extra.name.Substring(9) : extra.name.StartsWith("Mover_") ? extra.name.Substring(6) : "cube", present);
            }
            foreach (Prop prop in _world.Props.All)
                if (prop != null) RegisterObject(prop.gameObject, prop.GroupId, prop.Def.Id, present);
            foreach (RuleEntity e in _entities.Values.ToArray())
                if (!present.Contains(e.Id)) { e.Removed = true; Detach(e); }
            foreach (RuleEntity e in _entities.Values.Where(e => e.Removed).ToArray())
                if (!_pending.Any(p => p.Self == e || p.Source == e)) _entities.Remove(e.Id);
        }
        void RegisterObject(GameObject go, string group, string asset, HashSet<string> present)
        {
            RuleBody body = go.GetComponent<RuleBody>();
            if (body != null) { present.Add(body.Entity.Id); return; }
            // Projectile entities are transient and do not consume the interaction promotion budget.
            var e = new RuleEntity { Id = "e" + (++_next), GroupId = group, Asset = asset, Object = go, World = _world,
                BirthPosition = go.transform.position, BirthRotation = go.transform.rotation, BirthScale = go.transform.localScale };
            _entities.Add(e.Id, e); present.Add(e.Id);
            Group owner = _world.Objects.Get(group);
            if (owner?.Source != null) foreach (string tag in owner.Source.StrList("tags")) e.Tags.Add(tag);
            AssetDef def = Library.FindAsset(asset) ?? Library.Asset("cube");
            Color tint = go.GetComponent<Prop>() != null ? go.GetComponent<Prop>().Tint : owner != null ? owner.Tint : Color.white;
            e.Original = new Placement(def.Mesh, go.transform.position, go.transform.eulerAngles.y, go.transform.localScale.y, tint, false);
            body = go.AddComponent<RuleBody>(); body.Engine = this; body.Entity = e;
            PrepareForRules(e); Emit("spawn", e);
        }
        bool NeedsBody(Statement rule) => !new[] { "spawn", "remove", "timer", "interval", "counter_changed", "counter_reached", "state_changed" }.Contains(rule.Str("event"));
        bool NeedsProp(Statement rule) => new[] { "grab", "release", "throw", "place", "use_start", "use_end" }.Contains(rule.Str("event"));
        bool MakeGrabbable(RuleEntity e)
        {
            if (e.Object != null && e.Object.GetComponent<Prop>() != null) return true;
            if (_world.Props.All.Count(p => p != null && p.GroupId != "shot") >= PropSystem.MaxPhysics) { Fail("Grab rule exceeds physics budget"); return false; }
            if (!Promote(e)) return false;
            GameObject old = e.Object;
            AssetDef def = Library.FindAsset(e.Asset) ?? Library.Asset("cube");
            Prop prop = _world.Props.SpawnLoose(e.GroupId, def, e.Position, old.transform.rotation, e.BirthScale, e.Original.Tint, false);
            RuleBody body = prop.gameObject.AddComponent<RuleBody>(); body.Engine = this; body.Entity = e;
            e.Object = prop.gameObject; (e.Group ?? _world.Objects.Get(e.GroupId))?.Extras.Add(e.Object);
            UnityEngine.Object.Destroy(old); e.UpdatePlacement(); return true;
        }
        void PrepareForRules(RuleEntity e)
        {
            foreach (Statement rule in _rules)
                if (rule.Flag("enabled", true) && Matches(rule.Obj("selector"), e) && NeedsBody(rule))
                {
                    if (NeedsProp(rule) && !MakeGrabbable(e)) continue;
                    if (Promote(e) && new[] { "damage", "death", "health_below" }.Contains(rule.Str("event")) && e.Health < 0) e.Health = 100;
                    if (rule.Str("event") == "break" && e.Durability < 0) e.Durability = 100;
                }
        }
        bool Promote(RuleEntity e)
        {
            if (e.Object != null) return true;
            if (_entities.Values.Count(x => x.Alive && x.Object != null && x.GroupId != "shot") >= MaxInteractive) { Fail("Rule interaction limit reached; target was not promoted."); return false; }
            AssetDef def = Library.FindAsset(e.Asset) ?? Library.Asset("cube");
            GameObject go = new GameObject("Rule_" + e.Asset); go.transform.SetParent(_world.transform, false);
            go.transform.SetPositionAndRotation(e.Position, e.Original.Rotation); go.transform.localScale = e.Original.Scale;
            go.AddComponent<MeshFilter>().sharedMesh = e.Original.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = e.Original.Mesh.subMeshCount > 1
                ? new[] { _world.Objects.LitMaterial, _world.Objects.GlowMaterial } : new[] { _world.Objects.LitMaterial };
            BoxCollider collider = go.AddComponent<BoxCollider>(); collider.center = def.Bounds.center; collider.size = Vector3.Max(def.Bounds.size, Vector3.one * .05f);
            go.AddComponent<Rigidbody>().isKinematic = true;
            RuleBody body = go.AddComponent<RuleBody>(); body.Engine = this; body.Entity = e;
            e.Object = go; e.Group.Extras.Add(go); _interactive++; e.UpdatePlacement(); e.Tint(e.Original.Tint); return true;
        }
        public bool Install(Statement rule)
        {
            try
            {
                RuleContract.Validate(rule, _contract);
                string id = rule.Str("id");
                if (rule.Op == "rule_remove") { RemoveRule(id); return true; }
                if (string.IsNullOrEmpty(id) || rule.Obj("selector") == null || rule.Obj("selector").Count != 1 || !((List<object>)_contract["events"]).Contains(rule.Str("event")))
                    throw new InvalidOperationException("Invalid rule identity/event/selector");
                List<object> actions = rule.Fields.TryGetValue("actions", out object av) ? av as List<object> : null;
                if (actions == null || actions.Count == 0 || actions.Count > 8) throw new InvalidOperationException("Invalid rule actions");
                var schemas = (Dictionary<string, object>)_contract["actions"];
                float totalDelay = 0;
                foreach (object item in actions)
                {
                    var action = item as Dictionary<string, object>;
                    if (action == null || !action.TryGetValue("action", out object kind) || !schemas.ContainsKey((string)kind)) throw new InvalidOperationException("Unknown action");
                    var schema = (Dictionary<string, object>)schemas[(string)kind];
                    var allowed = new HashSet<string>(new[] { "action", "target", "delay" });
                    foreach (object key in (List<object>)schema["required"]) { allowed.Add((string)key); if (!action.ContainsKey((string)key)) throw new InvalidOperationException("Missing action field"); }
                    foreach (object key in (List<object>)schema["optional"]) allowed.Add((string)key);
                    if (action.Keys.Any(k => !allowed.Contains(k))) throw new InvalidOperationException("Unknown action field");
                    totalDelay += Wrap(action).Num("delay", 0);
                }
                if (totalDelay > 60 || totalDelay < 0) throw new InvalidOperationException("Action delay limit");
                int index = _rules.FindIndex(r => r.Str("id") == id);
                if (index < 0 && _rules.Count >= MaxRules) throw new InvalidOperationException("Rule limit");
                if ((rule.Str("event") == "timer" || rule.Str("event") == "interval") && _rules.Count(r => r.Str("id") != id && (r.Str("event") == "timer" || r.Str("event") == "interval")) + _intervals.Count >= 32) throw new InvalidOperationException("Timer limit");
                if ((rule.Str("event").StartsWith("zone_") || rule.Str("event").StartsWith("proximity_")) && _rules.Count(r => r.Str("id") != id && (r.Str("event").StartsWith("zone_") || r.Str("event").StartsWith("proximity_"))) >= 32) throw new InvalidOperationException("Zone limit");
                RuleEntity[] targets = _entities.Values.Where(e => e.Alive && Matches(rule.Obj("selector"), e)).ToArray();
                if (NeedsBody(rule) && _entities.Values.Count(e => e.Alive && e.Object != null && e.GroupId != "shot") + targets.Count(e => e.Object == null) > MaxInteractive) throw new InvalidOperationException("Interaction budget exceeded; rule unchanged");
                if (NeedsProp(rule) && _world.Props.All.Count(p => p != null && p.GroupId != "shot") + targets.Count(e => e.Object == null || e.Object.GetComponent<Prop>() == null) > PropSystem.MaxPhysics) throw new InvalidOperationException("Grab rule exceeds physics budget; rule unchanged");
                if (NeedsBody(rule)) foreach (RuleEntity e in targets) Promote(e);
                _pending.RemoveAll(p => p.RuleId == id);
                _once.RemoveWhere(k => k.StartsWith(id + ":", StringComparison.Ordinal));
                foreach (string key in _last.Keys.Where(k => k.StartsWith(id + ":", StringComparison.Ordinal)).ToArray()) _last.Remove(key);
                if (index >= 0) _rules[index] = rule; else _rules.Add(rule);
                foreach (RuleEntity e in targets) PrepareForRules(e);
                _timers[id] = Time.time + Mathf.Max(.25f, rule.Obj("filters") != null ? Wrap(rule.Obj("filters")).Num("duration", 1) : 1);
                return true;
            }
            catch (Exception ex) { Fail("Rule rejected: " + ex.Message); return false; }
        }
        void RemoveRule(string id)
        {
            _rules.RemoveAll(r => r.Str("id") == id); _pending.RemoveAll(p => p.RuleId == id); _timers.Remove(id);
            _once.RemoveWhere(k => k.StartsWith(id + ":", StringComparison.Ordinal));
            foreach (string key in _last.Keys.Where(k => k.StartsWith(id + ":", StringComparison.Ordinal)).ToArray()) _last.Remove(key);
        }
        public void ForgetGroup(Group group)
        {
            foreach (RuleEntity e in _entities.Values.Where(e => e.Group == group || e.GroupId == group.Id).ToArray())
            { e.Removed = true; Detach(e); _entities.Remove(e.Id); _pending.RemoveAll(p => p.Self == e || p.Source == e); }
        }
        public void RemovingGroup(string id)
        {
            foreach (RuleEntity e in _entities.Values.Where(e => e.Alive && (e.GroupId == id || e.GroupId.StartsWith(id + ".", StringComparison.Ordinal))).ToArray()) Emit("remove", e);
        }
        bool Fits(Statement rule, string name, RuleEntity self, RuleEntity source, string kind, float speed, string eventName = null, float previous = float.NaN, float current = float.NaN)
        {
            if (!rule.Flag("enabled", true) || rule.Str("event") != name || !Matches(rule.Obj("selector"), self)) return false;
            var filters = rule.Obj("filters");
            if (filters != null)
            {
                Statement f = Wrap(filters);
                if (name == "health_below" && !(previous >= f.Num("threshold", 50) && current < f.Num("threshold", 50))) return false;
                if (name == "counter_reached" && !(previous < f.Num("threshold", 1) && current >= f.Num("threshold", 1))) return false;
                if (f.Has("source_kind") && f.Str("source_kind") != kind || f.Has("source_asset") && (source == null || source.Asset != f.Str("source_asset")) || f.Has("source_tag") && (source == null || !source.Tags.Contains(f.Str("source_tag"))) || speed < f.Num("min_speed", 0) || f.Has("name") && f.Str("name") != eventName) return false;
            }
            if (rule.Fields.TryGetValue("conditions", out object cv) && cv is List<object> conditions)
                foreach (object item in conditions)
                {
                    Statement c = Wrap((Dictionary<string, object>)item); string field = c.Str("field");
                    object value = field == "health" ? self.Health : field == "durability" ? self.Durability : field == "speed" ? speed : field == "source_kind" ? kind : field == "source_asset" ? source?.Asset : field == "source_tag" ? (source != null && source.Tags.Contains(c.Str("value")) ? c.Str("value") : "") : field == "counter" ? (_counters.TryGetValue(c.Str("name"), out float n) ? n : 0) : field == "state" ? (_states.TryGetValue(c.Str("name"), out object state) ? state : null) : UnityEngine.Random.value;
                    object want = c.Fields["value"]; string op = c.Str("operator");
                    if (value is string || value is bool || value == null) { bool same = Equals(value, want); if (!(op == "eq" && same || op == "ne" && !same)) return false; }
                    else { float a = Convert.ToSingle(value), b = Convert.ToSingle(want); if (!(op == "eq" && a == b || op == "ne" && a != b || op == "lt" && a < b || op == "le" && a <= b || op == "gt" && a > b || op == "ge" && a >= b)) return false; }
                }
            return true;
        }
        public bool Replaces(string name, RuleEntity self, RuleEntity source = null, string kind = "", float speed = 0)
            => self != null && _rules.Any(r => r.Str("mode", "replace") == "replace" && Fits(r, name, self, source, kind, speed));
        public bool Emit(string name, RuleEntity self, RuleEntity source = null, string kind = "", float speed = 0, string eventName = null, float previous = float.NaN, float current = float.NaN)
        {
            if (self == null || _depth >= 4) return false;
            bool replace = false;
            foreach (Statement rule in _rules.ToArray())
            {
                if (!Fits(rule, name, self, source, kind, speed, eventName, previous, current)) continue;
                replace |= rule.Str("mode", "replace") == "replace";
                string key = rule.Str("id") + ":" + self.Id;
                if (rule.Flag("once") && _once.Contains(key) || _last.TryGetValue(key, out float last) && Time.time - last < rule.Num("cooldown", .1f)) continue;
                var actions = (List<object>)rule.Fields["actions"];
                if (_pending.Count + actions.Count > 256) { Fail("Rule event queue limit reached"); continue; }
                SessionLog.Write("rule_event", "rule", rule.Str("id"), "event", name, "entity", self.Id, "asset", self.Asset, "source", kind);
                _last[key] = Time.time; _once.Add(key); float due = Time.time;
                foreach (object item in actions)
                {
                    Statement a = Wrap((Dictionary<string, object>)item); due += Mathf.Clamp(a.Num("delay", 0), 0, 60);
                    _pending.Add(new Pending { RuleId = rule.Str("id"), Action = a, Self = self, Source = source, Position = self.Position, Due = due, Depth = _depth + 1 });
                }
            }
            return replace;
        }
        public bool Damage(RuleEntity e, float amount, RuleEntity source = null)
        {
            if (e == null || !e.Alive) return false;
            bool replaced = false;
            if (e.Health >= 0)
            {
                float old = e.Health; e.Health = Mathf.Max(0, old - amount); Emit("damage", e, source);
                Emit("health_below", e, source, previous: old, current: e.Health);
                if (old > 0 && e.Health == 0) replaced |= Emit("death", e, source);
            }
            if (e.Durability >= 0) { float old = e.Durability; e.Durability = Mathf.Max(0, old - amount); if (old > 0 && e.Durability == 0) replaced |= Emit("break", e, source); }
            return replaced;
        }
        void Detach(RuleEntity e)
        {
            if (e.Object != null)
            {
                Prop prop = e.Object.GetComponent<Prop>(); if (prop != null && prop.Holder != null) prop.Holder.ForceRelease();
                if (e.Index >= 0) _interactive = Mathf.Max(0, _interactive - 1);
                UnityEngine.Object.Destroy(e.Object); e.Object = null;
            }
            e.UpdatePlacement();
        }
        void Remove(RuleEntity e)
        {
            if (!e.Alive) return;
            Emit("remove", e); e.Removed = true; Detach(e);
        }
        public void Tick(float dt)
        {
            _frameActions = 0;
            foreach (Statement rule in _rules.ToArray())
            {
                string name = rule.Str("event"), id = rule.Str("id");
                RuleEntity[] targets = _entities.Values.Where(e => e.Alive && Matches(rule.Obj("selector"), e)).ToArray();
                if ((name == "timer" || name == "interval") && _timers.TryGetValue(id, out float at) && Time.time >= at)
                {
                    foreach (RuleEntity e in targets) Emit(name, e);
                    if (name == "timer") _timers.Remove(id); else _timers[id] = Time.time + Mathf.Max(.25f, Wrap(rule.Obj("filters")).Num("duration", 1));
                }
                if (name == "zone_enter" || name == "zone_exit" || name == "proximity_enter" || name == "proximity_exit")
                {
                    Statement filters = Wrap(rule.Obj("filters"));
                    float radius = Mathf.Clamp(filters.Num("radius", 3), .1f, 120);
                    foreach (RuleEntity e in targets)
                    {
                        if (filters.Has("source_asset") || filters.Has("source_tag"))
                        {
                            foreach (RuleEntity actor in _entities.Values.Where(actor => actor.Alive && actor != e && (!filters.Has("source_asset") || actor.Asset == filters.Str("source_asset")) && (!filters.Has("source_tag") || actor.Tags.Contains(filters.Str("source_tag")))))
                                SpatialEvent(rule, e, actor, actor.Position, radius);
                        }
                        else if (_world.Head != null) SpatialEvent(rule, e, null, _world.Head.position, radius);
                    }
                }
            }
            foreach (string name in _intervals.Keys.ToArray())
                if (_timers.TryGetValue("named:" + name, out float at) && Time.time >= at)
                { Broadcast("timer", name); if (_intervals[name] > 0) _timers["named:" + name] = Time.time + _intervals[name]; else { _timers.Remove("named:" + name); _intervals.Remove(name); } }
            while (_frameActions < 128)
            {
                int index = _pending.FindIndex(p => p.Due <= Time.time); if (index < 0) break;
                Pending pending = _pending[index]; _pending.RemoveAt(index); _frameActions++; _depth = pending.Depth;
                try { Execute(pending); } catch (Exception ex) { Fail("Rule action failed: " + ex.Message); }
                _depth = 0;
            }
        }
        void SpatialEvent(Statement rule, RuleEntity anchor, RuleEntity actor, Vector3 position, float radius)
        {
            string name = rule.Str("event"), key = rule.Str("id") + ":" + anchor.Id + ":" + (actor != null ? actor.Id : "player");
            bool inside = Vector3.Distance(anchor.Position, position) <= radius;
            bool was = _inside.Contains(key); if (inside) _inside.Add(key); else _inside.Remove(key);
            if (inside != was && (inside ? name.EndsWith("enter") : name.EndsWith("exit"))) Emit(name, anchor, actor);
        }
        public void Broadcast(string eventType, string name, float previous = float.NaN, float current = float.NaN)
        {
            foreach (RuleEntity e in _entities.Values.Where(e => e.Alive).ToArray()) Emit(eventType, e, eventName: name, previous: previous, current: current);
        }
        IEnumerable<RuleEntity> Targets(Pending p)
        {
            var selector = p.Action.Obj("target");
            if (selector != null) return _entities.Values.Where(e => e.Alive && Matches(selector, e)).ToArray();
            return new[] { p.Action.Str("target", "self") == "source" ? p.Source : p.Self }.Where(e => e != null);
        }
        void Execute(Pending p)
        {
            Statement a = p.Action; string kind = a.Op; string name = a.Str("name", "score");
            SessionLog.Write("rule_action", "rule", p.RuleId, "action", kind, "entity", p.Self?.Id);
            float value = a.Num("value", 0);
            Color color = a.TryColor("color", out Color c) ? c : Color.white;
            if (kind == "rule_enable" || kind == "rule_disable")
            {
                Statement rule = _rules.Find(r => r.Str("id") == a.Str("id"));
                if (rule != null)
                {
                    rule.Fields["enabled"] = kind == "rule_enable";
                    if (kind == "rule_disable") _pending.RemoveAll(next => next.RuleId == rule.Str("id"));
                    else foreach (RuleEntity e in _entities.Values.Where(e => e.Alive).ToArray()) PrepareForRules(e);
                }
                return;
            }
            if (kind == "counter_add" || kind == "counter_set")
            {
                if (!_counters.ContainsKey(name) && _counters.Count >= 64) { Fail("Counter limit"); return; }
                float old = _counters.TryGetValue(name, out float n) ? n : 0;
                float next = Mathf.Clamp(kind == "counter_add" ? old + value : value, -100000, 100000); _counters[name] = next;
                if (old != next) Broadcast("counter_changed", name);
                Broadcast("counter_reached", name, old, next);
                return;
            }
            if (kind == "state_set") { if (!_states.ContainsKey(name) && _states.Count >= 64) { Fail("State limit"); return; } object v = a.Fields["value"]; if (!_states.TryGetValue(name, out object old) || !Equals(old, v)) { _states[name] = v; Broadcast("state_changed", name); } return; }
            if (kind == "goal_complete") { Broadcast("goal_reached", name); _world.Emit(name); return; }
            if (kind == "timer_start") { if (_intervals.Count >= 32 && !_intervals.ContainsKey(name)) { Fail("Timer limit"); return; } float duration = Mathf.Clamp(a.Num("duration", 1), .25f, 60); _timers["named:" + name] = Time.time + duration; _intervals[name] = a.Flag("repeat") ? duration : 0; return; }
            if (kind == "timer_cancel") { _timers.Remove("named:" + name); _intervals.Remove(name); return; }
            if (kind == "message") { _world.Emit(a.Str("text")); return; }
            if (new[] { "sky", "light", "fog", "weather", "ambience" }.Contains(kind)) { var fields = new Dictionary<string, object>(a.Fields); fields["op"] = kind; _world.Apply(new Statement { Op = kind, Fields = fields }); return; }
            if (kind == "haptic") { Rig rig = _world.Head != null ? _world.Head.GetComponentInParent<Rig>() : null; if (rig != null) { rig.Haptic(true, Mathf.Clamp01(a.Num("strength", .5f)), Mathf.Clamp(a.Num("duration", .1f), 0, 1)); rig.Haptic(false, Mathf.Clamp01(a.Num("strength", .5f)), .1f); } return; }
            if (new[] { "teleport_player", "resize_player", "mount", "dismount" }.Contains(kind)) { PlayerAction(a); return; }
            if (kind == "set_gravity" && a.Str("target") == "world") { _world.Props.SetGravity(Mathf.Clamp(value, -.5f, 2)); return; }
            if (kind == "spawn" && a.Str("target") == "world") { Spawn(a, p.Position); return; }
            foreach (RuleEntity e in Targets(p))
            {
                Vector3 position = e.Alive ? e.Position : p.Position;
                if (kind == "sound") { Sfx.Play(a.Str("name"), position, Mathf.Clamp01(a.Num("volume", .8f)), Mathf.Clamp(a.Num("pitch", 1), .5f, 2)); continue; }
                if (kind == "particles") { Particles(a, position + Vector3.up * .5f, color); continue; }
                if (kind == "respawn") { Respawn(e); continue; }
                if (!e.Alive) continue;
                switch (kind)
                {
                    case "tint": e.Tint(color); break;
                    case "scale": e.Resize(Mathf.Clamp(value, .05f, 20)); break;
                    case "visibility": e.Show(a.Flag("enabled")); break;
                    case "remove": Remove(e); break;
                    case "set_health": e.Health = Mathf.Clamp(value, 0, 10000); break;
                    case "set_breakable": e.Durability = Mathf.Clamp(value, 0, 10000); break;
                    case "damage": Damage(e, Mathf.Max(0, value), p.Source); break;
                    case "heal": if (e.Health >= 0) e.Health = Mathf.Min(10000, e.Health + Mathf.Max(0, value)); break;
                    case "kill": if (e.Health < 0) e.Health = 100; Damage(e, e.Health, p.Source); break;
                    case "break": if (e.Durability < 0) e.Durability = 100; float health = e.Health; e.Health = -1; Damage(e, e.Durability, p.Source); e.Health = health; break;
                    case "set_gravity": e.Gravity = Mathf.Clamp(value, -.5f, 2); break;
                    case "rotate": if (a.TryVec3("rotation", out Vector3 rotation)) { if (e.Object != null) e.Object.transform.rotation = Quaternion.Euler(rotation); else { e.Original.Rotation = Quaternion.Euler(rotation); e.UpdatePlacement(); } } break;
                    case "set_collision": if (Promote(e)) foreach (Collider collider in e.Object.GetComponents<Collider>()) collider.enabled = a.Flag("enabled"); break;
                    case "set_physics": if (Promote(e)) { Rigidbody body = e.Object.GetComponent<Rigidbody>(); body.isKinematic = !a.Flag("enabled"); } break;
                    case "impulse": case "set_velocity": if (Promote(e) && a.TryVec3("vector", out Vector3 vector)) { Rigidbody body = e.Object.GetComponent<Rigidbody>(); if (body.isKinematic) { Fail("Physics action requires a dynamic body"); break; } if (kind == "impulse") body.AddForce(Vector3.ClampMagnitude(vector, 40), ForceMode.Impulse); else body.linearVelocity = Vector3.ClampMagnitude(vector, 40); } break;
                    case "set_behavior": if (Promote(e))
                        {
                            Creature creature = e.Object.GetComponent<Creature>();
                            if (creature == null)
                            {
                                if (_entities.Values.Count(x => x.Alive && x.Object != null && x.Object.GetComponent<Creature>() != null) >= Creature.MaxLive) { Fail("Creature budget exceeded"); break; }
                                GameObject old = e.Object;
                                GameObject next = Creature.Create(_world, Library.FindAsset(e.Asset) ?? Library.Asset("cube"), e.Position, e.BirthScale.y, e.Original.Tint, a.Str("behavior"));
                                RuleBody body = next.AddComponent<RuleBody>(); body.Engine = this; body.Entity = e;
                                e.Object = next; e.Group?.Extras.Add(next); UnityEngine.Object.Destroy(old); creature = next.GetComponent<Creature>();
                            }
                            creature.SetBehavior(a.Str("behavior"));
                        } break;
                    case "stop_motion": if (e.Object != null) { Creature creature = e.Object.GetComponent<Creature>(); if (creature != null) creature.SetBehavior("still"); RuleMotion motion = e.Object.GetComponent<RuleMotion>(); if (motion != null) UnityEngine.Object.Destroy(motion); Rigidbody body = e.Object.GetComponent<Rigidbody>(); if (body != null && !body.isKinematic) body.linearVelocity = Vector3.zero; } break;
                    case "move_to": case "follow_path": if (Promote(e)) { Creature creature = e.Object.GetComponent<Creature>(); if (creature != null) creature.SetBehavior("still"); RuleMotion motion = e.Object.GetComponent<RuleMotion>() ?? e.Object.AddComponent<RuleMotion>(); motion.Init(this, e, a); } break;
                    case "spawn": Spawn(a, position); break;
                    default: Fail("Unsupported runtime action: " + kind); break;
                }
            }
        }
        void Spawn(Statement a, Vector3 position)
        {
            int count = Mathf.Clamp(a.Int("count", 1), 1, 20);
            if (_world.Objects.CountKind("spawn") + count > ObjectSystem.MaxSpawned) { Fail("Spawn action exceeds object budget"); return; }
            AssetDef def = Library.FindAsset(a.Str("asset")); if (def == null) { Fail("Unknown action asset"); return; }
            a.TryVec3("offset", out Vector3 offset);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = position + offset + Vector3.right * i;
                var stmt = Statement.Make("spawn", "asset", def.Id, "id", "rule_spawn_" + (++_next), "at", new List<object> { (double)at.x, (double)(at.y - _world.Terrain.HeightAt(at.x, at.z)), (double)at.z }, "scale", (double)a.Num("scale", 1), "tint", a.Str("color", "#FFFFFF"), "behavior", a.Str("behavior", "still"), "tags", a.StrList("tags").Cast<object>().ToList());
                _world.Apply(stmt);
            }
            SyncEntities();
        }
        void Respawn(RuleEntity e)
        {
            if (e.Alive) Remove(e);
            var action = Statement.Make("spawn", "asset", e.Asset, "scale", (double)e.BirthScale.y);
            Spawn(action, e.BirthPosition);
            RuleEntity fresh = _entities.Values.LastOrDefault(x => x.Alive && x.Asset == e.Asset);
            if (fresh != null) { fresh.GroupId = e.GroupId; fresh.Health = e.Health >= 0 ? 100 : -1; fresh.Durability = e.Durability >= 0 ? 100 : -1; Emit("respawn", fresh); }
        }
        void Particles(Statement a, Vector3 at, Color color)
        {
            _effects.RemoveAll(effect => effect == null);
            int count = Mathf.Clamp(a.Int("count", 50), 1, 200);
            if (_effects.Sum(effect => effect.main.maxParticles) + count > 1000) { Fail("Particle budget exceeded"); return; }
            float life = Mathf.Clamp(a.Num("duration", 1.5f), .1f, 10);
            string preset = a.Str("preset");
            float size = Mathf.Clamp(a.Num("size", preset == "smoke" ? .3f : .06f), .01f, 3);
            ParticleSystem effect = preset == "confetti" ? Fx.Confetti(at, count, life, size, a.Has("color") ? (Color?)color : null)
                : Fx.Burst(at, color, color, count, preset == "smoke" ? .8f : 3, size, life, preset == "splash" ? 1 : 0, preset == "sparks" || preset == "sparkles");
            effect.transform.SetParent(_world.transform, true); _effects.Add(effect);
        }
        void PlayerAction(Statement a)
        {
            if (!PlayerEffectsEnabled) { Fail("Player effects disabled; enable them locally for this session"); return; }
            if (_playerTransition) { Fail("Player transition already running"); return; }
            if (_world.Vehicle == null || _world.Head == null) return;
            _playerRoutine = _world.StartCoroutine(PlayerTransition(a));
        }
        IEnumerator PlayerTransition(Statement a)
        {
            _playerTransition = true;
            var go = new GameObject("RuleFade"); var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            _fade = go;
            go.transform.SetParent(_world.Head, false); go.transform.localPosition = Vector3.forward * .6f; go.transform.localScale = Vector3.one * .01f;
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1000, 1000);
            var child = new GameObject("Black", typeof(RectTransform), typeof(Image)); child.transform.SetParent(go.transform, false);
            var image = child.GetComponent<Image>(); image.rectTransform.sizeDelta = new Vector2(1000, 1000); image.color = Color.clear;
            for (float t = 0; t < .2f; t += Time.unscaledDeltaTime) { image.color = new Color(0,0,0,t/.2f); yield return null; }
            image.color = Color.black;
            if (a.Op == "teleport_player" && a.TryVec3("position", out Vector3 position))
            {
                if (position.magnitude > World.Radius || Physics.CheckCapsule(position + Vector3.up * .3f, position + Vector3.up * 1.5f, .2f)) Fail("Teleport destination is obstructed or outside the world");
                else { _world.Vehicle.Dismount(); Rig rig = _world.Head.GetComponentInParent<Rig>(); Vector3 correction = _world.Head.position - rig.transform.position; correction.y = 0; rig.transform.position = position - correction; }
            }
            else if (a.Op == "resize_player") _world.Vehicle.Resize(a.Num("value", 1));
            else if (a.Op == "mount") _world.Vehicle.Apply(Statement.Make("ride", "asset", a.Str("asset"), "speed", (double)Mathf.Clamp(a.Num("speed", 1), .2f, 3)));
            else if (a.Op == "dismount") _world.Vehicle.Dismount();
            for (float t = .2f; t > 0; t -= Time.unscaledDeltaTime) { image.color = new Color(0,0,0,t/.2f); yield return null; }
            UnityEngine.Object.Destroy(go); _playerTransition = false;
        }
        public Dictionary<string, object> Snapshot()
        {
            SyncEntities();
            var entities = new List<object>();
            foreach (RuleEntity e in _entities.Values.Where(e => e.Alive && e.GroupId != "shot"))
            {
                Vector3 p = e.Position;
                var item = new Dictionary<string, object> { ["id"] = e.Id, ["group"] = e.GroupId, ["asset"] = e.Asset, ["position"] = new List<object> { (double)p.x, (double)p.y, (double)p.z }, ["tags"] = e.Tags.Cast<object>().ToList() };
                if (e.Health >= 0) item["health"] = (double)e.Health;
                if (e.Durability >= 0) item["durability"] = (double)e.Durability;
                entities.Add(item);
            }
            Vector3 head = _world.Head != null ? _world.Head.position : Vector3.zero;
            return new Dictionary<string, object> { ["generation"] = _generation, ["sequence"] = ++_sequence, ["entities"] = entities,
                ["rules"] = _rules.Select(r => (object)r.Fields).ToList(), ["counters"] = _counters.ToDictionary(p => p.Key, p => (object)(double)p.Value),
                ["states"] = new Dictionary<string, object>(_states), ["player"] = new List<object> { (double)head.x,(double)head.y,(double)head.z }, ["failures"] = _failures.Cast<object>().ToList() };
        }
    }
}
