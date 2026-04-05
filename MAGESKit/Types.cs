using System;
using System.Collections.Generic;
using System.Text;

namespace MAGESKit
{
    enum MsbOP : byte
    {
        LineBreak = 0x00,
        NameStart = 0x01,
        NameEnd = 0x02,
        PauseEndLine = 0x03,
        SetColor = 0x04,
        OP_05 = 0x05,
        OP_06 = 0x06,
        TextWait = 0x07,
        PauseEndPage = 0x08,
        RubyStart = 0x09,
        RubyTextStart = 0x0A,
        RubyTextEnd = 0x0B,
        SetFontSize = 0x0C,
        PrintInParallel = 0x0E,
        PrintInCenter = 0x0F,
        SetMarginTop = 0x11,
        SetMarginLeft = 0x12,
        GetHardcodedValue = 0x13,
        OP_14 = 0x14,
        EvaluateExpression = 0x15,
        Dictionary = 0x16,
        PauseClearPage = 0x18,
        Auto = 0x19,
        AutoClearPage = 0x1A,
        OP_1B = 0x1B,
        RubyCenterPerChar = 0x1E,
        AltLineBreak = 0x1F,
        END = 0xFF
    }

}
