using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
#pragma warning disable

namespace PicTool
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BUPHeader
    {
        public short BoundL;
        public short BoundT;
        public short BoundR;
        public short BoundB;
        public uint Flag;
        public uint CRC;
        public uint Reversed;
        public uint TableOffset;
        public uint TableSize;
        public uint AuxOffset;
        public uint AuxSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BUPTable
    {
        public uint Offset;
        public uint Size;
        public uint Hash;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BUPLayerHeader
    {
        public ushort Flags;
        public ushort Count0;
        public ushort Count1;
        public ushort ExtraCount;
        public short X;
        public short Y;
        public ushort Width;
        public ushort Height;
        public uint PackedSize;
    }

    public class ImageBUP : ImagePIC
    {
        public void ConvertBUP(string filepath)
        {
            string outPath = Path.ChangeExtension(filepath, ".png");
            using (var fs = new FileStream(filepath, FileMode.Open))
            using (var br = new BinaryReader(fs))
            {
                var magic = br.ReadUInt32();
                var version = br.ReadUInt32();
                var fileSize = br.ReadUInt32();
                if (magic != 0x34505542)  //BUP4
                {
                    throw new Exception("Not support BUP file type.");
                }

                var header = Utils.BytesToStruct<BUPHeader>(br);
                var tables = ReadBUPTables(br, header);

                List<Block> blocks = new List<Block>();
                foreach (var table in tables)
                {
                    br.BaseStream.Position = table.Offset;
                    BUPLayerHeader layer = Utils.BytesToStruct<BUPLayerHeader>(br);
                    byte[] rgbaData = DecodeLayer(br, table, layer);

                    blocks.Add(new Block
                    {
                        data = rgbaData,
                        X = layer.X,
                        Y = layer.Y,
                        W = layer.Width,
                        H = layer.Height,
                    });
                }
                CombineBUP(header, blocks, outPath);
            }
        }

        private List<BUPTable> ReadBUPTables(BinaryReader br, BUPHeader header)
        {
            List<BUPTable> tables = new List<BUPTable>();

            br.BaseStream.Position = header.TableOffset;
            uint count = br.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                tables.Add(Utils.BytesToStruct<BUPTable>(br));
            }

            if (header.AuxOffset != 0)
                Console.WriteLine("Aux Read Not Implemented yet.");

            return tables;
        }


        private byte[] DecodeLayer(BinaryReader br, BUPTable table, BUPLayerHeader layer)
        {
            long payloadOffset = table.Offset + Marshal.SizeOf<BUPLayerHeader>() + (layer.Count0 + layer.Count1) * 8 + layer.ExtraCount * 2;

            int srcPitch = (layer.Width + 3) & ~3;
            bool hasSeparateAlpha = (layer.Flags & 1) == 0;

            int uncompressedSize = 1024 + srcPitch * layer.Height;
            if (hasSeparateAlpha)
                uncompressedSize += srcPitch * layer.Height;

            br.BaseStream.Position = payloadOffset;
            byte[] decompressed;
            if (layer.PackedSize != 0)
            {
                byte[] compressed = br.ReadBytes((int)layer.PackedSize);
                decompressed = ShinLzss.Decompress(compressed, uncompressedSize);
            }
            else
            {
                decompressed = br.ReadBytes(uncompressedSize);
            }

            return ApplyPalette(decompressed, layer.Width, layer.Height, srcPitch, hasSeparateAlpha);
        }

        private void CombineBUP(BUPHeader header, List<Block> blocks, string outPath)
        {
            int canvasW = header.BoundR - header.BoundL;
            int canvasH = header.BoundB - header.BoundT;

            using (Bitmap canvas = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb))
            {
                foreach (var block in blocks)
                {
                    int dstX = block.X - header.BoundL;
                    int dstY = block.Y - header.BoundT;
                    int srcX = 0;
                    int srcY = 0;

                    if (dstX < 0)
                    {
                        srcX = -dstX;
                        dstX = 0;
                    }

                    if (dstY < 0)
                    {
                        srcY = -dstY;
                        dstY = 0;
                    }

                    int rectW = Math.Min(block.W - srcX, canvasW - dstX);
                    int rectH = Math.Min(block.H - srcY, canvasH - dstY);
                    if (rectW <= 0 || rectH <= 0)
                        continue;
                    BitmapData canvasData = canvas.LockBits(
                        new Rectangle(dstX, dstY, rectW, rectH),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppArgb
                    );

                    try
                    {
                        IntPtr ptr = canvasData.Scan0;
                        int canvasStride = canvasData.Stride;
                        int blockStride = (block.W * 4 + 3) & ~3;

                        unsafe
                        {
                            byte* pCanvas = (byte*)ptr.ToPointer();

                            for (int y = 0; y < rectH; y++)
                            {
                                int srcOffset = (srcY + y) * blockStride + srcX * 4;
                                int dstOffset = y * canvasStride;
                                Marshal.Copy(block.data, srcOffset, new IntPtr(pCanvas + dstOffset), rectW * 4);
                            }
                        }
                    }
                    finally
                    {
                        canvas.UnlockBits(canvasData);
                    }
                }

                canvas.Save(outPath, ImageFormat.Png);
            }
        }
    }


}
