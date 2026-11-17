using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using kali_ini_janji_terakhir.Engine.Shaders;

namespace kali_ini_janji_terakhir.Engine.Rendering
{
    public class Renderer : IDisposable
    {
        private Shader _shader;
        private int _vao;
        private int _vbo;

        // Quad vertices: x, y, z, u, v
        private readonly float[] _vertices = {
             0.5f,  0.5f, 0.0f,  1.0f, 1.0f, // bottom right
             0.5f, -0.5f, 0.0f,  1.0f, 0.0f, // top right
            -0.5f, -0.5f, 0.0f,  0.0f, 0.0f, // top left
            -0.5f,  0.5f, 0.0f,  0.0f, 1.0f  // bottom left
        };
        
        private readonly uint[] _indices = {
            0, 1, 3, // first triangle
            1, 2, 3  // second triangle
        };
        private int _ebo;

        public Renderer()
        {
            _shader = new Shader("Engine/Shaders/default.vert", "Engine/Shaders/default.frag");

            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            _ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

            // Position
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // TexCoord
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
        }

        public void SetupCamera(int width, int height, Vector3 cameraPos)
        {
            GL.Viewport(0, 0, width, height);
            
            // Orthographic projection for 2D/2.5D look
            var projection = Matrix4.CreateOrthographicOffCenter(0, width, height, 0, -100f, 100f);
            // Simple view matrix following camera
            var view = Matrix4.CreateTranslation(-cameraPos.X, -cameraPos.Y, 0);

            _shader.Use();
            _shader.SetMatrix4("projection", projection);
            _shader.SetMatrix4("view", view);

            // Set lighting (Example 2.5D lighting)
            _shader.SetVector3("ambientColor", new Vector3(0.4f, 0.4f, 0.4f));
            _shader.SetVector3("lightColor", new Vector3(1.0f, 0.9f, 0.7f)); // Warm light
            _shader.SetVector3("lightPos", new Vector3(cameraPos.X + width/2f, cameraPos.Y + height/2f, 50f)); // Light in center of screen
        }

        public void DrawQuad(Vector2 position, Vector2 size, float zIndex, Texture? texture = null)
        {
            if (texture != null)
                texture.Use();

            _shader.Use();

            var model = Matrix4.Identity;
            model *= Matrix4.CreateScale(size.X, size.Y, 1.0f);
            // Posisi (Translate)
            model *= Matrix4.CreateTranslation(position.X + size.X / 2f, position.Y + size.Y / 2f, zIndex);
            
            _shader.SetMatrix4("model", model);

            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
        }

        public void Dispose()
        {
            _shader.Dispose();
            GL.DeleteBuffer(_vbo);
            GL.DeleteBuffer(_ebo);
            GL.DeleteVertexArray(_vao);
        }
    }
}
