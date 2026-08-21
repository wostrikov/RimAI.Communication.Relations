using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;

namespace Ustas.RimAI.Communication.Relations.UI;

internal static class RelationsOpenAiImageSettingsUi
{
    internal static void DrawNative(Listing_Standard listing, DiplomacyImageApiConfig config, RelationsSettingsPages pages)
    {
        listing.Label("RimAI.Relations.ImageApi.Credentials".Translate(OpenAIProviderAdapter.CredentialDisplay));

        listing.Label("RimChat_ImageApiModel".Translate());
        Rect modelRect = listing.GetRect(24f);
        bool recommended = string.Equals(config.Model, DiplomacyOpenAiImageContract.RecommendedModel, StringComparison.OrdinalIgnoreCase);
        string modelLabel = recommended
            ? "RimAI.Relations.ImageApi.ModelRecommended".Translate()
            : "RimAI.Relations.ImageApi.ModelCustom".Translate();
        if (Widgets.ButtonText(modelRect, modelLabel))
        {
            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
            {
                new FloatMenuOption("RimAI.Relations.ImageApi.ModelRecommended".Translate(), () =>
                {
                    config.Model = DiplomacyOpenAiImageContract.RecommendedModel;
                }),
                new FloatMenuOption("RimAI.Relations.ImageApi.ModelCustom".Translate(), () =>
                {
                    if (string.Equals(config.Model, DiplomacyOpenAiImageContract.RecommendedModel, StringComparison.OrdinalIgnoreCase))
                    {
                        config.Model = string.Empty;
                    }
                })
            }));
        }

        if (!recommended)
        {
            config.Model = pages.ProviderCloud.DrawTextFieldWithPlaceholder(listing.GetRect(26f), config.Model ?? string.Empty, DiplomacyOpenAiImageContract.RecommendedModel);
        }

        listing.Label("RimAI.Relations.ImageApi.Size".Translate());
        Rect sizeRect = listing.GetRect(24f);
        if (Widgets.ButtonText(sizeRect, SizeLabel(config.DefaultSize)))
        {
            var options = new List<FloatMenuOption>();
            for (int i = 0; i < DiplomacyOpenAiImageContract.SizePresets.Length; i++)
            {
                string preset = DiplomacyOpenAiImageContract.SizePresets[i];
                options.Add(new FloatMenuOption(SizeLabel(preset), () => config.DefaultSize = preset));
            }

            options.Add(new FloatMenuOption("RimAI.Relations.ImageApi.SizeCustom".Translate(), () =>
            {
                if (!DiplomacyOpenAiImageContract.IsSizePreset(config.DefaultSize))
                {
                    return;
                }

                config.DefaultSize = string.Empty;
            }));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        if (!DiplomacyOpenAiImageContract.IsSizePreset(config.DefaultSize))
        {
            config.DefaultSize = pages.ProviderCloud.DrawTextFieldWithPlaceholder(listing.GetRect(26f), config.DefaultSize ?? string.Empty, "2048x1152");
            if (!string.IsNullOrWhiteSpace(config.DefaultSize) &&
                !DiplomacyOpenAiImageContract.TryNormalizeSize(config.DefaultSize, out _))
            {
                GUI.color = Color.red;
                Text.Font = GameFont.Tiny;
                listing.Label("RimAI.Relations.ImageApi.SizeInvalid".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
        }

        DrawChoice(listing, "RimAI.Relations.ImageApi.Quality", QualityLabel(config.Quality), new[]
        {
            Tuple.Create("auto", "RimAI.Relations.ImageApi.QualityAuto"),
            Tuple.Create("low", "RimAI.Relations.ImageApi.QualityLow"),
            Tuple.Create("medium", "RimAI.Relations.ImageApi.QualityMedium"),
            Tuple.Create("high", "RimAI.Relations.ImageApi.QualityHigh")
        }, value => config.Quality = value);

        DrawChoice(listing, "RimAI.Relations.ImageApi.OutputFormat", FormatLabel(config.OutputFormat), new[]
        {
            Tuple.Create(DiplomacyOpenAiImageContract.FormatPng, "RimAI.Relations.ImageApi.FormatPng"),
            Tuple.Create(DiplomacyOpenAiImageContract.FormatJpeg, "RimAI.Relations.ImageApi.FormatJpeg"),
            Tuple.Create(DiplomacyOpenAiImageContract.FormatWebp, "RimAI.Relations.ImageApi.FormatWebp")
        }, value =>
        {
            config.OutputFormat = value;
            config.Background = DiplomacyOpenAiImageContract.NormalizeBackground(config.Background, config.OutputFormat);
        });

        DrawChoice(listing, "RimAI.Relations.ImageApi.Background", BackgroundLabel(config.Background), new[]
        {
            Tuple.Create(DiplomacyOpenAiImageContract.BackgroundAuto, "RimAI.Relations.ImageApi.BackgroundAuto"),
            Tuple.Create(DiplomacyOpenAiImageContract.BackgroundOpaque, "RimAI.Relations.ImageApi.BackgroundOpaque"),
            Tuple.Create(DiplomacyOpenAiImageContract.BackgroundTransparent, "RimAI.Relations.ImageApi.BackgroundTransparent")
        }, value => config.Background = DiplomacyOpenAiImageContract.NormalizeBackground(value, config.OutputFormat));
    }

    internal static string ProbeOutcomeKey(OpenAiImageProbeOutcome outcome)
    {
        switch (outcome)
        {
            case OpenAiImageProbeOutcome.Success:
                return "RimChat_ConnectionSuccess";
            case OpenAiImageProbeOutcome.MissingCredential:
                return "RimAI.Relations.ImageApi.ErrorMissingCredential";
            case OpenAiImageProbeOutcome.Unauthorized:
                return "RimAI.Relations.ImageApi.ErrorUnauthorized";
            case OpenAiImageProbeOutcome.ModelUnavailable:
                return "RimAI.Relations.ImageApi.ErrorModelUnavailable";
            case OpenAiImageProbeOutcome.RateLimited:
                return "RimAI.Relations.ImageApi.ErrorRateLimited";
            case OpenAiImageProbeOutcome.ModerationBlocked:
                return "RimAI.Relations.ImageApi.ErrorModeration";
            default:
                return "RimAI.Relations.ImageApi.ErrorTransport";
        }
    }

    static void DrawChoice(Listing_Standard listing, string labelKey, string currentLabel, Tuple<string, string>[] options, Action<string> onSelected)
    {
        listing.Label(labelKey.Translate());
        Rect rect = listing.GetRect(24f);
        if (!Widgets.ButtonText(rect, currentLabel))
        {
            return;
        }

        var menu = new List<FloatMenuOption>();
        for (int i = 0; i < options.Length; i++)
        {
            string value = options[i].Item1;
            string optionKey = options[i].Item2;
            menu.Add(new FloatMenuOption(optionKey.Translate(), () => onSelected(value)));
        }

        Find.WindowStack.Add(new FloatMenu(menu));
    }

    static string SizeLabel(string size)
    {
        if (string.Equals(size, DiplomacyOpenAiImageContract.SizeAuto, StringComparison.OrdinalIgnoreCase))
        {
            return "RimAI.Relations.ImageApi.SizeAuto".Translate();
        }

        if (!DiplomacyOpenAiImageContract.IsSizePreset(size))
        {
            return "RimAI.Relations.ImageApi.SizeCustom".Translate();
        }

        return size.Replace("x", " × ");
    }

    static string QualityLabel(string quality)
    {
        switch (DiplomacyOpenAiImageContract.NormalizeQuality(quality))
        {
            case "low":
                return "RimAI.Relations.ImageApi.QualityLow".Translate();
            case "medium":
                return "RimAI.Relations.ImageApi.QualityMedium".Translate();
            case "high":
                return "RimAI.Relations.ImageApi.QualityHigh".Translate();
            default:
                return "RimAI.Relations.ImageApi.QualityAuto".Translate();
        }
    }

    static string FormatLabel(string format)
    {
        switch (DiplomacyOpenAiImageContract.NormalizeOutputFormat(format))
        {
            case DiplomacyOpenAiImageContract.FormatJpeg:
                return "RimAI.Relations.ImageApi.FormatJpeg".Translate();
            case DiplomacyOpenAiImageContract.FormatWebp:
                return "RimAI.Relations.ImageApi.FormatWebp".Translate();
            default:
                return "RimAI.Relations.ImageApi.FormatPng".Translate();
        }
    }

    static string BackgroundLabel(string background)
    {
        switch (DiplomacyOpenAiImageContract.NormalizeBackground(background, DiplomacyOpenAiImageContract.FormatPng))
        {
            case DiplomacyOpenAiImageContract.BackgroundOpaque:
                return "RimAI.Relations.ImageApi.BackgroundOpaque".Translate();
            case DiplomacyOpenAiImageContract.BackgroundTransparent:
                return "RimAI.Relations.ImageApi.BackgroundTransparent".Translate();
            default:
                return "RimAI.Relations.ImageApi.BackgroundAuto".Translate();
        }
    }
}
