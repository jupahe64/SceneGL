using SceneGL.GLWrappers;
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
    internal class Instances
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

        public struct InstanceData
        {
            public Matrix4x4 Transform;

            public InstanceData(Matrix4x4 transform)
            {
                Transform = transform;
            }
        }

        private static bool s_initialized = false;

        private static uint s_instanceBuffer;
        private static uint s_sceneDataBuffer;

        private static RenderableModel? s_model;

        private static ShaderProgram? s_shaderProgram;
        private static uint s_texture;
        private static uint s_sampler;
        public static readonly ShaderSource VertexSource = new(
            "Instances.vert",
            ShaderType.VertexShader,
            @"#version 330
                layout (std140) uniform ubScene
                {
                    mat4x4 uViewProjection;
                };

                layout (std140) uniform ubInstanceData
                {
                    mat4x3 uInstanceData[1000];
                };

                layout (location = 0) in vec3 aPosition;
                layout (location = 1) in vec2 aTexCoord;
                layout (location = 2) in vec4 aColor;

                out vec2 vTexCoord;
                out vec4 vColor;

                void main() {
                    vTexCoord = aTexCoord;
                    vColor = aColor;

                    vec3 pos = uInstanceData[gl_InstanceID]*vec4(aPosition, 1.0);

                    gl_Position = uViewProjection*vec4(pos, 1.0);
                }
                "
            );

        public static readonly ShaderSource FragmentSource = new(
            "Instances.frag",
            ShaderType.FragmentShader,
            @"#version 330
                uniform vec4 uColor;
                uniform sampler2D uTex;

                in vec2 vTexCoord;
                in vec4 vColor;

                out vec4 oColor;

                void main() {
                    vec4 tex = texture(uTex, vTexCoord);
                    oColor = vColor+uColor+tex*tex.a*0.1;
                }
                "
            );

        public static void Initialize(GL gl)
        {
            if (s_initialized)
                return;

            s_initialized = true;


            s_shaderProgram = new ShaderProgram(VertexSource, FragmentSource);

            s_instanceBuffer = gl.GenBuffer();
            s_sceneDataBuffer = gl.GenBuffer();

            //texture
            {
                var image = Image.Load<Rgba32>(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "res", "OpenGL_White_500px_June16.png"));

                var pixelData = new Rgba32[image.Width * image.Height];

                image.CopyPixelDataTo(pixelData);

                s_texture = TextureHelper.CreateTexture2D<Rgba32>(gl, InternalFormat.Rgba, (uint)image.Width, (uint)image.Height,
                    PixelFormat.Rgba, pixelData, true);
            }

            //sampler
            {
                s_sampler = gl.CreateSampler();
                gl.SamplerParameterI(s_sampler, SamplerParameterI.MagFilter, (int)TextureMagFilter.Linear);
                gl.SamplerParameterI(s_sampler, SamplerParameterI.MinFilter, (int)TextureMagFilter.Linear);
            }


            #region Cube Model
            {

                var builder = new ModelBuilder<Vertex>();

                float BEVEL = 0.1f;

                Vector4 defaultColor = new(0, 0, 0, 1);
                Vector4 lineColor = new(1, 1, 1, 1);

                Matrix4x4 mtx;

                #region Transform Helpers
                void Reset() => mtx = Matrix4x4.CreateScale(0.5f);

                static void Rotate(ref float x, ref float y)
                {
                    var _x = x;
                    x = y;
                    y = -_x;
                }

                void RotateOnX()
                {
                    Rotate(ref mtx.M12, ref mtx.M13);
                    Rotate(ref mtx.M22, ref mtx.M23);
                    Rotate(ref mtx.M32, ref mtx.M33);
                }

                void RotateOnY()
                {
                    Rotate(ref mtx.M11, ref mtx.M13);
                    Rotate(ref mtx.M21, ref mtx.M23);
                    Rotate(ref mtx.M31, ref mtx.M33);
                }

                void RotateOnZ()
                {
                    Rotate(ref mtx.M11, ref mtx.M12);
                    Rotate(ref mtx.M21, ref mtx.M22);
                    Rotate(ref mtx.M31, ref mtx.M32);
                }

                #endregion

                float w = 1 - BEVEL;
                float m = 1;


                #region Cube part Helpers
                void Face()
                {
                    builder!.AddPlane(
                        new Vertex{ Position = Vector3.Transform(new Vector3(-w, 1, -w), mtx), Color = defaultColor, UV = new Vector2(0,0) },
                        new Vertex{ Position = Vector3.Transform(new Vector3(w, 1, -w), mtx), Color = defaultColor, UV = new Vector2(1, 0) },
                        new Vertex{ Position = Vector3.Transform(new Vector3(-w, 1, w), mtx), Color = defaultColor, UV = new Vector2(0, 1) },
                        new Vertex{ Position = Vector3.Transform(new Vector3(w, 1, w), mtx), Color = defaultColor, UV = new Vector2(1, 1) }
                    );
                }

                void Bevel()
                {
                    builder!.AddPlane(
                        new Vertex { Position = Vector3.Transform(new Vector3(-w, 1, w), mtx), Color = defaultColor },
                        new Vertex { Position = Vector3.Transform(new Vector3(w, 1, w), mtx), Color = defaultColor },
                        new Vertex { Position = Vector3.Transform(new Vector3(-w, m, m), mtx), Color = lineColor },
                        new Vertex { Position = Vector3.Transform(new Vector3(w, m, m), mtx), Color = lineColor }
                    );

                    builder!.AddPlane(
                        new Vertex { Position = Vector3.Transform(new Vector3(-w, m, m), mtx), Color = lineColor },
                        new Vertex { Position = Vector3.Transform(new Vector3(w, m, m), mtx), Color = lineColor },
                        new Vertex { Position = Vector3.Transform(new Vector3(-w, w, 1), mtx), Color = defaultColor },
                        new Vertex { Position = Vector3.Transform(new Vector3(w, w, 1), mtx), Color = defaultColor }
                    );
                }

                void BevelCorner()
                {
                    void Piece(Vector3 v1, Vector3 v2, Vector3 v3)
                    {
                        Vector3 vm = new(m, m, m);

                        builder!.AddTriangle(
                            new Vertex { Position = Vector3.Transform(v1, mtx), Color = defaultColor },
                            new Vertex { Position = Vector3.Transform(vm, mtx), Color = lineColor },
                            new Vertex { Position = Vector3.Transform(v2, mtx), Color = lineColor }
                        );

                        builder!.AddTriangle(
                            new Vertex { Position = Vector3.Transform(v2, mtx), Color = lineColor },
                            new Vertex { Position = Vector3.Transform(vm, mtx), Color = lineColor },
                            new Vertex { Position = Vector3.Transform(v3, mtx), Color = defaultColor }
                        );
                    }

                    Piece(new Vector3(w, w, 1), new Vector3(w, m, m), new Vector3(w, 1, w));
                    Piece(new Vector3(w, 1, w), new Vector3(m, m, w), new Vector3(1, w, w));
                    Piece(new Vector3(1, w, w), new Vector3(m, w, m), new Vector3(w, w, 1));
                }
                #endregion


                #region Construction

                Reset();

                #region Faces
                Face();
                RotateOnX();

                for (int i = 0; i < 4; i++)
                {
                    Face();
                    RotateOnY();
                }
                RotateOnX();
                Face();
                #endregion

                Reset();

                #region Edges/Bevels
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        Bevel();
                        RotateOnY();
                    }

                    RotateOnZ();
                }
                #endregion

                Reset();

                #region Corners/BevelCorners
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        BevelCorner();
                        RotateOnY();
                    }

                    RotateOnZ();
                }
                #endregion

                #endregion


                s_model = builder.GetModel(gl);

            }
            #endregion
        }

        public static void Render(GL gl, ref Vector4 color, in Matrix4x4 viewProjection, ReadOnlySpan<InstanceData> instanceData)
        {
            if (!s_initialized)
                throw new InvalidOperationException($@"{nameof(ColoredTriangle)} must be initialized before any calls to {nameof(Render)}");


            if (s_shaderProgram!.TryUse(gl, out uint program))
            {
                int nextFreeUnit = 0;
                uint nextFreeBlockBinding = 0;

                if (s_shaderProgram.TryGetUniformLoc("uColor", out int loc))
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


                bool usesInstanceData = false;

                uint instanceDataBlockBinding = uint.MaxValue;

                if (s_shaderProgram.TryGetUniformBlockIndex("ubInstanceData", out uint blockIndex))
                {
                    gl.BindBuffer(BufferTargetARB.UniformBuffer, s_instanceBuffer);
                    gl.BufferData(BufferTargetARB.UniformBuffer, instanceData, BufferUsageARB.DynamicDraw);
                    gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);


                    usesInstanceData = true;
                    instanceDataBlockBinding = nextFreeBlockBinding;

                    gl.UniformBlockBinding(program, blockIndex, nextFreeBlockBinding);
                    nextFreeBlockBinding++;
                }

                if (s_shaderProgram.TryGetUniformBlockIndex("ubScene", out blockIndex))
                {
                    Span<float> floats = stackalloc float[4 * 4];

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

                    gl.BindBuffer(BufferTargetARB.UniformBuffer, s_sceneDataBuffer);
                    gl.BufferData<float>(BufferTargetARB.UniformBuffer, floats, BufferUsageARB.DynamicDraw);
                    gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);

                    gl.UniformBlockBinding(program, blockIndex, nextFreeBlockBinding);

                    gl.BindBufferBase(BufferTargetARB.UniformBuffer, nextFreeBlockBinding, s_sceneDataBuffer);
                    nextFreeBlockBinding++;
                }

                if (usesInstanceData)
                {
                    //gl.GetInteger(GetPName.MaxUniformBlockSize, out int maxBlockSize);



                    unsafe
                    {
                        for (int i = 0; i < (instanceData.Length+999)/1000; i++)
                        {
                            gl.BindBufferRange(BufferTargetARB.UniformBuffer, instanceDataBlockBinding, s_instanceBuffer, 
                                i * (sizeof(InstanceData) * 1000), (nuint)(sizeof(InstanceData) * 1000));

                            s_model!.Draw(gl, 1000);
                        }
                    }
                }
                else
                {
                    s_model!.Draw(gl, (uint)instanceData.Length);
                }

                

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
