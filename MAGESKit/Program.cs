using System;
using System.Collections.Generic;
using System.Text;

namespace MAGESKit
{
    class Program
    {
        static readonly string TBLMAP = "CommonCharsetMap.json";

        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                string tblPath = Path.Combine(AppContext.BaseDirectory, TBLMAP);
                var ext = Path.GetExtension(arg);
                if (ext == ".msb")
                {
                    var msb = new Scriptmsb();
                    msb.FromBytes(arg, tblPath);
                }
            }
        }
    }
}