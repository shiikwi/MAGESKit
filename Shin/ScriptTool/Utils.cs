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

        public static string ReadScString(BinaryReader br, ushort len)
        {
            byte[] raw = br.ReadBytes(len);
            if (raw.Length == 0) return "";

            List<byte> result = new List<byte>();

            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == 0x40 && i + 1 < raw.Length && raw[i + 1] == 0x72)
                {
                    byte[] newLine = Encoding.UTF8.GetBytes("\n");
                    result.AddRange(newLine);
                    i++;
                }
                else
                {
                    result.Add(raw[i]);
                }
            }

            int finalLen = result.Count;
            while (finalLen > 0 && result[finalLen - 1] == 0)
            {
                finalLen--;
            }

            return Encoding.UTF8.GetString(result.ToArray(), 0, finalLen);
        }

        public static string? TryChar(int codepoint)
        {
            try
            {
                return char.ConvertFromUtf32(codepoint);
            }
            catch
            {
                return null;
            }
        }
    }

}
