using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace ScriptTool
{
    public class ScriptSnr
    {
        private struct SnrHeader
        {
            public uint Magic;
            public uint FileSize;
            public uint TotalCount;
            public uint Version;
            public uint Field0;
            public uint Reserved0;
            public uint Reserved1;
            public uint Reserved2;
            public uint DataOffset;

            public uint UiSymbolSectionOffset;     // sec9
            public uint ResSectionOffset;          // sec10
            public uint EmptySectionOffset;        // sec11
            public uint BgmSectionOffset;          // sec12
            public uint SeSectionOffset;           // sec13
            public uint MovieSectionOffset;        // sec14
            public uint MonsSectionOffset;         // sec15
            public uint PrefixMapSectionOffset;    // sec16
            public uint EventGroupSectionOffset;   // sec17

            public uint Reserved3;
            public uint Reserved4;
        }

        private struct Entry
        {
            public string Name;
            public uint Value;
        }

        private struct PrefixMapEntry
        {
            public string Name;
            public ushort Value;
        }

        private struct EventGroupEntry
        {
            public string Name;
            public List<ushort> ItemIndices;
        }

        private struct MainSnr
        {
            public List<string> UiSymbolStrings;
            public List<string> ResStrings;
            //EMPTY
            public List<string> BgmStrings;
            public List<string> SeStrings;
            public List<Entry> MovieEntries;
            public List<Entry> MonsEntries;
            public List<PrefixMapEntry> PrefixMapEntries;
            public List<EventGroupEntry> EventGroups;
        }

        public void ParseExport(string inPath)
        {
            using var fs = new FileStream(inPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            var snr = new MainSnr();
            var snrHeader = new SnrHeader();
            snrHeader = ReadHeader(br);

            snr.UiSymbolStrings = ReadStringSection(br, snrHeader.SeSectionOffset);
            snr.ResStrings = ReadStringSection(br, snrHeader.ResSectionOffset);
            snr.BgmStrings = ReadStringSection(br, snrHeader.BgmSectionOffset);
            snr.SeStrings = ReadStringSection(br, snrHeader.SeSectionOffset);
            snr.MovieEntries = ReadEntrySection(br, snrHeader.MovieSectionOffset);
            snr.MonsEntries = ReadEntrySection(br, snrHeader.MonsSectionOffset);
            snr.PrefixMapEntries = ReadPrefixMapSection(br, snrHeader.PrefixMapSectionOffset);
            snr.EventGroups = ReadEventGroupSection(br, snrHeader.EventGroupSectionOffset);

            var outPath = Path.ChangeExtension(inPath, ".json");
            var json = JsonConvert.SerializeObject(snr, Formatting.Indented);
            File.WriteAllText(outPath, json);
        }

        private SnrHeader ReadHeader(BinaryReader br)
        {
            return new SnrHeader
            {
                Magic = br.ReadUInt32(),
                FileSize = br.ReadUInt32(),
                TotalCount = br.ReadUInt32(),
                Version = br.ReadUInt32(),
                Field0 = br.ReadUInt32(),
                Reserved0 = br.ReadUInt32(),
                Reserved1 = br.ReadUInt32(),
                Reserved2 = br.ReadUInt32(),
                DataOffset = br.ReadUInt32(),

                UiSymbolSectionOffset = br.ReadUInt32(),
                ResSectionOffset = br.ReadUInt32(),
                EmptySectionOffset = br.ReadUInt32(),
                BgmSectionOffset = br.ReadUInt32(),
                SeSectionOffset = br.ReadUInt32(),
                MovieSectionOffset = br.ReadUInt32(),
                MonsSectionOffset = br.ReadUInt32(),
                PrefixMapSectionOffset = br.ReadUInt32(),
                EventGroupSectionOffset = br.ReadUInt32(),

                Reserved3 = br.ReadUInt32(),
                Reserved4 = br.ReadUInt32()
            };
        }

        private List<string> ReadStringSection(BinaryReader br, uint offset)
        {
            var strs = new List<string>();
            br.BaseStream.Position = offset;
            var sectionSize = br.ReadUInt32();
            var sectionCount = br.ReadUInt32();
            for (int i = 0; i < sectionCount; i++)
            {
                strs.Add(Utils.ReadLenString(br));
            }
            return strs;
        }

        private List<Entry> ReadEntrySection(BinaryReader br, uint offset)
        {
            var result = new List<Entry>();
            br.BaseStream.Position = offset;
            var sectionSize = br.ReadUInt32();
            var sectionCount = br.ReadUInt32();
            for (int i = 0; i < sectionCount; i++)
            {
                result.Add(new Entry
                {
                    Name = Utils.ReadLenString(br),
                    Value = br.ReadUInt32(),
                });
            }
            return result;
        }

        private List<PrefixMapEntry> ReadPrefixMapSection(BinaryReader br, uint offset)
        {
            var result = new List<PrefixMapEntry>();
            br.BaseStream.Position = offset;
            var sectionSize = br.ReadUInt32();
            var sectionCount = br.ReadUInt32();
            for (int i = 0; i < sectionCount; i++)
            {
                result.Add(new PrefixMapEntry
                {
                    Name = Utils.ReadLenString(br),
                    Value = br.ReadUInt16(),
                });
            }
            return result;
        }


        private List<EventGroupEntry> ReadEventGroupSection(BinaryReader br, uint offset)
        {
            var result = new List<EventGroupEntry>();
            br.BaseStream.Position = offset;
            var sectionCount = br.ReadUInt32();
            br.BaseStream.Seek(2, SeekOrigin.Current);  //skip 0x00
            for (int i = 0; i < sectionCount; i++)
            {
                var entry = new EventGroupEntry();
                entry.Name = Utils.ReadLenString(br);
                entry.ItemIndices = new List<ushort>();
                var paramsCount = br.ReadUInt16();
                for (int j = 0; j < paramsCount; j++)
                {
                    entry.ItemIndices.Add(br.ReadUInt16());
                }
                br.ReadUInt16(); //skip 0x00
                result.Add(entry);
            }
            return result;
        }
    }
}
