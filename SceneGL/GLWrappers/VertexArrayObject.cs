using SceneGL.Util;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.GLWrappers
{
    public record VertexAttributeInfo(uint VertexBuffer, uint ShaderLocation, int Size, VertexAttribPointerType Type, bool Normalized, uint Stride, uint Offset, uint Divisor = 0);

    /// <summary>
    /// A thin wrapper of an OpenGL VertexArrayObject that simplifies common operations and working with multiple contexts
    /// </summary>
    public class VertexArrayObject : AbstractGLResourceHolder
    {
        private uint? _indexBuffer;

        private VertexAttributeInfo[] _attributeInfos;

        public VertexArrayObject(uint? indexBuffer, params VertexAttributeInfo[] attributeInfos)
        {
            _indexBuffer = indexBuffer;
            _attributeInfos = attributeInfos;
        }

        public void Bind(GL gl)
        {
            uint vao = GetOrCreateResource(gl);

            gl.BindVertexArray(vao);
        }

        protected override uint CreateResource(GL gl)
        {
            uint vao = gl.CreateVertexArray();
            gl.BindVertexArray(vao);

            if (_indexBuffer != null)
                gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer.Value);

            for (uint i = 0; i < _attributeInfos.Length; i++)
            {
                var info = _attributeInfos[i];

                gl.BindBuffer(BufferTargetARB.ArrayBuffer, info.VertexBuffer);

                unsafe //bruh
                {
                    gl.VertexAttribPointer(info.ShaderLocation, info.Size, info.Type, info.Normalized, info.Stride, (void*)info.Offset);
                }

                if (info.Divisor > 0)
                    gl.VertexAttribDivisor(info.ShaderLocation, info.Divisor);

                gl.EnableVertexAttribArray(info.ShaderLocation);
            }

            gl.BindVertexArray(0);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

            return vao;
        }

        protected override void CleanUpResource(GL gl, uint vao)
        {
            gl.DeleteVertexArray(vao);
        }
    }
}
