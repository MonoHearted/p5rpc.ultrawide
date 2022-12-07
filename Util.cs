using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p5rpc.ultrawide
{
    public unsafe static class Util
    {
        public static long GetAddressFromGlobalRef(long instructionAdr, byte length)
        {
            // From SecreC, gets the offset for the pointer from instruction address
            int opd = *(int*)(instructionAdr + length - 4);
            return instructionAdr + opd + length;
        }
    }
}
