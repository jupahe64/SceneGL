using SceneGL.GLWrappers;
using SceneGL.GLHelpers;
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
        private static MaterialShader? s_materialShader;
        private static RenderableModel? s_model;

        private static ShaderProgram? s_shaderProgram;
        private static uint s_texture;
        private static uint s_sampler;
        public static readonly ShaderSource VertexSource = new(
            "Instances.vert",
            ShaderType.VertexShader,"""
                #version 330

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
                out float vColorFade;

                void main() {
                    vTexCoord = aTexCoord;
                    vColor = aColor;

                    vec3 centerPos = uInstanceData[gl_InstanceID]*vec4(0.0, 0.0, 0.0, 1.0);
                    vec4 centerPosProj = uViewProjection*vec4(centerPos, 1.0);

                    vec3 pos = uInstanceData[gl_InstanceID]*vec4(aPosition, 1.0);

                    float scaleFade = clamp(abs(centerPosProj.w*0.3-12)-9,0.0,1.0);
                    vColorFade = clamp(abs(centerPosProj.w*0.3-12)-8.5,0.0,1.0);

                    vec3 finalPos = mix(pos, centerPos, scaleFade);

                    gl_Position = uViewProjection*vec4(finalPos, 1.0);
                }
                """
            );

        public static readonly ShaderSource FragmentSource = new(
            "Instances.frag",
            ShaderType.FragmentShader,"""
                #version 330

                layout (std140) uniform ubMaterial
                {
                    vec4 uColor;
                };
                
                uniform sampler2D uTex;

                in vec2 vTexCoord;
                in vec4 vColor;
                in float vColorFade;

                out vec4 oColor;

                void main() {
                    vec4 tex = texture(uTex, vTexCoord);
                    oColor = vColor+uColor+tex*tex.a*0.1;
                    oColor = mix(oColor, vec4(1.0), vColorFade);
                }
                """
            );

        public static void Initialize(GL gl)
        {
            if (s_initialized)
                return;

            s_initialized = true;


            s_shaderProgram = new ShaderProgram(VertexSource, FragmentSource);

            s_instanceBuffer = gl.GenBuffer();
            ObjectLabelHelper.SetBufferLabel(gl, s_instanceBuffer, "Instances.InstanceBuffer");

            s_sceneDataBuffer = gl.GenBuffer();
            ObjectLabelHelper.SetBufferLabel(gl, s_sceneDataBuffer, "Instances.SceneDataBuffer");

            s_materialShader = new MaterialShader(s_shaderProgram, 
                sceneBlockBinding: "ubScene",
                materialBlockBinding: "ubMaterial",
                instanceDataBlock: ("ubInstanceData", 1000));

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

        public static Material<Vector4> CreateMaterial(Vector4 color)
        {
            if (!s_initialized)
                throw new InvalidOperationException($@"{nameof(Instances)} must be initialized before any calls to {nameof(CreateMaterial)}");

            return new Material<Vector4>(color, new SamplerBinding[]
            {
                new SamplerBinding("uTex", s_sampler, s_texture)
            });
        }

        public unsafe static void Render(GL gl, Material material, in Matrix4x4 viewProjection, ReadOnlySpan<InstanceData> instanceData)
        {
            if (!s_initialized)
                throw new InvalidOperationException($@"{nameof(Instances)} must be initialized before any calls to {nameof(Render)}");


            var instanceBuffer = BufferHelper.SetBufferData(gl, BufferTargetARB.UniformBuffer, s_instanceBuffer, 
                BufferUsageARB.DynamicDraw, instanceData);

            var sceneDataBuffer = BufferHelper.SetBufferData(gl, BufferTargetARB.UniformBuffer, s_sceneDataBuffer,
                BufferUsageARB.DynamicDraw, viewProjection);

            
            if (s_materialShader!.TryUse(gl,
                sceneData: sceneDataBuffer,
                materialData: material.GetDataBuffer(gl),
                materialSamplers:  material.Samplers,
                otherUBOData: null,
                otherSamplers: null,
                out uint? instanceBlockIndex
                ))
            {
                if (instanceBlockIndex.HasValue)
                {
                    //gl.GetInteger(GetPName.MaxUniformBlockSize, out int maxBlockSize);

                    uint maxInstanceCount = s_materialShader.MaxInstanceCount!.Value;

                    s_model!.DrawWithInstanceData(gl, instanceBlockIndex.Value, 
                        ((uint)sizeof(InstanceData), maxInstanceCount), 
                        new BufferRange(s_instanceBuffer, (uint)(instanceData.Length * sizeof(InstanceData))));
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
