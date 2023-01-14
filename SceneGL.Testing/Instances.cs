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
using Silk.NET.Maths;

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

        private static InstanceData[] s_instanceTransformResultBuffer = new InstanceData[1];

        public struct InstanceData
        {
            /// <summary>
            /// The internal representation of the Transform as a 4x3 row_major matrix, 
            /// not intended for direct access, use <see cref="Transform"/> instead
            /// </summary>
            public Matrix3X4<float> TransformData;
            public Vector4 TintColor;


            
            public Matrix4x4 Transform
            {
                // row_major 4x3 -> column_major 4x4
                get => new (
                    TransformData.M11, TransformData.M21, TransformData.M31, 0,
                    TransformData.M12, TransformData.M22, TransformData.M32, 0,
                    TransformData.M13, TransformData.M23, TransformData.M33, 0,
                    TransformData.M14, TransformData.M24, TransformData.M34, 1
                    );

                // column_major 4x4 -> row_major 4x3
                set
                {
                    TransformData.M11 = value.M11;
                    TransformData.M21 = value.M12;
                    TransformData.M31 = value.M13;

                    TransformData.M12 = value.M21;
                    TransformData.M22 = value.M22;
                    TransformData.M32 = value.M23;

                    TransformData.M13 = value.M31;
                    TransformData.M23 = value.M32;
                    TransformData.M33 = value.M33;

                    TransformData.M14 = value.M41;
                    TransformData.M24 = value.M42;
                    TransformData.M34 = value.M43;
                }
            }

            public unsafe InstanceData(Matrix4x4 transform)
            {
                Transform = transform;
            }
        }

        private static bool s_initialized = false;

        private static uint s_instanceBuffer;
        private static BufferRange s_sceneDataBuffer;
        private static MaterialShader? s_materialShader;
        private static RenderableModel? s_model;

        private static ShaderProgram? s_shaderProgram;
        private static uint s_texture;
        private static uint s_sampler;
        public static readonly ShaderSource VertexSource = new(
            "Instances.vert",
            ShaderType.VertexShader, """
                #version 330

                layout (std140) uniform ubScene
                {
                    mat4x4 uViewProjection;
                };

                struct InstanceData {
                    mat4x3 transform;
                    vec4 tintColor;
                };

                layout (std140, row_major) uniform ubInstanceData
                {
                    InstanceData uInstanceData[1000];
                };

                layout (location = 0) in vec3 aPosition;
                layout (location = 1) in vec2 aTexCoord;
                layout (location = 2) in vec4 aColor;

                out vec2 vTexCoord;
                out vec4 vColor;
                out vec4 vInstanceTintColor;

                void main() {
                    vTexCoord = aTexCoord;
                    vColor = aColor;

                    mat4x3 mtx = uInstanceData[gl_InstanceID].transform;

                    vec3 pos = mtx*vec4(aPosition, 1.0);

                    vInstanceTintColor = uInstanceData[gl_InstanceID].tintColor;

                    gl_Position = uViewProjection*vec4(pos, 1.0);
                }
                """
            );

        public static readonly ShaderSource FragmentSource = new(
            "Instances.frag",
            ShaderType.FragmentShader, """
                #version 330

                layout (std140) uniform ubMaterial
                {
                    vec4 uColor;
                };
                
                uniform sampler2D uTex;

                in vec2 vTexCoord;
                in vec4 vColor;
                in vec4 vInstanceTintColor;

                out vec4 oColor;

                void main() {
                    vec4 tex = texture(uTex, vTexCoord);
                    oColor = vColor+uColor+tex*tex.a*0.1;
                    oColor = mix(oColor, vec4(vInstanceTintColor.xyz, 1.0), vInstanceTintColor.a);
                }
                """
            );

        public static void Initialize(GL gl)
        {
            if (s_initialized)
                return;

            s_initialized = true;


            s_shaderProgram = new ShaderProgram(VertexSource, FragmentSource);

            s_instanceBuffer = BufferHelper.CreateBuffer(gl);
            ObjectLabelHelper.SetBufferLabel(gl, s_instanceBuffer, "Instances.InstanceBuffer");

            s_sceneDataBuffer = BufferHelper.CreateBuffer(gl, BufferUsageARB.StreamDraw, Matrix4x4.Identity);
            ObjectLabelHelper.SetBufferLabel(gl, s_sceneDataBuffer.Buffer, "Instances.SceneDataBuffer");

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

            if (s_instanceTransformResultBuffer.Length < instanceData.Length)
            {
                int len = 1;
                while (len < instanceData.Length) len *= 2;

                s_instanceTransformResultBuffer = new InstanceData[len];
            }

            for (int i = 0; i < instanceData.Length; i++)
            {
                var data = instanceData[i];

                var transform = data.Transform;

                var projectedCenterPos = Vector4.Transform(transform.Translation, viewProjection);

                var destRotation = Quaternion.CreateFromYawPitchRoll(i, i * 2, i * 3);

                float scaleFade = Math.Clamp(MathF.Abs(projectedCenterPos.W * 0.3f - 12) - 9, 0, 1);
                float colorFade = Math.Clamp(MathF.Abs(projectedCenterPos.W * 0.3f - 12) - 8.5f, 0, 1);

                var rotation = Quaternion.Slerp(Quaternion.Identity, destRotation, MathF.Pow(scaleFade, 10));

                data.Transform = Matrix4x4.CreateScale(1- scaleFade * scaleFade) * Matrix4x4.CreateFromQuaternion(rotation) * transform;
                data.TintColor = new Vector4(1, 1, 1, colorFade * colorFade * 0.5f);
                s_instanceTransformResultBuffer[i] = data;
            }


            var instanceBuffer = BufferHelper.SetBufferData<InstanceData>(gl, s_instanceBuffer, 
                BufferUsageARB.DynamicDraw, s_instanceTransformResultBuffer.AsSpan(0, instanceData.Length));

            BufferHelper.UpdateBufferData(gl, s_sceneDataBuffer, in viewProjection);

            
            if (s_materialShader!.TryUse(gl,
                sceneData: s_sceneDataBuffer,
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
