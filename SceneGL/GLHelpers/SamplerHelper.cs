using Silk.NET.OpenGL;

namespace SceneGL.GLHelpers
{
    public static class SamplerHelper
    {

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