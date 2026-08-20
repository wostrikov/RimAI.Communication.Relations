# RimAI.Communication.Relations — API / ownership

## Ownership

- Product: `RimAI.Communication.Relations`
- PackageId: `ustas.rimai.communication.relations`
- Composition owner: `IRimAiModuleComposition` implementation in this module
- Host boundary: RimWorld/Harmony patches under `Source/RimWorld/` stay thin; domain logic lives under `Source/Domain/`, `Source/Application/`, `Source/AI/`

## Primary surfaces

- Diplomacy sessions and AI action execution (`Source/Domain/Diplomacy/`, `Source/AI/`)
- RPG pawn dialogue (`Source/Domain/Rpg/`, dialogue UI under `Source/UI/`)
- Prompt templates and settings (`Source/Config/`, `Source/Prompting/`, settings UI)
- Persistence / serialization for prompt and dialogue state (`Source/Persistence/`, domain memory types)

## Contracts to preserve

- Action JSON names and Scribe field identities are runtime contracts — do not rename casually.
- Shared Text-AI admission uses the Core request arbiter (Relations priority: `PlayerBlocking` / `Background` per Phase 7 mapping).
- Runtime-visible UI, prompt, and log strings are localization/product debt when they contain CJK or donor branding; they are not comment-cleanup targets.

## Further reading

- Module structural convention: repository `docs/architecture/rimai-module-structural-convention.md`
- Action semantics for players/authors: [help_en.md](help_en.md)
