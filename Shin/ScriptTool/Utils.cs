using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;

namespace ScriptTool
{
    public static class Utils
    {
        public static string ReadLenString(BinaryReader br)
        {
            byte len = br.ReadByte();
            var strBytes = br.ReadBytes(len);
            if (strBytes[len - 1] == 0)
            {
                return Encoding.UTF8.GetString(strBytes, 0, len - 1);
            }
            else
            {
                return Encoding.UTF8.GetString(strBytes);
            }
        }

    }
}
