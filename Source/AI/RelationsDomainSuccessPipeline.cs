using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Relations.AI
{
    internal enum DomainSuccessAction
    {
        Complete,
        Retry
    }

    /// <summary>
    /// Relations domain processing after any transport returns a parsed assistant payload.
    /// Order: envelope parse → immersion → text integrity → Diplomacy/RPG contract.
    /// Transport retries stay in Core; semantic/contract retries stay here.
    /// </summary>
    internal static class RelationsDomainSuccessPipeline
    {
        public static DomainSuccessAction Process(
            string parsedResponse,
            AIRequestDebugSource debugSource,
            DialogueUsageChannel usageChannel,
            ref List<ChatMessageData> attemptMessages,
            ref int parseRetryCount,
            ref int immersionRetryCount,
            ref int textIntegrityRetryCount,
            ref int contractRetryCount,
            ref string contractValidationStatus,
            ref string contractFailureReason,
            out string processedResponse)
        {
            processedResponse = parsedResponse ?? string.Empty;
            bool bypassDialogueGuardsForSocialNews = debugSource == AIRequestDebugSource.SocialNews;
            DialogueResponseEnvelope parsedEnvelope = null;
            bool useStableRpgFallback = usageChannel == DialogueUsageChannel.Rpg;

            if (ShouldUseStructuredDialogueEnvelope(debugSource, usageChannel))
            {
                parsedEnvelope = DialogueResponseEnvelopeParser.Parse(processedResponse, usageChannel);
                if (!parsedEnvelope.IsValid && parseRetryCount < RelationsSemanticRetry.MaxParseRetryCount)
                {
                    parseRetryCount++;
                    attemptMessages = RelationsSemanticRetry.AppendDialogueEnvelopeRetryMessage(
                        attemptMessages,
                        usageChannel,
                        parsedEnvelope.FailureReason);
                    DebugLogger.WarningGated($"Dialogue envelope retry requested: reason={parsedEnvelope.FailureReason}");
                    return DomainSuccessAction.Retry;
                }

                if (!parsedEnvelope.IsValid)
                {
                    string envelopeFailureReason = parsedEnvelope.FailureReason;
                    string rawPassthrough = processedResponse ?? string.Empty;
                    string safeVisible = useStableRpgFallback
                        ? ModelOutputSanitizer.TryExtractSafeVisibleDialogue(rawPassthrough)
                        : string.Empty;
                    parsedEnvelope = null;
                    processedResponse = useStableRpgFallback && !string.IsNullOrWhiteSpace(safeVisible)
                        ? safeVisible
                        : rawPassthrough;
                    string responsePreview = RelationsLocalProviderRetry.BuildResponsePreviewForLog(rawPassthrough, 280);
                    DebugLogger.WarningGated(
                        $"Dialogue envelope raw passthrough used after retry: reason={envelopeFailureReason}, response_preview={responsePreview}");
                }
                else
                {
                    processedResponse = parsedEnvelope.ToStructuredResponseText();
                }
            }

            if (!bypassDialogueGuardsForSocialNews && ShouldGuardImmersion(usageChannel))
            {
                ImmersionGuardResult guardResult = parsedEnvelope != null
                    ? ImmersionOutputGuard.ValidateVisibleDialogueParts(parsedEnvelope.VisibleDialogue, parsedEnvelope.ActionsJson)
                    : ImmersionOutputGuard.ValidateVisibleDialogue(processedResponse);
                if (!guardResult.IsValid && immersionRetryCount < RelationsSemanticRetry.MaxImmersionRetryCount)
                {
                    immersionRetryCount++;
                    attemptMessages = RelationsSemanticRetry.AppendImmersionRetryMessage(attemptMessages, usageChannel, guardResult);
                    DebugLogger.WarningGated(
                        $"Immersion guard requested retry: reason={ImmersionOutputGuard.BuildViolationTag(guardResult.ViolationReason)}, snippet={guardResult.ViolationSnippet}");
                    return DomainSuccessAction.Retry;
                }

                if (!guardResult.IsValid)
                {
                    if (useStableRpgFallback)
                    {
                        string safeVisible = ModelOutputSanitizer.TryExtractSafeVisibleDialogue(processedResponse);
                        processedResponse = !string.IsNullOrWhiteSpace(safeVisible)
                            ? safeVisible
                            : ImmersionOutputGuard.BuildLocalFallbackDialogue(DialogueUsageChannel.Rpg);
                        parsedEnvelope = null;
                    }

                    DebugLogger.WarningGated(
                        $"Immersion guard failed after retry, outputting raw response: reason={ImmersionOutputGuard.BuildViolationTag(guardResult.ViolationReason)}");
                }
                else if (parsedEnvelope != null)
                {
                    parsedEnvelope.VisibleDialogue = guardResult.VisibleDialogue;
                    parsedEnvelope.ActionsJson = guardResult.TrailingActionsJson;
                    processedResponse = parsedEnvelope.ToStructuredResponseText();
                }
                else
                {
                    processedResponse = ModelOutputSanitizer.ComposeVisibleAndTrailingActions(
                        guardResult.VisibleDialogue,
                        guardResult.TrailingActionsJson);
                }
            }

            if (!bypassDialogueGuardsForSocialNews && ShouldGuardImmersion(usageChannel))
            {
                TextIntegrityCheckResult integrityResult = parsedEnvelope != null
                    ? TextIntegrityGuard.ValidateVisibleDialogueParts(parsedEnvelope.VisibleDialogue, parsedEnvelope.ActionsJson)
                    : TextIntegrityGuard.ValidateVisibleDialogue(processedResponse);
                if (!integrityResult.IsValid && textIntegrityRetryCount < RelationsSemanticRetry.MaxTextIntegrityRetryCount)
                {
                    textIntegrityRetryCount++;
                    attemptMessages = RelationsSemanticRetry.AppendTextIntegrityRetryMessage(attemptMessages, usageChannel, integrityResult);
                    DebugLogger.WarningGated($"Text integrity guard requested retry: reason={integrityResult.ReasonTag}");
                    return DomainSuccessAction.Retry;
                }

                if (!integrityResult.IsValid)
                {
                    if (useStableRpgFallback)
                    {
                        string safeVisible = ModelOutputSanitizer.TryExtractSafeVisibleDialogue(processedResponse);
                        processedResponse = !string.IsNullOrWhiteSpace(safeVisible)
                            ? safeVisible
                            : ImmersionOutputGuard.BuildLocalFallbackDialogue(DialogueUsageChannel.Rpg);
                        parsedEnvelope = null;
                    }

                    DebugLogger.WarningGated($"Text integrity guard failed after retry, outputting raw response: reason={integrityResult.ReasonTag}");
                }
                else if (parsedEnvelope != null)
                {
                    parsedEnvelope.VisibleDialogue = integrityResult.VisibleDialogue;
                    parsedEnvelope.ActionsJson = integrityResult.TrailingActionsJson;
                    processedResponse = parsedEnvelope.ToStructuredResponseText();
                }
                else
                {
                    processedResponse = ModelOutputSanitizer.ComposeVisibleAndTrailingActions(
                        integrityResult.VisibleDialogue,
                        integrityResult.TrailingActionsJson);
                }
            }

            if (!bypassDialogueGuardsForSocialNews && usageChannel == DialogueUsageChannel.Diplomacy)
            {
                DiplomacyResponseContractCheckResult contractResult = parsedEnvelope != null
                    ? DiplomacyResponseContractGuard.ValidateVisibleDialogueParts(parsedEnvelope.VisibleDialogue, parsedEnvelope.ActionsJson)
                    : DiplomacyResponseContractGuard.Validate(processedResponse);
                if (!contractResult.IsValid && contractRetryCount < RelationsSemanticRetry.MaxDiplomacyContractRetryCount)
                {
                    contractRetryCount++;
                    contractValidationStatus = "retry";
                    contractFailureReason = DiplomacyResponseContractGuard.BuildViolationTag(contractResult.Violation);
                    attemptMessages = RelationsSemanticRetry.AppendDiplomacyContractRetryMessage(attemptMessages, contractResult);
                    Log.Warning($"[RimAI.Relations] Diplomacy contract guard requested retry: reason={contractFailureReason}");
                    return DomainSuccessAction.Retry;
                }

                if (!contractResult.IsValid)
                {
                    contractValidationStatus = "failed_after_retry";
                    contractFailureReason = DiplomacyResponseContractGuard.BuildViolationTag(contractResult.Violation);
                    Log.Warning($"[RimAI.Relations] Diplomacy contract guard failed after retry, outputting raw response: reason={contractFailureReason}");
                }
                else
                {
                    contractValidationStatus = contractRetryCount > 0 ? "pass_after_retry" : "pass";
                    contractFailureReason = string.Empty;
                    if (parsedEnvelope != null)
                    {
                        parsedEnvelope.VisibleDialogue = contractResult.VisibleDialogue;
                        parsedEnvelope.ActionsJson = contractResult.TrailingActionsJson;
                        processedResponse = parsedEnvelope.ToStructuredResponseText();
                    }
                    else
                    {
                        processedResponse = ModelOutputSanitizer.ComposeVisibleAndTrailingActions(
                            contractResult.VisibleDialogue,
                            contractResult.TrailingActionsJson);
                    }
                }
            }

            if (!bypassDialogueGuardsForSocialNews && usageChannel == DialogueUsageChannel.Rpg)
            {
                RpgResponseContractCheckResult contractResult = parsedEnvelope != null
                    ? RpgResponseContractGuard.ValidateVisibleDialogueParts(
                        parsedEnvelope.VisibleDialogue,
                        parsedEnvelope.ActionsJson,
                        parsedEnvelope.ActionsJson)
                    : RpgResponseContractGuard.Validate(processedResponse);
                if (!contractResult.IsValid && contractRetryCount < RelationsSemanticRetry.MaxRpgContractRetryCount)
                {
                    contractRetryCount++;
                    contractValidationStatus = "retry";
                    contractFailureReason = RpgResponseContractGuard.BuildViolationTag(contractResult.Violation);
                    attemptMessages = RelationsSemanticRetry.AppendRpgContractRetryMessage(attemptMessages, contractResult);
                    DebugLogger.WarningGated($"RPG contract guard requested retry: reason={contractFailureReason}");
                    return DomainSuccessAction.Retry;
                }

                if (!contractResult.IsValid)
                {
                    contractValidationStatus = "failed_after_retry";
                    contractFailureReason = RpgResponseContractGuard.BuildViolationTag(contractResult.Violation);
                    string safeVisible = parsedEnvelope != null
                        ? ModelOutputSanitizer.TryExtractSafeVisibleDialogue(parsedEnvelope.VisibleDialogue)
                        : ModelOutputSanitizer.TryExtractSafeVisibleDialogue(processedResponse);
                    parsedEnvelope = null;
                    processedResponse = !string.IsNullOrWhiteSpace(safeVisible)
                        ? safeVisible
                        : ImmersionOutputGuard.BuildLocalFallbackDialogue(DialogueUsageChannel.Rpg);
                    DebugLogger.WarningGated($"RPG contract guard failed after retry, outputting raw response: reason={contractFailureReason}");
                }
                else
                {
                    contractValidationStatus = contractRetryCount > 0 ? "pass_after_retry" : "pass";
                    contractFailureReason = string.Empty;
                    if (parsedEnvelope != null)
                    {
                        parsedEnvelope.VisibleDialogue = contractResult.VisibleDialogue;
                        parsedEnvelope.ActionsJson = contractResult.TrailingActionsJson;
                        processedResponse = parsedEnvelope.ToStructuredResponseText();
                    }
                    else
                    {
                        processedResponse = ModelOutputSanitizer.ComposeVisibleAndTrailingActions(
                            contractResult.VisibleDialogue,
                            contractResult.TrailingActionsJson);
                    }
                }
            }

            return DomainSuccessAction.Complete;
        }

        public static bool ShouldGuardImmersion(DialogueUsageChannel usageChannel)
        {
            return usageChannel == DialogueUsageChannel.Diplomacy || usageChannel == DialogueUsageChannel.Rpg;
        }

        public static bool ShouldUseStructuredDialogueEnvelope(
            AIRequestDebugSource debugSource,
            DialogueUsageChannel usageChannel)
        {
            if (!ShouldGuardImmersion(usageChannel))
            {
                return false;
            }

            return debugSource == AIRequestDebugSource.DiplomacyDialogue ||
                debugSource == AIRequestDebugSource.RpgDialogue ||
                debugSource == AIRequestDebugSource.NpcPush ||
                debugSource == AIRequestDebugSource.PawnRpgPush;
        }
    }
}
