using Silk.NET.OpenGL;
using System.Runtime.CompilerServices;

namespace SceneGL.GLHelpers
{
    public static class BufferHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe BufferRange SetBufferData<TData>(GL gl, BufferTargetARB bufferTarget, uint buffer,
            BufferUsageARB bufferUsage, ReadOnlySpan<TData> data)
            where TData : unmanaged
        {
            gl.BindBuffer(bufferTarget, buffer);
            gl.BufferData(bufferTarget, data, bufferUsage);
            gl.BindBuffer(bufferTarget, 0);
            return new BufferRange(buffer, (uint)(sizeof(TData) * data.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe BufferRange SetBufferData<TData>(GL gl, BufferTargetARB bufferTarget, uint buffer,
            BufferUsageARB bufferUsage, in TData data)
            where TData : unmanaged
        {
            gl.BindBuffer(bufferTarget, buffer);
            gl.BufferData(bufferTarget, (uint)sizeof(TData), in data, bufferUsage);
            gl.BindBuffer(bufferTarget, 0);
            return new BufferRange(buffer, (uint)sizeof(TData));
        }
    }
}