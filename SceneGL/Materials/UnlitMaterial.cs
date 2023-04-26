using SceneGL.GLHelpers;
using SceneGL.GLWrappers;
using SceneGL.Materials.Common;
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
            public PackedMatrix4x3 Transform;
            public Vector4 TintColor;
        }

        public const uint MaxInstanceCount = 1000;

        public const AttributeShaderLoc POSITION_LOC = AttributeShaderLoc.Loc0;
        public const AttributeShaderLoc UV_LOC = AttributeShaderLoc.Loc1;

        public static readonly ShaderSource VertexSource = new(
            "UnlitMaterial.vert",
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
            "UnlitMaterial.frag",
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

        private static readonly ShaderProgram s_shaderProgram = new(VertexSource, FragmentSource);

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




        private readonly ShaderParams _shaderParameters;

        public UnlitMaterial(ShaderParams shaderParams)
        {
            _shaderParameters = shaderParams;
        }

        public bool TryUse(GL gl, SceneParameters sceneParameters, out ProgramUniformScope scope, out uint? instanceBufferIndex)
        {
            return s_shaderProgram.TryUse(gl, "ubInstanceData", new IShaderBindingContainer[]
            {
                sceneParameters.ShaderParameters,
                _shaderParameters
            }, out scope, out instanceBufferIndex);
        }
    }
}
