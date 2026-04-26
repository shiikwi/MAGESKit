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
        public uint Reserved1;
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
        private class AuxRecord
        {
            public string Tag;
            public BUPTable primary0;
            public BUPTable primary1;
            public List<BUPTable> list0 = new();
            public List<BUPTable> list1 = new();
        }

        private class AuxLayout
        {
            public List<BUPTable> RootTables = new();
            public AuxRecord? auxRecord;
        }

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
                var layout = ReadBUPTables(br, header, filepath);
                var tables = new List<BUPTable>();

                tables.AddRange(layout.RootTables);
                if (layout.auxRecord != null)
                {
                    //tables.Add(layout.auxRecord.primary0);
                    //tables.Add(layout.auxRecord.primary1);
                    tables.AddRange(layout.auxRecord.list0);
                    tables.AddRange(layout.auxRecord.list1);
                }

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

        private AuxLayout ReadBUPTables(BinaryReader br, BUPHeader header, string filepath)
        {
            var tables = new AuxLayout();

            br.BaseStream.Position = header.TableOffset;
            uint count = br.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                tables.RootTables.Add(Utils.BytesToStruct<BUPTable>(br));
            }

            if (header.AuxOffset != 0)
            {
                var auxRecords = ReadAuxRecords(br, header);
                var variantTag = GuessAuxTag(filepath);

                foreach (var record in auxRecords)
                {
                    if (record.Tag == variantTag)
                    {
                        tables.auxRecord = record;
                    }
                }
            }

            return tables;
        }

        private List<AuxRecord> ReadAuxRecords(BinaryReader br, BUPHeader header)
        {
            var records = new List<AuxRecord>();

            long auxStart = header.AuxOffset;
            long auxEnd = header.AuxOffset + header.AuxSize;

            br.BaseStream.Position = auxStart;
            uint count = br.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                var record = new AuxRecord();
                long startPos = br.BaseStream.Position;
                uint size = br.ReadUInt32();
                record.primary0 = Utils.BytesToStruct<BUPTable>(br);
                record.primary1 = Utils.BytesToStruct<BUPTable>(br);
                ushort list0Count = br.ReadUInt16();
                ushort list1Count = br.ReadUInt16();

                record.Tag = Utils.ReadPaddingTag(br);

                for (int j = 0; j < list0Count; j++)
                {
                    var t = Utils.BytesToStruct<BUPTable>(br);
                    if (t.Offset == 0) continue;
                    record.list0.Add(t);
                }
                for (int j = 0; j < list1Count; j++)
                {
                    var t = Utils.BytesToStruct<BUPTable>(br);
                    if (t.Offset == 0) continue;
                    record.list1.Add(t);
                }

                records.Add(record);
                br.BaseStream.Position = startPos + size;
            }
            return records;
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
            int canvasW = blocks.Max(b => b.X + b.W) - blocks.Min(b => b.X);
            int canvasH = blocks.Max(b => b.Y + b.H) - blocks.Min(b => b.Y);

            using (Bitmap canvas = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb))
            {
                foreach (var block in blocks)
                {
                    int dstX = block.X - blocks.Min(b => b.X);
                    int dstY = block.Y - blocks.Min(b => b.Y);
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

        private string GuessAuxTag(string filepath)
        {
            string name = Path.GetFileNameWithoutExtension(filepath);
            int pos = name.LastIndexOf('_');
            if (pos >= 0 && pos + 1 < name.Length)
                return name.Substring(pos + 1);

            return string.Empty;
        }
    }


}
