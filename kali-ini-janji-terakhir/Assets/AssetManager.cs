using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace kali_ini_janji_terakhir.Assets
{
    /// <summary>
    /// Central manager for loading, caching, and releasing game graphical assets.
    /// </summary>
    public class AssetManager : IDisposable
    {
        private static AssetManager? instance;
        public static AssetManager Instance => instance ??= new AssetManager();

        private readonly Dictionary<string, Bitmap> textures = new(StringComparer.OrdinalIgnoreCase);

        public static string AssetDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "FileAssets");

        public static string GetAssetPath(string fileName) => Path.Combine(AssetDirectory, fileName);

        public Bitmap LoadTexture(string key, string path)
        {
            if (textures.TryGetValue(key, out var cached))
                return cached;

            if (!File.Exists(path))
                throw new FileNotFoundException($"Texture file not found: {path}", path);

            Bitmap texture = new Bitmap(path);
            textures[key] = texture;
            return texture;
        }

        public Bitmap LoadProcessed(string key, string fileName, Func<string, Bitmap> processor)
        {
            if (textures.TryGetValue(key, out var cached))
                return cached;

            string fullPath = GetAssetPath(fileName);
            Bitmap processed = processor(fullPath);
            textures[key] = processed;
            return processed;
        }

        public Bitmap LoadScaled(string key, string fileName, int w, int h, bool crop = true)
        {
            return LoadProcessed(key, fileName, path => ImageProcessor.Scale(path, w, h, crop));
        }

        public Bitmap LoadFit(string key, string fileName, int box)
        {
            return LoadProcessed(key, fileName, path => ImageProcessor.Fit(path, box));
        }

        public Bitmap? GetTexture(string key)
        {
            return textures.TryGetValue(key, out var texture) ? texture : null;
        }

        public bool TryGetTexture(string key, out Bitmap texture)
        {
            return textures.TryGetValue(key, out texture!);
        }

        public void Dispose()
        {
            foreach (Bitmap texture in textures.Values)
            {
                texture.Dispose();
            }
            textures.Clear();
        }
    }
}
