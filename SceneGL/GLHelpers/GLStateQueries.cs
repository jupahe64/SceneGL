using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.GLHelpers
{
    public static class GLStateQueries
    {
        public unsafe static (int, string)[] GetUniformBufferBindings(GL gl)
        {
            int maxLength = gl.GetInteger((GLEnum)GetPName.MaxLabelLength);

            var maxBlocks = gl.GetInteger((GLEnum)GetPName.MaxCombinedUniformBlocks);

            (int, string)[] res = new (int, string)[maxBlocks];

            var buffer = stackalloc byte[maxLength];

            for (uint i = 0; i < maxBlocks; i++)
            {
                gl.GetInteger(GetPName.UniformBufferBinding, i, out int val);

                if (val == 0)
                    continue;

                gl.GetObjectLabel(ObjectIdentifier.Buffer, (uint)val, (uint)maxLength, out uint len, buffer);

                string label = Marshal.PtrToStringUTF8((IntPtr)buffer, (int)len);

                res[i] = (val, label);
            }

            return res;
        }
    }
}
