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
using SceneGL.Materials;
using ImGuiNET;
using SceneGL.Materials.Common;

namespace SceneGL.Testing
{
    internal class Gizmos
    {
        private struct Vertex
        {
            [VertexAttribute(UnlitMaterial.POSITION_LOC, 3, VertexAttribPointerType.Float, normalized: false)]
            public Vector3 Position;

            [VertexAttribute(UnlitMaterial.UV_LOC, 2, VertexAttribPointerType.Float, normalized: false)]
            public Vector2 UV;
        }

        public struct InstanceData
        {
            public Vector3 Position;
            public Vector3 Color;
        }

        private static UnlitMaterial.InstanceData[] s_instanceTransformResultBuffer = new UnlitMaterial.InstanceData[1];

        private static uint s_instanceBuffer;
        private static SceneParameters? s_sceneParameters;
        private static RenderableModel? s_model;
        private static UnlitMaterial? s_material;

        private static uint s_texture;
        private static uint s_sampler;

        private static bool s_isInitialized = false;

        public static void Initialize(GL gl)
        {
            if (s_isInitialized)
                return;

            s_isInitialized = true;

            s_instanceBuffer = BufferHelper.CreateBuffer(gl);
            ObjectLabelHelper.SetBufferLabel(gl, s_instanceBuffer, "Gizmos.InstanceBuffer");

            s_sceneParameters = SceneParameters.Create(Matrix4x4.Identity, "Gizmos.SceneDataBuffer");

            //texture
            {
                var image = Image.Load<Rgba32>(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "res", "LightGizmo.png"));

                var pixelData = new Rgba32[image.Width * image.Height];

                image.CopyPixelDataTo(pixelData);

                s_texture = TextureHelper.CreateTexture2D<Rgba32>(gl, InternalFormat.Rgba, (uint)image.Width, (uint)image.Height,
                    PixelFormat.Rgba, pixelData, true);
            }

            s_sampler = SamplerHelper.CreateMipMapSampler2D(gl, lodBias: -3);

            s_material = UnlitMaterial.CreateMaterial(gl, new TextureSampler(s_sampler, s_texture));


            #region Cube Model
            {

                var builder = new ModelBuilder<Vertex>();

                builder!.AddPlane(
                        new Vertex { Position = new Vector3(-0.5f,  0.5f, 0), UV = new Vector2(0, 0) },
                        new Vertex { Position = new Vector3( 0.5f,  0.5f, 0), UV = new Vector2(1, 0) },
                        new Vertex { Position = new Vector3(-0.5f, -0.5f, 0), UV = new Vector2(0, 1) },
                        new Vertex { Position = new Vector3( 0.5f, -0.5f, 0), UV = new Vector2(1, 1) }
                    );


                s_model = builder.GetModel(gl);

            }
            #endregion
        }

        public unsafe static void Render(GL gl, in Quaternion cameraRot, in Matrix4x4 viewProjection, ReadOnlySpan<InstanceData> instancePositions)
        {
            if (!s_isInitialized)
                throw new InvalidOperationException($@"{nameof(Gizmos)} must be initialized before any calls to {nameof(Render)}");

            if (s_instanceTransformResultBuffer.Length < instancePositions.Length)
            {
                int len = 1;
                while (len < instancePositions.Length) len *= 2;

                s_instanceTransformResultBuffer = new UnlitMaterial.InstanceData[len];
            }

            for (int i = 0; i < instancePositions.Length; i++)
            {
                var position = instancePositions[i].Position;
                var color = instancePositions[i].Color;

                var projectedCenterPos = Vector4.Transform(new Vector4(position, 1), viewProjection);

                var destRotation = Quaternion.CreateFromYawPitchRoll(i, i * 2, i * 3);

                float alphaFade = Math.Clamp((projectedCenterPos.W-3) * 0.3f, 0, 1);

                s_instanceTransformResultBuffer[i] = new UnlitMaterial.InstanceData
                {
                    Transform = Matrix4x4.CreateFromQuaternion(cameraRot) *
                                Matrix4x4.CreateTranslation(position),

                    TintColor = new Vector4(color, alphaFade),
                };
            }


            s_sceneParameters!.ViewProjection = viewProjection;

            if (s_material!.TryUse(gl,
                s_sceneParameters,
                out ProgramUniformScope scope,
                out uint? instanceBlockIndex
            ))
            {
                using (scope)
                {
                    gl.Enable(EnableCap.Blend);

                    if (instanceBlockIndex.HasValue)
                    {
                        var instanceBufferBinding = InstanceBufferHelper.UploadData<UnlitMaterial.InstanceData>(
                            gl, s_instanceBuffer, (int)UnlitMaterial.MaxInstanceCount,
                            s_instanceTransformResultBuffer, BufferUsageARB.StreamDraw);

                        for (int i = 0; i < instanceBufferBinding.Blocks.Count; i++)
                        {
                            var (count, range) = instanceBufferBinding.Blocks[i];

                            gl.BindBufferRange(BufferTargetARB.UniformBuffer, instanceBlockIndex.Value,
                                range.Buffer, range.Offset, range.Size);

                            s_model!.Draw(gl, (uint)count);
                        }
                    }
                    else
                    {
                        s_model!.Draw(gl, (uint)instancePositions.Length);
                    }

                    gl.Disable(EnableCap.Blend);
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
