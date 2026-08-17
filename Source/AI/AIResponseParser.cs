using System;
using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Diagnostics;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Thin diplomacy parse facade. Envelope vs action vs strategy live in dedicated parsers.
    /// </summary>
    public class AIResponseParser
    {
        public static ParsedResponse ParseResponse(string response, Faction faction)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return new ParsedResponse
                {
                    Success = false,
                    ErrorMessage = "Empty response",
                    DialogueText = "I have nothing to say at the moment.",
                    Actions = new List<AIAction>(),
                    StrategySuggestions = new List<StrategySuggestion>()
                };
            }

            try
            {
                string narrativeFallback = DiplomacyNarrativeExtractor.ExtractNarrativeText(response);
                JsonResponse jsonResponse = ParseJsonResponse(response);
                if (jsonResponse != null)
                {
                    ParsedResponse parsed = ProcessJsonResponse(jsonResponse, narrativeFallback);
                    if (string.IsNullOrWhiteSpace(parsed.DialogueText) &&
                        parsed.Actions.Count == 0 &&
                        parsed.StrategySuggestions.Count == 0)
                    {
                        parsed.DialogueText = ImmersionOutputGuard.BuildLocalFallbackDialogue(DialogueUsageChannel.Diplomacy);
                    }

                    return parsed;
                }

                return new ParsedResponse
                {
                    Success = true,
                    DialogueText = DiplomacyNarrativeExtractor.NormalizeDialogueText(response),
                    Actions = new List<AIAction>(),
                    StrategySuggestions = new List<StrategySuggestion>()
                };
            }
            catch (Exception ex)
            {
                DebugLogger.WarningGated($"Failed to parse AI response: {ex.Message}");
                return new ParsedResponse
                {
                    Success = true,
                    DialogueText = DiplomacyNarrativeExtractor.NormalizeDialogueText(response),
                    Actions = new List<AIAction>(),
                    StrategySuggestions = new List<StrategySuggestion>()
                };
            }
        }

        public static ParsedResponse ParseResponse(DialogueResponseEnvelope envelope, Faction faction)
        {
            if (envelope == null)
            {
                return ParseResponse(string.Empty, faction);
            }

            try
            {
                string narrativeFallback = DiplomacyNarrativeExtractor.NormalizeDialogueText(envelope.VisibleDialogue);
                var result = new ParsedResponse
                {
                    Success = true,
                    DialogueText = narrativeFallback,
                    Actions = new List<AIAction>(),
                    StrategySuggestions = new List<StrategySuggestion>()
                };

                List<AIAction> parsedActions = DiplomacyActionParser.ParseActionsFromJson(
                    envelope.ActionsJson,
                    envelope.VisibleDialogue);
                if (parsedActions.Count > 0)
                {
                    result.Actions.AddRange(parsedActions);
                }

                if (string.IsNullOrWhiteSpace(result.DialogueText) && result.Actions.Count == 0)
                {
                    result.DialogueText = ImmersionOutputGuard.BuildLocalFallbackDialogue(DialogueUsageChannel.Diplomacy);
                }

                return result;
            }
            catch (Exception ex)
            {
                DebugLogger.WarningGated($"Failed to parse dialogue envelope: {ex.Message}");
                return ParseResponse(envelope.ToLegacyText(), faction);
            }
        }

        static JsonResponse ParseJsonResponse(string response)
        {
            List<JsonPayloadSegment> payloadSegments =
                DiplomacyNarrativeExtractor.ExtractJsonPayloadSegments(response, includeGenericJson: false);
            if (payloadSegments.Count == 0)
            {
                return null;
            }

            JsonPayloadSegment actionSegment = payloadSegments.Find(segment => segment.HasActions);
            JsonPayloadSegment strategySegment = payloadSegments.Find(segment => segment.HasStrategySuggestions);

            var result = new JsonResponse();
            result.RawJson = actionSegment?.Json ?? strategySegment?.Json ?? payloadSegments[0].Json;

            string strategySuggestionsSource = strategySegment?.Json ?? result.RawJson;
            string strategySuggestionsJson = JsonLooseObjectParser.ExtractJsonArray(
                strategySuggestionsSource,
                "strategy_suggestions");
            if (!string.IsNullOrEmpty(strategySuggestionsJson))
            {
                result.StrategySuggestions =
                    DiplomacyStrategySuggestionParser.ParseStrategySuggestions(strategySuggestionsJson);
            }
            else
            {
                result.StrategySuggestions = new List<StrategySuggestion>();
            }

            return result;
        }

        static ParsedResponse ProcessJsonResponse(JsonResponse json, string narrativeFallback)
        {
            var result = new ParsedResponse
            {
                Success = true,
                DialogueText = DiplomacyNarrativeExtractor.NormalizeDialogueText(narrativeFallback),
                Actions = new List<AIAction>(),
                StrategySuggestions = json.StrategySuggestions ?? new List<StrategySuggestion>()
            };

            List<AIAction> parsedActions = DiplomacyActionParser.ParseActionsFromJson(json?.RawJson, narrativeFallback);
            if (parsedActions.Count == 0)
            {
                return result;
            }

            result.Actions.AddRange(parsedActions);
            return result;
        }
    }
}
