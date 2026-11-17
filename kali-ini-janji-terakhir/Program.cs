namespace kali_ini_janji_terakhir
{
    internal static class Program
    {
        /// <summary>
        /// Titik masuk.
        ///   (tanpa argumen)      : Kebun versi WinForms (sprite dari Assets/Sprites).
        ///   --tandur             : versi OpenGL (OpenTK) lama.
        ///   --snapshot out.png   : alat developer, simpan screenshot permainan lalu keluar.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Contains("--tandur"))
            {
                using var tandur = new Tandur.TandurGame(Tandur.LaunchOptions.Parse(args));
                tandur.Run();
                return;
            }

            int snap = Array.IndexOf(args, "--snapshot");
            if (snap >= 0)
            {
                Farm.DevTools.Snapshot.Run(snap + 1 < args.Length ? args[snap + 1] : "snapshot.png");
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new GameForm());
        }
    }
}
