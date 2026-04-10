using System;
using System.IO;

namespace ScriptTool
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            string inFile = "main.snr";
            var snr = new ScriptSnr();
            snr.ParseExport(inFile);
        }
    }
}
