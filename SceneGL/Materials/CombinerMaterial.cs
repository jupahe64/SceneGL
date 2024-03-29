﻿using SceneGL.GLHelpers;
using SceneGL.GLWrappers;
using SceneGL.Materials.Common;
using SceneGL.Util;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.Materials
{
    public class GlslColorExpression
    {
        internal GlslColorExpression(string expression)
        {
            Expression = expression;
        }

        public string Expression { get; }

#pragma warning disable IDE1006 // Naming Styles
        public GlslColorExpression r => new(Expression + ".rrrr");
        public GlslColorExpression g => new(Expression + ".gggg");
        public GlslColorExpression b => new(Expression + ".bbbb");
        public GlslColorExpression a => new(Expression + ".aaaa");

        public GlslColorExpression withAlpha(float a) =>
            new(string.Format("vec4({0}.xyz, {1})", 
                Expression,
                a.ToString(CultureInfo.InvariantCulture)));

        public static GlslColorExpression vec4(float value) => vec4(value, value, value, value);
        public static GlslColorExpression vec4(float r, float g, float b, float a) =>
            new(string.Format("vec4({0},{1},{2},{3})",
                r.ToString(CultureInfo.InvariantCulture),
                g.ToString(CultureInfo.InvariantCulture),
                b.ToString(CultureInfo.InvariantCulture),
                a.ToString(CultureInfo.InvariantCulture)));
        public static GlslColorExpression vec4(Vector4 vector) =>
            new(string.Format("vec4({0},{1},{2},{3})",
                vector.X.ToString(CultureInfo.InvariantCulture),
                vector.Y.ToString(CultureInfo.InvariantCulture),
                vector.Z.ToString(CultureInfo.InvariantCulture),
                vector.W.ToString(CultureInfo.InvariantCulture)));

        public static GlslColorExpression clamp(GlslColorExpression a) =>
            new($"clamp({a.Expression}, vec4(0.0), vec4(1.0))");

        public static GlslColorExpression mix(GlslColorExpression x, GlslColorExpression y, GlslColorExpression a) =>
            new($"mix({x.Expression}, {y.Expression}, {a.Expression})");

#pragma warning restore IDE1006 // Naming Styles

        public static implicit operator GlslColorExpression(Vector4 vector) =>
            vec4(vector);

        public static implicit operator GlslColorExpression(float scalar) =>
            vec4(scalar);

        public static GlslColorExpression operator + (GlslColorExpression a, GlslColorExpression b) =>
            new($"{a.Expression}+{b.Expression}");

        public static GlslColorExpression operator *(GlslColorExpression a, GlslColorExpression b) =>
            new($"{a.Expression}*{b.Expression}");
    }

    public partial class CombinerMaterial
    {
        public struct MaterialData
        {
            public Vector4 Color0 = Vector4.One;
            public Vector4 Color1 = Vector4.One;
            public Vector4 Color2 = Vector4.One;

            public PackedMatrix3x2 Texture0Transform = Matrix3x2.Identity;
            public PackedMatrix3x2 Texture1Transform = Matrix3x2.Identity;
            public PackedMatrix3x2 Texture2Transform = Matrix3x2.Identity;

            public MaterialData()
            {
            }
        }

        public struct InstanceData
        {
            public PackedMatrix4x3 Transform;
            public Vector4 Color;
        }

        public const AttributeShaderLoc POSITION_LOC = AttributeShaderLoc.Loc0;
        public const AttributeShaderLoc COLOR_LOC = AttributeShaderLoc.Loc1;
        public const AttributeShaderLoc UV0_LOC = AttributeShaderLoc.Loc2;
        public const AttributeShaderLoc UV1_LOC = AttributeShaderLoc.Loc3;
        public const AttributeShaderLoc UV2_LOC = AttributeShaderLoc.Loc4;

        public const uint MaxInstanceCount = 1000;

        public static readonly GlslColorExpression Color0 = new("uColor0");
        public static readonly GlslColorExpression Color1 = new("uColor1");
        public static readonly GlslColorExpression Color2 = new("uColor2");

        public static readonly GlslColorExpression Texture0 = new("tex0");
        public static readonly GlslColorExpression Texture1 = new("tex1");
        public static readonly GlslColorExpression Texture2 = new("tex2");

        public static readonly GlslColorExpression VertexColor = new("vColor");
        public static readonly GlslColorExpression InstanceColor = new("vInstanceColor");

        private static readonly ShaderSource s_VertexSource = new(
            "CombinerMaterial.vert",
            ShaderType.VertexShader, """
                #version 330

                layout (std140) uniform ubScene
                {
                    mat4x4 uViewProjection;
                };

                struct InstanceData {
                    mat4x3 transform;
                    vec4 Color;
                };

                layout (std140, row_major) uniform ubInstanceData
                {
                    InstanceData uInstanceData[1000];
                };

                layout (location = 0) in vec3 aPosition;
                layout (location = 1) in vec4 aColor;
                layout (location = 2) in vec2 aTex0Coord;
                layout (location = 3) in vec2 aTex1Coord;
                layout (location = 4) in vec2 aTex2Coord;

                out vec4 vColor;
                out vec2 vTex0Coord;
                out vec2 vTex1Coord;
                out vec2 vTex2Coord;
                out vec4 vInstanceColor;

                void main() {
                    vColor = aColor;
                    vTex0Coord = aTex0Coord;
                    vTex1Coord = aTex1Coord;
                    vTex2Coord = aTex2Coord;

                    mat4x3 mtx = uInstanceData[gl_InstanceID].transform;

                    vec3 pos = mtx*vec4(aPosition, 1.0);

                    vInstanceColor = uInstanceData[gl_InstanceID].Color;

                    gl_Position = uViewProjection*vec4(pos, 1.0);
                }
                """
            );

        private static ShaderSource CreateFragmentSource(string combinerExpressionCode) => new(
            "CombinerMaterial.frag",
            ShaderType.FragmentShader, $$"""
                #version 330
                
                uniform sampler2D uTexture0;
                uniform sampler2D uTexture1;
                uniform sampler2D uTexture2;

                layout (std140, row_major) uniform ubMaterial
                {
                    vec4 uColor0;
                    vec4 uColor1;
                    vec4 uColor2;
                    mat3x2 uTexture0Mtx;
                    mat3x2 uTexture1Mtx;
                    mat3x2 uTexture2Mtx;
                };

                in vec4 vColor;
                in vec2 vTex0Coord;
                in vec2 vTex1Coord;
                in vec2 vTex2Coord;
                in vec4 vInstanceColor;

                out vec4 oColor;

                void main() {
                    vec4 tex0 = texture(uTexture0, uTexture0Mtx*vec3(vTex0Coord,1.0));
                    vec4 tex1 = texture(uTexture1, uTexture1Mtx*vec3(vTex1Coord,1.0));
                    vec4 tex2 = texture(uTexture2, uTexture2Mtx*vec3(vTex2Coord,1.0));
                    oColor = {{combinerExpressionCode}};

                    if(oColor.a < 0.001)
                        discard;
                }
                """
            );

        private static Dictionary<string, ShaderProgram> _shaderCache = new();

        public static CombinerMaterial CreateMaterial(GL gl, GlslColorExpression expression,
            MaterialData data,
            TextureSampler? texture0 = null,
            TextureSampler? texture1 = null,
            TextureSampler? texture2 = null, 
            string? uniformBufferLabel = null
            )
        {
            string expressionCode = expression.Expression;

            var shaderProgram = _shaderCache.GetOrCreate(expressionCode, 
                () => new ShaderProgram(s_VertexSource, CreateFragmentSource(expressionCode)));

            var shaderParams = ShaderParams.FromUniformBlockDataAndSamplers("ubMaterial",
                data, uniformBufferLabel, new SamplerBinding[]
            {
                new("uTexture0",
                texture0?.Sampler??SamplerHelper.GetOrCreate(gl, SamplerHelper.DefaultSamplerKey.LINEAR),
                texture0?.Texture??TextureHelper.GetOrCreate(gl, TextureHelper.DefaultTextureKey.WHITE)),

                new("uTexture1",
                texture1?.Sampler??SamplerHelper.GetOrCreate(gl, SamplerHelper.DefaultSamplerKey.LINEAR),
                texture1?.Texture??TextureHelper.GetOrCreate(gl, TextureHelper.DefaultTextureKey.WHITE)),

                new("uTexture2",
                texture2?.Sampler??SamplerHelper.GetOrCreate(gl, SamplerHelper.DefaultSamplerKey.LINEAR),
                texture2?.Texture??TextureHelper.GetOrCreate(gl, TextureHelper.DefaultTextureKey.WHITE)),
            }, out UniformBuffer<MaterialData> ubMaterial);

            return new CombinerMaterial(shaderParams, ubMaterial, shaderProgram);
        }




        private readonly ShaderProgram _shaderProgram;
        private readonly ShaderParams _shaderParameters;
        private readonly UniformBuffer<MaterialData> _ubMaterial;

        public MaterialData MaterialParams
        {
            get => _ubMaterial.Data;
            set => _ubMaterial.SetData(value);
        }

        public CombinerMaterial(ShaderParams shaderParams, UniformBuffer<MaterialData> ubMaterial, ShaderProgram shaderProgram)
        {
            _shaderParameters = shaderParams;
            _ubMaterial = ubMaterial;
            _shaderProgram = shaderProgram;
        }

        public bool TryUse(GL gl, SceneParameters sceneParameters, out ProgramUniformScope scope, out uint? instanceBufferIndex)
        {
            return _shaderProgram.TryUse(gl, "ubInstanceData", new IShaderBindingContainer[]
            {
                sceneParameters.ShaderParameters,
                _shaderParameters
            }, out scope, out instanceBufferIndex);
        }
    }
}
