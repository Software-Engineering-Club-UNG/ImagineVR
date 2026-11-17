using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ImagineVR
{
    /// <summary>What an expression asks: counters by name, and the world through its functions.</summary>
    public interface IExprHost
    {
        /// <summary>A counter's value: a number (double) or text (string). Unknown counters are 0.</summary>
        object Var(string name);
        /// <summary>count, alive, standing, tipped, moving, dist, inside, holding, riding, speed, height, health. Arguments are names of things.</summary>
        double World(string function, List<string> args);
    }

    /// <summary>
    /// The small arithmetic a rule may ask about the world: `standing(pins) == 0 and throws &lt; 2`.
    /// This is the same grammar as openrouter-api/imagine/expr.py, held to the same answers by
    /// shared/expr-cases.json. The server has already checked and tidied every expression; this
    /// one still never throws: what it cannot read is false.
    /// </summary>
    public sealed class Expr
    {
        enum Kind { Num, Text, Var, Call, Neg, Not, Bin }

        sealed class Node
        {
            public Kind Kind;
            public double Number;
            public string Name;      // var name, function name, text, or the binary operator
            public Node A, B;
            public List<Node> Args;

            public object Eval(IExprHost host)
            {
                switch (Kind)
                {
                    case Kind.Num: return Number;
                    case Kind.Text: return Name;
                    case Kind.Var:
                        if (Name == "time" && host == null) return 0.0;
                        object v = host != null ? host.Var(Name) : null;
                        return v is string ? v : (object)ToNumber(v);
                    case Kind.Call: return Call(this, host);
                    case Kind.Neg: return -ToNumber(A.Eval(host));
                    case Kind.Not: return Truthy(A.Eval(host)) ? 0.0 : 1.0;
                    case Kind.Bin:
                        if (Name == "and") return Truthy(A.Eval(host)) && Truthy(B.Eval(host)) ? 1.0 : 0.0;
                        if (Name == "or") return Truthy(A.Eval(host)) || Truthy(B.Eval(host)) ? 1.0 : 0.0;
                        return Binary(Name, A.Eval(host), B.Eval(host));
                }
                return 0.0;
            }
        }

        static readonly Dictionary<string, int[]> Functions = new Dictionary<string, int[]>
        {
            ["min"] = new[] { 2, 4 }, ["max"] = new[] { 2, 4 }, ["abs"] = new[] { 1, 1 }, ["floor"] = new[] { 1, 1 }, ["ceil"] = new[] { 1, 1 },
            ["round"] = new[] { 1, 1 }, ["sqrt"] = new[] { 1, 1 }, ["clamp"] = new[] { 3, 3 }, ["rand"] = new[] { 0, 2 },
            ["count"] = new[] { 1, 1 }, ["standing"] = new[] { 1, 1 }, ["tipped"] = new[] { 1, 1 }, ["alive"] = new[] { 1, 1 }, ["dist"] = new[] { 2, 2 },
            ["inside"] = new[] { 1, 2 }, ["holding"] = new[] { 0, 1 }, ["riding"] = new[] { 0, 1 }, ["speed"] = new[] { 0, 1 }, ["height"] = new[] { 0, 1 },
            ["health"] = new[] { 0, 1 }, ["moving"] = new[] { 1, 1 },
        };
        static readonly HashSet<string> WorldFunctions = new HashSet<string> { "count", "standing", "tipped", "alive", "dist", "inside", "holding", "riding", "speed", "height", "health", "moving" };

        static readonly Dictionary<string, Expr> Cache = new Dictionary<string, Expr>();

        readonly Node _root;
        public readonly string Source;
        public bool Valid => _root != null;

        Expr(string source, Node root) { Source = source; _root = root; }

        /// <summary>The expression for this text, parsed once. Never null: an unreadable one is not Valid and is false.</summary>
        public static Expr Of(string text)
        {
            text = text ?? "";
            if (Cache.TryGetValue(text, out Expr known)) return known;
            Node root = null;
            try
            {
                var p = new Parser(Tokenize(text));
                root = p.Expression();
                if (p.Peek().Type != "end") root = null;
            }
            catch (FormatException) { root = null; }
            if (Cache.Count > 512) Cache.Clear();
            return Cache[text] = new Expr(text, root);
        }

        /// <summary>Reads a value written as a number or as an expression in a statement (`by`, `to`).</summary>
        public static double Value(object written, IExprHost host, double fallback = 0)
        {
            if (written is double d) return d;
            if (written is bool b) return b ? 1 : 0;
            if (written is string s) { Expr e = Of(s); return e.Valid ? ToNumber(e._root.Eval(host)) : fallback; }
            return fallback;
        }

        public double Number(IExprHost host) => Valid ? ToNumber(_root.Eval(host)) : 0;
        public bool Truth(IExprHost host) => Valid && Truthy(_root.Eval(host));

        // ------------------------------------------------------------------ reading

        struct Token { public string Type, Text; }

        static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            int i = 0;
            text = text.Trim();
            if (text.Length == 0 || text.Length > 240) throw new FormatException("empty or too long");
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (char.IsDigit(c) || (c == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
                {
                    int start = i;
                    while (i < text.Length && char.IsDigit(text[i])) i++;
                    if (i < text.Length && text[i] == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])) { i++; while (i < text.Length && char.IsDigit(text[i])) i++; }
                    tokens.Add(new Token { Type = "number", Text = text.Substring(start, i - start) });
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_' || text[i] == '.')) i++;
                    string word = text.Substring(start, i - start).ToLowerInvariant();
                    switch (word)
                    {
                        case "and": case "or": case "not": tokens.Add(new Token { Type = "op", Text = word }); break;
                        case "true": case "yes": tokens.Add(new Token { Type = "number", Text = "1" }); break;
                        case "false": case "no": tokens.Add(new Token { Type = "number", Text = "0" }); break;
                        default: tokens.Add(new Token { Type = "name", Text = word }); break;
                    }
                    continue;
                }
                if (c == '\'' || c == '"')
                {
                    int end = text.IndexOf(c, i + 1);
                    if (end < 0) throw new FormatException("unclosed text");
                    tokens.Add(new Token { Type = "text", Text = text.Substring(i + 1, end - i - 1).Trim().ToLowerInvariant() });
                    i = end + 1;
                    continue;
                }
                string two = i + 1 < text.Length ? text.Substring(i, 2) : "";
                if (two == "<=" || two == ">=" || two == "==" || two == "!=" || two == "&&" || two == "||")
                {
                    tokens.Add(new Token { Type = "op", Text = two == "&&" ? "and" : two == "||" ? "or" : two });
                    i += 2;
                    continue;
                }
                if ("-+*/%<>()=,!".IndexOf(c) >= 0)
                {
                    tokens.Add(new Token { Type = "op", Text = c == '=' ? "==" : c == '!' ? "not" : c.ToString() });
                    i++;
                    continue;
                }
                throw new FormatException("cannot read " + c);
            }
            return tokens;
        }

        sealed class Parser
        {
            readonly List<Token> _t;
            int _at, _depth;
            public Parser(List<Token> tokens) { _t = tokens; }

            public Token Peek() => _at < _t.Count ? _t[_at] : new Token { Type = "end", Text = "" };
            bool IsOp(params string[] ops)
            {
                Token k = Peek();
                if (k.Type != "op") return false;
                foreach (string o in ops) if (k.Text == o) return true;
                return false;
            }
            Token Take(string op = null)
            {
                Token k = Peek();
                if (op != null && !(k.Type == "op" && k.Text == op)) throw new FormatException("expected " + op);
                _at++;
                return k;
            }
            static Node Bin(string op, Node a, Node b) => new Node { Kind = Kind.Bin, Name = op, A = a, B = b };

            public Node Expression()
            {
                if (++_depth > 24) throw new FormatException("too deep");
                Node left = And();
                while (IsOp("or")) { Take(); left = Bin("or", left, And()); }
                _depth--;
                return left;
            }
            Node And()
            {
                Node left = Not();
                while (IsOp("and")) { Take(); left = Bin("and", left, Not()); }
                return left;
            }
            Node Not()
            {
                if (IsOp("not")) { Take(); return new Node { Kind = Kind.Not, A = Not() }; }
                return Compare();
            }
            Node Compare()
            {
                Node left = Sum();
                if (IsOp("<", "<=", ">", ">=", "==", "!=")) { string op = Take().Text; return Bin(op, left, Sum()); }
                return left;
            }
            Node Sum()
            {
                Node left = Term();
                while (IsOp("+", "-")) { string op = Take().Text; left = Bin(op, left, Term()); }
                return left;
            }
            Node Term()
            {
                Node left = Unary();
                while (IsOp("*", "/", "%")) { string op = Take().Text; left = Bin(op, left, Unary()); }
                return left;
            }
            Node Unary()
            {
                if (IsOp("-")) { Take(); return new Node { Kind = Kind.Neg, A = Unary() }; }
                return Atom();
            }
            Node Atom()
            {
                Token k = Peek();
                if (k.Type == "number") { Take(); return new Node { Kind = Kind.Num, Number = double.Parse(k.Text, CultureInfo.InvariantCulture) }; }
                if (k.Type == "text") { Take(); return new Node { Kind = Kind.Text, Name = k.Text }; }
                if (k.Type == "name")
                {
                    Take();
                    if (!IsOp("(")) return new Node { Kind = Kind.Var, Name = k.Text };
                    if (!Functions.TryGetValue(k.Text, out int[] arity)) throw new FormatException("no function " + k.Text);
                    Take();
                    var args = new List<Node>();
                    if (!IsOp(")"))
                    {
                        args.Add(Expression());
                        while (IsOp(",")) { Take(); args.Add(Expression()); }
                    }
                    Take(")");
                    if (args.Count < arity[0] || args.Count > arity[1]) throw new FormatException("wrong arguments");
                    return new Node { Kind = Kind.Call, Name = k.Text, Args = args };
                }
                if (IsOp("(")) { Take(); Node inner = Expression(); Take(")"); return inner; }
                throw new FormatException("expected a value");
            }
        }

        // ------------------------------------------------------------------ working it out

        static double ToNumber(object v)
        {
            if (v is double d) return double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
            if (v is string s) return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0;
            return 0;
        }

        static bool Truthy(object v) => v is string s ? s.Length > 0 : Math.Abs(ToNumber(v)) > 1e-9;

        static string Spelt(double v) => v == Math.Floor(v) && Math.Abs(v) < 1e15 ? ((long)v).ToString(CultureInfo.InvariantCulture) : v.ToString("R", CultureInfo.InvariantCulture);

        static double Call(Node n, IExprHost host)
        {
            if (WorldFunctions.Contains(n.Name))
            {
                // the arguments name things: a bare word is the name itself, not a counter's value
                var names = new List<string>(n.Args.Count);
                foreach (Node a in n.Args) names.Add(a.Kind == Kind.Var || a.Kind == Kind.Text ? a.Name : Spelt(ToNumber(a.Eval(host))));
                return host != null ? host.World(n.Name, names) : 0;
            }
            var v = new List<double>(n.Args.Count);
            foreach (Node a in n.Args) v.Add(ToNumber(a.Eval(host)));
            switch (n.Name)
            {
                case "min": { double m = v[0]; foreach (double x in v) m = Math.Min(m, x); return m; }
                case "max": { double m = v[0]; foreach (double x in v) m = Math.Max(m, x); return m; }
                case "abs": return Math.Abs(v[0]);
                case "floor": return Math.Floor(v[0]);
                case "ceil": return Math.Ceiling(v[0]);
                case "round": return Math.Floor(v[0] + 0.5);
                case "sqrt": return Math.Sqrt(Math.Max(v[0], 0));
                case "clamp": return Math.Min(Math.Max(v[0], v[1]), v[2]);
                case "rand":
                    double lo = v.Count == 2 ? v[0] : 0, hi = v.Count == 0 ? 1 : v.Count == 1 ? v[0] : v[1];
                    return lo + UnityEngine.Random.value * (hi - lo);
            }
            return 0;
        }

        static object Binary(string op, object left, object right)
        {
            if (op == "==" || op == "!=")
            {
                bool same = left is string || right is string
                    ? string.Equals(left is string ls ? ls : Spelt(ToNumber(left)), right is string rs ? rs : Spelt(ToNumber(right)), StringComparison.Ordinal)
                    : Math.Abs(ToNumber(left) - ToNumber(right)) < 1e-6;
                return same == (op == "==") ? 1.0 : 0.0;
            }
            double a = left is string ? 0 : ToNumber(left), b = right is string ? 0 : ToNumber(right);
            switch (op)
            {
                case "<": return a < b ? 1.0 : 0.0;
                case "<=": return a <= b ? 1.0 : 0.0;
                case ">": return a > b ? 1.0 : 0.0;
                case ">=": return a >= b ? 1.0 : 0.0;
                case "+": return a + b;
                case "-": return a - b;
                case "*": return a * b;
                case "/": return Math.Abs(b) > 1e-9 ? a / b : 0.0;
                case "%": return Math.Abs(b) > 1e-9 ? a % b : 0.0;     // C#'s % is fmod, as Python's math.fmod
            }
            return 0.0;
        }

        /// <summary>Every counter name the expression reads (not names inside world functions).</summary>
        public HashSet<string> Names()
        {
            var found = new HashSet<string>();
            void Walk(Node n, bool inWorld)
            {
                if (n == null) return;
                switch (n.Kind)
                {
                    case Kind.Var: if (!inWorld) found.Add(n.Name); break;
                    case Kind.Call: foreach (Node a in n.Args) Walk(a, WorldFunctions.Contains(n.Name)); break;
                    case Kind.Neg: case Kind.Not: Walk(n.A, inWorld); break;
                    case Kind.Bin: Walk(n.A, inWorld); Walk(n.B, inWorld); break;
                }
            }
            Walk(_root, false);
            return found;
        }

        /// <summary>Fills `{name}` in a line of text with that counter's value, e.g. "Lap {lap}: {lap_time}s".</summary>
        public static string Fill(string text, IExprHost host)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0 || host == null) return text;
            var sb = new StringBuilder(text.Length + 16);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                if (open < 0) { sb.Append(text, i, text.Length - i); break; }
                int close = text.IndexOf('}', open + 1);
                if (close < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, open - i);
                string name = text.Substring(open + 1, close - open - 1).Trim().ToLowerInvariant();
                object v = host.Var(name);
                if (v is string s) sb.Append(s);
                else
                {
                    double d = ToNumber(v);
                    sb.Append(Math.Abs(d - Math.Round(d)) < 0.005 ? Spelt(Math.Round(d)) : d.ToString("0.0#", CultureInfo.InvariantCulture));
                }
                i = close + 1;
            }
            return sb.ToString();
        }
    }
}
