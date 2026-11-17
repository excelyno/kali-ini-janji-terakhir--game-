using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using kali_ini_janji_terakhir.Core;
using kali_ini_janji_terakhir.Core.Crops;
using kali_ini_janji_terakhir.Core.Economy;
using kali_ini_janji_terakhir.Core.Entities;
using kali_ini_janji_terakhir.Core.Entities.Critters;
using kali_ini_janji_terakhir.Core.Entities.Npcs;
using kali_ini_janji_terakhir.Core.Entities.Props;
using kali_ini_janji_terakhir.Core.Interfaces;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Core.Quests;
using kali_ini_janji_terakhir.Core.Tools;
using kali_ini_janji_terakhir.Core.World;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Farm
{
    /// <summary>
    /// Model dunia: peta, semua objek, petak, waktu, utang, dan misi, serta aturan main
    /// (panen, jual, tidur, ganti musim). Tidak tahu apa-apa tentang keyboard atau jendela;
    /// UI berlangganan event (DialogueRequested, ShopRequested, ...) untuk membuka jendela.
    /// </summary>
    public sealed class FarmWorld
    {
        // ENKAPSULASI: koleksi privat, luar kelas hanya dapat versi baca-saja.
        private readonly List<GameObject> objects = new();
        private readonly Dictionary<Point, FarmPlot> plots = new();
        private readonly List<Floater> floaters = new();
        private readonly HashSet<int> removedIds = new();
        private readonly Dictionary<int, Stump> stumps = new();     // id pohon asal -> tunggulnya
        private readonly HashSet<string> flags = new(StringComparer.Ordinal);
        private readonly Random random = new(5);
        private int nextId;

        public FarmWorld(string? mapPath = null)
        {
            Art.Load();
            var loaded = MapLoader.Load(mapPath);
            Map = loaded.Map;
            Physics = new CollisionWorld(Map);

            var start = loaded.Markers.FirstOrDefault(m => m.Symbol == 'P').Tile;
            var startPos = TileMap.TileCenter(start);
            PlayerStart = new PointF(startPos.X, startPos.Y + 16);
            Player = new Player(PlayerStart.X, PlayerStart.Y);

            BuildMap(loaded.Markers);
            Weather = Core.World.Weather.ForDay(Clock.Day, Clock.Season);
            ShopOffers = CropFactory.All.Select(k => (ShopOffer)new SeedOffer(k))
                .Append(new MealOffer("Nasi Bungkus", 30, 50))
                .Append(new CanUpgradeOffer())
                .ToList();
        }

        // ---------------------------------------------------------------- state
        public TileMap Map { get; }
        public CollisionWorld Physics { get; }
        public ParticleSystem Effects { get; } = new();
        public PointF PlayerStart { get; }

        public Player Player { get; }
        public GameClock Clock { get; } = new();
        public Debt Debt { get; } = new();
        public QuestLog Quests { get; } = new();
        public WeatherKind Weather { get; private set; }
        public IReadOnlyList<ShopOffer> ShopOffers { get; }
        public float Time { get; private set; }
        public bool IsFinished { get; private set; }
        public string Message { get; private set; } = "";
        public float MessageTimer { get; private set; }

        public IReadOnlyList<GameObject> Objects => objects;
        public IReadOnlyCollection<FarmPlot> Plots => plots.Values;
        public IReadOnlyList<Floater> Floaters => floaters;

        // Event untuk lapisan UI.
        public event Action<Dialogue, Npc?>? DialogueRequested;
        public event Action? ShopRequested;
        public event Action? EndingRequested;
        public event Action? SaveRequested;

        public bool HasFlag(string flag) => flags.Contains(flag);
        public void SetFlag(string flag) => flags.Add(flag);

        // ---------------------------------------------------------------- map
        private T Spawn<T>(T obj) where T : GameObject
        {
            obj.AssignId(nextId++);
            objects.Add(obj);
            Physics.Add(obj);
            return obj;
        }

        private void Despawn(GameObject obj)
        {
            obj.Remove();
            Physics.Remove(obj);
        }

        /// <summary>Buat objek untuk setiap huruf penanda di farm.txt (lihat MapLoader untuk daftar huruf).</summary>
        private void BuildMap(IReadOnlyList<MapMarker> markers)
        {
            Spawn(Player);
            var fenceTiles = markers.Where(m => m.Symbol == 'f').Select(m => m.Tile).ToHashSet();
            int seed = 1;
            foreach (var (symbol, tile) in markers)
            {
                var c = TileMap.TileCenter(tile);
                float bottom = (tile.Y + 1) * TileMap.Tile;
                switch (symbol)
                {
                    case 'H': Spawn(new House(c.X, bottom)); break;
                    case 'S': Spawn(new ShopStall(c.X, bottom)); break;
                    case 'X': Spawn(new ShippingBin(c.X, bottom - 6)); break;
                    case 'T': Spawn(new Tree(c.X, bottom - 8)); break;
                    case 'B': Spawn(new Tree(c.X, bottom - 8) { IsBorder = true }); break;
                    case 'o': Spawn(new Rock(c.X, bottom - 10)); break;
                    case 'b': Spawn(new Bush(c.X, bottom - 8)); break;
                    case 'c': Spawn(new Chicken(c.X, bottom - 10, seed++)); break;
                    case 'f': Spawn(new Fence(tile, TileMap.NeighborMask(tile, fenceTiles.Contains))); break;
                }
            }

            Spawn(new Butterfly(TileX(12), TileX(16), 4, "yellow"));
            Spawn(new Butterfly(TileX(24), TileX(15), 5, "white"));
            Spawn(new Butterfly(TileX(20), TileX(23), 6, "pink"));

            // Rute warga dalam koordinat tile (titik tengah tile), mengikuti jalan setapak.
            Spawn(new Wira(Route((12, 14), (20, 14), (22, 14), (22, 12), (22, 14), (12, 14))));
            Spawn(new Lastri(Route((5, 14), (8, 14))));
            Spawn(new Rentenir(Route((34, 15), (34, 24), (34, 15))));

            // Dua baris pertama ladang sudah dicangkul (yang tidak tertutup batu).
            var dirt = AllTiles().Where(t => Map[t] == Terrain.Dirt).ToList();
            int top = dirt.Min(t => t.Y);
            foreach (var t in dirt.Where(t => t.Y < top + 2 && IsTileFree(t)))
                plots[t] = new FarmPlot(t);
        }

        private static float TileX(int tile) => tile * TileMap.Tile + TileMap.Tile / 2f;

        private static IEnumerable<PointF> Route(params (int X, int Y)[] tiles)
            => tiles.Select(t => new PointF(TileX(t.X), TileX(t.Y) + 16));

        private IEnumerable<Point> AllTiles()
        {
            for (int y = 0; y < Map.Height; y++)
                for (int x = 0; x < Map.Width; x++)
                    yield return new Point(x, y);
        }

        // ---------------------------------------------------------------- queries
        public bool IsFarmTile(Point tile) => Map[tile] == Terrain.Dirt;
        public bool IsWater(Point tile) => Map.InBounds(tile) && Map[tile] == Terrain.Water;
        public FarmPlot? PlotAt(Point tile) => plots.TryGetValue(tile, out var p) ? p : null;

        /// <summary>Tile kosong: tidak ada benda padat (pohon, batu, pagar) di atasnya.</summary>
        public bool IsTileFree(Point tile)
        {
            var r = TileMap.TileRect(tile);
            return !objects.Any(o => o is not Entity && !o.Hitbox.IsEmpty && o.Hitbox.IntersectsWith(r));
        }

        /// <summary>Pohon/batu di tile sasaran yang cocok dengan alat ini.</summary>
        public ResourceNode? NodeAt(Point tile, Tool tool)
        {
            var area = RectangleF.Inflate(TileMap.TileRect(tile), -4, -4);
            return objects.OfType<ResourceNode>()
                .FirstOrDefault(n => !n.IsBorder && n.CanBeHitBy(tool) && (n.Hitbox.IntersectsWith(area) || area.Contains(n.HitPoint)));
        }

        /// <summary>Benda yang bisa diajak interaksi saat ini (prioritas: panen, warga, hewan, bangunan, air).</summary>
        public IInteractable? FindInteractable()
        {
            var plot = PlotAt(Player.TargetTile);
            if (plot != null && plot.InRange(this)) return plot;
            var found = objects.OfType<IInteractable>()
                .Where(i => i.InRange(this))
                .OrderBy(i => i switch { Npc => 0, Critter => 1, _ => 2 })
                .ThenBy(i => i is GameObject o ? Math.Abs(o.X - Player.X) + Math.Abs(o.Y - Player.Y) : 0)
                .FirstOrDefault();
            if (found == null && IsWater(Player.TargetTile)) return new WaterSpot();
            return found;
        }

        public int SellValue()
            => Player.Inventory.Entries.Where(kv => kv.Key.Sellable).Sum(kv => kv.Key.SellPrice(Clock.Season) * kv.Value);

        // ---------------------------------------------------------------- actions
        public void Say(string text)
        {
            Message = text;
            MessageTimer = 3f;
        }

        public void Float(string text, Color color, float x, float y) => floaters.Add(new Floater(text, color, x, y));

        public void Raise(GameEventType type, int amount = 1, string? subject = null)
        {
            var done = Quests.Handle(new GameEvent(type, amount, subject), this);
            while (done != null)
            {
                GiveReward(done);
                done = Quests.TryComplete(this); // misi berikutnya mungkin langsung selesai (misal misi utang)
            }
        }

        private void GiveReward(Quest q)
        {
            if (q.Reward.Coins > 0) Player.Wallet.Earn(q.Reward.Coins);
            if (q.Reward.Item != null && q.Reward.ItemAmount > 0) Player.Inventory.Add(q.Reward.Item, q.Reward.ItemAmount);
            Say(q.Reward.Coins > 0 || q.Reward.Item != null ? $"Misi selesai: {q.Title}! Hadiah: {q.Reward}" : $"Misi selesai: {q.Title}!");
            Float("Misi selesai!", Color.Gold, Player.X, Player.Y - 110);
            for (int i = 0; i < 4; i++) Effects.Emit("fx_sparkle", Player.X - 20 + i * 13, Player.Y - 90 + i % 2 * 12, life: 0.7f);
        }

        public void TillTile(Point tile) => plots[tile] = new FarmPlot(tile);

        public void HarvestPlot(FarmPlot plot)
        {
            var result = plot.Harvest();
            if (result == null) return;
            var r = result.Value;
            var produce = ItemDb.Produce(r.Kind);
            Player.Inventory.Add(produce, r.Amount);
            var c = TileMap.TileCenter(plot.Tile);
            Float($"+{r.Amount} {produce.Name}", Color.White, c.X, c.Y - 30);
            for (int i = 0; i < 3; i++) Effects.Emit("fx_sparkle", c.X - 12 + i * 12, c.Y - 10 - i % 2 * 10, life: 0.6f);
            Raise(GameEventType.Harvested, r.Amount, produce.Name);
        }

        /// <summary>Pohon/batu hancur: jatuhkan barang, ganti dengan sisanya (tunggul), catat untuk save.</summary>
        public void DestroyNode(ResourceNode node)
        {
            Despawn(node);
            if (node is Stump stump)
            {
                foreach (var (treeId, s) in stumps.Where(kv => kv.Value == stump).ToList()) stumps.Remove(treeId);
            }
            else removedIds.Add(node.Id);

            foreach (var (item, amount) in node.Drops)
                for (int i = 0; i < amount; i++)
                    objects.Add(new Pickup(item, node.X, node.Y - 4, random));

            if (node.Remains is { } remains) stumps[node.Id] = Spawn((Stump)remains);
            if (node.DestroyedEvent is { } e) Raise(e);
        }

        public void CollectPickup(Pickup pickup)
        {
            pickup.Remove();
            Player.Inventory.Add(pickup.Item);
            Float("+1 " + pickup.Item.Name, Color.White, Player.X, Player.Y - 96);
        }

        public void SellAll()
        {
            var sold = Player.Inventory.TakeAllSellable();
            int total = sold.Sum(kv => kv.Key.SellPrice(Clock.Season) * kv.Value);
            if (total == 0) { Say("Tidak ada yang bisa dijual. (Benih tidak dijual.)"); return; }
            Player.Wallet.Earn(total);
            Float($"+{total} koin", Color.Gold, Player.X, Player.Y - 96);
            Say($"Terjual {sold.Sum(kv => kv.Value)} barang seharga {total} koin.");
            Raise(GameEventType.Earned, total);
        }

        public void ShowDialogue(Dialogue d, Npc? speaker = null) => DialogueRequested?.Invoke(d, speaker);
        public void OpenShop() => ShopRequested?.Invoke();

        public void TriggerEnding()
        {
            if (IsFinished) return;
            IsFinished = true;
            EndingRequested?.Invoke();
        }

        /// <summary>Tidur: tanaman tumbuh, hari berganti, mungkin musim berganti dan Pak Rentenir datang.</summary>
        public void Sleep(bool passedOut)
        {
            var nightSeason = Clock.Season;
            bool seasonChanged = Clock.StartNextDay();
            Weather = Core.World.Weather.ForDay(Clock.Day, Clock.Season);
            bool rain = Core.World.Weather.WatersCrops(Weather);

            foreach (var p in plots.Values) p.OnNewDay(nightSeason, rain);
            foreach (var d in objects.OfType<IDailyUpdatable>()) d.OnNewDay(this);

            Player.RestoreEnergy(GameConfig.MaxEnergy);
            if (passedOut) Player.TrySpendEnergy(40);
            Player.Can.Refill();
            Player.Teleport(PlayerStart.X, PlayerStart.Y);
            Player.StopMoving();

            string morning = $"Pagi hari {Clock.DayOfSeason} musim {GameClock.SeasonName(Clock.Season)}. Cuaca: {Core.World.Weather.Name(Weather)}.";
            if (rain) morning += " Hujan menyiram kebunmu.";
            Say(passedOut ? "Kamu pingsan karena kelelahan... " + morning : morning);
            Raise(GameEventType.Slept);

            if (seasonChanged) SeasonVisit();
            SaveRequested?.Invoke();
        }

        private void SeasonVisit()
        {
            if (Debt.IsPaidOff) return;
            var r = Debt.EndSeason();
            string text = r.WasGrace
                ? $"Musim pertamamu sudah lewat. Masa tenggang selesai: mulai sekarang bunga {r.RatePercent}% per musim."
                : r.Missed
                    ? $"Kamu MENUNGGAK cicilan! Denda {r.LateFee} koin, bunga naik jadi {r.RatePercent}%. Ditambah bunga {r.Interest} koin."
                    : $"Cicilan tepat waktu, bagus. Bunga musim ini {r.Interest} koin.";
            ShowDialogue(new Dialogue("Pak Rentenir", "Selamat pagi. Musim baru, waktunya menghitung utang.", text,
                $"Sisa utangmu sekarang {r.Remaining} koin. Cicilan minimal {Debt.Installment} per musim."),
                objects.OfType<Rentenir>().FirstOrDefault());
        }

        // ---------------------------------------------------------------- loop
        public void Update(float dt, bool simulate)
        {
            Time += dt;
            if (MessageTimer > 0) MessageTimer -= dt;
            foreach (var f in floaters) f.Update(dt);
            floaters.RemoveAll(f => f.IsDead);
            Effects.Update(dt);
            if (!simulate) return;

            if (Clock.Advance(dt)) { Sleep(true); return; }
            for (int i = 0; i < objects.Count; i++) objects[i].Update(dt, this);
            objects.RemoveAll(o => o.IsRemoved);
        }

        // ---------------------------------------------------------------- save
        public SaveData ToSave() => new(
            SaveSystem.Version,
            Clock.Day,
            Clock.Minutes,
            Player.Wallet.Coins,
            Player.Wallet.TotalEarned,
            Player.Energy,
            Player.Can.Water,
            Player.Can.Capacity,
            Player.Inventory.ToSave(),
            plots.Values.Select(p => new PlotSave(p.Tile.X, p.Tile.Y, p.IsWatered, p.Crop?.Kind.ToString(), p.Crop?.Age ?? 0)).ToList(),
            removedIds.ToList(),
            objects.OfType<Chicken>().Where(c => !c.HasEgg).Select(c => c.Id).ToList(),
            flags.ToList(),
            Player.X,
            Player.Y,
            Debt.ToSave(),
            Quests.ActiveIndex,
            Quests.Active?.SavedProgress ?? 0,
            stumps.Keys.ToList());

        public void Restore(SaveData d)
        {
            Clock.Restore(d.Day, d.Minutes);
            Weather = Core.World.Weather.ForDay(Clock.Day, Clock.Season);
            Player.Wallet.Restore(d.Coins, d.TotalEarned);
            Player.RestoreState(d.Px, d.Py, d.Energy);
            Player.Can.Restore(d.Water, d.WaterCap);
            Player.Inventory.Restore(d.Items);
            Debt.Restore(d.Debt);
            Quests.Restore(d.QuestIndex, d.QuestProgress);

            flags.Clear();
            foreach (var f in d.Flags ?? new()) flags.Add(f);

            plots.Clear();
            foreach (var s in d.Plots ?? new())
            {
                var plot = new FarmPlot(new Point(s.X, s.Y));
                Crop? crop = null;
                if (s.Crop != null && Enum.TryParse<CropKind>(s.Crop, out var kind))
                {
                    crop = CropFactory.Create(kind);
                    crop.RestoreAge(s.Age);
                }
                plot.Restore(s.Watered, crop);
                plots[plot.Tile] = plot;
            }

            var stumpIds = new HashSet<int>(d.Stumps ?? new());
            foreach (int id in d.Removed ?? new())
            {
                if (objects.FirstOrDefault(q => q.Id == id && q is ResourceNode) is not ResourceNode node) continue;
                Despawn(node);
                removedIds.Add(id);
                if (stumpIds.Contains(id) && node.Remains is Stump stump) stumps[id] = Spawn(stump);
            }
            objects.RemoveAll(o => o.IsRemoved);

            var noEgg = new HashSet<int>(d.EggsTaken ?? new());
            foreach (var c in objects.OfType<Chicken>()) c.RestoreEgg(!noEgg.Contains(c.Id));
        }

        /// <summary>Air di depan pemain: tombol E mengisi penyiram.</summary>
        private sealed class WaterSpot : IInteractable
        {
            public bool InRange(FarmWorld world) => true;
            public string Prompt(FarmWorld world) => "Isi penyiram";

            public void Interact(FarmWorld world)
            {
                world.Player.Can.Refill();
                var c = TileMap.TileCenter(world.Player.TargetTile);
                world.Effects.Emit("fx_ripple", c.X, c.Y, life: 0.6f);
                world.Float("Air penuh", Color.LightSkyBlue, world.Player.X, world.Player.Y - 90);
            }
        }
    }
}
