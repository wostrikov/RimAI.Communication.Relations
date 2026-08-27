using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Memory;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>/// Dependencies: prompt policy config, RPG API response parsing, and RPG memory catalog.
    /// Responsibility: normalize RPG action names and apply exit/intent/memory fallback policies.
    ///</summary>
        internal sealed class RPGPawnDialogueActionPolicies : Dialog_RPGPawnDialogueCollaborator
    {
        internal RPGPawnDialogueActionPolicies(Dialog_RPGPawnDialogue owner) : base(owner)
        {
        }


        internal const int MemoryRound5Threshold = 5;
        internal const float MemoryRoundChance = 0.8f;
        internal bool memoryRound5Evaluated;
        internal int consecutiveNoActionAssistantTurns;
        internal int lastIntentMappedAssistantRound = -999;
        internal bool autoMemoryFallbackConsumed;
        internal bool suppressAutoMemoryFallbackForTurn;

        internal static readonly string[] CooldownExitFallbackHints =
        {
            "leave me alone", "do not contact me", "don't contact me", "stop talking",
            "get lost", "go away", "don't bother me",
            "别再打扰", "别联系我", "不要再找我",
            "离我远点", "滚开", "请离开"
        };

        internal static readonly string[] NormalExitFallbackHints =
        {
            "goodbye", "see you", "talk later", "let's pause", "need to go", "that's all for now",
            "再见", "不聊了", "今天就到这",
            "先聊到这", "就聊到这", "改天再聊",
            "我要去忙了", "晚点再说", "回头再聊"
        };

        internal static readonly string[] StrongRejectHints =
        {
            "never", "won't", "refuse", "stop", "don't ask again", "leave me alone",
            "休想", "不可能", "少来", "别再问", "禁止"
        };

        internal static readonly string[] CollaborationHints =
        {
            "i will do it", "i can help", "let me handle it", "leave it to me", "i'll take care of it",
            "我会去做", "我可以帮你", "我来处理", "交给我", "我会负责"
        };

        internal enum IntentActionCategory
        {
            NeutralInfo = 0,
            CollaborationCommitment = 1,
            SoftEnding = 2,
            StrongReject = 3
        }

        internal bool ExecuteTryGainMemory(LLMRpgApiResponse.ApiAction action)
        {
            if (target?.needs?.mood?.thoughts?.memories == null)
            {
                Owner.NotifyActionFailure("TryGainMemory", "RimChat_RPGActionFail_InvalidTarget".Translate());
                return false;
            }

            string requested = action?.defName ?? string.Empty;
            ThoughtDef def = Owner.ResolveTryGainMemoryThoughtDef(requested, out string resolvedFrom);
            if (def == null)
            {
                return Owner.NotifyInvalidTryGainMemory(requested);
            }

            Owner.LogTryGainMemoryResolution(requested, resolvedFrom, def);
            Owner.ApplyTryGainMemory(def);
            return true;
        }

        internal ThoughtDef ResolveTryGainMemoryThoughtDef(string requestedDefName, out string resolvedFrom)
        {
            return RpgMemoryCatalog.ResolveRequestedThoughtDef(requestedDefName, out resolvedFrom);
        }

        internal string BuildTryGainMemoryExamplesText()
        {
            return RpgMemoryCatalog.BuildPromptExamplesTextWithFallback("KindWordsMood, InsultedMood");
        }

        internal bool NotifyInvalidTryGainMemory(string requestedDefName)
        {
            string examples = Owner.BuildTryGainMemoryExamplesText();
            string reason = "RimChat_RPGActionFail_InvalidDefName".Translate(string.IsNullOrEmpty(requestedDefName) ? "null" : requestedDefName);
            if (!string.IsNullOrWhiteSpace(examples))
            {
                reason += " " + "RimChat_RPGActionFail_DefNameExamples".Translate(examples);
            }

            Owner.NotifyActionFailure("TryGainMemory", reason);
            return false;
        }

        internal void LogTryGainMemoryResolution(string requestedDefName, string resolvedFrom, ThoughtDef def)
        {
            if (string.IsNullOrWhiteSpace(resolvedFrom))
            {
                return;
            }

            Owner.LogRpgActionDebug($"TryGainMemory resolved alias '{requestedDefName}' -> '{def.defName}' via {resolvedFrom}");
        }

        internal void ApplyTryGainMemory(ThoughtDef def)
        {
            target.needs.mood.thoughts.memories.TryGainMemory(def, initiator);
            string displayName = RpgMemoryCatalog.BuildDisplayName(def);
            float moodEffect = Owner.GetMoodEffect(def);
            Color moodColor = moodEffect >= 0 ? MoodPositiveColor : MoodNegativeColor;
            // The name is drawn as the mood-coloured half right after this text, so the
            // string is a prefix, not a format: calling Translate() without an argument
            // used to leave a literal {0} on screen with the name jammed against it.
            Owner.AddActionFeedback(
                "RimChat_RPGSystem_MemoryApplied".Translate() + " ",
                displayName, ActionInfoColor, moodColor, 3.8f);
        }

        internal float GetMoodEffect(ThoughtDef def)
        {
            if (def?.stages == null || def.stages.Count == 0)
            {
                return 0f;
            }

            var stage = def.stages[0];
            return stage?.baseMoodEffect ?? 0f;
        }

        internal static string NormalizeRpgActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            string normalized = actionName.Trim().Replace("-", "_").ToLowerInvariant();
            return normalized switch
            {
                "romanceattempt" or "romance_attempt" or "romance" or "fall_in_love" or "start_romance" or "恋爱" => "RomanceAttempt",
                "marriageproposal" or "marriage_proposal" or "propose_marriage" or "marry" or "结婚" => "MarriageProposal",
                "breakup" or "break_up" or "split_up" or "分手" => "Breakup",
                "divorce" or "离婚" => "Divorce",
                "date" or "dating" or "约会" => "Date",
                "trygainmemory" or "try_gain_memory" => "TryGainMemory",
                "tryaffectsocialgoodwill" or "try_affect_social_goodwill" => "TryAffectSocialGoodwill",
                "reduceresistance" or "reduce_resistance" => "ReduceResistance",
                "reducewill" or "reduce_will" => "ReduceWill",
                "recruit" or "action4" or "action_4" or "action 4" or "第4个动作" or "第四个动作" => "Recruit",
                "trytakeorderedjob" or "try_take_ordered_job" => "TryTakeOrderedJob",
                "triggerincident" or "trigger_incident" => "TriggerIncident",
                "grantinspiration" or "grant_inspiration" => "GrantInspiration",
                "exitdialoguecooldown" or "exit_dialogue_cooldown" or "exit_dialogue_with_cooldown" => "ExitDialogueCooldown",
                "exitdialogue" or "exit_dialogue" or "end_dialogue" => "ExitDialogue",
                "convertideology" or "convert_ideology" or "改变意识形态" or "皈依" => "ConvertIdeology",
                "adjustcertainty" or "adjust_certainty" or "调整信仰度" or "动摇信仰" => "AdjustCertainty",
                _ => actionName.Trim()
            };
        }

        internal void EnsureRpgActionFallbacks(LLMRpgApiResponse apiResponse)
        {
            if (apiResponse?.Actions == null)
            {
                return;
            }

            bool allowAutoMemoryFallback = !Owner.ShouldSuppressAutoMemoryFallback();
            Owner.EnsureRpgExitActionFallback(apiResponse);
            Owner.EnsureRpgIntentDrivenActionMapping(apiResponse, allowAutoMemoryFallback);
            if (!allowAutoMemoryFallback)
            {
                return;
            }

            Owner.EnsureRpgMemoryActionFallback(apiResponse);
            Owner.EnsureRpgMinimumActionCoverage(apiResponse);
        }

        internal void EnsureRpgExitActionFallback(LLMRpgApiResponse apiResponse)
        {
            if (apiResponse?.Actions == null || Owner.HasExitAction(apiResponse))
            {
                return;
            }

            string text = apiResponse.DialogueContent ?? string.Empty;
            if (Owner.ShouldUseCooldownExitFallback(text))
            {
                apiResponse.Actions.Add(new LLMRpgApiResponse.ApiAction { action = "ExitDialogueCooldown" });
                return;
            }

            if (Owner.ShouldUseNormalExitFallback(text))
            {
                apiResponse.Actions.Add(new LLMRpgApiResponse.ApiAction { action = "ExitDialogue" });
            }
        }

        internal void EnsureRpgIntentDrivenActionMapping(LLMRpgApiResponse apiResponse, bool allowAutoMemoryFallback)
        {
            PromptPolicyConfig policy = Dialog_RPGPawnDialogue.GetPromptPolicyForActionMapping();
            if (policy?.EnableIntentDrivenActionMapping != true || apiResponse?.Actions == null)
            {
                return;
            }

            int rounds = Owner.GetNpcDialogueRoundCount();
            int cooldown = Math.Max(0, policy.IntentActionCooldownTurns);
            if (cooldown > 0 && rounds - lastIntentMappedAssistantRound < cooldown)
            {
                return;
            }

            if (!Owner.TryMapIntentDrivenAction(apiResponse, rounds, policy, allowAutoMemoryFallback))
            {
                return;
            }

            lastIntentMappedAssistantRound = rounds;
        }

        internal static PromptPolicyConfig GetPromptPolicyForActionMapping()
        {
            SystemPromptConfig config = Ustas.RimAI.Communication.Relations.Persistence.PromptPersistenceService.Instance?.LoadConfig();
            PromptPolicyConfig policy = config?.PromptPolicy;
            return policy?.Clone() ?? PromptPolicyConfig.CreateDefault();
        }

        internal bool TryMapIntentDrivenAction(
            LLMRpgApiResponse apiResponse,
            int rounds,
            PromptPolicyConfig policy,
            bool allowAutoMemoryFallback)
        {
            IntentActionCategory category = Owner.ClassifyIntentActionCategory(apiResponse.DialogueContent);
            switch (category)
            {
                case IntentActionCategory.StrongReject:
                    return Owner.TryMapStrongRejectToAction(apiResponse);
                case IntentActionCategory.SoftEnding:
                    return Owner.TryMapSoftEndingToAction(apiResponse);
                case IntentActionCategory.CollaborationCommitment:
                    if (!allowAutoMemoryFallback)
                    {
                        return false;
                    }

                    return Owner.TryMapCollaborationToAction(apiResponse, rounds, policy);
                default:
                    return false;
            }
        }

        internal IntentActionCategory ClassifyIntentActionCategory(string dialogueText)
        {
            string text = dialogueText ?? string.Empty;
            if (Owner.ShouldUseCooldownExitFallback(text) || Owner.ContainsAnyPhrase(text, StrongRejectHints))
            {
                return IntentActionCategory.StrongReject;
            }

            if (Owner.ShouldUseNormalExitFallback(text))
            {
                return IntentActionCategory.SoftEnding;
            }

            return Owner.ContainsAnyPhrase(text, CollaborationHints)
                ? IntentActionCategory.CollaborationCommitment
                : IntentActionCategory.NeutralInfo;
        }

        internal bool TryMapStrongRejectToAction(LLMRpgApiResponse apiResponse)
        {
            if (Owner.HasExitAction(apiResponse))
            {
                return false;
            }

            apiResponse.Actions.Add(new LLMRpgApiResponse.ApiAction
            {
                action = "ExitDialogueCooldown",
                reason = "intent_map_strong_reject"
            });
            return true;
        }

        internal bool TryMapSoftEndingToAction(LLMRpgApiResponse apiResponse)
        {
            if (Owner.HasExitAction(apiResponse))
            {
                return false;
            }

            apiResponse.Actions.Add(new LLMRpgApiResponse.ApiAction
            {
                action = "ExitDialogue",
                reason = "intent_map_soft_end"
            });
            return true;
        }

        internal bool TryMapCollaborationToAction(LLMRpgApiResponse apiResponse, int rounds, PromptPolicyConfig policy)
        {
            if (autoMemoryFallbackConsumed || Owner.HasAnyRpgEffects(apiResponse) || Owner.HasRpgAction(apiResponse, "TryGainMemory"))
            {
                return false;
            }

            int minRounds = Math.Max(0, policy?.IntentMinAssistantRoundsForMemory ?? 0);
            if (rounds < minRounds)
            {
                return false;
            }

            string memoryDefName = Owner.ResolveAutoMemoryDefName(rounds);
            if (string.IsNullOrWhiteSpace(memoryDefName))
            {
                return false;
            }

            apiResponse.Actions.Add(new LLMRpgApiResponse.ApiAction
            {
                action = "TryGainMemory",
                defName = memoryDefName,
                reason = "intent_map_collaboration"
            });
            autoMemoryFallbackConsumed = true;
            return true;
        }


        internal void EnsureRpgMemoryActionFallback(LLMRpgApiResponse apiResponse)
        {
            int rounds = Owner.GetNpcDialogueRoundCount();
            if (rounds < MemoryRound5Threshold || Owner.HasRpgAction(apiResponse, "TryGainMemory"))
            {
                memoryRound5Evaluated = rounds >= MemoryRound5Threshold;
                return;
            }

            if (memoryRound5Evaluated)
            {
                return;
            }

            memoryRound5Evaluated = true;
            Owner.TryAddRoundMemoryFallback(apiResponse, rounds, MemoryRoundChance);
        }

        internal void TryAddRoundMemoryFallback(LLMRpgApiResponse apiResponse, int rounds, float chance)
        {
            if (autoMemoryFallbackConsumed)
            {
                return;
            }

            float roll = Rand.Value;
            if (roll > chance)
            {
                Owner.AddSystemFeedback("RimChat_RPGSystem_MemoryRollFailed".Translate(rounds, (chance * 100f).ToString("F0"), (roll * 100f).ToString("F0")));
                return;
            }

            ThoughtDef def = Owner.ResolveAutoMemoryThoughtDef(rounds);
            if (def == null)
            {
                Owner.AddSystemFeedback("RimChat_RPGSystem_MemoryNoDef".Translate());
                return;
            }

            apiResponse.Actions.Add(new LLMRpgApiResponse.ApiAction
            {
                action = "TryGainMemory",
                defName = def.defName,
                reason = "auto_round_memory"
            });
            autoMemoryFallbackConsumed = true;
            Owner.AddSystemFeedback("RimChat_RPGSystem_MemoryRollSuccess".Translate(rounds, (chance * 100f).ToString("F0"), (roll * 100f).ToString("F0"), RpgMemoryCatalog.BuildDisplayName(def)), 4.8f);
        }

        internal string ResolveAutoMemoryDefName(int rounds)
        {
            ThoughtDef def = Owner.ResolveAutoMemoryThoughtDef(rounds);
            return def?.defName ?? string.Empty;
        }

        internal ThoughtDef ResolveAutoMemoryThoughtDef(int rounds)
        {
            string defName = RpgMemoryCatalog.ResolveAutoDefName(rounds);
            return DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
        }

        internal int GetNpcDialogueRoundCount()
        {
            return chatHistory?.Count(message => string.Equals(message.role, "assistant", StringComparison.Ordinal)) ?? 0;
        }

        internal bool HasRpgAction(LLMRpgApiResponse apiResponse, string actionName)
        {
            if (apiResponse?.Actions == null)
            {
                return false;
            }

            return apiResponse.Actions.Any(action => Dialog_RPGPawnDialogue.NormalizeRpgActionName(action?.action) == actionName);
        }

        internal bool HasExitAction(LLMRpgApiResponse apiResponse)
        {
            return Owner.HasRpgAction(apiResponse, "ExitDialogue") ||
                   Owner.HasRpgAction(apiResponse, "ExitDialogueCooldown");
        }

        internal bool ShouldUseCooldownExitFallback(string text)
        {
            return Owner.ContainsAnyPhrase(text, CooldownExitFallbackHints);
        }

        internal bool ShouldUseNormalExitFallback(string text)
        {
            return Owner.ContainsAnyPhrase(text, NormalExitFallbackHints);
        }

        internal void EnsureRpgMinimumActionCoverage(LLMRpgApiResponse apiResponse)
        {
            if (apiResponse == null)
            {
                return;
            }

            if (Owner.HasAnyRpgEffects(apiResponse))
            {
                consecutiveNoActionAssistantTurns = 0;
                return;
            }

            consecutiveNoActionAssistantTurns++;
            int noActionThreshold = Math.Max(1, Dialog_RPGPawnDialogue.GetPromptPolicyForActionMapping()?.IntentNoActionStreakThreshold ?? 2);
            if (consecutiveNoActionAssistantTurns < noActionThreshold)
            {
                return;
            }

            if (!Owner.TryAddNoActionStreakMemoryFallback(apiResponse))
            {
                return;
            }

            consecutiveNoActionAssistantTurns = 0;
        }

        internal bool HasAnyRpgEffects(LLMRpgApiResponse apiResponse)
        {
            return apiResponse?.Actions?.Count > 0;
        }

        internal bool TryAddNoActionStreakMemoryFallback(LLMRpgApiResponse apiResponse)
        {
            if (autoMemoryFallbackConsumed || apiResponse?.Actions == null || Owner.HasRpgAction(apiResponse, "TryGainMemory"))
            {
                return false;
            }

            int rounds = Owner.GetNpcDialogueRoundCount();
            ThoughtDef def = Owner.ResolveAutoMemoryThoughtDef(rounds);
            if (def == null)
            {
                return false;
            }

            apiResponse.Actions.Add(new LLMRpgApiResponse.ApiAction
            {
                action = "TryGainMemory",
                defName = def.defName,
                reason = "auto_no_action_streak"
            });
            autoMemoryFallbackConsumed = true;
            Owner.AddSystemFeedback("RimChat_RPGSystem_MemoryRollSuccess".Translate(rounds, "100", "100", RpgMemoryCatalog.BuildDisplayName(def)), 4.8f);
            return true;
        }

        internal bool ShouldSuppressAutoMemoryFallback()
        {
            return suppressAutoMemoryFallbackForTurn;
        }

        internal bool ContainsAnyPhrase(string text, IReadOnlyList<string> hints)
        {
            if (string.IsNullOrWhiteSpace(text) || hints == null || hints.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < hints.Count; i++)
            {
                string hint = hints[i];
                if (string.IsNullOrWhiteSpace(hint))
                {
                    continue;
                }

                if (Dialog_RPGPawnDialogue.MatchesPhraseWithBoundary(text, hint))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool MatchesPhraseWithBoundary(string text, string hint)
        {
            int idx = 0;
            while (idx <= text.Length - hint.Length)
            {
                int found = text.IndexOf(hint, idx, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    return false;
                }

                bool hasSpace = hint.Contains(' ');
                if (hasSpace)
                {
                    if (Dialog_RPGPawnDialogue.IsWordBoundaryBefore(text, found) && Dialog_RPGPawnDialogue.IsWordBoundaryAfter(text, found + hint.Length))
                    {
                        return true;
                    }
                }
                else
                {
                    if (!Dialog_RPGPawnDialogue.IsWordCharBefore(text, found) && !Dialog_RPGPawnDialogue.IsWordCharAfter(text, found + hint.Length))
                    {
                        return true;
                    }
                }

                idx = found + 1;
            }

            return false;
        }

        internal static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c);
        }

        internal static bool IsWordCharBefore(string text, int matchStart)
        {
            return matchStart > 0 && matchStart <= text.Length && Dialog_RPGPawnDialogue.IsWordChar(text[matchStart - 1]);
        }

        internal static bool IsWordCharAfter(string text, int matchEnd)
        {
            return matchEnd < text.Length && Dialog_RPGPawnDialogue.IsWordChar(text[matchEnd]);
        }

        internal static bool IsWordBoundaryBefore(string text, int position)
        {
            if (position == 0)
            {
                return true;
            }

            return !Dialog_RPGPawnDialogue.IsWordChar(text[position - 1]);
        }

        internal static bool IsWordBoundaryAfter(string text, int position)
        {
            if (position >= text.Length)
            {
                return true;
            }

            return !Dialog_RPGPawnDialogue.IsWordChar(text[position]);
        }
        }

}
