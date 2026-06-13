using System.Text;

namespace YoudaoPenToolbox.Helpers
{
    public static class TextEncodingHelper
    {
        public static string DecodeText(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(data, 3, data.Length - 3);
            }

            var utf8 = Encoding.UTF8.GetString(data);
            if (!ContainsReplacementChar(utf8))
            {
                return utf8;
            }

            try
            {
                var gbk = Encoding.GetEncoding(936);
                return gbk.GetString(data);
            }
            catch
            {
                return utf8;
            }
        }

        private static bool ContainsReplacementChar(string text)
        {
            foreach (var ch in text)
            {
                if (ch == '\uFFFD')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
