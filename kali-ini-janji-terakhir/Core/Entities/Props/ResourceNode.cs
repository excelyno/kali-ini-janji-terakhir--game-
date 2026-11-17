using System;
using System.Collections.Generic;
using System.Drawing;
using kali_ini_janji_terakhir.Core.Interfaces;
using kali_ini_janji_terakhir.Core.Items;
using kali_ini_janji_terakhir.Core.Quests;
using kali_ini_janji_terakhir.Core.Tools;
using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Entities.Props
{
    /// <summary>
    /// ABSTRAKSI: sumber bahan yang dipukul sampai hancur. Menyimpan HP (ENKAPSULASI)
    /// dan efek goyang saat dipukul. Jenis alat yang cocok, hasil, partikel, dan event quest
    /// ditentukan oleh subclass.
    /// </summary>
    public abstract class ResourceNode : StaticObject, IDamageable
    {
        private int hp;
        private float shake;

        protected ResourceNode(float x, float y, string sprite, int maxHp) : base(x, y, sprite)
        {
            MaxHp = maxHp;
            hp = maxHp;
        }

        public int Hp => hp;
        public int MaxHp { get; }
        public bool IsDestroyed => hp <= 0;

        /// <summary>Hasil saat hancur (muncul sebagai barang yang bisa dipungut).</summary>
        public abstract IEnumerable<(Item Item, int Amount)> Drops { get; }

        /// <summary>Event untuk quest saat hancur (null = tidak dihitung).</summary>
        public abstract GameEventType? DestroyedEvent { get; }

        /// <summary>Sprite partikel saat dipukul ("fx_leaf", "fx_chip_stone", ...).</summary>
        public abstract string HitParticle { get; }

        public abstract bool CanBeHitBy(Tool tool);

        /// <summary>Pohon/batu di pinggir peta adalah batas dunia, tidak bisa dihancurkan.</summary>
        public bool IsBorder { get; init; }

        /// <summary>Titik tengah sasaran pukulan (dekat pangkal benda).</summary>
        public PointF HitPoint => new(X, Y - 16);

        public bool TakeDamage(int amount)
        {
            if (IsDestroyed) return false;
            hp = Math.Max(0, hp - Math.Max(1, amount));
            shake = 0.25f;
            return IsDestroyed;
        }

        /// <summary>Benda yang muncul menggantikan benda ini setelah hancur (pohon → tunggul).</summary>
        public virtual ResourceNode? Remains => null;

        public override void Update(float dt, FarmWorld world)
        {
            if (shake > 0) shake -= dt;
        }

        protected float ShakeOffset => shake > 0 ? MathF.Sin(shake * 80) * 3 : 0;

        public override void Draw(Renderer r, float time)
        {
            if (ShadowSize != null) DrawShadow(r, ShadowSize, X, Y);
            r.Draw(Sprite, X + ShakeOffset, Y);
        }
    }

    /// <summary>
    /// Pohon. Saat pemain berdiri di balik tajuknya, pohon digambar transparan
    /// agar pemain tetap terlihat (trik klasik game top-down).
    /// </summary>
    public sealed class Tree : ResourceNode
    {
        private bool playerBehind;

        public Tree(float x, float y) : base(x, y, "tree", 4) { }

        public override RectangleF Hitbox => FootBox(30, 18);
        protected override string? ShadowSize => "xl";
        public override bool CanBeHitBy(Tool tool) => tool is Axe;
        public override IEnumerable<(Item, int)> Drops => new[] { ((Item)ItemDb.Wood, 3) };
        public override GameEventType? DestroyedEvent => GameEventType.Chopped;
        public override string HitParticle => "fx_leaf";
        public override ResourceNode Remains => new Stump(X, Y);

        public override void Update(float dt, FarmWorld world)
        {
            base.Update(dt, world);
            var p = world.Player;
            var crown = Bounds;
            crown.Height -= 40;                                   // hanya tajuk, bukan batang
            playerBehind = p.Y < Y && crown.Contains(p.X, p.Y - 30);
        }

        public override void Draw(Renderer r, float time)
        {
            DrawShadow(r, "xl", X, Y);
            r.Draw(Sprite, X + ShakeOffset, Y, playerBehind ? 0.55f : 1f);
        }
    }

    /// <summary>Tunggul sisa pohon: dua pukulan kapak lagi untuk membersihkannya.</summary>
    public sealed class Stump : ResourceNode
    {
        public Stump(float x, float y) : base(x, y, "stump", 2) { }

        public override RectangleF Hitbox => FootBox(26, 14);
        protected override string? ShadowSize => "m";
        public override bool CanBeHitBy(Tool tool) => tool is Axe;
        public override IEnumerable<(Item, int)> Drops => new[] { ((Item)ItemDb.Wood, 1) };
        public override GameEventType? DestroyedEvent => null;
        public override string HitParticle => "fx_chip_wood";
    }

    public sealed class Rock : ResourceNode
    {
        public Rock(float x, float y) : base(x, y, "rock", 3) { }

        public override RectangleF Hitbox => FootBox(34, 18);
        protected override string? ShadowSize => "m";
        public override bool CanBeHitBy(Tool tool) => tool is Pickaxe;
        public override IEnumerable<(Item, int)> Drops => new[] { ((Item)ItemDb.Stone, 2) };
        public override GameEventType? DestroyedEvent => GameEventType.Mined;
        public override string HitParticle => "fx_chip_stone";
    }
}
