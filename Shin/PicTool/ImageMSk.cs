using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
#pragma warning disable CA1416


namespace PicTool
{
    public class ImageMSK
    {
        public void ConvertMSK(string filepath)
        {
            using (var fs = new FileStream(filepath, FileMode.Open))
            using (var br = new BinaryReader(fs))
            {
                var magic = br.ReadUInt32();
                var version = br.ReadUInt32();
                var fileSize = br.ReadUInt32();
                var hash = br.ReadUInt32();

                if (magic != 0x344B534D) // "MSK4"
                {
                    throw new InvalidDataException("Not support msk type.");
                }

                var width = br.ReadUInt16();
                var height = br.ReadUInt16();
                var dataOffset = br.ReadUInt32();

                br.BaseStream.Position = dataOffset;
                var packSize = br.ReadUInt32();

                int stride = (width + 0xF) & ~0xF;
                int unpackSize = stride * height;

                byte[] mask;
                if (packSize != 0)
                {
                    byte[] compress = br.ReadBytes((int)packSize);
                    mask = ShinLzss.Decompress(compress, unpackSize);
                }
                else
                {
                    mask = br.ReadBytes(unpackSize);
                }

                var bgra = BuildGrayBgra(mask, width, height, stride);

                using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                {
                    BitmapData data = bitmap.LockBits(
                        new Rectangle(0, 0, width, height),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppArgb
                        );
                    try
                    {
                        Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
                    }
                    finally
                    {
                        bitmap.UnlockBits(data);
                    }

                    bitmap.Save(Path.ChangeExtension(filepath, ".png"), ImageFormat.Png);
                }
            }
        }

        private byte[] BuildGrayBgra(byte[] mask, int width, int height, int stride)
        {
            byte[] bgra = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                int srcRow = y * stride;
                int dstRow = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    byte v = mask[srcRow + x];
                    int p = dstRow + x * 4;

                    bgra[p + 0] = v;
                    bgra[p + 1] = v;
                    bgra[p + 2] = v;
                    bgra[p + 3] = 255;
                }
            }

            return bgra;
        }

    }
}
