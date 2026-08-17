using System;
using System.Collections;
using System.Linq;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Core.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class RelationsApiUsabilitySection
{
    readonly RelationsSettingsPages Pages;

    internal RelationsApiUsabilitySection(RelationsSettingsPages pages)
    {
        Pages = pages;
    }

    RelationsSettings Settings => Pages.Settings;

        internal bool _isTestingUsability;
        internal string _usabilityProgressText = string.Empty;
        internal ApiUsabilityDiagnosticResult _lastUsabilityResult;
        internal bool _showUsabilityTechDetail;

        internal bool IsAnyApiTestRunning()
        {
            return _isTestingUsability;
        }

        internal void DrawApiTestButton(Listing_Standard listing)
        {
            Rect buttonRect = listing.GetRect(30f);
            DrawUsabilityTestButton(buttonRect);
        }

        internal void DrawUsabilityTestButton(Rect buttonRect)
        {
            bool disable = IsAnyApiTestRunning();
            string label = _isTestingUsability
                ? "RimChat_UsabilityTesting".Translate()
                : "RimChat_TestUsabilityButton".Translate();
            GUI.color = disable ? Color.gray : Color.white;
            bool clicked = Widgets.ButtonText(buttonRect, label, active: !disable);
            GUI.color = Color.white;
            if (clicked && !disable)
            {
                StartUsabilityTest();
            }
        }

        internal void DrawUsabilityTestResult(Listing_Standard listing)
        {
            if (_isTestingUsability && !string.IsNullOrWhiteSpace(_usabilityProgressText))
            {
                GUI.color = Color.yellow;
                listing.Label(_usabilityProgressText);
                GUI.color = Color.white;
                return;
            }

            if (_lastUsabilityResult == null)
            {
                return;
            }

            Color statusColor = _lastUsabilityResult.IsSuccess ? Color.green : Color.red;
            GUI.color = statusColor;
            listing.Label(BuildUsabilitySummaryText(_lastUsabilityResult));
            GUI.color = Color.white;
            DrawUsabilityHints(listing, _lastUsabilityResult);
            DrawUsabilityDebugActions(listing, _lastUsabilityResult);
        }

        internal void DrawUsabilityHints(Listing_Standard listing, ApiUsabilityDiagnosticResult result)
        {
            if (result == null || result.IsSuccess)
            {
                return;
            }

            var suggestions = ApiDiagnosticSuggestionService.GenerateSuggestions(
                result.ErrorCode,
                result.HttpCode,
                result.TechDetail,
                result.Provider,
                result.IsCloud,
                result.EndpointUsed);

            int index = 1;
            foreach (var suggestion in suggestions.Where(s => s != null))
            {
                string causeText = suggestion.CauseKey.Translate();
                string solutionText = suggestion.SolutionKey.Translate();
                string displayText = $"{index}. [{suggestion.ProbabilityPercent}%] {causeText}";
                listing.Label(displayText);
                listing.Label($"   → {solutionText}");
                index++;
            }
        }

        internal void DrawUsabilityDebugActions(Listing_Standard listing, ApiUsabilityDiagnosticResult result)
        {
            if (result == null)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                Rect openRect = listing.GetRect(26f);
                if (Widgets.ButtonText(openRect, "RimChat_OpenApiDebugWindowButton".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_ApiDebugObservability());
                }
            }

            Rect toggleRect = listing.GetRect(24f);
            string toggleKey = _showUsabilityTechDetail
                ? "RimChat_UsabilityHideTechDetails"
                : "RimChat_UsabilityShowTechDetails";
            if (Widgets.ButtonText(toggleRect, toggleKey.Translate()))
            {
                _showUsabilityTechDetail = !_showUsabilityTechDetail;
            }

            if (_showUsabilityTechDetail)
            {
                listing.Label("RimChat_UsabilityTechDetails".Translate(result.TechDetail ?? string.Empty));
            }
        }

        internal string BuildUsabilitySummaryText(ApiUsabilityDiagnosticResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            if (result.IsSuccess)
            {
                string speedLabel = GetUsabilitySpeedLabelKey(result.ElapsedMs).Translate();
                string summary = "RimChat_UsabilitySuccessSummary".Translate(result.ElapsedMs.ToString(), speedLabel);
                if (IsUsabilityExtremeSlow(result.ElapsedMs))
                {
                    summary += " " + "RimChat_UsabilityPoorConnectionAdvice".Translate();
                }

                return summary;
            }

            string errorTitle = ApiUsabilityDiagnosticService.GetErrorTitleKey(result.ErrorCode).Translate();
            string stepTitle = ApiUsabilityDiagnosticService.GetStepLabelKey(result.Step).Translate();
            string failureSummary = "RimChat_UsabilityFailureSummary".Translate(errorTitle, stepTitle, result.ElapsedMs.ToString());
            if (result.Step == ApiUsabilityStep.ConfigValidation && !string.IsNullOrWhiteSpace(result.TechDetail))
            {
                failureSummary += " " + result.TechDetail;
            }

            return failureSummary;
        }

        internal static string GetUsabilitySpeedLabelKey(long elapsedMs)
        {
            if (elapsedMs < 500L)
            {
                return "RimChat_UsabilitySpeed_ExtremeFast";
            }

            if (elapsedMs < 1500L)
            {
                return "RimChat_UsabilitySpeed_Fast";
            }

            if (elapsedMs < 3000L)
            {
                return "RimChat_UsabilitySpeed_Normal";
            }

            if (elapsedMs < 6000L)
            {
                return "RimChat_UsabilitySpeed_Slow";
            }

            return "RimChat_UsabilitySpeed_ExtremeSlow";
        }

        internal static bool IsUsabilityExtremeSlow(long elapsedMs)
        {
            return elapsedMs >= 6000L;
        }

        internal void StartUsabilityTest()
        {
            _isTestingUsability = true;
            _showUsabilityTechDetail = false;
            _lastUsabilityResult = null;
            _usabilityProgressText = "RimChat_UsabilityTesting".Translate();

            if (AIChatServiceAsync.Instance == null)
            {
                HandleUsabilityCompleted(BuildImmediateUsabilityFailure(
                    ApiUsabilityErrorCode.UNKNOWN,
                    "Coroutine host is unavailable.",
                    string.Empty,
                    Settings.UseCloudProviders));
                return;
            }

            AIChatServiceAsync.Instance.StartCoroutine(RunUsabilityTestCoroutine());
        }

        internal IEnumerator RunUsabilityTestCoroutine()
        {
            ApiUsabilityDiagnosticResult result = null;
            if (Settings.UseCloudProviders)
            {
                ApiConfig config = ResolvePrimaryCloudConfigForUsability();
                if (config == null)
                {
                    HandleUsabilityCompleted(BuildImmediateUsabilityFailure(
                        ApiUsabilityErrorCode.UNKNOWN,
                        "RimChat_NoValidConfig".Translate(),
                        string.Empty,
                        true));
                    yield break;
                }

                yield return ApiUsabilityDiagnosticService.RunCloudDiagnosticCoroutine(
                    config,
                    HandleUsabilityProgress,
                    diagnostic => result = diagnostic);
            }
            else
            {
                yield return ApiUsabilityDiagnosticService.RunLocalDiagnosticCoroutine(
                    Settings.LocalConfig,
                    HandleUsabilityProgress,
                    diagnostic => result = diagnostic);
            }

            HandleUsabilityCompleted(result ?? BuildImmediateUsabilityFailure(
                ApiUsabilityErrorCode.UNKNOWN,
                "No diagnostic result returned.",
                string.Empty,
                Settings.UseCloudProviders));
        }

        internal ApiConfig ResolvePrimaryCloudConfigForUsability()
        {
            if (Settings.CloudConfigs == null || Settings.CloudConfigs.Count == 0)
            {
                return null;
            }

            ApiConfig enabled = Settings.CloudConfigs.FirstOrDefault(cfg => cfg != null && cfg.IsEnabled);
            return enabled ?? Settings.CloudConfigs.FirstOrDefault(cfg => cfg != null);
        }

        internal void HandleUsabilityProgress(ApiUsabilityProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            string stepLabel = ApiUsabilityDiagnosticService.GetStepLabelKey(progress.Step).Translate();
            _usabilityProgressText = "RimChat_UsabilityStepProgress".Translate(
                progress.Current.ToString(),
                progress.Total.ToString(),
                stepLabel);
        }

        internal void HandleUsabilityCompleted(ApiUsabilityDiagnosticResult result)
        {
            _isTestingUsability = false;
            _lastUsabilityResult = result;
            _usabilityProgressText = string.Empty;
            RecordUsabilityDebug(result);
        }

        internal void RecordUsabilityDebug(ApiUsabilityDiagnosticResult result)
        {
            if (result == null)
            {
                return;
            }

            AIChatServiceAsync.RecordExternalDebugRecord(
                AIRequestDebugSource.ApiUsabilityTest,
                DialogueUsageChannel.Unknown,
                result.ModelName ?? string.Empty,
                result.IsSuccess ? AIRequestDebugStatus.Success : AIRequestDebugStatus.Error,
                result.ElapsedMs,
                result.HttpCode,
                result.DebugRequestText ?? string.Empty,
                result.DebugResponseText ?? string.Empty,
                result.TechDetail ?? string.Empty);
        }

        internal static ApiUsabilityDiagnosticResult BuildImmediateUsabilityFailure(
            ApiUsabilityErrorCode code,
            string detail,
            string modelName,
            bool isCloud)
        {
            return new ApiUsabilityDiagnosticResult
            {
                IsSuccess = false,
                Step = ApiUsabilityStep.ConfigValidation,
                ErrorCode = code,
                TechDetail = detail ?? string.Empty,
                HttpCode = 0,
                EndpointUsed = string.Empty,
                ElapsedMs = 0,
                PlayerHintKeys = new System.Collections.Generic.List<string>
                {
                    "RimChat_UsabilityHint_UNKNOWN_1",
                    "RimChat_UsabilityHint_UNKNOWN_2"
                },
                ModelName = modelName ?? string.Empty,
                IsCloud = isCloud
            };
        }
    
}
