using System;
using System.Drawing;
using kali_ini_janji_terakhir.Farm;
using kali_ini_janji_terakhir.Farm.Engine;

namespace kali_ini_janji_terakhir.Core.Entities
{
    /// <summary>Sesuatu yang digambar di dunia dan diurutkan menurut kedalaman (Y-sorting).</summary>
    public interface IWorldDrawable
    {
        /// <summary>Kunci urutan gambar: yang kakinya lebih bawah digambar belakangan (tampak di depan).</summary>
        float SortY { get; }

        /// <summary>Kotak gambar, untuk culling (yang di luar layar tidak digambar).</summary>
        RectangleF Bounds { get; }

        void Draw(Renderer r, float time);
    }

    /// <summary>
    /// ABSTRAKSI: akar dari semua benda di dunia (pemain, warga, hewan, pohon, rumah).
    /// Dunia menyimpan semuanya dalam satu List&lt;GameObject&gt; lalu memanggil Update dan Draw
    /// tanpa tahu jenis aslinya: itulah POLIMORFISME.
    ///
    /// Posisi (X, Y) selalu titik kaki/alas benda. Sprite punya pivot di titik yang sama,
    /// jadi cukup r.Draw(sprite, X, Y).
    /// </summary>
    public abstract class GameObject : IWorldDrawable, ICollider
    {
        private int id = -1;

        protected GameObject(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Id tetap untuk save game. Diberikan sekali oleh dunia.</summary>
        public int Id => id;

        // ENKAPSULASI: posisi hanya bisa diubah oleh kelas itu sendiri dan turunannya.
        public float X { get; protected set; }
        public float Y { get; protected set; }

        public bool IsRemoved { get; private set; }

        public abstract RectangleF Bounds { get; }

        /// <summary>Kotak tabrakan. Default: tidak menghalangi.</summary>
        public virtual RectangleF Hitbox => RectangleF.Empty;

        /// <summary>Benda diam masuk grid spasial; turunan yang bergerak override jadi false.</summary>
        public virtual bool IsStatic => true;

        public virtual float SortY => Y;

        internal void AssignId(int newId)
        {
            if (id >= 0) throw new InvalidOperationException("Id sudah diberikan.");
            id = newId;
        }

        public void Remove() => IsRemoved = true;

        public virtual void Update(float dt, FarmWorld world) { }

        public abstract void Draw(Renderer r, float time);

        /// <summary>Bayangan di bawah benda. size: s, m, l, xl.</summary>
        protected static void DrawShadow(Renderer r, string size, float x, float y) => r.Draw("shadow_" + size, x, y - 2);

        protected static float Distance(float x1, float y1, float x2, float y2)
            => MathF.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
    }
}
