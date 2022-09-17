using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.GLWrappers
{
    public class Framebuffer
    {
        private uint _framebuffer;

        private (InternalFormat internalFormat, uint texture)? _depthAttachment;

        private (InternalFormat internalFormat, PixelFormat format, FramebufferAttachment attachment, uint texture)[] _colorAttachments;
        private uint _width = 0;
        private uint _height = 0;
        private uint _requestedWidth;
        private uint _requestedHeight;

        public Framebuffer(Vector2D<uint>? initialSize, InternalFormat? depthAttachment, params InternalFormat[] colorAttachments)
        {
            if (depthAttachment != null)
            {
                _depthAttachment = (depthAttachment.Value, 0);
            }

            _colorAttachments = new (InternalFormat internalFormat, PixelFormat format,
                FramebufferAttachment attachment, uint texture)[colorAttachments.Length];



            for (int i = 0; i < colorAttachments.Length; i++)
            {
                var format = colorAttachments[i];
                _colorAttachments[i] = (format, PixelFormat.Rgba,
                    (FramebufferAttachment)((int)FramebufferAttachment.ColorAttachment0 + i), 0);
            }
            _requestedWidth = initialSize?.X ?? 0;
            _requestedHeight = initialSize?.Y ?? 0;
        }

        public uint DepthStencilTexture
        {
            get
            {
                EnsureCreated();

                if (_depthAttachment == null)
                    throw new InvalidOperationException("Framebuffer has no DepthStencilAttachment");

                return _depthAttachment.Value.texture;
            }
        }

        public uint GetColorTexture(int attachment)
        {
            EnsureCreated();

            if (attachment < 0 || attachment > _colorAttachments.Length - 1)
                throw new InvalidOperationException($"Framebuffer has no ColorAttachment {attachment}");

            return _colorAttachments[attachment].texture;
        }

        private void EnsureCreated()
        {
            if (_framebuffer == 0)
                throw new InvalidOperationException("Framebuffer has not been created yet");
        }

        public void Create(GL gl)
        {
            if (_framebuffer == 0)
                _framebuffer = gl.CreateFramebuffer();

            if (_width != _requestedWidth || _height != _requestedHeight)
            {
                CreateTextures(gl, _requestedWidth, _requestedHeight);

                _width = _requestedWidth;
                _height = _requestedHeight;
            }

        }

        private void CreateTextures(GL gl, uint width, uint height)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

            if (_depthAttachment != null)
            {
                var (internalF, texture) = _depthAttachment.Value;

                if (texture == 0)
                    texture = gl.GenTexture();

                gl.BindTexture(TextureTarget.Texture2D, texture);
                unsafe
                {
                    gl.TexImage2D(TextureTarget.Texture2D, 0, internalF, width, height, 0,
                    PixelFormat.DepthStencil, GLEnum.UnsignedInt248, null);
                }

                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                gl.BindTexture(TextureTarget.Texture2D, 0);

                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                    TextureTarget.Texture2D, texture, 0);

                _depthAttachment = (internalF, texture);
            }

            for (int i = 0; i < _colorAttachments.Length; i++)
            {
                var (internalF, format, attachment, texture) = _colorAttachments[i];

                if (texture == 0)
                    texture = gl.GenTexture();

                gl.BindTexture(TextureTarget.Texture2D, texture);
                unsafe
                {
                    gl.TexImage2D(TextureTarget.Texture2D, 0, internalF, width, height, 0,
                    format, GLEnum.UnsignedByte, null);
                }

                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                gl.BindTexture(TextureTarget.Texture2D, 0);

                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, TextureTarget.Texture2D, texture, 0);

                _colorAttachments[i] = (internalF, format, attachment, texture);
            }

            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void SetSize(uint width, uint height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException($"Invalid size for Framebuffer ({width}, {height})");

            if (_requestedWidth == width && _requestedHeight == height)
                return;

            _requestedWidth = width;
            _requestedHeight = height;
        }

        public void Use(GL gl)
        {
            Create(gl);

            gl.Viewport(0, 0, _width, _height);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        }
    }
}
