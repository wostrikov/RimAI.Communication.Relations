using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RimChat.AI
{
    public enum OpenAIErrorCategory
    {
        None, AuthenticationError, PermissionError, InvalidRequest,
        UnsupportedParameter, ModelNotFound, RateLimit, ProviderServerError,
        Timeout, NetworkError, Cancelled, MalformedResponse, Unknown
    }

    public sealed class OpenAIError
    {
        public long HttpStatus { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Param { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public OpenAIErrorCategory Category { get; set; }

        public override string ToString()
        {
            var parts = new List<string> { Category.ToString().ToUpperInvariant() };
            if (HttpStatus > 0) parts.Add("HTTP " + HttpStatus);
            if (!string.IsNullOrWhiteSpace(Type)) parts.Add(Type);
            if (!string.IsNullOrWhiteSpace(Code)) parts.Add(Code);
            if (!string.IsNullOrWhiteSpace(Param)) parts.Add("parameter=" + Param);
            if (!string.IsNullOrWhiteSpace(Message)) parts.Add(Message);
            return string.Join(" | ", parts);
        }
    }

    /// <summary>Authoritative OpenAI Responses API wire contract. No Unity dependency.</summary>
    public static class OpenAIProviderAdapter
    {
        public const string CredentialVariable = "OPENAI_RIMCHAT";
        public const string ResponsesEndpoint = "https://api.openai.com/v1/responses";
        public const string ModelsEndpoint = "https://api.openai.com/v1/models";

        private static readonly Regex ErrorObject = new Regex("\\\"error\\\"\\s*:\\s*\\{(?<body>.*?)\\}", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        public static string ResolveCredential()
        {
            return (Environment.GetEnvironmentVariable(CredentialVariable) ?? string.Empty).Trim();
        }

        public static bool CredentialPresent => !string.IsNullOrWhiteSpace(ResolveCredential());
        public static string CredentialDisplay => CredentialPresent ? CredentialVariable + " ✓" : CredentialVariable + " ✗";

        public static string BuildResponsesRequest(string model, IList<ChatMessageData> messages, int maxOutputTokens)
        {
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("OpenAI model is required.", nameof(model));
            if (messages == null || messages.Count == 0) throw new ArgumentException("OpenAI input messages are required.", nameof(messages));
            if (maxOutputTokens < 1) throw new ArgumentOutOfRangeException(nameof(maxOutputTokens));

            var json = new StringBuilder();
            json.Append("{\"model\":\"").Append(Escape(model)).Append("\",\"input\":[");
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) json.Append(',');
                string role = NormalizeRole(messages[i]?.role);
                json.Append("{\"role\":\"").Append(role).Append("\",\"content\":[{\"type\":\"input_text\",\"text\":\"")
                    .Append(Escape(messages[i]?.content ?? string.Empty)).Append("\"}]}");
            }
            json.Append("],\"max_output_tokens\":").Append(maxOutputTokens).Append(",\"store\":false}");
            return json.ToString();
        }

        public static OpenAIError ParseError(long httpStatus, string json)
        {
            string body = ErrorObject.Match(json ?? string.Empty).Groups["body"].Value;
            string type = Field(body, "type");
            string param = Field(body, "param");
            string code = Field(body, "code");
            string message = Field(body, "message");
            return new OpenAIError
            {
                HttpStatus = httpStatus, Type = type, Param = param, Code = code, Message = message,
                Category = Classify(httpStatus, type, code)
            };
        }

        public static List<string> ParseModels(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Models response is empty.");
            var result = new List<string>();
            MatchCollection matches = Regex.Matches(json, "\\\"id\\\"\\s*:\\s*\\\"(?<id>(?:[^\\\"\\\\]|\\\\.)+)\\\"", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                string id = Unescape(match.Groups["id"].Value);
                if (!string.IsNullOrWhiteSpace(id)) result.Add(id);
            }
            if (result.Count == 0) throw new FormatException("Models response contains no model ids.");
            return result;
        }

        public static bool ContainsModel(IList<string> models, string selectedModel)
        {
            if (models == null || string.IsNullOrWhiteSpace(selectedModel)) return false;
            for (int i = 0; i < models.Count; i++)
                if (string.Equals(models[i], selectedModel, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string ParseOutputText(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            var values = new List<string>();
            MatchCollection objects = Regex.Matches(json, "\\{(?<object>(?:[^{}\\\"]|\\\"(?:[^\\\"\\\\]|\\\\.)*\\\")*)\\}", RegexOptions.Singleline);
            foreach (Match match in objects)
            {
                string item = match.Groups["object"].Value;
                if (!Regex.IsMatch(item, "\\\"type\\\"\\s*:\\s*\\\"output_text\\\"", RegexOptions.IgnoreCase)) continue;
                Match text = Regex.Match(item, "\\\"text\\\"\\s*:\\s*\\\"(?<v>(?:[^\\\"\\\\]|\\\\.)*)\\\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (text.Success) values.Add(Unescape(text.Groups["v"].Value));
            }
            if (values.Count == 0)
            {
                Match convenience = Regex.Match(json, "\\\"output_text\\\"\\s*:\\s*\\\"(?<v>(?:[^\\\"\\\\]|\\\\.)*)\\\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (convenience.Success) values.Add(Unescape(convenience.Groups["v"].Value));
            }
            return string.Join(" ", values).Trim();
        }

        public static OpenAIErrorCategory Classify(long status, string type, string code)
        {
            string marker = ((type ?? string.Empty) + " " + (code ?? string.Empty)).ToLowerInvariant();
            if (marker.Contains("unsupported_parameter")) return OpenAIErrorCategory.UnsupportedParameter;
            if (marker.Contains("model_not_found")) return OpenAIErrorCategory.ModelNotFound;
            if (status == 401 || marker.Contains("authentication")) return OpenAIErrorCategory.AuthenticationError;
            if (status == 403 || marker.Contains("permission")) return OpenAIErrorCategory.PermissionError;
            if (status == 429 || marker.Contains("rate_limit")) return OpenAIErrorCategory.RateLimit;
            if (status >= 500) return OpenAIErrorCategory.ProviderServerError;
            if (status == 400 || marker.Contains("invalid_request")) return OpenAIErrorCategory.InvalidRequest;
            return OpenAIErrorCategory.Unknown;
        }

        private static string NormalizeRole(string role)
        {
            string value = (role ?? string.Empty).Trim().ToLowerInvariant();
            return value == "system" || value == "developer" ? "developer" : value == "assistant" ? "assistant" : "user";
        }

        private static string Field(string json, string name)
        {
            Match match = Regex.Match(json ?? string.Empty, "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(?:\\\"(?<s>(?:[^\\\"\\\\]|\\\\.)*)\\\"|null)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? Unescape(match.Groups["s"].Value) : string.Empty;
        }

        private static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        private static string Unescape(string value) => (value ?? string.Empty).Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}
