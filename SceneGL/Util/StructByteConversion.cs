using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.Util
{
    internal static class StructByteConversion
    {
        public unsafe static T ToStruct<T>(this ReadOnlySpan<byte> data) where T : unmanaged
        {
            fixed (byte* ptr = data)
            {
                var result = (T)Marshal.PtrToStructure<T>((IntPtr)ptr)!;
                return result;
            }
        }

        public unsafe static byte[] ToByteArray<T>(this T data) where T : unmanaged
        {
            var result = new byte[sizeof(T)];
            var pResult = GCHandle.Alloc(result, GCHandleType.Pinned);
            Marshal.StructureToPtr(data, pResult.AddrOfPinnedObject(), true);
            pResult.Free();
            return result;
        }
    }
}
