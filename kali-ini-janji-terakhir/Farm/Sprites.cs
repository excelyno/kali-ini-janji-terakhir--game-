using System.Drawing;
using kali_ini_janji_terakhir.Assets;

namespace kali_ini_janji_terakhir.Farm
{
    /// <summary>
    /// Provides strongly-typed access to all cached sprite resources used across the game.
    /// </summary>
    public static class Sprites
    {
        // World & Nature
        public static Bitmap Tree = null!;
        public static Bitmap Rock = null!;
        public static Bitmap Fence = null!;
        public static Bitmap Fence2 = null!;
        public static Bitmap Shop = null!;
        public static Bitmap Crates = null!;
        public static Bitmap House = null!;
        public static Bitmap Chicken = null!;
        public static Bitmap Harvest = null!;
        public static Bitmap Pond = null!;

        // Soil & Crops
        public static Bitmap SoilTilled = null!;
        public static Bitmap SoilBare = null!;
        public static Bitmap SproutS = null!;
        public static Bitmap SproutM = null!;
        public static Bitmap SeedIcon = null!;

        // Tools & Items
        public static Bitmap Hoe = null!;
        public static Bitmap Axe = null!;
        public static Bitmap Pick = null!;
        public static Bitmap Can = null!;
        public static Bitmap Hand = null!;
        public static Bitmap Backpack = null!;

        // Character / Animations & UI
        public static Bitmap PFront = null!;
        public static Bitmap PBack = null!;
        public static Bitmap PRight = null!;
        public static Bitmap PRun = null!;
        public static Bitmap PDown1 = null!;
        public static Bitmap PDown2 = null!;
        public static Bitmap Hero = null!;

        private static bool loaded;

        /// <summary>Loads and caches every sprite once; the bitmaps live for the whole process.</summary>
        public static void Load()
        {
            if (loaded) return;
            var am = AssetManager.Instance;

            // World & Obstacles
            Tree = am.LoadScaled("Tree", "pohon.png", 0, 170);
            Rock = am.LoadScaled("Rock", "batu.png", 48, 0);
            Fence = am.LoadScaled("Fence", "pagar.png", 336, 0);
            Fence2 = am.LoadScaled("Fence2", "pagar-2.png", 336, 0);
            Shop = am.LoadScaled("Shop", "toko-bu-lastri.png", 190, 0);
            Crates = am.LoadScaled("Crates", "peti-barang.png", 0, 64);
            House = am.LoadScaled("House", "Rumah.png", 290, 0);
            Chicken = am.LoadScaled("Chicken", "ayam.png", 40, 0);
            Harvest = am.LoadScaled("Harvest", "harvest-tanaman(dummy).png", 42, 0);
            Pond = am.LoadScaled("Pond", "KolamAir.png", 300, 0);

            // Soil & Sprout
            SoilTilled = am.LoadScaled("SoilTilled", "Tanah(Dibajak).png", 48, 48, crop: false);
            SoilBare = am.LoadScaled("SoilBare", "Tanah(tanpaRumput).png", 48, 48, crop: false);
            SproutS = am.LoadScaled("SproutS", "BenihTumbuh(keciltransparant).png", 0, 16);
            SproutM = am.LoadScaled("SproutM", "BenihTumbuh(keciltransparant).png", 0, 30);
            SeedIcon = am.LoadFit("SeedIcon", "benihTumpuh(kecil).png", 34);

            // Tools & Icons
            Hoe = am.LoadFit("Hoe", "Cangkul.png", 36);
            Can = am.LoadFit("Can", "Shower.png", 36);
            Axe = am.LoadFit("Axe", "Kapak.png", 36);
            Pick = am.LoadFit("Pick", "Beliung(Pickaxe).png", 36);
            Hand = am.LoadFit("Hand", "Hand.png", 26);
            Backpack = am.LoadFit("Backpack", "BackPack.png", 34);

            // Player & Hero Screen
            PFront = am.LoadScaled("PFront", "Karakter(MenghadapDepan).png", 0, 84);
            PBack = am.LoadScaled("PBack", "Karakter(MenghadapBelakang(keatas)).png", 0, 84);
            PRight = am.LoadScaled("PRight", "Karakter(JalanKanan).png", 0, 84);
            PRun = am.LoadScaled("PRun", "Karakter(LariKanan).png", 0, 84);
            PDown1 = am.LoadScaled("PDown1", "Karakter(jalanKebawah).png", 0, 84);
            PDown2 = am.LoadScaled("PDown2", "Karakter(JalanKebawah2).png", 0, 84);
            Hero = am.LoadScaled("Hero", "HeroSection(homeScreen).png", 0, 640);

            loaded = true;
        }
    }
}
