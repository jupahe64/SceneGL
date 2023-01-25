using Silk.NET.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.GLHelpers
{
    public static class InstanceBufferHelper
    {
        public unsafe static InstanceBufferBinding UploadData<TInstanceData>(GL gl, 
            uint buffer, int maxCountPerBlock, ReadOnlySpan<TInstanceData> data,
            BufferUsageARB bufferUsage = BufferUsageARB.StaticDraw)
            where TInstanceData : unmanaged
        {
            if (data.Length <= maxCountPerBlock)
            {
                var range = BufferHelper.SetBufferData(gl, buffer, bufferUsage, data);
                return new InstanceBufferBinding(
                    new (int count, BufferRange range)[]
                    {
                        (maxCountPerBlock, range)
                    });
            }

            int neededBlocks = (data.Length + maxCountPerBlock-1) / maxCountPerBlock;
            var blocks = new (int count, BufferRange range)[neededBlocks];

            uint alignment = (uint)gl.GetInteger(GLEnum.UniformBufferOffsetAlignment);
            bool isAligned = (sizeof(TInstanceData) * maxCountPerBlock) % alignment == 0;

            int itemsLeft = data.Length;

            int offset = 0;

            uint totalSize = 0;

            for (int i = 0; i < neededBlocks; i++)
            {
                int blockItemCount = Math.Min(itemsLeft, maxCountPerBlock);
                uint blockSize = (uint)(blockItemCount * sizeof(TInstanceData));

                blocks[i] = new(blockItemCount, 
                    new BufferRange(buffer, offset, blockSize));

                itemsLeft -= maxCountPerBlock;

                offset += (int)blockSize;
                totalSize = (uint)offset;

                //align offset
                if(offset % alignment != 0)
                    offset += (int)(alignment - (offset % alignment));
            }

            if(isAligned)
            {
                BufferHelper.SetBufferData(gl, buffer, bufferUsage, data);
                return new InstanceBufferBinding(blocks);
            }

            byte[] tempBuffer = ArrayPool<byte>.Shared.Rent((int)totalSize);

            Span<byte> tempBufferSpan = tempBuffer.AsSpan(0, (int)totalSize);

            int dataPointer = 0;

            for (int i = 0; i < neededBlocks; i++)
            {
                var (count, range) = blocks[i];

                var source = data.Slice(dataPointer, count);
                var dest = tempBufferSpan.Slice(range.Offset, (int)range.Size);

                MemoryMarshal.AsBytes(source).CopyTo(
                    dest);

                dataPointer += count;
            }

            BufferHelper.SetBufferData(gl, buffer, bufferUsage, (ReadOnlySpan<byte>)tempBuffer);

            ArrayPool<byte>.Shared.Return(tempBuffer);
            return new InstanceBufferBinding(blocks);
        }
    }
    public record struct InstanceBufferBinding(IReadOnlyList<(int count, BufferRange range)> Blocks);
}
