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
        private static readonly Regex JsonObjectRegex = new Regex(@"\{[^{}]*\}", RegexOptions.Compiled);

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
            var match = JsonObjectRegex.Match(text);
            return match.Success ? match.Value : null;
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
