using SceneGL.GLHelpers;
using SceneGL.GLWrappers;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static SceneGL.Materials.CombinerMaterial;

namespace SceneGL.Materials
{

    public class UnlitMaterial
    {
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
                get => UniformBufferHelper.Unpack3dTransformMatrix(in TransformData);
                set => UniformBufferHelper.Pack3dTransformMatrix(value, ref TransformData);
            }
        }

        public struct SceneData
        {
            public Matrix4x4 ViewProjection;
        }

        public sealed class SceneParameters
        {
            private UniformBuffer<SceneData> _buffer;
            internal ShaderParams ShaderParameters { get; }

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
        }

        public const uint MaxInstanceCount = 1000;

        public const AttributeShaderLoc POSITION_LOC = AttributeShaderLoc.Loc0;
        public const AttributeShaderLoc UV_LOC = AttributeShaderLoc.Loc1;

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

                out vec2 vTexCoord;
                out vec4 vInstanceTintColor;

                void main() {
                    vTexCoord = aTexCoord;

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
                
                uniform sampler2D uTexture;

                in vec2 vTexCoord;
                in vec4 vInstanceTintColor;

                out vec4 oColor;

                void main() {
                    vec4 tex = texture(uTexture, vTexCoord);
                    oColor = vInstanceTintColor*tex;

                    if(oColor.a < 0.001)
                        discard;
                }
                """
            );

        private static ShaderProgram s_shaderProgram = new(VertexSource, FragmentSource);

        public static SceneParameters CreateSceneParameters(GL gl, Matrix4x4 viewProjection)
        {
            var _params = ShaderParams.FromUniformBlockDataAndSamplers(gl, "ubScene", new SceneData
            {
                ViewProjection = viewProjection
            }, Array.Empty<SamplerBinding>(), out UniformBuffer<SceneData> buffer);

            return new SceneParameters(buffer, _params);
        }

        public static UnlitMaterial CreateMaterial(GL gl, TextureSampler? texture = null)
        {
            var shaderParams = ShaderParams.FromSamplers(
                new SamplerBinding[]
            {
                new("uTexture",
                texture?.Sampler??SamplerHelper.GetOrCreate(gl, SamplerHelper.DefaultSamplerKey.LINEAR),
                texture?.Texture??TextureHelper.GetOrCreate(gl, TextureHelper.DefaultTextureKey.WHITE))
            });

            return new UnlitMaterial(shaderParams);
        }

        private ShaderParams _shaderParameters;

        public UnlitMaterial(ShaderParams shaderParams)
        {
            _shaderParameters = shaderParams;
        }

        public bool TryUse(GL gl, SceneParameters sceneParameters, out ProgramUniformScope scope, out uint? instanceBufferIndex)
        {
            return s_shaderProgram.TryUse(gl, "ubInstanceData", new ShaderParams[]
            {
                sceneParameters.ShaderParameters,
                _shaderParameters
            }, out scope, out instanceBufferIndex);
        }
    }
}
