using System;
using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Communication.Relations.AI
{
    public class LLMRpgApiResponse
    {
        public string DialogueContent { get; set; }
        public List<ApiAction> Actions { get; set; } = new List<ApiAction>();
        public string IncidentDefName { get; set; }
        public float IncidentPoints { get; set; }
        public string QuestTitle { get; set; }
        public string QuestDescription { get; set; }
        public string QuestRewardDescription { get; set; }
        public string QuestCallbackId { get; set; }
        public bool IsValid { get; set; }

        public class ApiAction
        {
            public string action;
            public string defName;
            public int amount;
            public string reason;
            public string title;
            public string description;
            public string rewardDescription;
            public string callbackId;
        }

        public static List<ApiAction> ParseActionsFromJson(string actionsJson)
        {
            return RpgActionParser.ParseActionsFromJson(actionsJson);
        }

        public static LLMRpgApiResponse Parse(string rawResponse)
        {
            var result = new LLMRpgApiResponse();
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return result;
            }

            try
            {
                string jsonContent = null;
                if (JsonMarkdownFence.TryExtractFencedBlock(rawResponse, out string fenced))
                {
                    jsonContent = fenced;
                }
                else
                {
                    jsonContent = RpgActionParser.ExtractFirstBalancedJsonObject(rawResponse);
                }

                if (!string.IsNullOrEmpty(jsonContent))
                {
                    RpgActionParser.ParseActions(jsonContent, result.Actions);
                    int jsonIndex = rawResponse.IndexOf(jsonContent, StringComparison.Ordinal);
                    if (jsonIndex > 0)
                    {
                        string content = rawResponse.Substring(0, jsonIndex).Trim();
                        content = JsonMarkdownFence.StripFenceMarkers(content);
                        result.DialogueContent = RpgActionParser.SanitizeDialogueContent(content);
                    }
                    else
                    {
                        string content = JsonMarkdownFence.StripFenceMarkers(rawResponse.Replace(jsonContent, ""));
                        result.DialogueContent = RpgActionParser.SanitizeDialogueContent(content);
                    }

                    if (string.IsNullOrWhiteSpace(result.DialogueContent))
                    {
                        result.DialogueContent = RpgActionParser.SanitizeDialogueContent(
                            RpgActionParser.ExtractLegacyDialogueContent(jsonContent));
                    }
                }
                else
                {
                    result.DialogueContent = RpgActionParser.SanitizeDialogueContent(rawResponse.Trim());
                }

                if (result.Actions.Count == 0)
                {
                    RpgActionParser.TryExtractInlineActions(rawResponse, result.Actions);
                }

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.DialogueContent = RpgActionParser.SanitizeDialogueContent(rawResponse.Trim());
                Log.Error($"[RimAI.Relations] RPG JSON parse error: {ex}");
            }

            return result;
        }
    }
}
