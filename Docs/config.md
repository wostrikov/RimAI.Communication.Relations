# RimAI.Communication.Relations — configuration overview

Player-facing and developer configuration for Relations lives primarily in:

- `Source/Config/RelationsSettings.cs` and related config types
- Settings UI under `Source/UI/Settings/`
- Prompt template catalogs / persistence under `Source/Config/` and `Source/Persistence/`

## Principles

- Prefer RimWorld keyed localization for player-visible labels; do not hardcode new UI copy in C#.
- Prompt/template text that is meant to be customized belongs in configuration/template resources, not ad-hoc source literals when a catalog already owns it.
- Provider credentials follow RimAI credential domains (`OPENAI_RIMAI` for gameplay). Do not fall back across credential domains.

## Compatibility notes

Legacy PackageId / serialization / bridge field names from absorbed products may still appear. Those identities are contracts; changing them is a shared-contract change, not documentation cleanup.

For a current action list, see [help_en.md](help_en.md).
