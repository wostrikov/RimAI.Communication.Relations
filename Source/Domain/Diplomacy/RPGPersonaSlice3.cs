using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Relations.Context;

using PersonaPronouns = Ustas.RimAI.Communication.Relations.DiplomacySystem.RPGManagerPersonaBootstrap.PersonaPronouns;
using PendingPersonaGenerationContext = Ustas.RimAI.Communication.Relations.DiplomacySystem.RPGManagerPersonaBootstrap.PendingPersonaGenerationContext;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
    internal sealed class RPGPersonaSlice3 : RPGManagerPersonaBootstrapCollaborator
    {
        internal RPGPersonaSlice3(RPGManagerPersonaBootstrap owner) : base(owner)
        {
        }

internal static string BuildPersonaBootstrapPrompt(RpgPromptDefaultsConfig defaults, PersonaPronouns pronouns, string profile)
        {
            string template = RPGManagerPersonaBootstrap.RenderPersonaBootstrapTemplate(defaults?.PersonaBootstrapOutputTemplate, pronouns);
            string userTemplate = defaults?.PersonaBootstrapUserPromptTemplate;
            if (string.IsNullOrWhiteSpace(userTemplate))
            {
                return profile ?? string.Empty;
            }

            const string templateId = "prompt_templates.persona_bootstrap.user";
            PromptRenderContext context = RPGManagerPersonaBootstrap.BuildPersonaBootstrapRenderContext(templateId, pronouns);
            context.SetValue("dialogue.template_line", template);
            context.SetValue("dialogue.example_line", defaults?.PersonaBootstrapExample ?? string.Empty);
            context.SetValue("pawn.profile", profile ?? string.Empty);
            return PromptTemplateRenderer.RenderOrThrow(templateId, "rpg", userTemplate, context);
        }

internal static string RenderPersonaBootstrapTemplate(string template, PersonaPronouns pronouns)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            const string templateId = "prompt_templates.persona_bootstrap.output";
            PromptRenderContext context = RPGManagerPersonaBootstrap.BuildPersonaBootstrapRenderContext(templateId, pronouns);
            return PromptTemplateRenderer.RenderOrThrow(templateId, "rpg", template, context);
        }

internal static PromptRenderContext BuildPersonaBootstrapRenderContext(string templateId, PersonaPronouns pronouns)
        {
            PromptRenderContext context = PromptRenderContext.Create(templateId, "rpg");
            context.SetValue("pawn.pronouns.subject", pronouns.Subject);
            context.SetValue("pawn.pronouns.subject_lower", pronouns.SubjectLower);
            context.SetValue("pawn.pronouns.be_verb", pronouns.BeVerb);
            context.SetValue("pawn.pronouns.object", pronouns.Objective);
            context.SetValue("pawn.pronouns.possessive", pronouns.Possessive);
            context.SetValue("pawn.pronouns.seek_verb", pronouns.SeekVerb);
            return context;
        }

internal static string BuildCoreTemperament(Pawn pawn)
        {
            List<string> traits = pawn?.story?.traits?.allTraits?
                .Select(t => t?.Label)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Take(2)
                .Select(v => v.ToLowerInvariant())
                .ToList();
            if (traits != null && traits.Count > 0)
            {
                return string.Join(" and ", traits);
            }

            int social = pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            if (social >= 8)
            {
                return "calm and perceptive";
            }

            return "practical and cautious";
        }

internal static string BuildEmotionalPattern(Pawn pawn)
        {
            int social = pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            if (social >= 10)
            {
                return "keep emotions measured and carefully filtered";
            }

            if (social >= 5)
            {
                return "stay polite while keeping feelings under control";
            }

            return "keep feelings guarded and close to the chest";
        }

internal static string BuildBehavioralStrategy(Pawn pawn)
        {
            int intellectual = pawn?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
            int social = pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            int combat = Math.Max(
                pawn?.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0,
                pawn?.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0);
            if (intellectual >= 8)
            {
                return "careful observation and planning";
            }

            if (social >= 8)
            {
                return "reading people first and responding with tact";
            }

            if (combat >= 8)
            {
                return "disciplined action and steady pressure";
            }

            return "steady routines and deliberate choices";
        }

internal static string BuildCoreMotivation(Pawn pawn)
        {
            int intellectual = pawn?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
            int social = pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            if (intellectual >= 8)
            {
                return "clarity and control";
            }

            if (social >= 8)
            {
                return "stable trust and mutual understanding";
            }

            return "security and dependable bonds";
        }

internal static string BuildDefenseWeakness(Pawn pawn)
        {
            int social = pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            if (social >= 8)
            {
                return "hard to read and slow to lower defenses";
            }

            return "distant and slow to trust others";
        }

internal static string BuildPersonalityCost(Pawn pawn)
        {
            int social = pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            if (social >= 8)
            {
                return "missed chances for deeper closeness";
            }

            return "emotional distance in close relationships";
        }

internal static bool StartsWithVowelSound(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            char c = char.ToLowerInvariant(text[0]);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }

internal void CompleteNpcPersonaBootstrap()
        {
            npcPersonaBootstrapCompleted = true;
            npcPersonaBootstrapVersion = CurrentNpcPersonaBootstrapVersion;
            Owner.ResetNpcPersonaBootstrapRuntimeState();
            ModuleLog.Message("[RimAI.Relations] Existing NPC persona bootstrap completed.");
        }

internal bool ShouldRunNpcPersonaBootstrap()
        {
            if (npcPersonaBootstrapVersion < CurrentNpcPersonaBootstrapVersion)
            {
                npcPersonaBootstrapCompleted = false;
            }

            return !npcPersonaBootstrapCompleted;
        }

internal void ResetNpcPersonaBootstrapRuntimeState()
        {
            npcPersonaBootstrapTargets.Clear();
            npcPersonaPendingRequests.Clear();
            npcPersonaPendingThingIds.Clear();
            cachedNpcPersonaTargets = null;
            npcPersonaTargetsCacheTick = 0;
            nextPersonaBootstrapTick = 0;
            nextPersonaRuntimeScanTick = 0;
            npcPersonaRuntimeScanDisabledNoRimTalk = false;
        }
    }
}
