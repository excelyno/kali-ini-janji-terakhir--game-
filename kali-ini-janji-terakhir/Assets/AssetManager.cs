using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;

namespace kali_ini_janji_terakhir.Assets
{
    public class AssetManager
    {
        private readonly Dictionary<string, Bitmap> textures = new();
        public Bitmap LoadTexture(string key, string path)
        {
            if (textures.ContainsKey(key))
            {
                return textures[key];
            }

            if(!File.Exists(path))
            {
                throw new FileNotFoundException($"Texture file not found: {path}");
            }

            Bitmap texture = new Bitmap(path);

            textures.Add(key,texture);
            return texture;
        }

        public Bitmap GetTexture(string key)
        {
            if(textures.TryGetValue(key, out Bitmap texture))
            {
                return texture;
            }
            return null;

        }

        public void Dispose()
        {
            foreach(Bitmap texture in textures.Values)
            {
                texture.Dispose();
            }
            textures.Clear();
        }
    }
}
