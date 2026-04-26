using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        public static T BytesToStruct<T>(BinaryReader br) where T : struct
        {
            byte[] bytes = br.ReadBytes(Marshal.SizeOf(typeof(T)));
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try { return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!; }
            finally { handle.Free(); }
        }

        public static string ReadPaddingTag(BinaryReader br)
        {
            long pos = br.BaseStream.Position;
            List<byte> bytes = new List<byte>();
            byte b;
            while ((b = br.ReadByte()) != 0)
            {
                bytes.Add(b);
            }

            br.BaseStream.Position = pos + ((bytes.Count + 4) & ~3);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

    }
}
