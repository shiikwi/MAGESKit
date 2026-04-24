using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
#pragma warning disable

namespace PicTool
{
    public struct TXAEntry
    {
        public int Index;
        public ushort RecordSize;
        public ushort Id;
        public ushort Width;
        public ushort Height;
        public uint DataOffset;
        public uint CompressedSize;
        public uint WorkSize;
        public string Name;
    }


    public class ImageTXA
    {
        public void ConvertTXA(string filepath)
        {
            var outDir = Path.Combine(Path.GetDirectoryName(filepath), Path.GetFileNameWithoutExtension(filepath) + "Unpack");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            using (var fs = new FileStream(filepath, FileMode.Open))
            using (var br = new BinaryReader(fs))
            {
                var magic = br.ReadUInt32();
                var version = br.ReadUInt32();
                if (magic != 0x34415854) // "TXA4"
                    throw new Exception("Not support TXA file type.");

                var fileSize = br.ReadUInt32();
                var flags = br.ReadUInt32();
                var entryCount = br.ReadUInt32();
                var maxWorkSize = br.ReadUInt32();
                var recordTableSize = br.ReadUInt32();
                var reserved = br.ReadUInt32();

                var entries = ReadEntry(br, entryCount);
                foreach (var entry in entries)
                {
                    br.BaseStream.Position = entry.DataOffset;
                    byte[] decoded;
                    if (entry.CompressedSize != 0)
                    {
                        byte[] compressed = br.ReadBytes((int)entry.CompressedSize);
                        decoded = ShinLzss.Decompress(compressed, (int)entry.WorkSize);
                    }
                    else
                    {
                        decoded = br.ReadBytes((int)entry.WorkSize);
                    }

                    byte[] bgra = ApplyPalette(decoded, entry.Width, entry.Height);
                    string outPath = Path.Combine(outDir, $"{entry.Name}.png");
                    WritePng(outPath, bgra, entry.Width, entry.Height);
                }
            }
        }


        private List<TXAEntry> ReadEntry(BinaryReader br, uint count)
        {
            var entries = new List<TXAEntry>();
            br.BaseStream.Seek(0x20, SeekOrigin.Begin);

            for (int i = 0; i < count; i++)
            {
                long startPos = br.BaseStream.Position;
                var entry = new TXAEntry
                {
                    Index = i,
                    RecordSize = br.ReadUInt16(),
                    Id = br.ReadUInt16(),
                    Width = br.ReadUInt16(),
                    Height = br.ReadUInt16(),
                    DataOffset = br.ReadUInt32(),
                    CompressedSize = br.ReadUInt32(),
                    WorkSize = br.ReadUInt32(),
                    Name = Utils.ReadCString(br)
                };
                entries.Add(entry);
                br.BaseStream.Seek(startPos + entry.RecordSize, SeekOrigin.Begin);
            }
            return entries;
        }

        private byte[] ApplyPalette(byte[] decoded, int width, int height)
        {
            int srcPitch = (width + 3) & ~3;
            int dstPitch = width * 4;

            int paletteOffset = 0;
            int indexMapOffset = 0x400;

            int requiredSize = indexMapOffset + srcPitch * height;
            if (decoded.Length < requiredSize)
                throw new Exception($"Decoded TXA fail. Need 0x{requiredSize:X}, got 0x{decoded.Length:X}.");

            byte[] bgraData = new byte[dstPitch * height];

            for (int y = 0; y < height; y++)
            {
                int srcRow = indexMapOffset + y * srcPitch;
                int dstRow = y * dstPitch;

                for (int x = 0; x < width; x++)
                {
                    byte colorIndex = decoded[srcRow + x];
                    int pal = paletteOffset + colorIndex * 4;

                    byte r = decoded[pal + 0];
                    byte g = decoded[pal + 1];
                    byte b = decoded[pal + 2];
                    byte a = decoded[pal + 3];

                    int dst = dstRow + x * 4;
                    bgraData[dst + 0] = b;
                    bgraData[dst + 1] = g;
                    bgraData[dst + 2] = r;
                    bgraData[dst + 3] = a;
                }
            }

            return bgraData;
        }

        private void WritePng(string outPath, byte[] bgraData, int width, int height)
        {
            int srcStride = width * 4;

            using (Bitmap canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                BitmapData canvasData = canvas.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    for (int y = 0; y < height; y++)
                    {
                        IntPtr dst = IntPtr.Add(canvasData.Scan0, y * canvasData.Stride);
                        Marshal.Copy(bgraData, y * srcStride, dst, srcStride);
                    }
                }
                finally
                {
                    canvas.UnlockBits(canvasData);
                }

                canvas.Save(outPath, ImageFormat.Png);
            }
        }

    }
}
