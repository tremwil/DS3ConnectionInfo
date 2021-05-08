using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace DS3ConnectionInfo.WinAPI
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int x1; // x position of upper-left corner
        public int y1; // y position of upper-left corner
        public int x2; // x position of lower-right corner
        public int y2; // y position of lower-right corner
    }
}
