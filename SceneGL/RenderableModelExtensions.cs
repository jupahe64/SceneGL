using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL
{
    public static class RenderableModelExtensions
    {
        public unsafe static void DrawWithInstanceData(this RenderableModel model, GL gl,
            uint uniformBlockIndex, (uint elementSize, uint elementCount) uniformArrayInfo, BufferRange buffer)
        {
            var (elementSize, elementCount) = uniformArrayInfo;

            var blockSize = elementSize * elementCount;

            for (int i = 0; i < (buffer.Size + blockSize - 1) / blockSize; i++)
            {
                gl.BindBufferRange(BufferTargetARB.UniformBuffer, uniformBlockIndex, buffer.Buffer,
                    buffer.Offset + (nint)(i * blockSize),
                    blockSize);

                model.Draw(gl, elementCount);
            }
        }
    }
}
