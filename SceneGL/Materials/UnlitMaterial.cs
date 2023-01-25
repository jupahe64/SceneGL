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

    public static class UnlitMaterial
    {
        public struct UbMaterial
        {
            public Vector4 Color;
        }

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
                get => new(
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

        public struct UbScene
        {
            public Matrix4x4 ViewProjection;
        }

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
                out vec4 vColor;
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

        public static readonly MaterialShader Shader = new(
            new ShaderProgram(VertexSource, FragmentSource),
                sceneBlockBinding: "ubScene",
                materialBlockBinding: "ubMaterial",
                instanceDataBlock: ("ubInstanceData", 1000));

        public static Material<UbMaterial> CreateMaterial(GL gl, Vector4 color, TextureSampler? texture = null)
        {
            return Shader.CreateMaterial(new UbMaterial { Color = color }, new SamplerBinding[]
            {
                new("uTexture", 
                texture?.Sampler??SamplerHelper.GetOrCreate(gl, SamplerHelper.DefaultSamplerKey.LINEAR),
                texture?.Texture??TextureHelper.GetOrCreate(gl, TextureHelper.DefaultTextureKey.WHITE))
            });
        }
    }
}
