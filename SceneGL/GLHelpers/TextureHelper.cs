using Silk.NET.Core.Attributes;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.GLHelpers
{
    public static class TextureHelper
    {
        public static uint CreateTexture2D<TPixel>(GL gl, InternalFormat internalformat,
            uint width, uint height, PixelFormat format, ReadOnlySpan<TPixel> pixels, bool generateMipmaps)
            where TPixel : unmanaged
        {
            uint texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, texture);

            gl.TexImage2D(TextureTarget.Texture2D, 0, internalformat, width, height, 0, format, PixelType.UnsignedByte, pixels);

            if (generateMipmaps)
                gl.GenerateMipmap(TextureTarget.Texture2D);

            gl.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        public static uint CreateTexture2D<TPixel, TImageSource>(GL gl, InternalFormat internalformat,
            PixelFormat format, IReadOnlyList<(TPixel[] pixels, uint width, uint height)> mipLevels)
            where TPixel : unmanaged
        {
            uint texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, texture);

            for (int i = 0; i < mipLevels.Count; i++)
            {
                var (pixels, width, height) = mipLevels[i];

                gl.TexImage2D<TPixel>(TextureTarget.Texture2D, i, internalformat, width, height, 0, format, PixelType.UnsignedByte, pixels);
            }

            gl.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        public static uint CreateTexture2DCompressed(GL gl, InternalFormat internalformat,
            uint width, uint height, ReadOnlySpan<byte> data, bool generateMipmaps)
        {
            uint texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, texture);

            gl.CompressedTexImage2D(TextureTarget.Texture2D, 0, internalformat, width, height, 0, data);

            if (generateMipmaps)
                gl.GenerateMipmap(TextureTarget.Texture2D);

            gl.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        public static uint CreateTexture2DCompressed(GL gl, InternalFormat internalformat,
            IReadOnlyList<(byte[] data, uint width, uint height)> mipLevels)
        {
            uint texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, texture);

            for (int i = 0; i < mipLevels.Count; i++)
            {
                var (data, width, height) = mipLevels[i];

                gl.CompressedTexImage2D<byte>(TextureTarget.Texture2D, i, internalformat, width, height, 0, data);
            }

            gl.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }
    }
}
