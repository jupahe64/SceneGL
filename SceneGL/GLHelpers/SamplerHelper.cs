using SceneGL.Util;
using Silk.NET.OpenGL;

namespace SceneGL.GLHelpers
{
    public static class SamplerHelper
    {
        public enum DefaultSamplerKey
        {
            LINEAR,
            NEAREST,
            MIPMAP
        }

        private static readonly Dictionary<DefaultSamplerKey, uint> _defaultSamplers = new();

        public static uint GetOrCreate(GL gl, DefaultSamplerKey key) =>
            _defaultSamplers.GetOrCreate(key, () =>
            {
                switch (key)
                {
                    case DefaultSamplerKey.LINEAR:
                        uint sampler = CreateSampler2D(gl, 
                            TextureWrapMode.Repeat, TextureWrapMode.Repeat,
                            TextureMagFilter.Linear, TextureMinFilter.Linear);

                        ObjectLabelHelper.SetSamplerLabel(gl, sampler, "Default Black");
                        return sampler;
                    case DefaultSamplerKey.NEAREST:
                        sampler = CreateSampler2D(gl,
                            TextureWrapMode.Repeat, TextureWrapMode.Repeat,
                            TextureMagFilter.Nearest, TextureMinFilter.Nearest);

                        ObjectLabelHelper.SetSamplerLabel(gl, sampler, "Default White");
                        return sampler;
                    case DefaultSamplerKey.MIPMAP:
                        sampler = CreateMipMapSampler2D(gl,
                            TextureWrapMode.Repeat, TextureWrapMode.Repeat,
                            TextureMagFilter.Linear, TextureMinFilter.LinearMipmapLinear,
                            0, float.MaxValue, 0);

                        ObjectLabelHelper.SetSamplerLabel(gl, sampler, "Default Normal");
                        return sampler;
                    default:
                        throw new ArgumentException($"{key} is not a valid {nameof(DefaultSamplerKey)}");
                }
            });

        public static uint CreateMipMapSampler2D(GL gl,
            TextureWrapMode wrapModeS = TextureWrapMode.Repeat,
            TextureWrapMode wrapModeT = TextureWrapMode.Repeat,
            TextureMagFilter magFilter = TextureMagFilter.Linear,
            TextureMinFilter minFilter = TextureMinFilter.LinearMipmapLinear,
            float minLod = 0,
            float maxLod = float.MaxValue,
            float lodBias = 0
            )
        {
            uint sampler = gl.CreateSampler();
            gl.SamplerParameter(sampler, SamplerParameterI.WrapS, (int)wrapModeS);
            gl.SamplerParameter(sampler, SamplerParameterI.WrapT, (int)wrapModeT);

            gl.SamplerParameter(sampler, SamplerParameterI.MagFilter, (int)magFilter);
            gl.SamplerParameter(sampler, SamplerParameterI.MinFilter, (int)minFilter);

            gl.SamplerParameter(sampler, SamplerParameterF.MinLod, minLod);
            gl.SamplerParameter(sampler, SamplerParameterF.MaxLod, maxLod);
            gl.SamplerParameter(sampler, SamplerParameterF.LodBias, lodBias);

            return sampler;
        }

        public static uint CreateSampler2D(GL gl,
            TextureWrapMode wrapModeS = TextureWrapMode.Repeat,
            TextureWrapMode wrapModeT = TextureWrapMode.Repeat,
            TextureMagFilter magFilter = TextureMagFilter.Linear,
            TextureMinFilter minFilter = TextureMinFilter.Linear)
        {
            uint sampler = gl.CreateSampler();
            gl.SamplerParameter(sampler, SamplerParameterI.WrapS, (int)wrapModeS);
            gl.SamplerParameter(sampler, SamplerParameterI.WrapT, (int)wrapModeT);

            gl.SamplerParameter(sampler, SamplerParameterI.MagFilter, (int)magFilter);
            gl.SamplerParameter(sampler, SamplerParameterI.MinFilter, (int)minFilter);

            return sampler;
        }
    }
}