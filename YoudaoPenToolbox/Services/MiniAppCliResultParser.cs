using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace YoudaoPenToolbox.Services
{
    public sealed class MiniAppCliResult
    {
        public bool HasJson { get; set; }
        public int? ReturnCode { get; set; }
        public string AppId { get; set; }
        public string Reason { get; set; }
        public string RawOutput { get; set; }

        public bool IsSuccess => ReturnCode.HasValue && ReturnCode.Value == 0;

        public string Summary
        {
            get
            {
                if (ReturnCode.HasValue)
                {
                    if (ReturnCode.Value == 0)
                    {
                        return string.IsNullOrWhiteSpace(AppId)
                            ? "安装成功"
                            : $"安装成功 (AppId: {AppId})";
                    }

                    if (!string.IsNullOrWhiteSpace(Reason))
                    {
                        return $"安装失败 (ret={ReturnCode.Value}): {Reason}";
                    }

                    return $"安装失败 (ret={ReturnCode.Value})";
                }

                return null;
            }
        }
    }

    public static class MiniAppCliResultParser
    {
        public static MiniAppCliResult ParseInstall(string rawOutput)
        {
            return Parse(rawOutput);
        }

        public static MiniAppCliResult Parse(string rawOutput)
        {
            var result = new MiniAppCliResult
            {
                RawOutput = rawOutput ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(rawOutput))
            {
                return result;
            }

            var jsonText = ExtractJsonObject(rawOutput);
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return result;
            }

            try
            {
                var json = JObject.Parse(jsonText);
                result.HasJson = true;
                result.ReturnCode = ReadInt(json["ret"]);
                result.AppId = FirstNonEmpty(json["appid"]?.ToString(), json["appId"]?.ToString());
                result.Reason = FirstNonEmpty(json["reason"]?.ToString(), json["msg"]?.ToString(), json["message"]?.ToString());
            }
            catch
            {
            }

            return result;
        }

        private static string ExtractJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var start = text.LastIndexOf('{');
            while (start >= 0)
            {
                var depth = 0;
                for (var i = start; i < text.Length; i++)
                {
                    var ch = text[i];
                    if (ch == '{')
                    {
                        depth++;
                    }
                    else if (ch == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return text.Substring(start, i - start + 1);
                        }
                    }
                }

                start = text.LastIndexOf('{', start - 1);
            }

            return null;
        }

        public static bool LooksLikeCliSuccess(string rawOutput, MiniAppCliResult parsed)
        {
            if (parsed != null && parsed.HasJson)
            {
                return parsed.IsSuccess;
            }

            if (string.IsNullOrWhiteSpace(rawOutput))
            {
                return false;
            }

            if (rawOutput.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                || rawOutput.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return rawOutput.IndexOf("\"ret\":0", StringComparison.OrdinalIgnoreCase) >= 0
                   || rawOutput.IndexOf("ret=0", StringComparison.OrdinalIgnoreCase) >= 0
                   || rawOutput.IndexOf("install ok", StringComparison.OrdinalIgnoreCase) >= 0
                   || rawOutput.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int? ReadInt(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>();
            }

            return int.TryParse(token.ToString(), out var value) ? value : (int?)null;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }
    }
}
