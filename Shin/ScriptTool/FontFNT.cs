using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace ScriptTool
{
    public class FontFNT
    {
        private sealed class FntMeta
        {
            public uint Unk0;
            public ushort PageCount;
            public ushort GlyphCount;
            public ushort RecordSize;
            public ushort RecordPad;
            public ushort Metric0;
            public ushort Metric1;
            public ushort Metric2;
            public ushort Metric3;
            public int RecordStart;

            public List<PageMap> Pages = new();
            public List<GlyphRecord> Records = new();
            public Dictionary<int, int> CodepointToGlyph = new();
        }

        private sealed class PageMap
        {
            public ushort PageId;
            public ushort[] GlyphIndices = Array.Empty<ushort>();
        }

        private sealed class GlyphRecord
        {
            public short OffsetX;
            public ushort OffsetY;
            public ushort Width;
            public ushort Height;
            public ushort AdvanceX16;
            public ushort AdvanceY16;
            public uint Hash;
            public uint CompressedSize;
            public uint DataOffset;
        }

        public void MapFont(string fntpath)
        {
            var outDir = Path.Combine(Path.GetDirectoryName(fntpath), Path.GetFileNameWithoutExtension(fntpath) + "Unpack");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            using (var fs = new FileStream(fntpath, FileMode.Open))
            using (var br = new BinaryReader(fs))
            {
                var magic = br.ReadUInt32();
                var version = br.ReadUInt32();
                var fileSize = br.ReadUInt32();
                if (magic != 0x36544E46)  //FNT6
                {
                    throw new Exception("Not support FNT file type.");
                }

                var meta = ReadMeta(br);
                var glyphMasks = DecodeGlyphMasks(br, meta);
            }
        }

        private FntMeta ReadMeta(BinaryReader br)
        {
            var meta = new FntMeta()
            {
                Unk0 = br.ReadUInt32(),
                PageCount = br.ReadUInt16(),
                GlyphCount = br.ReadUInt16(),
                RecordSize = br.ReadUInt16(),
                RecordPad = br.ReadUInt16(),
                Metric0 = br.ReadUInt16(),
                Metric1 = br.ReadUInt16(),
                Metric2 = br.ReadUInt16(),
                Metric3 = br.ReadUInt16(),
            };

            var pageMaps = new List<PageMap>();
            for (int i = 0; i < meta.PageCount; i++)
            {
                ushort pageId = br.ReadUInt16();
                ushort[] glyphIndices = new ushort[256];
                for (int j = 0; j < 256; j++)
                    glyphIndices[j] = br.ReadUInt16();

                pageMaps.Add(new PageMap
                {
                    PageId = pageId,
                    GlyphIndices = glyphIndices,
                });
            }

            meta.RecordStart = (int)br.BaseStream.Position;
            var records = new List<GlyphRecord>();
            for (int i = 0; i < meta.GlyphCount; i++)
            {
                var record = new GlyphRecord
                {
                    OffsetX = br.ReadInt16(),
                    OffsetY = br.ReadUInt16(),
                    Width = br.ReadUInt16(),
                    Height = br.ReadUInt16(),
                    AdvanceX16 = br.ReadUInt16(),
                    AdvanceY16 = br.ReadUInt16(),
                    Hash = br.ReadUInt32(),
                    CompressedSize = br.ReadUInt32(),
                    DataOffset = br.ReadUInt32(),
                };
                records.Add(record);
            }

            Dictionary<int, int> codepointToGlyph = new Dictionary<int, int>();
            for (int p = 0; p < pageMaps.Count; p++)
            {
                int hi = pageMaps[p].PageId;
                for (int lo = 0; lo < 256; lo++)
                {
                    int glyphIndex = pageMaps[p].GlyphIndices[lo];
                    if (glyphIndex == 0xFFFF)
                        continue;

                    int codepoint = (hi << 8) | lo;
                    codepointToGlyph[codepoint] = glyphIndex;
                }
            }

            meta.Pages = pageMaps;
            meta.Records = records;
            meta.CodepointToGlyph = codepointToGlyph;

            return meta;
        }

        private Dictionary<int, byte[]> DecodeGlyphMasks(BinaryReader br, FntMeta meta)
        {
            Dictionary<int, byte[]> masks = new Dictionary<int, byte[]>();
            for (int i = 0; i < meta.Records.Count; i++)
            {
                GlyphRecord record = meta.Records[i];
                if(record.Width == 0 || record.Height == 0 || record.DataOffset == 0 || record.CompressedSize == 0)
                {
                    masks[i] = Array.Empty<byte>();
                    continue;
                }

                br.BaseStream.Position = record.DataOffset;
                byte[] compressed = br.ReadBytes((int)record.CompressedSize);
                byte[] mask = InnerDeflate(compressed);

                masks[i] = mask;
            }
            return masks;
        }

        private byte[] InnerDeflate(byte[] compressed)
        {
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }

    }
}
