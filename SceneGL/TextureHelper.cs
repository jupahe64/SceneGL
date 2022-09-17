using Silk.NET.Core.Attributes;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL
{
    public static class TextureHelper
    {
        public delegate ReadOnlySpan<TPixel> TextureDataProvider<TPixel>(out uint width, out uint height);
        public delegate ReadOnlySpan<byte> CompressedTextureDataProvider(out uint width, out uint height);

        public static uint CreateTexture2D<TPixel>(GL gl, InternalFormat internalformat,
            uint width, uint height, PixelFormat format, ReadOnlySpan<TPixel> pixels, bool generateMipmaps)
            where TPixel : unmanaged
        {
            uint texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, texture);

            gl.TexImage2D(TextureTarget.Texture2D, 0, internalformat, width, height, 0, format, PixelType.UnsignedByte, pixels);

            if(generateMipmaps)
                gl.GenerateMipmap(TextureTarget.Texture2D);

            gl.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        public static uint CreateTexture2D<TPixel>(GL gl, InternalFormat internalformat,
            PixelFormat format, params TextureDataProvider<TPixel>[] levels)
            where TPixel : unmanaged
        {
            uint texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, texture);

            for (int i = 0; i < levels.Length; i++)
            {
                var pixels = levels[i].Invoke(out uint width, out uint height);

                gl.TexImage2D(TextureTarget.Texture2D, i, internalformat, width, height, 0, format, PixelType.UnsignedByte, pixels);
            }

            gl.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        public static uint CreateTexture2DCompressed(GL gl, InternalFormat internalformat,
            uint width, uint height, ReadOnlySpan<byte> pixels, bool generateMipmaps)
        {
            uint texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, texture);

            gl.CompressedTexImage2D(TextureTarget.Texture2D, 0, internalformat, width, height, 0, pixels);

            if (generateMipmaps)
                gl.GenerateMipmap(TextureTarget.Texture2D);

            gl.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        public static uint CreateTexture2DCompressed(GL gl, InternalFormat internalformat,
            params CompressedTextureDataProvider[] levels)
        {
            uint texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, texture);

            for (int i = 0; i < levels.Length; i++)
            {
                var pixels = levels[i].Invoke(out uint width, out uint height);

                gl.CompressedTexImage2D(TextureTarget.Texture2D, i, internalformat, width, height, 0, pixels);
            }

            gl.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }
    }
}
