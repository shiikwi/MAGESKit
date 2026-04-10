using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using Newtonsoft.Json;

namespace MAGESKit
{
    public class Scriptmsb
    {
        private Dictionary<string, string> CharSetMap = new Dictionary<string, string>();

        private struct CommandLine
        {
            public uint Index;
            public uint RVOffset;
            public byte[] Data;
        }

        public void FromBytes(string inPath, string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            CharSetMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)!;

            var outPath = Path.ChangeExtension(inPath, ".txt");
            var sb = new StringBuilder();

            using (var fs = new FileStream(inPath, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                var magic = br.ReadUInt32();
                var version = br.ReadUInt32();
                var count = br.ReadUInt32();
                var textOffset = br.ReadUInt32();
                if (magic != 0x0053454D)
                    throw new Exception("Not valid msb file");

                var cmdLines = new List<CommandLine>();
                var cmdDict = new List<(uint index, uint offset)>();
                for (int i = 0; i < count; i++)
                {
                    var index = br.ReadUInt32();
                    var offset = br.ReadUInt32();
                    cmdDict.Add((index, offset));
                }

                for (int i = 0; i < cmdDict.Count; i++)
                {
                    uint curRel = cmdDict[i].offset;
                    uint nextRel;
                    if (i + 1 < cmdDict.Count)
                        nextRel = cmdDict[i + 1].offset;
                    else
                        nextRel = (uint)(fs.Length - textOffset);

                    br.BaseStream.Position = br.BaseStream.Seek(textOffset + curRel, SeekOrigin.Begin);
                    var data = br.ReadBytes((int)(nextRel - curRel));

                    cmdLines.Add(new CommandLine
                    {
                        Index = cmdDict[i].index,
                        RVOffset = curRel,
                        Data = data
                    });

                    sb.Append($"◇0x{(curRel + textOffset):X}◇");
                    //sb.Append(BitConverter.ToString(data).Replace("-", " "));
                    sb.Append(ParseLineData(data));
                    sb.Append("\n\n");
                }
                File.WriteAllText(outPath, sb.ToString());
            }

        }

        private string ParseLineData(byte[] data)
        {
            StringBuilder linesb = new StringBuilder();
            int i = 0;
            while (i < data.Length)
            {
                byte token = data[i];
                if (token < 0x80 || token == 0xFF) //command
                {
                    string OpCode = Enum.IsDefined(typeof(MsbOP), token) ? $"{{{(MsbOP)token}}}" : $"{{OP_{token:X2}}}";
                    switch ((MsbOP)token)
                    {
                        case MsbOP.LineBreak: i++; break;
                        case MsbOP.NameStart:
                            {
                                OpCode = "[";
                                i++;
                                break;
                            }
                        case MsbOP.NameEnd:
                            {
                                OpCode = "] ";
                                i++;
                                break;
                            }
                        case MsbOP.PauseEndLine: i++; break;
                        case MsbOP.SetColor:
                            {
                                linesb.Append(BuildParams(data, i + 1, 3));
                                i += 4;
                                break;
                            }
                        case MsbOP.OP_05: i++; break;
                        case MsbOP.OP_06: i++; break;
                        case MsbOP.TextWait:
                            {
                                linesb.Append(BuildParams(data, i + 1, 1));
                                i += 2;
                                break;
                            }
                        case MsbOP.PauseEndPage: i++; break;
                        case MsbOP.RubyStart: i++; break;
                        case MsbOP.RubyTextStart: i++; break;
                        case MsbOP.RubyTextEnd: i++; break;
                        case MsbOP.SetFontSize:
                            {
                                linesb.Append(BuildParams(data, i + 1, 2));
                                i += 3;
                                break;
                            }
                        case MsbOP.PrintInParallel: i++; break;
                        case MsbOP.PrintInCenter: i++; break;
                        case MsbOP.SetMarginTop:
                            {
                                linesb.Append(BuildParams(data, i + 1, 2));
                                i += 3;
                                break;
                            }
                        case MsbOP.SetMarginLeft:
                            {
                                linesb.Append(BuildParams(data, i + 1, 2));
                                i += 3;
                                break;
                            }
                        case MsbOP.GetHardcodedValue:
                            {
                                linesb.Append(BuildParams(data, i + 1, 2));
                                i += 3;
                                break;
                            }
                        case MsbOP.OP_14:
                            {
                                linesb.Append(BuildParams(data, i + 1, 2));
                                i += 3;
                                break;
                            }
                        case MsbOP.EvaluateExpression:
                            {
                                int start = i;
                                while (i < data.Length)
                                {
                                    byte exprToken = data[i];
                                    if ((exprToken & 0x80) == 0x80)
                                    {
                                        i += ((exprToken & 0x60) >> 5) + 1;
                                    }
                                    else
                                    {
                                        i++;
                                        if (exprToken == 0x00) break;
                                    }
                                }
                                linesb.Append(BuildParams(data, start, i - start));
                                break;
                            }
                        case MsbOP.Dictionary:
                            {
                                linesb.Append(BuildParams(data, i + 1, 2));
                                i += 3;
                                break;
                            }
                        case MsbOP.PauseClearPage: i++; break;
                        case MsbOP.Auto:
                            {
                                linesb.Append(BuildParams(data, i + 1, 2));
                                i += 3;
                                break;
                            }
                        case MsbOP.AutoClearPage:
                            {
                                linesb.Append(BuildParams(data, i + 1, 2));
                                i += 3;
                                break;
                            }
                        case MsbOP.OP_1B:
                            {
                                linesb.Append(BuildParams(data, i + 1, 1));
                                i += 2;
                                break;
                            }
                        case MsbOP.RubyCenterPerChar: i++; break;
                        case MsbOP.AltLineBreak: i++; break;
                        case MsbOP.END: i++; break;
                        default: i++; break;
                    }
                    linesb.Append(OpCode);
                }
                else //character
                {
                    if (i + 4 < data.Length)
                    {
                        i++; i++; //Skip 0x8000
                        string Index = (data[i] << 8 | data[i + 1]).ToString();
                        if (CharSetMap.TryGetValue(Index, out var c))
                        {
                            if (c == "") c = $"<{Index}>";
                            linesb.Append(c);
                        }
                        else
                        {
                            linesb.Append($"<{Index}>");
                        }
                        i += 2;
                    }
                    else
                        throw new Exception("Character exceed data length");
                }
            }
            return linesb.ToString();
        }


        private string BuildParams(byte[] data, int offset, int len)
        {
            byte[] param = new byte[len];
            Array.Copy(data, offset, param, 0, len);
            string hex = BitConverter.ToString(param).Replace("-", "");
            return $"(0x{hex:X})";
        }
    }
}
