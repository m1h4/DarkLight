using System;
using System.Runtime.InteropServices;

namespace DarkLight
{
    public class ConsoleColor
    {
        const int STD_INPUT_HANDLE = -10;
        const int STD_OUTPUT_HANDLE = -11;
        const int STD_ERROR_HANDLE = -12;

        [DllImportAttribute("Kernel32.dll")]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImportAttribute("Kernel32.dll")]
        private static extern bool SetConsoleTextAttribute(IntPtr hConsoleOutput,int wAttributes);

        [Flags]
        public enum ForeGroundColor
        {
            Black = 0x0000,
            Blue = 0x0001,
            Green = 0x0002, 
            Cyan = 0x0003,
            Red = 0x0004,
            Magenta = 0x0005,
            Yellow = 0x0006,
            Grey = 0x0007,
            White = 0x0008
        }

        private ConsoleColor()
        {
        }

        public static bool SetForeGroundColor()
        {
            return SetForeGroundColor(ForeGroundColor.Grey);
        }

        public static bool SetForeGroundColor(ForeGroundColor foreGroundColor)
        {
            return SetForeGroundColor(foreGroundColor, false);
        }

        public static bool SetForeGroundColor(ForeGroundColor foreGroundColor,bool brightColors)
        {
            IntPtr nConsole = GetStdHandle(STD_OUTPUT_HANDLE);
            int colorMap;
            
            if (brightColors)
                colorMap = (int) foreGroundColor | 
                    (int) ForeGroundColor.White;
            else
                colorMap = (int) foreGroundColor;

            return SetConsoleTextAttribute(nConsole, colorMap);
        }
    }
}