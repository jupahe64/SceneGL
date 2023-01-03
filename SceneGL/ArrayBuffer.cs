using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL
{
    public class ArrayBuffer<TElement> where TElement : unmanaged
    {
        private uint? _buffer = null;

        private TElement[] _data;
        public ArrayBuffer(ReadOnlySpan<TElement> data)
        {
            _data = data.ToArray();
        }

        /// <summary>
        /// Uploads the buffer to the GPU (or whereever OpenGL decides to put it)
        /// </summary>
        /// <param name="usage">The buffer's expected usage</param>
        public unsafe void Upload(GL gl, BufferUsageARB usage = BufferUsageARB.StaticDraw)
        {
            if (_buffer.HasValue)
                return;

            _buffer = gl.GenBuffer();

            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _buffer.Value);
            gl.BufferData<TElement>(BufferTargetARB.ArrayBuffer, (uint)sizeof(TElement), _data, usage);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        }

        /// <summary>
        /// Updates the content of the Buffer and uploads it right away
        /// </summary>
        /// <param name="usage">The buffer's expected usage</param>
        public void Update(GL gl, ReadOnlySpan<TElement> data, BufferUsageARB usage = BufferUsageARB.StaticDraw)
        {
            _data = data.ToArray();
            Upload(gl, usage);
        }

        public void Bind(GL gl)
        {
            if(!_buffer.HasValue)
                throw new InvalidOperationException($"Array buffer hasn't been uploaded yet. " +
                    $"You must call {nameof(Upload)} before using this {nameof(ArrayBuffer<TElement>)}");

            gl.BindBuffer(BufferTargetARB.ArrayBuffer,_buffer.Value);
        }
    }
}
