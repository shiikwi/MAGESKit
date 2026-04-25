using System;
using System.Collections.Generic;
using System.Text;

namespace PicTool
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                var ext = Path.GetExtension(arg);

                if (ext == ".pic")
                {
                    var pic = new ImagePIC();
                    pic.ConvertPIC(arg);
                }
                else if (ext == ".txa")
                {
                    var txa = new ImageTXA();
                    txa.ConvertTXA(arg);
                }
                else if (ext == ".bup")
                {
                    var bup = new ImageBUP();
                    bup.ConvertBUP((arg));
                }
            }
        }

    }
}
