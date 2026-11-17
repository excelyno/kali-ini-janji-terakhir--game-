using System.Drawing;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Entities
{
    /// <summary>
    /// Benda diam bergambar satu sprite, berjangkar di titik alasnya (X, Y).
    /// PEWARISAN: GameObject → StaticObject → (ResourceNode, Building, Fence, Bush).
    /// </summary>
    public abstract class StaticObject : GameObject
    {
        protected StaticObject(float x, float y, string sprite) : base(x, y) => Sprite = Art.Get(sprite);

        protected Sprite Sprite { get; set; }

        public override RectangleF Bounds => Sprite.BoundsAt(X, Y);

        /// <summary>Ukuran bayangan (s, m, l, xl); null = tanpa bayangan.</summary>
        protected virtual string? ShadowSize => null;

        public override void Draw(Renderer r, float time)
        {
            if (ShadowSize != null) DrawShadow(r, ShadowSize, X, Y);
            r.Draw(Sprite, X, Y);
        }

        /// <summary>Kotak tabrakan selebar 'width' dan setinggi 'height' tepat di atas titik alas.</summary>
        protected RectangleF FootBox(float width, float height, float lift = 0)
            => new(X - width / 2, Y - height - lift, width, height);
    }

    /// <summary>Semak: hiasan padat yang tidak bisa dihancurkan.</summary>
    public sealed class Bush : StaticObject
    {
        public Bush(float x, float y) : base(x, y, "bush") { }

        public override RectangleF Hitbox => FootBox(40, 16);
        protected override string? ShadowSize => "l";
    }
}
