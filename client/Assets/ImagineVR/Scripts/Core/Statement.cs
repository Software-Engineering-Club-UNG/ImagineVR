using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// One line of ImagineScript: an operation name and its fields.
    /// Unknown fields are simply never read, which is what makes the language forward compatible.
    /// </summary>
    public sealed class Statement
    {
        public string Op;
        public Dictionary<string, object> Fields;

        static readonly HashSet<string> StreamOps = new HashSet<string> { "transcript", "status", "note", "error", "done" };
        static readonly HashSet<string> WorldOps = new HashSet<string>
        {
            "say", "clear", "sky", "light", "fog", "terrain", "weather", "physics",
            "kit", "spawn", "scatter", "give", "ride", "goal", "player", "modify", "remove", "ambience",
        };

        public bool IsStream => StreamOps.Contains(Op);
        public bool IsWorld => WorldOps.Contains(Op);

        public static bool TryParse(string line, out Statement statement)
        {
            statement = null;
            if (string.IsNullOrWhiteSpace(line)) return false;
            string text = line.Trim();
            if (text.Length < 2 || text[0] != '{') return false;
            var fields = MiniJson.ParseObject(text);
            if (fields == null || !fields.TryGetValue("op", out object op) || !(op is string name) || name.Length == 0)
                return false;
            statement = new Statement { Op = name.ToLowerInvariant(), Fields = fields };
            return true;
        }

        public static Statement Make(string op, params object[] keyValues)
        {
            var fields = new Dictionary<string, object> { ["op"] = op };
            for (int i = 0; i + 1 < keyValues.Length; i += 2) fields[(string)keyValues[i]] = keyValues[i + 1];
            return new Statement { Op = op, Fields = fields };
        }

        public bool Has(string key) => Fields.TryGetValue(key, out object v) && v != null;

        public string Str(string key, string fallback = null)
        {
            if (Fields.TryGetValue(key, out object v) && v != null)
            {
                if (v is string s) return s;
                if (v is double d) return d.ToString(CultureInfo.InvariantCulture);
            }
            return fallback;
        }

        public float Num(string key, float fallback = 0f)
        {
            if (Fields.TryGetValue(key, out object v)) return ToFloat(v, fallback);
            return fallback;
        }

        public int Int(string key, int fallback = 0) => Mathf.RoundToInt(Num(key, fallback));

        public bool Flag(string key, bool fallback = false)
        {
            if (Fields.TryGetValue(key, out object v))
            {
                if (v is bool b) return b;
                if (v is string s) return s.ToLowerInvariant() == "true";
            }
            return fallback;
        }

        /// <summary>Transition time in seconds.</summary>
        public float T(float fallback = 1.5f, float longest = 6f) => Mathf.Clamp(Num("t", fallback), 0.2f, longest);

        /// <summary>Reads a list of [x, y, z] points. False unless there are at least two.</summary>
        public bool TryPath(string key, out List<Vector3> points)
        {
            points = new List<Vector3>();
            if (!Fields.TryGetValue(key, out object v) || !(v is List<object> list)) return false;
            foreach (object item in list)
            {
                if (item is List<object> p && p.Count == 3) points.Add(new Vector3(ToFloat(p[0], 0), ToFloat(p[1], 0), ToFloat(p[2], 0)));
            }
            return points.Count >= 2;
        }

        public bool TryColor(string key, out Color color)
        {
            color = Color.white;
            return Fields.TryGetValue(key, out object v) && v is string s && TryParseColor(s, out color);
        }

        public bool TryVec3(string key, out Vector3 value)
        {
            value = Vector3.zero;
            if (Fields.TryGetValue(key, out object v) && v is List<object> list && list.Count == 3)
            {
                value = new Vector3(ToFloat(list[0], 0), ToFloat(list[1], 0), ToFloat(list[2], 0));
                return true;
            }
            return false;
        }

        /// <summary>Reads either a single number or a [min, max] pair.</summary>
        public bool TryRange(string key, out float min, out float max)
        {
            min = max = 0f;
            if (!Fields.TryGetValue(key, out object v) || v == null) return false;
            if (v is List<object> list && list.Count == 2)
            {
                min = ToFloat(list[0], 0);
                max = ToFloat(list[1], 0);
                if (min > max) { float swap = min; min = max; max = swap; }
                return true;
            }
            if (v is double d)
            {
                min = max = (float)d;
                return true;
            }
            return false;
        }

        /// <summary>Reads a scale that is either one number or [x, y, z].</summary>
        public Vector3 Scale(string key, float fallback = 1f)
        {
            if (TryVec3(key, out Vector3 vec)) return vec;
            float s = Num(key, fallback);
            return new Vector3(s, s, s);
        }

        public List<string> StrList(string key)
        {
            var result = new List<string>();
            if (Fields.TryGetValue(key, out object v))
            {
                if (v is List<object> list)
                {
                    foreach (object item in list) if (item is string s) result.Add(s.ToLowerInvariant());
                }
                else if (v is string single) result.Add(single.ToLowerInvariant());
            }
            return result;
        }

        public Dictionary<string, object> Obj(string key)
        {
            return Fields.TryGetValue(key, out object v) ? v as Dictionary<string, object> : null;
        }

        static float ToFloat(object v, float fallback)
        {
            if (v is double d) return (float)d;
            if (v is string s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return f;
            return fallback;
        }

        public static bool TryParseColor(string text, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(text)) return false;
            string hex = text.Trim().TrimStart('#');
            if (hex.Length == 3) hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
            if (hex.Length != 6) return false;
            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
            color = new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
            return true;
        }

        // ------------------------------------------------------------------ terminal text

        static readonly string[] Shown =
        {
            "preset", "type", "material", "kit", "asset", "loop", "count", "area", "place", "distance",
            "density", "size", "intensity", "gravity", "stars", "clouds", "moon", "aurora", "nebula",
            "sun_elevation", "height", "scale", "tint", "color", "motion", "physics", "keep", "id",
        };

        /// <summary>How the statement reads as a tool call in the terminal, e.g. "sky(preset: golden_hour)".</summary>
        public string ToToolCall(int maxLength = 58)
        {
            var sb = new StringBuilder();
            sb.Append(Op).Append('(');
            bool first = true;
            string lead = LeadField();
            if (lead != null)
            {
                string leadText = Str(lead, "");
                if (Op == "scatter" && Has("count")) leadText += " × " + Int("count");
                sb.Append(leadText);
                first = false;
            }
            foreach (string key in Shown)
            {
                if (key == lead || !Has(key)) continue;
                if (Op == "scatter" && key == "count") continue;
                if (key == "id" && Op != "modify" && Op != "remove") continue;
                string value = FormatValue(Fields[key]);
                if (value == null) continue;
                string part = (key == "id" ? "" : key + ": ") + value;
                if (sb.Length + part.Length + 3 > maxLength) { sb.Append(first ? "" : ", ").Append('…'); break; }
                if (!first) sb.Append(", ");
                sb.Append(part);
                first = false;
            }
            sb.Append(')');
            return sb.ToString();
        }

        string LeadField()
        {
            switch (Op)
            {
                case "sky": return Has("preset") ? "preset" : null;
                case "terrain": return Has("type") ? "type" : null;
                case "weather": return "type";
                case "kit": return "kit";
                case "spawn":
                case "scatter":
                case "give": return "asset";
                case "ambience": return "loop";
                default: return null;
            }
        }

        static string FormatValue(object v)
        {
            switch (v)
            {
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case double d: return System.Math.Round(d, 2).ToString(CultureInfo.InvariantCulture);
                case List<object> list:
                    var parts = new List<string>();
                    foreach (object item in list) parts.Add(FormatValue(item) ?? "?");
                    return "[" + string.Join(",", parts) + "]";
                default: return null;
            }
        }
    }

    public static class Ease
    {
        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        public static float OutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }
        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
