using Silk.NET.OpenGL;
using System.Numerics;

namespace SceneGL.Materials.Common
{
    public struct SceneData
    {
        public Matrix4x4 ViewProjection;
    }

    public sealed class SceneParameters
    {
        public const string UniformBlockBinding = "ubScene";
        private readonly UniformBuffer<SceneData> _buffer;
        public IShaderBindingContainer ShaderParameters { get; }

        internal SceneParameters(UniformBuffer<SceneData> buffer, ShaderParams shaderParameters)
        {
            _buffer = buffer;
            ShaderParameters = shaderParameters;
        }

        public Matrix4x4 ViewProjection
        {
            get => _buffer.Data.ViewProjection;
            set => _buffer.SetData(_buffer.Data with { ViewProjection = value });
        }

        public static SceneParameters Create(Matrix4x4 viewProjection, string? uniformBufferLabel = null)
        {
            var _params = ShaderParams.FromUniformBlockDataAndSamplers(UniformBlockBinding, new SceneData
            {
                ViewProjection = viewProjection
            }, uniformBufferLabel, Array.Empty<SamplerBinding>(), out UniformBuffer<SceneData> buffer);

            return new SceneParameters(buffer, _params);
        }
    }
}
