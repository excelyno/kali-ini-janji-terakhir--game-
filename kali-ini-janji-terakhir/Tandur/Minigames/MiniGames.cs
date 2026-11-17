using Tandur.World;

namespace Tandur.Minigames
{
    public sealed class MiniInput
    {
        public bool Pull;
        public float Steer;
    }

    public sealed record MiniResult(int Stars, string Message, List<string> Facts, bool Bonus = false);

    /// <summary>
    /// ABSTRAKSI minigame panen: logika murni (tanpa grafis/input device). Tiap tanaman punya
    /// subclass sendiri (PEWARISAN), dan scene presenter memanggil Update()/Press()/Pick()
    /// secara polimorfik. Tidak ada gagal total: permainan buruk hanya menurunkan bintang.
    /// </summary>
    public abstract class MiniGame
    {
        protected readonly Rng Rng;

        protected MiniGame(string crop, bool block, int seed)
        {
            Crop = crop; Block = block;
            Rng = new Rng(seed);
        }

        public string Crop { get; }
        public bool Block { get; }
        public float T { get; protected set; }
        public bool Done { get; private set; }
        public MiniResult? Result { get; private set; }
        public abstract string Title { get; }
        public abstract string Hint { get; }

        public abstract void Update(float dt, MiniInput inp);
        public virtual void Press() { }
        public virtual void Pick(int i) { }

        protected void Finish(int stars, string message)
        {
            var facts = new List<string> { $"{Crop}_first" };
            if (stars == 3) facts.Add($"{Crop}_perfect");
            if (Block) facts.Add("block_first");
            Result = new MiniResult(stars, message, facts, stars == 3 && Block);
            Done = true;
        }

        public static MiniGame Create(string crop, bool block = false, int seed = 0) => crop switch
        {
            "carrot" => new CarrotRhythm(crop, block, seed),
            "tomato" => new TomatoPick(crop, block, seed),
            _ => new TurnipPull(crop, block, seed),
        };
    }

    /// <summary>Lobak: tarik dan jaga keseimbangan jarum di zona hijau.</summary>
    public sealed class TurnipPull : MiniGame
    {
        public const float TimeLimit = 28;
        private readonly float p1, p2;
        private bool edge;
        private float pullTime, goodTime;

        public TurnipPull(string crop = "turnip", bool block = false, int seed = 0) : base(crop, block, seed)
        {
            Zone = block ? 0.22f : 0.30f;
            p1 = Rng.Uniform(0, 6.28f); p2 = Rng.Uniform(0, 6.28f);
        }

        public override string Title => "Cabut Lobak";
        public override string Hint => "Tahan Spasi / klik untuk menarik. A/D atau gerakkan mouse agar jarum tetap di zona hijau.";
        public float Zone { get; }
        public float Needle { get; private set; }
        public float Vel { get; private set; }
        public float Progress { get; private set; }
        public int Snaps { get; private set; }
        public bool Pulling { get; private set; }
        public bool InZone => MathF.Abs(Needle) <= Zone;

        public override void Update(float dt, MiniInput inp)
        {
            if (Done) return;
            T += dt;
            Pulling = inp.Pull;
            float wind = MathF.Sin(T * 1.7f + p1) * 0.9f + MathF.Sin(T * 3.1f + p2) * 0.6f;
            Vel += (wind * (1 + 1.6f * Progress) + inp.Steer * 7) * dt;
            Vel *= Math.Max(0, 1 - 2 * dt);
            Needle += Vel * dt;
            if (MathF.Abs(Needle) >= 0.96f)
            {
                Needle = MathF.CopySign(0.96f, Needle);
                Vel *= -0.4f;
                if (!edge) { edge = true; Snaps++; Progress = Math.Max(0, Progress - 0.10f); }
            }
            else if (MathF.Abs(Needle) < 0.88f) edge = false;
            if (Pulling)
            {
                pullTime += dt;
                if (InZone) { goodTime += dt; Progress += 0.17f * dt; }
                else Progress = Math.Max(0, Progress - 0.08f * dt);
            }
            if (Progress >= 1)
            {
                float eff = goodTime / Math.Max(pullTime, 1e-6f);
                if (Snaps == 0 && eff >= 0.75f) Finish(3, "Dicabut pelan dan lurus, akar lobak tetap utuh.");
                else if (Snaps <= 1 && eff >= 0.5f) Finish(2, "Lumayan! Tapi akar sempat tegang. Tarik lebih pelan.");
                else Finish(1, "Terlalu kasar: ujung akar lobak patah. Cabut perlahan agar utuh.");
            }
            else if (T >= TimeLimit) Finish(1, "Waktu habis. Lobak akhirnya terangkat, tapi kurang rapi.");
        }
    }

    /// <summary>Wortel: tekan tepat saat penanda melewati zona tengah (ritme).</summary>
    public sealed class CarrotRhythm : MiniGame
    {
        private static readonly float[] Speeds = { 0.55f, 0.75f, 0.95f };
        private const float PauseTime = 0.7f;
        private float rt, pause;

        public CarrotRhythm(string crop = "carrot", bool block = false, int seed = 0) : base(crop, block, seed)
        {
            Perfect = block ? 0.035f : 0.05f;
            Good = block ? 0.10f : 0.13f;
        }

        public override string Title => "Tarik Wortel";
        public override string Hint => "Tekan Spasi / klik tepat saat penanda melewati zona tengah.";
        public float Perfect { get; }
        public float Good { get; }
        public int Round { get; private set; }
        public List<string> Results { get; } = new();
        public string? Last { get; private set; }
        public bool Paused => pause > 0;

        public float Speed => Speeds[Math.Min(Round, 2)] * (Block ? 1.15f : 1f);

        public float Marker
        {
            get
            {
                float p = rt * Speed % 1f;
                return p < 0.5f ? 2 * p : 2 - 2 * p;
            }
        }

        public override void Update(float dt, MiniInput inp)
        {
            if (Done) return;
            T += dt;
            if (pause > 0)
            {
                pause -= dt;
                if (pause <= 0) Next();
                return;
            }
            rt += dt;
            if (rt * Speed >= 2) Score("miss");
        }

        public override void Press()
        {
            if (Done || pause > 0) return;
            float d = MathF.Abs(Marker - 0.5f);
            Score(d <= Perfect ? "perfect" : d <= Good ? "good" : "miss");
        }

        private void Score(string kind)
        {
            Results.Add(kind);
            Last = kind;
            pause = PauseTime;
        }

        private void Next()
        {
            Round++;
            rt = 0;
            if (Round < 3) return;
            int pts = Results.Sum(r => r == "perfect" ? 2 : r == "good" ? 1 : 0);
            if (pts >= 5) Finish(3, "Ditarik tepat dan lurus. Wortel keluar utuh!");
            else if (pts >= 3) Finish(2, "Bagus. Tarik lurus ke atas agar wortel tidak patah.");
            else Finish(1, "Wortel ditarik miring dan sebagian patah. Tarik lurus ke atas!");
        }
    }

    /// <summary>Tomat: petik hanya yang sudah merah. Kuning akan matang sebentar lagi.</summary>
    public sealed class TomatoPick : MiniGame
    {
        public const float RipenAt = 6;
        private readonly Dictionary<int, float> ripeAt = new();

        public TomatoPick(string crop = "tomato", bool block = false, int seed = 0) : base(crop, block, seed)
        {
            var (reds, yellows, greens) = block ? (4, 3, 3) : (3, 2, 3);
            Kinds = Enumerable.Repeat("red", reds).Concat(Enumerable.Repeat("yellow", yellows)).Concat(Enumerable.Repeat("green", greens)).ToList();
            Rng.Shuffle(Kinds);
            for (int i = 0; i < Kinds.Count; i++) if (Kinds[i] == "yellow") ripeAt[i] = RipenAt + Rng.Uniform(0, 2.5f);
            Picked = new bool[Kinds.Count];
            TimeLimit = block ? 22 : 18;
        }

        public override string Title => "Petik Tomat";
        public override string Hint => "Pilih tomat yang sudah MERAH. Kuning akan matang sebentar lagi, hijau belum matang.";
        public List<string> Kinds { get; }
        public bool[] Picked { get; }
        public int GoodCount { get; private set; }
        public int Wrong { get; private set; }
        public float TimeLimit { get; }
        public (string Kind, int Index)? Last { get; private set; }
        public int Targets => Kinds.Count(k => k != "green");

        public string ColorOf(int i) => Kinds[i] == "yellow" && T >= ripeAt[i] ? "red" : Kinds[i];

        public override void Update(float dt, MiniInput inp)
        {
            if (Done) return;
            T += dt;
            if (GoodCount >= Targets || T >= TimeLimit) End();
        }

        public override void Pick(int i)
        {
            if (Done || i < 0 || i >= Kinds.Count || Picked[i]) return;
            Picked[i] = true;
            if (ColorOf(i) == "red") { GoodCount++; Last = ("good", i); }
            else { Wrong++; Last = ("wrong", i); }
            if (GoodCount >= Targets) End();
        }

        private void End()
        {
            if (Done) return;
            if (GoodCount == Targets && Wrong == 0) Finish(3, "Semua dipetik saat merah dan matang. Manis dan segar!");
            else if (GoodCount >= (int)Math.Ceiling(Targets * 0.6) && Wrong <= 1) Finish(2, "Hampir sempurna. Sabar menunggu tomat matang untuk rasa terbaik.");
            else if (Wrong > 0) Finish(1, "Tomat hijau dipetik terlalu dini: rasanya asam dan keras. Tunggu sampai merah.");
            else Finish(1, "Banyak tomat matang yang terlewat. Petik saat merah sebelum terlalu masak.");
        }
    }
}
