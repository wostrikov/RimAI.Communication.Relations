# RimChat Diplomacy Action Reference

This document summarizes **all diplomacy actions that can currently be triggered** in the RimChat communication flow.

Notes:

- “Triggerable” does not mean an action will always succeed. Most actions are still constrained by **relations, cooldowns, online state, feature toggles, runtime eligibility checks, and parameter completeness**.
- If the AI promises a behavior but the action does not actually trigger, that is not necessarily a code bug. It may be a hallucinated model output. If an action repeatedly fails to trigger, the player should describe the intent in a command / instruction tone, or directly use the action name.
- Except for quests, there are no valid cross-map behaviors that require map-to-map execution.
- There are no valid delayed promise semantics such as “I promise I will send it to you tomorrow”; treat that as false if no actual action is triggered.
- Some actions have limited parameters. For example, quests cannot freely customize rewards and content; only limited quest types are supported. Caravans cannot specify exact goods.
- Prefer using the action menu when sending information. Compared with freeform natural-language negotiation, action triggering is more stable.
- The AI cannot recognize items carried by your caravan / expedition / rocket airdrop inventory.

***

## I. Relations and stance

### 1. adjust_goodwill

- **Purpose**: adjust the target faction’s goodwill toward the player.
- **Main parameters**:
  - `amount` (int, required): goodwill change value.
  - `reason` (string, optional): the reason for the adjustment.
- **Requirements / limits**:
  - This is a direct diplomatic relation-change action.
  - It is limited by per-faction cooldown.
  - The code requires `amount` to parse correctly; missing or invalid values fail.
- **Good fit for**:
  - Situations where the player clearly shows goodwill, hostility, betrayal, help, insult, and the outcome should land as an actual numeric relation change. The AI may adjust goodwill when appropriate, but the action is not guaranteed to trigger every time.

### 2. declare_war

- **Purpose**: declare war and move the faction into a hostile / wartime relationship with the player.
- **Main parameters**:
  - `reason` (string, optional / recommended): the reason for the declaration.
- **Requirements / limits**:
  - Only used when relations have deteriorated enough to justify war.
- **Good fit for**:
  - Repeated threats, insults, extortion, or clear escalation into hostility.

### 3. make_peace

- **Purpose**: offer peace and attempt to end a hostile relationship.
- **Main parameters**:
  - `cost` (int, required): peace payment in silver; this enters a player confirmation payment flow.
- **Requirements / limits**:
  - Can only be used while the faction is already **at war / hostile**.
  - By design, it should only be used when the player’s sincerity is high enough.
  - The action does **not** take effect immediately after the AI calls it; it first opens a confirmation dialog.
  - If the player confirms, the system deducts the required silver from **tradeable stock / powered orbital trade beacon coverage**, then formally makes peace.
  - If the player cancels, no silver is deducted, relations do not change, and the peace cooldown is not consumed.
- **Good fit for**:
  - Cases where the player actively seeks peace, offers compensation, or otherwise creates a high-sincerity peace window.

***

## II. Support, visits, and trade

### 4. request_aid

- **Purpose**: request aid.
- **Main parameters**:
  - `type` (string, required): `Military` / `Medical` / `Resources`.
- **Requirements / limits**:
  - Can only be requested from **allied factions**.
  - The corresponding goodwill threshold must be met.
  - Has a per-faction cooldown.
  - The executor maps `type` to different aid cost categories.
  - You cannot specify reinforcement count, equipment, or similar extra details.
- **Good fit for**:
  - Explicit requests for military support, medical support, or resource support.

### 5. request_caravan

- **Purpose**: request a trade caravan.
- **Main parameters**:
  - `goods` (string, optional): the desired caravan / goods direction.
    - `General` = general trader
    - `BulkGoods` = bulk goods trader
    - `CombatSupplier` = combat supplier
    - `Exotic` = exotic goods trader
    - `Slaver` = slaver caravan
- **Requirements / limits**:
  - Can only be used while relations are **not hostile**.
  - Has a per-faction cooldown.
  - You cannot specify caravan size, exact goods, or similar extra details.
- **Good fit for**:
  - Asking a faction to send a caravan, supply team, or trade party.

### 6. request_visitor

- **Purpose**: request a visitor group.
- **Main parameters**:
  - No required parameters.
- **Requirements / limits**:
  - Can only be used while relations are **not hostile**.
  - Has a per-faction cooldown.
  - You cannot specify visitor count or similar extra details.
- **Good fit for**:
  - When the player wants envoys, delegates, or visitors sent to the colony.

### 7. send_gift

- **Purpose**: send a silver gift to the target faction and improve relations.
- **Main parameters**:
  - `silver` (int, required): the amount of silver in the gift.
  - `goodwill_gain` (int, optional): the expected goodwill gain.
- **Requirements / limits**:
  - The current implementation only allows **silver** and does not support other gift items.
  - The action does **not** execute immediately after the AI calls it; it first opens a confirmation dialog.
  - If the player confirms, the system deducts the required silver from **tradeable stock / powered orbital trade beacon coverage**, then sends it as a gift and applies the goodwill gain.
  - If the player cancels, no silver is deducted, no goodwill is gained, and the gift cooldown is not consumed.
  - If there is not enough valid silver in stock / beacon coverage, the confirmation submit step fails.
- **Good fit for**:
  - Friendly outreach, rewards, diplomatic gifts, and goodwill-building gestures.

***

## III. Raids

### 8. request_raid

- **Purpose**: launch a raid against the player.
- **Main parameters**:
  - `strategy` (string): such as `ImmediateAttack`, `ImmediateAttackSmart`, `StageThenAttack`, `ImmediateAttackSappers`, `Siege`.
  - `arrival` (string): such as `EdgeWalkIn`, `EdgeDrop`, `EdgeWalkInGroups`, `RandomDrop`, `CenterDrop`.
- **Requirements / limits**:
  - Can only be used while the faction is **hostile**.
  - Has a per-faction cooldown.
  - The parameters are normalized before execution.
- **Good fit for**:
  - The player actively provokes, requests open war, or the hostile faction chooses to escalate conflict.

### 9. request_raid_call_everyone

- **Purpose**: launch a cross-faction coordinated joint assault.
- **Main parameters**:
  - None.
- **Requirements / limits**:
  - This is a high-intensity action and is not just an alias of `request_raid`.
  - It is limited by **global cooldown** and runtime eligibility checks.
  - The system attempts to organize multiple related factions into the attack.
- **Good fit for**:
  - The player explicitly says things like “call everyone”, “joint raid”, “all in”, or equivalent commands.
  - May cause participation from multiple factions.

### 10. request_raid_waves

- **Purpose**: launch sustained multi-wave raids.
- **Main parameters**:
  - `waves` (int, required, 2-6): number of raid waves.
- **Requirements / limits**:
  - Values outside 2-6 fail.
  - Has a per-faction cooldown.
  - Limited by runtime eligibility checks.
- **Good fit for**:
  - The player explicitly asks for repeated waves, sustained pressure, or a multi-round challenge.

***

## IV. Quests and contracts

### 11. create_quest

- **Purpose**: create a quest / contract.
- **Main parameters**:
  - `questDefName` (string, required): the quest template Def name.
  - `askerFaction` (string, optional): defaults to the current faction.
  - `points` (int, optional): quest threat points.
- **Requirements / limits**:
  - Must use a **valid questDefName from the currently injected approved list**.
  - Custom no-template quests are forbidden.
  - Execution is validated by `ApiActionEligibilityService` before it runs.
  - In some contexts (for example orbital trade communication), certain ground-fulfillment quest types are forbidden.
  - Detailed quest parameters cannot be freely customized; only limited quest types are supported.
- **Good fit for**:
  - When a faction wants to issue a formal quest, contract, bounty, or support request to the player.

### 12. request_info

- **Purpose**: request missing information required before execution.
- **Main parameters**:
  - `info_type` (string, required): currently only `prisoner` is supported.
- **Requirements / limits**:
  - This is **not executed directly by `AIActionExecutor`**; it is handled by the **diplomacy dialogue-specific pipeline**.
  - It is mainly used in prisoner ransom flows when a valid prisoner target ID is still missing.
- **Good fit for**:
  - When the player wants to negotiate ransom but the system still lacks a specific prisoner target.

### 13. pay_prisoner_ransom

- **Purpose**: submit a one-time prisoner ransom payment.
- **Main parameters**:
  - `target_pawn_load_id` (int, required): target prisoner ID.
  - `offer_silver` (int > 0, required): silver offer.
  - `payment_mode` (string, optional): if provided, only `silver` is supported.
- **Requirements / limits**:
  - Can only be used for prisoners that belong to the current faction and are still held by the player.
  - Offers reference the current valid offer range; out-of-range values are clamped to the nearest valid boundary before execution.
  - If the natural-language reply already claims the ransom was paid / submitted, the same reply must include this action.
  - No alternate payment mode is supported; only `silver`.
  - If the prisoner is not released within 12 hours after payment, penalties apply.
  - If the prisoner’s health is worse at release than during negotiation, penalties apply.
  - If core organs are missing at release compared with the negotiation state, penalties apply.
- **Good fit for**:
  - The final payment submission stage of a prisoner ransom negotiation.

***

## V. Airdrops and social-circle actions

### 14. request_item_airdrop

- **Purpose**: send goods instantly through the orbital / airdrop pipeline.
- **Main parameters**:
  - `need` (string, required): the player’s requested need; if the player specified a quantity, that quantity must be preserved.
  - `payment_items` (array, required): the list of payment items.
  - `budget_silver` (int, optional, audit only; used to prevent unreasonable orders).
- **Requirements / limits**:
  - `need` and `payment_items` are required.
  - The payment items must be truly removable from powered orbital trade beacon coverage.
  - Only one-item-for-one-item trade is supported per request.
- **Good fit for**:
  - Orbital trade, diplomatic supply drops, and instant item exchange.

### 15. publish_public_post

- **Purpose**: publish a public post or public stance to the social sphere / public opinion layer.
- **Main parameters**:
  - Specific parameters are controlled by the social-circle pipeline and the current prompt constraints.
- **Requirements / limits**:
  - The player can explicitly ask the AI to publish a public post.
- **Good fit for**:
  - Public declarations, condemnations, formal statements, or broadcasting a political stance.

***

## VI. Session and online-state actions

### 16. exit_dialogue

- **Purpose**: end the current dialogue while keeping the faction online.
- **Main parameters**:
  - `reason` (string, optional)
- **Requirements / limits**:
  - Controlled by the faction online-state feature toggle.
  - This is a session-control action, not a normal diplomacy-effect action.
- **Good fit for**:
  - Natural topic closure or a polite wrap-up.

### 17. go_offline

- **Purpose**: end the dialogue and switch the faction to offline state.
- **Main parameters**:
  - `reason` (string, optional)
- **Requirements / limits**:
  - Controlled by the faction online-state feature toggle.
- **Good fit for**:
  - When the other side decides to go offline and stop responding for now.

### 18. set_dnd

- **Purpose**: switch to do-not-disturb and stop message exchanges.
- **Main parameters**:
  - `reason` (string, optional)
- **Requirements / limits**:
  - Controlled by the faction online-state feature toggle.
- **Good fit for**:
  - Clear refusal to chat, bad mood, or a need for temporary silence.

### 19. reject_request

- **Purpose**: explicitly mark the current request as rejected.
- **Main parameters**:
  - `reason` (string, recommended)
- **Requirements / limits**:
  - Always allowed.
  - Used for **formal rejection** of a specific request that should be recorded as rejected.
  - Ordinary disagreement, delay, or vague refusal does not necessarily need this action.
- **Good fit for**:
  - When the player makes a clear request and the faction wants to issue a formal rejection result.
