using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.Util
{
    public static class BitUtils
    {
        public static IEnumerable<int> AllSetBits(ushort bits)
        {
            int index = 0;

            while (bits > 0)
            {
                if ((bits & 0x1) == 1) //scenario bit set at index
                    yield return index;

                bits >>= 1;
                index++;
            }
        }
    }
}
