using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PicTool
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BUPHeader
    {
        public ushort BoundL;
        public ushort BoundT;
        public ushort BoundR;
        public ushort BoundB;
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
    public struct BUPChunk
    {
        public BUPTable table;
        public uint Type;
        public ushort Unk1;
        public ushort Unk2;
        public ushort X;
        public ushort Y;
        public ushort Width;
        public ushort Height;
        public uint DataLen;
        public uint Unk14;
        public byte[] Meta;
        public byte[] ImageStream;

        public static BUPChunk Read(BinaryReader br, BUPTable table)
        {
            var chunk = new BUPChunk();
            chunk.table = table;
            chunk.Type = br.ReadUInt32();
            chunk.Unk1 = br.ReadUInt16();
            chunk.Unk2 = br.ReadUInt16();
            chunk.X = br.ReadUInt16();
            chunk.Y = br.ReadUInt16();
            chunk.Width = br.ReadUInt16();
            chunk.Height = br.ReadUInt16();
            chunk.DataLen = br.ReadUInt32();
            chunk.Unk14 = br.ReadUInt32();

            br.BaseStream.Position = table.Offset;
            chunk.Meta = br.ReadBytes((int)(table.Size - chunk.DataLen));
            chunk.ImageStream = br.ReadBytes((int)chunk.DataLen);

            return chunk;
        }

    }

    public class ImageBUP
    {
        private List<BUPTable> bupTables = new();
        private List<BUPChunk> bupChunks = new();

        public void ConvertBUP(string filepath)
        {
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
                br.BaseStream.Position = header.TableOffset;
                uint count = br.ReadUInt32();
                for (int i = 0; i < count; i++)
                {
                    bupTables.Add(Utils.BytesToStruct<BUPTable>(br));
                }

                foreach (var table in bupTables)
                {
                    bupChunks.Add(BUPChunk.Read(br, table));
                }
            }
        }
    }


}
