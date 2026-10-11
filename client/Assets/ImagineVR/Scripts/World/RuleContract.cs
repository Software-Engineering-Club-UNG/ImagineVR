using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ImagineVR
{
    // Defense in depth for demo scripts and streamed data. Server validation is not trusted blindly.
    public static class RuleContract
    {
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static bool Id(object v) => v is string s && Regex.IsMatch(s, "^[a-zA-Z0-9_.-]{1,80}$");
        static bool Num(object v, double lo, double hi) => v is double d && !double.IsNaN(d) && !double.IsInfinity(d) && d >= lo && d <= hi;
        static bool Vector(object v) => Vector(v, 120);
        static bool Vector(object v, double bound) => v is List<object> list && list.Count == 3 && list.All(n => Num(n, -bound, bound));
        static void Fields(Dictionary<string, object> data, IEnumerable<string> allowed, params string[] required)
        {
            Require(data != null && !data.Keys.Except(allowed).Any() && required.All(data.ContainsKey), "Unknown or missing rule fields");
        }
        public static void Selector(Dictionary<string, object> data)
        {
            Fields(data, new[] { "instance", "group", "asset", "tag" });
            Require(data.Count == 1 && Id(data.Values.First()), "Invalid selector");
        }
        public static void Validate(Statement rule, Dictionary<string, object> contract, Dictionary<string,object> blueprintContract=null)
        {
            if (rule.Op == "rule_remove") { Fields(rule.Fields, new[] { "op", "id" }, "op", "id"); Require(Id(rule.Fields["id"]), "Invalid rule id"); return; }
            Fields(rule.Fields, new[] { "op", "id", "selector", "event", "filters", "conditions", "actions", "mode", "enabled", "cooldown", "once" }, "op", "id", "selector", "event", "actions");
            Require(Id(rule.Fields["id"]) && rule.Str("op") == "rule", "Invalid rule id/op");
            Selector(rule.Obj("selector"));
            Require(((List<object>)contract["events"]).Contains(rule.Str("event")), "Unknown event");
            Require(new[] { "replace", "add" }.Contains(rule.Str("mode", "replace")), "Invalid mode");
            foreach (string key in new[] { "once", "enabled" }) if (rule.Has(key)) Require(rule.Fields[key] is bool, "Invalid boolean");
            if (rule.Has("cooldown")) Require(Num(rule.Fields["cooldown"], 0, 60), "Invalid cooldown");
            if (rule.Has("filters"))
            {
                var filters = rule.Obj("filters"); Fields(filters, new[] { "source_kind", "source_asset", "source_tag", "min_speed", "radius", "threshold", "name", "duration", "part", "line_of_sight" });
                foreach (var field in filters)
                {
                    if (field.Key == "source_kind") Require(new[] { "projectile", "thrown", "melee", "collision" }.Contains(field.Value as string), "Invalid source kind");
                    else if (new[] { "radius", "min_speed", "threshold", "duration" }.Contains(field.Key)) Require(Num(field.Value, field.Key == "duration" ? .25 : 0, field.Key == "duration" ? 60 : 120), "Invalid event parameter");
                    else if(field.Key=="line_of_sight") Require(field.Value is bool,"Invalid line_of_sight");
                    else Require(Id(field.Value), "Invalid filter identifier");
                }
            }
            if (new[] { "timer", "interval" }.Contains(rule.Str("event"))) Require(rule.Obj("filters")?.ContainsKey("duration") == true, "Timer duration required");
            if (rule.Str("event").StartsWith("zone_") || rule.Str("event").StartsWith("proximity_")) Require(rule.Obj("filters")?.ContainsKey("radius") == true, "Spatial radius required");
            if(rule.Str("event")=="part_contact") Require(rule.Obj("filters")?.ContainsKey("part")==true,"Part filter required");
            if (rule.Has("conditions"))
            {
                var conditions = rule.Fields["conditions"] as List<object>; Require(conditions != null && conditions.Count <= 8, "Condition limit");
                foreach (object item in conditions)
                {
                    var condition = item as Dictionary<string, object>; Fields(condition, new[] { "field", "operator", "value", "name" }, "field", "operator", "value");
                    string field = condition["field"] as string;
                    Require(new[] { "health", "durability", "speed", "source_kind", "source_asset", "source_tag", "counter", "state", "actor_state", "chance" }.Contains(field), "Unknown condition");
                    Require(new[] { "eq", "ne", "lt", "le", "gt", "ge" }.Contains(condition["operator"] as string), "Unknown comparison");
                    if (field == "counter" || field == "state") Require(condition.TryGetValue("name", out object name) && Id(name), "Condition name required");
                    object value = condition["value"];
                    if (new[] { "source_kind", "source_asset", "source_tag", "state", "actor_state" }.Contains(field)) Require(value is bool || value is string s && s.Length <= 80, "Invalid condition value");
                    else Require(Num(value, field == "chance" ? 0 : -100000, field == "chance" ? 1 : 100000), "Invalid condition number");
                }
            }
            var actions = rule.Fields["actions"] as List<object>; Require(actions != null && actions.Count > 0 && actions.Count <= 8, "Action limit");
            double delay = 0;
            foreach (object item in actions)
            {
                var action = item as Dictionary<string, object>;
                Require(action != null && action.TryGetValue("action", out object kindObject) && kindObject is string, "Invalid action");
                string kind = (string)action["action"];
                if(blueprintContract!=null && ((Dictionary<string,object>)blueprintContract["actions"]).ContainsKey(kind))
                { BlueprintContract.ExtensionAction(action,blueprintContract); if(action.TryGetValue("delay",out object wait)) delay+=(double)wait; continue; }
                var schemas = (Dictionary<string, object>)contract["actions"];
                Require(schemas.ContainsKey(kind), "Unknown action");
                var schema = (Dictionary<string, object>)schemas[kind];
                Fields(action, new[] { "action", "target", "delay" }.Concat(((List<object>)schema["required"]).Cast<string>()).Concat(((List<object>)schema["optional"]).Cast<string>()), ((List<object>)schema["required"]).Cast<string>().ToArray());
                foreach (var field in action)
                {
                    string key = field.Key; object v = field.Value;
                    if (key == "action") continue;
                    if (key == "target") { if (v is Dictionary<string, object> selector) Selector(selector); else Require(new[] { "self", "source", "world", "player" }.Contains(v as string), "Invalid target"); }
                    else if (new[] { "position", "offset", "rotation", "vector" }.Contains(key)) Require(Vector(v, key == "rotation" ? 720 : 120), "Invalid vector");
                    else if (key == "path") { Require(v is List<object> list && list.Count >= 2 && list.Count <= 24 && list.All(Vector), "Invalid path"); }
                    else if (key == "tags") Require(v is List<object> tags && tags.Count <= 8 && tags.All(Id), "Invalid tags");
                    else if (key == "enabled" || key == "repeat" || key == "loop" && kind == "follow_path") Require(v is bool, "Invalid boolean");
                    else if (key == "color") Require(v is string color && Regex.IsMatch(color, "^#[a-fA-F0-9]{6}$"), "Invalid color");
                    else if (key == "text") Require(v is string text && text.Length > 0 && text.Length <= 200, "Invalid message");
                    else if (key == "value" && kind == "state_set") Require(v is bool || v is string state && state.Length <= 80, "Invalid state");
                    else if (new[] { "delay", "duration", "value", "count", "volume", "pitch", "size", "speed", "strength", "intensity", "density", "scale" }.Contains(key))
                    {
                        double lo = 0, hi = 60;
                        switch (key)
                        {
                            case "value": lo = kind == "scale" || kind == "resize_player" ? .05 : kind == "set_gravity" ? -.5 : kind.StartsWith("counter_") ? -100000 : 0; hi = kind == "scale" || kind == "resize_player" ? 20 : kind == "set_gravity" ? 2 : kind.StartsWith("counter_") ? 100000 : 10000; break;
                            case "count": lo = 1; hi = kind == "particles" ? 200 : 20; break;
                            case "volume": case "strength": case "density": hi = 1; break;
                            case "pitch": lo = .5; hi = 2; break;
                            case "size": lo = .01; hi = 3; break;
                            case "speed": lo = .1; hi = 40; break;
                            case "intensity": hi = 2; break;
                            case "scale": lo = .05; hi = 20; break;
                        }
                        Require(Num(v, lo, hi), "Number outside rule bounds");
                        if (key == "delay") delay += (double)v;
                    }
                    else Require(Id(v), "Invalid action identifier");
                }
                if (kind == "particles") Require(((List<object>)contract["particles"]).Contains(action["preset"]), "Unknown particle preset");
                if (action.TryGetValue("behavior", out object behavior)) Require(new[] { "chase", "wander", "flee", "still" }.Contains(behavior as string), "Unknown behavior");
                if (kind == "timer_start") Require((double)action["duration"] >= .25, "Timer too fast");
            }
            Require(delay <= 60, "Combined delay limit");
        }
    }
}
