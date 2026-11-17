using System;
using System.Drawing;
using System.Windows.Forms;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using kali_ini_janji_terakhir.Engine.Rendering;
using kali_ini_janji_terakhir.Farm;
using System.Diagnostics;
using System.Collections.Generic;

namespace kali_ini_janji_terakhir.Forms
{
    public class GLGamePanel : GLControl
    {
        private Renderer? _renderer;
        private System.Windows.Forms.Timer _timer;
        private Stopwatch _sw;
        private double _lastTime;

        private FarmGame _game;
        private Texture? _texGround;
        private Dictionary<Bitmap, Texture> _texCache = new();

        private Vector3 _cameraPos = new Vector3(0, 0, 0);

        public GLGamePanel()
        {
            Dock = DockStyle.Fill;
            
            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _sw = Stopwatch.StartNew();
            
            Load += OnLoad;
            Paint += OnPaint;
            Resize += OnResize;

            Sprites.Load();
            _game = new FarmGame();
        }

        public void KeyDownEvt(Keys k) { _game.KeyDown(k); }
        public void KeyUpEvt(Keys k) { _game.KeyUp(k); }
        public void WheelEvt(int d) { _game.Wheel(d); }
        public void LoseFocusEvt() { _game.LoseFocus(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _game.Click();
        }

        private Texture GetTexture(Bitmap bmp)
        {
            if (!_texCache.TryGetValue(bmp, out var tex))
            {
                tex = new Texture(bmp);
                _texCache[bmp] = tex;
            }
            return tex;
        }

        private void OnLoad(object? sender, EventArgs e)
        {
            MakeCurrent();
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _renderer = new Renderer();
            
            _texGround = new Texture(_game.Ground);

            _timer.Tick += (s, ev) => {
                double now = _sw.Elapsed.TotalSeconds;
                float dt = (float)(now - _lastTime);
                _lastTime = now;
                UpdateGame(dt);
                Invalidate();
            };
            _timer.Start();
        }

        private void UpdateGame(float dt)
        {
            if (_game.RestartRequested) 
            {
                _game.Dispose();
                _game = new FarmGame();
                _texGround = new Texture(_game.Ground);
            }
            
            _game.Update(dt);
        }

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (_renderer != null)
            {
                float vw = ClientSize.Width;
                float vh = ClientSize.Height;
                
                // Camera follow player
                float targetCamX = Math.Clamp(_game.Px - vw / 2f, 0, Math.Max(0, 40*48 - vw));
                float targetCamY = Math.Clamp(_game.Py - vh / 2f, 0, Math.Max(0, 30*48 - vh));
                _cameraPos = new Vector3(targetCamX, targetCamY, 0);
                _game.CamX = targetCamX;
                _game.CamY = targetCamY;

                _renderer.SetupCamera(ClientSize.Width, ClientSize.Height, _cameraPos);
                
                // Draw Ground (Z = -0.99 so it's at the back)
                _renderer.DrawQuad(new Vector2(0, 0), new Vector2(_game.Ground.Width, _game.Ground.Height), -0.99f, _texGround);

                // Draw Pond
                Texture texPond = GetTexture(Sprites.Pond);
                _renderer.DrawQuad(new Vector2(1240, 390), new Vector2(texPond.Width, texPond.Height), -390f / 10000f, texPond);

                // Draw Soil and Crops
                Texture texSoil = GetTexture(Sprites.SoilTilled);
                foreach (var kv in _game.SoilData)
                {
                    int tx = kv.Key.X * 48;
                    int ty = kv.Key.Y * 48;
                    _renderer.DrawQuad(new Vector2(tx, ty), new Vector2(48, 48), -0.98f, texSoil);

                    if (kv.Value.Crop >= 0)
                    {
                        int c = kv.Value.Crop;
                        float p = kv.Value.Age / (float)CropCatalog.GrowDays[c];
                        Texture texCrop;
                        if (p < 0.34f) texCrop = GetTexture(Sprites.SproutS);
                        else if (p < 1f) texCrop = GetTexture(Sprites.SproutM);
                        else texCrop = GetTexture(Sprites.Harvest);
                        
                        int cx = tx + 24;
                        int by = ty + 40;
                        _renderer.DrawQuad(new Vector2(cx - texCrop.Width / 2f, by - texCrop.Height), new Vector2(texCrop.Width, texCrop.Height), -(ty + 48) / 10000f, texCrop);
                    }
                }

                // Buildings & Static objects
                Texture texShop = GetTexture(Sprites.Shop);
                _renderer.DrawQuad(new Vector2(360, 625), new Vector2(texShop.Width, texShop.Height), -((625f + texShop.Height) / 10000f), texShop);

                Texture texCrates = GetTexture(Sprites.Crates);
                _renderer.DrawQuad(new Vector2(312, 696), new Vector2(texCrates.Width, texCrates.Height), -((696f + texCrates.Height) / 10000f), texCrates);

                Texture texFence = GetTexture(Sprites.Fence);
                _renderer.DrawQuad(new Vector2(17 * 48, 350), new Vector2(336, texFence.Height), -((350f + texFence.Height) / 10000f), texFence);

                // Draw Objects with Y-Sorting
                foreach (var obj in _game.Objs)
                {
                    Texture? tex = obj.Kind switch {
                        ObjKind.Tree => GetTexture(Sprites.Tree),
                        ObjKind.Rock => GetTexture(Sprites.Rock),
                        ObjKind.House => GetTexture(Sprites.House),
                        ObjKind.Chicken => GetTexture(Sprites.Chicken),
                        _ => null
                    };
                    
                    if (tex != null)
                    {
                        float yPos = obj.Y;
                        float bob = 0;
                        if (obj.Kind == ObjKind.Chicken)
                            bob = (float)Math.Sin(_game.Anim * 4 + obj.X) * 2;

                        // Z is negative Y to draw lower items on top
                        _renderer.DrawQuad(new Vector2(obj.Bounds.X, obj.Bounds.Y + bob), new Vector2(tex.Width, tex.Height), -yPos / 10000f, tex);
                    }
                }

                // Draw Player
                bool run = _game.Moving;
                int frame = ((int)(_game.Walk / 2f)) & 1;
                Bitmap pImg;
                if (_game.Fdx != 0) { pImg = run ? Sprites.PRun : Sprites.PRight; }
                else if (_game.Fdy < 0) { pImg = Sprites.PBack; }
                else pImg = !_game.Moving ? Sprites.PFront : (frame == 0 ? Sprites.PDown1 : Sprites.PDown2);

                Texture pTex = GetTexture(pImg);
                float pBob = _game.Moving ? MathF.Abs(MathF.Sin(_game.Walk)) * 3 : 0;
                float px = _game.Px - pImg.Width / 2f;
                float py = _game.Py - pImg.Height + 6 - pBob;
                
                _renderer.DrawQuad(new Vector2(px, py), new Vector2(pTex.Width, pTex.Height), -_game.Py / 10000f, pTex);

                // Draw UI Overlay via GDI+ to Texture
                using (Bitmap uiBmp = new Bitmap(ClientSize.Width, ClientSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(uiBmp))
                    {
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        _game.DrawOverlay(g);
                    }
                    using (Texture uiTex = new Texture(uiBmp))
                    {
                        // Reset camera for UI (ortho screen space)
                        _renderer.SetupCamera(ClientSize.Width, ClientSize.Height, Vector3.Zero);
                        _renderer.DrawQuad(new Vector2(0, 0), new Vector2(vw, vh), -50.0f, uiTex);
                    }
                }
            }

            SwapBuffers();
        }

        private void OnResize(object? sender, EventArgs e)
        {
            MakeCurrent();
            GL.Viewport(0, 0, ClientSize.Width, ClientSize.Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
                _renderer?.Dispose();
                _game?.Dispose();
                _texGround?.Dispose();
                foreach (var tex in _texCache.Values) tex.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
