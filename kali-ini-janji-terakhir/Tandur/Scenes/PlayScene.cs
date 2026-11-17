using System.Text.Json.Nodes;
using Tandur.Core;
using Tandur.Engine;
using Tandur.Entities;
using Tandur.Minigames;
using Tandur.Ui;
using Tandur.World;

namespace Tandur.Scenes
{
    /// <summary>Kepulan debu langkah saat berlari (efek tambahan versi C#), ikut diurutkan kedalaman.</summary>
    public sealed class DustPuff : Actor
    {
        private readonly float wx, wy;

        public DustPuff(float wx, float wy, float gx, float gy)
        {
            this.wx = wx; this.wy = wy; GX = gx; GY = gy;
        }

        public float Age { get; set; }
        public override float Depth => GX + GY - 0.05f;

        public override void Draw(SpriteBatch b, Atlas a)
            => b.Sprite(a[$"dust_{Math.Min(3, (int)(Age / 0.09f))}"], MathF.Round(wx), MathF.Round(wy - Age * 10), new Rgba(1, 1, 1, Math.Clamp(1 - Age / 0.36f, 0, 1)));
    }

    /// <summary>PlayScene: ladang. Jalan, bertani, bicara, tidur, ulangi.</summary>
    public sealed class PlayScene : Scene
    {
        private const float MouseIdleSeconds = 3, HintSeconds = 30;
        private static readonly (float, float, float) Warm = (1f, 0.72f, 0.38f), Cool = (0.62f, 0.86f, 1f);
        private static readonly Dictionary<string, (float Dx, float Dy, float R, float I, (float, float, float) C)[]> LightSources = new()
        {
            ["house"] = new[] { (-14f, -18f, 64f, 0.85f, Warm), (12f, -10f, 52f, 0.7f, Warm) },
            ["house_blue"] = new[] { (-14f, -18f, 64f, 0.85f, Warm), (12f, -10f, 52f, 0.7f, Warm) },
            ["house_green"] = new[] { (-14f, -18f, 64f, 0.85f, Warm), (12f, -10f, 52f, 0.7f, Warm) },
            ["stall"] = new[] { (0f, -16f, 56f, 0.75f, Warm) },
            ["windmill"] = new[] { (0f, -20f, 46f, 0.55f, Warm) },
            ["lantern_off"] = new[] { (0f, -28f, 74f, 0.95f, (1f, 0.84f, 0.46f)) },
            ["well"] = new[] { (0f, -8f, 30f, 0.35f, Cool) },
            ["lamp_post"] = new[] { (7f, -36f, 72f, 0.95f, (1f, 0.84f, 0.50f)) },
            ["campfire"] = new[] { (0f, -8f, 84f, 1f, (1f, 0.62f, 0.30f)) },
            ["shrine"] = new[] { (0f, -19f, 38f, 0.6f, (1f, 0.84f, 0.46f)) },
            ["barn"] = new[] { (-12f, -12f, 50f, 0.6f, Warm) },
            ["hut"] = new[] { (0f, -14f, 44f, 0.5f, Warm) },
        };
        private static readonly Dictionary<string, (float, float)> Chimneys = new() { ["house"] = (-8, -60), ["house_blue"] = (-8, -60), ["house_green"] = (-8, -60) };
        private static readonly (int, int)[] LooseChickens = { (15, 21), (12, 13), (29, 13) };
        private const double SharePriceBonus = 0.10;
        private static readonly Dictionary<ActionKind, Rgba> ParticleColors = new()
        {
            [ActionKind.Till] = new(0.50f, 0.34f, 0.20f), [ActionKind.Water] = new(0.52f, 0.78f, 0.96f),
            [ActionKind.Plant] = new(0.50f, 0.80f, 0.40f), [ActionKind.Harvest] = new(1f, 0.90f, 0.40f),
        };

        private Dictionary<string, Action<string[]>> handlers = new();
        private readonly CritterCtx ctx = new();
        private List<(float X, float Y, string Kind)> emitters = new();
        private float smokeT;
        private string? atFired;
        private readonly List<Action> cutscenes = new();
        private (int, int, bool)? lightKey;
        private List<Light> staticLights = new();
        private bool wantQuests;
        private DebtReport? visit;
        private Random rng = new(11);
        private float fogToast = -99, mouseTime = -99, promptT;
        private (float, float) lastCursor;
        private (string Title, string Sub, float Age)? banner;
        private bool introBannerDone;
        private (string Phase, float T, bool Forced, string Report)? sleep;
        private readonly List<(float Wx, float Wy, string Text, string Icon, float Age, Rgba Color)> floaters = new();
        private string? promptId;
        private readonly List<DustPuff> dust = new();
        private bool debug;

        public ItemDb Items { get; private set; } = null!;
        public Dictionary<string, CropDef> Crops { get; private set; } = null!;
        public ChunkWorld Map { get; private set; } = null!;
        public FarmField Farm { get; private set; } = null!;
        public WorldRenderer WorldView { get; private set; } = null!;
        public Inventory Inv { get; private set; } = null!;
        public Journal Journal { get; private set; } = null!;
        public Debt Debt { get; private set; } = null!;
        public QuestLog Quests { get; private set; } = null!;
        public Story Story { get; private set; } = null!;
        public Storyline Storyline { get; private set; } = null!;
        public Player Player { get; private set; } = null!;
        public List<Npc> Npcs { get; } = new();
        public List<Interactable> Interactables { get; } = new();
        public List<Critter> Critters { get; } = new();
        public List<FarmTool> Hotbar { get; } = new();
        public int Slot { get; private set; }
        public Camera Camera { get; } = new();
        public ToastQueue Toasts { get; } = new();
        public Particles Particles { get; } = new();
        public Hud Hud { get; } = new();
        public Atmosphere Atmo { get; private set; } = null!;
        public float Time { get; set; }
        public bool Cinematic { get; set; }
        public Interactable? Focus { get; private set; }
        public TileCursor? Cursor { get; private set; }

        private GameState St => Game.State;

        public int Energy
        {
            get => St.GetInt("energy", Balance.EnergyMax);
            set => St.Set("energy", Math.Clamp(value, 0, Balance.EnergyMax));
        }

        // ================================================================ setup
        public override void OnEnter()
        {
            var g = Game;
            var defs = TileMap.LoadObjectDefs(TandurGame.Asset("data", "objects.json"));
            Items = new ItemDb(TandurGame.Asset("data", "items.json"));
            Crops = CropDef.Load(TandurGame.Asset("data", "crops.json"));
            var anchor = TileMap.Load(TandurGame.Asset("maps", "farm.json"), defs);
            St.SetDefault("world_seed", 20240);
            St.SetDefault("rings_open", 1);
            Map = new ChunkWorld(St.GetInt("world_seed"), defs, anchor);
            Farm = new FarmField(Map, Crops);
            WorldView = new WorldRenderer(g.Atlas, Map, Farm);
            St.SetDefault("gold", 150);
            St.SetDefault("energy", Balance.EnergyMax);
            Inv = new Inventory(St, Items);
            Journal = new Journal(TandurGame.Asset("data", "facts.json"), St);
            Debt = new Debt(St);
            Quests = new QuestLog(TandurGame.Asset("data", "quests.json"), St);
            Story = new Story(St);
            Storyline = new Storyline(TandurGame.Asset("data", "story.json"), St);
            St.SetDefault("mini_mode", "skip");

            Player = new Player(Map.Spawn.X, Map.Spawn.Y);
            Player.Footstep += (wx, wy) => dust.Add(new DustPuff(wx, wy, Player.GX, Player.GY));
            SpawnEntities(Map.Entities);
            Map.DynamicBlockers = Npcs.Select(n => (MathX.Round(n.GX), MathX.Round(n.GY))).ToHashSet();
            var data = g.PendingLoad;
            g.PendingLoad = null;
            if (data != null)
            {
                Farm.LoadJson(data["farm"]);
                Map.LoadJson(data["world"]);
                foreach (var ch in Map.Chunks.Values.Where(c => c.Visible && !c.Anchor).ToList()) SpawnEntities(ch.Entities);
                var p = data["player"]!.AsArray();
                float px = J.F(p[0]), py = J.F(p[1]);
                if (Map.IsWalkable(px, py)) { Player.GX = px; Player.GY = py; }
            }

            Hotbar.Add(new HoeTool());
            Hotbar.Add(new CanTool());
            foreach (var c in Crops.Values) Hotbar.Add(new SeedTool(c));

            Camera.Bounds = null;
            var (wx0, wy0) = Player.WorldPosition;
            Camera.SnapTo(wx0, wy0);
            Atmo = new Atmosphere(g.Atlas, St.GetInt("world_seed"));
            Hud.Coin.Shown = Hud.Coin.Target = Inv.Gold;
            Hud.EnergyTrail = Hud.EnergyLast = Energy / (float)Balance.EnergyMax;
            lastCursor = g.Input.Cursor;

            handlers = new()
            {
                ["give_item"] = a => OnGiveItem(a[0], a.Length > 1 ? int.Parse(a[1]) : 1),
                ["sell_all"] = _ => OnSellAll(),
                ["sleep"] = _ => BeginSleep(false),
                ["pay_debt"] = a => OnPayDebt(a.Length > 0 ? a[0] : "min"),
                ["open_quests"] = _ => wantQuests = true,
                ["memory"] = a => OnMemory(a[0]),
                ["take_produce"] = a => OnTakeProduce(a.Length > 0 ? int.Parse(a[0]) : 5),
            };
            foreach (var (n, h) in handlers) g.Events.On(n, h);

            if (g.Options.Act >= 0) St.Set("act", g.Options.Act);
            if (g.Options.Minutes >= 0) g.Clock.SkipTo(g.Options.Minutes);
            StartDay(first: true);
            if (Storyline.Current is { } beat && (string?)beat["spawn"] == "chickens") SpawnLoose(3 - St.GetInt("chickens_caught"));
            if (!St.GetBool("intro_done") && !g.Options.AutoPlay) g.Scenes.Push(new VnScene("intro"));
            if (g.Options.AutoPlay) St.Set("intro_done", true);
        }

        public override void OnExit()
        {
            foreach (var (n, h) in handlers) Game.Events.Off(n, h);
        }

        private void SpawnEntities(IEnumerable<JsonObject> entities)
        {
            foreach (var e in entities)
            {
                string type = (string)e["type"]!;
                if (type == "npc")
                {
                    var npc = new Npc(e);
                    Npcs.Add(npc);
                    npc.Snap(Game.Clock.Minutes);
                    Interactables.Add(new Interactable(npc.Id, npc.GX, npc.GY, npc.Script, npc.Prompt, npc, 50, npc));
                }
                else if (type == "interact")
                    Interactables.Add(new Interactable((string)e["id"]!, J.F(e["x"]), J.F(e["y"]), (string)e["script"]!, (string?)e["prompt"] ?? "Periksa"));
                else if (type == "critter")
                {
                    string kind = (string?)e["kind"] ?? "chicken";
                    int count = J.In(e["count"]) ?? 1;
                    float radius = J.Fn(e["radius"]) ?? 3f;
                    for (int i = 0; i < count; i++)
                        for (int tries = 0; tries < 10; tries++)
                        {
                            float gx = J.F(e["x"]) + (float)(rng.NextDouble() * 2 - 1), gy = J.F(e["y"]) + (float)(rng.NextDouble() * 2 - 1);
                            int tx = MathX.Round(gx), ty = MathX.Round(gy);
                            bool ok = Critter.Floating(kind) ? Map.KindAt(tx, ty) == "water" && Map.HeightAt(tx, ty) == 0
                                    : Critter.Airborne(kind) || (Map.IsWalkable(gx, gy) && Map.HeightAt(tx, ty) == 0);
                            if (ok) { AddCritter(Critter.Make(kind, gx, gy, radius, rng.Next(0, 10000))); break; }
                        }
                }
            }
        }

        private void AddCritter(Critter c)
        {
            Critters.Add(c);
            if (c.Pettable != null)
                Interactables.Add(new Interactable($"critter_{c.GetHashCode()}", c.GX, c.GY, c is Chicken { Loose: true } ? "@catch" : "@pet", c.Pettable, lift: 24, owner: c));
        }

        private void SpawnLoose(int n)
        {
            foreach (var (x, y) in LooseChickens.Take(Math.Max(0, n))) AddCritter(new Chicken(x, y, 2, rng.Next(0, 10000), loose: true));
        }

        // ================================================================ world / story
        private void UpdateWorld(float dt)
        {
            var (started, locked) = Map.Update(Player.GX, Player.GY, dt, St.GetInt("rings_open", 1));
            foreach (var ch in started)
            {
                SpawnEntities(ch.Entities);
                string first = $"seen_{ch.Biome}";
                if (!St.GetBool(first))
                {
                    St.Set(first, true);
                    Toasts.Add($"Wilayah baru: {ch.Name}", ch.Biome == "fields" ? "icon_coin" : null);
                }
                Game.Events.Emit("region_revealed", ch.Cx.ToString(), ch.Cy.ToString(), ch.Biome);
                QuestNotify(new QuestEvent("discover", Biome: ch.Biome));
            }
            if (locked && Time - fogToast > 12)
            {
                fogToast = Time;
                Toasts.Add("Kabut tebal menghalangi jalan. Selesaikan misi di papan desa.");
            }
        }

        private bool QuestDone(string q) => Quests.Status(q) == "done";

        private void UpdateStory(bool newDay = false)
        {
            var started = Storyline.Begin(newDay);
            if (started != null)
            {
                Toasts.Add($"Tujuan baru: {(string)started["text"]!}", "ui_star");
                if ((string?)started["spawn"] == "chickens") { St.Set("chickens_caught", 0); SpawnLoose(3); }
                if ((string?)started["intro"] is { } intro) cutscenes.Add(() => Game.Scenes.Push(new VnScene(intro)));
            }
            var cur = Storyline.Current;
            if (cur == null) return;
            if (cur["at"] is JsonObject at && atFired != (string)cur["id"]!)
            {
                var tile = at["tile"]!.AsArray();
                if (MathX.Hypot(Player.GX - J.I(tile[0]), Player.GY - J.I(tile[1])) <= (J.Fn(at["r"]) ?? 2.5f))
                {
                    atFired = (string)cur["id"]!;
                    SyncDebtVars();
                    string script = (string)at["script"]!;
                    cutscenes.Add(() => Game.Scenes.Push(new VnScene(script)));
                    return;
                }
            }
            var done = Storyline.FinishIfDone(QuestDone);
            if (done == null) return;
            Toasts.Add("Kisah berlanjut...", "ui_star");
            if (done["reward"] is JsonObject r)
            {
                if (J.In(r["gold"]) is int gold and > 0) { St.Add("gold", gold); Toasts.Add($"+{Fmt.N(gold)} keping", "icon_coin"); }
                if (r["items"] is JsonObject items)
                    foreach (var (item, n) in items) { Inv.Add(item, J.I(n)); Toasts.Add($"+{J.I(n)}  {Items.Name(item)}", $"icon_{item}"); }
            }
            if ((string?)done["outro"] is { } outro) cutscenes.Add(() => Game.Scenes.Push(new VnScene(outro)));
            UpdateStory();
        }

        private void BeatTalked(JsonObject? beat)
        {
            if (beat?["done"]?["talk"] is { } t && (bool)t) Storyline.Talked((string)beat["id"]!);
        }

        /// <summary>Posisi dunia yang ditunjuk kompas: (wx, wy, lift) atau null.</summary>
        private (float X, float Y, float Lift)? StoryTarget()
        {
            var loose = Critters.OfType<Chicken>().Where(c => c.Loose).ToList();
            if (loose.Count > 0)
            {
                var c = loose.OrderBy(c => MathX.Hypot(c.GX - Player.GX, c.GY - Player.GY)).First();
                var (wx, wy) = c.WorldPosition;
                return (wx, wy, 22);
            }
            var t = Storyline.Target();
            if (t == null) return null;
            var (kind, arg, tx, ty) = t.Value;
            if (kind == "npc")
            {
                var npc = Npcs.FirstOrDefault(n => n.Id == arg);
                if (npc == null) return null;
                var (wx, wy) = npc.WorldPosition;
                return (wx, wy, 46);
            }
            (float X, float Y) tile = (tx, ty);
            if (kind == "script")
            {
                float best = float.MaxValue;
                foreach (var it in Interactables)
                {
                    if (it.Script != arg || (arg == "@sit" && Map.HeightAt(MathX.Round(it.GX), MathX.Round(it.GY)) == 0)) continue;
                    float d = MathX.Hypot(it.GX - Player.GX, it.GY - Player.GY);
                    if (d < best) { best = d; tile = (it.GX, it.GY); }
                }
            }
            var (twx, twy) = Iso.ToWorld(tile.X, tile.Y);
            return (twx, twy, 30 + Map.LiftAt(tile.X, tile.Y));
        }

        private void OnTakeProduce(int amount)
        {
            var kinds = Items.Produce.OrderBy(i => i.Sell).ToList();
            bool ok = kinds.Sum(i => Inv.Count(i.Id)) >= amount;
            if (ok)
            {
                int left = amount;
                foreach (var i in kinds)
                {
                    int take = Math.Min(left, Inv.Count(i.Id));
                    if (take > 0) { Inv.Remove(i.Id, take); left -= take; }
                }
                Toasts.Add($"Kau menyerahkan {amount} hasil panen");
            }
            St.Set("gave_ok", ok);
        }

        private void Ambient(float dt)
        {
            smokeT -= dt;
            if (smokeT > 0) return;
            smokeT = 0.3f;
            float m = Game.Clock.Minutes;
            bool cooking = (m >= 360 && m < 570) || (m >= 1020 && m < 1320);
            var (px, py) = Player.WorldPosition;
            foreach (var (wx, wy, kind) in emitters)
            {
                if (MathF.Abs(wx - px) > 420 || MathF.Abs(wy - py) > 260) continue;
                if (kind == "fire")
                {
                    Particles.AddPuff(wx + (float)(rng.NextDouble() * 4 - 2), wy - 8, 1.6f);
                    Particles.Burst(wx, wy - 4, new Rgba(1f, 0.72f, 0.30f), 1, 10, 0.7f, 34, 1);
                }
                else if (cooking) Particles.AddPuff(wx + (float)(rng.NextDouble() * 2 - 1), wy);
            }
        }

        private void StartDay(bool first = false)
        {
            string weather = Atmo.SetDay(St.GetInt("world_seed"), Game.Clock.Day, Game.Clock.Season, Story.Act);
            WorldView.Dry = Story.Drought;
            WorldView.LanternsLit = Story.Act >= Story.Lentera;
            Hud.Weather = Atmosphere.WeatherNames[weather];
            if (Atmo.Raining && Farm.Plots.Count > 0)
            {
                Farm.WaterAll();
                if (!first) Toasts.Add("Hujan turun. Seluruh kebunmu tersiram.");
            }
        }

        private void CheckAct(bool newDay = false)
        {
            var clock = Game.Clock;
            if (Story.Advance(clock.Day, QuestDone, newDay) is not int act) return;
            WorldView.Dry = Story.Drought;
            WorldView.LanternsLit = act >= Story.Lentera;
            Atmo.SetDay(St.GetInt("world_seed"), clock.Day, clock.Season, act);
            Hud.Weather = Atmosphere.WeatherNames[Atmo.Weather];
            var (head, name) = Story.Acts[act];
            cutscenes.Add(() => { banner = (head.ToUpperInvariant(), name, 0); Game.Scenes.Push(new VnScene($"act_{act}")); });
            ProcessQuests(Quests.Notify(new QuestEvent("noop")));
        }

        private void OnMemory(string name)
        {
            if (!Story.Remember(name)) return;
            Toasts.Add($"Kenangan Sekar ditemukan ({St.GetInt("memories")}/5)");
            QuestNotify(new QuestEvent("memory"));
        }

        private List<Light> Lights()
        {
            var key = (Map.Chunks.Count, Map.Chunks.Values.Count(c => c.Visible), WorldView.LanternsLit);
            if (key != lightKey)
            {
                lightKey = key;
                staticLights = new();
                emitters = new();
                foreach (var ch in Map.Chunks.Values.Where(c => c.Visible))
                    foreach (var o in ch.Objects)
                    {
                        string sprite = o.Def.Sprite;
                        var (ex, ey) = o.WorldPosition;
                        if (Chimneys.ContainsKey(sprite) || sprite == "campfire")
                        {
                            var (cx, cy) = Chimneys.GetValueOrDefault(sprite, (0, -6));
                            emitters.Add((ex + cx, ey - o.Lift + cy, sprite == "campfire" ? "fire" : "smoke"));
                        }
                        if (!LightSources.TryGetValue(sprite, out var src) || (sprite == "lantern_off" && !WorldView.LanternsLit)) continue;
                        foreach (var (dx, dy, r, i, (cr, cg, cb)) in src) staticLights.Add(new Light(ex + dx, ey - o.Lift + dy, r, i, cr, cg, cb));
                    }
            }
            float n = Atmo.Night;
            if (n <= 0.01f) { Atmo.Lights = new(); return new(); }
            var lights = staticLights.Select(l => l with { Intensity = l.Intensity * n * (0.93f + 0.07f * MathF.Sin(Time * 4.3f + l.X * 0.1f)) }).ToList();
            Atmo.Lights = lights.Select(l => (l.X, l.Y, l.Radius / 64f)).ToList();
            var (px, py) = Player.WorldPosition;
            lights.Insert(0, new Light(px, py - 10 - Player.Lift, 58, 0.55f * n, 1f, 0.86f, 0.6f));
            return lights;
        }

        // ================================================================ VN events
        private void OnGiveItem(string item, int amount)
        {
            Inv.Add(item, amount);
            Toasts.Add($"+{amount}  {Items.Name(item)}", $"icon_{item}");
        }

        private void OnSellAll()
        {
            int total = Inv.SellAllProduce(), baseAmt = total;
            if (baseAmt > 0 && Story.Drought)
            {
                int bonus = (int)Math.Round(baseAmt * Story.DroughtPriceBonus, MidpointRounding.ToEven);
                St.Add("gold", bonus);
                total += bonus;
                Toasts.Add($"Kemarau: harga naik, +{bonus} keping");
            }
            if (baseAmt > 0 && St.GetBool("shared_water"))
            {
                int bonus = (int)Math.Round(baseAmt * SharePriceBonus, MidpointRounding.ToEven);
                St.Add("gold", bonus);
                total += bonus;
                Toasts.Add($"Harga sahabat: +{bonus} keping");
            }
            St.Set("last_sale", total);
            if (total > 0) QuestNotify(new QuestEvent("earn", total));
            Toasts.Add(total > 0 ? $"Terjual! +{total} keping" : "Belum ada hasil panen untuk dijual", total > 0 ? "icon_coin" : null);
        }

        private void OnPayDebt(string mode)
        {
            int want = mode switch { "min" => Debt.Installment, "big" => 1000, _ => Debt.Remaining };
            want = Math.Min(want, Debt.Remaining);
            bool ok = Debt.Remaining > 0 && (mode != "max" ? Inv.Gold >= want : Inv.Gold > 0);
            int paid = ok ? Debt.Pay(want) : 0;
            St.Set("pay_ok", paid > 0);
            St.Set("pay_amount", Fmt.N(paid));
            if (paid > 0)
            {
                Toasts.Add($"Utang dibayar {Fmt.N(paid)} keping", "icon_coin");
                var (wx, wy) = Player.WorldPosition;
                Particles.Burst(wx, wy - 20, Theme.Gold, 16, 30, 0.7f, 30);
            }
            SyncDebtVars();
            ProcessQuests(Quests.Recheck());
        }

        private void SyncDebtVars()
        {
            St.Set("t_debt", Fmt.N(Debt.Remaining));
            St.Set("t_min", Fmt.N(Debt.Minimum));
            St.Set("t_left", Fmt.N(Debt.SeasonLeft));
            St.Set("t_rate", (int)Math.Round(Debt.Rate * 100));
            St.Set("t_days", Debt.DaysUntilVisit(Game.Clock.Day));
            St.Set("t_gold", Fmt.N(Inv.Gold));
            St.Set("chapter", St.GetInt("rings_open", 1));
            St.Set("t_drought", Story.DroughtDaysLeft(Game.Clock.Day));
        }

        // ================================================================ update
        public override void Update(float dt)
        {
            var g = Game;
            var inp = g.Input;
            Time += dt;
            Particles.Update(dt);
            Toasts.Update(dt);
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                if (f.Age + dt >= 1.1f) floaters.RemoveAt(i); else floaters[i] = f with { Age = f.Age + dt };
            }
            foreach (var d in dust) d.Age += dt;
            dust.RemoveAll(d => d.Age > 0.36f);
            var (w, h) = g.Gfx.UiSize;
            Hud.Update(dt, Inv.Gold, Energy / (float)Balance.EnergyMax, Slot, Hotbar.Count);
            Atmo.Update(dt, g.Clock.Minutes);
            if (sleep != null) { UpdateSleep(dt); return; }
            if (cutscenes.Count > 0) { var c = cutscenes[0]; cutscenes.RemoveAt(0); c(); return; }
            if (inp.Pressed(InputAction.Cancel)) { g.Scenes.Push(new PauseScene(this)); return; }
            if (wantQuests || inp.Pressed(InputAction.Quests)) { wantQuests = false; SyncDebtVars(); g.Scenes.Push(new QuestScene(this)); return; }
            if (inp.Pressed(InputAction.Journal)) { g.Scenes.Push(new JournalScene(this)); return; }
            if (inp.Pressed(InputAction.Save)) SaveGame();
            if (inp.Pressed(InputAction.Inventory)) { g.Scenes.Push(new InventoryScene(this)); return; }
            if (inp.Pressed(InputAction.Debug)) debug = !debug;

            if (g.Clock.Update(dt)) BeginSleep(true);
            if (!introBannerDone && St.GetBool("intro_done"))
            {
                introBannerDone = true;
                banner = (Map.Name.ToUpperInvariant(), g.Clock.DateLabel, 0);
            }
            if (banner is { } bn) banner = bn.Age < 4 ? (bn.Title, bn.Sub, bn.Age + dt) : null;

            Player.Update(dt, inp, Map);
            if (g.Options.Walk.Length > 0) AutoWalk(dt);
            Player.Lift = Map.LiftAt(Player.GX, Player.GY);
            UpdateWorld(dt);
            var here = Map.ChunkAt(MathX.Round(Player.GX), MathX.Round(Player.GY));
            Atmo.Fireflies = here?.Biome is "forest" or "falls" or "pond" ? 1f : 0.35f;
            foreach (var (fx, fy) in WorldView.Foam.Take(6))
                if (rng.NextDouble() < 0.18) Particles.Burst(fx + (float)(rng.NextDouble() * 12 - 6), fy, new Rgba(0.86f, 0.95f, 1f), 2, 14, 0.5f, 20);
            float minutes = g.Clock.Minutes;
            foreach (var npc in Npcs) npc.Update(dt, Map, minutes, Player);
            var me = (MathX.Round(Player.GX), MathX.Round(Player.GY));
            Map.DynamicBlockers = Npcs.Select(n => (MathX.Round(n.GX), MathX.Round(n.GY))).Where(t => t != me).ToHashSet();
            ctx.Px = Player.GX; ctx.Py = Player.GY; ctx.Night = Atmo.Night; ctx.Raining = Atmo.Raining;
            ctx.Running = Player.Moving && inp.Held(InputAction.Run);
            foreach (var c in Critters) c.Update(dt, Map, ctx);
            foreach (var it in Interactables) if (it.Owner != null) { it.GX = it.Owner.GX; it.GY = it.Owner.GY; }
            WorldView.Wind = Atmo.Weather == "wind" ? 2.6f : Atmo.Raining ? 1.7f : 1f;
            WorldView.Night = Atmo.Night;
            WorldView.Daylight = 1 - Atmo.Night;
            Ambient(dt);
            UpdateStory();

            var mouse = g.MouseUi();
            bool overHud = inp.CursorSeen && Hud.OverUi(mouse.X, mouse.Y, w, h, Hotbar.Count);
            UpdateHotbar(inp, mouse, (w, h));
            UpdateCursor(inp, overHud);
            Focus = NearestInteractable();
            if (Focus?.Id != promptId) { promptId = Focus?.Id; promptT = 0; }
            promptT += dt;
            if (Focus != null && inp.Pressed(InputAction.Interact)) Interact(Focus);
            else if (!Player.Acting)
            {
                if (inp.Clicked && !overHud && MouseActive && Cursor is { } cur) UseToolAt(cur.X, cur.Y);
                else if (inp.Pressed(InputAction.Use)) { var (fx, fy) = FrontTile(); UseToolAt(fx, fy); }
            }
            var (pwx, pwy) = Player.WorldPosition;
            Camera.Follow(pwx, pwy, Balance.CameraSmoothing, dt);
            Camera.Clamp(g.Gfx.WorldViewSize.W, g.Gfx.WorldViewSize.H);
        }

        /// <summary>Untuk tangkapan layar otomatis: berjalan mengikuti string arah (contoh "ddddrr").</summary>
        private void AutoWalk(float dt)
        {
            string path = Game.Options.Walk;
            int idx = Math.Min(path.Length - 1, (int)(Time / 0.6f));
            if (Time / 0.6f >= path.Length) return;
            char c = path[idx];
            var (dx, dy) = c switch { 'l' => (-1, 0), 'r' => (1, 0), 'u' => (0, -1), _ => (0, 1) };
            var (dgx, dgy) = Iso.ToGrid(dx * Balance.WalkSpeed * 1.7f * dt, dy * Balance.WalkSpeed * 1.7f * dt);
            if (Map.IsWalkable(Player.GX + dgx, Player.GY + dgy)) { Player.GX += dgx; Player.GY += dgy; }
            Player.Moving = true;
            Player.Facing = c switch { 'l' => Facing.Left, 'r' => Facing.Right, 'u' => Facing.Up, _ => Facing.Down };
        }

        private void UpdateHotbar(InputState inp, (float X, float Y) mouse, (float W, float H) size)
        {
            int n = Hotbar.Count, digit = inp.DigitPressed();
            if (digit >= 1 && digit <= n) Slot = digit - 1;
            if (inp.Scroll != 0) Slot = ((Slot - inp.Scroll) % n + n) % n;
            if (inp.Clicked && inp.CursorSeen && Hud.SlotAt(mouse.X, mouse.Y, size.W, size.H, n) is int i) Slot = i;
        }

        private bool MouseActive => Time - mouseTime < MouseIdleSeconds;

        private void UpdateCursor(InputState inp, bool overHud)
        {
            if (inp.Cursor != lastCursor) { lastCursor = inp.Cursor; mouseTime = Time; }
            var tile = MouseActive && !overHud && inp.CursorSeen ? MouseToTile() : FrontTile();
            if (!Map.InBounds(tile.X, tile.Y)) { Cursor = null; return; }
            var color = Hotbar[Slot].PreviewAt(Farm, Inv, tile.X, tile.Y);
            if (MathX.Hypot(tile.X - Player.GX, tile.Y - Player.GY) > Balance.ToolReach && color != Preview.Info) color = Preview.Bad;
            Cursor = new TileCursor(tile.X, tile.Y, color);
        }

        private (int X, int Y) MouseToTile()
        {
            var gfx = Game.Gfx;
            var (fx, fy) = Game.MouseFramebuffer();
            float z = gfx.WorldZoom;
            var (camx, camy) = Camera.PixelPosition;
            var (gx, gy) = Iso.ToGrid(camx + (fx - gfx.Width / 2f) / z, camy + (fy - gfx.Height / 2f) / z);
            return (MathX.Round(gx), MathX.Round(gy));
        }

        private (int X, int Y) FrontTile()
        {
            var (wx, wy) = Player.FrontWorldPoint();
            var (gx, gy) = Iso.ToGrid(wx, wy);
            return (MathX.Round(gx), MathX.Round(gy));
        }

        // ================================================================ farming
        private int ToolCost(FarmTool tool, int x, int y)
        {
            if (Farm.IsRipe(x, y)) return 0;
            int extra = tool is CanTool && Story.Drought ? Story.DroughtWaterCost : 0;
            if (extra > 0 && St.GetBool("shared_water")) extra++;
            return Balance.EnergyCost.GetValueOrDefault(tool.Id) + extra;
        }

        private void UseToolAt(int x, int y)
        {
            if (!Map.InBounds(x, y)) return;
            if (MathX.Hypot(x - Player.GX, y - Player.GY) > Balance.ToolReach) { Toasts.Add("Terlalu jauh untuk dijangkau"); return; }
            if (Farm.IsRipe(x, y)) { BeginHarvest(x, y); return; }
            var tool = Hotbar[Slot];
            int cost = ToolCost(tool, x, y);
            if (cost > 0 && tool.PreviewAt(Farm, Inv, x, y) == Preview.Ok && Energy < cost)
            {
                Hud.Exhausted();
                Toasts.Add("Terlalu lelah. Tidurlah dulu.", "icon_energy");
                return;
            }
            var res = tool.Use(Farm, Inv, x, y);
            if (!res.Ok) { if (res.Message.Length > 0) Toasts.Add(res.Message); return; }
            if (res.Kind is ActionKind.Till or ActionKind.Water or ActionKind.Plant)
                QuestNotify(new QuestEvent("action", Kind: res.Kind.ToString().ToLowerInvariant()));
            var (wx, wy) = Iso.ToWorld(x, y);
            Player.FaceToward(wx, wy);
            Player.StartAction(res.Kind is ActionKind.Till or ActionKind.Water ? tool.Icon : null);
            Particles.Burst(wx, wy, ParticleColors[res.Kind], res.Kind != ActionKind.Harvest ? 10 : 18, 26, 0.55f, res.Kind != ActionKind.Water ? 18 : 6);
            if (cost > 0) Energy -= cost;
        }

        private void BeginHarvest(int x, int y)
        {
            var inp = Game.Input;
            string crop = Farm.Plots[(x, y)].Crop!;
            var block = inp.Held(InputAction.Run) ? null : Farm.FindBlock(x, y);
            var tiles = block ?? new List<(int, int)> { (x, y) };
            bool mastered = St.GetInt($"mastery_{crop}") >= Balance.MasteryStreak && St.GetString("mini_mode", "skip") == "skip" && !inp.Held(InputAction.Skip);
            if (mastered)
            {
                FinishHarvest(tiles, crop, new MiniResult(3, "", new(), block != null), instant: true);
                return;
            }
            Game.Scenes.Push(new MinigameScene(this, crop, block != null, r => FinishHarvest(tiles, crop, r)));
        }

        private void FinishHarvest(List<(int X, int Y)> tiles, string crop, MiniResult res, bool instant = false)
        {
            var def = Crops[crop];
            int got = 0;
            foreach (var (tx, ty) in tiles)
            {
                var r = Farm.Harvest(tx, ty);
                if (!r.Ok) continue;
                Inv.Add(r.Item!, 1, res.Stars);
                got++;
                var (wx, wy) = Iso.ToWorld(tx, ty);
                Particles.Burst(wx, wy, ParticleColors[ActionKind.Harvest], 14, 26, 0.55f, 18);
                floaters.Add((wx, wy - 18, "+1", $"icon_{r.Item}", 0, Theme.Gold));
            }
            if (got == 0) return;
            QuestNotify(new QuestEvent("harvest", got, Crop: crop, Stars: res.Stars));
            if (res.Bonus) { Inv.Add(def.Produce, 1, res.Stars); got++; }
            string name = Items.Name(def.Produce);
            Toasts.Add($"+{got}  {name}  ({res.Stars} bintang)", $"icon_{def.Produce}");
            if (res.Bonus) Toasts.Add("Bonus panen blok!", "icon_coin");
            Player.StartAction(null);
            if (!instant)
            {
                string key = $"mastery_{crop}";
                int streak = St.GetInt(key);
                St.Set(key, res.Stars == 3 ? streak + 1 : 0);
                if (res.Stars == 3 && streak + 1 == Balance.MasteryStreak) Toasts.Add($"Ahli panen {name}! Berikutnya otomatis.", "icon_" + def.Produce);
            }
            else Toasts.Add("Ahli: panen otomatis (tahan Ctrl untuk main)");
        }

        // ================================================================ quests
        private void QuestNotify(QuestEvent e) => ProcessQuests(Quests.Notify(e));

        private void ProcessQuests((List<QuestDef> Completed, List<QuestDef> Started) result)
        {
            foreach (var q in result.Completed)
            {
                var r = q.Reward;
                Toasts.Add($"Misi selesai: {q.Title}", "icon_coin");
                if (r.Gold > 0) { St.Add("gold", r.Gold); Toasts.Add($"+{Fmt.N(r.Gold)} keping"); }
                foreach (var (item, n) in r.Items) { Inv.Add(item, n); Toasts.Add($"+{n}  {Items.Name(item)}", $"icon_{item}"); }
                if (r.Rings > St.GetInt("rings_open", 1)) { St.Set("rings_open", r.Rings); Toasts.Add("Kabut menipis. Wilayah baru bisa dijelajahi!"); }
                if (q.Lesson.Length > 0) St.Set("last_lesson", new JsonArray(q.Title, q.Lesson));
            }
            foreach (var q in result.Started) Toasts.Add($"Misi baru: {q.Title}");
            if (result.Completed.Count > 0) CheckAct();
        }

        // ================================================================ interaction
        private Interactable? NearestInteractable()
        {
            Interactable? best = null;
            float bestD = Balance.InteractRadius;
            int level = Map.HeightAt(MathX.Round(Player.GX), MathX.Round(Player.GY));
            foreach (var it in Interactables)
            {
                if (Map.HeightAt(MathX.Round(it.GX), MathX.Round(it.GY)) != level) continue;
                if (it.Owner is { Hidden: true }) continue;
                float d = MathX.Hypot(it.GX - Player.GX, it.GY - Player.GY);
                if (it.Script == "@pet") d += 0.9f;
                if (d < bestD) { best = it; bestD = d; }
            }
            return best;
        }

        private void Interact(Interactable it)
        {
            Player.Moving = false;
            var (iwx, iwy) = Iso.ToWorld(it.GX, it.GY);
            Player.FaceToward(iwx, iwy);
            if (it.Npc != null) { var (pwx, pwy) = Player.WorldPosition; it.Npc.FaceToward(pwx, pwy); }
            SyncDebtVars();
            switch (it.Script)
            {
                case "@sit":
                    if (Map.HeightAt(MathX.Round(it.GX), MathX.Round(it.GY)) != 0) QuestNotify(new QuestEvent("sit"));
                    Game.Scenes.Push(new SitScene(this));
                    return;
                case "@pet" when it.Owner is Pet pet:
                    pet.PetIt();
                    Particles.Burst(iwx, iwy - 12, new Rgba(1f, 0.55f, 0.65f), 8, 16, 0.7f, 26);
                    Toasts.Add(pet.Kind == "dog" ? "Si Belang mengibaskan ekor. Dia mau ikut!" : "Jahe mendengkur senang.");
                    return;
                case "@catch":
                    if (it.Owner is Critter c) Critters.Remove(c);
                    Interactables.Remove(it);
                    int n = St.Add("chickens_caught", 1);
                    Particles.Burst(iwx, iwy - 6, new Rgba(1, 1, 1), 14, 30, 0.6f, 26);
                    Toasts.Add($"Ayam tertangkap! ({Math.Min(n, 3)}/3)");
                    Focus = null;
                    return;
            }
            if (it.Npc != null && Storyline.TalkScript(it.Npc.Id) is { } script)
            {
                var beat = Storyline.Current;
                Game.Scenes.Push(new VnScene(script, onFinish: () => BeatTalked(beat)));
                return;
            }
            Game.Scenes.Push(new VnScene(it.Script));
        }

        // ================================================================ sleep
        private void BeginSleep(bool forced) => sleep ??= ("out", 0, forced, "");

        private void UpdateSleep(float dt)
        {
            var s = sleep!.Value;
            s.T += dt;
            if (s.Phase == "out" && s.T >= 0.9f) { s.Report = EndDay(s.Forced); s.Phase = "hold"; s.T = 0; }
            else if (s.Phase == "hold" && s.T >= 2.6f) { s.Phase = "in"; s.T = 0; }
            else if (s.Phase == "in" && s.T >= 0.9f)
            {
                sleep = null;
                banner = ($"HARI {Game.Clock.DayOfSeason}", Game.Clock.FullLabel, 0);
                if (visit != null) cutscenes.Add(StartVisit);
                return;
            }
            sleep = s;
        }

        private string EndDay(bool forced)
        {
            int grown = Farm.EndDay();
            Game.Clock.NextDay();
            Energy = forced ? Balance.FaintEnergy : Balance.EnergyMax;
            Player.GX = Map.Spawn.X; Player.GY = Map.Spawn.Y;
            Player.Facing = Facing.Down;
            var (wx, wy) = Player.WorldPosition;
            Camera.SnapTo(wx, wy);
            var clock = Game.Clock;
            if (clock.Day > 1 && (clock.Day - 1) % GameClock.DaysPerSeason == 0)
            {
                visit = Debt.NewSeason();
                if (visit is { Missed: true }) St.Add("debt_missed_total", 1);
            }
            CheckAct(newDay: true);
            StartDay();
            foreach (var npc in Npcs) npc.Snap(clock.Minutes);
            UpdateStory(newDay: true);
            SaveGame(auto: true);
            return grown > 0 ? $"{grown} tanaman tumbuh semalam."
                 : Farm.PlotsWithCrops > 0 ? "Tanamanmu tidak tumbuh. Ingat menyiramnya sebelum tidur." : "";
        }

        private void StartVisit()
        {
            var v = visit!;
            visit = null;
            SyncDebtVars();
            St.Set("col_missed", v.Missed);
            St.Set("col_grace", v.Grace);
            St.Set("t_interest", Fmt.N(v.Interest));
            St.Set("t_fee", Fmt.N(v.Fee));
            St.Set("t_short", Fmt.N(v.Shortfall));
            Game.Scenes.Push(new VnScene("penagih"));
        }

        public bool SaveGame(bool auto = false)
        {
            try
            {
                Game.Saves.Save(new JsonObject
                {
                    ["state"] = St.ToJson(),
                    ["clock"] = Game.Clock.ToJson(),
                    ["farm"] = Farm.ToJson(),
                    ["world"] = Map.ToJson(),
                    ["player"] = new JsonArray(Math.Round(Player.GX, 2), Math.Round(Player.GY, 2)),
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Toasts.Add("Gagal menyimpan permainan");
                return false;
            }
            Toasts.Add(auto ? "Tersimpan otomatis" : "Permainan tersimpan");
            return true;
        }

        // ================================================================ draw
        public override void Draw(Renderer gfx)
        {
            var g = Game;
            var entities = new List<Actor> { Player };
            entities.AddRange(Npcs);
            entities.AddRange(Critters);
            entities.AddRange(dust);
            gfx.Lights = Lights();
            Atmo.ConfigurePost(gfx.Post);
            WorldView.Draw(gfx, Camera, Time, entities, Atmo.Tint(g.Clock.TintNow()), Cursor, Particles, Atmo);
            gfx.Lights = new();

            gfx.ViewUi();
            Atmo.DrawScreen(gfx);
            if (Cinematic) return;
            var batch = gfx.Batch;
            var font = g.Font;
            var (uiW, uiH) = gfx.UiSize;
            bool active = g.Scenes.Top == this && sleep == null;
            var counts = Hotbar.Where(e => e.Item != null).ToDictionary(e => e.Item!, e => Inv.Count(e.Item!));
            var mouse = g.Input.CursorSeen ? g.MouseUi() : (-999f, -999f);
            float hint = Ease.Clamp01((HintSeconds - Time) / 3f) * Ease.Clamp01(Time / 1f);
            Hud.Draw(gfx, font, g.Atlas, g.Clock, Energy, Balance.EnergyMax, Hotbar, Slot, counts, mouse, hint * (active ? 1 : 0));
            if (active) { Tracker.Draw(gfx, font, g.Atlas, this); DrawMarker(gfx); }
            if (active && Focus != null) DrawPrompt(gfx, Focus);
            DrawFloaters(gfx);
            Toasts.Draw(batch, font, g.Atlas, uiH, uiH - (hint > 0.01f ? 150 : 24));
            if (active) DrawBanner(batch, font, uiW);
            if (active && g.Input.CursorSeen) Hud.Tooltip(gfx, font, Hotbar, counts, mouse, Energy, Balance.EnergyMax);
            if (sleep != null) DrawSleep(batch, font, uiW, uiH);
            if (debug) DrawDebug(batch, font);
        }

        private void DrawDebug(SpriteBatch b, BitmapFont font)
        {
            string text = $"grid {Player.GX:0.0},{Player.GY:0.0}  chunks {Map.Chunks.Count}  act {Story.Act}  beat {Storyline.Index}  {Atmo.Weather}  night {Atmo.Night:0.00}";
            Theme.Text(font, b, text, 22, Game.Gfx.UiSize.H - 200, Theme.Small, Theme.Sky);
        }

        public (float X, float Y) WorldToUi(Renderer gfx, float wx, float wy)
        {
            var (dx, dy) = Camera.WorldToView(wx, wy);
            float z = gfx.WorldZoom / gfx.UiZoom;
            var (uiW, uiH) = gfx.UiSize;
            return (uiW / 2 + dx * z, uiH / 2 + dy * z);
        }

        private void DrawMarker(Renderer gfx)
        {
            if (StoryTarget() is not { } tgt) return;
            var batch = gfx.Batch;
            var atlas = Game.Atlas;
            var (uiW, uiH) = gfx.UiSize;
            var (sx, sy) = WorldToUi(gfx, tgt.X, tgt.Y - tgt.Lift);
            if (sx > 60 && sx < uiW - 60 && sy > 96 && sy < uiH - 140)
            {
                var mark = atlas["ui_marker"];
                float bob = MathF.Abs(MathF.Sin(Time * 3.2f)) * 10;
                batch.Quad(mark, sx - mark.W * 1.5f, sy - mark.H * 3 - bob, mark.W * 3, mark.H * 3);
                return;
            }
            float cx = uiW / 2, cy = uiH / 2, dx = sx - cx, dy = sy - cy;
            float k = Math.Min((cx - 70) / Math.Max(MathF.Abs(dx), 1e-3f), (cy - 150) / Math.Max(MathF.Abs(dy), 1e-3f));
            float ex = cx + dx * k, ey = cy + dy * k;
            int idx = (((int)MathF.Round(MathF.Atan2(dy, dx) * 180 / MathF.PI / 45f)) % 8 + 8) % 8;
            var arrow = atlas[$"ui_arrow_{idx}"];
            float pulse = 3 + 0.25f * MathF.Sin(Time * 5);
            batch.Quad(arrow, ex - arrow.W * pulse / 2, ey - arrow.H * pulse / 2, arrow.W * pulse, arrow.H * pulse);
            var (px, py) = Player.WorldPosition;
            int steps = (int)(MathX.Hypot(tgt.X - px, (tgt.Y - py) * 2) / 16);
            Theme.TextCentered(Game.Font, batch, $"{steps} langkah", ex, ey + 34, Theme.Small, Theme.Gold);
        }

        private void DrawFloaters(Renderer gfx)
        {
            var batch = gfx.Batch;
            foreach (var (wx, wy, text, icon, age, color) in floaters)
            {
                var (sx, sy) = WorldToUi(gfx, wx, wy);
                float rise = Ease.OutCubic(age / 0.9f) * 46, a = Ease.Clamp01((1.1f - age) / 0.4f), pop = Ease.OutBack(age / 0.2f);
                float sz = 32 * Math.Min(1.2f, Math.Max(0.2f, pop));
                batch.Quad(Game.Atlas[icon], sx - sz - 2, sy - rise - sz / 2, sz, sz, new Rgba(1, 1, 1, a));
                Theme.Text(Game.Font, batch, text, sx + 2, sy - rise - 12, Theme.Ui, color, a);
            }
        }

        private void DrawPrompt(Renderer gfx, Interactable it)
        {
            var batch = gfx.Batch;
            var font = Game.Font;
            var (wx, wy) = Iso.ToWorld(it.GX, it.GY);
            var (sx, sy) = WorldToUi(gfx, wx, wy - it.LiftPx);
            float pop = Ease.OutBack(promptT / 0.28f), a = Ease.Clamp01(promptT / 0.12f);
            float bob = MathF.Sin(Time * 4) * 3 + (1 - pop) * 22;
            float w = font.Measure(it.Prompt, Theme.Ui) + 36 + 36 + 22, h = 57;
            float x = sx - w / 2, y = sy - 74 + bob;
            Theme.Panel(batch, x, y, w, h, a, "paper");
            var tail = Game.Atlas["ui_tail"];
            batch.Quad(tail, sx - tail.W * 1.5f, y + h - 6, tail.W * 3, tail.H * 3, new Rgba(1, 1, 1, a));
            Theme.Nine(batch, "ui_keycap", x + 16, y + 10, 36, 36, new Rgba(1, 1, 1, a));
            Theme.InkText(font, batch, "E", x + 16 + 18 - font.Measure("E", Theme.Ui) / 2, y + 15, Theme.Ui, Theme.Ink, a);
            Theme.InkText(font, batch, it.Prompt, x + 62, y + 16, Theme.Ui, Theme.Ink, a);
        }

        private void DrawBanner(SpriteBatch batch, BitmapFont font, float uiW)
        {
            if (banner is not { } bn) return;
            float drop = bn.Age < 3.2f ? Ease.OutBack(bn.Age / 0.7f) : 1 - Ease.OutCubic((bn.Age - 3.2f) / 0.8f);
            float w = Math.Max(font.Measure(bn.Title, Theme.Body), font.Measure(bn.Sub, Theme.Ui)) + 120;
            float x = (uiW - w) / 2, y = -130 + drop * 150;
            Theme.Panel(batch, x, y, w, 120);
            Theme.TextCentered(font, batch, bn.Title, uiW / 2, y + 26, Theme.Body, Theme.Cream);
            batch.Rect(uiW / 2 - 60, y + 68, 120, 3, Theme.Gold);
            Theme.TextCentered(font, batch, bn.Sub, uiW / 2, y + 78, Theme.Small + 4, Theme.Gold);
        }

        private void DrawSleep(SpriteBatch batch, BitmapFont font, float uiW, float uiH)
        {
            var s = sleep!.Value;
            float a = s.Phase == "out" ? Math.Min(1, s.T / 0.9f) : s.Phase == "hold" ? 1 : Math.Max(0, 1 - s.T / 0.9f);
            batch.Rect(0, 0, uiW, uiH, new Rgba(0.03f, 0.03f, 0.07f, a));
            if (s.Phase != "hold") return;
            var clock = Game.Clock;
            float fade = Math.Min(1, Math.Min(s.T / 0.5f, (2.6f - s.T) / 0.5f));
            Theme.TextCentered(font, batch, s.Forced ? "Kau tertidur karena kelelahan..." : "Selamat pagi.", uiW / 2, uiH * 0.34f, Theme.Ui, Theme.Dim, fade);
            Theme.TextCentered(font, batch, $"HARI {clock.DayOfSeason}", uiW / 2, uiH * 0.42f, Theme.Title, Theme.Cream, fade);
            Theme.TextCentered(font, batch, clock.FullLabel, uiW / 2, uiH * 0.42f + 72, Theme.Ui, Theme.Gold, fade);
            if (s.Report.Length > 0) Theme.TextCentered(font, batch, s.Report, uiW / 2, uiH * 0.68f, Theme.Ui, Theme.Leaf, fade);
        }
    }

    /// <summary>Plakat kecil kiri atas: misi saat ini dan utang, agar tujuan tidak pernah tersembunyi.</summary>
    public static class Tracker
    {
        public static List<(string Text, Rgba Color)> Lines(PlayScene play)
        {
            var lines = new List<(string, Rgba)>();
            if (play.Storyline.Current is { } beat) lines.Add(((string)beat["text"]!, Theme.Gold));
            var active = play.Quests.Active();
            if (active.Count > 0)
            {
                var q = active.OrderBy(q => q.Chapter).First();
                int i = Enumerable.Range(0, q.Objectives.Length).FirstOrDefault(k => play.Quests.Progress(q, k).Cur < q.Objectives[k].N);
                var (cur, n) = play.Quests.Progress(q, i);
                lines.Add(($"[Q] {q.Title}   {Fmt.N(cur)}/{Fmt.N(n)}", Theme.Cream));
            }
            var d = play.Debt;
            int days = Debt.DaysUntilVisit(play.Game.Clock.Day);
            if (!d.Free && d.SeasonLeft > 0 && days <= 3) lines.Add(($"Cicilan kurang {Fmt.N(d.SeasonLeft)} | penagih {days} hari lagi", Theme.Berry));
            return lines;
        }

        public static void Draw(Renderer gfx, BitmapFont font, Atlas atlas, PlayScene play)
        {
            var lines = Lines(play);
            if (lines.Count == 0) return;
            float size = Theme.Small + 4;
            bool star = play.Storyline.Current != null;
            float w = lines.Max(l => font.Measure(l.Text, size)) + 44 + (star ? 26 : 0), h = 22 + lines.Count * 26, x = 22, y = 20;
            Theme.Panel(gfx.Batch, x, y, w, h);
            for (int i = 0; i < lines.Count; i++)
            {
                float dx = 0;
                if (star && i == 0) { gfx.Batch.Quad(atlas["ui_star"], x + 16, y + 9, 24, 24); dx = 26; }
                Theme.Text(font, gfx.Batch, lines[i].Text, x + 22 + dx, y + 14 + i * 26, size, lines[i].Color);
            }
        }
    }
}
