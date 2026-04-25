using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
#pragma warning disable

namespace PicTool
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PICHeader
    {
        public ushort Width;
        public ushort Height;
        public ushort TextureWidth;
        public ushort TextureHeight;
        public uint ImageFormat;
        public uint BlockCount;
        public uint HashId;
        public uint AnimParam;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PICBlock
    {
        public ushort X;
        public ushort Y;
        public uint Offset;
        public uint DataSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LayerHeader
    {
        public ushort Flags;
        public ushort V4;
        public ushort V5;
        public ushort V13;
        public ushort DestX;
        public ushort DestY;
        public ushort Width;
        public ushort Height;
        public uint CompressedSize;
    }

    public class ImagePIC
    {
        public void ConvertPIC(string filepath)
        {
            string outPath = Path.ChangeExtension(filepath, ".png");

            using (var fs = new FileStream(filepath, FileMode.Open))
            using (var br = new BinaryReader(fs))
            {
                var sig = br.ReadUInt32();
                if (sig != 0x34434950) // "PIC4"
                    throw new Exception("Not support PIC file type.");
                var version = br.ReadUInt32();
                if (version != 0x03)
                    throw new Exception($"Not support veriosn{version}");

                var fileSize = br.ReadUInt32();
                PICHeader header = Utils.BytesToStruct<PICHeader>(br);

                List<PICBlock> blocks = new List<PICBlock>();
                for (int i = 0; i < header.BlockCount; i++)
                {
                    blocks.Add(Utils.BytesToStruct<PICBlock>(br));
                }

                List<Block> outBlocks = new List<Block>();
                for (int i = 0; i < blocks.Count; i++)
                {
                    var block = blocks[i];
                    br.BaseStream.Seek(block.Offset, SeekOrigin.Begin);
                    LayerHeader layer = Utils.BytesToStruct<LayerHeader>(br);

                    int srcPitch = (layer.Width + 3) & ~3;
                    int uncompressedSize = 1024 + srcPitch * layer.Height;
                    bool hasAlphaMap = (layer.Flags & 1) == 0;
                    if (hasAlphaMap)
                    {
                        uncompressedSize += srcPitch * layer.Height;
                    }

                    long payloadSize = layer.CompressedSize > 0 ? layer.CompressedSize : uncompressedSize;
                    long payloadOffset = block.Offset + block.DataSize - payloadSize;

                    br.BaseStream.Seek(payloadOffset, SeekOrigin.Begin);

                    byte[] decompressed;
                    if (layer.CompressedSize > 0)
                    {
                        byte[] compressed = br.ReadBytes((int)layer.CompressedSize);
                        decompressed = ShinLzss.Decompress(compressed, uncompressedSize);
                    }
                    else
                    {
                        decompressed = br.ReadBytes(uncompressedSize);
                    }

                    byte[] rgbaData = ApplyPalette(decompressed, layer.Width, layer.Height, srcPitch, hasAlphaMap);
                    outBlocks.Add(new Block
                    {
                        data = rgbaData,
                        X = block.X,
                        Y = block.Y,
                        W = layer.Width,
                        H = layer.Height
                    });
                }

                CombinePIC(header, outBlocks, outPath);
            }
        }

        private byte[] ApplyPalette(byte[] decompressed, int width, int height, int srcPitch, bool hasAlphaMap)
        {
            int dstPitch = (width * 4 + 3) & ~3;
            byte[] bgraData = new byte[dstPitch * height];

            int indexMapOffset = 1024;
            int alphaMapOffset = 1024 + srcPitch * height;

            for (int y = 0; y < height; y++)
            {
                int srcRow = indexMapOffset + y * srcPitch;
                int alphaRow = alphaMapOffset + y * srcPitch;
                int dstRow = y * dstPitch;

                for (int x = 0; x < width; x++)
                {
                    byte colorIdx = decompressed[srcRow + x];
                    int palOffset = colorIdx * 4;

                    byte r = decompressed[palOffset + 0];
                    byte g = decompressed[palOffset + 1];
                    byte b = decompressed[palOffset + 2];
                    byte a = decompressed[palOffset + 3];

                    if (hasAlphaMap)
                    {
                        a = decompressed[alphaRow + x];
                    }

                    int p = dstRow + x * 4;
                    bgraData[p + 0] = b; // B
                    bgraData[p + 1] = g; // G
                    bgraData[p + 2] = r; // R
                    bgraData[p + 3] = a; // A
                }
            }
            return bgraData;
        }

        private void CombinePIC(PICHeader header, List<Block> blocks, string outputPath)
        {
            int canvasW = header.TextureWidth;
            int canvasH = header.TextureHeight;

            using (Bitmap canvas = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb))
            {
                foreach (var block in blocks)
                {
                    int endX = Math.Min(canvasW, block.X + block.W);
                    int endY = Math.Min(canvasH, block.Y + block.H);

                    int rectW = endX - block.X;
                    int rectH = endY - block.Y;
                    if (rectW <= 0 || rectH <= 0) continue;

                    BitmapData canvasData = canvas.LockBits(
                        new Rectangle(block.X, block.Y, rectW, rectH),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppArgb
                    );

                    IntPtr ptr = canvasData.Scan0;
                    int canvasStride = canvasData.Stride;
                    int blockStride = (block.W * 4 + 3) & ~3;
                    unsafe
                    {
                        byte* pCanvas = (byte*)ptr.ToPointer();

                        for (int y = 0; y < rectH; y++)
                        {
                            int srcOffset = y * blockStride;
                            int dstOffset = y * canvasStride;
                            Marshal.Copy(block.data, srcOffset, new IntPtr(pCanvas + dstOffset), rectW * 4);
                        }
                    }

                    canvas.UnlockBits(canvasData);
                }
                canvas.Save(outputPath, ImageFormat.Png);
            }
        }

        private struct Block
        {
            public byte[] data;
            public int X;
            public int Y;
            public int W;
            public int H;
        }
    }
}