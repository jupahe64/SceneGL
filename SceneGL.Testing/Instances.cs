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
using SharpDX.Direct3D11;
using SceneGL.Materials;

namespace SceneGL.Testing
{
    internal class Instances
    {
        private struct Vertex
        {
            [VertexAttribute(CombinerMaterial.POSITION_LOC, 3, VertexAttribPointerType.Float, normalized: false)]
            public Vector3 Position;

            [VertexAttribute(CombinerMaterial.UV0_LOC, 2, VertexAttribPointerType.Float, normalized: false)]
            public Vector2 UV;

            [VertexAttribute(CombinerMaterial.COLOR_LOC, 4, VertexAttribPointerType.Float, normalized: false)]
            public Vector4 Color;
        }

        public struct InstanceData
        {
            public Matrix4x4 Transform;

            public unsafe InstanceData(Matrix4x4 transform)
            {
                Transform = transform;
            }
        }

        private static CombinerMaterial.InstanceData[] s_instanceTransformResultBuffer = 
            new CombinerMaterial.InstanceData[1];

        

        private static uint s_instanceBuffer;
        private static BufferRange s_sceneDataBuffer;
        private static RenderableModel? s_model;

        private static uint s_texture;
        private static uint s_sampler;
        private static GlslColorExpression? s_shaderColorExpression;

        public static void Initialize(GL gl)
        {
            if (s_shaderColorExpression!=null)
                return;

            var vColor = CombinerMaterial.VertexColor;
            var uColor = CombinerMaterial.Color0;
            var tex = CombinerMaterial.Texture0;
            var instanceColor = CombinerMaterial.InstanceColor;

            var temp = vColor + uColor + tex * tex.a * 0.1f;

            s_shaderColorExpression = GlslColorExpression.mix(temp, instanceColor.withAlpha(1), instanceColor.a);

            s_instanceBuffer = BufferHelper.CreateBuffer(gl);
            ObjectLabelHelper.SetBufferLabel(gl, s_instanceBuffer, "Instances.InstanceBuffer");

            s_sceneDataBuffer = BufferHelper.CreateBuffer(gl, BufferUsageARB.StreamDraw, 
                new CombinerMaterial.UbScene { ViewProjection = Matrix4x4.Identity });

            ObjectLabelHelper.SetBufferLabel(gl, s_sceneDataBuffer.Buffer, "Instances.SceneDataBuffer");

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

        public static Material<CombinerMaterial.UbMaterial> CreateMaterial(GL gl, Vector4 color)
        {
            if (s_shaderColorExpression == null)
                throw new InvalidOperationException($@"{nameof(Instances)} must be initialized before any calls to {nameof(CreateMaterial)}");

            return CombinerMaterial.CreateMaterial(gl, s_shaderColorExpression,
                new CombinerMaterial.UbMaterial { Color0 = color }, 
                texture0: new TextureSampler(s_sampler, s_texture));
        }

        public unsafe static void Render(GL gl, Material material, in Matrix4x4 viewProjection, ReadOnlySpan<InstanceData> instanceData)
        {
            if (s_shaderColorExpression == null)
                throw new InvalidOperationException($@"{nameof(Instances)} must be initialized before any calls to {nameof(Render)}");

            if (s_instanceTransformResultBuffer.Length < instanceData.Length)
            {
                int len = 1;
                while (len < instanceData.Length) len *= 2;

                s_instanceTransformResultBuffer = new CombinerMaterial.InstanceData[len];
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

                var newData = new CombinerMaterial.InstanceData
                {
                    Transform = Matrix4x4.CreateScale(1 - scaleFade * scaleFade) *
                    Matrix4x4.CreateFromQuaternion(rotation) * transform,
                    Color = new Vector4(Vector3.One, colorFade * colorFade * 0.5f)
                };
                
                s_instanceTransformResultBuffer[i] = newData;
            }

            BufferHelper.UpdateBufferData(gl, s_sceneDataBuffer, 
                new CombinerMaterial.UbScene
                {
                    ViewProjection = viewProjection 
                });

            
            if (material.Shader!.TryUse(gl,
                sceneData: s_sceneDataBuffer,
                materialData: material.GetDataBuffer(gl),
                materialSamplers:  material.Samplers,
                otherUBOData: null,
                otherSamplers: null,
                out MaterialShaderScope scope,
                out uint? instanceBlockIndex
                ))
            {
                using (scope)
                {
                    if (instanceBlockIndex.HasValue)
                    {
                        var instanceBufferBinding = InstanceBufferHelper.UploadData<CombinerMaterial.InstanceData>(
                            gl, s_instanceBuffer, (int)material.Shader.MaxInstanceCount!.Value,
                            s_instanceTransformResultBuffer, BufferUsageARB.StreamDraw);

                        for (int i = 0; i < instanceBufferBinding.Blocks.Count; i++)
                        {
                            var (count, range) = instanceBufferBinding.Blocks[i];

                            gl.BindBufferRange(BufferTargetARB.UniformBuffer, instanceBlockIndex.Value, 
                                range.Buffer, 
                                range.Offset, range.Size);

                            s_model!.Draw(gl, (uint)count);
                        }
                        
                        gl.BindBufferBase(BufferTargetARB.UniformBuffer, instanceBlockIndex.Value, 0);
                    }
                    else
                    {
                        s_model!.Draw(gl, (uint)instanceData.Length);
                    }
                }
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
