using Silk.NET.Core.Attributes;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SceneGL.GLWrappers
{
    public enum FramebufferColorAttachment
    {
        ColorAttachment0 = 36064,
        [NativeName("Name", "GL_COLOR_ATTACHMENT1")]
        ColorAttachment1 = 36065,
        [NativeName("Name", "GL_COLOR_ATTACHMENT2")]
        ColorAttachment2 = 36066,
        [NativeName("Name", "GL_COLOR_ATTACHMENT3")]
        ColorAttachment3 = 36067,
        [NativeName("Name", "GL_COLOR_ATTACHMENT4")]
        ColorAttachment4 = 36068,
        [NativeName("Name", "GL_COLOR_ATTACHMENT5")]
        ColorAttachment5 = 36069,
        [NativeName("Name", "GL_COLOR_ATTACHMENT6")]
        ColorAttachment6 = 36070,
        [NativeName("Name", "GL_COLOR_ATTACHMENT7")]
        ColorAttachment7 = 36071,
        [NativeName("Name", "GL_COLOR_ATTACHMENT8")]
        ColorAttachment8 = 36072,
        [NativeName("Name", "GL_COLOR_ATTACHMENT9")]
        ColorAttachment9 = 36073,
        [NativeName("Name", "GL_COLOR_ATTACHMENT10")]
        ColorAttachment10 = 36074,
        [NativeName("Name", "GL_COLOR_ATTACHMENT11")]
        ColorAttachment11 = 36075,
        [NativeName("Name", "GL_COLOR_ATTACHMENT12")]
        ColorAttachment12 = 36076,
        [NativeName("Name", "GL_COLOR_ATTACHMENT13")]
        ColorAttachment13 = 36077,
        [NativeName("Name", "GL_COLOR_ATTACHMENT14")]
        ColorAttachment14 = 36078,
        [NativeName("Name", "GL_COLOR_ATTACHMENT15")]
        ColorAttachment15 = 36079,
        [NativeName("Name", "GL_COLOR_ATTACHMENT16")]
        ColorAttachment16 = 36080,
        [NativeName("Name", "GL_COLOR_ATTACHMENT17")]
        ColorAttachment17 = 36081,
        [NativeName("Name", "GL_COLOR_ATTACHMENT18")]
        ColorAttachment18 = 36082,
        [NativeName("Name", "GL_COLOR_ATTACHMENT19")]
        ColorAttachment19 = 36083,
        [NativeName("Name", "GL_COLOR_ATTACHMENT20")]
        ColorAttachment20 = 36084,
        [NativeName("Name", "GL_COLOR_ATTACHMENT21")]
        ColorAttachment21 = 36085,
        [NativeName("Name", "GL_COLOR_ATTACHMENT22")]
        ColorAttachment22 = 36086,
        [NativeName("Name", "GL_COLOR_ATTACHMENT23")]
        ColorAttachment23 = 36087,
        [NativeName("Name", "GL_COLOR_ATTACHMENT24")]
        ColorAttachment24 = 36088,
        [NativeName("Name", "GL_COLOR_ATTACHMENT25")]
        ColorAttachment25 = 36089,
        [NativeName("Name", "GL_COLOR_ATTACHMENT26")]
        ColorAttachment26 = 36090,
        [NativeName("Name", "GL_COLOR_ATTACHMENT27")]
        ColorAttachment27 = 36091,
        [NativeName("Name", "GL_COLOR_ATTACHMENT28")]
        ColorAttachment28 = 36092,
        [NativeName("Name", "GL_COLOR_ATTACHMENT29")]
        ColorAttachment29 = 36093,
        [NativeName("Name", "GL_COLOR_ATTACHMENT30")]
        ColorAttachment30 = 36094,
        [NativeName("Name", "GL_COLOR_ATTACHMENT31")]
        ColorAttachment31 = 36095,
    }

    public class Framebuffer
    {
        private uint _framebuffer;

        private (PixelFormat format, uint texture)? _depthAttachment;

        private (PixelFormat format, FramebufferAttachment attachment, uint texture)[] _colorAttachments;
        private DrawBufferMode[] _colorAttachmentDrawBufferModes;
        private uint _width = 0;
        private uint _height = 0;
        private uint _requestedWidth;
        private uint _requestedHeight;

        public Framebuffer(Vector2D<uint>? initialSize, PixelFormat? depthAttachment, params PixelFormat[] colorAttachments)
        {
            if (depthAttachment != null)
            {
                _depthAttachment = (depthAttachment.Value, 0);
            }

            _colorAttachments = new (PixelFormat format,
                FramebufferAttachment attachment, uint texture)[colorAttachments.Length];
            _colorAttachmentDrawBufferModes = new DrawBufferMode[colorAttachments.Length];


            for (int i = 0; i < colorAttachments.Length; i++)
            {
                var format = colorAttachments[i];
                _colorAttachments[i] = (format,
                    (FramebufferAttachment)((int)FramebufferAttachment.ColorAttachment0 + i), 0);
                _colorAttachmentDrawBufferModes[i] = DrawBufferMode.ColorAttachment0 + i;
            }
            _requestedWidth = initialSize?.X ?? 0;
            _requestedHeight = initialSize?.Y ?? 0;
        }

        public uint GetDepthStencilTexture(out uint width, out uint height)
        {
            width = _width;
            height = _height;
            return DepthStencilTexture;
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

        public uint GetColorTexture(int attachment, out uint width, out uint height)
        {
            width = _width;
            height = _height;
            return GetColorTexture(attachment);
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
                var (format, texture) = _depthAttachment.Value;

                if (texture == 0)
                    texture = gl.GenTexture();

                gl.BindTexture(TextureTarget.Texture2D, texture);
                unsafe
                {
                    gl.TexImage2D(TextureTarget.Texture2D, 0,
                    TextureFormats.VdToGLInternalFormat(format),
                    width, height, 0,
                    TextureFormats.VdToGLPixelFormat(format),
                    TextureFormats.VdToPixelType(format), 
                    null);
                }

                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                gl.BindTexture(TextureTarget.Texture2D, 0);

                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                    TextureTarget.Texture2D, texture, 0);

                _depthAttachment = (format, texture);
            }

            for (int i = 0; i < _colorAttachments.Length; i++)
            {
                var (format, attachment, texture) = _colorAttachments[i];

                if (texture == 0)
                    texture = gl.GenTexture();

                gl.BindTexture(TextureTarget.Texture2D, texture);
                unsafe
                {
                    gl.TexImage2D(TextureTarget.Texture2D, 0,
                    TextureFormats.VdToGLInternalFormat(format),
                    width, height, 0,
                    TextureFormats.VdToGLPixelFormat(format),
                    TextureFormats.VdToPixelType(format), 
                    null);
                }

                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                gl.BindTexture(TextureTarget.Texture2D, 0);

                gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment, TextureTarget.Texture2D, texture, 0);

                _colorAttachments[i] = (format, attachment, texture);
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

        public unsafe void Use(GL gl, ReadOnlySpan<FramebufferColorAttachment> attachments = default)
        {
            Create(gl);

            gl.Viewport(0, 0, _width, _height);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

            if (attachments == default)
                gl.DrawBuffers(_colorAttachmentDrawBufferModes);
            else
            {
                fixed (FramebufferColorAttachment* ptr = attachments)
                {
                    gl.DrawBuffers((uint)attachments.Length, (DrawBufferMode*)ptr);
                }
            }
        }
    }
}
