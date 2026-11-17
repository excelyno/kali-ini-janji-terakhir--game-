using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tandur.Core;

namespace Tandur.Vn
{
    public sealed class VnSyntaxError : Exception
    {
        public VnSyntaxError(string msg) : base(msg) { }
    }

    /// <summary>Satu klausa kondisi: [not] nama [op nilai].</summary>
    public sealed record Clause(bool Negated, string Name, string? Op, JsonNode? Value);

    /// <summary>Kondisi = OR dari grup-grup AND.</summary>
    public sealed class Condition
    {
        private readonly List<List<Clause>> groups;
        private Condition(List<List<Clause>> g) => groups = g;

        private static readonly Regex ClauseRx = new(@"^(not\s+)?(\w+)(?:\s*(==|!=|>=|<=|>|<)\s*(.+))?$", RegexOptions.Compiled);

        public static Condition Parse(string expr)
        {
            var groups = new List<List<Clause>>();
            foreach (var orPart in Regex.Split(expr.Trim(), @"\s+or\s+"))
            {
                var clauses = new List<Clause>();
                foreach (var andPart in Regex.Split(orPart, @"\s+and\s+"))
                {
                    var m = ClauseRx.Match(andPart.Trim());
                    if (!m.Success) throw new VnSyntaxError($"kondisi salah: {expr}");
                    clauses.Add(new Clause(m.Groups[1].Success, m.Groups[2].Value, m.Groups[3].Success ? m.Groups[3].Value : null,
                                           m.Groups[4].Success ? VnScript.ParseValue(m.Groups[4].Value) : null));
                }
                groups.Add(clauses);
            }
            return new Condition(groups);
        }

        public bool Evaluate(GameState state) => groups.Any(g => g.All(c => ClauseTrue(c, state)));

        private static bool ClauseTrue(Clause c, GameState state)
        {
            var cur = state.Get(c.Name);
            bool result;
            if (c.Op == null) result = GameState.Truthy(cur);
            else
            {
                var cmp = Compare(cur ?? JsonValue.Create(false), c.Value);
                result = c.Op switch
                {
                    "==" => cmp == 0,
                    "!=" => cmp != 0,
                    ">=" => cmp is >= 0 and not int.MinValue,
                    "<=" => cmp is <= 0 and not int.MinValue,
                    ">" => cmp is > 0 and not int.MinValue,
                    "<" => cmp is < 0 and not int.MinValue,
                    _ => false,
                };
                if (cmp == int.MinValue) result = c.Op == "!=";
            }
            return result != c.Negated;
        }

        /// <summary>Perbandingan gaya Python. int.MinValue = tipe tak sebanding.</summary>
        private static int Compare(JsonNode a, JsonNode? b)
        {
            if (b == null) return int.MinValue;
            double? na = Num(a), nb = Num(b);
            if (na != null && nb != null) return na.Value.CompareTo(nb.Value);
            if (a is JsonValue va && b is JsonValue vb && va.TryGetValue(out string? sa) && vb.TryGetValue(out string? sb))
                return string.CompareOrdinal(sa, sb);
            return int.MinValue;
        }

        private static double? Num(JsonNode n)
        {
            if (n is not JsonValue v) return null;
            if (v.TryGetValue(out bool b)) return b ? 1 : 0;
            if (v.TryGetValue(out double d)) return d;
            if (v.TryGetValue(out int i)) return i;
            if (v.TryGetValue(out long l)) return l;
            return null;
        }
    }

    public sealed record MenuOption(string Text, string Label, Condition? Cond);

    /// <summary>
    /// ABSTRAKSI node skrip VN. Tiap perintah (.vn) menjadi subclass; runtime cukup memanggil
    /// Execute() (POLIMORFISME) dan node memutuskan apakah eksekusi berhenti (say, menu, wait, end).
    /// </summary>
    public abstract class VnNode
    {
        protected VnNode(int line) => Line = line;
        public int Line { get; }
        /// <summary>Jalankan node. true = berhenti dan tunggu pemain/waktu.</summary>
        public abstract bool Execute(VnRuntime rt);
        public virtual IEnumerable<string> Targets => Array.Empty<string>();
    }

    public sealed class SayNode : VnNode
    {
        public SayNode(int line, string? speaker, string? expr, string text) : base(line) { Speaker = speaker; Expr = expr; Text = text; }
        public string? Speaker { get; }
        public string? Expr { get; }
        public string Text { get; }
        public override bool Execute(VnRuntime rt) { rt.Say(Speaker, Expr, rt.Interpolate(Text)); return true; }
    }

    public sealed class ShowNode : VnNode
    {
        private readonly string ch, slot, expr;
        public ShowNode(int line, string ch, string slot, string expr) : base(line) { this.ch = ch; this.slot = slot; this.expr = expr; }
        public override bool Execute(VnRuntime rt) { rt.Actors[ch] = new VnActor(slot, expr); return false; }
    }

    public sealed class HideNode : VnNode
    {
        private readonly string ch;
        public HideNode(int line, string ch) : base(line) => this.ch = ch;
        public override bool Execute(VnRuntime rt) { rt.Actors.Remove(ch); return false; }
    }

    public sealed class BgNode : VnNode
    {
        private readonly string? name;
        public BgNode(int line, string? name) : base(line) => this.name = name;
        public override bool Execute(VnRuntime rt) { rt.Bg = name; return false; }
    }

    public sealed class GotoNode : VnNode
    {
        private readonly string label;
        public GotoNode(int line, string label) : base(line) => this.label = label;
        public override IEnumerable<string> Targets => new[] { label };
        public override bool Execute(VnRuntime rt) { rt.Jump(label); return false; }
    }

    public sealed class SetNode : VnNode
    {
        private readonly string name, op;
        private readonly JsonNode? value;
        public SetNode(int line, string name, string op, JsonNode? value) : base(line) { this.name = name; this.op = op; this.value = value; }

        public override bool Execute(VnRuntime rt)
        {
            var s = rt.State;
            if (op == "=") s.Set(name, value?.DeepClone());
            else
            {
                double cur = s.GetDouble(name), v = value is JsonValue jv && jv.TryGetValue(out double d) ? d : 0;
                double r = op == "+=" ? cur + v : cur - v;
                s.Set(name, r == Math.Floor(r) ? JsonValue.Create((int)r) : JsonValue.Create(r));
            }
            return false;
        }
    }

    public sealed class IfNode : VnNode
    {
        private readonly Condition cond;
        private readonly string label;
        public IfNode(int line, Condition cond, string label) : base(line) { this.cond = cond; this.label = label; }
        public override IEnumerable<string> Targets => new[] { label };
        public override bool Execute(VnRuntime rt) { if (cond.Evaluate(rt.State)) rt.Jump(label); return false; }
    }

    public sealed class MenuNode : VnNode
    {
        private readonly List<MenuOption> options;
        public MenuNode(int line, List<MenuOption> options) : base(line) => this.options = options;
        public override IEnumerable<string> Targets => options.Select(o => o.Label);

        public override bool Execute(VnRuntime rt)
        {
            var shown = options.Where(o => o.Cond == null || o.Cond.Evaluate(rt.State)).Select(o => new VnChoice(rt.Interpolate(o.Text), o.Label)).ToList();
            if (shown.Count == 0) return false;
            rt.Offer(shown);
            return true;
        }
    }

    public sealed class EmitNode : VnNode
    {
        private readonly string name;
        private readonly string[] args;
        public EmitNode(int line, string name, string[] args) : base(line) { this.name = name; this.args = args; }
        public override bool Execute(VnRuntime rt) { rt.Emit(name, args); return false; }
    }

    public sealed class WaitNode : VnNode
    {
        private readonly float seconds;
        public WaitNode(int line, float seconds) : base(line) => this.seconds = seconds;
        public override bool Execute(VnRuntime rt) { rt.Wait(seconds); return true; }
    }

    public sealed class EndNode : VnNode
    {
        public EndNode(int line) : base(line) { }
        public override bool Execute(VnRuntime rt) { rt.Finish(); return true; }
    }

    public sealed class RunNode : VnNode
    {
        private readonly string script, label;
        public RunNode(int line, string script, string label) : base(line) { this.script = script; this.label = label; }
        public override bool Execute(VnRuntime rt) { rt.RunScript(script, label); return false; }
    }

    /// <summary>Hasil parse satu file .vn.</summary>
    public sealed class VnScript
    {
        private static readonly HashSet<string> Keywords = new() { "label", "show", "hide", "bg", "goto", "set", "if", "menu", "emit", "wait", "end", "run" };
        private static readonly Regex SayRx = new(@"^([A-Za-z_]\w*)?(?:\((\w+)\))?\s*:\s*(.*)$", RegexOptions.Compiled);
        private static readonly Regex SetRx = new(@"^(\w+)\s*(=|\+=|-=)\s*(.+)$", RegexOptions.Compiled);

        private VnScript(string name, List<VnNode> nodes, Dictionary<string, int> labels) { Name = name; Nodes = nodes; Labels = labels; }

        public string Name { get; }
        public List<VnNode> Nodes { get; }
        public Dictionary<string, int> Labels { get; }

        public static JsonNode? ParseValue(string token)
        {
            token = token.Trim();
            string low = token.ToLowerInvariant();
            if (low == "true") return JsonValue.Create(true);
            if (low == "false") return JsonValue.Create(false);
            if (token.Length >= 2 && token[0] == token[^1] && (token[0] == '"' || token[0] == '\'')) return JsonValue.Create(token[1..^1]);
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return JsonValue.Create(i);
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return JsonValue.Create(d);
            return JsonValue.Create(token);
        }

        /// <summary>Pemecah argumen sederhana gaya shlex (menghormati tanda kutip).</summary>
        private static List<string> Split(string s)
        {
            var outList = new List<string>();
            var cur = new System.Text.StringBuilder();
            char quote = '\0';
            bool any = false;
            foreach (char c in s)
            {
                if (quote != '\0') { if (c == quote) quote = '\0'; else cur.Append(c); continue; }
                if (c is '"' or '\'') { quote = c; any = true; continue; }
                if (char.IsWhiteSpace(c)) { if (cur.Length > 0 || any) { outList.Add(cur.ToString()); cur.Clear(); any = false; } continue; }
                cur.Append(c);
            }
            if (quote != '\0') throw new VnSyntaxError("tanda kutip tidak ditutup");
            if (cur.Length > 0 || any) outList.Add(cur.ToString());
            return outList;
        }

        public static VnScript Parse(string text, string name = "<script>")
        {
            var nodes = new List<VnNode>();
            var labels = new Dictionary<string, int>();
            var lines = text.Replace("\r", "").Split('\n');
            int i = 0;
            VnSyntaxError Err(int ln, string msg) => new($"{name}:{ln}: {msg}");
            while (i < lines.Length)
            {
                int ln = i + 1;
                string line = lines[i].Trim();
                i++;
                if (line.Length == 0 || line.StartsWith('#')) continue;
                int sp = line.IndexOf(' ');
                string head = sp < 0 ? line : line[..sp];
                string rest = sp < 0 ? "" : line[(sp + 1)..].Trim();
                if (Keywords.Contains(head))
                {
                    switch (head)
                    {
                        case "label":
                            if (rest.Length == 0) throw Err(ln, "label butuh nama");
                            if (labels.ContainsKey(rest)) throw Err(ln, $"label ganda {rest}");
                            labels[rest] = nodes.Count;
                            break;
                        case "show":
                        {
                            var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 0) throw Err(ln, "show butuh id karakter");
                            var kv = parts.Skip(1).Where(p => p.Contains('=')).ToDictionary(p => p[..p.IndexOf('=')], p => p[(p.IndexOf('=') + 1)..]);
                            nodes.Add(new ShowNode(ln, parts[0], kv.GetValueOrDefault("at", "center"), kv.GetValueOrDefault("expr", "neutral")));
                            break;
                        }
                        case "hide": nodes.Add(new HideNode(ln, rest)); break;
                        case "bg": nodes.Add(new BgNode(ln, rest is "none" or "" ? null : rest)); break;
                        case "goto": nodes.Add(new GotoNode(ln, rest)); break;
                        case "set":
                        {
                            var m = SetRx.Match(rest);
                            if (!m.Success) throw Err(ln, "harusnya: set nama = nilai");
                            nodes.Add(new SetNode(ln, m.Groups[1].Value, m.Groups[2].Value, ParseValue(m.Groups[3].Value)));
                            break;
                        }
                        case "if":
                        {
                            int g = rest.LastIndexOf(" goto ", StringComparison.Ordinal);
                            if (g < 0) throw Err(ln, "harusnya: if <kondisi> goto <label>");
                            nodes.Add(new IfNode(ln, Condition.Parse(rest[..g]), rest[(g + 6)..].Trim()));
                            break;
                        }
                        case "menu":
                        {
                            var opts = new List<MenuOption>();
                            while (i < lines.Length)
                            {
                                string nxt = lines[i].Trim();
                                if (nxt.Length == 0 || nxt.StartsWith('#')) { i++; continue; }
                                if (!nxt.StartsWith('*')) break;
                                int optLine = i + 1;
                                i++;
                                string body = nxt[1..].Trim();
                                int arrow = body.LastIndexOf(" -> ", StringComparison.Ordinal);
                                if (arrow < 0) throw Err(optLine, "opsi menu: * teks -> label [if kondisi]");
                                string textPart = body[..arrow].Trim(), target = body[(arrow + 4)..];
                                Condition? cond = null;
                                string label = target.Trim();
                                int ifAt = target.IndexOf(" if ", StringComparison.Ordinal);
                                if (ifAt >= 0) { label = target[..ifAt].Trim(); cond = Condition.Parse(target[(ifAt + 4)..]); }
                                opts.Add(new MenuOption(textPart, label, cond));
                            }
                            if (opts.Count == 0) throw Err(ln, "menu tanpa opsi");
                            nodes.Add(new MenuNode(ln, opts));
                            break;
                        }
                        case "emit":
                        {
                            var tokens = Split(rest);
                            if (tokens.Count == 0) throw Err(ln, "emit butuh nama event");
                            nodes.Add(new EmitNode(ln, tokens[0], tokens.Skip(1).ToArray()));
                            break;
                        }
                        case "wait":
                            if (!float.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out float sec)) throw Err(ln, "wait butuh detik");
                            nodes.Add(new WaitNode(ln, sec));
                            break;
                        case "end": nodes.Add(new EndNode(ln)); break;
                        case "run":
                        {
                            var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 0) throw Err(ln, "run butuh nama skrip");
                            nodes.Add(new RunNode(ln, parts[0], parts.Length > 1 ? parts[1] : "start"));
                            break;
                        }
                    }
                    continue;
                }
                var say = SayRx.Match(line);
                if (!say.Success) throw Err(ln, $"tidak dimengerti: {line}");
                string? speaker = say.Groups[1].Success && say.Groups[1].Value.Length > 0 ? say.Groups[1].Value : null;
                if (speaker == "narrator") speaker = null;
                nodes.Add(new SayNode(ln, speaker, say.Groups[2].Success ? say.Groups[2].Value : null, say.Groups[3].Value.Replace("\\n", "\n")));
            }
            var script = new VnScript(name, nodes, labels);
            foreach (var n in nodes)
                foreach (var t in n.Targets)
                    if (!labels.ContainsKey(t)) throw new VnSyntaxError($"{name}:{n.Line}: label tak dikenal {t}");
            return script;
        }
    }

    public sealed class VnActor
    {
        public VnActor(string slot, string expr) { Slot = slot; Expr = expr; }
        public string Slot { get; }
        public string Expr { get; set; }
    }

    public sealed record VnChoice(string Text, string Label);

    public enum VnMode { Idle, Say, Choice, Wait, Done }

    /// <summary>Menjalankan skrip hasil parse. Logika murni: tanpa grafis, tanpa input.</summary>
    public sealed class VnRuntime
    {
        private const int MaxSteps = 5000;
        private static readonly Regex Interp = new(@"\{(\w+)\}", RegexOptions.Compiled);
        private readonly Func<string, VnScript>? loader;
        private readonly Action<string, string[]> emit;
        private int pc;
        private float timer;

        public VnRuntime(VnScript script, GameState state, Func<string, VnScript>? loader = null, Action<string, string[]>? emit = null)
        {
            Script = script; State = state; this.loader = loader; this.emit = emit ?? ((_, _) => { });
        }

        public VnScript Script { get; private set; }
        public GameState State { get; }
        public VnMode Mode { get; private set; } = VnMode.Idle;
        public string? Speaker { get; private set; }
        public string? Expr { get; private set; }
        public string Text { get; private set; } = "";
        public int LineId { get; private set; }
        public List<VnChoice> Choices { get; private set; } = new();
        public Dictionary<string, VnActor> Actors { get; } = new();
        public string? Bg { get; set; }
        public List<(string? Speaker, string Text)> History { get; } = new();
        public bool Finished => Mode == VnMode.Done;

        public void Start(string label = "start")
        {
            if (label != "start" && !Script.Labels.ContainsKey(label)) throw new KeyNotFoundException($"{Script.Name}: label {label}");
            pc = Script.Labels.GetValueOrDefault(label, 0);
            Run();
        }

        public void Advance() { if (Mode == VnMode.Say) Run(); }

        public void Choose(int index)
        {
            if (Mode != VnMode.Choice || index < 0 || index >= Choices.Count) return;
            pc = Script.Labels[Choices[index].Label];
            Run();
        }

        public void Update(float dt)
        {
            if (Mode != VnMode.Wait) return;
            timer -= dt;
            if (timer <= 0) Run();
        }

        public string Interpolate(string text) => Interp.Replace(text, m => State.Has(m.Groups[1].Value) ? State.GetString(m.Groups[1].Value) : m.Value);

        // --- dipanggil oleh node ---
        internal void Say(string? speaker, string? expr, string text)
        {
            Speaker = speaker; Expr = expr; Text = text;
            if (speaker != null && expr != null && Actors.TryGetValue(speaker, out var a)) a.Expr = expr;
            LineId++;
            History.Add((speaker, text));
            Mode = VnMode.Say;
        }

        internal void Jump(string label) => pc = Script.Labels[label];
        internal void Offer(List<VnChoice> c) { Choices = c; Mode = VnMode.Choice; }
        internal void Emit(string name, string[] args) => emit(name, args);
        internal void Wait(float s) { timer = s; Mode = VnMode.Wait; }
        internal void Finish() => Mode = VnMode.Done;

        internal void RunScript(string name, string label)
        {
            if (loader == null) throw new InvalidOperationException("`run` butuh loader skrip");
            Script = loader(name);
            pc = Script.Labels.GetValueOrDefault(label, 0);
        }

        private void Run()
        {
            for (int step = 0; step < MaxSteps; step++)
            {
                if (pc >= Script.Nodes.Count) { Mode = VnMode.Done; return; }
                var node = Script.Nodes[pc++];
                if (node.Execute(this)) return;
            }
            throw new InvalidOperationException($"{Script.Name}: lebih dari {MaxSteps} langkah tanpa say/menu");
        }
    }

    public sealed record VnCharacter(string Id, string Name, Engine.Rgba Color, string Portrait);

    /// <summary>Memuat dan menyimpan cache skrip serta daftar karakter dari assets/vn/.</summary>
    public sealed class VnLibrary
    {
        private readonly string root;
        private readonly Dictionary<string, VnScript> cache = new();

        public VnLibrary(string root)
        {
            this.root = root;
            foreach (var (id, d) in JsonNode.Parse(File.ReadAllText(Path.Combine(root, "characters.json")))!.AsObject())
            {
                string hex = ((string?)d!["color"] ?? "#d9c48a").TrimStart('#');
                var col = new Engine.Rgba(Convert.ToInt32(hex[..2], 16) / 255f, Convert.ToInt32(hex[2..4], 16) / 255f, Convert.ToInt32(hex[4..6], 16) / 255f);
                Characters[id] = new VnCharacter(id, (string)d["name"]!, col, (string?)d["portrait"] ?? $"portrait_{id}");
            }
        }

        public Dictionary<string, VnCharacter> Characters { get; } = new();

        public VnScript Load(string name)
        {
            if (!cache.TryGetValue(name, out var s))
            {
                string path = Path.Combine(root, "scripts", name + ".vn");
                if (!File.Exists(path)) throw new FileNotFoundException("Skrip VN tidak ada: " + path);
                cache[name] = s = VnScript.Parse(File.ReadAllText(path), name);
            }
            return s;
        }

        public IEnumerable<string> AllScriptNames() => Directory.GetFiles(Path.Combine(root, "scripts"), "*.vn").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n)!;
    }
}
