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

namespace SceneGL.Materials
{

    public static class DrawMaterial
    {
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

        public const AttributeShaderLoc POSITION_LOC = AttributeShaderLoc.Loc0;
        public const AttributeShaderLoc COLOR_LOC = AttributeShaderLoc.Loc1;

        public static readonly ShaderSource VertexSource = new(
            "Instances.vert",
            ShaderType.VertexShader, """
                #version 330

                layout (std140) uniform ubScene
                {
                    mat4x4 uViewProjection;
                };

                layout (location = 0) in vec3 aPosition;
                layout (location = 1) in vec3 aColor;

                out vec3 vColor;

                void main() {
                    vColor = aColor;

                    gl_Position = uViewProjection*vec4(aPosition, 1.0);
                }
                """
            );

        public static readonly ShaderSource FragmentSource = new(
            "Instances.frag",
            ShaderType.FragmentShader, """
                #version 330
                
                uniform sampler2D uTexture;

                in vec3 vColor;

                out vec4 oColor;

                void main() {
                    oColor = vec4(vColor, 1.0);
                }
                """
            );

        private static readonly ShaderProgram s_shaderProgram = new ShaderProgram(VertexSource, FragmentSource);

        public static SceneParameters CreateSceneParameters(GL gl, Matrix4x4 viewProjection)
        {
            var _params = ShaderParams.FromUniformBlockDataAndSamplers(gl, "ubScene", new SceneData
            {
                ViewProjection = viewProjection
            }, Array.Empty<SamplerBinding>(), out UniformBuffer<SceneData> buffer);

            return new SceneParameters(buffer, _params);
        }

        public static bool TryUse(GL gl, SceneParameters sceneParameters, out ProgramUniformScope scope)
        {
            return s_shaderProgram.TryUse(gl, null, new ShaderParams[]
            {
                sceneParameters.ShaderParameters
            }, out scope, out _);
        }
    }
}
