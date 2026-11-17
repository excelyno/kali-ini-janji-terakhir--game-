namespace kali_ini_janji_terakhir.Farm
{
    public sealed partial class FarmGame
    {
        public static bool HasSave => SaveSystem.Load() != null;

        public void Save()
        {
            if (world.IsFinished) return;
            SaveSystem.Save(world.ToSave());
        }

        private void ApplySave()
        {
            var d = SaveSystem.Load();
            if (d == null) return;
            world.Restore(d);
            world.Say($"Selamat datang kembali! Hari ke-{world.Clock.Day}.");
        }
    }
}
