using System;
using System.Collections.Generic;
using System.IO;
using Scriban;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;
using Ustas.RimAI.Communication.Relations.Prompting.Diplomacy;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;

internal static class PromptDecompositionTests
{
    public static void Run(Action<bool, string> check)
    {
        ConfigStore(check);
        Normalization(check);
        BundleTransfer(check);
        DiplomacyBuilder(check);
        RpgBuilder(check);
        ContextAssembler(check);
        Snapshot(check);
        TemplateBoundary(check);
    }

    static void ConfigStore(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "rimai-prompt-config-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "SystemPrompt_Custom.json");
        try
        {
            var store = new PromptConfigStore(() => path, () => Directory.CreateDirectory(root));
            check(!store.Exists(), "config store missing file");
            check(PromptConfigStore.ReadAllText(path) == string.Empty, "config store missing file reads empty");

            const string payload = "{\"PromptSchemaVersion\":3,\"Enabled\":true}";
            store.WriteAllText(payload);
            check(store.Exists(), "config store save creates file");
            check(store.ReadAllText() == payload, "config store save/load roundtrip");

            PromptConfigStore.WriteAllText(path, "{\"PromptSchemaVersion\":0}");
            check(store.ReadAllText().Contains("PromptSchemaVersion"), "config store overwrite preserves json object");

            string malformedPath = Path.Combine(root, "malformed.json");
            PromptConfigStore.WriteAllText(malformedPath, "not-json");
            check(PromptConfigStore.ReadAllText(malformedPath) == "not-json", "config store malformed file is readable as text");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    static void Normalization(Action<bool, string> check)
    {
        check(PromptConfigDocumentNormalizer.NormalizeSchemaVersion(0, 3) == 3, "schema 0 becomes current");
        check(PromptConfigDocumentNormalizer.NormalizeSchemaVersion(-1, 3) == 3, "negative schema becomes current");
        check(PromptConfigDocumentNormalizer.NormalizeSchemaVersion(3, 3) == 3, "current schema preserved");
        check(PromptConfigDocumentNormalizer.NormalizeSchemaVersion(2, 3) == 2, "existing schema preserved");
        check(PromptConfigDocumentNormalizer.NullToEmpty(null) == string.Empty, "null string normalizes to empty");
        check(PromptConfigDocumentNormalizer.NullToEmpty(" keep ") == " keep ", "non-null string is not trimmed by schema normalizer");
        check(PromptConfigDocumentNormalizer.CurrentPromptSchemaVersion == 3, "canonical prompt schema version");
        check(PromptConfigDocumentNormalizer.CurrentPromptPolicySchemaVersion == 4, "canonical policy schema version");
    }

    static void BundleTransfer(Action<bool, string> check)
    {
        check(PromptBundleModuleCatalog.ToStorageToken(PromptBundleModule.DiplomacyPrompt) == "diplomacy_prompt", "bundle diplomacy token");
        check(PromptBundleModuleCatalog.TryParseStorageToken("rpg_prompt", out PromptBundleModule rpg) && rpg == PromptBundleModule.RpgPrompt, "bundle rpg token parse");
        check(!PromptBundleModuleCatalog.TryParseStorageToken("rimtalk_rpg", out _), "donor rimtalk token rejected");

        check(!PromptBundleEnvelope.TryValidate("", out PromptBundleImportFailure emptyFailure, out string emptyCode)
            && emptyFailure == PromptBundleImportFailure.EmptyFile
            && emptyCode == PromptBundleImportErrorCodes.EmptyFile, "bundle empty json");
        check(!PromptBundleEnvelope.TryValidate("not-json", out PromptBundleImportFailure invalidFailure, out _)
            && invalidFailure == PromptBundleImportFailure.InvalidJson, "bundle malformed json");
        check(!PromptBundleEnvelope.TryValidate("{\"Presets\":[]}", out PromptBundleImportFailure presetFailure, out _)
            && presetFailure == PromptBundleImportFailure.PresetFileDetected, "bundle preset file rejected");
        check(!PromptBundleEnvelope.TryValidate("{\"hello\":1}", out PromptBundleImportFailure notBundle, out _)
            && notBundle == PromptBundleImportFailure.NotPromptBundle, "bundle missing markers");

        string valid = "{\"BundleVersion\":2,\"IncludedModules\":[\"system_prompt\"],\"SystemPrompt\":{}}";
        check(PromptBundleEnvelope.TryValidate(valid, out PromptBundleImportFailure ok, out string okCode)
            && ok == PromptBundleImportFailure.None
            && okCode == string.Empty, "bundle valid envelope");

        string partial = "{\"BundleVersion\":2,\"IncludedModules\":[\"diplomacy_prompt\"]}";
        check(!PromptBundleEnvelope.TryValidate(partial, out PromptBundleImportFailure missingPayload, out _)
            && missingPayload == PromptBundleImportFailure.NotPromptBundle, "bundle missing payload is not imported");

        check(PromptJsonText.LooksLikeJsonObject(valid), "bundle json object probe");
        check(!PromptJsonText.LooksLikeJsonObject("[1]"), "array is not bundle object");
    }

    static void DiplomacyBuilder(Action<bool, string> check)
    {
        check(PromptRuntimeChannels.ResolveDiplomacy(false) == "diplomacy_dialogue", "diplomacy base channel");
        check(PromptRuntimeChannels.ResolveDiplomacy(true) == "proactive_diplomacy_dialogue", "diplomacy proactive channel");

        var complete = new DiplomacyStrategyPromptContext
        {
            NegotiatorContextText = " Negotiator ",
            StrategyFactPackText = "Fact pack",
            ScenarioDossierText = "Dossier"
        };
        Dictionary<string, object> values = DiplomacyStrategyRuntimeValues.BuildOrThrow(complete);
        check((string)values[DiplomacyStrategyRuntimeValues.NegotiatorKey] == "Negotiator", "strategy negotiator trimmed");
        check((string)values[DiplomacyStrategyRuntimeValues.FactPackKey] == "Fact pack", "strategy fact pack");
        check((string)values[DiplomacyStrategyRuntimeValues.DossierKey] == "Dossier", "strategy dossier");

        bool missingThrows = false;
        try
        {
            DiplomacyStrategyRuntimeValues.BuildOrThrow(new DiplomacyStrategyPromptContext());
        }
        catch (PromptRenderException)
        {
            missingThrows = true;
        }
        check(missingThrows, "strategy incomplete context fails closed");

        string hints = DiplomacyPromptContractComposer.ComposeStaticContractHints();
        check(hints.Contains(PromptTextConstants.OutputSpecificationAuthorityHeader), "diplomacy authority header");
        check(hints.Contains(PromptTextConstants.ResponseFormatReference), "diplomacy response contract");
        check(hints.Contains("visible_dialogue"), "diplomacy visible_dialogue contract");
        check(hints.Contains(PromptTextConstants.NoActionResponseHint), "diplomacy empty-action hint");
        check(!hints.Contains("OPENAI_RIMAI"), "diplomacy hints have no credential");
    }

    static void RpgBuilder(Action<bool, string> check)
    {
        check(RpgPromptComposition.ResolveChannel(false) == "rpg_dialogue", "rpg ordinary channel");
        check(RpgPromptComposition.ResolveChannel(true) == "proactive_rpg_dialogue", "rpg proactive channel");
        string format = RpgPromptComposition.ComposeStaticFormatHints();
        check(format.Contains(PromptTextConstants.StrictJsonFormatHeader), "rpg format header");
        check(format.Contains(PromptTextConstants.StrictJsonFormatRequirement), "rpg json contract");
        check(format.Contains("{") && format.Contains("}"), "rpg contract mentions json braces");
        check(!format.Contains("pawn.player_negotiator"), "rpg does not inject diplomacy negotiator key");
    }

    static void ContextAssembler(Action<bool, string> check)
    {
        check(PromptTextTruncate.AtNaturalBoundary("abc", 10) == "abc", "truncate leaves short text");
        check(PromptTextTruncate.AtNaturalBoundary("hello world", 0) == string.Empty, "truncate zero max");
        string longText = "First sentence. Second sentence is longer than the limit.";
        string truncated = PromptTextTruncate.AtNaturalBoundary(longText, 20);
        check(truncated.EndsWith("\n..."), "truncate adds ellipsis");
        check(truncated.StartsWith("First sentence."), "truncate prefers sentence boundary");

        string original = "unchanged config body";
        string copy = original;
        PromptTextTruncate.AtNaturalBoundary(copy, 4);
        check(copy == original, "truncate does not mutate caller string identity source");

        check(RelationsContextContributorOrder.DiplomacySnapshotBlocks.Length == 7, "diplomacy contributor count");
        check(RelationsContextContributorOrder.DiplomacySnapshotBlocks[0] == "environment", "contributor order starts with environment");
        check(RelationsContextContributorOrder.DiplomacySnapshotBlocks[6] == "quest", "contributor order ends with quest");
    }

    static void Snapshot(Action<bool, string> check)
    {
        string rendered = PromptConfigMetadataSnapshot.Render(
            3,
            4,
            true,
            "Prompt/Custom/SystemPrompt_Custom.json",
            new Dictionary<string, string>
            {
                ["api_key"] = "sk-secret",
                ["OPENAI_RIMAI"] = "should-not-appear",
                ["modules"] = "5"
            });
        check(rendered.Contains("schema=3"), "snapshot schema");
        check(rendered.Contains("policy=4"), "snapshot policy");
        check(rendered.Contains("modules=5"), "snapshot safe extra");
        check(!rendered.Contains("sk-secret"), "snapshot omits api key");
        check(!rendered.Contains("should-not-appear"), "snapshot omits gameplay credential");
        check(!rendered.Contains("Authorization"), "snapshot omits auth header");
        check(PromptConfigMetadataSnapshot.LooksLikeSecret("openai_rimai"), "credential names are secrets");
        check(!PromptConfigMetadataSnapshot.LooksLikeSecret("schemaVersion"), "schema is not a secret");
    }

    static void TemplateBoundary(Action<bool, string> check)
    {
        var cache = new PromptTemplateCache(2);
        Template first = Template.Parse("Hello {{ name }}");
        Template second = Template.Parse("Bye {{ name }}");
        Template third = Template.Parse("Third {{ name }}");
        cache.Set("a", first);
        check(cache.TryGet("a", out Template cached) && cached == first, "template cache hit");
        cache.Set("b", second);
        cache.Set("c", third);
        check(!cache.TryGet("a", out _), "template cache evicts oldest");
        check(cache.TryGet("c", out _), "template cache keeps newest");

        string rendered = first.Render(new { name = "colony" });
        check(rendered.Contains("Hello colony"), "existing scriban render unchanged");
        check(PromptTextConstants.ResponseContractNodeLiteralDefault.Contains("{{ dialogue.response_contract_body }}"), "scriban namespaced token preserved");
    }
}
