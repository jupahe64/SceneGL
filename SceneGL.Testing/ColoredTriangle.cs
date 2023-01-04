using SceneGL.GLHelpers;
using SceneGL.GLWrappers;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.Testing
{
    internal static class ColoredTriangle
    {
        private struct Vertex
        {
            [VertexAttribute(AttributeShaderLoc.Loc0, 3, VertexAttribPointerType.Float, normalized: false)]
            public Vector3 Position;

            [VertexAttribute(AttributeShaderLoc.Loc1, 2, VertexAttribPointerType.Float, normalized: false)]
            public Vector2 UV;

            [VertexAttribute(AttributeShaderLoc.Loc2, 4, VertexAttribPointerType.Float, normalized: false)]
            public Vector4 Color;
        }

        private static readonly Vertex[] s_data = new[]
        {
            new Vertex{Position = new Vector3(+0,+1,0), UV = new Vector2(0.5f, 1.0f), Color = new Vector4(1.0f, 1.0f, 0.0f, 1.0f)},
            new Vertex{Position = new Vector3(-1,-1,0), UV = new Vector2(0.0f, 0.0f), Color = new Vector4(0.0f, 1.0f, 1.0f, 1.0f)},
            new Vertex{Position = new Vector3(+1,-1,0), UV = new Vector2(1.0f, 0.0f), Color = new Vector4(1.0f, 0.0f, 1.0f, 1.0f)},
        };

        private static bool s_initialized = false;

        private static RenderableModel? s_model;

        private static ShaderProgram? s_shaderProgram;
        private static uint s_texture;
        private static uint s_sampler;
        public static readonly ShaderSource VertexSource = new(
            "ColoredTriangle.vert",
            ShaderType.VertexShader,"""
                #version 330

                uniform mat4x3 uTransform;
                uniform mat4x4 uViewProjection;
                layout (location = 0) in vec3 aPosition;
                layout (location = 1) in vec2 aTexCoord;
                layout (location = 2) in vec4 aColor;

                out vec2 vTexCoord;
                out vec4 vColor;

                void main() {
                    vTexCoord = aTexCoord;
                    vColor = aColor;

                    vec3 pos = uTransform*vec4(aPosition, 1.0);

                    gl_Position = uViewProjection*vec4(pos, 1.0);
                }
                """
            );

        public static readonly ShaderSource FragmentSource = new(
            "ColoredTriangle.frag",
            ShaderType.FragmentShader, """
                #version 330

                uniform vec4 uColor;
                uniform sampler2D uTex;

                in vec2 vTexCoord;
                in vec4 vColor;

                out vec4 oColor;

                void main() {
                    vec4 tex = texture(uTex, 
                       vTexCoord.x*vec2(0.707,0.707)*3+
                       vTexCoord.y*vec2(0.707,-0.707)*3);
                    oColor = vColor+uColor+tex*tex.a*0.1;
                }
                """
            );

        public static void Initialize(GL gl)
        {
            if (s_initialized)
                return;

            s_initialized = true;


            s_shaderProgram = new ShaderProgram(VertexSource, FragmentSource);

            //texture
            {
                var image = Image.Load<Rgba32>(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "res", "OpenGL_White_500px_June16.png"));

                var pixelData = new Rgba32[image.Width * image.Height];

                image.CopyPixelDataTo(pixelData);

                s_texture = TextureHelper.CreateTexture2D<Rgba32>(gl, InternalFormat.Rgba, (uint)image.Width, (uint)image.Height,
                    PixelFormat.Rgba, pixelData, true);
            }

            s_sampler = SamplerHelper.CreateMipMapSampler2D(gl, lodBias: -3);
            
            s_model = RenderableModel.Create<Vertex>(gl, s_data);
        }

        public static void Render(GL gl, ref Vector4 color, in Matrix4x4 transform, in Matrix4x4 viewProjection)
        {
            if (!s_initialized)
                throw new InvalidOperationException($@"{nameof(ColoredTriangle)} must be initialized before any calls to {nameof(Render)}");


            if (s_shaderProgram!.TryUse(gl, out _))
            {
                int nextFreeUnit = 0;

                if(s_shaderProgram.TryGetUniformLoc("uColor", out int loc))
                {
                    gl.Uniform4(loc, ref color);
                }

                if (s_shaderProgram.TryGetUniformLoc("uTex", out loc))
                {
                    gl.BindTextureUnit((uint)nextFreeUnit, s_texture);
                    gl.BindSampler((uint)nextFreeUnit, s_sampler);
                    gl.Uniform1(loc, nextFreeUnit);

                    nextFreeUnit++;
                }

                if (s_shaderProgram.TryGetUniformLoc("uTransform", out loc))
                {
                    Span<float> floats = stackalloc float[3 * 4];

                    const int N = 3;

                    {
                        var mtx = transform;

                        floats[(1 - 1) * N + (1 - 1)] = mtx.M11;
                        floats[(1 - 1) * N + (2 - 1)] = mtx.M12;
                        floats[(1 - 1) * N + (3 - 1)] = mtx.M13;

                        floats[(2 - 1) * N + (1 - 1)] = mtx.M21;
                        floats[(2 - 1) * N + (2 - 1)] = mtx.M22;
                        floats[(2 - 1) * N + (3 - 1)] = mtx.M23;

                        floats[(3 - 1) * N + (1 - 1)] = mtx.M31;
                        floats[(3 - 1) * N + (2 - 1)] = mtx.M32;
                        floats[(3 - 1) * N + (3 - 1)] = mtx.M33;

                        floats[(4 - 1) * N + (1 - 1)] = mtx.M41;
                        floats[(4 - 1) * N + (2 - 1)] = mtx.M42;
                        floats[(4 - 1) * N + (3 - 1)] = mtx.M43;
                    }


                    gl.UniformMatrix4x3(loc, 1, false, in floats[0]);
                }

                if (s_shaderProgram.TryGetUniformLoc("uViewProjection", out loc))
                {
                    Span<float> floats = stackalloc float[4 * 4];

                    const int N = 4;

                    {
                        int i = 0;
                        var mtx = viewProjection;

                        floats[i++] = mtx.M11;
                        floats[i++] = mtx.M12;
                        floats[i++] = mtx.M13;
                        floats[i++] = mtx.M14;

                        floats[i++] = mtx.M21;
                        floats[i++] = mtx.M22;
                        floats[i++] = mtx.M23;
                        floats[i++] = mtx.M24;

                        floats[i++] = mtx.M31;
                        floats[i++] = mtx.M32;
                        floats[i++] = mtx.M33;
                        floats[i++] = mtx.M34;

                        floats[i++] = mtx.M41;
                        floats[i++] = mtx.M42;
                        floats[i++] = mtx.M43;
                        floats[i++] = mtx.M44;
                    }

                    gl.UniformMatrix4(loc, 1, false, in floats[0]);
                }

                s_model!.Draw(gl);

                gl.UseProgram(0);

                gl.BindBufferBase(BufferTargetARB.UniformBuffer, 0, 0);
            }
            else
            {
                Debugger.Break();
            }
            
        }

        public static void CleanUp(GL gl)
        {
            s_model?.CleanUp(gl);
        }
    }
}
