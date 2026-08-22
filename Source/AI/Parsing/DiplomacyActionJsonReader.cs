using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Relations.AI
{
    /// <summary>
    /// Verse-free walk of a diplomacy actions payload. Catalog acceptance is
    /// decided here; parameter-level airdrop/ransom checks stay in the parser.
    /// </summary>
    public static class DiplomacyActionJsonReader
    {
        public sealed class Candidate
        {
            public string RawActionType { get; set; }
            public string NormalizedActionType { get; set; }
            public string Reason { get; set; }
            public Dictionary<string, object> Parameters { get; set; }
            public string RawObject { get; set; }
            public bool Accepted { get; set; }
        }

        public static List<Candidate> Read(string json)
        {
            var candidates = new List<Candidate>();
            string trimmedJson = (json ?? string.Empty).Trim();
            string actionsArray = trimmedJson.StartsWith("[", StringComparison.Ordinal)
                ? trimmedJson
                : JsonLooseObjectParser.ExtractJsonArray(trimmedJson, "actions");
            if (string.IsNullOrEmpty(actionsArray))
            {
                return candidates;
            }

            foreach (string actionObj in JsonLooseObjectParser.SplitJsonObjects(actionsArray))
            {
                string actionType = JsonLooseObjectParser.ExtractJsonString(actionObj, "action");
                if (string.IsNullOrEmpty(actionType))
                {
                    continue;
                }

                string reason = JsonLooseObjectParser.ExtractJsonString(actionObj, "reason");
                string parametersJson = JsonLooseObjectParser.ExtractJsonObject(actionObj, "parameters");
                Dictionary<string, object> parameters = string.IsNullOrEmpty(parametersJson)
                    ? new Dictionary<string, object>()
                    : JsonLooseObjectParser.ParseParameters(parametersJson);
                string normalized = DiplomacyActionCatalog.NormalizeActionName(actionType);
                if (string.Equals(normalized, "create_quest", StringComparison.Ordinal))
                {
                    string questDefName = JsonLooseObjectParser.ExtractJsonString(actionObj, "questDefName");
                    if (string.IsNullOrEmpty(questDefName))
                    {
                        questDefName = JsonLooseObjectParser.ExtractJsonString(actionObj, "defName");
                    }

                    if (!string.IsNullOrEmpty(questDefName) && !parameters.ContainsKey("questDefName"))
                    {
                        parameters["questDefName"] = questDefName;
                    }
                }

                candidates.Add(new Candidate
                {
                    RawActionType = actionType,
                    NormalizedActionType = normalized,
                    Reason = reason,
                    Parameters = parameters,
                    RawObject = actionObj,
                    Accepted = DiplomacyActionCatalog.IsValidAction(normalized)
                });
            }

            return candidates;
        }

        public static List<string> AcceptedTypes(string json)
        {
            var accepted = new List<string>();
            foreach (Candidate candidate in Read(json))
            {
                if (!candidate.Accepted || string.IsNullOrEmpty(candidate.NormalizedActionType))
                {
                    continue;
                }

                accepted.Add(candidate.NormalizedActionType);
            }

            return accepted;
        }

        public static List<string> DroppedUnknownTypes(string json)
        {
            var dropped = new List<string>();
            foreach (Candidate candidate in Read(json))
            {
                if (candidate.Accepted)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(candidate.NormalizedActionType) || candidate.NormalizedActionType == "none")
                {
                    continue;
                }

                dropped.Add(candidate.NormalizedActionType);
            }

            return dropped;
        }
    }
}
