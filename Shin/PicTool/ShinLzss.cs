using System;
using System.Collections.Generic;
using System.Text;

namespace PicTool
{
    public static class ShinLzss
    {
        public static byte[] Decompress(byte[] src, int unpackSize)
        {
            byte[] dst = new byte[unpackSize];
            int srcPtr = 0, dstPtr = 0;
            uint flags = 0;

            while (srcPtr < src.Length && dstPtr < unpackSize)
            {
                if ((flags & 0x100) == 0)
                {
                    if (srcPtr >= src.Length) break;
                    flags = src[srcPtr++] | 0xFF00u;
                }

                if ((flags & 1) != 0)
                {
                    if (srcPtr + 1 >= src.Length) break;
                    ushort val = (ushort)((src[srcPtr] << 8) | src[srcPtr + 1]);
                    srcPtr += 2;

                    int length = (val >> 12) + 3;
                    int offset = (val & 0xFFF) + 1;

                    for (int i = 0; i < length; i++)
                    {
                        if (dstPtr < unpackSize)
                        {
                            dst[dstPtr] = dst[dstPtr - offset];
                            dstPtr++;
                        }
                    }
                }
                else
                {
                    if (srcPtr >= src.Length) break;
                    dst[dstPtr++] = src[srcPtr++];
                }
                flags >>= 1;
            }
            return dst;
        }

    }
}
