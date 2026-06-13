using System;
using System.Text;

namespace YoudaoPenToolbox.Helpers
{
    public static class HexDumpFormatter
    {
        private const int BytesPerLine = 16;
        private static readonly char[] HexChars = "0123456789ABCDEF".ToCharArray();

        public static int GetLineCount(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0;
            }

            return (data.Length + BytesPerLine - 1) / BytesPerLine;
        }

        public static HexDumpLine FormatLine(byte[] data, int lineIndex)
        {
            if (data == null || data.Length == 0)
            {
                return new HexDumpLine();
            }

            var offset = lineIndex * BytesPerLine;
            if (offset >= data.Length)
            {
                return new HexDumpLine();
            }

            var hexChars = new char[10 + (BytesPerLine * 3) + 1];
            WriteOffsetHex(offset, hexChars, 0);
            hexChars[8] = ' ';
            hexChars[9] = ' ';

            var asciiChars = new char[BytesPerLine];
            var hexIndex = 10;
            for (var column = 0; column < BytesPerLine; column++)
            {
                if (offset + column < data.Length)
                {
                    var value = data[offset + column];
                    hexChars[hexIndex++] = HexChars[value >> 4];
                    hexChars[hexIndex++] = HexChars[value & 0x0F];
                    hexChars[hexIndex++] = ' ';
                    asciiChars[column] = value >= 32 && value < 127 ? (char)value : '.';
                }
                else
                {
                    hexChars[hexIndex++] = ' ';
                    hexChars[hexIndex++] = ' ';
                    hexChars[hexIndex++] = ' ';
                    asciiChars[column] = ' ';
                }

                if (column == 7)
                {
                    hexChars[hexIndex++] = ' ';
                }
            }

            return new HexDumpLine
            {
                HexText = new string(hexChars, 0, hexIndex).TrimEnd(),
                AsciiText = new string(asciiChars).TrimEnd()
            };
        }

        private static void WriteOffsetHex(int offset, char[] buffer, int start)
        {
            for (var i = 7; i >= 0; i--)
            {
                buffer[start + i] = HexChars[offset & 0x0F];
                offset >>= 4;
            }
        }
    }
}
