using System.Drawing;

namespace kali_ini_janji_terakhir.Farm
{
    /// <summary>
    /// Temporary floating text animation displayed when gathering resources, harvesting, or taking actions.
    /// </summary>
    public class Floater
    {
        public string Text { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Life { get; set; } = 1.4f;
        public Color Color { get; set; }

        /// <summary>
        /// Convenient alias for <see cref="Color"/>.
        /// </summary>
        public Color C
        {
            get => Color;
            set => Color = value;
        }
    }
}

