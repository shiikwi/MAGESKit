using System;
using System.Collections.Generic;
using System.Text;

namespace PicTool
{
    public static class Utils
    {
        public static string ReadCString(BinaryReader br)
        {
            List<byte> bytes = new List<byte>();
            byte b;
            while ((b = br.ReadByte()) != 0)
            {
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
