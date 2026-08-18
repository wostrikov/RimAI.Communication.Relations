using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    using StatusFilterMode = Dialog_ApiDebugObservability.StatusFilterMode;
    using SourceFilterMode = Dialog_ApiDebugObservability.SourceFilterMode;
    internal sealed class ApiDebugObservabilitySlice3 : Dialog_ApiDebugObservabilityCollaborator
    {
        internal ApiDebugObservabilitySlice3(Dialog_ApiDebugObservability owner) : base(owner)
        {
        }

internal static string GetSourceLabel(AIRequestDebugSource source)
        {
            switch (source)
            {
                case AIRequestDebugSource.DiplomacyDialogue:
                    return "RimChat_ApiDebugSourceDiplomacyDialogue".Translate();
                case AIRequestDebugSource.RpgDialogue:
                    return "RimChat_ApiDebugSourceRpgDialogue".Translate();
                case AIRequestDebugSource.NpcPush:
                    return "RimChat_ApiDebugSourceNpcPush".Translate();
                case AIRequestDebugSource.PawnRpgPush:
                    return "RimChat_ApiDebugSourcePawnRpgPush".Translate();
                case AIRequestDebugSource.SocialNews:
                    return "RimChat_ApiDebugSourceSocialNews".Translate();
                case AIRequestDebugSource.StrategySuggestion:
                    return "RimChat_ApiDebugSourceStrategySuggestion".Translate();
                case AIRequestDebugSource.PersonaBootstrap:
                    return "RimChat_ApiDebugSourcePersonaBootstrap".Translate();
                case AIRequestDebugSource.MemorySummary:
                    return "RimChat_ApiDebugSourceMemorySummary".Translate();
                case AIRequestDebugSource.ArchiveCompression:
                    return "RimChat_ApiDebugSourceArchiveCompression".Translate();
                case AIRequestDebugSource.SendImage:
                    return "RimChat_ApiDebugSourceSendImage".Translate();
                case AIRequestDebugSource.ApiUsabilityTest:
                    return "RimChat_ApiDebugSourceApiUsabilityTest".Translate();
                case AIRequestDebugSource.AirdropSelection:
                    return "RimChat_ApiDebugSourceAirdropSelection".Translate();
                default:
                    return "RimChat_ApiDebugSourceOther".Translate();
            }
        }

internal static string GetStatusLabel(AIRequestDebugStatus status)
        {
            switch (status)
            {
                case AIRequestDebugStatus.Success:
                    return "RimChat_ApiDebugStatusSuccess".Translate();
                case AIRequestDebugStatus.Cancelled:
                    return "RimChat_ApiDebugStatusCancelled".Translate();
                default:
                    return "RimChat_ApiDebugStatusError".Translate();
            }
        }

internal static Color GetStatusColor(AIRequestDebugStatus status, Color fallback)
        {
            switch (status)
            {
                case AIRequestDebugStatus.Success:
                    return new Color(0.42f, 0.9f, 0.42f);
                case AIRequestDebugStatus.Error:
                    return new Color(1f, 0.47f, 0.47f);
                case AIRequestDebugStatus.Cancelled:
                    return new Color(0.95f, 0.83f, 0.42f);
                default:
                    return fallback;
            }
        }

internal static string Shorten(string value, int maxLength)
        {
            string text = value ?? string.Empty;
            if (text.Length <= maxLength || maxLength <= 3)
            {
                return text;
            }

            return text.Substring(0, maxLength - 3) + "...";
        }

internal void TryCopySelectedRecordJson()
        {
            AIRequestDebugRecord record = Owner.GetSelectedRecord(Owner.GetFilteredRecords());
            if (record == null)
            {
                Messages.Message("RimChat_ApiDebugCopyNoSelection".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            GUIUtility.systemCopyBuffer = Dialog_ApiDebugObservability.BuildRecordJson(record);
            Messages.Message("RimChat_ApiDebugCopySelectedSuccess".Translate(), MessageTypeDefOf.TaskCompletion, false);
        }

internal void TryCopyFilteredJson()
        {
            List<AIRequestDebugRecord> filtered = Owner.GetFilteredRecords();
            GUIUtility.systemCopyBuffer = Owner.BuildFilteredJson(filtered);
            Messages.Message("RimChat_ApiDebugCopyFilteredSuccess".Translate(filtered.Count.ToString()), MessageTypeDefOf.TaskCompletion, false);
        }

internal string BuildFilteredJson(List<AIRequestDebugRecord> records)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"generatedAtUtc\":\"").Append(Dialog_ApiDebugObservability.EscapeJson((snapshot?.GeneratedAtUtc ?? DateTime.UtcNow).ToString("o"))).Append("\",\n");
            sb.Append("  \"windowMinutes\":").Append(snapshot?.WindowMinutes ?? 60).Append(",\n");
            sb.Append("  \"count\":").Append(records?.Count ?? 0).Append(",\n");
            sb.Append("  \"records\":[\n");
            if (records != null)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    sb.Append(Dialog_ApiDebugObservability.IndentRecordJson(Dialog_ApiDebugObservability.BuildRecordJson(records[i]), "    "));
                    if (i < records.Count - 1)
                    {
                        sb.Append(',');
                    }

                    sb.Append('\n');
                }
            }

            sb.Append("  ]\n");
            sb.Append("}");
            return sb.ToString();
        }

internal static string BuildRecordJson(AIRequestDebugRecord record)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"requestId\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.RequestId ?? string.Empty)).Append("\",\n");
            sb.Append("  \"recordedAtUtc\":\"").Append(Dialog_ApiDebugObservability.EscapeJson((record?.RecordedAtUtc ?? DateTime.UtcNow).ToString("o"))).Append("\",\n");
            sb.Append("  \"source\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.Source.ToString() ?? AIRequestDebugSource.Other.ToString())).Append("\",\n");
            sb.Append("  \"channel\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.Channel.ToString() ?? DialogueUsageChannel.Unknown.ToString())).Append("\",\n");
            sb.Append("  \"model\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.Model ?? string.Empty)).Append("\",\n");
            sb.Append("  \"status\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.Status.ToString() ?? AIRequestDebugStatus.Error.ToString())).Append("\",\n");
            sb.Append("  \"durationMs\":").Append(record?.DurationMs ?? 0).Append(",\n");
            sb.Append("  \"httpStatusCode\":").Append(record?.HttpStatusCode ?? 0).Append(",\n");
            sb.Append("  \"promptTokens\":").Append(record?.PromptTokens ?? 0).Append(",\n");
            sb.Append("  \"completionTokens\":").Append(record?.CompletionTokens ?? 0).Append(",\n");
            sb.Append("  \"totalTokens\":").Append(record?.TotalTokens ?? 0).Append(",\n");
            sb.Append("  \"isEstimatedTokens\":").Append((record?.IsEstimatedTokens ?? false) ? "true" : "false").Append(",\n");
            sb.Append("  \"contractValidationStatus\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.ContractValidationStatus ?? string.Empty)).Append("\",\n");
            sb.Append("  \"contractRetryCount\":").Append(record?.ContractRetryCount ?? 0).Append(",\n");
            sb.Append("  \"contractFailureReason\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.ContractFailureReason ?? string.Empty)).Append("\",\n");
            sb.Append("  \"errorText\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.ErrorText ?? string.Empty)).Append("\",\n");
            sb.Append("  \"requestText\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.RequestText ?? string.Empty)).Append("\",\n");
            sb.Append("  \"responseText\":\"").Append(Dialog_ApiDebugObservability.EscapeJson(record?.ResponseText ?? string.Empty)).Append("\"\n");
            sb.Append("}");
            return sb.ToString();
        }

internal static string IndentRecordJson(string json, string indent)
        {
            string[] lines = (json ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.None);
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                sb.Append(indent).Append(lines[i]);
                if (i < lines.Length - 1)
                {
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

internal static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                switch (ch)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(ch))
                        {
                            sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }

                        break;
                }
            }

            return sb.ToString();
        }
    }
}
