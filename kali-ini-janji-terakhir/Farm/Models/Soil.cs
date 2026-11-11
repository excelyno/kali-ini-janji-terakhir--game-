namespace kali_ini_janji_terakhir.Farm
{
    /// <summary>
    /// Represents an individual tilled plot of soil on the farm grid.
    /// </summary>
    public class Soil
    {
        public bool Watered { get; set; }
        public int Crop { get; set; } = -1;
        public int Age { get; set; }
    }
}

