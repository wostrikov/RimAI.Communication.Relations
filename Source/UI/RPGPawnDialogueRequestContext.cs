using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

namespace Ustas.RimAI.Communication.Relations.UI
{
    // Responsibilities: build request-time prompt context for RPG pawn dialogue turns.
    // Dependencies: Ustas.RimAI.Communication.Relations.AI.ChatMessageData, Ustas.RimAI.Communication.Relations.Persistence.PromptPersistenceService.
        internal sealed class RPGPawnDialogueRequestContext : Dialog_RPGPawnDialogueCollaborator
    {
        internal RPGPawnDialogueRequestContext(Dialog_RPGPawnDialogue owner) : base(owner)
        {
        }


        internal const string OpeningFallbackUserPrompt =
            "Start the conversation naturally in-character with one concise opening line.";

        internal string NormalizeHistoryAssistantContent(DialogueResponseEnvelope envelope, string visibleDialogueText)
        {
            if (envelope != null)
            {
                string normalizedVisible = Owner.NormalizeEnvelopeVisibleDialogueForDisplay(envelope, "history");
                if (!string.IsNullOrWhiteSpace(normalizedVisible))
                {
                    return normalizedVisible;
                }
            }

            if (!string.IsNullOrWhiteSpace(visibleDialogueText))
            {
                return Owner.NormalizeVisibleNpcDialogueText(visibleDialogueText);
            }

            return string.Empty;
        }

        internal string ExtractNarrativeOnly(string rawResponse)
        {
            string narrative = ModelOutputSanitizer.TryExtractSafeVisibleDialogue(rawResponse);
            if (string.IsNullOrWhiteSpace(narrative))
            {
                return string.Empty;
            }

            return Owner.NormalizeVisibleNpcDialogueText(narrative);
        }

        internal string NormalizeVisibleNpcDialogueText(string content)
        {
            string normalized = Dialog_RPGPawnDialogue.CollapseWhitespace(content);
            ImmersionGuardResult guardResult = ImmersionOutputGuard.ValidateVisibleDialogue(normalized);
            if (!string.IsNullOrWhiteSpace(guardResult?.TrailingActionsJson))
            {
                Log.Warning("[RimAI.Relations] RPG display stripped trailing action JSON from visible text path: source=NormalizeVisibleNpcDialogueText");
            }

            if (!guardResult.IsValid)
            {
                Log.Warning($"[RimAI.Relations] Immersion guard blocked RPG visible text: reason={ImmersionOutputGuard.BuildViolationTag(guardResult.ViolationReason)}, snippet={guardResult.ViolationSnippet}");
                normalized = ImmersionOutputGuard.BuildLocalFallbackDialogue(DialogueUsageChannel.Rpg);
            }
            else
            {
                normalized = guardResult.VisibleDialogue;
            }

            if (!Owner.ShouldApplyNonVerbalSpeechFormatting())
            {
                return normalized;
            }

            return Owner.EnsureNonVerbalSpeechFormat(normalized);
        }

        internal string NormalizeEnvelopeVisibleDialogueForDisplay(DialogueResponseEnvelope envelope, string sourceTag)
        {
            if (envelope == null)
            {
                return string.Empty;
            }

            string normalized = Owner.NormalizeVisibleNpcDialogueText(envelope.VisibleDialogue ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(envelope.ActionsJson) &&
                envelope.ProtocolKind == DialogueResponseProtocolKind.LegacyText)
            {
                Log.Warning(
                    $"[RimAI.Relations] RPG UI consumed legacy dialogue bridge with detached actions JSON: source={sourceTag}, protocol={envelope.ProtocolKind}, visible_len={normalized.Length}, actions_len={envelope.ActionsJson.Length}");
            }

            return normalized;
        }

        internal static string CollapseWhitespace(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(content.Length);
            bool previousWasWhitespace = false;
            for (int i = 0; i < content.Length; i++)
            {
                char character = content[i];
                if (!char.IsWhiteSpace(character))
                {
                    sb.Append(character);
                    previousWasWhitespace = false;
                    continue;
                }

                if (previousWasWhitespace)
                {
                    continue;
                }

                sb.Append(' ');
                previousWasWhitespace = true;
            }

            return sb.ToString().Trim();
        }

        internal bool ShouldApplyNonVerbalSpeechFormatting()
        {
            return RelationsMod.Settings?.EnableRPGNonVerbalPawnSpeech == true && Dialog_RPGPawnDialogue.IsNonVerbalSpeechPawn(target);
        }

        internal string EnsureNonVerbalSpeechFormat(string normalized)
        {
            bool useFullWidth = Dialog_RPGPawnDialogue.UseFullWidthParentheses();
            string open = useFullWidth ? "（" : "(";
            string close = useFullWidth ? "）" : ")";
            if (Dialog_RPGPawnDialogue.TryParseSoundThoughtPair(normalized, out string sound, out string thought))
            {
                return $"{sound}{open}{thought}{close}";
            }

            string defaultSound = Dialog_RPGPawnDialogue.ResolveDefaultNonVerbalSound(target);
            string thoughtText = string.IsNullOrWhiteSpace(normalized)
                ? "RimChat_RPGNonVerbalFallbackThought".Translate().ToString()
                : normalized;
            return $"{defaultSound}{open}{thoughtText}{close}";
        }

        internal string ApplyNonVerbalSpeechFormatting(string basePrompt)
        {
            string result = basePrompt ?? string.Empty;

            if (Owner.ShouldApplyNonVerbalSpeechFormatting())
            {
                result = Owner.ApplyNonVerbalSpeechConstraintTemplate(result);
            }

            result = Owner.ApplyCharacterStyleConstraint(result);

            return result;
        }

        internal string ApplyNonVerbalSpeechConstraintTemplate(string basePrompt)
        {
            RpgPromptDefaultsConfig defaults = RpgPromptDefaultsProvider.GetDefaults();
            string template = defaults?.NonVerbalOutputConstraintTemplate;
            if (string.IsNullOrWhiteSpace(template))
            {
                return basePrompt ?? string.Empty;
            }

            bool useFullWidth = Dialog_RPGPawnDialogue.UseFullWidthParentheses();
            const string templateId = "prompt_templates.rpg_non_verbal_constraint";
            PromptRenderContext context = PromptRenderContext.Create(templateId, "rpg");
            context.SetValue("pawn.speaker.kind", Dialog_RPGPawnDialogue.ResolveNonVerbalSpeakerKind(target));
            context.SetValue("pawn.speaker.default_sound", Dialog_RPGPawnDialogue.ResolveDefaultNonVerbalSound(target));
            context.SetValue("pawn.speaker.animal_sound", "RimChat_RPGNonVerbalSound_Animal".Translate().ToString());
            context.SetValue("pawn.speaker.baby_sound", "RimChat_RPGNonVerbalSound_Baby".Translate().ToString());
            context.SetValue("pawn.speaker.mechanoid_sound", "RimChat_RPGNonVerbalSound_Mechanoid".Translate().ToString());
            context.SetValue("system.punctuation.open_paren", useFullWidth ? "（" : "(");
            context.SetValue("system.punctuation.close_paren", useFullWidth ? "）" : ")");
            string rendered = PromptTemplateRenderer.RenderOrThrow(templateId, "rpg", template, context);
            if (string.IsNullOrWhiteSpace(rendered))
            {
                return basePrompt ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(basePrompt))
            {
                return rendered;
            }

            return $"{basePrompt}\n\n{rendered}";
        }

        internal string ApplyCharacterStyleConstraint(string basePrompt)
        {
            RpgPromptDefaultsConfig defaults = RpgPromptDefaultsProvider.GetDefaults();
            string template = defaults?.CharacterStyleTemplate;
            if (string.IsNullOrWhiteSpace(template))
            {
                return basePrompt ?? string.Empty;
            }

            const string templateId = "prompt_templates.rpg_character_style_constraint";
            PromptRenderContext context = PromptRenderContext.Create(templateId, "rpg");
            context.SetValue("pawn.speaker.racial_type", Dialog_RPGPawnDialogue.ResolveRacialType(target));
            context.SetValue("pawn.speaker.social_identity", Dialog_RPGPawnDialogue.ResolveSocialIdentity(target));
            context.SetValue("pawn.speaker.relationship_status", Dialog_RPGPawnDialogue.ResolveRelationshipStatus(target));
            context.SetValue("pawn.speaker.personality_traits", Dialog_RPGPawnDialogue.ResolvePersonalityTraits(target));
            context.SetValue("pawn.speaker.style_guidelines", Dialog_RPGPawnDialogue.BuildStyleGuidelines(target));
            string rendered = PromptTemplateRenderer.RenderOrThrow(templateId, "rpg", template, context);
            if (string.IsNullOrWhiteSpace(rendered))
            {
                return basePrompt ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(basePrompt))
            {
                return rendered;
            }

            return $"{basePrompt}\n\n{rendered}";
        }

        internal static bool HasVisibleAssistantReply(IEnumerable<ChatMessageData> messages)
        {
            if (messages == null)
            {
                return false;
            }

            return messages.Any(message =>
                message != null &&
                string.Equals(message.role, "assistant", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(message.content));
        }

        internal static string ExtractLatestVisibleUserIntent(IEnumerable<ChatMessageData> messages)
        {
            if (messages == null)
            {
                return string.Empty;
            }

            List<ChatMessageData> reversed = messages
                .Where(message =>
                    message != null &&
                    string.Equals(message.role, "user", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(message.content))
                .Reverse()
                .ToList();

            for (int i = 0; i < reversed.Count; i++)
            {
                string content = reversed[i].content?.Trim() ?? string.Empty;
                if (!Dialog_RPGPawnDialogue.IsPromptSeedUserMessage(content))
                {
                    return content;
                }
            }

            return string.Empty;
        }

        internal static bool IsPromptSeedUserMessage(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            return string.Equals(content.Trim(), OpeningFallbackUserPrompt, StringComparison.Ordinal) ||
                content.StartsWith("A proactive trigger opened this chat from NPC side.", StringComparison.Ordinal);
        }

        internal string BuildRpgSystemPromptForRequest(bool openingTurn, string currentTurnUserIntent)
        {
            var settings = RelationsMod.Settings;
            List<string> tags = Dialog_RPGPawnDialogue.ParseSceneTagsCsv(settings?.RpgManualSceneTagsCsv) ?? new List<string>();
            if (openingTurn && !tags.Contains("phase:opening"))
            {
                tags.Add("phase:opening");
            }

            string prompt;
            using (RpgPromptTurnContextScope.Push(
                currentTurnUserIntent,
                allowMemoryCompressionScheduling: !openingTurn,
                allowMemoryColdLoad: !openingTurn,
                turnCount: Owner.GetNpcDialogueRoundCount()))
            using (Ustas.RimAI.Communication.Relations.Context.ExpandMemoryMatchContext.Push(currentTurnUserIntent))
            {
                prompt = Ustas.RimAI.Communication.Relations.Persistence.PromptPersistenceService.Instance.BuildRPGFullSystemPrompt(
                    initiator,
                    target,
                    false,
                    tags,
                    allowMemoryCompressionScheduling: !openingTurn,
                    allowMemoryColdLoad: !openingTurn);
            }

            prompt = Owner.ApplyNonVerbalSpeechFormatting(prompt);
            Owner.UpdateRpgActionContractGuard(prompt, settings?.EnableRPGAPI == true);
            return prompt;
        }

        internal void UpdateRpgActionContractGuard(string prompt, bool rpgApiEnabled)
        {
            if (!rpgApiEnabled)
            {
                suppressAutoMemoryFallbackForTurn = false;
                return;
            }

            bool hasContract = Dialog_RPGPawnDialogue.HasRpgActionContract(prompt);
            suppressAutoMemoryFallbackForTurn = !hasContract;
            if (!hasContract)
            {
                Log.Warning("[RimAI.Relations] RPG prompt missing response contract body; auto memory fallback disabled for this turn.");
            }
        }

        internal static bool HasRpgActionContract(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return false;
            }

            return prompt.IndexOf("<response_contract>", StringComparison.OrdinalIgnoreCase) >= 0
                || prompt.IndexOf("=== AVAILABLE NPC ACTIONS", StringComparison.OrdinalIgnoreCase) >= 0
                || prompt.IndexOf("Allowed actions:", StringComparison.OrdinalIgnoreCase) >= 0
                || prompt.IndexOf("ExitDialogueCooldown", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        }

}
