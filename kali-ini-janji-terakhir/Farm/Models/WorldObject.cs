using System.Drawing;

namespace kali_ini_janji_terakhir.Farm
{
    public enum ObjKind
    {
        Tree,
        Rock,
        House,
        Chicken
    }

    /// <summary>
    /// Represents an entity or obstacle in the farm world (trees, rocks, house, animals).
    /// </summary>
    public class WorldObject
    {
        public int Id { get; set; }
        public ObjKind Kind { get; set; }
        public int Hp { get; set; } = 3;
        public float X { get; set; }
        public float Y { get; set; }
        public Rectangle Solid { get; set; }
        public Rectangle Bounds { get; set; }
    }
}

