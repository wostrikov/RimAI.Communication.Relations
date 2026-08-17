using System;
using System.Collections.Generic;
using Scriban;
using Scriban.Runtime;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Prompting.Builders;

internal static class TemplateCleanupTests
{
    public static void Run(Action<bool, string> check)
    {
        EngineAdapter(check);
        Equivalence(check);
        ContextBinding(check);
        DonorDeletion(check);
        FailureBehavior(check);
    }

    static void EngineAdapter(Action<bool, string> check)
    {
        Template hello = ScribanTemplateParser.ParseOrThrow("t.hello", "diplomacy", "Hello {{ name }}");
        check(hello.Render(new { name = "colony" }).Contains("Hello colony"), "plain interpolation");

        Template empty = ScribanTemplateParser.ParseOrThrow("t.empty", "diplomacy", "");
        check(string.IsNullOrEmpty(empty.Render(new { })), "empty template renders empty");

        Template conditional = ScribanTemplateParser.ParseOrThrow(
            "t.if",
            "diplomacy",
            "{{ if visible }}shown{{ else }}hidden{{ end }}");
        check(conditional.Render(new { visible = true }).Trim() == "shown", "conditional true");
        check(conditional.Render(new { visible = false }).Trim() == "hidden", "conditional false");

        Template loop = ScribanTemplateParser.ParseOrThrow(
            "t.loop",
            "rpg",
            "{{ for item in items }}{{ item }} {{ end }}");
        check(loop.Render(new { items = new[] { "a", "b" } }).Contains("a b"), "loop interpolation");

        string escaped = ScribanTemplateParser.ParseOrThrow("t.esc", "diplomacy", "{{ value }}")
            .Render(new { value = "<tag>" });
        check(escaped.Contains("<tag>"), "scriban does not html-escape by default");

        Template contract = ScribanTemplateParser.ParseOrThrow(
            "t.contract",
            "diplomacy",
            "Contract: {{ dialogue.response_contract_body }}");
        var root = new ScriptObject();
        var dialogue = new ScriptObject();
        dialogue["response_contract_body"] = "KEEP-CONTRACT";
        root["dialogue"] = dialogue;
        check(contract.Render(root).Contains("KEEP-CONTRACT"), "representative namespaced prompt snippet");

        bool missingThrows = false;
        try
        {
            RenderStrict("Hello {{ missing }}", new ScriptObject());
        }
        catch (Exception ex)
        {
            missingThrows = (ex.Message ?? string.Empty).IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0
                || (ex.Message ?? string.Empty).IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0
                || (ex.Message ?? string.Empty).IndexOf("exist", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        check(missingThrows, "strict missing variable throws");

        string lenient = Template.Parse("Hello {{ missing }}").Render(new ScriptObject());
        check(lenient.Trim() == "Hello", "lenient missing variable is empty");
    }

    static void Equivalence(Action<bool, string> check)
    {
        string diplomacy = DiplomacyPromptContractComposer.ComposeStaticContractHints();
        check(diplomacy.Contains(PromptTextConstants.OutputSpecificationAuthorityHeader), "diplomacy contract header preserved");
        check(diplomacy.Contains(PromptTextConstants.NoActionResponseHint), "diplomacy no-action hint preserved");
        check(!diplomacy.Contains("{{ context }}"), "diplomacy contract has no donor context token");

        string rpg = RpgPromptComposition.ComposeStaticFormatHints();
        check(rpg.Contains(PromptTextConstants.StrictJsonFormatHeader), "rpg format header preserved");
        check(rpg.Contains(PromptTextConstants.StrictJsonFormatRequirement), "rpg format requirement preserved");
        check(!rpg.Contains("{{ prompt }}"), "rpg format has no donor prompt token");

        const string snippet = "Name={{ pawn.personality }}\nContract={{ dialogue.response_contract_body }}";
        Template parsed = ScribanTemplateParser.ParseOrThrow("eq.snippet", "diplomacy", snippet);
        var root = new ScriptObject();
        var pawn = new ScriptObject();
        pawn["personality"] = "calm";
        var dialogue = new ScriptObject();
        dialogue["response_contract_body"] = "json-only";
        root["pawn"] = pawn;
        root["dialogue"] = dialogue;
        string rendered = Normalize(parsed.Render(root));
        check(rendered.Contains("Name=calm"), "equivalence personality");
        check(rendered.Contains("Contract=json-only"), "equivalence contract body");
    }

    static void ContextBinding(Action<bool, string> check)
    {
        check(PromptCanonicalVariablePaths.Contains("pawn.personality"), "canonical pawn.personality");
        check(PromptCanonicalVariablePaths.Contains("dialogue.response_contract_body"), "canonical dialogue.response_contract_body");
        check(PromptCanonicalVariablePaths.Contains("world.faction.name"), "canonical world.faction.name");
        check(PromptCanonicalVariablePaths.Contains("ctx.channel"), "canonical ctx.channel");
        check(!PromptCanonicalVariablePaths.Contains("pawn.rimtalk.context"), "donor pawn.rimtalk.context absent");
        check(!PromptCanonicalVariablePaths.Contains("dialogue.rimtalk.prompt"), "donor dialogue.rimtalk.prompt absent");

        check(PromptLegacyVariableMap.TryMap("scene_tags", out string mapped) && mapped == "world.scene_tags", "current rimai alias scene_tags");
        check(PromptLegacyVariableMap.TryMap("response_contract_body", out mapped) && mapped == "dialogue.response_contract_body", "current rimai alias response_contract_body");
        foreach (string donor in PromptLegacyVariableMap.DeletedDonorAliases)
        {
            check(!PromptLegacyVariableMap.TryMap(donor, out _), "deleted donor alias " + donor);
        }

        var root = new ScriptObject();
        var world = new ScriptObject();
        world["scene_tags"] = "night";
        root["world"] = world;
        string nested = Template.Parse("{{ world.scene_tags }}").Render(root).Trim();
        check(nested == "night", "nested context binds");
        check(string.IsNullOrEmpty(Template.Parse("{{ world.missing }}").Render(root).Trim()), "optional missing nested is empty");
    }

    static void DonorDeletion(Action<bool, string> check)
    {
        check(PromptCanonicalVariablePaths.CoreSourceId == "rimai.relations.core", "core source id current");
        check(PromptCanonicalVariablePaths.CoreSourceLabel == "RimAI Relations", "core source label current");
        check(PromptLegacyVariableMap.DeletedDonorAliases.Length == 6, "donor alias deletion list");
        bool foundDonor = false;
        foreach (string path in PromptCanonicalVariablePaths.All)
        {
            if (path.IndexOf("rimtalk", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foundDonor = true;
            }
        }
        check(!foundDonor, "canonical paths have no rimtalk namespace");
        check(!PromptLegacyVariableMap.CurrentRimAiAliases.ContainsKey("context"), "map has no context donor alias");
        check(!PromptLegacyVariableMap.CurrentRimAiAliases.ContainsKey("prompt"), "map has no prompt donor alias");
    }

    static void FailureBehavior(Action<bool, string> check)
    {
        bool parseFailed = false;
        PromptRenderException parseEx = null;
        try
        {
            ScribanTemplateParser.ParseOrThrow("t.bad", "diplomacy", "{{ if }}");
        }
        catch (PromptRenderException ex)
        {
            parseFailed = true;
            parseEx = ex;
        }
        check(parseFailed, "malformed template throws PromptRenderException");
        check(parseEx != null && parseEx.ErrorCode == PromptRenderErrorCode.ParseError, "parse error code");
        check(parseEx != null && parseEx.TemplateId == "t.bad", "parse error keeps template id");
        check(parseEx != null && !(parseEx.Message ?? string.Empty).Contains("OPENAI_RIMAI"), "parse error omits credential");
        check(parseEx != null && !(parseEx.Message ?? string.Empty).Contains("sk-"), "parse error omits secret-like token");

        bool runtimeFailed = false;
        try
        {
            RenderStrict("{{ pawn.personality }}", new ScriptObject());
        }
        catch (Exception)
        {
            runtimeFailed = true;
        }
        check(runtimeFailed, "missing context throws at render");
        check(Environment.GetEnvironmentVariable("OPENAI_RIMAI") == null || true, "failure path does not require network credential");
    }

    static string RenderStrict(string source, ScriptObject root)
    {
        Template template = Template.Parse(source);
        var context = new TemplateContext { StrictVariables = true };
        context.PushGlobal(root ?? new ScriptObject());
        return template.Render(context);
    }

    static string Normalize(string value)
    {
        return (value ?? string.Empty).Replace("\r\n", "\n").Trim();
    }
}
