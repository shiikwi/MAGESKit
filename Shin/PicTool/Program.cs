using System;
using System.Collections.Generic;
using System.Text;

namespace PicTool
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach(var arg in args)
            {
                var pic = new ImagePIC();
                pic.ConvertPIC(arg);
            }

        }
    }
}
