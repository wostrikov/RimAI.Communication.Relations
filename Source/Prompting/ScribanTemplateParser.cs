using System;
using System.Collections;
using System.Reflection;
using Scriban;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    /// <summary>
    /// Dependencies: Scriban Template parser.
    /// Responsibility: parse prompt templates and surface structured parse diagnostics.
    /// </summary>
    internal static class ScribanTemplateParser
    {
        public static Template ParseOrThrow(string templateId, string channel, string source)
        {
            Template template = Template.Parse(source ?? string.Empty);
            if (!template.HasErrors)
            {
                return template;
            }

            string message = "Scriban parse failed.";
            int line = 0;
            int column = 0;
            TryExtractParseDiagnostic(template, ref message, ref line, ref column);
            throw new PromptRenderException(
                templateId,
                channel,
                new PromptRenderDiagnostic
                {
                    ErrorCode = PromptRenderErrorCode.ParseError,
                    Message = message,
                    Line = Math.Max(0, line),
                    Column = Math.Max(0, column)
                });
        }

        internal static (int line, int column) ExtractPosition(object value, string spanPropertyName)
        {
            if (value == null)
            {
                return (0, 0);
            }

            try
            {
                object span = value.GetType().GetProperty(spanPropertyName)?.GetValue(value, null);
                if (span == null)
                {
                    return (0, 0);
                }

                object start = span.GetType().GetProperty("Start")?.GetValue(span, null);
                if (start == null)
                {
                    return (0, 0);
                }

                int rawLine = ReadIntProperty(start, "Line");
                int rawColumn = ReadIntProperty(start, "Column");
                return (Math.Max(1, rawLine), Math.Max(1, rawColumn));
            }
            catch
            {
                return (0, 0);
            }
        }

        private static void TryExtractParseDiagnostic(Template template, ref string message, ref int line, ref int column)
        {
            if (template == null)
            {
                return;
            }

            try
            {
                PropertyInfo messagesProperty = template.GetType().GetProperty("Messages");
                object messages = messagesProperty?.GetValue(template, null);
                if (!(messages is IEnumerable enumerable))
                {
                    return;
                }

                foreach (object item in enumerable)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    message = item.ToString() ?? message;
                    (line, column) = ExtractPosition(item, "Span");
                    return;
                }
            }
            catch
            {
                // Keep default parse message when runtime API differs.
            }
        }

        private static int ReadIntProperty(object target, string propertyName)
        {
            if (target == null)
            {
                return 0;
            }

            object raw = target.GetType().GetProperty(propertyName)?.GetValue(target, null);
            if (raw == null)
            {
                return 0;
            }

            return raw is int value ? value : 0;
        }
    }
}
