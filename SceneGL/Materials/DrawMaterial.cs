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

namespace SceneGL.Materials
{

    public static class DrawMaterial
    {
        public const AttributeShaderLoc POSITION_LOC = AttributeShaderLoc.Loc0;
        public const AttributeShaderLoc COLOR_LOC = AttributeShaderLoc.Loc1;

        public static readonly ShaderSource VertexSource = new(
            "DrawMaterial.vert",
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
            "DrawMaterial.frag",
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

        public static bool TryUse(GL gl, SceneParameters sceneParameters, out ProgramUniformScope scope)
        {
            return s_shaderProgram.TryUse(gl, null, new IShaderBindingContainer[]
            {
                sceneParameters.ShaderParameters
            }, out scope, out _);
        }
    }
}
