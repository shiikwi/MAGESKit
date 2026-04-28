using System;
using System.IO;

namespace ScriptTool
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var inFile = Path.GetFileName(args[0]);
            if (inFile == "main.snr")
            {
                var snr = new ScriptSnr();
                snr.ParseExport(inFile);
            }
            else if (inFile == "default.fnt")
            {
                var fnt = new FontFNT();
                fnt.MapFont(inFile);
            }

        }
    }
}
