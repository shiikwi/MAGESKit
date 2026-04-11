using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptTool
{
    enum OPTable : byte
    {
        MsgShow = 0x84,
        Jmp = 0x47,
        Call = 0x46,
        LayerShow = 0x82,
        OP_86 = 0x86,
    }
}
