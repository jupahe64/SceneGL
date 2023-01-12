using Silk.NET.OpenGL;
using System.Runtime.CompilerServices;

namespace SceneGL.GLHelpers
{
    public static class BufferHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint CreateBuffer(GL gl)
        {
            uint buffer = gl.GenBuffer();

            gl.BindBuffer(BufferTargetARB.UniformBuffer, buffer);
            gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe BufferRange CreateBuffer<TData>(GL gl,
            BufferUsageARB bufferUsage, ReadOnlySpan<TData> data)
            where TData : unmanaged
        {
            uint buffer = gl.GenBuffer();

            gl.BindBuffer(BufferTargetARB.UniformBuffer, buffer);
            gl.BufferData(BufferTargetARB.UniformBuffer, data, bufferUsage);
            gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
            return new BufferRange(buffer, (uint)(sizeof(TData) * data.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe BufferRange CreateBuffer<TData>(GL gl,
            BufferUsageARB bufferUsage, in TData data)
            where TData : unmanaged
        {
            uint buffer = gl.GenBuffer();

            gl.BindBuffer(BufferTargetARB.UniformBuffer, buffer);
            gl.BufferData(BufferTargetARB.UniformBuffer, (uint)sizeof(TData), in data, bufferUsage);
            gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
            return new BufferRange(buffer, (uint)sizeof(TData));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe BufferRange SetBufferData<TData>(GL gl, uint buffer,
            BufferUsageARB bufferUsage, ReadOnlySpan<TData> data)
            where TData : unmanaged
        {
            gl.BindBuffer(BufferTargetARB.UniformBuffer, buffer);
            gl.BufferData(BufferTargetARB.UniformBuffer, data, bufferUsage);
            gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
            return new BufferRange(buffer, (uint)(sizeof(TData) * data.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe BufferRange SetBufferData<TData>(GL gl, uint buffer,
            BufferUsageARB bufferUsage, in TData data)
            where TData : unmanaged
        {
            gl.BindBuffer(BufferTargetARB.UniformBuffer, buffer);
            gl.BufferData(BufferTargetARB.UniformBuffer, (uint)sizeof(TData), in data, bufferUsage);
            gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
            return new BufferRange(buffer, (uint)sizeof(TData));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void UpdateBufferData<TData>(GL gl, BufferRange bufferRange, ReadOnlySpan<TData> data)
            where TData : unmanaged
        {
            if (bufferRange.Size < sizeof(TData) * data.Length)
                throw new ArgumentException($"{nameof(data)} doesn't fit in provided {nameof(BufferRange)}");

            gl.BindBuffer(BufferTargetARB.UniformBuffer, bufferRange.Buffer);
            gl.BufferSubData(BufferTargetARB.UniformBuffer, bufferRange.Offset, data);
            gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void UpdateBufferData<TData>(GL gl, BufferRange bufferRange, in TData data)
            where TData : unmanaged
        {
            if (bufferRange.Size < sizeof(TData))
                throw new ArgumentException($"{nameof(data)} doesn't fit in provided {nameof(BufferRange)}");

            gl.BindBuffer(BufferTargetARB.UniformBuffer, bufferRange.Buffer);
            gl.BufferSubData(BufferTargetARB.UniformBuffer, bufferRange.Offset, (uint)sizeof(TData), in data);
            gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
        }
    }
}