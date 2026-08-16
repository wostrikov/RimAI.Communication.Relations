# RimChat AI API Документ (v0.9.1024)

## Уніфікація формулювань для вимкнення й затримки відновлення завдань запиту на нагородження (v0.9.1024)

- `RimChat.DiplomacySystem.ApiActionEligibilityService`
  - `SupportedQuestDefs` вилучити `BestowingCeremony`; цей шаблон більше не входить до набору доступних завдань `GetFactionQuestAvailabilityReport(...)`.
  - `ValidateCreateQuest(...)` додати примусове блокування шаблонів нагородження:
    - У разі спрацювання `questDefName == "BestowingCeremony"` негайно повертати `bestowing_disabled`.
    - Одночасно записувати стабільний якір журналу: `[RimAI.Relations][QuestGuard] blocked create_quest for disabled template ...` (містить faction/questDefName/code).
  - Семантика: це блокування діє лише на RimChat у ланцюжку дій `create_quest` і не змінює вбудовану систему завдань гри.

- `RimChat.UI.Dialog_DiplomacyDialogue.ActionHint`
  - `FormatCooldownReason(...)` уніфіковано перевести на форматування часу, що залишився в грі (повторно використовуючи `FormatGameTimeCooldownReason`).
  - Охопити всі підказки дипломатичних `*_cooldown`, зокрема `quest_cooldown`, `aid_cooldown`, `caravan_cooldown` тощо.
  - Семантика: змінюється лише шар тексту підказок, а зберігання та логіка перевірки затримки відновлення не змінюються (на нижньому рівні все ще керується `TicksGame`).

## Резервний варіант за відсутності `/models` та інтерфейс прямої видачі структурованого оригінального тексту (v0.9.1023)

- `RimChat.Config.ApiUsabilityDiagnosticService.RunCloudDiagnosticCoroutine(...)`
  - Додати перевірку коду стану відсутності `/models`: лише `404/405/501` вважати «кінцеву точку не знайдено, але можна перейти до перевірки через chat».
  - Після виявлення коду відсутності збігу:
    - Пропустити жорстку перевірку списку моделей для `ModelAvailability`.
    - Продовжуйте виконання `ChatProbe -> ResponseContractValidation`.
    - У разі успішного chat загальна перевірка доступності вважається успішною, а в технічні деталі записується warning (`models_endpoint_missing_http=...; fallback_to_chat_probe=true`).
  - Коди, що не є відсутніми (наприклад, `429/5xx/网络异常`), мають зберігати fail-fast; не дозволяти їх проходження.

- `RimChat.Config.ApiUsabilityDiagnosticService.ProbeLocalServiceCoroutine(...)`
  - OpenAI Перевірка сумісності локального зонда триває; `/v1/models` повертає `404/405/501`, що більше не вважається прямою ознакою недоступності локальної служби.
  - Нова поведінка: продовжувати переходити до chat для перевірки; відповідь від chat слугує остаточним критерієм доступності.

- `RimChat.AI.AIChatServiceAsync.ProcessRequestCoroutine(...)`
  - Після вичерпання повторних спроб структурованої оболонки вилучити ін’єкцію фіксованої резервної фрази занурення (вихід зі шляху `RimChat_ImmersionFallback_*`).
  - Нова поведінка: безпосередньо передавати необроблений видимий текст моделі (raw passthrough) як вивід `parsedResponse`.
  - Ця гілка додає нову мітку журналу: `Dialogue envelope raw passthrough used after retry`, щоб відрізняти її від старої резервної поведінки.

- `RimChat.Persistence.PromptPersistenceService` / `PromptPersistenceService.Hierarchical`
  - На початку розділу контракту відповіді додайте уніфікований текст із жорсткими обмеженнями:
    - `### 格式要求`
    - «Остаточна відповідь МАЄ бути JSON і відповідати цьому формату»
    - `visible_dialogue` JSON демонстраційний блок.
  - RPG набуває чинності узгоджено з дипломатичним ланцюжком і відображається в ланцюжку попереднього перегляду Prompt Workbench (WYSIWYG).

## AI інтерфейс гарячого виправлення для наскрізного обмеження викупних пропозицій, що виходять за межі (v0.9.1022)

- RimChat.DiplomacySystem.GameAIInterface.PreparePrisonerRansom(...)
  - Перед виконанням викупу для однієї цілі додано єдину точку нормалізації: на основі актуального стану переговорів обчислюється діапазон пропозиції, а пропозиції, що виходять за межі offer_silver, затискаються до найближчої межі.
  - Уточнення семантики:
    - OfferedSilver: AI початкова пропозиція
    - AcceptedSilver: фактична пропозиція для виконання (можливо, нормалізована)
  - Додано журнал нормалізації на етапі prepare: target/original/window/normalized/current_ask, щоб полегшити Player.log відтворення та перевірку.

- RimChat.DiplomacySystem.PrisonerRansomResultData
  - Додано поля відповіді:
    - OfferWindowMinSilver
    - OfferWindowMaxSilver
  - Дані результату можуть одночасно містити початкову пропозицію, пропозицію для виконання та межі діапазону, що забезпечує узгоджене відображення для UI і ланцюжка налагодження.

- RimChat.UI.Dialog_DiplomacyDialogue.AppendRansomSuccessSystemMessage(...)
  - Якщо paid_submitted і OfferedSilver != AcceptedSilver, додано системне повідомлення «Пропозицію нормалізовано відповідно до діапазону».
  - Зберегти наявне повідомлення про успіх без змін; нове повідомлення показувати лише в разі нормалізації.

- Ключі багатомовності
  - Додано:
    - RimChat_RansomOfferNormalizedSystem（1.6/Languages/ChineseSimplified/Keyed/RimChat_Keys.xml）
    - RimChat_RansomOfferNormalizedSystem（1.6/Languages/English/Keyed/RimChat_Keys.xml）
  - Заборонено жорстко вбудовувати текст UI; необхідно зберігати узгодженість мовної системи.

## Виправлення сумісності визначення діапазону викупу та соціальних новин（v0.9.1019）

- `RimChat.Persistence.PromptPersistenceService.BuildUnifiedChannelSystemPrompt(...)`
  - Канал `social_circle_post` більше не викидає `RimTalkPromptRenderCompatibilityException` і не перериває процес, якщо сумісність нативного рендерингу не вдалася.
  - Нова поведінка: після запису діагностичного журналу помилки сумісності продовжити виконання запиту, використовуючи структурований текст рендерингу.

- `RimChat.Memory.FactionDialogueSession`
  - Додано інтерфейс знімка викупу для однієї цілі:
    - `SetPendingRansomOfferReference(int targetPawnLoadId, int currentAskSilver, int minOfferSilver, int maxOfferSilver)`
    - `ClearPendingRansomOfferReference()`
    - `TryGetPendingRansomOfferReference(out int targetPawnLoadId, out int currentAskSilver, out int minOfferSilver, out int maxOfferSilver)`
    - `TryBuildPendingRansomOfferReference(out string referenceBlock)`
  - До блоку цитування діалогу додано: `[RansomOfferReference]`, поле `target_pawn_load_id/current_ask_silver/offer_window_min_silver/offer_window_max_silver/requirement`.

- `RimChat.UI.Dialog_DiplomacyDialogue`
  - `BuildAiUserMessage(...)` тепер стабільно додає `[RansomOfferReference]` (якщо є знімок).

- `RimChat.UI.Dialog_DiplomacyDialogue.PrisonerRansomSelection`
  - `TryHandlePrisonerRansomActionWithSelection(...)` додано нормалізацію перед виконанням для однієї цілі: якщо `offer_silver` виходить за межі, його затискають до найближчої межі вікна, а потім виконують.
  - Ланцюжок вибору персонажа/скасування/масового перемикання підтримує життєвий цикл знімка однієї цілі, запобігаючи забрудненню між процесами.

- `RimChat.UI.Dialog_DiplomacyDialogue.PrisonerRansomBatchRuntime`
  - Оновлено поведінку `BuildBatchRansomExecutionPlan(...)`: якщо загальна масова ціна виходить за межі, її більше не відхиляють одразу, а автоматично нормалізують.
  - Правила нормалізації:
    - Спочатку загальну ціну затискають до `[total_offer_window_min_silver, total_offer_window_max_silver]`.
    - Потім масштабують до кожної цілі пропорційно початковій пропозиції.
    - Для цілочислового коригування використовують «розподіл спочатку за дробовим залишком», щоб забезпечити точну суму.
    - Якщо межа менша за кількість цілей і через це неможливо виконати умову «кожна ціль >=1», зберігається fail-fast.

- Синхронізація контракту
  - Семантику контракту `pay_prisoner_ransom` звужено з «без верхньої та нижньої межі» до «орієнтуватися на системне вікно; нормалізувати перед виконанням, якщо значення виходить за межі».
  - Семантику пакетних контрактів змінено з «відхилення через вихід за межі» на «автоматичне приведення загальної ціни до норми через вихід за межі та запис назад у параметри дії».
## Інтерфейс кешу знімків активних дипломатичних/соціальних новин (v0.9.1018)

- `RimChat.Persistence.DiplomacyPromptRuntimeSnapshot`
  - Модель знімка виконання лише для читання, що інкапсулює спільні для активної дипломатії та соціальних новин високовитратні динамічні текстові блоки:
    - `EnvironmentPromptBlock`
    - `MemoryDataBlock`
    - `FactionInfoBlock`
    - `PlayerPawnProfileBlock`
    - `PlayerRoyaltySummaryBlock`
    - `FactionSettlementSummaryBlock`
  - Поля метаданих: `MemoryRevision / WorldEventRevision / PlayerGoodwill / PlayerRelationKind / PromptFilesStampUtcTicks / SettingsSignature`.

- `RimChat.Persistence.IDiplomacyPromptSnapshotCache`
  - `WarmupOnLoad()`: ініціалізує чергу та запускає цільовий набір прогрівання після завантаження збереження/початку гри.
  - `TryGetSnapshot(Faction faction, out DiplomacyPromptRuntimeSnapshot snapshot)`: шлях запиту отримує дані лише для читання; у разі промаху синхронний перерахунок не виконується.
  - `Invalidate(Faction faction = null, string reason = "manual")`: анулювання за фракцією або глобальне.
  - `RequestWarmup(Faction faction, string reason = "request")`: гілка запиту активно додає завдання прогрівання.
  - `Tick(int currentTick, int maxBuildsPerTick = 1)`: точка входу для побудови бюджету покадрової обробки (у поточній реалізації за замовчуванням 1 за tick).

- `RimChat.Persistence.PromptPersistenceService`
  - `BuildFullSystemPrompt(...)` додано перевантаження `DiplomacyPromptRuntimeSnapshot runtimeSnapshot`.
  - `BuildUnifiedChannelSystemPrompt(...)` додано необов’язковий параметр `runtimeSnapshot`, що повторно використовує динамічні блоки знімка в межах потоку.
  - `BuildRuntimeSnapshotForFaction(...)`: перерахунок пам’яті/поселення/переговірника/інформації про довкілля зосереджено на шляху прогрівання.

- Обмеження викликів
  - Повідомлення активних фракцій (`GameComponent_NpcDialoguePushManager`) і соціальні новини (`SocialNewsPromptBuilder + NewsRequests`) повинні спочатку спробувати отримати знімок.
  - Якщо знімок не готовий, слід уніфіковано виконати відкладену повторну спробу за принципом fail-fast; заборонено виконувати повне резервне синхронне сканування в кадрі запиту.

## Кореневе виправлення продуктивності Tick (RPGManager) (v0.9.1016)

- `RimChat.Core.ModDependencyProbe`
  - Додано `IsLoaded(string token)`: кешоване визначення доступності залежностей модів (після першого сканування результат береться з кешу) для зменшення витрат на часто виконуваних шляхах під час роботи.
- `RimChat.DiplomacySystem.GameComponent_RPGManager.PersonaBootstrap`
  - `ProcessNpcPersonaRuntimeTick()`: спочатку виконується перевірка обмеження Tick, а потім визначення залежностей RimTalk, що зменшує фіксовані витрати на кожен Tick.
  - `ProcessNpcPersonaBootstrapTick()`: перед ініціалізацією черги bootstrap додано перевірку доступності RimTalk за принципом fail-fast.
  - `IsRimTalkLoadedForPersonaBlock()`: тепер уніфіковано використовується `ModDependencyProbe.IsLoaded("rimtalk")`.
  - `TrySyncPawnPersonaFromRimTalk(...)` / `TryCopyPawnPersonaFromRimTalk(...)`：
    - Збережено оригінальний підпис (для сумісності з кодом, що викликає).
    - Додано внутрішнє перевантаження (із передаванням уже розібраного шаблону), яке дає змогу повторно використовувати шаблон за один раунд під час сканування в реальному часі, сканування черги bootstrap і ручної повної синхронізації, уникаючи повторного запуску ланцюжка нормалізації конфігурації.
- Сумісність
  - Зовнішні інтерфейси не порушено; наявні сторінка налаштувань і точки виклику під час роботи не потребують змін.

## Система єдиного множника ринкової ціни для повітряних поставок（v0.9.88）

- `RimChat.DiplomacySystem.ItemAirdropTradePolicy`
  - Необхідні для повітряної поставки ресурси оцінюються за розподілом `tradeTags`: якщо містять `ExoticMisc`, використовується `ThingDef.BaseMarketValue x3.0`, для решти — `ThingDef.BaseMarketValue x1.8`.
  - Ресурси, що використовуються для оплати повітряної поставки, оцінюються за `ThingDef.BaseMarketValue x0.6`.
  - Винятки для `Silver` та `Gold`: зберігається `ThingDef.BaseMarketValue x1.0`.
  - Усунути залежність повітряних поставок від `TradeUtility.GetPricePlayerBuy/GetPricePlayerSell` і контексту торгівлі з караванами.
- `RimChat.DiplomacySystem.GameAIInterface.ItemAirdrop.Barter`
  - Розрахунок бюджету для `BuildPaymentPlan(...)` тепер повністю використовує правила множника ринкової ціни на боці оплати; спеціальну логіку x10 / x2 для `tradeTags` більше не зберігати.
- `RimChat.UI.Dialog_ItemAirdropTradeCard`
  - Картки потреб, оплати та даних надсилання мають однаково відображати новий підхід до множника ринкової ціни.
- `RimChat.Memory.FactionDialogueSession`
  - Прихований контекст котирувань, що надається AI, також потрібно синхронізувати з новими правилами множника, щоб UI / AI / фактична угода збігалися.

## Повернення ціноутворення повітряних поставок до системи ринкових цін（v0.9.87）

- `RimChat.DiplomacySystem.GameAIInterface.ItemAirdrop`
  - Для `PrepareItemAirdropCandidates(...)` вилучити шлях перевизначення торгової ціни купівлі; ціна кандидата має повертатися до `ThingDefRecord.MarketValue` (мінімальне значення `0.01`).
- `RimChat.DiplomacySystem.GameAIInterface.ItemAirdrop.Barter`
  - `BuildPaymentPlan(...)` Зберігати похідне обчислення бюджету за «ринковою ціною + чинними правилами множників (без tradeTags x10、ExoticMisc x2)», більше не залежати від торгового контексту.
  - `ResolveAirdropPaymentUnitPrice(...)` Код помилки для відсутнього def змінити на семантику ринкової ціни `market_value_def_missing`.
- `RimChat.UI.Dialog_ItemAirdropTradeCard`
  - Еталонну ціну з боку потреб змінити на безпосереднє використання ринкової ціни; більше не аналізуватиувати ціну купівлі в торгівлі.
- Синхронізація контракту промпту
  - `request_item_airdrop` Опис бюджету змінити на «похідне обчислення через Floor після підсумовування за ринковою ціною (з чинними множниками); `budget_silver` — лише аудит».

## Уніфікація правил торгівлі для повітряного десанту та усунення розбіжностей у трактуванні торгових цін（v0.9.87）

- `RimChat.DiplomacySystem.ItemAirdropTradePolicy`
  - Додати єдину точку входу правил `ResolveRuleSnapshot(Faction)`, що виводить `AirdropTradeRuleSnapshot`:
    - `TradersGuild + Ally`: `shipping=150`, `tradeLimit=12000`
    - `TradersGuild + 非 Ally`: `shipping=200`, `tradeLimit=800`
    - `非 TradersGuild + Ally`: `shipping=200`, `tradeLimit=8000`
    - Інші фракції: `shipping=250`, `tradeLimit=max(500, 500 + floor(goodwill/5)*300)`
  - Додатитекстування ціни купівлі в торгівлі `TryResolvePlayerBuyPrice(...)`, що вимагає наявності дійсного гравецького перемовника та торгового контексту (караван/орбітальний торговець).
- `RimChat.DiplomacySystem.GameAIInterface.ItemAirdrop.Barter`
  - `BuildPaymentPlan(...)` Змінено на розрахунок бюджету `payment_items` за ціною купівлі в торгівлі, більше не використовується `MarketValue/BaseMarketValue`.
  - `PrepareItemAirdropTradeForMap(...)` Додано fail-fast для ліміту транзакцій: коли `paymentTotalSilver > tradeLimit`, повертається `trade_limit_exceeded`.
- `RimChat.DiplomacySystem.GameAIInterface.ItemAirdrop`
  - `RequestItemAirdrop(...)` Тепер спочатку визначає доступного переговорника гравця; якщо його немає, виконується fail-fast `player_negotiator_required`.
  - Ціну пакетів-кандидатів уніфіковано шляхом підстановки ціни купівлі в торгівлі, `max_legal_count` і кандидат `unit` використовують єдину основу ціноутворення.
- `RimChat.Persistence.PromptPersistenceService`
  - `AppendAirdropTradeRules(...)` Змінено на читання уніфікованого знімка правил і підстановку динамічного тексту вартості перевезення/лімітів.
  - Текст контракту `request_item_airdrop` оновлено на «Бюджет виводиться з ціни купівлі в торгівлі (з урахуванням впливу соціальних навичок)».

## Google API Виправлення кореневої проблеми завантаження моделей і перевірки конфігурації (v0.9.79)

- `RimChat.Config.RimChatSettings`
  - `ParseGoogleModelsFromResponse(string json)`
    - Розбір списку моделей Google змінено на пріоритет typed JSON , а якщо результат порожній — виконується резервне сканування JSON у `name` полях.
    - Виведення й надалі уніфіковано як модель без префікса «`models/`», що відповідає контракту поля «ID» у запиті чату, сумісному з OpenAI.`model`.
  - `TestConnectionSync()`
    - Тест швидкого з'єднання більше не повторно використовує визначення «конфігурації, придатної для чату» з `ApiConfig.IsValid()`.
    - Швидке хмарне зондування тепер вимагає лише: наявної ввімкненої конфігурації + заповненого API Key; вибір моделі тепер виконує ланцюжок глибокої перевірки доступності.
  - `ResolvePrimaryCloudConfigForConnectivity()` / `TryValidateCloudConfigForConnectivity(...)`
    - Стати єдиною точкою входу до конфігурації для кнопки швидкої перевірки зв’язку на сторінці налаштувань API.
- `RimChat.Config.ApiUsabilityDiagnosticService`
  - `ValidateCloudConfig(...)`
    - На етапі перевірки конфігурації повертати локалізований докладний текст, якщо відсутній API Key або модель.
    - Для провайдера Google зберігається семантика «Base URL може бути порожнім, використовується вбудована endpoint провайдера»; додаткової обов’язкової вимоги URL не вводиться.
- `RimChat.Config.RimChatSettings_ApiUsability`
  - `BuildUsabilitySummaryText(...)`
    - Якщо помилка виникла на етапі `ConfigValidation`, до підсумку безпосередньо додається точний текст помилки конфігурації, щоб зменшити оманливість, коли «підсумок узагальнений, а деталі сховані в технічних відомостях».

## Основний протокол структурованого діалогу та вихід основного ланцюжка міркувань (v0.9.71)

- `RimChat.Dialogue.DialogueResponseEnvelope`
  - Уніфікувати об’єкт результату діалогу stage-A.
  - Основними полями стають `VisibleDialogue`, `ActionsJson`, `ProtocolKind`, `FailureReason`, `IsValid`.
  - `DialogueText` зберігається як сумісний аксесор для `VisibleDialogue`.
- `RimChat.Dialogue.DialogueResponseEnvelopeParser`
  - `Parse(string response, DialogueUsageChannel usageChannel)`
    - Структурована точка входу для розбору основного протоколу.
    - Пріоритетно приймає верхньорівневий JSON: `visible_dialogue` + необов’язковий `actions/meta/debug`.
    - Старий формат одного текстового поля використовується лише як перехідний адаптер введення; подальша розширена сумісність із винятками не підтримується.
- `RimChat.AI.AIChatServiceAsync`
  - Додано перевірку envelope за принципом fail-fast для `DiplomacyDialogue / RpgDialogue / NpcPush / PawnRpgPush`.
  - `ShouldUseStructuredDialogueEnvelope(...)`
    - Структурований основний протокол вмикається лише в ланцюжку справжнього діалогу й не впливає на `StrategySuggestion / SocialNews` та інші недіалогові JSON канали.
- `RimChat.AI.AIResponseParser`
  - Додано `ParseResponse(DialogueResponseEnvelope envelope, Faction faction)`.
  - Дипломатичний канал із envelope `VisibleDialogue / ActionsJson` побудовано `ParsedResponse` і більше не можна вільно вирізати сирий текст.
- `RimChat.AI.ModelOutputSanitizer`
  - Додано `SplitVisibleAndTrailingActions(...)`
  - Додано `ComposeVisibleAndTrailingActions(...)`
  - Стає єдиним джерелом істини для розділення «репліки/дії JSON».
- `RimChat.AI.ImmersionOutputGuard`
  - Додано тип порушення `ReasoningLeakage`.
  - Додано `ValidateVisibleDialogueParts(...)`, перевіряється лише структурований `visible_dialogue`.
- `RimChat.AI.TextIntegrityGuard`
  - Додано `ValidateVisibleDialogueParts(...)`, перевіряється лише структурований `visible_dialogue`.
- `RimChat.AI.DiplomacyResponseContractGuard`
  - Додано `ValidateVisibleDialogueParts(...)`.
  - Чітко визначено, що «зобов’язання виконати дію у видимій репліці» має збігатися з `actions` у тому самому раунді.
- `RimChat.AI.RpgResponseContractGuard`
  - Додано `ValidateVisibleDialogueParts(...)`.
  - Чітко визначено перевірку однорядкового `visible_dialogue`, даних-заповнювачів дії та контракту виводу RPG.

## Високоточне перероблення панелі історії дипломатії (v0.9.70)

- `RimChat.UI.Dialog_DiplomacyHistory`
  - Перебудовано на структуру з однією панеллю; перемикання подання `当前派系 / 玩家总历史` більше не передбачено.
  - Історію показано як «поточний сеанс + групи історичних сеансів».
  - Взаємодію з рядками змінено: одинарне клацання — вибір, подвійне — редагування, після вибору праворуч відображається символ видалення.
- `RimChat.Memory.LeaderMemoryManager`
  - `GetDialogueHistorySessionGroups(Faction faction)`
    - Повертає історію групування сеансів поточної фракції, що містить активний сеанс і збережені постійні сеанси після сегментації.
  - `TryUpdateDialogueHistoryRow(Faction faction, DiplomacyHistoryRow row, string newMessage, out string error)`
    - Одночасно записує назад поточний `FactionDialogueSession.messages` і постійний `DialogueHistory`.
  - `TryDeleteDialogueHistoryRow(Faction faction, DiplomacyHistoryRow row, out string error)`
    - Одночасно видаляє відповідні записи поточного `FactionDialogueSession.messages` і постійного `DialogueHistory`.

## Вікно керування історією дипломатії（v0.9.69）

- `RimChat.UI.Dialog_DiplomacyDialogue`
  - `DrawDialogueMainTabs(...)`
    - У верхній панелі action-tab додано `RimChat_DialogueMainTabHistory`; натискання відкриває окреме вікно історії.
- `RimChat.UI.Dialog_DiplomacyHistory`
  - Надає два подання `当前派系 / 玩家总历史`.
  - `玩家总历史` лише узагальнює дані й не створює нової постійної таблиці.
- `RimChat.Memory.LeaderMemoryManager`
  - `GetDialogueHistoryRows(Faction faction)`
    - Зчитує `DialogueHistory` поточної фракції та перетворює його на рядкову модель UI.
  - `GetAggregatedDialogueHistoryRows()`
    - Об’єднати всі фракції, що не належать гравцеві, `DialogueHistory`, і повернути їх у порядку спадання за `GameTick`.
  - `TryUpdateDialogueHistoryMessage(string factionId, int recordIndex, string newMessage, out string error)`
    - Оновити лише цільовий `DialogueRecord.Message`, після чого негайно нормалізувати та зберегти.
  - `TryDeleteDialogueHistoryRecord(string factionId, int recordIndex, out string error)`
    - Видалити один запис `DialogueHistory`, після чого негайно нормалізувати та зберегти.
- Обмеження
  - Цього разу керування історією дозволяє лише `DialogueHistory`.
  - Не дозволяється редагувати `IsPlayer`, `GameTick`, належність до фракції, знімок відносин, важливі події, дипломатичний підсумок і підсумок RPG.

## Вхід до дипломатичної відправки перейменовано на «Швидка дія / Actions» (v0.9.67)‌

- `RimChat_SendInfoEntry`
  - Китайське відображуване ім’я змінено з `+发送信息` на `快速行动`.
  - Англійське відображуване ім’я змінено з `+Send Info` на `Actions`.
  - Змінюється лише видимий текст UI, поведінка входу та подальший ланцюжок дій не змінюються.

## Повне виправлення сумісності предметів mod для торгівлі через десантний контейнер (v0.9.66)‌

- `RimChat.DiplomacySystem.ThingDefCatalog`
  - `GetRecords()`
    - Розширити правила додавання предметів mod-кандидатів до пулу: додавати def із чіткою ознакою «можна торгувати / можна використовувати / наявний дійсний сигнал категорії item», більше не покладаючись надмірно на метадані в стилі оригінальної гри.
  - `TryGetRecordByDefName(string defName, out ThingDefRecord record)`
    - Для прив’язки торгової картки додано direct def fallback; якщо запис відсутній у кешованому catalog, для легального item def усе одно можна створити запис.
  - `GetTradeablePaymentRecords()`
    - Надано глобальне представлення всіх доступних для торгівлі def для розбору платежів, щоб відрізняти «предмет неможливо розібрати» від «його можна розібрати глобально, але на поточному маяку його немає в наявності».
- `RimChat.DiplomacySystem.ItemAirdropSafetyPolicy`
  - `IsResourceCandidate(ThingDefRecord record)`
    - Визначення ресурсів перейшло від єдиної комбінації `stuffProps / 价值 / tradeability` до стабільного визначення за кількома сигналами: сильний сигнал ресурсу, структурований сигнал торгівлі та сигнал ресурсу з метаданих.
- `RimChat.DiplomacySystem.GameAIInterface.ItemAirdrop.Barter`
  - `BuildPaymentPlan(...)`
    - Розбір `payment_items.item` тепер спочатку обмежується фактичними запасами «поточного орбітального маяка».
    - Якщо предмет можна однозначно розібрати серед глобальних defs, доступних для торгівлі, але на поточному маяку його немає в наявності, повертається `payment_item_insufficient`, а не неоднозначний `payment_item_unresolved`.
  - `TryResolvePaymentThingDef(...)`
    - Тепер приймається набір записів-кандидатів, а діапазон розбору визначає викликач, щоб схожі модові предмети поза запасами не помилково визначалися як поточна ціль платежу.
- `RimChat.DiplomacySystem.GameAIInterface.ItemAirdrop.BoundNeed`
  - `TryResolveBoundNeedInfo(...)`
    - Прив’язку торгової картки `need_def` змінено на «спочатку пошук у catalog, потім direct fallback»; сувора прив’язка зберігається, непомітна заміна товару не дозволяється.

## `+发送信息` Вхід для провокації / запиту каравану (v0.9.64)

- `RimChat.UI.Dialog_DiplomacyDialogue`
  - `OpenSendInfoMenu()`
    - Додано входи гравця `Taunt` і `Request Caravan`.
  - `BuildChatMessages(...)`
    - Якщо останнє повідомлення сеансу є системним повідомленням, тотожним поточному тексту драйвера, пропустіть цей запис історії, щоб уникнути повторної ін’єкції системного запиту драйвера.
- `RimChat.UI.Dialog_DiplomacyDialogue.SendInfoActions`
  - Додано окреме вікно «Провокація» з 3 варіантами:
    - Звичайна атака
    - Безперервна атака
    - Спільна атака
  - Для спільної атаки додано повторне підтвердження.
  - Після надсилання дія не прив’язується примусово; натомість записується системне повідомлення та запускається наявний ланцюжок відповіді й аналізу дій AI.
- `RimChat.DiplomacySystem.ApiActionEligibilityService`
  - Видалено перевірку projected goodwill floor для `request_caravan`, `request_aid`, `create_quest`.
  - Збережено фактичні бізнес-обмеження: відносини, затримку відновлення, шаблони завдань тощо.
- `RimChat.Persistence.PromptPersistenceService`
  - Скасовано дзеркальне приховування projected goodwill floor на рівні промпту, щоб видимість у промпті відповідала перевіркам під час виконання.
- Зовнішній інтерфейс/локалізація
  - Додано ключі китайською та англійською:
    - Пункт меню надсилання повідомлення
    - Заголовок вікна провокації та описи 3 варіантів
    - Вікно підтвердження спільного рейду
    - Шаблони системних повідомлень «Провокація» та «Запросити караван»

## Fail-fast для сумісності нативного рендерингу соціального кола (v0.9.58)

- `RimChat.Prompting.RimTalkNativeRpgPromptRenderer`
  - `TryRenderWithNativeScriban(...)`
    - Прив’язку нативного `ScribanParser.Render` змінено на виявлення кількох сигнатур із кешуванням знайденого результату.
    - Якщо сумісний підпис не виявлено, повертається структурована помилка сумісності (успішний рендеринг більше не імітується).
  - `RimTalkNativeRenderDiagnostic`
    - Додано `BoundMethodVariant`, `IsCompatibilityFailure`, `FailureStage` для визначення точки збою прив’язки в різних середовищах.
- `RimChat.Persistence.PromptPersistenceService`
  - `BuildUnifiedChannelSystemPrompt(...)`
    - Для каналу `social_circle_post` увімкнено fail-fast: у разі помилки сумісності нативного рендерингу негайно викидається `RimTalkPromptRenderCompatibilityException`, що блокує постановку запиту в чергу.
- `RimChat.DiplomacySystem.GameComponent_DiplomacyManager`
  - `TryQueueNewsSeed(...)`
    - Перехоплюється виняток сумісності та зберігається стан помилки; запит AI більше не надсилається.
  - `OnSocialNewsRequestSuccess(...)`
    - До журналу помилок parse додано структуроване поле `requestId/debugSource/stage/response_preview`.
- `RimChat.AI.AIChatServiceAsync`
  - `ProcessRequestCoroutine(...)`
    - Для точного розподілу `AIRequestDebugSource.SocialNews`: пропускати Guard дипломатичного діалогу (повторне занурення/обробка контракту), зберігаючи суворий ланцюжок виводу JSON.
- Зовнішні інтерфейси/конфігурація
  - Нових public API немає.
  - Нових користувацьких параметрів конфігурації немає.
  - Додано ключ причини невдачі соціального кола: `RimChat_SocialFailureReason_prompt_render_incompatible` (кит./англ.).

## Оптимізація гарячого шляху піктограми перемикача Comms (v0.9.57)

- `RimChat.Patches.PlaySettingsPatch_CommsToggleIcon`
  - `Postfix(WidgetRow row, bool worldView)`
    - Спочатку виконати перевірку fail-fast, потім у межах одного виклику кешувати `WindowStack` і посилання на вже відкрите вікно.
    - Малювання піктограми та застосування стану використовують один і той самий результат перевірки вікна, щоб уникнути повторного сканування вікон у тому самому кадрі.
  - `DrawToggleButton(...)`
    - Перейти на отримання вже визначеного стану, більше не зчитувати стек вікон повторно всередині.
  - `ApplyToggleAndPersist(...)`
    - Перейти на отримання `WindowStack` і вже відкритого вікна, переданих стороною виклику, зберігаючи семантику одноразового застосування.
  - `GetToggleTooltip(bool enabled)`
    - Додано легкий кеш tooltip, який перебудовує текст `Translate(...)` лише зі зміною стану перемикача.
- Зміни зовнішніх інтерфейсів
  - Немає нових public API.
  - Змін у параметрах конфігурації немає.
  - Змін у структурі збережень немає.

## Кореневе виправлення fail-fast для ланцюжка розбору（v0.9.52）

- `RimChat.AI.AIJsonContentExtractor`
  - Тип повернення `TryExtractPrimaryText(string json)` розширено з булевого значення + `out string` до `PrimaryTextExtractionResult`.
  - Поля `PrimaryTextExtractionResult`:
    - `IsSuccess`: чи успішно виконано вилучення
    - `Content`: видимий текст після вилучення
    - `ReasonTag`: мітка причини невдачі/успіху (наприклад, `ok`, `empty_primary_text`, `no_extractable_text`)
    - `MatchedPath`: шляхтекст, який спрацював (наприклад, `content[].text`)
  - Додано можливість вилучення тексту з фрагментів `content[]`, що охоплює типові відповіді content-part локальних моделей.

- `RimChat.AI.AIChatServiceAsync`
  - Оновлено маршрутизацію помилоктекст:
    - Повторна спроба дозволена лише один раз і лише коли `ReasonTag=empty_primary_text`;
    - У разі будь-якої іншої помилки аналізу негайно виконати fail-fast і викликати зворотний виклик помилки локалізованого аналізу.
  - Після помилки аналізу більше не записувати фіксовану фразу `RimChat_ImmersionFallback_*` до історії сеансу.
  - До повідомлення повторної спроби додано поле `PARSE_MATCH_PATH`, щоб підказати моделі виправити формат виводу.

- `RimChat.Util.DebugLogger`
  - Додано `LogParseExtraction(string context, PrimaryTextExtractionResult result)` для виведення доказової інформації аналізу.

## Переговори про викуп кількох ув’язнених (v0.9.48)

- `RimChat.UI.Dialog_DiplomacyDialogue.PrisonerRansomSelection`
  - `Dialog_PrisonerRansomTargetSelector`
    - Вікно вибору ув’язнених змінено з вибору одного варіанта на вибір кількох; додано `全选/全不选/确认`, за замовчуванням нічого не вибрано.
  - `BuildRansomBatchExecutionPlan(...)`
    - Якщо сеанс містить пакетні посилання, а поточний вивід містить `pay_prisoner_ransom`, перед виконанням спершу виконати попередню перевірку fail-fast:
      - Кожна дія повинна мати `target_pawn_load_id` + `offer_silver`
      - Цільовий набір має повністю збігатися з вибраним набором (без пропусків/дублікатів/несанкціонованих елементів)
      - `offer_silver`Загальна сума має перебувати в межах загального діапазону партії
    - У разі помилки попередньої перевірки відхилити виконання всього пакета й повернути зрозумілу системну помилку.
  - `HandleBatchRansomPaymentSuccess(...)`
    - У пакетному режимі набір очікуваних платежів витрачається з кроком, що відповідає цілі; стан прив’язки викупу очищується лише після повного завершення пакета.
    - Під час послідовного виконання перша помилка перериває подальші дії цього циклу; успішно виконані дії не відкочуються.
- `RimChat.Memory.FactionDialogueSession`
  - Додано поля стану пакетного викупу:
    - `hasPendingRansomBatchSelection`
    - `pendingRansomBatchGroupId`
    - `pendingRansomBatchTargetPawnLoadIds`
    - `pendingRansomBatchTotalCurrentAskSilver`
    - `pendingRansomBatchTotalMinOfferSilver`
    - `pendingRansomBatchTotalMaxOfferSilver`
  - Додано метод:
    - `SetPendingRansomBatchSelection(...)`
    - `TryGetPendingRansomBatchSelection(...)`
    - `TryBuildPendingRansomBatchReference(...)`
    - `ConsumePendingRansomBatchTarget(...)`
    - `ClearPendingRansomBatchSelection()`
- `RimChat.DiplomacySystem.GameAIInterface.PrisonerRansom`
  - `PreparePrisonerRansom(...)` Додано зчитування полів пакетного контексту (`batch_group_id`/`batch_target_count`) і їх запис до даних попередньої обробки.
  - `BuildContract(...)` Додано збереження метаданих пакетного контракту:
    - `IsBatchRansom`
    - `BatchGroupId`
    - `BatchTargetCount`
  - Кінцевий термін звільнення за пакетним контрактом фіксовано збільшено до `1.5x` (помірне послаблення).
- `RimChat.DiplomacySystem.RansomContractManager`
  - Гілка штрафів за пакетним контрактом:
    - Звичайні штрафи за падіння ціни/прострочення масштабуються за `0.7x`
    - Підвищено пороги падіння ціни major/severe
  - Штраф за відсутність органа залишається на рівні для одного персонажа (коефіцієнт пакетного масштабування не застосовується).

## Кореневе виправлення регресії прямого відкриття після заміни комунікаційної панелі (v0.9.47)

- `RimChat.Patches.CommsConsolePatch`
  - `GetFloatMenuOptionsPostfix(...)`
    - Оновлено визначення перехоплення: більше не залежить від збігу оголошеного типу/назви збірки оригінальної дії.
    - Новий ланцюжок перевірки: `菜单项非空 -> action 非空 -> 可解析有效派系 -> 替换 action 为 RimChat 直开`.
    - Стандартизація причин пропуску fail-fast: `NullOption / NullAction / InvalidFaction`.
- Зміни зовнішнього інтерфейсу
  - Нових public API немає.
  - Змін конфігурації немає.
  - Змін структури збережень немає.

## Посилення активного дипломатичного обмеження та відсутність надсилання накопичених запитів після відновлення (v0.9.46)

- `RimChat.NpcDialogue.GameComponent_NpcDialoguePushManager`
  - `CancelQueuedTriggersForFaction(Faction faction, string reason = "manual")`
    - Повертає: `int` (фактичну кількість очищених записів).
    - Поведінка: використовується для очищення історичної активної черги фракції, зокрема під час відновлення онлайн-режиму; може містити причину в журналі.
  - Зчитування параметрів обмеження переведено на конфігурацію:
    - `NpcGlobalDeliveryCooldownHours` (типово `6`)
    - `NpcFactionCooldownMinDays` (типово `3`)
    - `NpcFactionCooldownMaxDays` (типово `7`)
    - `NpcQueueMaxPerFaction`（за замовчуванням `3`）
    - `NpcQueueExpireHours`（за замовчуванням `12`）
  - Оптимізація опитування кандидатів:
    - Додано кеш активних кандидатів, низькочастотну синхронізацію сесій і низькочастотне очищення; `EvaluateRegularTriggers(...)` більше не перебудовує повний набір кандидатів щоразу.
  - Журнал налагодження:
    - Додано журнали спрацьовування обмеження частоти під керуванням `EnableNpcPushThrottleDebugLog` (глобальна затримка відновлення/затримка відновлення фракції/очищення черги).

- `RimChat.DiplomacySystem.GameComponent_DiplomacyManager`
  - `ForcePresenceOnlineForNpcInitiated(Faction faction)`
    - Поведінка: якщо стан зазнає `Unavailable -> Online`, запускається очищення черги історії активних дипломатичних дій.
  - `RefreshPresenceOnDialogueOpen(Faction faction)`
    - Поведінка: під час відновлення на межі `Unavailable -> Online` також запускається очищення черги, щоб гарантувати: «після відновлення онлайн історичні тригери не надсилаються заднім числом».

- `RimChat.Config.RimChatSettings`
  - Додано поля, що зберігаються:
    - `NpcGlobalDeliveryCooldownHours`
    - `NpcFactionCooldownMinDays`
    - `NpcFactionCooldownMaxDays`
    - `EnableNpcPushThrottleDebugLog`
    - `NpcPushThrottleProfileVersion`
  - Стратегія міграції:
    - Під час першого завантаження старого збереження буде примусово виконано міграцію на стандартний рівень обмеження частоти (`6h / 3~7d / 3 / 12h`).

## Публікація дописів у соціальному колі гравцем вручну + примусова активна відповідь фракцій（v0.9.44）

- `RimChat.DiplomacySystem.GameComponent_DiplomacyManager`
  - `TryPublishManualPlayerSocialPost(string title, string body)`
    - Безпосередньо створює публічний допис із оригінальним текстом гравця, не проходячи через генерацію новин AI.
    - Перевіряє в режимі fail-fast, чи порожні заголовок або текст і чи не перевищено обмеження довжини.
    - Повертає `ManualSocialPostResult`:
      - `Success`
      - `PostId`
      - `TriggeredFactionCount`
      - `FailureReason`
  - `GetManualSocialPostFailureReasonLabel(ManualSocialPostFailureReason reason)`
    - Уніфіковано повертає локалізований текст причини помилки, призначений для UI.
- `RimChat.DiplomacySystem.SocialEnums`
  - Додано `SocialNewsOriginType.PlayerManual`
    - Позначає дописи в соціальному колі, опубліковані гравцем вручну, щоб відрізняти їх від новин, згенерованих AI.
  - Додано `ManualSocialPostFailureReason`
    - Причина помилки: `Disabled / MissingTitle / MissingBody / TitleTooLong / BodyTooLong / Unknown`.
  - Додано `ManualSocialPostResult`
    - Для читання результату публікації допису та фактичної кількості активованих фракцій через UI.
- `RimChat.NpcDialogue.GameComponent_NpcDialoguePushManager`
  - `manual_social_post` Користувацький контекст тригера буде вставлено на етапі генерації активного діалогу:
    - Заголовок допису
    - Текст допису
    - Пояснення контексту «це допис гравця у відкритому соціальному колі»
  - Мета — щоб активне повідомлення безпосередньо відповідало змісту допису, а не поверталося до звичайної невимушеної бесіди.
- Поведінка під час доставки
  - Ручні дописи не потрапляють до AI черги запитів новин.
  - Для ручних дописів більше не надсилається додатковий лист «світові новини соціального кола».
  - Активні відповіді відповідних фракцій і надалі використовують наявний ланцюжок `ChoiceLetter_NpcInitiatedDialogue` і запису сеансу.

## Повне видалення ексклюзивних звуків спільного рейду (v0.9.42)

- `RimChat.AI.AIActionExecutor`
  - `ExecuteRequestRaidCallEveryone(...)`
    - Після успішного планування спільного рейду ексклюзивний звуковий ефект більше не відтворюється; зберігається лише текстовий/системний відгук про успішне виконання дії.
- `1.6/Defs/SoundDefs/Diplomacy_Sounds.xml`
  - Видалено `RimChat_RequestRaidCallEveryone`; ексклюзивний `SoundDef` спільного рейду більше не зберігається.
- `build.ps1`
  - Видалити fail-fast перевірку аудіо під час збирання для `sound_request_raid_call_everyone`, більше не вимагати наявності цього ресурсу.
- Ресурс
  - Видалити `1.6/Sounds/sound_request_raid_call_everyone.wav`.

## Кореневе виправлення вимкнення завдань замовлень орбітального торговця (v0.9.42)

- `RimChat.DiplomacySystem.ApiActionEligibilityService`
  - Додати розпізнавання сеансу орбітального торговця: спочатку зчитувати явне поле контексту з параметрів дії, а за його відсутності повертатися до перевірки поточного `TradeShip` на мапі.
  - У контексті орбітального торговця `TradeRequest` змінити на блокування fail-fast, повертати `orbital_trader_trade_request_disabled` і використовувати уніфіковане локалізоване повідомлення: «Орбітальний торговець не може оформлювати замовлення на виконання в наземному поселенні; скористайтеся торгівлею з десантуванням».
  - Підключити `GetQuestEligibilityReport(...)` / `GetAvailableQuestDefsForFaction(...)` / `ValidateCreateQuest(...)` до єдиної перевірки, щоб список доступних завдань у промпті, перевірка виконання та повідомлення про помилку були узгодженими.
- `RimChat.Persistence.PromptPersistenceService`
  - Додати до `AppendDynamicQuestGuidance(...)` під час зв’язку з орбітальним торговцем спеціальний опис контексту та вилучити завдання замовлень на доставку до поселення зі списку доступних завдань.
  - Додати до `AppendQuestSelectionHardRules(...)` і `AppendOutputSpecificationAuthorityRules(...)` жорстке обмеження для орбітального торговця: заборонити обіцяти доставити визначені ресурси до наземного поселення для виконання замовлення; на такі запити можна лише пояснити обмеження та спрямувати до `request_item_airdrop`.
- `RimChat.AI.AIActionExecutor`
  - У разі помилки перевірки `create_quest` використовувати список доступних завдань із того самого контексту, щоб у сценарії з орбітальним торговцем повідомлення про помилку знову не додавало `TradeRequest` до allowed quest.
- `Prompt/Default/DiplomacyDialoguePrompt_Default.json`
  - Доповнити стандартний дипломатичний промпт правилами для орбітального торговця: у разі обміну конкретними ресурсами орбітальний торговець може лише спрямовувати до `request_item_airdrop` або виконувати його; заборонити створення замовлень/завдань на доставку, виконання яких потребує наземного поселення.
- Локалізація
  - Додано мовні ключі `RimChat_OrbitalTraderTradeRequestBlocked` для китайської та англійської мов, щоб використовувати підказки fail-fast у передньому кінці та уніфіковані тексти.

## Уточнення семантики каталогу дій спільного рейду + вилучення blocked зі стандартного шару підказок（v0.9.41）

- `Prompt/Default/DiplomacyDialoguePrompt_Default.json`
  - `request_raid_call_everyone`
    - Семантику дії змінено на «відкрито скликати всіх для спільного наступу», щоб чітко вказати: це спільний рейд між фракціями, а не псевдонім звичайного рейду.
    - Чітко зазначено, що `call everyone / 联合袭击 / 都叫来 / 全都叫来 / everyone attack / all in` — це команда, якою гравець безпосередньо просить розпочати спільний наступ.
  - `request_raid_waves`
    - Формулювання змінено на «багатохвильова атака з постійним тиском»; більше не описується як стандартна заміна, коли спільний рейд недоступний.
- `RimChat.Config.PromptTextConstants`
  - `RequestRaidCallEveryoneActionDescription`
  - `RequestRaidCallEveryoneActionRequirement`
  - `RequestRaidWavesActionDescription`
  - `RequestRaidWavesActionRequirement`
    - Уніфіковано стандартний JSON і тексти, що доповнюються під час виконання, щоб уникнути розбіжностей у семантиці каталогу дій.
- `RimChat.Persistence.PromptPersistenceService`
  - `AppendBlockedActionHints(...)`
    - На етапі складання промпту `call_everyone_requires_post_raid_escalation` приховується й більше не надається моделі як стандартна заблокована дія.
    - Жорсткі блокування, як-от глобальна затримка відновлення та відсутність доступних фракцій, і надалі потрапляють до blocked actions.
- Примітка щодо сумісності
  - Фактичні критерії доступності `ApiActionEligibilityService.ValidateRaidCallEveryoneAvailability(...)` не послаблено; цього разу змінено лише спосіб подання в промпті, а правила виконання не змінено.

## Доповнення опису дії спільного рейду + зіставлення псевдонімів + звук успішного виконання（v0.9.40）

- `RimChat.AI.AIResponseParser`
  - `NormalizeActionName(...)`
    - Додано нормалізоване зіставлення від `联合袭击 / 一起上 / 都叫来 / 全都叫来 / everyone_attack / all_in` до `request_raid_call_everyone`.
- `RimChat.AI.AIActionExecutor`
  - `ExecuteRequestRaidCallEveryone(...)`
    - Раніше після успішного `DiplomacyEventManager.ScheduleRaidCallEveryone(...)` відтворювався ексклюзивний звуковий ефект спільної атаки; весь ланцюжок цього звукового ефекту видалено в `v0.9.42`.
- `RimChat.Config.PromptTextConstants`
  - Доповнено пояснення дії `request_raid_call_everyone`, чітко визначено час використання, призначення та розмовні назви для гравців.
- `RimChat.Persistence.PromptPersistenceService.DomainStorage`
  - Під час доповнення каталогу дій у реальному часі одночасно записуються новий опис `request_raid_call_everyone` і пояснення коротких фраз викликів.

## Дедуплікація промптів + примусове впровадження раси + дворівневе стиснення подій（v0.9.39）

- `PromptPersistenceService.WorkbenchComposer.ComposePromptWorkspace(...)`
  - У реальному часі додано впровадження додаткових блоків `mandatory_race_profile` (diplomacy/rpg/proactive).
- `PromptPersistenceService.WorkbenchComposer.ValidateRuntimePromptComposition(...)`
  - Додано обов’язкову перевірку `mandatory_race_profile`; у разі відсутності `PromptRenderException` негайно завершує роботу з помилкою.
- `PromptPersistenceService.WorkbenchComposer.BuildPromptNodePlacementsForCompose(...)`
  - Дипломатичний канал за наявності `instruction_stack.faction_characteristics` пригнічує повторний вивід вузлів.`diplomacy_fallback_role`
- `PromptPersistenceService.TemplateVariables`
  - `world.environment_params` Вивід змінено на компактний текстовий знімок (із посиланням на `<environment>` як на авторитетний блок подробиць).
  - `world.recent_world_events` Вивід змінено на компактний підсумок, викликається `BuildRecentWorldEventIntelCompactDigest(...)`.
- `PromptPersistenceService.AppendRecentWorldEventIntel(...)`
  - Введення подій оновлено з «лише обрізання» до дворівневого виводу «оригінальний текст + EventDigest підсумок» (підсумок додається, якщо бюджет перевищено).

## Фіксоване введення розвідданих дипломатичного каналу та узгодження торговельних правил (v0.9.37)

- `PromptPersistenceService.AppendOutputSpecificationAuthorityRules(...)`
  - Під час роботи дипломатичного каналу примусово вводяться торговельні правила:
    - Миттєвий обмін ресурсами дозволено лише `request_item_airdrop`;
    - Одна операція з десантуванням дозволяє обміняти лише один вид на один вид (`need` відповідає одній групі `payment_items`);
    - Караван (`request_caravan`) — це відкладена торгівля; фракція не може контролювати остаточні види та кількість ресурсів, які він перевозить;
    - Якщо гравець точно влучає у відомий торговельний факт, дозволено поступитися в ціні/зробити знижку в межах собівартості.
- `PromptPersistenceService.Hierarchical.ResolveFactionPromptText(...)`
  - Після рендерингу промпту фракції завжди додається структурований блок `FIXED_FACTION_INTEL`, незалежно від вмісту шаблону робочої панелі.
  - Обсяг введення: `diplomacy_dialogue`, `proactive_diplomacy_dialogue`, `diplomacy_strategy`.
- `PromptPersistenceService.Hierarchical.BuildDiplomacyStrategySystemPromptHierarchical(...)`
  - До стратегічного каналу додано вузол `instruction_stack.faction_characteristics`, щоб стратегічний ланцюжок стабільно містив підказки фракції та фіксований блок розвідданих.
- `DiplomacyFactionFixedIntelBuilder.Build(...)`
  - Фіксовані поля:
    - `FactionDescription`
    - `FactionTechLevel`
    - `HasFactionCaravanDispatchedNow`
    - `HasFactionQuestPublishedNow`
    - `HasFactionRaidScheduledNow`
    - `HasPlayerExpeditionNow`
    - `FactionSettlementDestroyedHistory`
    - `FactionRaidImpactOnPlayerLatest`
    - `FactionRaidImpactOnPlayerTotal`
    - `FactionRaidCasualtiesLatest`
    - `FactionRaidCasualtiesTotal`
    - `PlayerFactionTechLevel`
- `GameComponent_DiplomacyManager.EventQueries`
  - Додано інтерфейс перевірки лише для читання:
    - `HasCaravanDispatchedNow(Faction faction)`
    - `HasRaidScheduledNow(Faction faction)`
- `GameAIInterface.QuestTracking`
  - Додано збереження відстеження публікації завдань RimChat:
    - `ExposeQuestPublicationData()`
    - `TryTrackCreateQuestResult(...)`
    - `HasActiveRimChatQuestForFaction(Faction faction)`
- `FactionIntelLedgerComponent`
  - Додано збереження історії знищення баз і реєстру збитків від руйнувань під час нападів:
    - `RecordSettlementDestroyed(WorldObject worldObject)`
    - `NotifyBuildingDestroyed(Thing building, DamageInfo? dinfo)`
    - `GetSettlementDestructionRecords(Faction ownerFaction)`
    - `GetRaidDamageRecordsForAttacker(Faction attackerFaction)`

## Коренева причина втрати кількості підтвердження десантування (v0.9.29)

- `Dialog_DiplomacyDialogue.ItemAirdropConfirmation.TryInjectPendingAirdropCountFromLatestPlayerMessage(...)`
  - Пріоритет ін’єкції змінено на:
    - 1) `FactionDialogueSession.pendingAirdropTradeCardRequestedCount`
    - 2) Розбір тексту останнього повідомлення гравця (лише якщо поточне значення відсутнє)
  - Обмеження поведінки:
    - Якщо параметри action уже містять `count/quantity`, явну кількість не перезаписувати.
  - Очікуваний результат:
    - Якщо під час підтвердження гравець вводить лише «Погоджуюсь/Підтверджую», кількість виконання й надалі відповідає кількості, прив’язаній до торгової картки, і більше не повертається до кількості за замовчуванням для родини.

## Коренева причина зациклення повторного входу під час підтвердження десантування (v0.9.28)

- `GameAIInterface.ItemAirdrop.Async.TryBuildAirdropAsyncContext(...)`
  - Додано примусову передачу параметра вибору через асинхронний контекст:
    - Зчитувати `selected_def` із параметрів action.
    - Заповнювати `ItemAirdropAsyncPrepareContext.ForcedSelectedDef`, щоб на етапі асинхронного вибору пріоритетно виконувати `TryBuildForcedSelection(...)`.
  - Додано синхронізацію семантики прив’язки:
    - `HasBoundNeed`: чи існує `__airdrop_bound_need_def`.
    - `HadForcedSelectionConflict`: встановлюється, коли `selected_def` і `bound_need_def` не збігаються; зрештою пріоритет має `bound_need_def`.
  - Очікуваний результат: `selection_manual_choice` більше не повторно входить після підтвердження автоматичного дозаповнення.
- `ItemAirdropSafetyPolicy.IsResourceCandidate(...)`
  - Діагностичний журнал змінено з «безумовного виведення кожного запису» на:
    - Для не-`Prefs.DevMode` нічого не виводити;
    - Для DevMode виводити записи з обмеженням частоти в межах вікна (для запобігання переповненню журналу).
  - Очікуваний результат: на етапі сканування кандидатів `Player.log` більше не розростається стрімко через масовий запис у журнал і не посилює зависання.

## Кореневе виправлення регресії арбітражу вимог прив’язки десантування (v0.9.27)

- `ItemAirdropSafetyPolicy.IsResourceCandidate(...)`
  - Відновлено порядок «сильний сигнал ресурсу має пріоритет»:
    - Якщо `ThingCategory.Item + stuffProps != null + 非食物/药物/服装`, одразу визначати об’єкт як кандидата на ресурс.
    - Потім застосовувати загальну логіку виключення, зокрема `IsWeapon`.
  - Очікуваний результат: сировина, як-от `WoodLog`, більше не спричиняє помилкове визначення належності до сімейства ресурсів через шумові метадані `IsWeapon`.
- `GameAIInterface.TryApplyBoundNeedArbitration(...)`
  - Завершення стратегії пріоритету прив’язаних ресурсів:
    - Якщо `bound_need_def` можна розібрати, але текст `intent.Family` не відповідає прив’язаним ресурсам, більше не повертати блокування `bound_need_family_conflict`.
    - Система записує аудиторський код `bound_need_family_conflict_overridden` і продовжує виконання відповідно до прив’язаних ресурсів.
  - Межі fail-fast залишаються без змін:
    - Лише якщо `bound_need_def` неможливо розібрати, транзакція й надалі блокується через `bound_need_unresolved`.
- Додано ключ локалізації:
  - `RimChat_ItemAirdropBoundNeedFamilyConflictOverrideAudit`（EN/ZH）

## Пряме передавання бойових активних повідомлень `call_everyone/waves`（v0.9.26）

## Виправлення кореня дружніх і нейтральних підкріплень `request_raid_call_everyone`（v0.9.30）

- `DiplomacyEventManager.ScheduleRaidCallEveryone(...)`
  - Час прибуття уніфіковано до випадкового інтервалу 16–36 годин:
    - Hostile -> `CallEveryoneActionKind.Raid`
    - Friendly/Neutral -> `CallEveryoneActionKind.MilitaryAidCustom`
  - Скасовано гілку «негайного виконання» для дружніх і нейтральних.
- `DelayedDiplomacyEvent.ExecuteRaidCallEveryoneEvent(...)`
  - Додано ланцюжок виконання `MilitaryAidCustom`:
    - Перевірка fail-fast шаблону генерації map/faction/Combat;
    - Створення групи combat-персонажів і висадка на карту;
    - Використання `LordJob_AssistColony` для формування поведінки підкріплення.
  - Більше не залежить від виконуваності incident `FriendlyRaid/RaidFriendly`.
- `GameComponent_DiplomacyManager.ProcessDelayedEvents(...)`
  - Стратегію помилки `RaidCallEveryone` змінено на no-retry: після помилки безпосередньо відкинути й записати в журнал.
- Міграція старих збережень (`PostLoadInit`)
  - Невиконаний `RaidCallEveryone` автоматично переплановується на вікно 16–36 годин;
  - Дії дружніх і нейтральних сторін перенесено до `MilitaryAidCustom`;
  - Очищено історичний стан повторних спроб (`MaxRetryCount/RetryCount/NextRetryTick`).

## Примусова подвійна публікація в соціальному колі `request_raid_call_everyone` (v0.9.33)

- `DiplomacyEventManager.ScheduleRaidCallEveryone(...)`
  - Після успішного планування спільного нападу негайно примусово опублікувати допис фракції в соціальному колі (військова категорія, негативні емоції).
  - Одночасно додати до черги відкладену подію `RaidCallEveryoneSocialPost`, час виконання — через 36 годин.
- `DelayedDiplomacyEvent`
  - Додано новий тип події `RaidCallEveryoneSocialPost`.
  - Під час виконання викликайте `TryEnqueueRaidCallEveryoneSocialPost(..., isFollowup:true)`, щоб опублікувати 36-годинне продовження в соціальній мережі.
- Ключі локалізації (китайська й англійська):
  - `RimChat_RaidCallEveryoneSocialPostImmediate`
  - `RimChat_RaidCallEveryoneSocialPostFollowup`

## Розширення вікна/обрізання учасників/переходу до подій `request_raid_call_everyone`（v0.9.32）

- `DiplomacyEventManager.ScheduleRaidCallEveryone(...)`
  - Вікно прибуття спільного рейду змінено з `16-36h` на `16-30h`.
  - Додано обрізання учасників: коли кількість ворожих фракцій `<=` кількість дружніх/нейтральних, дружніх і нейтральних буде по одному вилучено в порядку зростання `PlayerGoodwill`, доки кількість ворожих не `>` кількість дружніх/нейтральних.
  - Сповіщення про планування `detail` змінено на `hostile|friendly|16|30`.
- `GameComponent_DiplomacyManager.MigrateLegacyRaidCallEveryoneEvents(...)`
  - Вікно перепланування старих подій також змінено на `16-30h`.
- `DiplomacyEventManager.SendAidLetter(...)`
  - У листі `lookTargets` змінено з `null` на `LookTargets` у центрі домашньої карти гравця; у листі про прибуття підкріплення можна використовувати стандартну кнопку «Перейти до місця надсилання події».
- `PromptTextConstants.RequestRaidCallEveryoneActionDescription`
  - Опис дії синхронізовано з `16-30h` + «Обрізати дружніх і нейтральних учасників за прихильністю».

## Виправлення кореневої причини координат прибуття підкріплення на карту `request_raid_call_everyone`（v0.9.31）

- `DiplomacyEventManager.TryArriveCallEveryoneAidPawns(...)`
  - Видалено неявний шлях до точки висадки з `arrivalMode.Worker.Arrive(...)`, щоб уникнути появи координат `IntVec3.Invalid (-1000,-1000,-1000)`, що виходять за межі.
  - Новий процес:
    - Спочатку використати `CellFinder.TryFindRandomEdgeCellWith(...)`, щоб знайти легальну крайню клітинку для входу;
    - У разі невдачі повернутися до `DropCellFinder.TradeDropSpot(map)`;
    - Для кожного персонажа явно додати на карту за допомогою `CellFinder.TryFindRandomSpawnCellForPawnNear(...)` + `GenSpawn.Spawn(...)`.
  - Після додавання на карту створити `LordJob_AssistColony`; якщо зрештою на карту не додано жодної особи, негайно завершити з помилкою та записати аудиторську інформацію `entry/attempted/spawnFailed`.

## `call_everyone/waves` Пряме надсилання активних бойових повідомлень (v0.9.26)

- `DiplomacyEventManager.ScheduleRaidCallEveryone(...)`
  - На етапі диспетчеризації записати намір виконання відповідно до відносин:
    - Ворожі -> `CallEveryoneActionKind.Raid`
    - Дружні/нейтральні -> `CallEveryoneActionKind.MilitaryAidVanilla`
- `DelayedDiplomacyEvent.ExecuteRaidCallEveryoneEvent(...)`
  - Виконувати відповідно до наміру виконання:
    - `Raid` -> `TriggerRaidEvent(...)`
    - `MilitaryAidVanilla` -> `TriggerMilitaryAidEvent(...)` (vanilla `FriendlyRaid`)
  - Після успіху негайно активувати активне повідомлення arrival і додати подію до моніторингу departure.
- `DelayedDiplomacyEvent` поля бойового сеансу
  - Додано `ParticipantPawnThingIds`, `TriggerWaveEndAfterDeparture`, `CallEveryoneActionKindInt`.
  - `RaidDepartureMessage` змінено на «повторювати спробу, якщо pawn, який бере участь у бою, усе ще перебуває на карті поселення гравця», доки він справді не залишить карту або подію не буде завершено; лише після цього надсилати повідомлення departure.
  - `RaidWaveEndMessage` тепер запускається після успішного departure фінальної хвилі, без оцінювання на основі фіксованої затримки.
- `NpcDialogueTriggerContext` / `QueuedNpcDialogueTrigger`
  - Додано та збережено поле bypass:
    - `BypassRateLimit`
    - `BypassCategoryGate`
    - `BypassPlayerBusyGate`
- `GameComponent_NpcDialoguePushManager`
  - Фільтрацію `HandleTriggerContext(...)` для `WarningThreat` змінено на «типово блокувати + пропускати з bypass».
  - faction/global cooldown, reinitiate cooldown і busy gate не застосовуються до тригерів `BypassRateLimit`.
  - Якщо не вдалося згенерувати бойове повідомлення AI або результат порожній, fail-fast негайно доставляє резервний текст `Reason` із контексту.

## Кореневе виправлення відкладених подій `raid_call_everyone` (v0.9.25)

- `GameComponent_DiplomacyManager.ProcessDelayedEvents()`
  - Обробку відкладених подій змінено з «безпосередньо перебирати початковий список» на «перебирати знімок + об’єднувати із затримкою».
  - Додано захист від повторного входу на рівні tick, щоб уникнути повторного входу в ланцюжок обробки в тому самому tick.
  - Під час обробки `AddDelayedEvent(...)` тепер записується до черги очікування об’єднання, а після завершення обробки повертається до основної черги єдиною операцією.
- `DelayedDiplomacyEvent.ExecuteRaidCallEveryoneEvent()`
  - Семантику виконання зведено до raid-only: усі цілі відправлення системно проходять через `DiplomacyEventManager.TriggerRaidEvent(...)`.
  - `raid_call_everyone` Шлях більше не викликає гілку виконання військової допомоги, щоб уникнути шуму від недійсного пошуку Def за відсутності `FriendlyRaid`.

## Уточнення критерію походження оригінальної версії термінала зв’язку（v0.9.24）

- `CommsConsolePatch.GetFloatMenuOptionsPostfix(...)`
  - Попередня умова перехоплення працює за принципом fail-fast:
    - `option == null` або `option.action == null` — пропустити без перевірки;
    - якщо `IsVanillaCommsAction(option.action)` не виконується, пропустити без перевірки;
    - замінювати action лише для «джерела action оригінальної версії зв’язку + чинної фракції».
- `CommsConsolePatch.IsVanillaCommsAction(Action action)`
  - через `Method/DeclaringType/Assembly` статичне розпізнавання `Assembly-CSharp` за умови `Building_CommsConsole` джерела, щоб уникнути помилкового перехоплення пунктів меню сторонніх модифікацій.
- `CommsConsolePatch.ExtractFactionFromOption(...)`
  - Зберігати лише керований ланцюжок у контексті оригінальної версії зв’язку:
    - витягувати `Faction` із замикання action;
    - зіставлення `console.GetCommTargets(myPawn)` + label.
  - Видалити нечіткий резервний пошук label для списку всіх фракцій, щоб зменшити ймовірність помилкових збігів між модифікаціями.
- Діагностичний журнал:
  - Додано журнал причин пропуску (з обмеженням частоти): `Comms option bypassed: reason=NullOption|NullAction|NonVanillaAction|InvalidFaction`.

## Виправлення кольорів і обрізання карток чату з повітряними вантажами (v0.9.23)

- `Dialog_DiplomacyDialogue.ImageRendering`
  - Колір тексту сірого шару вмісту змінено на фіксовану висококонтрастну схему; більше не використовуються кольори, які виглядали тьмяними на зеленій зовнішній бульбашці.
  - Доступний простір для назви ресурсу, `defName` і рядка показників додатково збільшено, а висоту області показників синхронно піднято.

## Повернення початкової бульбашки карток чату з повітряними вантажами та сірий шар вмісту (v0.9.22)

- `Dialog_DiplomacyDialogue.ImageRendering`
  - Для зовнішнього шару картки повітряного вантажу відновлено `PlayerBubbleColor / AIBubbleColor`.
  - Шар вмісту карток запитів і ставок тепер окремо малюється на сірій основі; сірою більше не робиться вся картка.
  - Збільшено доступну висоту тексту назви ресурсу та рядка показників, щоб виправити обрізання через надмірну компактність.

## Повернення шару заголовка карток чату з повітряними вантажами (v0.9.21)

- `Dialog_DiplomacyDialogue.ImageRendering`
  - Трохи збільшено висоту рядка заголовка картки повітряного вантажу та верхні й нижні відступи, щоб заголовок залишався стабільно видимим у мінімалістичному макеті із сірим тлом.
  - Решту мінімалістичного макета із сірим тлом залишено без змін.

## Уніфікація мінімалістичного сірого тла карток чату з повітряними вантажами (v0.9.20)

- `Dialog_DiplomacyDialogue.ImageRendering`
  - Тло бульбашки картки повітряного вантажу уніфіковано як одношарове сіре; кольорове тло більше не застосовується окремо для гравця/AI.
  - Стрічку заголовка, тло внутрішніх блоків і акцентний блок нижньої панелі вилучено; використано макет із текстом і роздільними лініями.
  - Константи мініатюри, області заголовка та області показників знову ущільнено, щоб додатково зменшити загальну висоту картки чату.

## Компактизація картки повітряного десанту в чаті та блокування входу під час затримки відновлення (v0.9.19)

- `Dialog_DiplomacyDialogue.ImageRendering`
  - Бюджет висоти картки повітряного десанту тепер динамічно обчислюється на основі фактичного обсягу тексту, розміру мініатюри та нижньої області показників, щоб довгі назви/довгий `defName` не накладалися на блок показників.
  - `defName` тепер завжди обрізається й відображається в окремому рядку; області назви та показників більше не накладаються одна на одну.
- `Dialog_DiplomacyDialogue.OpenSendInfoMenu()`
  - Тепер `ApiActionEligibilityService.ValidateActionExecution(faction, "request_item_airdrop", null)` викликається заздалегідь для створення пункту меню повітряного десанту.
  - У разі спрацювання `airdrop_cooldown` повертається вимкнений пункт меню, повторно використовуючи наявний локалізований текст про час, що залишився в грі.
- `Dialog_DiplomacyDialogue.TryStartManualAirdropTradeSend()`
  - Перед фактичним відкриттям картки повторно застосувати ту саму перевірку eligibility, щоб уникнути розбіжностей у перевірці затримки відновлення між входом UI і етапом дії.

## Виправлення вигляду картки повітряного десанту та скидання стану Presence (v0.9.18)

- `Dialog_DiplomacyDialogue.AddAIResponseToSession(...)`
  - Видалити `hadPendingAirdropTradeCardReference && !hasAirdropAction` із гілки `RimChat_AirdropTradeCardIgnoredSystem` ін’єкцію системних повідомлень.
  - Збережено без змін наявний процес прив’язки повітряного десанту, виконання дії, обробки помилок і резервного переходу presence.
- `Dialog_DiplomacyDialogue.ImageRendering`
  - Висота `CalculateAirdropTradeCardBubbleHeight(...)` тепер динамічно обчислюється на основі вмісту картки потреби/картки ставки.
  - `DrawAirdropTradeCardBubble(...)` / `DrawAirdropItemCard(...)` перемальовано у стилі занурювального термінального документа; контракт даних повідомлення не змінено.
- `FactionPresenceState`
  - Додано поле збереження `doNotDisturbUntilTick`, типове значення — `0`; старі збереження автоматично підтримуються.
- `GameComponent_DiplomacyManager.ApplyPresenceAction(...)`
  - `go_offline` і надалі використовує логіку завершення в офлайн-режимі та очищає DND після завершення.
  - `set_dnd` тепер записується в окремий `doNotDisturbUntilTick = currentTick + 3 * GenDate.TicksPerDay`.
- `GameComponent_DiplomacyManager.RefreshPresenceOnDialogueOpen(...)`
  - Спочатку перевіряється, чи `forcedOfflineUntilTick` і `doNotDisturbUntilTick` досі чинні.
  - Після завершення обох очистити відповідні поля стану виконання та повторно виконати перерахунок розкладу `EvaluateScheduledPresence(...)`.
- `Dialog_DiplomacyDialogue.ActionHint`
  - Спеціальне відображення `airdrop_cooldown` тепер перетворює `RemainingSeconds` назад на ігрові тіки, а потім виводить текст про залишок часу в грі.

## Оновлення зануреного аналізу повторного котирування десантування (v0.9.17)

- `TryCaptureAndCacheAirdropCounteroffer(...)` тепер підтримує три типи вводу:
  - Старий фіксований шаблон: `重报价: item=... count=... silver=... reason=...`
  - Природне речення китайською: наприклад, «За цю партію деревини я максимум дам тобі 50 одиниць, ціна — 400 срібла, бо запаси обмежені».
  - Природне речення англійською: наприклад, «WoodLog, we can spare 50 units for 400 silver because stock is tight.»
- Правила резервного аналізу:
  - Якщо в природному реченні бракує `item`, спочатку підставити значення з прив’язаного до поточної торгової картки `NeedDefName`.
  - `reason` тепер витягується з `因为/原因/due to/because/since` та інших природних підказок.
- `Dialog_ItemAirdropTradeCard.ApplyCounterofferDefaults()` тепер одночасно заповнює:
  - `requestedCountText <- lastAirdropCounterofferCount`
  - `offerCountText <- lastAirdropCounterofferSilver`

## Виправлення джерела кількості для підтвердження десантування (v0.9.16)

- `TryInjectPendingAirdropCountFromLatestPlayerMessage(...)` тепер використовує такий порядок пріоритетів:
  - 1. `FactionDialogueSession.pendingAirdropTradeCardRequestedCount`
  - 2. Структурована «потреба xN / need xN» у тексті
  - 3. Старе вилучення з резервним використанням лише числових значень
- Очікуваний результат:
  - Коли текст картки торгівлі має вигляд «потреба Деревина x50, пропозиція Срібло x400», фіксоване значення `count`, яке вставляється на рівні виконання, дорівнює `50`, і більше не помилково бере `400`.

## Виправлення помилкового визначення придатності деревини як ресурсу（v0.9.15）

- Змінено порядок визначення придатності `ItemAirdropSafetyPolicy.IsResourceCandidate(...)` як ресурсу:
  - Якщо `ThingDef` має чіткі ознаки ресурсу (нині `stuffProps != null`, але не їжа/ліки/одяг), спочатку його визначають як ресурс.
  - Лише після цього застосовується загальне виключення `IsWeapon`.
- Очікуваний результат:
  - Сировина на кшталт `WoodLog` із шумовою позначкою `IsWeapon` усе ще може пройти арбітраж пов’язаної потреби в межах `ItemAirdropNeedFamily.Resource`.

## Повне виправлення проходження стану прив’язаного предмета картки торгівлі за допомогою десанту（v0.9.14）

- До внутрішніх обмежень виконання `request_item_airdrop` додано:
  - Якщо поточний діалог усе ще містить `need_def`, прив’язаний до картки торгівлі за допомогою десанту, усі подальші підтвердження, відкладене зіставлення намірів і остаточне виконання повинні містити ту саму групу метаданих bound need.
  - Якщо гравець явно змінює вибір кандидата, система спочатку очищає прив’язку картки торгівлі, а потім дозволяє застосувати новий `selected_def`.
  - Якщо `preparedTrade.SelectedDefName`, отриманий під час асинхронної підготовки, не відповідає bound need, інтерфейс негайно виконує fail-fast і повертає `bound_need_prepared_mismatch`.
- Додано ключ локалізованого повідомлення про помилку:
  - `RimChat_ItemAirdropBoundNeedStateLostSystem`
  - `RimChat_ItemAirdropBoundNeedPreparedMismatchSystem`

## Кореневе виправлення арбітражу прив’язаних потреб для аеродропу (v0.9.13)

- Додано ключ внутрішнього параметра:
  - `__airdrop_bound_need_def`
  - `__airdrop_bound_need_label`
  - `__airdrop_bound_need_search_text`
  - `__airdrop_bound_need_source`
  - `__airdrop_bound_need_conflict_code`
  - `__airdrop_bound_need_conflict_message`
- `FactionDialogueSession.SetPendingAirdropTradeCardReference(...)` тепер одночасно містить:
  - Початковий `need`
  - Прив’язаний `NeedDefName`
  - Прив’язаний `NeedLabel`
  - Прив’язаний `NeedSearchText`
- До внутрішнього довідкового блоку `[AirdropTradeCardReference]` додано поля:
  - `need_def`
  - `need_label`
  - `need_search_text`
- Уточнення семантики виконання:
  - Якщо існують метадані прив’язаної потреби, на етапі підготовки рівень виконання спочатку проводить арбітраж прив’язаної потреби.
  - Якщо пул кандидатів конфліктує з прив’язаною потребою, система додає прив’язані ресурси до пулу кандидатів і перебудовує вибір.
  - Якщо прив’язану потребу неможливо розібрати або вона конфліктує із сімейством потреб, негайно виконується fail-fast без переходу до діалогового вікна підтвердження.

## Перебудова ланцюжка запиту аеродропу UI/тайм-ауту/зіставлення (v0.9.12)

- `ItemAirdropTradeCardPayload` Додано структуроване поле ціни:
  - `NeedUnitPrice`
  - `NeedReferenceTotalPrice`
  - `OfferUnitPrice`
  - `OfferTotalPrice`
- Уніфіковано контракт подання картки десантування:
  - `NeedDefName` тепер є обов’язковим полем для подання картки десантування.
  - `GetNeedReferenceText()` має пріоритетно виводити `NeedDefName`, щоб вставляти внутрішній довідковий блок і уникати дрейфу вільного тексту.
- У структуру повідомлення картки десантування `DialogueMessageData` додано:
  - `airdropNeedUnitPrice`
  - `airdropNeedReferenceTotalPrice`
  - `airdropOfferUnitPrice`
  - `airdropOfferTotalPrice`
- Змінено семантику тайм-ауту другого етапу:
  - Лише `selection_timeout/queue_timeout` може автоматично отримувати `Options[0]`.
  - Після автоматичного отримання Top1 одразу продовжується підготовка остаточного підтвердження; `pendingDelayedActionIntent` більше не кешується, і гравцеві більше не потрібно відповідати `1/2/3/...`.
  - Для pending, не пов’язаних із тайм-аутом, як і раніше повертається чітка помилка; автоматичне укладання угоди не виконується.
- Уніфіковано точку входу зіставлення:
  - Додано `ThingDefMatchEngine.cs`, що надає `ThingDefMatchRequest / ThingDefMatchCandidate / ThingDefMatchResult`.
  - Пошукові підказки, розбір платежів і стандартне сортування кандидатів використовують єдиний порядок оцінювання: exact `defName` > exact `label` > normalized exact > alias > token full cover > search text > semantic tokens > near match.

## Рефакторинг картки запиту на десантування — структурований вибір товару + структурована пропозиція ціни（v0.9.10）

- `ItemAirdropTradeCardPayload` додано точні поля на стороні запиту: `NeedDefName`, `NeedLabel`, `NeedSearchText`.
- `Dialog_ItemAirdropTradeCard` запроваджує локальний стан пошуку `SearchStateManager`, який керує дебаунсом (180 мс), обчисленням кандидатів (за замовчуванням 6) і точним прив’язуванням.
- Процес пошуку:
  - Після введення застосовується локальне усунення тремтіння на 180 мс.
  - Повторно обчислювати кандидатів лише після зміни нормалізованого тексту запиту.
  - Сортувати кандидатів у такому порядку: exact `defName` / exact `label` / токен із сильним збігом, а потім передавати до наявного resolver.
  - Після вибору пропозиції негайно створити структуроване прив’язування та очистити список пропозицій.
  - Якщо гравець змінює слово й воно більше не дає точного збігу з прив’язуванням, негайно очистити прив’язування.
- Логіка повторного заповнення:
  - Спочатку розібрати за `lastAirdropCounterofferDefName` у `ThingDef` та створити структуровану картку потрібного ресурсу.
  - `count` заповнює кількість потрібного ресурсу, `silver` — кількість запропонованого ресурсу.
  - Відображати нормативну назву в полі пошуку лише за умови точного розбору `ThingDef`; у разі помилки розбору зберігати лише заповнення кількості.
- Макет інформаційної картки (двоколонковий):
  - Вгорі: поле пошуку потрібного ресурсу.
  - Під полем пошуку: спадний список пропозицій, що відображається лише за наявності кандидатів.
  - У центрі: ліворуч картка потрібного ресурсу (мініатюра, назва, `defName`, ринкова ціна, максимальний розмір стосу), праворуч — картка запропонованого ресурсу.
  - Унизу: `需求物资数量` / `出价物资数量` / кнопки «Надіслати»/«Скасувати».
- Структурований ланцюжок надсилання:
  - `ItemAirdropTradeCardPayload` містить текст початкової вимоги та точні поля вимоги.
  - `FactionDialogueSession.SetPendingAirdropTradeCardReference` / `TryBuildPendingAirdropTradeCardReference` розширено, щоб одночасно додавати текст початкової вимоги та точні поля вимоги.
  - Зберегти наявний `need/count/payment_items/scenario`, щоб додавання точних полів вимоги не порушило старий ланцюжок.

## Виправлення доступності інформаційної картки аірдропу (v0.9.9)

- Семантику UI зведено до «введення вимоги + вибір запасів для бартеру»; три типи взаємодій, що могли ввести в оману — вибір кандидатів, оновлення та пошук — вилучено.
- Пакет надсилання зберігає семантику еталонної пропозиції: `need` + `count` + `payment_items[{item,count}]`, де `payment_items` походить із вибору запасів маяка.
- Додано текст для порожнього стану: якщо немає доступних для торгівлі запасів маяка, про це повідомляється безпосередньо; порожній список із доступними для натискання елементами більше не відображається.

## Висаджено інформаційну картку «Переговори під керуванням ШІ» (v0.9.8)

- `Dialog_DiplomacyDialogue.OnAirdropTradeCardSubmitted` тепер надсилає підсумок природною мовою, а не записує структурований блок торгівлі безпосередньо в історію чату.
- Структуровані поля інформаційної картки аірдропу тепер внутрішньо додаються через контекст виконання `FactionDialogueSession` (`TryBuildPendingAirdropTradeCardReference`), лише для довідки AI у цьому раунді, а не як джерело примусового виконання.
- `Dialog_DiplomacyDialogue.AddAIResponseToSession` додатково розбирає фіксовані формулювання повторної пропозиції та записує їх у кеш сеансу (`CacheAirdropCounteroffer`).
- Якщо під час раунду, ініційованого інформаційною карткою, AI не повертає дію `request_item_airdrop`, система додає підказку: `RimChat_AirdropTradeCardIgnoredSystem`, і не виконує автоматично додаткову дію.
- Запаси праворуч у `Dialog_ItemAirdropTradeCard` тепер відображаються як «матеріали й кількість, доступні через підключений до живлення орбітальний маяк»; під час відкриття картки для початкового заповнення спершу зчитується остання AI повторна пропозиція.
- Відображення причин обмежень у `Dialog_DiplomacyDialogue.ActionHint` змінено на: локалізація code в пріоритеті -> `validation.Message` -> загальний текст; усі `*_cooldown` уніфіковано форматуються як час.
- Оновлення контракту промпту: поля інформаційної картки є орієнтовною пропозицією; AI може відхилити, подати зустрічну пропозицію або змінити параметри виконання; фіксований формат зустрічної пропозиції:
  - `重报价: item=<defName> count=<int> silver=<int> reason=<text>`
  - `Counteroffer: item=<defName> count=<int> silver=<int> reason=<text>`

## Специфікація параметрів і самоперевірка патчу Harmony під час запуску (v0.9.6)

- Область впливу: ланцюжок ін’єкції ключових патчів під час запуску (`RimChatMod` -> `Harmony.PatchAll`).
- Специфікація параметрів:
  - Для ключових патчів уніфіковано стиль позиційних параметрів (`__0/__1/...`) або забезпечено сувору відповідність назвам параметрів оригінального методу.
  - Резервний патч `Translator.TryTranslate` переключено на `__0/__1`.
- Самоперевірка під час запуску:
  - Додано `HarmonyPatchStartupSelfCheck.Run()`, який перед `PatchAll` перевіряє підписи ключових патчів і виводить мінімальний журнал:
    - Успішно: `[RimAI.Relations][HarmonySelfCheck] Startup patch checks passed`
    - Помилка: `[RimAI.Relations][HarmonySelfCheck] ... failed` + докладні відомості про помилки

## Кореневе виправлення англійського резервного перекладу для мовних ключів не китайською й не англійською (v0.9.5)

- Область впливу: глобальний ланцюжок аналізу ключів локалізації `RimChat_*`.
- Механізм:
  - Додати постпатч для `Translator.TryTranslate(string, out TaggedString)`.
  - Лише якщо оригінальний переклад не вдався й префікс ключа — `RimChat_`, виконати резервний розбір із `1.6/Languages/English/Keyed/RimChat_Keys.xml`.
- Обмеження:
  - Ключі, відмінні від `RimChat_*`, не беруть участі в цьому патчі.
  - Не змінює оригінальну мовну систему та не перезаписує поведінку перекладу інших модів.
- Спостереження:
  - Під час першого резервного переходу буде записано попередження в журнал із позначенням, що в поточній активній мові відсутній запис із ключем RimChat.

## Пряме читання каталогу мов журналу версій і динамічний список мов (v0.9.4)

- Діапазон впливу:`RimChatSettings_APIHeader.UX` з API ланцюжок читання журналу версій у верхній частині сторінки.
- Контракт розбору каталогу мов:
  - Спершу прочитайте безпосередньо `LanguageDatabase.activeLanguage.folderName` безпосередньо `1.6/Languages/<folderName>`.
  - Якщо збігу не знайдено, виконати нормалізований пошук (ігноруючи пробіли, роздільники та відмінності регістру), а також пошук за мапуванням псевдонімів.
  - Якщо збігу все ще немає, виконати fail-fast із резервним переходом на `English` і вивести чітке `Log.Warning`.
- Джерело доступних мов:
  - `AvailableLanguages` більше не містить жорстко заданих китайської та англійської мов; список динамічно генерується скануванням безпосередніх підкаталогів `1.6/Languages`.
- Порядок кандидатів для файлу журналу версій:
  - `VersionLog_<languageFolder>.txt`
  - `VersionLog.txt`
  - `VersionLog_en.txt`
- Семантика винятків:
  - Якщо файл цільової мови не існує, негайно виконується відкат до файлу English із збереженням наявного ланцюжка повідомлень UI про «відсутній файл/порожній файл/помилку читання».

## Інформаційна картка десанту та 3-денна затримка відновлення десанту (v0.9.7)

- Для `RimChat.DiplomacySystem.GameAIInterface` додано ключ затримки відновлення `RequestItemAirdrop`, параметр конфігурації `ItemAirdropCooldownTicks` (за замовчуванням 180000 ticks = 3 дні), а також читання/запис у збереження та повзунок UI.
- `ApiActionEligibilityService.ValidateActionExecution("request_item_airdrop", ...)` підключає перевірку затримки відновлення (`ValidateCooldown(faction, "RequestItemAirdrop", "airdrop_cooldown")`) уже на етапі підказки з порожніми параметрами, відхиляє запит протягом періоду затримки відновлення та повертає `RemainingSeconds`.
- Після успішного виконання `GameAIInterface.ItemAirdrop.Barter.CommitPreparedItemAirdropTrade` викликається `SetCooldown(faction, "RequestItemAirdrop")`; затримка відновлення запускається лише після успішного commit, а скасування/помилка її не запускають.
- У меню `+发送信息` додано пункт «Надіслати запит на торгівлю десантом» (`RimChat_SendInfoMenuAirdropTrade`), який відкриває вікно інформаційної картки `Dialog_ItemAirdropTradeCard` (два списки: рекомендовані кандидати + запаси колонії); гравець указує кількість і ставку в сріблі, після чого надсилає запит, автоматично генерується структурований блок повідомлення та запускається запит AI.
- Payload, який надсилає `Dialog_ItemAirdropTradeCard`, завжди містить поля: `need`, `selected_def`, `count`, `payment_items=[{"item":"Silver","count":N}]`, `scenario=trade`.
- Оновлення `Dialog_DiplomacyDialogue.ActionHint`: коли всі дипломатичні дії `[?]` обмежені, відображається «статус + локалізована причина обмеження» (`BuildActionHintLine` підключає `ActionValidationResult`, а виклики, пов’язані із затримкою відновлення, форматуються `FormatCooldownReason(remainingSeconds)` у днях/годинах/хвилинах).
- Додано тексти Keyed: `RimChat_SendInfoMenuAirdropTrade`, `RimChat_AirdropTradeCard_*` (заголовок/мітка/кнопка), `RimChat_ActionsHint_CooldownDays/Hours/Minutes`, `RimChat_ActionsHint_Reason_*` (відповідники різних причин обмеження).

## Видалення другого етапу десанту та підтвердження ручного повторного вибору (v0.9.3)

- Ланцюжок вибору другого етапу:
  - `request_item_airdrop` більше не надсилає запит на вибір AI другого етапу.
  - Натомість пул кандидатів безпосередньо передається до процесу підтвердження, за замовчуванням автоматично обирається Top1.
- Взаємодія з підтвердженням:
  - У вікні підтвердження додано малопомітну кнопку `RimChat_ItemAirdropAlternativeLowVisibility`.
  - Після натискання можна вручну змінити вибір серед 5 найкращих кандидатів, а потім перейти до підтвердження виконання.
- Сумісність параметрів кількості:
  - Витягування кількості підтримує обидва ключі: `count` і `quantity`.
  - Як і раніше, значення об’єднується з явно вказаною кількістю `need`, після чого виконується перевірка допустимого діапазону.

## Повне виправлення тайм-ауту другого етапу десантування та коригування діапазону кількості (v0.9.2)

- Повне виправлення тайм-ауту другого етапу:
  - Канал `AirdropSelection` вимикає повторні спроби через тайм-аут локального з’єднання, щоб після одноразового `timeout` автоматично не очікувати ще один раунд до завершення тайм-ауту запиту.
  - Для другого етапу додано окреме налаштування тайм-ауту черги: `ItemAirdropSecondPassQueueTimeoutSeconds` (типове значення `15`, діапазон `3..120`).
  - `ItemAirdropSecondPassTimeoutSeconds` і надалі є налаштуванням «тайм-ауту одноразового запиту» (типове значення `25`, діапазон `3..30`).
- Розширення діагностики другого етапу:
  - До діагностики `selection_async_success/timeout/error` додано поля:
    - `firstByteMs`（від dispatch до отримання першого байта）
    - `attempts`（кількість спроб запиту）
    - `payloadBytes`（кількість байтів тіла запиту）
    - `http`（код стану останнього HTTP）
    - `endpoint`（хост:порт endpoint）
- Налаштування вікна кількості десантування:
  - `hardMax` більше не використовує `ItemAirdropMaxTotalItemsPerDrop` для жорсткого обмеження загальної кількості.
  - Нова логіка: `hardMax=min(maxByBudget, maxByStacks)`, де `maxByStacks = ItemAirdropMaxStacksPerDrop * def.stackLimit`.
  - Семантика результату: кількість визначається потребою та бюджетом і водночас обмежується фізичними властивостями стосів предметів, що випадають.

## Діагностика та виправлення ланцюжка десантування（v0.9.1）

- Стратегія виконання `request_item_airdrop` в одному раунді
  - Виконавець тепер «приймає лише перший успішний запит на десантування в межах одного раунду», а наступні дії десантування в тому самому раунді повертають результат відхилення, замість «відхиляти всі».
- Попереднє пряме підключення до відповіді кандидатів другого етапу
  - Перед надсиланням нового запиту AI, якщо в сесії є стан очікування вибору кандидата десантування, а введення гравця відповідає `1/2/3/defName/label`, його безпосередньо зіставляють із `selected_def` і переходять до ланцюжка підготовки десантування.
  - Після спрацювання попереднього прямого підключення новий запит вибору AI другого етапу більше не надсилається.
- Пріоритет джерел кількості
  - Рішення щодо додаткової кількості: якщо одночасно наявні явно вказані в тексті кількості `parameters.count` і `need`, береться більша.
  - Якщо кількість, визначена рішенням, перевищує `hardMax`, її автоматично обрізають до `hardMax` відповідно до стратегії, а в аудиті записують `original->hardMax`.
- Покращення двоетапної діагностики
  - Додано журнали аудиту етапів другого етапу: `selection_async_dispatch`, `selection_async_success`, `selection_async_timeout`, `selection_async_parse_failed`, `selection_async_error`.
  - Журнал містить requestId, timeout, кількість кандидатів, queueMs, processingMs, endToEndMs, failureReason, щоб розрізняти queue_timeout / request timeout / parse failure.

## Звірка органів у картці інформації про в’язня та оновлення розцінок під час завантаження збереження (v0.8.20)

- Розширення знімка органів і збережених даних:
  - `RansomContractRecord` додані поля:
    - `BaselineCoreOrganMissingSnapshot`
    - `ExitCoreOrganMissingSnapshot`
    - `NewlyMissingCoreOrgans`
    - `OrganFailureScheduled`
    - `OrganFailureDueTick`
    - `OrganFailurePenaltyApplied`
  - Стратегія сумісності: для всіх нових полів у `ExposeData` указано значення за замовчуванням, тому старі збереження можна завантажувати безпосередньо.
- Примусове оновлення розцінок у картці інформації:
  - `CalculatePrisonerRansomQuote(...)` додано необов’язковий параметр `forceRefresh=false`.
  - У ланцюжку публікації картки інформації про в’язня фіксовано `forceRefresh=true`, щоб під час кожної публікації заново обчислювати орієнтовний викуп на основі поточного стану.
- Керування кешем у робочому стані:
  - `GameAIInterface.ResetPrisonerRansomRuntimeState()` очищає робочий стан торгу щодо викупу та знімок картки інформації.
  - Викликайте цей метод у `GameComponent_DiplomacyManager.StartedNewGame/LoadedGame`, щоб уникнути залишкових статичних синглтонів між ігровими сесіями.
- Перевірка невдалого виходу з карти:
  - Правило перевірки: під час виходу з карти перевіряється лише «відсутність нових життєво важливих органів порівняно з базовими даними інформаційної картки».
  - Перелік життєво важливих органів: `Heart/Liver/Lung/Kidney/Eye` (підрахунок за екземплярами).
  - У разі спрацювання заплануйте `dueTick = exitTick + Rand[12500, 25000]`; після завершення терміну застосовується покарання за перевищення часу (зменшення прихильності + напад).
  - Якщо вихід через ураження органу визнано невдалим, додаткове негайне `drop_penalty` більше не накладається, щоб уникнути подвійного покарання.

## Підтвердження успішного виходу за контрактом на викуп і докір за перевищення часу (v0.8.19)

- Розширення збереження контракту на викуп:
  - У `RansomContractRecord` додано поля:
    - `TargetPawnLabelSnapshot`
    - `ReleasedTick`
    - `HealthyExitReplyScheduled`
    - `HealthyExitReplyDueTick`
    - `HealthyExitReplySent`
  - Стратегія сумісності: усі поля в `ExposeData` мають значення за замовчуванням, старі збереження заповнюються автоматично.
- Відкладене підтвердження успішного виходу:
  - Умова спрацювання: під час виходу в’язня з карти дотримано суворих вимог до здоров’я (`SummaryHealth >= 85%`, `Consciousness >= 85%` і не `Downed`).
  - Правило планування: `dueTick = exitTick + Rand[12500, 25000]` (5–10 ігрових годин).
  - Дія після доставки: після завершення терміну записати повідомлення NPC до сеансу цієї фракції та надіслати вхідний лист `ChoiceLetter_NpcInitiatedDialogue`.
- Посилення покарання за нездійснений вихід після завершення терміну:
  - Зберегти наявну логіку листів `ApplyRansomPenaltyAndRaid` і `RimChat_PrisonerRansomTimeout*`.
  - Додатково синхронно додати:
    - Повідомлення про тайм-аут у сеансі та нагадування про вхідні листи.
    - Негативна подія соціального кола `EnqueuePublicPost(...)`, що запускає підготовку матеріалу «Осуд лідера фракції» AI.
- Додано ключі локалізації:
  - `RimChat_PrisonerRansomHealthyExitReplyMessage`
  - `RimChat_PrisonerRansomHealthyExitLetterTitle`
  - `RimChat_PrisonerRansomTimeoutWarningMessage`
  - `RimChat_PrisonerRansomTimeoutWarningLetterTitle`
  - `RimChat_PrisonerRansomTimeoutCondemnSummary`

## Стабілізація дедуплікації та затримки відновлення тайм-ауту для викупу request_info（v0.8.14）

- Посилення поведінки `request_info(info_type=prisoner)`:
  - Якщо до сеансу прив’язано дійсну ціль для викупу, `request_info(prisoner)` безпосередньо повертає успішний результат і більше не запускає вікно вибору персонажа.
  - Якщо сеанс очікує вибору персонажа, повторний запуск буде усунуто дедуплікацією та не ввійде повторно в процес вибору.
- Затримка відновлення тайм-ауту автоматичної відповіді:
  - У ланцюжку автоматичної відповіді на картку інформації про в’язня додано 90-секундний бар’єр затримки відновлення.
  - Після виявлення категорії тайм-ауту（`queue_timeout` / `network_timeout` / `drop_timeout`）у межах вікна затримки відновлення повторне автоматичне надсилання запиту в тому самому ланцюжку не виконується.
  - Шлях ручного надсилання гравцем не залежить від цього бар’єра затримки відновлення.
- Спостережуваність:
  - Додано журнал: `request_info(prisoner) dedup hit`
  - Додано журнал: `ransom auto-reply timeout classified=... cooldown=90s`
- Контракт і сумісність:
  - Структура дій, назви параметрів і семантика повернення `request_info` та `pay_prisoner_ransom` не змінюються.
  - Нові поля постійного збереження не додаються; нові поля затримки відновлення стану виконання не записуються в `ExposeData`.

## Посилення узгодженості дії обіцянки викупу (MUST) (v0.8.13)

- Жорсткі обмеження семантики викупу:
  - Якщо природною мовою з’являється «подано/сплачено/гроші й товар обміняно/відпущено людину» або інший стан завершення, **у цій самій відповіді обов’язково має бути** `pay_prisoner_ransom` дія.
  - Якщо цей раунд не містить `pay_prisoner_ransom`за замовчуванням природна мова має перейти до формулювання на кшталт «очікує підтвердження» / «очікує надсилання».
- Жорсткі обмеження комунікаційного контексту:
  - Поточний сценарій фіксовано як онлайн-чат через комунікаційний термінал; заборонено описувати завершення офлайн (прибуття на місце, особисту передачу, відведення людини).
- Обсяг синхронізації:
  - Промпт за замовчуванням, системну конфігурацію за замовчуванням, патч міграції та контракт стислих відповідей уже узгоджено.

## Комунікаційний контекст термінала та узгодженість дії обіцянки викупу (v0.8.12)

- Обмеження контексту термінала:
  - Діалог завжди слід розглядати як онлайн-чат через комунікаційний термінал, а не як офлайн-зустріч.
  - Природна мова не повинна виражати стан офлайн-завершення на кшталт «я вже прибув/обробив особисто/передав офлайн».
- Обмеження узгодженості обіцянки викупу:
  - Якщо природна мова заявляє «викуп уже подано/сплачено», відповідь у тому самому повідомленні повинна містити дію `pay_prisoner_ransom`.
  - Якщо в цьому раунді не виведено `pay_prisoner_ransom`, природну мову потрібно змінити на формулювання, що викуп ще не подано (уточнення або очікування підтвердження).
- Обсяг синхронізації:
  - Уже синхронізовано з типовим промптом, системною типовою конфігурацією, правилами міграції під час виконання та текстом правил дій.

## Подання одноразової сплати викупу (v0.8.11)

- Зміни контракту виконання:
  - `pay_prisoner_ransom` і надалі використовує початкові параметри: `target_pawn_load_id`, `offer_silver`, `payment_mode?`.
  - Код успішного стану змінено на `paid_submitted` (значення: платіж отримано та контракт зареєстровано, автоматичного звільнення немає).
  - `counter_offer/rejected_floor_not_met` більше не повертається як процес торгу через код.
- Зміни ланцюжка виконання:
  - Рівень виконання перевіряє лише параметри, відповідність цілі вимогам і діапазон пропозиції; після проходження перевірки безпосередньо скидає срібло з повітря та реєструє контракт.
  - Видалено попередню перевірку перед звільненням (`warden/exit cell`) і надсилання job `ReleasePrisoner`.
  - За відсутності або недійсності `target_pawn_load_id` повертається до семантики вибору персонажа `request_info(prisoner)`.
- Зворотний зв’язок про помилки:
  - Повідомлення про помилки мають бути стислими системними причинами (помилка параметрів, недійсна ціль, вихід за межі діапазону, помилка режиму, система недоступна).
- Межі сумісності:
  - Не додавати нових полів збереження; використовувати наявні поля сесії та механізм штрафів за контрактом.

## Візуалізація неостаточних результатів викупу (v0.8.10)

- Додати зворотний зв’язок для UI:
  - Коли `pay_prisoner_ransom` повертає `counter_offer`, системне повідомлення показує: ціль, відхилену пропозицію, поточну зустрічну пропозицію, раунд.
  - Коли повертається `rejected_floor_not_met`, системне повідомлення показує: ціль, останню пропозицію, мінімальну ціну та пропонує підвищити пропозицію й повторити спробу.
- Джерело даних:
  - текст `PrisonerRansomResultData` текст `StatusCode/OfferedSilver/CurrentAskSilver/FloorSilver/RoundIndex/MaxRounds/TargetPawnLoadId`。
- Збереження контракту:
  - Протокол дій і параметри не змінюються; нових полів збереження не додавати.

## Перевірка діапазону для регресії пропозицій викупу (v0.8.9)

- Зміна правил виконання:
  - `pay_prisoner_ransom` більше не вимагає `offer_silver == currentAsk`.
  - Якщо `offer_silver` потрапляє в поточне дійсне вікно пропозиції (min/max), дозволено перейти до автомата станів торгу.
- Коригування правил промпту:
  - Поточна запитувана ціна все ще є рекомендованим орієнтиром, але не є жорсткою перепоною на рівні виконання.
  - Промпт за замовчуванням, системну конфігурацію за замовчуванням і патч міграції синхронізовано із семантикою «дійсне в межах діапазону».
- Контракт збережено:
  - Структура дії `request_info/pay_prisoner_ransom` та назви параметрів не змінюються.
  - Нові поля збереження не додаються.

## Жорстка перевірка на рівні виконання поточної запитуваної ціни викупу (v0.8.8)

- Додано перепону на рівні виконання:
  - Коли `PrisonerRansomNegotiationState.CurrentAskSilver > 0`, `offer_silver` має дорівнювати `CurrentAskSilver`.
  - Якщо поточна запитувана ціна не збігається, безпосередньо повертається fail-fast (`offer_must_match_current_ask`).
- Повідомлення про помилку:
  - Використовуйте `RimChat_RansomOfferMustMatchCurrentAskSystem` (локалізація китайською та англійською), явно повертаючи `offered/currentAsk/min/max`.
- Контракт збережено:
  - Структура дії `request_info/pay_prisoner_ransom` та назви параметрів не змінюються.
  - Не додавати нових полів збереження.

## Правила успішного очищення стану викупу в термінальному стані（v0.8.7）

- Виправлення автомата станів:
  - Після успішного `pay_prisoner_ransom` стан прив’язки сеансу викупу очищується лише в `accepted_and_released`.
  - `counter_offer`、`rejected_floor_not_met` Належить до не-фінального стану успіху/переговорів; потрібно зберегти прив’язану ціль для подальших переговорів.
- Джерело визначення стану:
  - У пріоритеті зчитувати код стану повідомлення з результатом виконання（`result.Message`）.
  - Для сумісності зчитувати код стану даних результату（`PrisonerRansomResultData.StatusCode`）.
- Збереження контракту:
  - Не змінювати структуру дії `request_info/pay_prisoner_ransom` та обмеження її параметрів.
  - Не додавати нових полів збереження.

## Умовне спрацювання викупу request_info（v0.8.6）

- Коригування семантики дії:
  - `request_info(info_type=prisoner)` більше не є обов’язковою передумовою для `pay_prisoner_ransom`.
  - Використовувати лише за відсутності дійсного `target_pawn_load_id`, щоб ініціювати вибір персонажа й доповнити інформацію про ціль.
- Коригування ланцюжка виконання:
  - `pay_prisoner_ransom` можна виконати безпосередньо, якщо інформація про ціль уже чітко визначена та дійсна.
  - Якщо інформація про ціль відсутня або недійсна, рівень виконання запустить підказку щодо вибору персонажа для доповнення інформації та відхилить цю платіжну дію (fail-fast).
- Межі контракту без змін:
  - Не додавати нових полів збереження та не змінювати структуру дії `request_info/pay_prisoner_ransom`.
  - Обмеження вікна пропозицій `offer_silver` і правила `payment_mode` залишаються без змін.

## Картка інформації про в’язня як повідомлення гравця з автоматичним запуском відповіді (v0.8.5)

- Коригування семантики повідомлення:
  - Картку інформації про в’язня перетворено на повідомлення гравця: `AddImageMessage(..., isPlayer=true, ...)`.
  - Як відправника використовується ім’я поточного переговорника нашої фракції (`ResolvePlayerSenderName`).
- Автоматична відповідь:
  - Після надсилання картки інформації про в’язня негайно повторно використовується ланцюжок дипломатичного запиту для запуску одного запиту відповіді AI.
  - Повторно використовуються наявні: `BuildChatMessages`, аналіз/перевірка контексту, `conversationController.TrySendDialogueRequest(...)`.
- Межі:
  - Як і раніше, підпорядковується шлюзу `CanSendMessageNow()`.
  - Не додавати нові поля до збережень і не змінювати контракт `request_info/pay_prisoner_ransom`.

## Реконструкція «+Надіслати повідомлення» в дипломатичній зоні надсилання та картки інформації про в’язня（v0.8.4）

- Точка входу UI:
  - У зоні введення дипломатичних повідомлень додано точку входу для звичайного тексту `RimChat_SendInfoEntry`（`+发送信息` / `+Send Info`）。
  - Після натискання відкривається легке `FloatMenu`, наразі лише з одним пунктом `RimChat_SendInfoMenuPrisoner`。
  - Доступність точки входу збігається з кнопкою надсилання; повторно використовується обмеження `SendGateState.CanSendNow`。
- Ручний виклик інформації про в’язня:
  - Додано метод ручної точки входу (всередині дипломатичного вікна): `TryStartManualPrisonerInfoSend()`。
  - Повторно використовується наявне діалогове вікно вибору в’язня: `Dialog_PrisonerRansomTargetSelector`。
  - До `StartRansomTargetSelection(...)` додано параметр: `emitSelectionPromptMessage = true`。
    - Ланцюжок дій AI залишається типовим (буде записано системне повідомлення «спершу виберіть в’язня»)。
    - Ручна точка входу передає `false`, безпосередньо відкриває діалогове вікно й не записує це повідомлення。
- Візуальна належність картки інформації про в’язня:
  - Семантика незмінна: повідомлення й надалі має системну семантику（`isPlayer=false`, не входить до семантичного ланцюга введення гравця）。
  - Візуальна переробка: коли спрацьовує `imageSourceUrl == \"rimchat://ransom-proof\"`, відображати його у візуальному стилі нашої сторони (праворуч, аватар нашої сторони, кольорова схема бульбашки нашої сторони)。
  - Додано візуальну перевірку:
    - `IsOutboundPrisonerInfoMessage(msg)`
    - `IsPlayerVisualMessage(msg)`
- Макет картки в’язня:
  - Інформаційна картка в’язня використовує окремий горизонтальний компактний макет (зображення ліворуч, текст праворуч).
  - Мініатюру змінено на `ScaleAndCrop`, щоб зменшити порожній простір; стратегію висоти й ширини бульбашки зменшено, щоб скоротити використання UI.

## Обмеження вікна пропозиції викупу та візуальний зворотний зв’язок（v0.8.4）

- Обмеження вікна пропозиції викупу залишаються без змін:
  - `offer_silver` має перебувати в поточному вікні `[negotiationBase*0.60, negotiationBase*1.40]`.
- Попередня візуалізація:
  - Після завершення `request_info(info_type=prisoner)` системне повідомлення доповнюється поточним діапазоном доступної пропозиції (min/max) і `currentAsk`.
- Повідомлення про помилку виходу за межі:
  - Якщо `pay_prisoner_ransom` виходить за межі, повертається зрозуміле повідомлення (із зазначенням `offered/min/max/currentAsk`), щоб спрямувати виправлення пропозиції в наступному раунді.
## Стабілізація ланцюжка попереднього request_info викупу（v0.8.3）

- Додано контракт дії:
  - `request_info(info_type)`
  - Перша версія підтримує лише `info_type=prisoner`, призначене для запитів попередньої інформації про викуп.
- Попередня перевірка перед дією викупу:
  - Перед виконанням `pay_prisoner_ransom` необхідно успішно завершити `request_info(info_type=prisoner)`.
  - Якщо попередню дію не завершено або немає дійсної прив’язаної цілі-в’язня, ланцюжок виконання негайно повертає системне повідомлення про відмову.
- Нормалізація на рівні аналізу:
  - Якщо модель повертає `pay_prisoner_ransom` з відсутніми або недійсними параметрами, `AIResponseParser` нормалізує дію до `request_info(prisoner)`.
- Нове поле стану сеансу під час виконання (не зберігається у грі):
  - `FactionDialogueSession.hasCompletedRansomInfoRequest`
  - Разом із наявним полем `isWaitingForRansomTargetSelection / boundRansomTargetPawnLoadId / boundRansomTargetFactionId` утворює повну машину попередніх станів.
- Очищення стану після оплати:
  - Після успішного виконання `pay_prisoner_ransom` скидаються попередній стан викупу та прив’язана ціль.
  - У разі невдачі стан не скидається, а контекст зберігається для продовження переговорів.
- Ключові точки журналювання:
  - Отримання request_info, кількість кандидатів, запуск діалогового вікна, завершення/скасування вибору, скидання стану після успішної оплати.


## Кореневе виправлення: явна кількість для десантування має пріоритет (v0.7.106)

- Застосовна дія: `request_item_airdrop(need, payment_items, scenario?, constraints?, budget_silver?(audit only), selected_def?(follow-up))`
- Зміна поведінки (визначення кількості):
  - Коли `need` містить явну кількість (наприклад, `50个干肉饼` / `50 pemmican`), на етапі виконання примусово використовуйте цю кількість як `count`; вона більше не залежить від LLM другого етапу, який повертає `count`.
  - Явно вказана кількість усе ще має відповідати єдиному допустимому діапазону: `count <= max_legal_count(hardMax)`; перевищення кількості й надалі спричиняє негайний fail-fast ( `selection_count_out_of_range` ).
- Виправлення промпту другого етапу:
  - Усунуто оманливе формулювання «single-item airdrop / count=1».
  - Чітке правило: якщо `need` має явно вказану кількість, її слід використовувати в першу чергу; інакше `count` має бути в межах `1..max_legal_count`.
- Поля аудиту:
  - `RequestItemAirdrop.Stage(selection)` текст `countSource` текст `llm|fallback_explicit|fallback_default_family`，текст.

## Асинхронізація другого етапу повітряного постачання ( v0.7.105 )

- Додано внутрішню асинхронну точку входу (ланцюжок підготовки дипломатичного повітряного постачання):
  - `GameAIInterface.BeginPrepareItemAirdropTradeAsync(...)`
  - Семантика: запускає асинхронний процес підготовки (перевірка оплати -> формування кандидатів -> розширення псевдонімів (необов’язково) -> вибір на другому етапі), негайно повертаючи результат постановки в чергу або миттєвий результат невдачі/успіху.
- Додано внутрішню точку входу для скасування:
  - `GameAIInterface.CancelItemAirdropAsyncRequest(requestId, cancelReason, error)`
  - Семантика: у разі закриття вікна або втрати контексту проактивно скасувати асинхронний запит на десант.
- Зовнішній контракт дії десанту не змінюється:
  - `request_item_airdrop(need, payment_items, scenario?, constraints?, budget_silver?(audit only), selected_def?(follow-up))`
- Зміни поведінки (на рівні ланцюжка):
  - Старий синхронний другий етап `Task.Wait(timeout)` видалено; він більше не блокує головний потік.
  - Тайм-аут другого етапу та розширення псевдонімів перевизначаються через `AIChatServiceAsync.SendChatRequestAsync(...requestTimeoutSecondsOverride, queueTimeoutSecondsOverride)` відповідно до налаштувань десанту.
  - У разі timeout/queue_timeout другого етапу все одно повертається `ItemAirdropPendingSelectionData` і запускається ланцюжок підтвердження кандидатів гравцем.

## Виправлення семантичної відповідності під час розбору оплати за десант (v0.7.104)

- Виправлення: `ItemAirdropPaymentResolver` додано рівень відповідності «повне включення семантичних токенів», що підтримує відмінності в порядку слів між CamelCase і словами тегів.
- Проблема, яку виправлено: `payment_item_unresolved` помилково повідомляє про помилку для таких введень, як `MealPackaged`.
- Правило сумісності: за однакової найвищої оцінки все одно повертається `payment_item_ambiguous` (кандидати Top3), зберігаючи fail-fast.

## Повне виправлення похідного бюджету десанту та видимості підказок (v0.7.103)

- Оновлення контракту `request_item_airdrop`:
  - Обов’язкові: `need`, `payment_items`
  - Необов’язкові: `scenario`, `constraints`
  - Необов’язкове поле аудиту: `budget_silver` (може бути передане у вхідних даних, але ігнорується під час виконання)
- Правила бюджету:
  - Бюджет під час виконання виводиться із суми ринкових цін `payment_items` `Floor`.
  - Виведений бюджет використовується для подальшого відбору кандидатів, обчислення дозволеної кількості, відображення вікна підтвердження та аудиту виконання.
  - Якщо передані `budget_silver` і виведений бюджет не збігаються, це лише записується в аудит (`RequestItemAirdrop.BudgetMismatch`) і не враховується під час визначення можливості виконання.
- Видимість взаємодії:
  - На другому етапі `selection_timeout` дипломатичний ланцюжок завжди додає системну підказку щодо кандидатів (TopN + вказівки для відповіді) і більше не залежить від того, чи порожні видимі репліки NPC.

## Кореневе виправлення тайм-ауту другого етапу аеродропу та семантичне розмежування (v0.7.102)

- Ланцюжок вибору на другому етапі переведено на структуровану відповідь (`AIChatClientResponse`):
  - До спостережуваних полів додано наскрізну передачу: `httpStatusCode/promptTokens/completionTokens/totalTokens/isEstimatedTokens/failureReason`.
- Розмежування семантики помилок другого етапу:
  - `selection_timeout`: тайм-аут локального вікна очікування або сервісний timeout.
  - `selection_queue_timeout`: семантика тайм-ауту черги (зберігається гілка «кандидат очікує підтвердження гравця»).
  - `selection_service_error`: сервісна помилка, не пов’язана з timeout (fail-fast).
- Стиснення промпту другого етапу: рядки кандидатів скорочено до `def/label/unit/max_legal_count`, а відображення обмежено першими 20 записами.
- Налаштування значень за замовчуванням: `ItemAirdropSecondPassTimeoutSeconds` за замовчуванням `25` (діапазон `3..30`).

## Виправлення кореневої причини розбору оплати за допомогою повітряного десанту та очікування підтвердження після тайм-ауту (v0.7.101)

- Оновлення ланцюжка розбору платіжних предметів (`request_item_airdrop.payment_items[].item`):
  - Порядок розбору фіксований: `defName 精确` -> `label 精确` -> `归一化强匹配` -> `近似匹配`.
  - Якщо найвищий бал мають кілька кандидатів, застосовується fail-fast: повертається `payment_item_ambiguous`, а повідомлення про помилку містить 3 найкращих кандидатів (Top3) (`defName(label)`).
  - `payment_item_unresolved` повертається лише за відсутності доступного збігу.
- Зміна семантики `selection_timeout`:
  - Після тайм-ауту другого етапу більше не укладається угода автоматично з кандидатом Top1.
  - Тепер повертаються дані, що очікують підтвердження, `ItemAirdropPendingSelectionData` (кандидати Top3), а дипломат UI очікує, доки гравець вибере кандидата, після чого повторно подає дію.
- Додано внутрішні моделі повернення (зворотна сумісність):
  - `ItemAirdropPendingSelectionData`
    - `needText`
    - `budgetSilver`
    - `failureCode` (`selection_timeout` або `selection_queue_timeout`)
    - `failureReason`
    - `options[]` (`index/defName/label/unitPrice/maxLegalCount`)
- Розширення параметрів дії (зворотна сумісність):
  - `request_item_airdrop` може приймати необов’язковий параметр `selected_def`, щоб гравець міг явно вказати кандидата після тайм-ауту та очікування підтвердження.
  - Обов’язкові параметри залишаються без змін: `need`, `budget_silver`, `payment_items`.
- Оновлення контракту промпту:
  - `payment_items.item` має означати «пріоритетно використовувати `defName`, а `label` використовувати як запасний варіант лише за єдиної можливості розбору».

## Блокування аеродропу за відсутності параметрів і виправлення контракту дії（v0.7.99）

- Fail-fast на етапі розбору（`AIResponseParser.AddActionIfValid`）:
  - Додати перевірку структури параметрів для `request_item_airdrop`;
  - У разі відсутності або недійсності безпосередньо відкидати дію, не передаючи її до ланцюжка виконання.
- Контракт `request_item_airdrop` (стислий каталог дій) виправити так:
  - `request_item_airdrop(need, budget_silver, payment_items, scenario?, constraints?)`
  - `need`: string, обов’язкове
  - `budget_silver`: int, обов’язкове, а також `> 0`
  - `payment_items`: array, обов’язкове; кожен елемент містить `item`(string) + `count`(int>0)
  - Обмеження оплати: загальна ціна `payment_items` має `>= budget_silver`, а переплата `<= 5%`
- Примітка: це виправлення узгоджується з визначенням дії `SystemPromptConfig`, усуваючи розбіжність у ланцюжку, коли «підказка каталогу спирається на старий контракт, а виконавець перевіряє за новим контрактом».

## Бартерний аеродроп AI + вікно остаточного підтвердження（v0.7.98）

- Оновлення контракту дії: `request_item_airdrop(need, budget_silver, scenario?, constraints?, payment_items)`
  - `need`: string, обов’язкове
  - `budget_silver`: int, обов’язкове, і `> 0`
  - `payment_items`: array, обов’язковий; кожен елемент повинен містити:
    - `item`: string (підтримує defName / label / псевдонім)
    - `count`: int (`> 0`)
  - `scenario`: необов’язкове, `general|trade|ransom`
  - `constraints`: необов’язкове, текстове обмеження
- Семантика виконання:
  - Ланцюжок дипломатичного діалогу змінено на «Prepare -> Confirm -> Commit».
  - На етапі Prepare лише створюється та перевіряється торговельний ордер; списання товарів і десант припасів не виконуються.
  - На етапі Confirm гравець підтверджує дію у спливному вікні; лише після підтвердження виконуються списання товарів і десант припасів.
  - Етап Cancel припиняє поточну дію та записує системне повідомлення; товари не списуються, припаси не десантуються.
- Правила оплати та бюджету:
  - `budget_silver` є авторитетним значенням бюджету.
  - Загальна ціна після перерахунку `payment_items` повинна `>= budget_silver`.
  - Верхню межу надбавки зафіксовано на рівні `5%` (у разі перевищення — fail-fast: `payment_overpay_too_high`).
  - Джерелом вилучення товарів можуть бути лише фактично доступні для торгівлі ресурси в зоні покриття «увімкненого залізничного маяка».
- Коди помилок fail-fast (додано/посилено):
  - `budget_required`
  - `payment_items_missing`
  - `payment_items_invalid`
  - `payment_item_unresolved`
  - `payment_item_ambiguous`
  - `payment_item_insufficient`
  - `payment_overpay_too_high`
  - `beacon_source_unavailable`
  - `player_negotiator_required` (етап підготовки дипломатичного діалогу)
- Поведінка UI:
  - Якщо в одному раунді діалогу з’являється кілька `request_item_airdrop`, усі їх відхилити та повернути повідомлення про помилку.
  - Додано багатомовні ключі для нових вікон підтвердження; усі тексти UI мають використовувати мовні ключі, а не бути захардкодженими.

## Керування затримкою першого раунду RPG та потокове мисленнєве дерево (v0.7.97)

- Мета: усунути тривале очікування першого раунду нового сеансу RPG, щоб уникнути блокування синхронними важкими завданнями в ланцюжку побудови промпту.
- Ключові зміни інтерфейсу:
  - Для `IPromptPersistenceService.BuildRPGFullSystemPrompt(...)` додано параметри:
    - `allowMemoryCompressionScheduling` (типово `true`)
    - `allowMemoryColdLoad` (типово `true`)
  - Для `RpgNpcDialogueArchiveManager.BuildPromptMemoryBlock(...)` додано параметри:
    - `allowCompressionScheduling` (типово `true`)
    - `allowCacheLoad`（за замовчуванням `true`）
  - `RpgNpcDialogueArchiveManager.HasPromptMemory(...)` Нові параметри:
    - `allowCacheLoad`（за замовчуванням `true`）
- Додано можливості виконання під час роботи:
  - `RpgNpcDialogueArchiveManager.BeginPromptMemoryWarmup(...)`: асинхронно прогрівати кеш архіву під час відкриття вікна.
  - Розширення `RpgPromptTurnContextScope`:
    - `AllowMemoryCompressionScheduling`
    - `AllowMemoryColdLoad`
- Поведінкові обмеження:
  - RPG Для нової сесії opening turn завжди `allowMemoryCompressionScheduling=false`, а також `allowMemoryColdLoad=false`.
  - Стиснення планується до виконання в безпечній точці головного потоку через чергу завдань прогрівання, щоб уникнути безпосереднього надсилання запитів фоновим потоком.

## Подвійне усунення першопричини: від дипломатичного наміру до дії（v0.7.96）

- Додано захист контракту дипломатичного виводу: `DiplomacyResponseContractGuard`
  - Правило: якщо у видимому діалозі є чітка обіцянка виконання (наприклад, «я організую/я вже подав/я зараз відправлю»), але не додано `{"actions":[...]}`, це вважається порушенням контракту.
  - Процес: перше порушення -> автоматично додати підказку для повторної спроби; якщо після повторної спроби порушення триває -> перейти до уточнювального запитання в межах ролі.
- У дипломатичному каналі `AIChatServiceAsync` додано ланцюжок повторної спроби за контрактом:
  - Підказка-тригер: `DIPLOMACY_CONTRACT_VIOLATION=...`
  - Поле спостереження: `contractValidationStatus/contractRetryCount/contractFailureReason`.
- До основного дипломатичного ланцюжка додано стратегію зіставлення намірів (`Dialog_DiplomacyDialogue.ActionPolicies`):
  - Охоплює відкладену дію: `request_item_airdrop/request_caravan/request_aid/request_raid/trigger_incident/create_quest`.
  - За нечітких нагадувань про замовлення (наприклад, «надішли ще раз» / «надішли запит» / «досі не отримав») спочатку перепитати для підтвердження, не виконувати дію безпосередньо.
  - Повторно виконати дію лише після однозначного підтвердження (наприклад, «підтверджую» / «замовляй» / «yes» / «confirm»).
  - Якщо бракує обов’язкових параметрів, продовжувати перепитувати; не дозволяється «домовленість на словах».
- Додано захист від повторів у короткому вікні:
  - Повторне виконання тієї самої дії з тими самими параметрами блокується протягом 2 раундів асистента.
- Додано дипломатичний робочий стан (не зберігається в архіві):
  - `FactionDialogueSession.pendingDelayedActionIntent`
  - `FactionDialogueSession.lastDelayedActionIntent`
  - `FactionDialogueSession.lastDelayedActionExecutionSignature`
  - `FactionDialogueSession.lastDelayedActionExecutionAssistantRound`

## Системне повідомлення про успішне скидання через дипломатичний інтерфейс (v0.7.95)

- Контракт зовнішніх дій не змінено: `request_item_airdrop(need, budget_silver?, scenario?, constraints?)`.
- Додано нове системне повідомлення про успішне виконання дипломатичної сесії:
  - Вставляється лише в ланцюжку дипломатичного діалогу та коли дію `request_item_airdrop` виконано успішно.
  - Шаблон системного повідомлення: `成功触发空投({0} x{1}@{2}银)` (китайською), англійською використовується відповідний ключ локалізації.
- Джерело даних змінено на структурований payload (без аналізу природної мови):
  - `ItemAirdropResultData.ResolvedLabel`
  - `ItemAirdropResultData.Quantity`
  - `ItemAirdropResultData.BudgetUsed`
- Пряма передача результату виконання:
  - У `Dialog_DiplomacyDialogue` всередині `ActionExecutionOutcome` додано `Data` для зберігання даних, повернутих дією, щоб їх могла використати система складання системних повідомлень рівня UI.
- Поведінка співіснування:
  - Зберігається наявний лист «Доставка з повітря прибула» (`RimChat_ItemAirdropArrivedTitle/Body`), а системне повідомлення лише додається й не замінює лист.

## Єдине джерело істини щодо допустимості кількості request_item_airdrop (v0.7.94)

- Контракт зовнішньої дії не змінюється: `request_item_airdrop(need, budget_silver?, scenario?, constraints?)`.
- Обмеження семантики одного предмета:
  - Якщо в `need` є кілька явно зазначених чисел, негайно виконати fail-fast: `need_count_ambiguous`.
  - Більше не виконувати «вгадування більшості цифр» або мовчазне обмеження значень.
- Єдиний розрахунок верхньої межі кількості:
  - Додано єдину функцію вікна: `ComputeLegalCountWindow(...)`.
  - `ValidateAirdropSelection`, резервне використання після тайм-ауту та відображення верхньої межі промпту другого етапу — усе повторно використовує цю функцію.
- Правило резервного використання після тайм-ауту другого етапу (Top1):
  - Явно вказану кількість і `requested > hardMax`: `selection_count_out_of_range`.
  - Без явно вказаної кількості: використати типове значення для групи (Food=25, Medicine=10, Weapon=1, Apparel=1, Unknown=5), потім `min(baseCount, hardMax)`.
- Уточнення промпту другого етапу:
  - Явно записати `BudgetSilver` і кожного кандидата `max_legal_count`.
  - Правило фіксоване: `count must be 1..max_legal_count for selected_def`.
- Покращення спостережуваності (етап `RequestItemAirdrop.Stage` для `selection`):
  - Додано: `countSource=llm|fallback_explicit|fallback_default_family`, `hardMax`, `maxByBudget`.

## Виправлення фільтрації каталогу кандидатів для повітряного скидання припасів (v0.7.92)

- Точка входу для побудови каталогу: `ThingDefCatalog.IsSpawnableItemDef(...)`
- Вміст виправлення:
  - Більше не виключати Def ресурсів глобально за `scatterableOnMapGen/mineable`.
  - Додано виключення на рівні каталогу `def.IsCorpse`, щоб Def трупів не домінували серед кандидатів і near-miss.
- Вплив:
  - Етап prepare для `request_item_airdrop`: `recordsScanned` і пошук кандидатів серед фракцій знову працюють нормально.
  - У діагностиці помилок `nearMisses` більше не має переважати над `Corpse_*`.

## Покращення пошуку кандидатів і діагностики для десанту ресурсів (v0.7.91)

- Контракт зовнішніх дій без змін: `request_item_airdrop(need, budget_silver?, scenario?, constraints?)`.
- Покращення на етапі prepare:
  - Спочатку локальне розширення синонімів, потім розширення псевдонімів AI (AI і далі є остаточним вибирачем предметів).
  - Вхідні токени підтримують змішане розділення та очищення від шуму, що покращує пошук таких виразів, як `steel10个`.
- Розширено поля спостережуваності (внутрішній аудит):
  - `recordsScanned`
  - `rejectedByBlacklist`
  - `rejectedByBlockedCategory`
  - `rejectedByFamily`
  - `rejectedByMatchScore`
  - `nearMisses`
- Аудит помилок: у разі помилки `no_candidates` або `need_family_unknown` додається діагностичний підсумок prepare, щоб швидко визначити, чи проблема у «вхідних даних», чи у «стратегії фільтрації».

## Видалення ланцюжка запитів Persona Bootstrap (v0.7.90)

- Обсяг: видаляються лише зовнішні запити `persona_bootstrap`, без видалення наявної структури даних persona та визначень переліку налагодження.
- Поведінка під час виконання:
  - `GameComponent_RPGManager.PersonaBootstrap.StartNpcPersonaGeneration(...)` більше не викликає `AIChatServiceAsync.SendChatRequestAsync(...)`.
  - Доповнення особистості NPC зберігає лише шлях синхронізації/копіювання RimTalk.
  - За відсутності RimTalk ланцюжок сканування bootstrap/runtime завершується за принципом fail-fast і більше не створює запитів.

## API повітряного десантування припасів（v0.7.89）

- Дія: `request_item_airdrop`（зовнішній контракт без змін）
- Ланцюжок виконання: `PrepareCandidates -> InternalSelectionLLM -> ValidateSelection -> ExecuteDrop`
- Обов’язкова стратегія:
  - Двоетапний вибір типово примусово ввімкнено (перемикача двоетапного режиму немає)
  - Зберегти загальний перемикач дій `EnableAIItemAirdrop`
  - Якщо список кандидатів у першому раунді порожній, автоматично один раз виконати розширення псевдонімів AI CN/EN, а потім повторно сформувати список кандидатів
  - Якщо фракцію розпізнано, але список кандидатів порожній, дозволити одну повторну спробу з послабленням у межах тієї самої фракції (не переходити між фракціями)
  - Якщо потребу неможливо класифікувати й після повторної спроби кандидатів усе ще немає, завершити за принципом fail-fast（`need_family_unknown`）
- Схема виводу двоетапного вибору (суворо):
  - `selected_def`（string）
  - `count`（int）
  - `reason`（string）
- Коди помилок (нові):
  - `need_count_ambiguous`
  - `need_family_unknown`
  - `selection_timeout`
  - `selection_json_missing`
  - `selection_selected_def_missing`
  - `selection_count_missing`
  - `selection_reason_missing`
  - `selection_out_of_candidates`
  - `selection_count_out_of_range`
- Сегменти аудиту:
  - `prepare`: підсумок побудови кандидатів
  - `selection`: результат вибору моделі другого етапу
  - `execute/failed`: результат розгортання або код помилки
- Спостереження налагодження:
  - Нове джерело: `AIRequestDebugSource.AirdropSelection`
  - Мітка запиту другого етапу: `channel:airdrop_selection`

## Десант припасів API（v0.7.86）

- Нова дія: `request_item_airdrop`
- Точка входу виконання: `RimChat.DiplomacySystem.GameAIInterface.RequestItemAirdrop(Faction faction, Dictionary<string, object> parameters)`
- Параметри:
  - `need`（string, обов’язковий）
  - `budget_silver`（int, необов’язковий, найвищий пріоритет）
  - `scenario`（string, необов’язковий: `trade|ransom|general`）
  - `constraints`（рядок, необов’язковий, наразі обробляється як текстове обмеження）
- Правила бюджету：
  - `budget_silver`（якщо надано） > `scenario=ransom` коли `colony_wealth * 1%` > `ItemAirdropDefaultAIBudgetSilver`
  - Остаточний бюджет обмежується діапазоном `[ItemAirdropMinBudgetSilver, ItemAirdropMaxBudgetSilver]`
- Дані, що повертаються (успіх)：
  - `selectedDefName`
  - `resolvedLabel`
  - `budgetUsed`
  - `quantity`
  - `dropCell`
- Семантика помилки (Fail Fast)：
  - Відсутня вимога, недійсний бюджет, спрацювання чорного списку, відсутній відповідний Def, кількість дорівнює 0 або відсутня допустима точка розміщення — негайна помилка з поверненням коду помилки

## Відповідь після завершення дипломатії та черга спостереження（v0.7.84）

- `RimChat.AI.AIRequestState`
  - Додано стани: `Queued`、`Cancelled`。
- `RimChat.AI.AIRequestResult`
  - Додано поля стану виконання：
    - `Source`
    - `Priority`
    - `EnqueuedAtUtc`
    - `QueueDeadlineUtc`
    - `StartedProcessingAtUtc`
    - `QueuePosition`
    - `AllowCallbacks`
    - `CancelReason`
    - `FailureReason`
- `RimChat.AI.AIRequestPriority`
  - Додано внутрішні перелічувані пріоритети: `Background`、`Interactive`。
- `RimChat.AI.AIChatServiceAsync`
  - `SendChatRequestAsync(...)`
    - Під час надсилання пріоритет запиту та метадані планування автоматично записуються відповідно до `AIRequestDebugSource`.
  - `CancelRequest(...)`
    - Додано необов’язкові параметри: `string cancelReason`, `string error`; типову поведінку збережено для сумісності зі старими версіями.
    - Після скасування запит переходить до `Cancelled`, а подальші зворотні виклики UI забороняються.
  - Оновлено семантику локальної черги з одним запитом у польоті:
    - Зберігається максимальна кількість одночасних операцій `1`.
    - Запити взаємодії на передньому плані мають пріоритет над фоновими запитами.
    - Для однакового пріоритету зберігається FIFO.
    - Якщо очікування в черзі перевищує `60s`, запит автоматично завершується помилкою; ключ помилки — `RimChat_ErrorQueueTimeout`.
  - `ProcessRequestCoroutine(...)`
    - Під час мережевого запиту система реагує на скасування та негайно `Abort()`.
    - Перед розсиланням зворотних викликів додано перевірки `AllowCallbacks` / `Cancelled`; скасовані та недійсні запити більше не передаватимуть stale callback до UI.
- `RimChat.DiplomacySystem.DiplomacyConversationController`
  - `CancelPendingRequest(...)` і ланцюжок заміщення в межах тієї самої сесії тепер передають чітку причину скасування (закриття вікна / superseded).
- `RimChat.Dialogue.DialogueDropPolicy`
  - Додано класифікатор причин втрати пакетів, який уніфіковано визначає, які dropped reason залишати лише у внутрішньому журналі, не показуючи їх гравцеві.
- `RimChat.UI.Dialog_DiplomacyDialogue`
  - Вікно дипломатії зчитує спільний стан запиту й розрізняє «у черзі» та «генерується».
  - Справжню помилку замінено на видиме повідомлення про помилку; повідомлення системи `RimChat_DialogueResponseDropped` більше не додається.

## Перший попередній перегляд Prompt Workspace із поетапною побудовою (v0.7.76)

- `RimChat.Persistence.PromptPersistenceService`
  - Додано:
    - `CreatePromptWorkspaceIncrementalPreviewBuild(RimTalkPromptChannel rootChannel, string promptChannel)`
    - `StepPromptWorkspaceIncrementalPreviewBuild(PromptWorkspaceIncrementalPreviewBuildState state)`
  - Призначення:
    - Використовується лише в робочому процесі попереднього перегляду deterministic preview у робочому просторі;
    - Поетапно будує `PromptWorkspaceStructuredPreview` (Init/Sections/Nodes/Finalize);
    - У разі помилки шаблону fail-fast переходить до `Failed`, зберігає завершені блоки та записує блок діагностики помилки.

- `RimChat.Persistence.PromptWorkspaceStructuredPreview`
  - Додано поля стану:
    - `IsBuilding`
    - `IsFailed`
    - `Completed` / `Total`
    - `CompletedSections` / `TotalSections`
    - `CompletedNodes` / `TotalNodes`
    - `Stage`（`PromptWorkspacePreviewBuildStage`）
    - `ErrorDiagnostic`（шаблон/канал/код помилки/рядок/стовпець/повідомлення）

- `RimChat.Config.RimChatSettings`（Робочий простір промптів）
  - `DrawPromptSectionWorkspace(...)`
    - Побудова інкрементального прев’ю виконується щокадрово з фіксованим бюджетом у 2 мс.
  - `GetPromptWorkspaceStructuredPreview(...)`
    - Видалено шлях синхронного повного `BuildPromptWorkspaceStructuredLayoutPreview(...)` під час першого відкриття;
    - Натомість повертається знімок інкрементального кешу (автоматична перебудова).
  - `InvalidatePromptWorkspacePreviewCache(...)`
    - Додатково очищається стан інкрементальної побудови, щоб запобігти залишковим даним між каналами.

- `RimChat.UI.PromptWorkspaceStructuredPreviewRenderer`
  - Угорі додано індикатор прогресу та відображення лічильників (загальний прогрес + прогрес section/node).
  - Додано відтворення блоку типу `Error` (червона область заголовка).
  - Як і раніше, оновлення кешу компонування виконується під керуванням `Signature`.

## Виправлення визначення фракції на комунікаційній панелі (v0.7.72)

- `RimChat.Patches.CommsConsolePatch`
  - `GetFloatMenuOptionsPostfix(...)`
    - Умову перехоплення змінено на «можна розібрати до дійсної цілі фракції».
    - Більше не покладається на ключові слова тегів (`call/contact/呼叫/联系`) для активації перехоплення.
  - `ExtractFactionFromOption(...)`
    - Спершу витягніть із «`FloatMenuOption.action`» замикання відбиття `Faction`.
    - Як другий пріоритет витягує збіг із label через `console.GetCommTargets(myPawn)`.
    - У крайньому разі перебирає `Find.FactionManager.AllFactionsListForReading` для пошуку збігу з label.
    - Більше не читає `Find.Selector.SingleSelectedThing`.
- Додано журнали налагодження:
  - `Comms option intercepted: pawn=..., faction=...`
  - `Comms menu patch found no faction options: ...`

## Журнал відмови у відкритті вікна дипломатії та блокування входу (v0.7.71)

- Уніфікована поведінка на рівні входу (відкриття вікна дипломатії):
  - Спочатку викликає: `DialogueWindowCoordinator.TryOpen(...)`
  - Якщо повертається `false`: записує `reason` і безпосередньо блокує відкриття вікна на рівні входу (`new Dialog_DiplomacyDialogue(...)`).
- Уже підключені точки входу:
  - `RimChat.Patches.FactionDialogRimChatBridgePatch`
  - `RimChat.Patches.CommsConsolePatch.CommsConsoleCallback`
  - `RimChat.UI.Dialog_SelectFactionForDialogue`
  - `RimChat.UI.MainTabWindow_RimChat`
  - `RimChat.NpcDialogue.ChoiceLetter_NpcInitiatedDialogue`
  - `RimChat.UI.Dialog_DiplomacyDialogue` (точка входу перемикання фракцій)
- Ключове слово журналу налагодження:
  - `Bridge dialogue open rejected`
  - `MainTab dialogue open rejected`
  - `Select-faction dialogue open rejected`
  - `NPC letter dialogue open rejected`
  - `Comms dialogue open rejected`
  - `Applying direct diplomacy open fallback`

## Уніфікована модель життєвого циклу діалогу (v0.7.70)

- Нові типи:
  - `RimChat.Dialogue.DialogueRuntimeContext`
  - `RimChat.Dialogue.DialogueContextResolver`
  - `RimChat.Dialogue.DialogueContextValidator`
  - `RimChat.Dialogue.DialogueRequestLease`
  - `RimChat.Dialogue.DialogueResponseEnvelope`
  - `RimChat.Dialogue.DialogueOpenIntent`
  - `RimChat.Dialogue.DialogueWindowCoordinator`
- Додано контролер:
  - `RimChat.Rpg.RpgDialogueConversationController`
    - `TrySend(...)`
    - `Cancel(...)`
    - `CloseLease(...)`
    - `TryApplyResponseEnvelope(...)`
- Оновлення дипломатичного контролера:
  - `RimChat.DiplomacySystem.DiplomacyConversationController.TrySendDialogueRequest(...)` Додано параметри:
    - `DialogueRuntimeContext runtimeContext`
    - `string ownerWindowId`
    - `Action<string> onDropped`
  - Додано `CloseLease(FactionDialogueSession session)`.
- У сервісі AI додано:
  - `RimChat.AI.AIChatServiceAsync.GetCurrentContextVersionSnapshot()`.
- RPG Оновлення рівня постійного зберігання менеджера:
  - `GameComponent_RPGManager` Додано поля постійного зберігання:
    - `pawnDialogueCooldownUntilTickById: Dictionary<string,int>`
    - `pawnPersonaPromptsById: Dictionary<string,string>`
  - Старі `pawnDialogueCooldownUntilTick` / `pawnPersonaPrompts` використовуються лише для міграції під час читання збережень і більше не записуються.

## RPG Впровадження контрактів дій і автоматичне керування пам’яттю（v0.7.67）

- `RimChat.Persistence.PromptPersistenceService.WorkbenchComposer`
  - `InjectRuntimeNodeBodies(...)`
    - RPG До каналу додано впровадження основного тексту `response_contract_node_template`:
      - Вихідна змінна: `dialogue.response_contract_body`
      - Джерело основного тексту: `BuildRpgApiContractText(...)`
  - `GetRequiredRuntimeNodeIds(...)`
    - RPG До обов’язкових вузлів під час виконання додано: `response_contract_node_template`（fail-fast）.
  - `BuildPromptNodePlacementsForCompose(...)`
    - Додано автоматичне доповнення allowed-node: якщо в макеті, налаштованому користувачем, бракує дозволених вузлів, автоматично додаються вузли макета за замовчуванням.

- `RimChat.Persistence.PromptPersistenceService.Hierarchical`
  - `ResolveRpgNodePlacements(...)`
    - Додано гілку `response_contract_node_template`, яка уніфікує візуалізацію вузлів RPG і поведінку під час виконання.

- `RimChat.UI.Dialog_RPGPawnDialogue.RequestContext`
  - `BuildRpgSystemPromptForRequest(...)`
    - Додано перевірку наявності контракту дії (лише `EnableRPGAPI=true`).
    - Якщо контракт відсутній: записати попередження в журнал і вимкнути «резервний варіант автоматичної пам’яті для цього раунду».

- `RimChat.UI.Dialog_RPGPawnDialogue.ActionPolicies`
  - `EnsureRpgActionFallbacks(...)`
    - Коригування поведінки: резервний варіант для виходу завжди зберігається; зіставлення автоматичної пам’яті та резервний варіант пам’яті можуть бути вимкнені керуванням цього раунду.
  - Одноразове керування автоматичною пам’яттю:
    - Автоматичні джерела (зіставлення співпраці/резервний варіант раунду/резервний варіант серії відсутності дій) можуть спрацювати не більше одного разу за сеанс.
    - Явна дія моделі `TryGainMemory` не враховується в цьому обмеженні.
  - Словник слів для намірів співпраці звужено:
    - Видалено короткі слова з високою неоднозначністю (наприклад, `okay` і односкладові слова підтвердження); тепер спрацювання відбувається за чіткими фразами-зобов’язаннями.

## Виправлення ізоляції архівів пам’яті NPC (v0.7.61)

- `RimChat.Memory.RpgNpcDialogueArchiveManager`
  - Аварійне завершення під час запису на диск:
    - `OnBeforeGameSave(...)`
    - `RecordTurn(...)`
    - `FinalizeSession(...)`
    - `RecordDiplomacySummary(...)`
    - Поведінка: якщо ідентифікатор архіву неможливо розібрати, негайно заблокувати запис на диск і вивести повідомлення про помилку в журнал; більше не дозволяти запис до спільного кошика `Default`.
  - Ланцюжок розбору назви архіву:
    - `ResolveCurrentSaveKey()`
    - `GetCurrentSaveName()`
    - Порядок розбору: `name/Name/fileName/FileName` -> евристичний пошук у будь-якому рядковому члені -> `ScribeMetaHeaderUtility.loadedGameName`.
  - ланцюжок міграції legacy:
    - `TryMigrateLegacyArchives(...)`
    - Під час першого входу до цільового збереження спочатку створити резервну копію legacy JSON у `Prompt/NPC/_migration_backup/...`, потім перенести її до каталогу поточного збереження та записати одноразову позначку міграції.
  - поля ізоляції профілів:
    - `RpgNpcDialogueArchive.SaveKey` (поле JSON: `saveKey`)
    - Під час читання приймати лише профіль із «saveKey поточного збереження» або legacy-профіль без `saveKey`.

## Зведення ланцюжка діалогу та рівні журналювання (v0.7.59)

- `RimChat.UI.Dialog_DiplomacyDialogue`
  - Класифікація невдалого виконання дій:
    - `ExpectedDenied` (cooldown / blocked / validation тощо — очікувані відмови): за замовчуванням записувати `Info`, не вважати це помилкою.
    - `UnexpectedFailure`: записувати `Warning`.
  - Якщо є успішна дія, очікувану відмову більше не додавати до системного повідомлення про помилку, щоб уникнути шуму подвійного стану «завдання виконано, але також повідомлено про помилку».
  - Підсумок невдалого діалогу враховує лише `UnexpectedFailure`, не забруднюючи його очікуваними відмовами.

- `RimChat.AI.AIChatServiceAsync`
  - Промпт повторної спроби `BuildRejectedInputFallbackMessages(...)` замінити на стислий контракт, щоб зменшити роздуття казенної та механічно багатослівної відповіді.

## Виправлення помилкового визначення ланцюжка доступності API (v0.7.58)

- `RimChat.Config.RimChatSettings`（`RimChatSettings_ApiUsability.cs`）
  - UI Модифікація входу:
    - `测试连通性` + `测试可用性` Дві кнопки поруч в одному рядку (50/50).
  - Розширення підсумку успішного виконання:
    - Додано відповідність оцінки швидкості тривалості виконання（`<500 极快`、`500-1499 快`、`1500-2999 正常`、`3000-5999 慢`、`>=6000 极慢`）。
    - Оцінка швидкості відображається лише для успішних результатів; формат підсумку невдалого результату не змінюється.
    - Якщо швидкість дорівнює `极慢`, додати повідомлення «Якість з’єднання низька, рекомендується змінити постачальника послуг».

- `RimChat.Config.ApiUsabilityDiagnosticService`
  - Зміни процесу `RunLocalDiagnosticCoroutine(...)`:
    - Локальний процес скорочено з 5 кроків до 4 (видалено крок блокування через недоступність локальної моделі).
    - Локальний ланцюжок більше не завершується невдачею безпосередньо через відсутність моделі у списку; доступність визначається остаточно за результатами чат-зондування та перевірки контракту відповіді.
  - Хмарний процес із 6 кроків залишається без змін (і надалі містить перевірку доступності моделі).

## API Подвійне тестування та діагностика доступності на сторінці налаштувань API（v0.7.57）

- `RimChat.Config.RimChatSettings`（`RimChatSettings_ApiUsability.cs`）
  - Додано UI/метод диспетчеризації:
    - `DrawQuickConnectivityTestButton(...)`
    - `DrawUsabilityTestButton(...)`
    - `DrawUsabilityTestResult(...)`
    - `StartUsabilityTest()` / `RunUsabilityTestCoroutine()`
  - Дії:
    - Зберегти `测试连通性` для швидкого зондування.
    - Додати `测试可用性` для поетапного глибокого тестування за принципом fail-fast.
    - Після невдалого глибокого тестування надати список рекомендацій, згортання технічних деталей і перехід до перегляду журналів.

- `RimChat.Config.ApiUsabilityDiagnosticService`
  - Додати основну точку входу:
    - `RunCloudDiagnosticCoroutine(ApiConfig, Action<ApiUsabilityProgress>, Action<ApiUsabilityDiagnosticResult>)`
    - `RunLocalDiagnosticCoroutine(LocalModelConfig, Action<ApiUsabilityProgress>, Action<ApiUsabilityDiagnosticResult>)`
  - Модель діагностичного виводу:
    - `ApiUsabilityDiagnosticResult`
    - `ApiUsabilityStepResult`
    - `ApiUsabilityStep`
    - `ApiUsabilityErrorCode`
  - Охоплення кодів помилок:
    - `AUTH_INVALID`
    - `ENDPOINT_NOT_FOUND`
    - `MODEL_NOT_FOUND`
    - `TIMEOUT`
    - `RATE_LIMIT`
    - `TLS_OR_CERT`
    - `DNS_OR_NETWORK`
    - `RESPONSE_SCHEMA_INVALID`
    - `LOCAL_SERVICE_DOWN`
    - `UNKNOWN`

- Взаємодія зі спостереженням API:
  - `RimChat.AI.AIRequestDebugSource` додати `ApiUsabilityTest`.
  - `Dialog_ApiDebugObservability.GetSourceLabel(...)` додати відображення джерел тестів доступності.

## Консолідація єдиного джерела істини для промптів API (v0.7.55)

- `RimChat.Config.RimChatSettings` (`RimChatSettings_RimTalkCompat.cs`)
  - `SetPromptSectionCatalog(...)`
    - Семантична зміна: перенесено спеціальний fail-fast вхід, виклик у штатному ланцюжку редагування заборонено.
  - `ImportLegacySectionCatalogToUnifiedCatalog(RimTalkPromptEntryDefaultsConfig sections, string sourceId, bool persistToFiles = true)`
    - Додано: односторонній імпорт legacy section -> unified через API.
  - `SetPromptSectionText(string promptChannel, string sectionId, string content, bool persistToFiles = true)`
    - Додано: редагування section у робочому столі уніфіковано із записом у unified.
  - `SetPromptUnifiedCatalog(PromptUnifiedCatalog catalog, bool persistToFiles = true)`
    - Додано `persistToFiles` для керування станами в пам’яті та на диску.
  - `SetPromptNodeText(...)` / `SetPromptNodeLayout(...)` / `SavePromptNodeLayouts(...)`
    - Додано параметр `persistToFiles`, що підтримує «редагування лише в пам’яті, явне збереження на диск».
  - `PersistUnifiedPromptCatalogToCustom()` / `HasPendingUnifiedPromptCatalogChanges()`
    - Додано: уніфікований інтерфейс керування станом змін catalog і явного збереження на диск.

- `RimChat.Config.PromptPresetChannelPayloads`
  - Зміна: вилучено штатне поле `PromptSectionCatalog`, єдиним штатним джерелом істини для payload залишено лише `UnifiedPromptCatalog`.
  - Сумісність: поле section у legacy payload усе ще можна імпортувати, але його більше не буде записано назад до штатного payload.

- `RimChat.Config.RpgPromptCustomConfig`
  - Зміна: вилучено офіційне поле `PromptSectionCatalog`.
  - Сумісність: одноразово імпортувати дані з legacy section через `RpgPromptCustomStore.LoadLegacyPromptSectionCatalogSnapshot()`.

- `RimChat.Persistence.IPromptPersistenceService` / `PromptPersistenceService`
  - Додано: `LoadConfigReadOnly()`
  - Додано: `RepairAndRewritePromptDomains()`
  - Обмеження: попередній перегляд робочого столу, оновлення UI та ланцюжок попереднього перегляду складання промпту мають використовувати `LoadConfigReadOnly()`.

## Покращення пресетів і редактора робочого столу промптів（v0.7.54）

- `RimChat.Config.PromptPresetStoreConfig`
  - Оновити `SchemaVersion` до `2`.
  - Додано `DefaultPresetId` для стабільної ідентифікації стандартного пресету лише для читання (більше не покладається на визначення за назвою).
- `RimChat.Config.IPromptPresetService`
  - Додано:
    - `bool IsDefaultPreset(PromptPresetStoreConfig store, string presetId)`
    - `bool EnsureEditablePresetForMutation(RimChatSettings settings, PromptPresetStoreConfig store, string selectedPresetId, string forkNamePrefix, out PromptPresetConfig editablePreset, out bool forked, out string error)`
- `RimChat.Config.PromptPresetService`
  - У ланцюжку нормалізації додано правило заповнення резервним значенням для стандартного пресета:
    - Спочатку зіставляти з canonical default payload;
    - За наявності кількох кандидатів обирати створений найраніше;
    - За відсутності кандидатів обирати пресет, створений найраніше;
    - Ні на якому етапі не вгадувати стандартний пресет за назвою.
  - Автоматичне розгалуження під час перейменування: `Custom yyyyMMdd-HHmmss` (за збігу назв автоматично додається суфікс).
- `RimChat.Config.RimChatSettings` (Робочий простір промптів)
  - Дії на панелі інструментів замінено на `Undo/Redo/Save/Reset`.
  - Undo/Redo реалізовано як стек текстової історії, ізольований за виміром `preset + channel + mode(section|node) + targetId`.
  - `Save` використовує примусовий `PersistPromptWorkspaceBufferNow()`; `Reset` діє лише на поточний редагований об’єкт (сегмент або вузол).
  - `PersistPromptWorkspaceBufferNow(..., persistToDisk:true)` синхронізує preset payload лише за фактичної зміни тексту; збереження без фактичних змін завершується мовчазно успішно.
  - Захист під час перемикання: перед перемиканням сегмента/каналу/вузла/пресета завжди виконується `PersistPromptWorkspaceBufferNow(force: true)`; у разі помилки перемикання переривається (fail-fast), щоб текст, не збережений на диск, не було перезаписано старим payload.
  - Семантика помилки синхронізації пресета: якщо синхронізація preset payload не вдалася, `PersistPromptWorkspaceBufferNow(...)` повертає `false` і зберігає стан pending; викликач зобов’язаний заблокувати подальше перемикання.
  - Список пресетів підтримує копіювання/видалення в рядку та перейменування подвійним клацанням; намір перейменувати стандартний пресет спочатку запускає автоматичне розгалуження, а потім перейменування.

## RPG PromptContext Виправлення кореневого прив’язування персонажа (Pawn)（v0.7.53）

- `RimChat.Prompting.RimTalkNativeRpgPromptRenderer`
  - `TryRenderRpgPrompt(string promptText, string promptChannel, DialogueScenarioContext scenarioContext, out string rendered, out RimTalkNativeRenderDiagnostic diagnostic)`
    - Додано вхідний параметр `promptChannel`, семантика каналу більше не визначається лише за tags.
    - Уніфіковано побудову кореневого прив’язування pawn для `PromptContext`: `CurrentPawn / Pawns / AllPawns / ScopedPawnIndex`.
- `RimChat.Prompting.RimTalkNativeRenderDiagnostic`
  - Додано поля:
    - `PromptChannel`
    - `CurrentPawnLabel`
    - `PawnCount`
    - `AllPawnCount`
    - `ScopedPawnIndex`
    - `RemainingTokensPreview`
- `RimChat.Memory.RpgNpcDialogueArchiveManager` (`Sessions` partial)
  - `BuildSessionSummaryRequestMessages(...)`
    - Контракт введення змінено на «за можливості надавати справжнього NPC/персонажа-співрозмовника».
    - Побудову сцени змінено на `CreateRpg(interlocutorPawn, npcPawn, false, ...)`, `CreateRpg(null, null, ...)` заборонено.

## RPG Зведення змінних нативного RimTalk до єдиного місця（v0.7.52）

- `RimChat.Prompting.RimTalkNativeRpgPromptRenderer`
  - `TryRenderRpgPrompt(string promptText, string promptChannel, DialogueScenarioContext scenarioContext, out string rendered, out RimTalkNativeRenderDiagnostic diagnostic)`
    - Під час RPG викликається на етапі фінального тексту під час виконання RimTalk нативний `ScribanParser.Render(...)`.
    - Відповідає за побудову нативного `PromptContext`, впровадження `VariableStore` / `ChatHistory` / `PawnContext` / `DialoguePrompt`, а також виведення структурованої діагностики.
- `RimChat.Prompting.RimTalkNativeRenderDiagnostic`
  - Поля:
    - `BoundMethod`
    - `PromptChannel`
    - `CurrentPawnLabel`
    - `PawnCount`
    - `AllPawnCount`
    - `ScopedPawnIndex`
    - `ContextBuilt`
    - `ErrorMessage`
    - `RemainingTokenCount`
    - `RemainingTokensPreview`
- `RimChat.Persistence.PromptPersistenceService`
  - `BuildUnifiedChannelSystemPrompt(...)`
    - Коли RPG кореневий канал працює в runtime і не є preview, додати RimTalk нативний етап повторного рендерингу.
  - `RenderRawModVariablesSection(...)`
    - Для RimTalk token змінити обробку на «зберегти/нормалізувати як raw token» і більше не споживати його на цьому етапі як локальне змодельоване значення.
  - `IsDiplomacyNativeVariablePassthroughSection(RimTalkPromptChannel rootChannel, string promptChannel, string templateId)`
    - Визначити section у каналі діалогу diplomacy (diplomacy_dialogue / proactive_diplomacy_dialogue), який потребує наскрізної обробки (не `mod_variables`).
    - Застосовується лише до каналів діалогу, кореневим каналом яких є Diplomacy; не поширюється на інші типи каналів.
  - `ShouldPassthroughRimTalkNativeToken(string normalizedToken)`
    - Розпізнавати RimTalk нативний змінний token. Перевіряти шлях простору імен `.rimtalk.` або token, зіставлений legacy із `.rimtalk.`.
  - `ExtractSectionIdFromTemplateId(string templateId)`
    - Витягти ідентифікатор section останнього сегмента з templateId.
  - `PreprocessDiplomacyNativeVariables(string template)`
    - Попередньо обробити текст шаблону в цільовому section каналу дипломатії, замінивши розпізнані нативні змінні token на текст RimTalk raw token.
    - Нерозбірливі нативні змінні зберігати як оригінальний текст token (WYSIWYG узгодженість із preview).
  - `RenderUnifiedTemplate(...)`
    - Для каналу діалогів diplomacy перед уніфікованим рендерингом Scriban спочатку викликати `PreprocessDiplomacyNativeVariables` для попередньої обробки нативних змінних.

## Виправлення ланцюжка знімків власних змінних RimTalk (v0.7.51)

- `RimChat.Prompting.PromptRuntimeVariableBridge`
  - `RefreshRimTalkCustomVariableSnapshot(bool force = false)`
    - Зчитувати `GetAllCustomVariables()` і виконувати регульоване оновлення (затримка відновлення за замовчуванням — 1000 мс).
    - Виводити журнал телеметрії знімка: `raw_count` / `parsed_count` / `duplicate_count` / `force`.
    - Якщо `raw_count > 0` і `parsed_count == 0`, блокувати ланцюжок Bridge за принципом fail-fast і записувати чітку помилку.
  - `GetCustomVariables()`
    - Замінити на «спроба оновлення перед зчитуванням + повернення знімка», щоб уникнути заморожування знімка під час першого циклу.
  - `ParseCustomVariable(object item)`
    - Підтримувати зчитування за двома протоколами: з полями tuple і з іменованими полями:
      - Назви: `Item1` / `VariableName` / `Name` / `LegacyName` / `Key`
      - ModId: `Item2` / `SourceModId` / `ModId` / `SourceId`
      - Опис: `Item3` / `Description` / `Desc` / `Tooltip`
      - Тип: `Item4` / `Kind` / `VariableKind` / `Type` / `Scope`
- `RimChat.Config.RimChatSettings`
- `RimChat.Config.RimChatSettings (RimTalkVariableBrowser partial)`
  - `EnsurePromptVariableSnapshotCacheFresh()`
    - Перед відновленням браузера спочатку оновіть знімок RimTalk, щоб ланцюжок відображення та ланцюжок ручної вставки залишалися узгодженими.

## Зміни мосту RimChat ↔ RimTalk API（v0.7.50）

- `RimChat.Prompting.PromptRuntimeVariableBridge`
  - `InitializeBridgeChain()`
    - Сувора оркестрація запуску мосту з негайним завершенням у разі помилки перевірки підпису.
  - `ValidateRimTalkBridgeSignaturesOrFail()`
    - Обов’язкові підписи:
      - `RimTalk.API.ContextHookRegistry.RegisterContextVariable(...)`
      - `RimTalk.API.ContextHookRegistry.GetAllCustomVariables()`
      - `RimTalk.API.ContextHookRegistry.UnregisterMod(string)`
      - `RimTalk.API.ContextHookRegistry.TryGetContextVariable(...)`
      - `RimTalk.Prompt.PromptContext`
      - `RimTalk.Prompt.VariableStore`
  - `RegisterRimChatSummaryVariable()`
    - Реєструє `rimchat_summary` через RimTalk контекстну змінну API.
  - `BuildRimChatSummaryAggregateText()`
    - Експортує стислий блок міжканального підсумку (ліміт 1200 символів).
  - `StrictLegacyCleanup()`
    - Видаляє застарілі артефакти мосту, зокрема старі ключі середовища виконання та старі записи модів у пресеті.
## Типовий `mod_variables` процес лише в ручному режимі（v0.9.72）

- `RimChat.Config.RimChatSettings`
  - `LoadRpgPromptTextsFromCustom()` більше не заповнює автоматично порожні розділи `mod_variables` під час завантаження налаштувань.
  - `BuildCanonicalSectionEntry(...)` більше не вставляє згенерований вміст із необробленими токенами в порожні записи `mod_variables` під час перебудови канонічного охоплення записів промпту.
- `RimChat.Config.RimChatSettings_PromptSectionWorkspace`
  - Робоча область промптів більше не замінює порожній текст редактора RPG `mod_variables` згенерованим вмістом списку змінних.
- `RimChat.Persistence.PromptPersistenceService`
  - Створення / інкрементальний попередній перегляд у робочій області більше не вставляє згенерований вміст `mod_variables`, якщо шаблон розділу порожній.
- `RimChat.Prompting.PromptRuntimeVariableBridge`
  - `BuildModVariablesSectionContent()` залишається доступним як допоміжний інструмент для ручних процесів вставлення в браузері; він більше не є частиною семантики автоматичного заповнення типових наборів.

  - `GetRimTalkCustomVariablesSnapshot()` / `RefreshRimTalkCustomVariableSnapshot()`
    - Створює знімок APIs для синхронізації користувацьких змінних RimTalk.
  - `BuildModVariablesSectionContent()`
    - Створює список необроблених токенів для ручних процесів вставлення в браузері/інструменті.
  - `ResolveRawToken(string variablePath)`
    - За потреби перетворює токен вставлення в браузері на необроблений токен RimTalk.

- `RimChat.Prompting.PromptVariableDisplayEntry`
  - Додані поля:
    - `RawToken`
    - `NamespacedToken`
    - `DefaultInsertToken`
  - Contract:
    - Браузер змінних відображає обидва треки токенів; вставка використовує `DefaultInsertToken` (політика пріоритету raw).

## create_quest — швидке завершення в разі помилки + розширення профілю RPG（v0.7.48）

- `AIActionExecutor.ExecuteCreateQuest(...)`
  - Тепер помилка перевірки повертає: початкову причину відмови + список дозволених `questDefName` для поточної фракції.
  - Політика поведінки: лише суворе швидке завершення в разі помилки, без переназначення псевдонімів і без резервної генерації завдань.
- `PromptTextConstants.QuestGuidanceNodeLiteralDefault`
  - Типовий шаблон вузла змінено на `{{ dialogue.quest_guidance_body }}`, щоб гарантувати динамічне додавання доступних завдань.
- `PromptPersistenceService.TryMigrateLegacyNodeBodyLiteralTemplates(...)`
  - Додано шаблон міграції для застарілих китайських жорстко закодованих літералів настанов щодо завдань до шаблону заповнювача для тіла під час виконання.
- Нова змінна промпту під час виконання:
  - `pawn.relation.social_summary`
  - Контракт: двосторонній соціальний підсумок для активної пари персонажів, що містить думку A->B / B->A, прямі взаємини, родинні зв’язки/романтичні стосунки та підказки щодо прихильності фракцій.
- Розширений контракт виводу змінної (канал RPG):
  - `pawn.target.profile`
  - `pawn.initiator.profile`
  - Додано поля: `Recent Job State`, `Needs` (керується перемикачем), `Visible Conditions` (керується перемикачем), `Recent Memories` (керується перемикачем).

## Змінні промпту + визначення персони（v0.7.47）

- Нова змінна:
  - `world.faction.description`
  - Джерело під час виконання: `FactionPromptManager.GetPrompt(currentFactionDefName)`.
  - Контракт значення: фактичний текст промпту фракції (типові шаблони + власні перевизначення).
- Оновлено визначення змінних:
  - `pawn.personality` тепер визначається через `GameComponent_RPGManager.ResolveEffectivePawnPersonalityPrompt(...)`.
  - Порядок визначення:
    1. Персона RimTalk (якщо доступна та придатна для читання)
    2. Збережена персона RimChat
    3. Зовнішній запит на початкове завантаження персони не надсилається
- Швидкі дії верстака промптів:
  - Кнопка швидкого доступу `Faction Prompt` тепер відкриває записи редактора шаблонів фракцій (`Dialog_FactionPromptEditor`).
  - Процес швидкого збереження `Persona Prompt` тепер автоматично намагається вставити `{{ pawn.personality }}` у `character_persona` поточного каналу (ідемпотентно).

## Дедуплікація профілів стосунків RPG + усунення обмеження kinship=no（v0.7.44）

- `RimChat.Persistence.PromptPersistenceService` (частково `Hierarchical`)
  - `ResolveRpgNodePlacements(...)`
    - Розміщення `rpg_kinship_boundary` тепер сумісне лише з макетом і більше не генерує окремий текстовий вивід.
  - `BuildRpgKinshipBoundaryGuidanceText(...)`
    - повертає `string.Empty`, коли `kinship == no`;
    - відображає текст правила межі лише коли `kinship == yes`.
- Оновлення ланцюжка шаблонів за замовчуванням:
  - `Prompt/Default/PawnDialoguePrompt_Default.json`
  - `Prompt/Default/PromptUnifiedCatalog_Default.json`
  - `RimChat/Config/RpgPromptDefaultsConfig.cs`
  - `RimChat/Config/PromptUnifiedDefaults.cs`
  - `rpg_relationship_profile` тепер використовує умовне відображення рядка підказки:
    - показувати рядок `Guidance` лише коли `dialogue.guidance` не порожній.
- Автоматична міграція уніфікованого каталогу:
  - `RimChat/Config/PromptUnifiedCatalog.cs`
  - Під час нормалізації вузлів шаблони застарілого формату:
    - `引导：{{ dialogue.guidance }}`
    - `Guidance: {{ dialogue.guidance }}`
    переносяться до форми умовних настанов ідемпотентним способом.
- Контракт поведінки під час виконання:
  - `kinship=no`: настанови щодо родинних меж не додаються, і жоден рядок настанов не відображається в профілі.
  - `kinship=yes`: настанови щодо родинних меж залишаються активними, але відображаються лише один раз (у профілі стосунків).

## Вікно спостереження + 30-хвилинний тренд токенів + кеш пам’яті промптів RPG（v0.7.43）

- `RimChat.UI.Dialog_ApiDebugObservability`
  - Додано дію в заголовку:
    - `TryOpenRimChatSettingsWindow()`
  - Behavior:
    - Відкриває `Dialog_ModSettings` для `RimChatMod`.
    - У разі недоступності екземпляра модуля негайно повертає локалізоване повідомлення про помилку.
- `RimChat.AI.AIChatServiceAsync` (`DebugTelemetry` частково)
  - Оновлено константи вікна налагоджувальної телеметрії:
    - `DebugWindowMinutes = 30`
    - `DebugBucketMinutes = 1`
    - `DebugRetentionMinutes = 35`
  - `BuildRequestDebugSnapshot(DateTime nowUtc)` тепер виконує агрегацію за один прохід:
    - копіює записи у межах вікна
    - формує щохвилинні кошики
    - обчислює підсумкові показники за той самий прохід
- `RimChat.Memory.RpgNpcDialogueArchiveManager`
  - Додано легковаговий засіб перевірки пам’яті:
    - `HasPromptMemory(Pawn targetNpc, Pawn currentInterlocutor = null)`
  - `BuildPromptMemoryBlock(...)` тепер використовує кеш у пам’яті на основі версій із ключами:
    - ідентифікатор цільового персонажа
    - ідентифікатор персонажа-співрозмовника
    - ліміт ходів підсумку
    - бюджет символів підсумку
  - Додано інвалідацію кешу пам’яті промпту під час операцій зміни архіву:
    - запис ходу
    - завершення сеансу
    - запис дипломатичного підсумку
    - повторне завантаження архіву
    - успішне або невдале стискання
- Контракт сумісності:
  - Жодних змін схеми збережень/API дротового контракту.
  - Жодних змін цільової поведінки Def/Harmony.

## Двоетапна фільтрація тегів Think（v0.7.42）

- Новий санітизатор:
  - `RimChat.AI.ModelOutputSanitizer`
  - `StripReasoningTags(string text)`
  - Контракт: видаляти повні блоки прихованого міркування (`<think>...</think>`, `<thinking>...</thinking>`), обрізати незавершені відкриті блоки та видаляти сторонні закривальні теги.
- Фільтрація вхідних даних на етапі сервісу:
  - `RimChat.AI.AIJsonContentExtractor.TryExtractPrimaryText(...)`
  - Зміна поведінки: текст кандидата санітизується до того, як його можна буде повернути чат-сервісам; кандидатів, порожніх після санітизації, відкидають.
- Фільтрація на етапі відображення:
  - `RimChat.AI.ImmersionOutputGuard.ValidateVisibleDialogue(...)`
  - Зміна поведінки: той самий санітизатор запускається перед розділенням на видимий текст і дії, тому шляхи відображення UI не можуть розкрити блоки think, навіть коли вміст оминає звичайний потік аналізу.
- Узгодження парсера дипломатії:
  - `RimChat.AI.AIResponseParser.NormalizeDialogueText(...)`
  - Зміна поведінки: видалення тегів think тепер є явним першим кроком перед обрізанням розділу стратегії та перевіркою занурення.
- Compatibility:
  - Схему дій не змінено.
  - Формат збережень не змінено.
  - Нового зовнішнього перемикача конфігурації немає.

## Аватар імені доповідача в дипломатичній бульбашці + доповнення даних про доповідача（v0.7.41）

- `RimChat.Memory.FactionDialogueSession`
  - `AddMessage(string sender, string message, bool isPlayer, DialogueMessageType messageType = DialogueMessageType.Normal, Pawn speakerPawn = null)`
  - `AddImageMessage(string sender, string caption, bool isPlayer, string imageLocalPath, string imageSourceUrl, Pawn speakerPawn = null)`
- `RimChat.Memory.DialogueMessageData`
  - Нові поля:
    - `string speakerPawnThingId`
    - `Pawn speakerPawn` (серіалізоване посилання)
  - Новий APIs:
    - `void SetSpeakerPawn(Pawn pawn)`
    - `Pawn ResolveSpeakerPawn()`
- `RimChat.UI.Dialog_DiplomacyDialogue` (поведінка мовця/аватара)
  - Після відкриття вікна старі повідомлення доповнюються даними мовця.
  - Резервний мовець гравця: найкращий персонаж колонії за навичкою «Соціальність», якщо перемовник недоступний.
  - Резервний мовець фракції: спочатку лідер, інакше — фіксований випадковий мовець для кожного сеансу.
  - Тепер під час компонування бульбашок резервуються місця для аватарів, а максимальна ширина становить 85% доступної доріжки.

## Бойове блокування «Персонаж↔Персонаж»（v0.7.40）

- `RimChat.Core.PawnCombatStateUtility`
  - `IsEitherPawnInCombatOrDrafted(Pawn first, Pawn second)`
  - `IsPawnInCombatOrDrafted(Pawn pawn)`
  - Єдина перевірка: спрацьовує `pawn.Drafted == true` або `pawn.CurJob?.def`
    - `JobDefOf.Wait_Combat`
    - `JobDefOf.AttackMelee`
    - `JobDefOf.AttackStatic`
    - `JobDefOf.UseVerbOnThing`
- `RimChat.Comp.CompPawnDialogue`
  - `CanShowRpgDialogueOption(...)` Додано двостороннє керування для бойового стану; якщо спрацьовує будь-яка зі сторін, точка входу до діалогу через праву кнопку миші не повертається.
- `RimChat.AI.JobDriver_RPGPawnDialogue`
  - На етапі ініціалізації `MakeNewToils(...)` `openDialogue` додано повторну перевірку fail-fast; після спрацьовування відкриття вікна негайно припиняється.

## Очищення заголовка компонування вузлів Prompt Workbench（v0.7.32）

- `RimChat.Config.RimChatSettings_PromptSectionWorkspace`
  - `DrawPromptWorkspaceNodeLayoutList(...)` Видалено верхній фіксований рядок заголовка списку `Body/正文`, залишено лише список редагованих елементів вузлів.
  - Ця зміна впливає лише на відображення панелі компонування вузлів і не змінює правила сортування та збереження вузлів.

## Порядок терміналів Body/ThoughtChain у Prompt Workbench（v0.7.31）

- `RimChat.Persistence.PromptPersistenceService.WorkbenchComposer`
  - `ComposePromptWorkspace(...)`Порядок складання блоків попереднього перегляду уніфіковано:
    - Вузли без ланцюжка міркувань（`metadata_after/main_chain_before/main_chain_after/dynamic_data_after/contract_before_end`，зберігають початковий взаємний порядок）
    - Агрегований блок основного тексту（`SectionAggregate/Body`）
    - Вузли ланцюжка міркувань（`thought_chain_node_template`）
    - Footer（`</prompt_context>`）
- `RimChat.Persistence.PromptPersistenceService.SectionAggregates`
  - Додано `AddPromptWorkspaceThoughtChainBlocks(...)`, призначений виключно для додавання блоків ланцюжка міркувань наприкінці.
  - `AddPromptWorkspaceNodeBlocks(...)` додає вимір фільтрації `includeThoughtChain`, повністю розділяючи рендеринг звичайних вузлів і рендеринг ланцюжка міркувань.
  - Логіку розпізнавання ланцюжка міркувань уніфіковано до `IsThoughtChainPlacement(...)`; залежність від фіксованого slot більше не використовується.
- `RimChat.Persistence.PromptPersistenceService.Hierarchical`
  - «Постфактум-визначення» для `ApplyResolvedNodePlacements(...)` також перемкнено на `IsThoughtChainPlacement(...)`, щоб узгодити його з правилами розпізнавання Workbench.

## Очищення порядку мітки + тіла Prompt Workbench（v0.7.30）

- `RimChat.Config.PromptUnifiedNodeSchemaCatalog`
  - Назви відображення трьох вузлів уніфіковано до міток із бізнес-семантикою:
    - `api_limits_node_template -> API Limits`
    - `quest_guidance_node_template -> Quest Rules`
    - `response_contract_node_template -> Response Contract`
- `RimChat.Persistence.PromptPersistenceService`
  - І попередній перегляд, і середовище виконання використовують те саме правило placement, розміщуючи `thought_chain_node_template` після об’єднаного блоку основного тексту.
- `RimChat.UI.PromptWorkspaceStructuredPreviewRenderer`
  - До заголовків вузлів попереднього перегляду більше не додається мітка slot; заголовок блоку основного тексту уніфіковано до `Body`, а підзаголовки сегментів відображають лише назву.

## Регресія шаблону вузла + тіла середовища виконання（v0.7.28）

- `RimChat.Config.PromptTextConstants`
  - `ApiLimitsNodeLiteralDefault` / `QuestGuidanceNodeLiteralDefault` / `ResponseContractNodeLiteralDefault` Від жорстко заданого тексту опису перейти до «багаторядкового шаблону вузла + тексту з змінними Scriban»:
    - `{{ dialogue.api_limits_body }}`
    - `{{ dialogue.quest_guidance_body }}`
    - `{{ dialogue.response_contract_body }}`
- `RimChat.Persistence.PromptPersistenceService.Hierarchical`
  - `ResolveDiplomacyNodePlacements(...)` і надалі використовувати старі джерела значень:
    - `AppendApiLimits(...)`
    - `AppendDynamicQuestGuidance(...) + AppendQuestSelectionHardRules(...)`
    - `AppendAdvancedConfig(...) / AppendSimpleConfig(...)`
  - `RenderPromptNodeTemplate(...)` Додати fail-fast: якщо body трьох сегментів під час виконання порожні, викидати `PromptRenderException(TemplateMissing)`, більше не мовчки виводити порожній вузол.
- `RimChat.Persistence.PromptPersistenceService`
  - `EnsurePromptTemplateDefaults(...)` Додати одноразову автоматичну міграцію: розпізнавати три старі шаблони з жорстко заданим текстом і переписувати їх у нові шаблони вузлів Scriban.
  - У разі успішної міграції записувати `Player.log`, щоб можна було перевірити, чи стару конфігурацію було автоматично виправлено.

## Узгодження контракту Social News JSON（v0.7.27）

- `Prompt/Default/PromptUnifiedCatalog_Default.json`
  - `social_news_style`: змінити на повний шаблон у стилі соціальних новин (зі змінними category/source/credibility/game language).
  - `social_news_json_contract`: примусово виводити повну структуру; обов’язкові ключі — `headline/lead/cause/process/outlook`, необов’язковий — `quote/quote_attribution`.
  - `social_news_fact`: змінити на структурований шаблон fact seed (містить `origin_type/source_faction/target_faction/summary/intent_hint/facts`).
- `RimChat.Config.PromptUnifiedDefaults`
  - `social_news_*` Вузол відкату більше не використовує спрощене жорстко закодоване значення, а посилається на константу стандартного соціального шаблону `PromptTextConstants`, щоб забезпечити відповідність відкату стандартним ресурсам.

## Strict Workbench WYSIWYG（v0.7.26）

- `RimChat.Persistence.PromptPersistenceService`
  - `BuildFullSystemPrompt(...)`：замінено на уніфікований виклик `BuildUnifiedChannelSystemPrompt(...)` (diplomacy runtime channel).
  - `BuildRPGFullSystemPrompt(...)`：замінено на уніфікований виклик `BuildUnifiedChannelSystemPrompt(...)` (rpg runtime channel).
  - `BuildDiplomacyStrategySystemPrompt(...)`：замінено на уніфікований виклик `BuildUnifiedChannelSystemPrompt(...)`（`diplomacy_strategy`）。
- `RimChat.Persistence.PromptPersistenceService.WorkbenchComposer`
  - `BuildUnifiedChannelSystemPrompt(...)`：режим складання змінено на deterministic（`deterministicPreview=true`）, відмінності через середовище виконання та динамічне введення даних вимкнено.
- `RimChat.Config.RimChatSettings_PromptSectionWorkspace`
  - `GetPromptWorkspaceStructuredPreview()`：попередній перегляд уніфіковано до повного вигляду макета `BuildPromptWorkspaceStructuredLayoutPreview(...)`.
- `RimChat.AI.AIChatServiceAsync`
  - `NormalizeRequestMessagesForProvider(...)`：для запитів лише до system більше не додається автоматично повідомлення `user`.
  - `ProcessRequestCoroutine(...)`：ланцюжок «переписування зі зниженням навантаження та повторної спроби» для HTTP 400 rejected-input видалено; надісланий payload більше не переписується вдруге.

## Узгодженість життєвого циклу уніфікованого каталогу промптів（v0.7.25）

- `RimChat.Config.PromptUnifiedNodeSchemaCatalog`
  - Додано вхід для суворої перевірки каналів вузлів:
    - `NormalizeStrictChannelOrThrow(promptChannel)`
    - `GetAllowedNodesStrict(promptChannel)`
    - `EnsureNodeAllowedForChannelOrThrow(promptChannel, nodeId, operation)`
  - Збережено сумісний вхід `GetAllowedNodes(...)`, глобальну поведінку `NormalizeLoose(...)` не змінено.
- `RimChat.Config.PromptUnifiedCatalog`
  - Оновлено поведінковий контракт (fail-fast):
    - `ResolveNode(...)`: неприпустимий `channel/nodeId` -> викидається `InvalidOperationException`.
    - `ResolveNodeLayout(...)`: неприпустимий `channel/nodeId` -> викидається `InvalidOperationException`.
    - `SetNode(...)` / `SetNodeLayout(...)`: неприпустимий `channel/nodeId` -> викидається `InvalidOperationException`.
  - Додано:
    - `NormalizeWithReport(fallback)` -> `PromptUnifiedCatalogNormalizeReport`
    - `ValidateInvariantsOrThrow()`
  - Уніфіковані поля звіту:
    - `RemovedNodeCount`
    - `RemovedLayoutCount`
    - `FilledDefaultLayoutCount`
    - `UnknownChannelCount`
    - `HasStructuralChange`
- `RimChat.Config.RimChatSettings_RimTalkCompat`
  - `EnsureUnifiedCatalogReady()` Зміну перевірки збереження на:
    - `legacyMigrationChanged || migrationVersionChanged || normalizeReport.HasStructuralChange`
  - Видалити стару перевірку кількості `requiresLayoutSave`.
  - Розподіл журналу: помилка блокування інваріанта `Error` + throw; підсумок очищення `Warning`; успішне доповнення/перенесення макета `Info`.

## Нормалізація повідомлень запиту + безпека прихильності фракцій（v0.7.24）

- `RimChat.AI.AIChatServiceAsync`
  - `SendChatRequestAsync(...)`
    - Перед надсиланням додати `NormalizeRequestMessagesForProvider(...)`:
      - role дозволено лише `system/user/assistant` (недійсні значення нормалізуються до `user`).
      - Якщо запит містить повідомлення, але не має дійсного повідомлення `user`, автоматично додати мінімальну інструкцію `user`.
  - `BuildRejectedInputFallbackMessages(...)`
    - Результат fallback повертається до єдиного процесу стандартизації, щоб запит повторної спроби також відповідав контракту повідомлень provider.
- `RimChat.AI.AIChatService`
  - `SendChatRequest(...)`
    - До синхронного ланцюжка запитів додано таку саму логіку стандартизації повідомлень, щоб уникнути запитів лише із system у старій точці входу.
- `RimChat.Memory.DialogueSummaryService`
  - `TryQueueLlmFallback(...)`
    - `messages` змінено з одного system на system+user.
    - `usageChannel` змінено з `Unknown` на зіставлення з `DialogueUsageChannel.Diplomacy/Rpg` через канал root.
- `RimChat.Memory.RpgNpcDialogueArchiveManager.Sessions`
  - `BuildSessionSummaryRequestMessages(...)`
    - `rpg_archive_compression` Запит змінено на system+user.
- `RimChat.Persistence.PromptPersistenceService.TemplateVariables`
  - `BuildCurrentFactionProfileVariableText(...)`
    - Зчитування прихильності змінено на `TryGetGoodwillTowardPlayer(...)` безпечний шлях: для фракції гравця або власної фракції повертається `N/A`, у разі помилки лише записується warning без викидання винятку.

## Захист каналу вузла промпту + нормалізація з негайним завершенням（v0.7.23）

- `RimChat.Config.PromptUnifiedNodeSchemaCatalog`
  - Додано інтерфейс білого списку каналів:
    - `GetAllowedNodes(promptChannel)`
    - `IsNodeAllowedForChannel(promptChannel, nodeId)`
  - Редагування вузлів і впровадження під час виконання узгоджено використовують білий список каналів як єдине джерело істини.
- `RimChat.Config.PromptUnifiedCatalog`
  - Для `ResolveNode(...)` і `ResolveNodeLayout(...)` додано перевірку допустимості каналу, заборонено читати вузли, що не належать цьому каналу.
  - Для `NormalizeNodes(...)` / `NormalizeNodeLayout(...)` додано очищення обмежень каналу:
    - Недопустимі вузли й компонування видаляються, а в журнал виводиться повідомлення про помилку (діагностика з негайним завершенням).
- `RimChat.Persistence.PromptPersistenceService.Hierarchical`
  - `GetOrderedNodeLayouts(promptChannel)` Змінено на фільтрацію за білим списком каналів із доповненням макета за замовчуванням; під час виконання макети вузлів між каналами більше не приймаються.
- `RimChat.Persistence.PromptPersistenceService.WorkbenchComposer`
  - Якщо макет користувача відсутній, макет вузлів за замовчуванням генерується за білим списком каналів, щоб уникнути забруднення ланцюжка попереднього перегляду вузлами з інших каналів.
- `RimChat.Config.RimChatSettings_PromptSectionWorkspace`
  - Вибір вузла та перемикання режиму Node фільтруються за білим списком поточного каналу;
  - Якщо канал вузла відсутній, виконується автоматичне повернення до режиму Section, що блокує недійсну точку входу для редагування.
- `RimChat.Config.RimChatSettings_RimTalkCompat`
  - `requiresLayoutSave` Визначення змінено з «загальної кількості вузлів» на «кількість дозволених каналом вузлів», щоб перемикання каналів не спричиняло постійного помилкового визначення міграції.

## Workbench WYSIWYG Composer Merge（v0.7.22）

- `RimChat.Persistence.PromptPersistenceService.WorkbenchComposer`
  - Додано уніфікований інтерфейс складання:
    - `BuildUnifiedChannelSystemPrompt(rootChannel, promptChannel, scenarioContext, environmentConfig, additionalValues, payloadTag, payloadText)`
  - Уніфіковано повернення готового єдиного system prompt, що містить:
    - Фіксований envelope（`<prompt_context>`）
    - Агреговане рендерення sections основного ланцюжка
    - компонування вузлів текст
    - ін’єкція payload（необов’язково）
- `RimChat.Persistence.PromptPersistenceService.SectionAggregates`
  - `BuildPromptWorkspaceStructuredSectionPreview(...)` і `BuildPromptWorkspaceStructuredLayoutPreview(...)` перевести на виклик єдиного composer.
  - Семантику попереднього перегляду Workbench змінити на детермінований рендеринг заповнювача зі стабільним і відтворюваним підписом.
- `RimChat.Config.PromptSectionSchemaCatalog`
  - Додати інтерфейси нормалізації каналів і визначення належності:
    - `GetAllWorkspaceChannels()`
    - `NormalizeWorkspaceChannel(...)`
    - `NormalizeRuntimePromptChannel(...)`
    - `DoesChannelBelongToRoot(...)`
    - `ResolveRootChannel(...)`
- `RimChat.Config.RimChatSettings_RimTalkCompat`
  - Оновити шлюз міграції Unified до `MigrationVersion=2`.
  - Додати одноразову точку входу міграції: імпортувати власні тексти legacy RPG і шаблони зображень legacy до Unified Catalog (разом із image alias).
- Переробити обхідних викликачів на єдиний system:
  - `RimChat.DiplomacySystem.Social.SocialNewsPromptBuilder`
  - `RimChat.DiplomacySystem.GameComponent_RPGManager.PersonaBootstrap`
  - `RimChat.Memory.DialogueSummaryService`
  - `RimChat.Memory.RpgNpcDialogueArchiveManager.Sessions`
- Ланцюжок зображень:
  - `RimChat.UI.Dialog_DiplomacyDialogue.ImageAction`
  - `RimChat.DiplomacySystem.ApiActionEligibilityService`
  - Єдиний розбір шаблонів виконується через `ResolvePromptTemplateAlias(...) / ResolvePreferredPromptTemplateAlias(...)`.

## Перевірка узгодженості промпту з виконанням у Runtime + структурований попередній перегляд（v0.7.21）

- `RimChat.Persistence.TemplateVariableValidationContext`
  - Додано модель контексту перевірки узгодженості з виконанням, яка централізовано керує «змінними, відомими під час виконання + змінними, що вводяться вузлами».
  - Workbench може створювати контекст за `rootChannel / promptChannel / sectionId|nodeId`, щоб забезпечити узгодженість перевірки з виконанням.
- `RimChat.Persistence.PromptPersistenceService.TemplateVariables`
  - Додано внутрішнє перевантаження:
    - `ValidateTemplateVariables(string templateText, TemplateVariableValidationContext validationContext)`
  - Старе публічне перевантаження зберігає сумісність і перенаправляє до реалізації через контекст.
- `RimChat.Persistence.PromptWorkspacePreviewModels`
  - Додано `PromptWorkspaceStructuredPreview`, `PromptWorkspacePreviewBlock`, `PromptWorkspacePreviewBlockKind`, `PromptWorkspacePreviewSubsection`.
- `RimChat.Persistence.PromptPersistenceService.SectionAggregates`
  - Додано:
    - `BuildPromptWorkspaceStructuredSectionPreview(rootChannel, promptChannel)`
    - `BuildPromptWorkspaceStructuredLayoutPreview(rootChannel, promptChannel, out placements)`
  - Порядок блоків попереднього перегляду фіксований: `Context -> Slot Nodes -> Main Sections -> Footer`.
  - Сегментовані блоки основного ланцюжка також виводять список підсегментів на рівні section для відтворення підзаголовків праворуч у попередньому перегляді.
- `RimChat.UI.PromptWorkspaceStructuredPreviewRenderer`
  - Додано легкий засіб структурованого відтворення попереднього перегляду, який кешує висоту макета за `signature + width`.
  - Блок `SectionAggregate` підтримує відтворення смуг підзаголовків за section і відповідного основного тексту.
- `RimChat.Config.RimChatSettings_PromptSectionWorkspace`
  - Додано дебаунс-автозбереження редагувань у робочій області: автоматичний запис на диск після 500 мс бездіяльності; під час перемикання каналу/сегмента/вузла/режиму та закриття вікна виконується примусовий запис на диск.
- `RimChat.UI.Dialog_PromptWorkbenchLarge`
  - Додано `PreClose()`, перед закриттям примусово викликає flush робочої області.

## Макет вузла промпту + інжектор слотів（v0.7.19）

- `RimChat.Config.PromptUnifiedCatalog`
  - `SchemaVersion` оновлено до `2`.
  - Додано `PromptUnifiedChannelConfig` `NodeLayout` (`List<PromptUnifiedNodeLayoutConfig>`).
  - Додано інтерфейси читання та запису макета вузлів:
    - `ResolveNodeLayout(promptChannel, nodeId)`
    - `SetNodeLayout(promptChannel, nodeId, slot, order, enabled)`
    - `GetOrderedNodeLayouts(promptChannel)`
- `RimChat.Config.PromptUnifiedNodeLayoutConfig`
  - Поля: `NodeId`, `Slot`, `Order`, `Enabled`.
  - `Slot` підтримує фіксовані 5 слотів (серіалізовані рядкові значення):
    - `metadata_after`
    - `main_chain_before`
    - `main_chain_after`
    - `dynamic_data_after`
    - `contract_before_end`
- `RimChat.Config.RimChatSettings_RimTalkCompat`
  - Додано уніфікований інтерфейс доступу до макета вузлів:
    - `GetPromptNodeLayouts(promptChannel)`
    - `ResolvePromptNodeLayout(promptChannel, nodeId)`
    - `SetPromptNodeLayout(promptChannel, nodeId, slot, order, enabled)`
    - `SavePromptNodeLayouts(promptChannel, layouts)`
- `RimChat.Persistence.PromptPersistenceService.Hierarchical`
  - Дипломатія / RPG / Три основні стратегічні ланцюжки змінено з фіксованого порядку на рендеринг за схемою «каркас + вставка слотів».
  - Порядок вставки фіксований: `slot -> order -> nodeId`.
  - Додано модель розміщення вузлів під час виконання: `ResolvedPromptNodePlacement`.
- `RimChat.Persistence.PromptPersistenceService.SectionAggregates`
  - Додано `BuildPromptWorkspaceLayoutPreview(rootChannel, promptChannel, out placements)` для повного попереднього перегляду робочого столу та відображення розміщення вузлів.

## Єдиний каталог промптів（v0.7.18）

- Додано єдину модель зберігання промптів:
  - `PromptUnifiedCatalog`（`Channels + Sections + Nodes + SchemaVersion + MigrationVersion`）
- Додано схему вузла:
  - `PromptUnifiedNodeSchemaCatalog`
  - Вузли під час виконання (наприклад, `fact_grounding`、`decision_policy`、`turn_objective`、`strategy_output_contract`、`social_news_*`) уніфіковано розбираються з node.
- Додано єдиний provider зберігання:
  - `PromptUnifiedCatalogProvider.LoadMerged()`
  - `PromptUnifiedCatalogProvider.SaveCustom(...)`
- Шлях зберігання:
  - За замовчуванням: `Prompt/Default/PromptUnifiedCatalog_Default.json`
  - Користувацьке: `Prompt/Custom/PromptUnifiedCatalog_Custom.json`
- Сумісна міграція:
  - Під час першого завантаження legacy `PromptSectionCatalog + PromptTemplates` автоматично зіставляється з unified catalog, а також записується позначка `legacyMigrated`.
- Підпис збірки для зовнішнього використання залишається незмінним:
  - `BuildFullSystemPrompt(...)`
  - `BuildDiplomacyStrategySystemPrompt(...)`
  - `BuildRPGFullSystemPrompt(...)`

## Швидкі дії персонажа Prompt Workbench（v0.7.17）

- `RimChat.Config.RimChatSettings_PromptSectionWorkspace`
  - У верхній частині Prompt Workbench додано швидкий доступ `派系提示词 / 人设提示词`.
  - Доступно лише під час `Current.Game != null && Current.ProgramState == Playing`; кнопка поза грою вимкнена й показує підказку.
- `RimChat.Config.RimChatSettings_PromptQuickActions`
  - Відповідає за перелік фактичних фракцій поточного збереження, а також персонажів-колоністів гравця, приручених тварин і механізмів.
  - Відповідає за меню конфліктів швидких дій, відкриття спрощеного редактора, фокусування сегмента `character_persona` після успішного збереження та повідомлення про результат.
- `RimChat.UI.Dialog_QuickPromptVariableRuleEditor`
  - Дає змогу швидко редагувати окреме правило швидкої дії, не відкриваючи повний редактор користувацьких змінних.
  - Під час збереження оновлюється лише правило; token автоматично не записується в основний текст поточного section.
- `RimChat.Prompting.UserDefinedPromptVariableService.QuickActions`
  - Додано фіксовані слоти змінних швидкого доступу:
    - `system.custom.quick_faction_persona`
    - `system.custom.quick_pawn_persona`
  - Повторно використовується єдиний ланцюжок перевірки й збереження `TrySaveEdit(...)`.
  - Якщо фіксований шлях уже зайнятий наявною користувацькою змінною, підтримуються два режими обробки `ReuseExisting / TakeOver`.
- `RimChat.Prompting.UserDefinedPromptVariableRuleMatcher`
  - `NameExact` додатково підтримує формат `thingid:*`, що дає змогу правилам швидкого доступу персонажа точно знаходити відповідний реальний екземпляр `ThingID`.

### Обмеження поведінки

- Точка швидкого доступу лише відповідає за «створення/оновлення правила змінної + відображення токена + перехід до рекомендованого фрагмента» і не записує дані в основний текст автоматично.
- Точка швидкого доступу фракції записує `Faction Rule`, а точка швидкого доступу персонажа — `Pawn Rule`.
- Пріоритет під час виконання не змінюється: `pawn exact -> pawn conditional -> faction -> default -> empty`.

## Єдиний набір правил користувацьких змінних і безпечний експорт особистості (v0.7.16)

- `RimChat.Config.UserDefinedPromptVariableConfig`
  - Кореневу модель конфігурації користувацьких змінних оновлено до `Id / Key / DisplayName / Description / DefaultTemplateText / Enabled`.
  - Зберігається сумісність зі старим полем серіалізації `templateText`; під час завантаження його автоматично переносять у `DefaultTemplateText`.
- `RimChat.Config.FactionPromptVariableRuleConfig`
  - Додано уніфіковану модель правил фракцій із персистентністю `Id / VariableKey / FactionDefName / Priority / TemplateText / Enabled / Order`.
- `RimChat.Config.PawnPromptVariableRuleConfig`
  - Додано уніфіковану модель правил персонажів із персистентністю `NameExact / FactionDefName / RaceDefName / Gender / AgeStage / TraitsAny / TraitsAll / XenotypeDefName / PlayerControlled / Priority / TemplateText / Enabled / Order`.
- `RimChat.Config.FactionScopedPromptVariableOverrideConfig`
  - Збережено як застарілу модель сумісності конфігурації лише для завантаження; після читання її дані автоматично переносяться до нового списку правил фракцій, а під час збереження застарілі поля більше не записуються.
- `RimChat.Prompting.UserDefinedPromptVariableService`
  - Відповідає за уніфіковане перенесення правил, нормалізацію, перевірку під час збереження, перевірку циклічних залежностей, сканування посилань, точку входу змінних офіційних прикладів і розбір правил під час виконання.
  - Порядок спрацьовування правил фіксований: `pawn exact -> pawn conditional -> faction -> default -> empty`.
  - Порядок сортування на одному рівні фіксований: `Priority desc -> Specificity desc -> Order asc`.
- `RimChat.Prompting.UserDefinedPromptVariableRuleMatcher`
  - Відповідає за зіставлення правил персонажів/фракцій, оцінювання специфічності, створення міток рівня спрацьовування та стислого опису умов.
- `RimChat.Prompting.UserDefinedVariableProvider`
  - І надалі використовується як `IPromptRuntimeVariableProvider` для підключення до `PromptRuntimeVariableRegistry`, але під час виконання розбір делегується уніфікованому сервісу правил.
  - Після завершення рендерингу всіх змінних `system.custom.*` додається ланцюжок перевизначень effective export для `pawn.personality`.
- `RimChat.Persistence.PromptPersistenceService`
  - Основний шлях рендерингу промпту й надалі отримує дійсне експортоване значення `pawn.personality` через конвеєр provider.
- `RimChat.DiplomacySystem.GameComponent_RPGManager`
  - RimTalk шлях рендерингу шаблону копії персони також підключено до уніфікованого сервісу користувацьких змінних, щоб забезпечити `pawn.personality` можливість бачити ефективну особистість.
- `RimChat.UI.Dialog_UserDefinedPromptVariableEditor`
  - Редактор оновлено до структури «Основна інформація / Шаблон за замовчуванням / Список правил», а список правил розділено на дві вкладки: `Faction Rules` і `Pawn Rules`.
- `RimChat.Config.RimChatSettings_RimTalkVariableBrowser`
  - У браузері змінних додано точки входу для створення «порожньої змінної + офіційної демонстраційної змінної».

## Консолідація виконання дипломатичного промпту + огортка секції на кшталт XML（v0.7.14）

- `RimChat.Persistence.PromptPersistenceService`
  - Єдиною офіційною точкою входу для фінального дипломатичного system prompt і надалі є `BuildFullSystemPrompt(...)`, але під час виконання `GlobalSystemPrompt / GlobalDialoguePrompt` більше не читається як офіційне джерело складання.
  - Додано `BuildDiplomacyStrategySystemPrompt(...)` для окремого одиничного system prompt у ланцюжку стратегічних рекомендацій.
  - `LoadConfig()` / `SaveConfig()` тепер записують старі дипломатичні поля назад як compatibility mirror і, якщо каталог секцій усе ще має значення за замовчуванням, одноразово намагаються імпортувати legacy дипломатичний prompt.
- `RimChat.Prompting.Builders.DiplomacyPromptBuilder`
  - І надалі відповідає за складання та диспетчеризацію звичайного дипломатичного system prompt; семантику результату змінено на «єдина дозволена точка входу дипломатичного виконання».
- `RimChat.Prompting.Builders.DiplomacyStrategyPromptBuilder`
  - Додано спеціальний builder для рекомендацій щодо стратегій, який окремо виводить контракт стратегії JSON, контекст перемовника, пакет фактів, досьє сценарію та основний ланцюжок секції `diplomacy_strategy`.
- `RimChat.Persistence.DiplomacyStrategyPromptContext`
  - Додано DTO для зберігання ексклюзивних для стратегічних запитів текстових блоків контексту перемовника, пакета фактів і досьє сценарію.
- `RimChat.Persistence.PromptPersistenceService.Hierarchical`
  - Під час роботи дипломатії/RPG вузол `GlobalSystemPrompt / GlobalDialoguePrompt` більше не вставляється в остаточний промпт.
  - `main_prompt_sections` тепер є дочірнім вузлом секції, подібним до справжнього XML, а не текстовим блоком `[SECTION: ...]`.
  - Готовий промпт під час виконання більше не виводить вихідні мітки `[CODE]` / `[FILE]`.
- `RimChat.Prompting.PromptHierarchyRenderer`
  - Ієрархічний рендеринг тепер остаточно зводиться до виводу, подібного до XML; `UseHierarchicalPromptFormat` зберігається лише як сумісне дзеркальне поле для застарілих даних.
- `RimChat.AI.AIChatServiceAsync`
  - Видалено логіку додавання `Think step by step.` / `Review your rules.` перед надсиланням; мережевий рівень тепер лише передає результати builder без змін.
- `RimChat.UI.Dialog_DiplomacyDialogue`
  - До звичайних дипломатичних запитів більше не додається другий system `PLAYER NEGOTIATOR CONTEXT`.
- `RimChat.UI.Dialog_DiplomacyDialogue.Strategy`
  - Запити стратегічних рекомендацій змінено так, щоб надсилати лише 1 окремий strategy system prompt, а потім історичні повідомлення та фінальну user фразу-тригер.
- Активи за замовчуванням:
  - `Prompt/Default/PromptSectionCatalog_Default.json`
    - Стає офіційним основним джерелом тексту правил дипломатії/стратега.
  - `Prompt/Default/SystemPrompt_Default.json`
    - `GlobalSystemPrompt` змінено на текст compatibility mirror промпту; він більше не містить офіційний пакет правил для виконання під час роботи.

## Консолідація системи змінних промптів за замовчуванням + канал робочого простору соціального кола（v0.7.13）

- `RimChat.Config.PromptSectionSchemaCatalog`
  - У робочому підканалі section під root дипломатії додано `social_circle_post`; тепер він бере участь у редагуванні section та попередньому перегляді aggregate.
- `RimChat.Config.SocialCirclePromptDefaultsProvider`
  - Додано провайдер шаблонів соціального кола за замовчуванням, який уніфіковано зчитує з `Prompt/Default/SocialCirclePrompt_Default.json` шаблони новин соціального кола та визначення дій за замовчуванням `publish_public_post`.
- `RimChat.Config.PromptTextConstants`
  - Описи/параметри/вимоги `publish_public_post` і значення шаблонів новин соціального кола за замовчуванням більше не використовують константи коду як довгострокове джерело; натомість вони делегуються файлу конфігурації за замовчуванням.
- `RimChat.Prompting.PromptEntryStaticTextCatalog`
  - Стандартні абзаци основного ланцюжка дипломатії більше не містять дубльований fallback-текст; тепер спочатку розбирається `PromptSectionCatalog` / стандартний каталог section.
- `doc/PromptVariableGapReport.md`
  - Додано список відсутніх лише для читання змінних, який фіксує семантичні позиції зі старого default prompt, що ще не були включені до наявної системи змінних із просторами імен.

## Посилення перевірки недійсних просторів імен у редакторі промптів（v0.7.12）

- `RimChat.Persistence.PromptPersistenceService.ValidateTemplateVariables(...)`
  - У режимі редагування до контексту перевірки передаються лише шляхи змінних із п’яти просторів імен `ctx / pawn / world / dialogue / system`.
  - Недійсні простори імен або незавершені змінні, введені вручну, більше не спричиняють помилку на рівні сторінки, а залишаються в результатах діагностики як невідомі змінні.

## Виправлення маршрутизації вставлення змінних у робочому просторі промптів（v0.7.11）

- `RimChat.Config.RimChatSettings_PromptSectionWorkspace`
  - Колбек вставлення панелі змінних у правій частині робочого простору тепер безпосередньо викликає `TryInsertVariableTokenToPromptWorkspace(...)`, а ціллю завжди є текст поточного розділу.
  - Перевірку можливості вставлення `TryInsertVariableTokenToPromptWorkspace(...)` змінено на таку, що ґрунтується на дійсному `promptChannel + sectionId` поточного робочого простору, без залежності від застарілого стану індексу вкладки.

## Посилення кешування попереднього перегляду робочого столу промптів（v0.7.10）

- `RimChat.UI.PromptWorkbenchChipEditor`
  - `DrawReadOnly(...)` тепер кешує лише для читання результати компонування сегментів; ключ кешу — «вихідний текст + ширина області перегляду».
  - Вміст кешу охоплює: блоки сегментів, висоту кожного сегмента та загальну висоту вмісту, щоб уникнути повторного `ParseSegments(...)` і `CalcHeight(...)` під час прокручування/наведення курсора.
- `RimChat.Config.RimChatSettings_PromptSectionWorkspace`
  - Текст aggregate-прев’ю поточного каналу тепер кешується на рівні робочого столу.
  - Умови недійсності: зміна тексту section, зміна кореневого каналу, зміна prompt channel, заміна всього `PromptSectionCatalog`.

## Автономне відображення змінної у власному рядку в попередньому перегляді Prompt Workbench（v0.7.9）

- `RimChat.UI.PromptWorkbenchChipEditor`
  - `DrawReadOnly(...)` переходить на режим відображення лише для читання по сегментах і більше не використовує схему редагування «цілий блок TextArea + підсвічування перекриттів фрагментів».
  - Звичайні текстові сегменти й надалі автоматично переносяться відповідно до поточної ширини області перегляду.
  - Знайдений сегмент змінної `{{ namespace.path }}` виноситься в окремий блок відображення, примусово відокремлюється до та після й зберігає підтримку підказок.
  - Послідовні змінні відображаються за правилом «одна змінна — один рядок»; поведінка `Draw(...)` у внутрішньому редакторі не змінюється.

## Безпечне базове покриття Scriban для RimTalk（v0.7.6）

- `RimChat.Prompting.RimChatCoreVariableProvider`
  - Додано каталог безпечних змінних: `world.time.hour/day/quadrum/year/season/date`、`world.weather`、`world.temperature`、`pawn.recipient`、`pawn.recipient.name`.
- `RimChat.Persistence.PromptPersistenceService`
  - `ResolveTemplateVariableValue(...)` отримує вбудований розбір часу/пори року/погоди/температури/псевдоніма одержувача, усе безпосередньо зчитується з поточних `Map`、`TickManager`、`DialogueScenarioContext.Target`.
- Межі сумісності:
  - Зберігається контракт простору імен strict; не відновлюються RimTalk — його `Find`、`settings`、статичні класи, службові функції та нечутливий до регістру доступ.
  - Наявні сумісні змінні `dialogue.rimtalk.*` / `pawn.rimtalk.context` і надалі зберігаються, попередня поведінка не змінюється.

## Маршрутизація великого вікна вкладки Prompt（v0.7.5）

- `RimChat.Config.RimChatSettings_PromptAdvancedFramework`
  - `OpenPromptWorkbenchWindow(...)` змінено з «перемикання вкладки на сторінці налаштувань» на «відкриття окремого вікна великого розміру».
  - Перед відкриттям перевіряється, чи вже існує `Dialog_PromptWorkbenchLarge`, щоб уникнути повторного накладання однотипних вікон.
- `RimChat.UI.Dialog_PromptWorkbenchLarge`
  - Додано спливне вікно робочої області Prompt великого розміру; початковий розмір вікна адаптується до `90%` екрана з обмеженнями мінімального й максимального розміру.
  - Вміст спливного вікна повторно використовує `RimChatSettings.DrawTab_PromptSettingsDirect(...)`, не змінюючи наявний ланцюжок відтворення робочої області з розділеними промптами.
- `RimChat.Config.RimChatSettings`
  - Рівень доступу `DrawTab_PromptSettingsDirect(Rect rect)` підвищено до `internal`, щоб безпечно повторно використовувати його в окремому спливному вікні.

## Фінальне завершення сумісності Prompt（v0.7.0）

- `RimChat.Config.RimChatSettings`
  - В офіційних live-полях залишаються лише `PromptSectionCatalog`, `RimTalkSummaryHistoryLimit`, `RimTalkPersonaCopyTemplate`, `RimTalkAutoPushSessionSummary`, `RimTalkAutoInjectCompatPreset`.
  - Старий `EnableRimTalkPromptCompat / RimTalkCompatTemplate / RimTalkDiplomacy / RimTalkRpg / RimTalkChannelSplitMigrated` під час завантаження перетворюється на тимчасовий legacy payload, використовується лише для імпорту й більше не бере участі в офіційному збереженні та стабільному UI.
- `RimChat.Config.PromptLegacyCompatMigration`
  - Оновити до єдиної точки імпорту legacy, яка уніфіковано обробляє чотири типи старих payload: settings / preset / bundle / custom store.
  - Додати `LegacyPromptMigrationReport` і `LegacyPromptMigrationEntry`, а результат останнього імпорту перезаписувати в `Prompt/Reports/LegacyPromptMigrationReport.json`.
- `RimChat.Config.PromptSectionSchemaCatalog`
  - Уніфіковано оголосити фіксовану схему основного ланцюга з 8 секцій, а також основний ланцюг prompt channel, доступний для редагування у стабільній робочій області сторінки Prompt.
- `RimChat.Prompting.PromptSectionAggregateBuilder`
  - Додати canonical aggregate builder, який безпосередньо генерує aggregate секцій поточного prompt channel за `PromptSectionCatalog` для спільного використання runtime і попереднім переглядом UI.
- `RimChat.Persistence.PromptPersistenceService.SectionAggregates`
  - Дипломатія та основний ланцюг RPG тепер уніфіковано відтворюють один canonical section aggregate і вставляють його лише один раз у hierarchical builder.
- `RimChat.Config.RimChatSettings_PromptSectionWorkspace`
  - Стабільною точкою входу сторінки Prompt тепер є робоча область `root channel -> prompt channel -> sectionId`.
  - Підтримується лише відновлення типових значень для section / поточного prompt channel; вставлення змінних записує текст лише поточної сфокусованої секції, а попередній перегляд показує canonical aggregate поточного каналу.
- `RimChat.Config.RimChatSettings_RimTalkBridgePage`
  - На сторінці RimTalk залишити лише `Bridge / Variables / Summary & Persona`.
- `RimChat.Prompting.PromptVariableDisplayEntry`
  - Змінну UI уніфіковано змінити на нейтральну модель відображення: `path / scope / sourceId / sourceLabel / availability / description`.
- Стандартні активи:
  - Головна точка входу: `Prompt/Default/PromptSectionCatalog_Default.json`
  - Резервний варіант сумісності з однією версією: `Prompt/Default/RimTalkPromptEntries_Default.json`

## RimTalk Провайдер мосту змінних (v0.6.35)

- `RimChat.Prompting.IPromptRuntimeVariableProvider`
  - Новий обов’язок: окрім `GetDefinitions()` і `PopulateValues(...)`, також підтримує `TryMapLegacyToken(...)`, щоб переписувати старі токени в простір імен RimChat.
- `RimChat.Prompting.RimTalkVariableProvider`
  - Надає перший набір змінних мосту старої семантики:
    - `pawn.rimtalk.context`
    - `dialogue.rimtalk.prompt`
    - `dialogue.rimtalk.history`
    - `dialogue.rimtalk.history_simplified`
  - Якщо RimTalk API зареєстрував змінні context/pawn/environment, їх буде автоматично зіставлено з:
    - `dialogue.rimtalk.*`
    - `pawn.rimtalk.*`
    - `world.rimtalk.*`
- `RimChat.Prompting.RimTalkMemoryPatchVariableProvider`
  - Відповідає за приймання зареєстрованих змінних, у яких `modId/name` збігається з `memorypatch`, і зіставляє їх за правилами того самого простору імен.
- Правила міграції:
  - `{{context}} -> {{pawn.rimtalk.context}}`
  - `{{prompt}} -> {{dialogue.rimtalk.prompt}}`
  - `{{chat.history}} -> {{dialogue.rimtalk.history}}`
  - `{{chat.history_simplified}} -> {{dialogue.rimtalk.history_simplified}}`
  - `{{json.format}} ->` мігрує в текст інструкції JSON, отриманий під час поточного розбору
- Правила UI:
  - Засіб вибору змінних і браузер змінних тепер відображають `SourceLabel` і стан залежностей;
  - Якщо provider наразі недоступний, змінна все одно може відображатися, але позначається як «Відсутня залежність під час виконання».

## Власна міграція каталогу секцій промпта（v0.6.34）

- `RimChat.Config.RimTalkPromptEntryDefaultsConfig`
  - Тепер також виконує роль власної моделі конфігурації секцій, додаючи можливості clone / `IExposable` / `SetContent(...)`, і слугує уніфікованим носієм даних для `PromptSectionCatalog`.
- `RimChat.Config.PromptLegacyCompatMigration`
  - Додано `NormalizePromptSections(...)`, `CreateLegacyAdapterFromPromptSections(...)`, `ApplyLegacyAdapterToPromptSections(...)`.
  - legacy `PromptEntries` / `CompatTemplate` тепер дозволено імпортувати лише до каталогу section; якщо шаблон містить невідомі необгорнуті змінні або забруднений уже відрендерений промпт, міграцію буде відхилено та виконано повернення до стандартного section, а також записано `Player.log`.
- `RimChat.Config.RimChatSettings`
  - Додано поле постійного збереження `PromptSectionCatalog`, а також `GetPromptSectionCatalogClone()`, `SetPromptSectionCatalog(...)`, `ResolvePromptSectionText(...)`.
  - `GetRimTalkChannelConfig(...)` / `SetRimTalkChannelConfig(...)` перетворено на legacy adapter для UI/адаптації імпорту; вони більше не представляють офіційний стан середовища виконання.
- `RimChat.Prompting.PromptEntryStaticTextCatalog`
  - `DiplomacyDialogueRequest.*` аналізується з каталогу section і безпосередньо вбудовується в шаблон на етапі міграції; `dialogue.diplomacy_dialogue.*` більше не надається як змінна Scriban середовища виконання.
- `RimChat.Config.PromptPresetService` / `RimChat.Persistence.PromptBundleConfig` / `RimChat.Config.RpgPromptCustomConfig`
  - Для preset, bundle і RPG custom store додано синхронізований вхід постійного збереження `PromptSectionCatalog`; під час збереження/імпорту спочатку нормалізуються нативні section, після чого структури compat очищуються.

## Видалення середовища виконання сумісності промптів（v0.6.33）

- `RimChat.Persistence.PromptPersistenceService`
  - `BuildFullSystemPromptHierarchical(...)` і `BuildRpgSystemPromptHierarchical(...)` тепер завжди використовують RimChat для побудови нативної ієрархії та більше не викликають RimTalk entry-driven точку входу сумісності.
- `RimChat.Prompting`
  - Додано `IPromptRuntimeVariableProvider`, `PromptRuntimeVariableRegistry`, `RimChatCoreVariableProvider`, `RimTalkVariableProvider`, `RimTalkMemoryPatchVariableProvider`.
  - `PromptVariableCatalog` Перехід від статичного набору констант до агрегації на основі реєстру provider.
- `RimChat.Config` / `RimChat.Persistence`
  - legacy `RimTalkChannelCompatConfig` зберігається лише для міграції/імпорту й більше не належить до інтерфейсу складання робочого prompt.

## Виправлення збереження активації пресета Prompt Workbench（v0.6.32）

- `RimChat.Config.PromptPresetService`
  - Скориговано `Activate(...)`: після активації пресета оновлений стан редактора більше не записується одразу назад на диск, щоб уникнути перезапису щойно застосованого нового payload результатом повторного завантаження зі старого файла.
  - Скориговано `ApplyPayloadToSettings(...)`:
    - Під час `persistToFiles = true`, окрім `Prompt/Custom/SystemPrompt_Custom.json`, `DiplomacyDialoguePrompt_Custom.json`, `SocialCirclePrompt_Custom.json` і `FactionPrompts_Custom.json`,
    - також перебудовуються пов’язані з `RpgPromptCustomConfig` поля відповідно до поточного payload і записуються назад у `Prompt/Custom/PawnDialoguePrompt_Custom.json`, щоб рівень сумісності RPG/RimTalk відповідав явно заданому payload пресета.
  - Скориговано `WriteIfNotNull(...)`: коли payload порожній, старий custom-файл видаляється, а не мовчки зберігається.
- Результат:
  - Під час перемикання `Default -> Migrated -> Default` середній «Вміст елемента (Scriban)» і правий «Попередній перегляд» повертаються до фактичного вмісту відповідних пресетів;
  - Згенерований міграцією `Migrated` більше не перезаписує робочий стан `Default` тим самим вмістом;
  - Ланцюжок активації пресета Prompt Workbench знову узгоджує результати запису `Prompt/Custom/*` на диск зі станом у пам’яті.

## Початкове завантаження канонічного стандартного пресета Prompt Workbench（v0.6.26）

- `RimChat.Config.PromptPresetService`
  - Додано canonical `Default` логіку початкового налаштування пресетів: під час першого створення сховища пресетів більше не захоплюється `Default` із поточного стану legacy/runtime.
  - Джерела canonical payload:
    - `Prompt/Default/SystemPrompt_Default.json`
    - `Prompt/Default/DiplomacyDialoguePrompt_Default.json`
    - `Prompt/Default/PawnDialoguePrompt_Default.json`
    - `Prompt/Default/SocialCirclePrompt_Default.json`
    - `Prompt/Default/FactionPrompts_Default.json`
    - `RimChatSettings.CreateCanonicalDefaultRimTalkChannelConfig(...)`
  - Сумісність під час оновлення:
    - Якщо поточний legacy payload суттєво відрізняється від canonical payload, буде додано `Migrated` пресет, який зберігає старий промпт;
    - `Default` завжди зберігається як canonical типовий вміст.
- `IPromptPresetService.ApplyPayloadToSettings(...)`
  - Вхідні дані: `RimChatSettings`, payload пресету, `persistToFiles`.
  - Поведінка:
    - `persistToFiles = true`: використати наявний шлях активації, записати назад `Prompt/Custom/*` і оновити стан редактора;
    - `persistToFiles = false`: лише синхронізувати active preset із поточним об’єктом налаштувань, не перезаписуючи наявні користувацькі файли.
- `RimChatSettings_PromptAdvancedFramework.EnsurePresetStoreReady()`
  - Нова поведінка: після першого завантаження preset store спочатку синхронізувати active preset із поточним станом редагування workbench, а потім установити вибраний пресет ID.
  - Результат: під час першого відкриття Prompt Workbench вибраний пресет ліворуч і вміст елементів праворуч більше не розходяться.

## Фіксований редактор основного тексту Prompt Workbench + контракт вертикального прокручування（v0.6.25）

- `RimChat.UI.PromptWorkbenchChipEditor`
  - Вхідні дані: фіксований `Rect`, поточний текст, стан прокручування.
  - Вихідні дані: відредагований текст оригінального шаблону (досі записується назад у `RimTalkPromptEntryConfig.Content`).
  - Поведінка:
    - Основна область Workbench має фіксовану висоту й більше не розширюється візуально разом із вмістом тексту;
    - Для текстової області ввімкнено автоматичне перенесення рядків;
    - Основною взаємодією залишається лише вертикальне прокручування; стан прокручування скидається під час перемикання між елементами;
    - Структура підсвічування капсул змінних token і підказок tooltip залишається без змін.
- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryEditor(..., bool useChipEditor = false)`
  - Основна область Workbench і надалі використовує наявний макет `contentRect` і панелі стану перевірки.
- `RimChatSettings_RimTalkTab.DrawLegacyPromptEntryTextArea(...)`
  - Текстове поле fallback у Workbench і chip-редактор узгоджуються за принципом «фіксована висота + автоматичне перенесення рядків + вертикальне прокручування», щоб після відкату м’якого обмеження взаємодія не змінювалася різко.

## Редактор змінних чипів Prompt Workbench + уніфікований контракт спливних підказок（v0.6.24）

- `RimChat.UI.PromptWorkbenchChipEditor`
  - Вхідні дані: `Rect`, поточний текст, стан прокручування.
  - Вихідні дані: відредагований вихідний текст шаблону (записується назад у `RimTalkPromptEntryConfig.Content`).
  - Поведінка:
    - Розпізнаються лише повні токени змінних із білого списку `PromptVariableCatalog`;
    - Дійсні токени відображаються з фоновим кольором у вигляді капсул;
    - Одинарне клацання вибирає капсулу, подвійне відкриває редагування вихідного тексту токена, Backspace/Delete видаляє весь токен.
- `PromptVariableTokenScanner.ParseSegments(...)`
  - Сувора відповідність: `{{ namespace.path }}` (має відповідати білому списку змінних).
  - Вихідні дані: `PromptTokenSegment` (`Text`/`VariableToken`) для використання шаром відображення UI.
- `PromptVariableTooltipCatalog.Resolve(...)`
  - Вихідні дані: `PromptVariableTooltipInfo` (статична інформація `name/dataType/description/typicalValues`).
  - Призначення: уніфікувати структуру вмісту спливної інформації змінних на бічній панелі Workbench і в капсулах редактора.
  - Правило: спочатку повертати специфічний для змінної опис і явно вказані типові значення; якщо специфічні метадані відсутні, робити висновок за загальними правилами.
- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryEditor(..., bool useChipEditor = false)`
  - Новий параметр: `useChipEditor`, призначений для обмеження редактора капсул шляхом Prompt Workbench.

Гарантія сумісності: поля збереження та основний ланцюжок рендерингу не змінюються, `entry.Content` і надалі зберігає текст оригінального шаблону.

## Джерело вмісту записів за замовчуванням для всіх каналів + суворе початкове заповнення змінних (v0.6.23)

- Джерело вмісту записів за замовчуванням:
  - Додано `Prompt/Default/RimTalkPromptEntries_Default.json`, який надає текст запису за замовчуванням відповідно до `PromptChannel + SectionId`.
  - Додано `RimTalkPromptEntryDefaultsProvider.ResolveContent(promptChannel, sectionId)` для зчитування під час відновлення записів за замовчуванням у робочому середовищі.
- Поведінка відновлення записів:
  - Коли вміст запису порожній, `RimChatSettings.BuildCanonicalSectionEntry(...)` спочатку заповнює відповідний розділ зі стандартного JSON.
  - `RimChatSettings_RimTalkTab.TryRestoreDefaultEntriesForScopedChannel(...)` тепер відновлює «структуру + текст за замовчуванням», а не лише структуру.
- Суворе початкове заповнення змінних Scriban:
  - `PromptPersistenceService.BuildSharedPromptTemplateVariables(...)` спочатку ініціалізує весь простір імен змінних порожніми рядками відповідно до `PromptVariableCatalog.GetAll()`, а потім замінює їх значеннями поточного контексту.
  - Мета: уникнути помилок strict під час рендерингу на основі записів через «наявність змінної у білому списку, але без присвоєного значення».
- Зміна стратегії сумісності (несумісна зміна):
  - Вимкнено точку збереження та зворотного запису застарілих записів у старі поля:
    - `RimChatSettings.SaveRpgPromptTextsToCustom(...)` більше не викликає `SyncLegacyPromptFieldsFromEntryChannels()`.
    - `RimChatSettings_Prompt.SaveSystemPromptConfig()` більше не викликає `SyncLegacyPromptFieldsFromEntryChannels()`.
  - Примітка: ланцюжок читання старих полів збережено, але на етапі збереження більше не гарантується двостороння синхронізація з новою системою записів.

## Контракт каналу з обмеженою областю Prompt Workbench（v0.6.22）

- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryList(...)`
  - У режимі workbench список записів тепер працює лише з підмножиною каналів в області дії.
  - Додавання, дублювання, видалення та зміна порядку обмежені лише видимими записами в області дії.
- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryEditor(...)`
  - Перед редагуванням вибір у редакторі нормалізується відповідно до видимості каналів в області дії.
- `RimChatSettings_RimTalkTab.EnsureRimTalkEditableEntry(...)`
  - У режимі workbench канал за замовчуванням для нового запису тепер визначається активним каналом в області дії.
- Гарантія поведінки:
  - Редагування з обмеженням за каналом не змінює порядок або вміст записів в інших каналах.

## Канонічна схема розділів Prompt Workbench（v0.6.21）

- `RimTalkPromptEntryConfig`
  - Додано постійне поле: `SectionId` (рядок, за замовчуванням порожній).
  - Призначення: стабільна ідентичність розділу, незалежна від локалізованого тексту назви.
- `RimChatSettings.EnsurePromptEntrySeedForChannel(...)`
  - Розширений потік охоплення: після синхронізації початкових даних кожен доступний для вибору канал нормалізується до канонічного макета з 8 розділів.
  - Канонічні назви розділів:
    - `System Rules`
    - `Character Persona`
    - `Memory System`
    - `Environment Perception`
    - `Context`
    - `Action Rules`
    - `Repetition Reinforcement`
    - `Output Specification`
- Контракт поведінки під час виконання:
  - Застарілі макети перебудовуються в канонічні розділи один раз після визначення форми каналу.
  - Відсутні розділи створюються автоматично.
  - Неканонічні додаткові розділи видаляються під час нормалізації.
  - Канонічні назви розділів примусово встановлюються англійською; ручне змінення порядку списку залишається дозволеним.

## Контракт рушія Scriban（Крок 2: несумісне оновлення）

- Основна точка входу рендерингу:
  - `RimChat.Prompting.IScribanPromptEngine.RenderOrThrow(templateId, channel, templateText, context)`
- Контракт виконання:
  - Дозволені лише змінні простору імен: `ctx.* / pawn.* / world.* / dialogue.* / system.*`
  - Помилка під час аналізу/рендерингу/невідомої змінної/доступу до нульового об’єкта обов’язково має виникати `PromptRenderException`
  - Заборонено після помилки рендерингу промпту передавати далі оригінальний текст або порожній рядок як резервний варіант
- Контракт міграції:
  - Оновлення Schema виконує одноразовий перезапис і перевірку через `PromptTemplateAutoRewriter`
  - Позначити шаблон із помилкою `Blocked` і викинути `PromptRenderException(ErrorCode=1200)`
  - Якщо відсутній обов’язковий текст шаблону, викинути `PromptRenderException(ErrorCode=1201, TemplateMissing)`
- Ланцюжок шаблонів сценарію:
  - `PromptPersistenceService.RenderTemplateVariables(...)` уже переключено на суворий рендеринг Scriban; старий рендеринг із регулярною заміною більше не виконується
- Стан мосту виконання:
  - `RimTalkCompatBridge` фізично видалено з кодової бази RimChat
- Спостережуваність:
  - `ScribanPromptEngine` вбудований LRU кеш компіляції (фіксована місткість) і телеметрія рендерингу
  - `Dialog_ApiDebugObservability` відображення коефіцієнта влучань у кеш, кількості влучань/промахів/витіснень, середнього часу компіляції, середнього часу рендерингу

> Примітка: якщо в історичних записах старих версій нижче цього документа трапляється опис «fallback», це стосується лише історичної поведінки; у середовищі виконання v0.6.15+ чинним є strict-контракт цього розділу.

## Перехоплення каналу записів RimTalk + виправлення недійсної зони робочого столу (v0.6.18)

- `RimTalkPromptEntryConfig`
  - Додано поле постійного зберігання: `PromptChannel` (рядок, типове значення `any`, нормалізація перед записом на диск).
- `RimTalkPromptEntryChannelCatalog`
  - Додано каталог каналів API: `GetSelectableChannels(...)`, `NormalizeForRoot(...)`, `MatchesRuntimeChannel(...)`, `GetSeedDefinitions(...)`.
  - Охоплення каналів: `外交对话 / RPG对话 / 外交策略 / 主动外交 / 主动RPG / 社交圈推文 / 人格初始化 / 摘要生成 / RPG归档压缩 / 图像生成`.
- `RimChatSettings.LoadRpgPromptTextsFromCustom(...)` / `EnsurePromptEntrySeedForChannel(...)`
  - Після міграції старих полів відсутні записи каналів автоматично доповнюються, а типовий стан увімкнення задається за стратегією початкового заповнення, зберігаючи сумісність зі старими збереженнями.
- `PromptPersistenceService.TryBuildEntryDrivenChannelPrompt(...)`
  - Під час виконання додано сувору попередню умову: якщо `EnablePromptCompat == false`, негайно вийти з ланцюжка ін'єкції запису та повернутися до стандартного пошарового промпту.
  - До фільтрації записів додано зіставлення каналів: інжектувати лише записи `Enabled && Content 非空 && PromptChannel 匹配当前运行通道/模式`.
- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryEditor(...)`
  - Вхід редактора змінено з `Role/Position` на `PromptChannel`, що виправляє невідповідність «елемент керування натискається, але під час виконання не працює».
- `RimChatSettings_PromptAdvancedFramework.GetWorkbenchEditingChannelConfig(...)`
  - На робочому столі додано кеш конфігурації в режимі редагування, щоб уникнути відкату введення в текстовій області через clone/set кожного кадру.

## Суворий ланцюжок Persona + замикання діагностики RimTalk (v0.6.17)

- `GameComponent_RPGManager.PersonaBootstrap.BuildPersonaBootstrapPrompt(...)` / `RenderPersonaBootstrapTemplate(...)`
  - Перехід від ланцюжка заміни рядків до `PromptTemplateRenderer.RenderOrThrow(...)` суворого рендерингу Scriban.
- `GameComponent_RPGManager.PersonaBootstrap.RenderPersonaCopyTemplateOrThrow(...)`
  - У разі помилки рендерингу або порожнього результату copy особистості тепер безпосередньо викидається `PromptRenderException` і ланцюжок переривається (без silent fallback).
- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryEditor(...)`
  - До редактора вмісту запису додано стан діагностики Scriban у реальному часі (код помилки + рядок і стовпець + невідомі змінні).
- `RimChatSettings_PromptAdvancedFramework.DrawWorkbenchMainPanel(...)`
  - «Вміст запису (Scriban)» у робочому столі промптів зберігає багаторядкове поле редагування фіксованої висоти з вертикальним прокручуванням усередині; токени змінних і надалі мають підсвічування у вигляді капсул і підказки, але стиль повернуто до початкового вигляду без обведення, водночас зберігається неперекриття сусідніх символів.
- `RimChatSettings_RimTalkTab.TryInsertVariableIntoFocusedEditor(...)`
  - Коли робочий стіл вставляє повний токен змінної у вже сфокусоване поле редагування вмісту запису, він автоматично додає відсутні пробіли перед і після токена, зменшуючи злипання капсули із сусіднім текстом.
- `PromptWorkbenchChipEditor.DrawChipLabel(...)`
  - Для текстового шару капсули використовується `new Color(184f/255f, 230f/255f, 184f/255f, 1f)` як колір шрифту, узгоджений із зеленим оформленням капсул змінних.
- `RimChatSettings_RimTalkTab.DrawRimTalkChannelTemplateTextArea(...)`
  - До текстової області шаблону каналу додано стан діагностики Scriban у реальному часі.
- `RimChatSettings_RimTalkTab.DrawRimTalkPersonaCopyTemplateEditor(...)`
  - До області редагування шаблону Persona copy додано стан діагностики Scriban у реальному часі.

## Виправлення надійності зони натискання у Prompt Workbench（v0.6.14）

- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryList(...)`
  - Інтерактивну зону натискання розширено на всю область вибору рядка.
  - Відновлено верхню клавішу швидкого доступу для дублювання (`⧉`) і обробку конфліктів назв дублікатів.
- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryEditor(...)`
  - Додано адаптивний резервний варіант: коли горизонтального простору недостатньо, дії Role/Position перемикаються на вертикальне розташування одна під одною.
- `RimChatSettings_RimTalkVariableBrowser.DrawRimTalkWorkbenchVariableRow(...)`
  - Додано явну кнопку `Insert` на рівні рядка в панелі змінних верстата, водночас вставлення натисканням на тіло рядка збережено.
- Примітки щодо сумісності:
  - Несумісна міграція збережень або схеми не потрібна.
  - Застарілі файли промптів і старі поля залишаються доступними для читання.

## Виправлення реагування кнопки Prompt Workbench（v0.6.13）

- `RimChatSettings_PromptAdvancedFramework.OpenPromptWorkbenchWindowForRpg(...)`
  - Додано спеціальний шлях відкриття Prompt Workbench для RPG, щоб уникнути скидання каналу з точок входу RPG.
- `RimChatSettings_AI.RpgDialogue.DrawRpgNonPromptSettings(...)`
  - Параметри середовища виконання RPG тепер відкривають Prompt Workbench через точку входу каналу RPG.
- `RimChatSettings_RimTalkTab.DrawTab_RimTalk(...)`
  - Вкладка міграції RimTalk тепер відкриває Prompt Workbench через точку входу каналу RPG.
- `RimChatSettings_PromptAdvancedFramework.TryActivatePresetById(...)`
  - Додано централізований процес активації попередніх налаштувань із явною обробкою помилок і локалізованим повідомленням про помилку.
- `RimChatSettings_PromptAdvancedFramework.ShowImportPresetDialog(...)` / `ShowExportPresetDialog(...)`
  - Додано локалізований зворотний зв’язок про успішне виконання дій імпорту/експорту.
- `RimChatSettings_PromptAdvancedFramework.DrawPresetActions(...)` / `DrawPresetBottomActions(...)`
  - Додано локалізований зворотний зв’язок про успішне виконання дій створення/дублювання/перейменування/видалення.
- Примітки щодо сумісності:
  - Без руйнівної міграції збережень/схеми.
  - Застарілі файли промптів і старі поля залишаються доступними для читання.

## Виправлення взаємодії з майстернею промптів + порт змінної RimTalk UI（v0.6.12）

- `RimChatSettings_PromptAdvancedFramework.DrawWorkbenchVariables(...)`
  - Бічна панель змінних майстерні тепер використовує спеціалізований рендерер на основі Rect замість вкладеного `Listing_Standard`, що усуває невідповідність зон натискання та ділянки без реакції на натискання.
- `RimChatSettings_RimTalkVariableBrowser.DrawRimTalkWorkbenchVariableBrowser(...)`
  - Додано схему бічної панелі змінних у стилі RimTalk: пошук, згруповані секції змінних, вставлення натисканням по всьому рядку, метадані підказок.
- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryList(...)`
  - Взаємодію зі списком записів узгоджено з RimTalk: вбудований прапорець увімкнення, вбудована кнопка видалення та елементи керування переміщенням угору/вниз.
- `RimChatSettings_PromptAdvancedFramework.DrawPresetList(...)`
  - Тепер вибір рядка пресета негайно активує та застосовує цей пресет, тому вміст редактора перемикається разом із вибором.
- Примітки щодо сумісності:
  - Без руйнівної міграції збережень/схеми.
  - Застарілі файли промптів і старі поля залишаються доступними для читання.

## Узгодження відповідності майстерні промптів RimTalk（v0.6.11）

- `RimChatSettings_PromptAdvancedFramework.DrawWorkbenchBody(...)`
  - Геометрію майстерні перебалансовано до пропорцій на кшталт RimTalk: вузька ліва панель + правий робочий простір, розділений на редактор і бічну панель.
- `RimChatSettings_PromptAdvancedFramework.DrawWorkbenchPresetPanel(...)`
  - Ліву панель реорганізовано в компактний робочий процес пресетів/записів, а загальні кнопки дій із промптами, яких не було в RimTalk робочому процесі майстерні UX, вилучено.
- `RimTalkPromptEntryConfig`
  - Додано поле `CustomRole` для явного збереження власної ролі під час редагування на рівні запису.
- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryEditor(...)`
  - Виправлено прив’язку текстового поля `Custom Role`: тепер воно записує дані в `entry.CustomRole`, а не перезаписує `entry.Role`.
- Примітки щодо сумісності:
  - Міграція зі зламом сумісності для збережень/схеми не потрібна.
  - Файли зі старими промптами, у яких відсутній `CustomRole`, залишаються дійсними та використовують порожнє значення як запасний варіант.

## Ізоляція простору назв іконок налаштувань моду（v0.6.10）

- `About/About.xml`
  - Тепер `modIconPath` — це `UI/RimChat/Logo`, а не загальний `UI/Logo`.
- `1.6/Textures/UI/RimChat/Logo.png`
  - Додано логотип із простором назв для визначення іконки налаштувань моду/списку модів.
- Примітки щодо сумісності:
  - Старий `1.6/Textures/UI/Logo.png` збережено.
  - Зміни схеми збережень відсутні.
  - Зміни схеми файлів промптів відсутні.

## Ізоляція простору назв іконки перемикача зв’язку（v0.6.9）

- `PlaySettingsPatch_CommsToggleIcon.ResolveCommsToggleIcon()`
  - Завантаження іконки тепер надає перевагу унікальному шляху до ресурсу `UI/RimChat/CommsToggleIcon` і використовує старий `UI/CommsToggleIcon` як запасний варіант.
- `1.6/Textures/UI/RimChat/CommsToggleIcon.png`
  - Додано окремий ресурс іконки виконання за простором імен, щоб уникнути конфліктів шляхів до текстур між модами.
- Примітки щодо сумісності:
  - Змін у схемі збережень немає.
  - Змін у схемі файлів промптів немає.
  - Старий шлях до іконки й надалі підтримується для старіших дистрибутивів.

## RimTalk Поліпшення взаємодії зі списком записів（v0.6.8）

- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryList(...)`
  - Список записів переведено на дворядкове відображення (назва + роль/позиція), а довгий текст безпечно обрізається та повністю показується в підказці.
- `RimChatSettings_RimTalkTab.DrawRimTalkPromptEntryEditor(...)`
  - Макет елементів керування активацією/роллю/позицією переведено на адаптивну ширину, щоб уникнути накладання кнопок і недоступних для натискання ділянок на вузькій ширині.
- `1.6/Languages/*/Keyed/RimChat_Keys.xml`
  - Додано мовні ключі `RimChat_Import` і `RimChat_Export`, виправлено обрізання тексту через відкат ключа кнопки у верхній частині робочого столу.
- Примітки щодо сумісності:
  - Цього разу доповнено лише взаємодію UI і ключі локалізації; структуру збережень і схему файлів промптів не змінено.

## Оглядач змінних робочого столу промптів UX + кеш продуктивності（v0.6.7）

- `RimChatSettings_RimTalkVariableBrowser.DrawRimTalkTabVariableBrowser(...)`
  - Оглядач змінних перебудовано у структуру «список із можливістю вибору + відомості про вибране», додано підсвічування вибраного рядка зі збереженням дії вставлення.
- `RimChatSettings_RimTalkVariableBrowser.GetFilteredRimTalkVariables(...)`
  - Додано кеш із регулюванням частоти оновлення знімків змінних (1.2 секунд) і кеш результатів пошуку, що зменшує отримання даних через рефлексію та повторне сортування кожного кадру.
- `RimChatSettings_RimTalkVariableBrowser.DrawRimTalkVariableDetails(...)`
  - Додано розділ із детальною інформацією про змінні, де відображаються повні token, група й опис, щоб зменшити помилкові висновки через обрізання довгого тексту.
- Зміни архітектури:
  - Логіку браузера змінних винесено з `RimChatSettings_RimTalkTab.cs` до `RimChatSettings_RimTalkVariableBrowser.cs` (partial).
- Примітка щодо сумісності:
  - Цього разу оптимізовано лише UI взаємодію та продуктивність рендерингу; поля збережень і schema файлів промптів не змінюються, сумісність зі старими версіями зберігається.

## Вставлення змінних у Prompt Workbench і розділення seed（v0.6.6）

- `RimChatSettings_PromptAdvancedFramework.DrawWorkbenchVariables(...)`
  - Панель змінних праворуч у робочому середовищі тепер повторно використовує шлях рендерингу браузера змінних RimTalk.
- `RimChatSettings_RimTalkTab.AppendVariableToCurrentRimTalkTemplate(...)`
  - Стратегію вставлення змінних змінено на «спочатку вставлення в позицію курсора, після втрати фокуса — додавання в кінець».
- `RimChatSettings_PromptEntrySeedImport`
  - Додано роздільник застарілого об’єднаного тексту, який створює кілька записів seed за заголовками розділів для `BuildLegacyPromptEntries(...)`.
- Примітка щодо сумісності:
  - Розділення seed запускається лише під час міграції за шляхом «немає дійсних записів» і не перезаписує наявні записи користувача.

## Оновлення прототипу Prompt Workbench і вхід через одну вкладку（v0.6.5）

- Зміни інтерфейсу навігації сторінки налаштувань（`RimChatSettings`）:
  - Верхній рівень Tab змінено на `API / ModOptions / PromptWorkbench / ImageApi`.
  - Початковий пункт верхнього рівня `Prompts / RPG / RimTalk` більше не відображається.
- Поведінка входу Prompt Workbench:
  - Натискання `PromptWorkbench` безпосередньо викликає `OpenPromptWorkbenchWindow()` і відкриває окреме вікно;
  - Дія входу не змушує змінювати контекст вмісту поточної сторінки налаштувань.
- Зміни взаємодії з інтерфейсом робочого столу промптів（`RimChatSettings_PromptAdvancedFramework`）：
  - Основний канал фіксовано як `Diplomacy` / `RPG`;
  - Канал RPG доповнено перемикачем другого рівня: `Common Entries` / `Pawn Persona`;
  - Праву панель інструментів змінено на режим перемикання панелей: `Preview` / `Variables` / `Help`;
  - Панель `Variables` повторно використовує браузер змінних RimTalk (пошук, групування, вставка до поточного запису).
- Поведінку вставки змінних узгоджено з RimTalk (`RimChatSettings_RimTalkTab`):
  - Спочатку вставляти змінну в поточну позицію курсора редактора;
  - Якщо редактор не сфокусований, додавати змінну в кінці, зберігаючи сумісність.
- Доповнення імпорту початкових даних legacy-записів (`RimChatSettings_PromptEntrySeedImport`):
  - Коли стара конфігурація містить лише об’єднаний текст, автоматично розділити його на кілька `[Section]` за заголовками сегментів (наприклад, `=== Section ===`),`PromptEntries`.
- текстRPG-текст (`RimChatSettings_AI.RpgDialogue.cs`）：
  - Додано ModOptions Група `RPG Runtime Settings`;
  - Підтримуються перемикачі середовища виконання: `EnableRPGDialogue`, `EnableRPGAPI`, перемикач ін’єкції, `RpgManualSceneTagsCsv`.
- Базова сумісність:
  - Структура `PromptPresetChannelPayloads` не змінюється;
  - Сумісні поля та шляхи читання й запису RimTalk не змінюються; приховується лише вхід до візуального каналу.

## Unified Channels входу промпту（v0.6.4）

- Зміни поведінки каналу Prompt Workbench:
  - `Diplomacy` і `RPG` тепер безпосередньо повторно використовують робочий процес редагування записів RimTalk, більше не проходячи через проміжний шар старого «редактора секцій».
  - Структура редагування записів залишається незмінною: `Name / Enabled / Role / Position / InChatDepth / Content`.
- Зміни точки входу складання під час виконання:
  - `PromptPersistenceService.BuildFullSystemPromptHierarchical(...)`
  - `PromptPersistenceService.BuildRpgSystemPromptHierarchical(...)`
  - Нова логіка спочатку об’єднує «увімкнені записи» у порядку записів; якщо виявлено лише старі поля (без дійсних записів), вона створює тимчасові резервні записи зі старих полів, щоб забезпечити сумісність після оновлення.
- Ланцюжок рендерингу Scriban:
  - Вміст записів рендериться через `PromptTemplateRenderer.RenderOrThrow(...)` -> `IScribanPromptEngine.RenderOrThrow(...)`.
  - Рендеринг записів більше не залежить від методу середовища виконання bridge RimTalk.
- Стратегія зворотного запису застарілих полів:
  - Під час збереження застарілі поля записуються назад із системи записів (дипломатія: `GlobalSystemPrompt/GlobalDialoguePrompt`, RPG: `RoleSetting/DialogueStyle` тощо), а також зберігаються за старим шляхом JSON, щоб старі версії могли читати їх без збоїв.

## Редактор записів змінних каналу RimTalk (v0.6.3)

- Нові поля `RimTalkChannelCompatConfig`:
  - `PromptEntries: List<RimTalkPromptEntryConfig>`
- Новий контракт даних запису: `RimTalkPromptEntryConfig`
  - `Id`, `Name`, `Role`, `Position`, `InChatDepth`, `Enabled`, `Content`
- Стратегія сумісності:
  - Якщо стара конфігурація містить лише `CompatTemplate`, на етапі завантаження її буде автоматично перенесено до одного типового запису;
  - Список записів буде автоматично об’єднано в `CompatTemplate`, щоб старий ланцюжок залишався доступним для читання.
- Оновлення взаємодії з каналом RimTalk UI:
  - Список записів: додавання, копіювання, видалення, переміщення вгору, переміщення вниз;
  - Редагування запису: назва, стан увімкнення, роль, позиція, глибина InChat, вміст;
  - Вставлення змінної: пріоритетно записувати до вмісту поточного вибраного запису.

## Prompt Workbench + Preset Framework (v0.6.2)

- Додано контракт даних пресетів:
  - `PromptPresetStoreConfig`: `SchemaVersion`, `ActivePresetId`, `Presets`.
  - `PromptPresetConfig`: `Id`, `Name`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `ChannelPayloads`.
  - `PromptPresetChannelPayloads`: обмежувальні поля `Diplomacy`, `Rpg`, `RimTalkDiplomacy`, `RimTalkRpg` і RimTalk.
- Додано сервісний інтерфейс: `IPromptPresetService` (`LoadAll/SaveAll/CreateFromLegacy/Duplicate/Activate/ImportPreset/ExportPreset/BuildSummaries`).
- У розширеному режимі сторінки промптів додано робочий стіл:
  - Навігація каналами: `Diplomacy`, `RPG`, `RimTalk-Diplomacy`, `RimTalk-RPG`.
  - Керування пресетами: створення, копіювання, активація, видалення, перейменування, імпорт, експорт.
- Стратегія сумісності:
  - Під час першого завантаження, якщо файл пресетів відсутній, із конфігурації старого `Prompt/Custom/*` автоматично переноситься й створюється пресет за замовчуванням.
  - Під час активації пресету оновлюється старий файл `Prompt/Custom/*`, а поля сумісності двох каналів RimTalk залишаються синхронізованими.
- Стару вкладку RimTalk перетворено на точку входу для міграції: користувачеві пропонується перейти до відповідного каналу робочого столу промптів.

## Перемикач суворої ізоляції RimTalk (v0.6.1)

- Додано параметри ізоляції (`RimChatSettings` / `RpgPromptCustomConfig`):
  - `RimTalkAutoPushSessionSummary` (за замовчуванням `false`)
  - `RimTalkAutoInjectCompatPreset` (за замовчуванням `false`)
- Поведінка ланцюжка сумісності (основний ланцюжок Scriban strict):
  - `PushSessionSummary` виконує запис глобальних змінних лише коли `RimTalkAutoPushSessionSummary == true`;
  - Елемент сумісного пресету `RimChat Compat Variables` автоматично створюється/оновлюється лише коли `RimTalkAutoInjectCompatPreset == true`;
  - Коли `RimTalkAutoInjectCompatPreset == false`, якщо цей елемент існує, його буде примусово вимкнено, щоб запобігти зчитуванню звичайним ланцюжком чату RimTalk резюме RimChat.
- Спосіб реєстрації змінних залишається без змін:
  - Змінна `rimchat_*` і надалі надається через ланцюжок реєстрації змінних Context;
  - Рекомендується явно посилатися на `{{variable}}` лише в шаблоні, без неявного автоматичного впровадження.
- Сумісність:
  - Для старих збережень / старих власних промптів, у яких відсутнє нове поле JSON, автоматично використовується `false`;
  - Наявна структура полів і шляхи зчитування не змінюються.

## Керування відображенням прихованих фракцій дипломатії на комунікаційній панелі (v0.5.29)

- Новий стан на рівні збереження (`GameComponent_DiplomacyManager`):
  - `HashSet<Faction> manuallyVisibleHiddenFactions`
  - Ключ серіалізації: `manuallyVisibleHiddenFactions`
  - Сумісність зі старими збереженнями: якщо в старому збереженні немає цього поля, автоматично повернутися до порожньої множини та очистити недійсні посилання.
- Нові інтерфейси (для виклику з боку UI):
  - `List<Faction> GetManuallyVisibleHiddenFactions()`
  - `bool IsHiddenFactionManuallyVisible(Faction faction)`
  - `void SetManuallyVisibleHiddenFactions(IEnumerable<Faction> factions)`
- Правила фільтрації списку дипломатичних делегацій на комунікаційній станції (`Dialog_DiplomacyDialogue.GetAvailableFactions`):
  - Базова відповідність вимогам: `!IsPlayer && !defeated`
  - Видимі за замовчуванням: `!Hidden`
  - Додатково видимі: `Hidden && manuallyVisibleHiddenFactions.Contains(faction)`
- Додано UI:
  - Кнопка-шестерня праворуч від заголовка фракції: відкриває діалогове вікно множинного вибору прихованих фракцій.
  - Дії у вікні: вибрати все, очистити, підтвердити, скасувати (лише підтвердження записує стан у збереження).

## Три режими генерації зображень API та асинхронний ланцюжок ComfyUI (v0.5.22)

- Режими виконання генерації зображень (`DiplomacyImageApiConfig` / `DiplomacyImageGenerationService`):
  - `sync_url`: синхронний запит, розібрати URL і завантажити.
  - `sync_payload`：синхронний запит, спочатку розібрати URL, а в разі невдачі — декодувати навантаження Base64.
  - `async_job`：режим асинхронного завдання (надсилання -> опитування -> завантаження зображення).
- Додано поля конфігурації, які можна зберігати (якщо їх немає у старому збереженні, автоматично використовуються значення за замовчуванням):
  - `Mode`, `SchemaPreset`, `AuthMode`
  - `ApiKeyHeaderName`, `ApiKeyQueryName`
  - `ResponseUrlPath`、`ResponseB64Path`
  - `AsyncSubmitPath`, `AsyncStatusPathTemplate`, `AsyncImageFetchPath`
  - `PollIntervalMs`, `PollMaxAttempts`
- Режими автентифікації:
  - `bearer` (за замовчуванням)
  - `api_key_header` (використовує `ApiKeyHeaderName`)
  - `query_key` (використовує `ApiKeyQueryName`)
  - `none`
- Сумісність ComfyUI (`SchemaPreset=comfyui`):
  - автоматично перемикається на `async_job`;
  - процес надсилання: `/prompt`;
  - процес опитування: `/history/{job_id}`;
  - Процес Рату: `/view?filename=...&subfolder=...&type=...`.
- Перевірка зв’язності на сторінці налаштувань зображень API (`RimChatSettings_ImageApi`):
  - Надати в тому ж стилі, що й головна API сторінка, `Test Connection` кнопки та зворотний зв’язок кольорами стану;
  - У режимі `sync_*` виконати пробний мінімальний запит на створення зображення;
  - У режимі `async_job` виконати пробний запит надсилання (ComfyUI зчитує `prompt_id`).
- Попередні налаштування Provider на сторінці налаштувань зображень API:
  - Пункти попередніх налаштувань: `Volcengine ARK`, `OpenAI Compatible`, `SiliconFlow`, `ComfyUI Local`, `Custom`.
  - Для попередніх налаштувань, відмінних від `Custom`, автоматично заповнюються стандартні значення режиму/протоколу/автентифікації; звичайному користувачеві потрібно налаштувати лише endpoint/apiKey/model.
  - Попередні налаштування `Custom` можуть розгортати розширені параметри (режим, автентифікація, шлях відповіді, шлях асинхронної операції, параметри опитування).
- Сумісність:
  - Не змінювати контракт дії `send_image`;
  - Не змінювати структуру наявних файлів промптів;
  - Старі збереження та старі конфігурації можна безпосередньо прочитати.

## Панель історії діалогових сеансів і часова шкала поведінки RPG (v0.5.21)

- У ручному вікні діалогу RPG додано панель історії сеансів (`Dialog_RPGPawnDialogue.HistoryPanel`):
  - У нижньому лівому куті додано кнопку входу `RimChat_RPGHistoryButton`;
  - Область відображення панелі фіксована як «поточний сеанс»;
  - Порядок записів — за часом у прямому порядку (від старих до нових);
  - Натискання за межами панелі лише закриває панель, але не закриває вікно діалогу RPG.
- Модель історії в межах сеансу (лише під час виконання, тільки UI):
  - Елемент діалогу: `speaker + text` (гравець/NPC);
  - Елемент поведінки: прикріплюється до відповідного елемента діалогу NPC, містить `actionName + result(success/failure/error) + reason`.
- Інтеграція ланцюжка виконання дій:
  - `NotifyActionSuccess/NotifyActionFailure/NotifyActionError` тепер також записує історію поведінки в режимі реального часу;
  - Зберігаються без змін наявна логіка виконання дій і поведінка toast.
- Новий ключ локалізації:
  - `RimChat_RPGHistoryButton`
  - `RimChat_RPGHistoryPanelTitle`
  - `RimChat_RPGHistoryEmpty`
  - `RimChat_RPGHistoryActionPrefix`
  - `RimChat_RPGHistoryActionResultSuccess/Failure/Error`
  - `RimChat_RPGHistoryReasonPrefix`

## Стратегія Caption для дипломатичної передачі зображення та приховування заповнювача під час блокування введення (v0.5.20)

- Налаштування відображення заблокованого введення (`Dialog_DiplomacyDialogue`):
  - Під час блокування поля введення зберігається режим лише для читання, але `DrawLockedInputPreview(...)` більше не відображає текст очікування.
  - Нижній шар стану typing і логіка пріоритету кінцевих станів залишаються без змін.
- Обробка caption `send_image` (`Dialog_DiplomacyDialogue.ImageAction`):
  - Як і раніше, зчитується `parameters.caption`;
  - Якщо значення порожнє, більше не виконується відкат до назви шаблону — натомість використовується локальний резервний шаблон;
  - Резервні заповнювачі: `{leader}`, `{faction}`, `{template_name}`;
  - Якщо після рендерингу значення все ще порожнє, виконується відкат до `RimChat_SendImageDefaultCaption`.
- До збережених налаштувань додано нові поля (`RimChatSettings`):
  - `SendImageCaptionStylePrompt` (типове значення: `PromptTextConstants.SendImageCaptionStylePromptDefault`)
  - `SendImageCaptionFallbackTemplate` (типове значення: `PromptTextConstants.SendImageCaptionFallbackTemplateDefault`)
  - Якщо в старому збереженні відсутнє поле, автоматично використовується типове значення; десеріалізація не порушується.
- Сторінка налаштувань зображень API (`RimChatSettings_ImageApi`):
  - Додано два багаторядкові поля редагування: стильовий промпт caption і локальний резервний шаблон caption.
- Побудова промпту (`PromptPersistenceService.AppendSendImageTemplateGuidance`):
  - Для `SEND_IMAGE TEMPLATE RULE` додано вказівку щодо caption із вимогою спочатку заповнювати `parameters.caption`;
  - Стиль caption зчитується з `SendImageCaptionStylePrompt`;
  - Мова caption має відповідати поточній мові гри.

## Перемикач ін’єкції мініатюр дипломатичного альбому та селфі（v0.5.19）

- `AlbumImageEntry` Додано необов’язкове поле: `sourceType`（`chat/selfie/unknown`）.
  - Якщо в старому збереженні це поле відсутнє, автоматично використовується `unknown`, що не впливає на десеріалізацію.
- Вікно альбому `Dialog_DiplomacyAlbum` оновлено до вигляду сітки карток із мініатюрами:
  - Кеш мініатюр (м’який ліміт + очищення);
  - Значок джерела (зображення з чату/селфі);
  - У контекстне меню додано `复制图片路径`, збережено `打开图片保存目录`.
- Виправлено збереження вбудованих зображень із чату через контекстне меню（`Dialog_DiplomacyDialogue.ImageRendering`）:
  - Тригер змінено на подвійну резервну перевірку `ContextClick + MouseDown(button=1)`;
  - Область спрацьовування змінено на фактичний видимий прямокутник зображення (aspect-fit), а не на весь контейнер.
- У вікні параметрів селфі `Dialog_DiplomacySelfieConfig` додано перемикач ін’єкції:
  - `服饰/体型/发型/武器/植入物/状态` (типово все ввімкнено);
  - За допомогою `SelfiePromptInjectionBuilder` приховувати та додавати до фінального промпту перед надсиланням;
  - Не змінювати текстове поле промпту, введене користувачем вручну.
- Під час ручного додавання селфі з попереднього перегляду до альбому метадані позначаються `sourceType=selfie`; під час додавання до альбому через контекстне меню чату позначаються `sourceType=chat`.

## Дипломатичний альбом і робочий процес селфі (v0.5.18)

- Додано новий тип даних із постійним збереженням у сейві: `AlbumImageEntry`
  - Поля: `id`, `savedTick`, `sourcePath`, `albumPath`, `caption`, `factionId`, `negotiatorId`, `size`.
- `GameComponent_DiplomacyManager` Додано інтерфейс альбому:
  - `bool AddAlbumEntry(AlbumImageEntry entry)`
  - `List<AlbumImageEntry> GetAlbumEntries()`
  - `int PruneMissingAlbumFiles()`
- Додано службу альбомів: `DiplomacyAlbumService`
  - `SaveToAlbum(sourcePath, metadata, out savedEntry, out error)`: копіює файл до каталогу альбому відповідного виміру сейву й автоматично запобігає дублюванню імен.
  - `OpenImageDirectory(item, out error)`: відкриває фактичний каталог збереження вибраного зображення.
- У дипломатичному вікні додано поведінку UI:
  - На головній панелі вкладок додано кнопки: `Album`, `Selfie`.
  - Вбудовані зображення в чаті підтримують контекстне меню: `Save to Album`.
  - Процес створення селфі змінено на: вікно параметрів -> генерація -> вікно попереднього перегляду -> користувач вручну зберігає до альбому (без автоматичного додавання до альбому).
- Сумісність:
  - Додано поле `albumEntries` до збережень; у старих збереженнях відсутнє поле автоматично ініціалізується порожнім значенням.
  - Не змінювати наявний контракт дій `send_image` і структуру файлів промптів.

## текстRPG-текст/текст (v0.5.17）

- До стандартної/користувацької конфігурації Prompt RPG додано нове поле:
  - `RelationshipProfileTemplate`
  - `KinshipBoundaryRuleTemplate`
- Складання поведінки:
  - У `PromptPersistenceService.BuildRpgSystemPromptHierarchical(...)`, коли виконується `isProactive == false`, додається новий вузол `relationship_profile`.
  - Поля вихідних даних вузла: `Kinship` (yes/no), `RomanceState` (spouse/fiance/lover/ex-or-none/none), `Guidance` (результат обробки шаблону правил меж).
- Межі визначення стосунків:
  - Для кровного зв’язку виводиться лише булева наявність (без деталізації типу).
  - Пріоритет романтичних статусів: `spouse -> fiance -> lover -> ex-or-none -> none`.
- Сумісність:
  - Нові поля до збережень не додаються;
  - Якщо в старому `Prompt/Custom/PawnDialoguePrompt_Custom.json` відсутнє нове поле, використовується стандартний запасний варіант;
  - текстRPG-текст (`AppendRpgScenarioTags/HasIntimateRelation` текст）。

## Додавання й видалення шаблонів промптів фракцій та захист шаблонів за замовчуванням（v0.5.16）

- `FactionPromptManager` Новий інтерфейс:
  - `bool TryAddTemplateForFaction(string factionDefName, string displayName, out string status)`
  - `bool TryRemoveTemplate(string factionDefName, out string reason)`
  - `bool IsDefaultTemplate(string factionDefName)`
  - `bool IsFactionMissing(string factionDefName)`
- Джерело каталогу шаблонів за замовчуванням:
  - Під час запуску спочатку побудувати каталог шаблонів за замовчуванням із `Prompt/Default/FactionPrompts_Default.json` (набір `FactionDefName` + джерело клонування конфігурації за замовчуванням).
  - Елементи шаблонів за замовчуванням не можна видаляти (`TryRemoveTemplate` повертає `default_protected`).
- Зміни правил автоматичного доповнення:
  - Доповнювати лише `FactionDefName` у каталозі шаблонів за замовчуванням;
  - Після видалення власноруч доданий шаблон не буде автоматично відновлено під час завантаження.
- Поведінка імпорту:
  - Після імпорту `ImportConfigsFromJson(...)` буде викликано доповнення шаблонів за замовчуванням, щоб правила захисту шаблонів за замовчуванням продовжували діяти.
- Сумісність:
  - Без змін `FactionPrompts_Custom.json` структури JSON;
  - Файли промптів зі старих версій і старі збереження можна читати безпосередньо.

## Керування очікуванням зображень дипломатії та пріоритет завершального стану (v0.5.15)

- Додано до стану виконання (не записується у збереження): `FactionDialogueSession.pendingImageRequests`.
  - Допоміжні методи: `BeginImageRequest()`, `EndImageRequest()`, `HasPendingImageRequests()`.
  - Призначення: уніфіковано позначає, чи асинхронний запит дипломатії `send_image` усе ще обробляється.
- Поведінка життєвого циклу `send_image`:
  - Викликати `BeginImageRequest()` перед запуском генерації зображення.
  - У зворотному виклику (успішному/невдалому) також викликати `EndImageRequest()`; для лічильника забезпечити невід’ємність.
- Правила керування введенням уніфіковано так:
  - `session.isWaitingForResponse == true`, або
  - `session.HasPendingImageRequests() == true`, або
  - дослівне відтворення NPC ще не завершено.
- Пріоритет завершального стану сеансу:
  - Коли `session.isConversationEndedByNpc == true`, у стані області введення спочатку відображати «причину завершення сеансу/повідомлення про затримку відновлення», а не стан typing.
  - Навіть після завершення сеансу пізній зворотний виклик `send_image` усе ще може додавати повідомлення до історії (картку зображення або системне повідомлення про помилку).
- Сумісність: нові поля Scribe не додаються, структура файлів промптів не змінюється, старі збереження сумісні безпосередньо.

## Узгодження порогів розміру зображень, що надсилаються через дипломатію (v0.5.14)

- `send_image` текст `size`Нижню межу перевірки параметра змінено на `>= 3,686,400` пікселів (відповідно до останніх вимог інтерфейсу зображень).
- Якщо параметр action або стара конфігурація задає замалий розмір (наприклад, `1024x1024`), його буде автоматично нормалізовано до стандартного розміру `2560x1440` перед надсиланням запиту.
- Оновлено зіставлення псевдонімів розмірів:
  - `small` / `landscape` -> `2560x1440`
  - `portrait` -> `1440x2560`
  - `medium` -> `3072x1728`
  - `large` -> `3840x2160`
- Сумісність: нові поля збережень не додаються, структура файлів промптів не змінюється, старі збереження й надалі можна читати.

## Інтерфейс надсилання зображень через дипломатію (v0.5.11)

- Додано дію: `send_image`.
  - Контракт параметрів: `template_id` (обов’язковий), `extra_prompt` (необов’язковий), `caption` (необов’язковий), `size` (необов’язковий), `watermark` (необов’язковий).
  - Перевірка відповідності вимогам: для зображення API налаштовано доступність, шаблон існує та ввімкнений, `template_id` не порожній.
- Ланцюжок виконання запиту:
  - На етапі виконання дій у дипломатичному вікні додано перехоплення `TryHandleSendImageAction(...)` (на тому самому рівні, що й presence/social).
  - За раунд можна виконати не більше 1 `send_image`; решту буде безпосередньо проігноровано із системним повідомленням.
- ARK REST контракт запиту (фіксовані поля):
  - Header: `Content-Type: application/json`, `Authorization: Bearer <ImageApiKey>`.
  - Body: `model`, `prompt`, `sequential_image_generation="disabled"`, `response_format="url"`, `stream=false`, `size`, `watermark`.
  - Опис: `size/watermark` типово бере окрему конфігурацію зображення, яку можна перевизначити параметрами action; інші фіксовані поля не можна змінювати через action.
- Правила складання промпту:
  - `模板正文 + extra_prompt + LeaderProfile`.
  - `LeaderProfile` містить: особу лідера (ім’я/звертання/расу/стать), зовнішність (статура/зачіска/борода/видимий одяг), інформацію про фракцію (тип/технології/відносини/передісторію).
  - Якщо персонаж-лідер Pawn відсутній, автоматично використовується опис передісторії на рівні фракції; створення зображення не блокується.
- Обробка відповіді:
  - Наразі підтримується лише гілка `response_format=url`.
  - Спочатку аналізується URL, потім завантажуються байти зображення та зберігаються в кеш-каталозі на рівні збереження, а зрештою результат повертається як вбудована картка зображення в чаті.
  - У разі помилки завантаження або створення це не впливає на текстову відповідь; лише додається системне повідомлення про помилку.
- Сумісність зі збереженнями:
  - `DialogueMessageType` додає `Image`.
  - `DialogueMessageData` додає `imageLocalPath`, `imageSourceUrl` (усі мають значення за замовчуванням, старі збереження можна читати безпосередньо).
## NPC Перемикач розділення ініціативних діалогів（v0.5.8）

- Додано поле конфігурації:
  - `RimChatSettings.EnablePawnRpgInitiatedDialogue`（за замовчуванням `true`, Scribe key: `EnablePawnRpgInitiatedDialogue`）。
- Семантика наявних полів без змін:
  - `RimChatSettings.EnableNpcInitiatedDialogue` і надалі використовується для керування ініціативними дипломатичними діалогами.
- Оновлення керування ініціативними ланцюжками:
  - Ініціативна дипломатія: `GameComponent_NpcDialoguePushManager` і надалі зчитує `EnableNpcInitiatedDialogue`.
  - Ініціатива PawnRPG: `GameComponent_PawnRpgDialoguePushManager.IsFeatureEnabled()` тепер зчитує `EnablePawnRpgInitiatedDialogue && EnableRPGDialogue`.
- Стратегія міграції старих збережень:
  - На етапі завантаження `RimChatSettings.ExposeData_AI()`, якщо у вузлі збереження відсутній `EnablePawnRpgInitiatedDialogue`, йому автоматично присвоюється старе `EnableNpcInitiatedDialogue`, щоб зберегти поведінку старої конфігурації.

## Огляд

`GameAIInterface` — це основний інтерфейсний клас у модулі RimChat, призначений для взаємодії AI з грою. Він надає низку методів API, що дають змогу AI динамічно змінювати стан гри відповідно до змісту діалогу та реалізовувати розумну дипломатичну взаємодію.

## API Вікно налагоджувального спостереження（v0.5.7）

- Розширення інтерфейсу `AIChatServiceAsync.SendChatRequestAsync(...)`（зворотна сумісність）:
  - Додано необов’язковий параметр: `AIRequestDebugSource debugSource = AIRequestDebugSource.Other`。
  - Якщо старий викликач не передає цей параметр, зберігається попередня поведінка.
- Додано перелік категорій джерел: `AIRequestDebugSource`.
  - Значення переліку: `DiplomacyDialogue`, `RpgDialogue`, `NpcPush`, `PawnRpgPush`, `SocialNews`, `StrategySuggestion`, `PersonaBootstrap`, `MemorySummary`, `ArchiveCompression`, `Other`.
- Додано модель налагоджувального спостереження (лише для читання):
  - `AIRequestDebugRecord`: запис окремого запиту (часова позначка, джерело, канал, модель, стан, тривалість, HTTP, токени, повний request/response).
  - `AIRequestDebugSummary`: зведена статистика за вікном (загальна кількість токенів, кількість запитів, показник успішності, середня тривалість, частка токенів дипломатії/RPG).
  - `AIRequestDebugBucket`: статистика за 5-хвилинними інтервалами (загалом 12 інтервалів за останні 60 хвилин).
  - `AIRequestDebugSnapshot`: знімок вікна (summary + buckets + records).
- Додано інтерфейси запитів лише для читання:
  - `AIChatServiceAsync.TryGetRequestDebugSnapshot(out AIRequestDebugSnapshot snapshot)`
  - `AIChatServiceAsync.GetRequestDebugSnapshot()`
- Стратегія збору:
  - охоплює всі джерела запитів `SendChatRequestAsync`, не залежить від `EnableDebugLogging`.
  - Кільцеве зберігання в пам’яті: щонайбільше 2000 записів; автоматично видаляються дані, старші за 65 хвилин; у вікні відображаються фіксовано останні 60 хвилин.
  - Нові поля Scribe не додаються, дані не записуються до збережень, сумісність зі старими збереженнями зберігається.

## Надійність виконання рейду (v0.5.5)

- `DiplomacyEventManager.TriggerRaidEvent(...)`:
  - Якщо після нормалізації стратегії/режиму входу попередня перевірка все ще не проходить або виконання завершується невдачею, примусово додається одна резервна спроба з «оригінальною автоматичною стратегією + автоматичним входом».
  - Мета: уникнути повного провалу `request_raid` через неможливість виконання певної стратегії.
- `GameComponent_DiplomacyManager.ProcessDelayedEvents()`：
  - Подію видалено після успішного виконання.
  - Невдалі події більше не відкидаються одразу, а відкладаються для повторної спроби (не більше 3 разів).
- Розширено сумісність збережень `DelayedDiplomacyEvent`:
  - Додано поля `raidStrategyDefName` / `arrivalModeDefName`.
  - Якщо `Scribe_Defs` не може відновити посилання на Def, дозволено заповнити Def за назвою для сумісності зі старими збереженнями та змінами модів.

## Вибірковий імпорт і експорт пакета промптів + каналібзація RimTalk (v0.5.4)

- Оновлено структуру даних пакета промптів:
  - `PromptBundleConfig.BundleVersion` оновлено до `v2`.
  - Додано `IncludedModules` (білий список модулів).
  - Додано поля каналу RimTalk: `RimTalkDiplomacy`, `RimTalkRpg` (спільний `RimTalkSummaryHistoryLimit`).
- `PromptPersistenceService` отримав нові можливості:
  - `ExportConfig(string filePath, IEnumerable<PromptBundleModule> selectedModules)` (експорт вибору модулів).
  - `ImportConfig(string filePath, IEnumerable<PromptBundleModule> selectedModules)` (імпорт вибору модулів).
  - `TryGetImportPreview(string filePath, out PromptBundleImportPreview preview)` (попередній перегляд імпорту).
- Стратегія сумісності:
  - `v1` старі файли й надалі можна імпортувати; якщо відсутній `IncludedModules`, за замовчуванням використовується весь набір модулів.
  - Старе одноканальне поле RimTalk під час імпорту/завантаження автоматично переноситься у двоканальну конфігурацію «дипломатія + RPG».
- Розширення RimTalk для сумісності моста під час виконання:
  - `RimTalkCompatBridge.GetRuntimeStatus()` надає стан увімкнення, доступність під час виконання та причину збою.
  - `RenderCompatTemplate(...)` / `RenderActivePresetModEntries(...)` тепер використовують канальну конфігурацію відповідно до `channel`.
  - Реєстрація змінних контексту підтримує гнучкіше зіставлення сигнатур через рефлексію: пріоритет має PromptAPI, запасний варіант — ContextHookRegistry.
  - Під час складання параметрів через рефлексію спочатку використовуються стандартні значення цільового методу, що зменшує ризик помилок ін'єкції через відмінності сигнатур у різних версіях RimTalk.
- Додаткові заходи для надійності імпорту й експорту:
  - `ExportConfig(...)` централізовано перевіряє в сервісному шарі порожні значення шляхів і створення каталогів (без залежності від шару UI).
  - `ImportConfig(...)` додає раннє блокування та повідомлення в журналі для порожніх шляхів, порожніх файлів і вибору модулів без перетину.
- Уніфікація шляхів UI:
  - Старий RPG внутрішньосторінковий RimTalk сумісний шлях до інструментів зведено до єдиного варіанта: залишено лише окрему RimTalk сторінку верхнього рівня, щоб уникнути розгалуженого супроводу двох входів.

## Сумісність діалогів невербальних персонажів RPG (v0.5.0)

- Ручний RPG вхід до діалогу більше не обмежує `Human/Humanlike`: його можна ініціювати для всіх цілей-персонажів.
- Категорію невербальності визначено фіксовано як: `Animal` або `Baby` або `Mechanoid`.
- Для відповіді цілі, що належить до невербальної категорії, рівень відображення примусово використовує: `叫声 + （内心想法）` (китайською) або `sound + (inner thought)` (іншими мовами).
- Якщо модель уже вивела коректну структуру «звук + думка в дужках», система зберігає її звук і думку, виконуючи лише нормалізацію дужок.
- Якщо модель не вивела коректну структуру, система повертається до локалізованого типового звуку + початковий текст наміру в дужках.
- Ланцюжок розбору та виконання RPG actions із JSON залишається без змін.

## Посилення ін’єкції Def рас HAR (v0.5.1)

- Селектор Def у XML Patch розширено з фіксованого `ThingDef` до вузла Def із підстановочними символами, сумісного з користувацькими тегами Def у Humanoid Alien Races 2.0.
- Додано інжектор Def під час виконання: після завершення завантаження Def він доповнює `CompPawnDialogue` за результатами розбору, усуваючи випадки пропущеної статичної ін’єкції XML, спричинені ланцюжком успадкування.

## Виправлення помилкових збігів XML (v0.5.2)

- Обсяг ін’єкції XML повернуто до консервативного `ThingDef[defName="Human"]`, щоб уникнути помилкового збігу XML з `PawnKindDef`, який спричиняє помилку поля `<comps>`.
- Перекриття HAR/інших рас і надалі забезпечує `PawnDialogueCompDefInjector` під час виконання.

## Посилення контракту виводу RPG (v0.4.12)

- RPG `actions[]` Поля сумісності розбору:
  - Підтримується `params` (історична форма)
  - Підтримується `parameters` (поширена форма сумісності з OpenAI)
- Обмеження ланцюжка запитів RPG:
  - До звичайних запитів додаватиметься нагадування про суворий контракт виводу; більше не покладаємося лише на додаткове нагадування під час повторної спроби `HTTP 400`.
- Рекомендації щодо структури RPG JSON:
  - Повертайте один і лише один об’єкт верхнього рівня JSON.
  - Розміщуйте видимий текст у `visible_dialogue`.
  - Надавайте `actions` в тому самому об’єкті верхнього рівня лише за потреби ігрового ефекту.
  - Для `action` використовуйте дозволені назви дій (наприклад: `TryGainMemory`), а для параметрів надавайте перевагу `defName` / `amount` / `reason`.

## Безпечне зіставлення та шаблонний розбір Custom URL (v0.4.9)

- `ApiConfig` Додано поле, яке можна серіалізувати:
  - `CustomUrlMode`: `BaseUrl` / `FullEndpoint`.
  - Під час завантаження старої конфігурації одноразово виконується автоматичне визначення: якщо містить `/chat/completions`, відноситься до `FullEndpoint`, інакше — до `BaseUrl`.
- Правила розбору URL під час виконання Custom provider:
  - Відображати лише хост `cloud.siliconflow.*` на `api.siliconflow.cn`.
  - `FullEndpoint`: зберігати оригінальний шлях/параметри запиту, не переписувати кінцеву точку.
  - `BaseUrl`: автоматично доповнювати лише порожній шлях, `/` і `/v1` до `/v1/chat/completions`.
  - Нестандартний шлях залишати без змін (і повертати маркер підказки), щоб уникнути помилкової зміни адрес сумісних шлюзів.
- Список моделей і перевірка підключення:
  - Тестовий ланцюжок `Custom FullEndpoint`: «спочатку `/models`, після помилки — перевірка chat endpoint як запасний варіант».
  - До тексту стану підключення додаються підказки про відображення, підозрілий шлях і спрацювання запасного варіанта, щоб полегшити діагностику поведінки конфігурації.

## Резервний механізм отримання списку моделей (v0.4.7)

- Адресу списку моделей DeepSeek узгоджено з RimTalk, використовується кінцева точка `/models`.
- Із ключа API автоматично видаляються пробіли на початку та в кінці під час запиту списку моделей.
- OpenAI текст,текст,текст JSON текст `id` текст.

## Налаштування значень за замовчуванням для покриття комунікаційної панелі (v0.4.5)

- Зміна значень за замовчуванням:
  - Значення `RimChatSettings.ReplaceCommsConsole` за замовчуванням змінено на `false`.
  - `Scribe_Values.Look(ref ReplaceCommsConsole, "ReplaceCommsConsole", false)` використовується як значення для заповнення за відсутності даних.
- UI Зміна скидання за замовчуванням:
  - `ResetUISettingsToDefault()` Тепер скидає `ReplaceCommsConsole` до `false`.
- Примітки щодо сумісності:
  - Структура збережень не змінюється.
  - Впливає лише на шляхи «значення за замовчуванням/відновити значення за замовчуванням»; пріоритет ручних налаштувань користувача не змінюється.

## Точка входу мосту зв’язку з оригінальною фракцією (v0.4.4)

- Містовий патч UI:
  - `FactionDialogRimChatBridgePatch` (`HarmonyPatch` до `FactionDialogMaker.FactionDialogFor` через `Postfix`).
- Умови спрацьовування:
  - `RimChatMod.Settings != null`
  - `ReplaceCommsConsole = false`
  - Наразі є дійсним кореневим вузлом зв’язку фракції, якою не керує гравець
- Дія ін’єкції:
  - У `DiaNode.options` додати точку входу локалізації `RimChat_UseRimChatContact`.
  - Дія точки входу: `Find.WindowStack.Add(new Dialog_DiplomacyDialogue(faction, negotiator))`.
  - Додати пункт входу `resolveTree = true`, який закриває вікно оригінального дерева зв’язку після натискання.
  - Позицію вставки бажано обрати перед «Вийти/покласти слухавку» (`resolveTree=true && link == null && linkLateBind == null`), інакше додати в кінець.
- Примітки щодо сумісності:
  - Не додає полів до збережень і не змінює зовнішній API.
  - Якщо `ReplaceCommsConsole = true`, цей місток входу не працює, а наявний процес перехоплення зв’язкової консолі залишається без змін.

## Динамічне доповнення контексту дипломатичного промпту（v0.4.3）

- До дипломатичного каналу додано вузол динамічної ін’єкції（`dynamic_data`）:
  - `player_pawn_profile`
  - `player_royalty_summary`
  - `faction_settlement_summary`
    - Виводить «повний список поселень» цієї фракції (більше не виводить лише ключові поселення)
- Стратегія вибору персонажа гравця:
  - Спочатку використовує явно `negotiator` у дипломатичному вікні
  - Якщо відсутній, використовує «колоніста з найвищим показником соціальності»
  - Проактивні дипломатичні повідомлення також використовують стратегію відкату
- Ін’єкція м’яких обмежень Імперії (рівень промпту):
  - Зчитує очки честі Імперії на боці гравця（`Pawn_RoyaltyTracker.GetFavor(faction)`）
  - Зчитує поточний титул фракції（`GetCurrentTitleInFaction(faction)`）
  - Зведення доступності дозволів (`AllFactionPermits`)
  - Виведення `create_quest/request_aid` підказки з м’яким обмеженням (перевірка відповідності на рівні виконання все ще має остаточну силу)
- Додано перезавантаження сервісу (підпис розширено без порушення старих викликів):
  - `BuildFullSystemPrompt(Faction faction, SystemPromptConfig config, bool isProactive, IEnumerable<string> additionalSceneTags, Pawn playerNegotiator)`
- Розширення змінних шаблону:
  - `player_pawn_profile`
  - `player_royalty_summary`
  - `faction_settlement_summary`

## Мирна стратегія за рівнями прихильності (v0.3.164)

- Ланцюжок застосування:
  - Рівень виконання: `ApiActionEligibilityService.ValidateActionExecution(...)`, `TryValidateQuestTemplateForFaction(...)`
  - Рівень промпту: динамічне впровадження `PromptPersistenceService.AppendCompactDiplomacyResponseContract(...)`
- Правила рівнів `make_peace`:
  - `< -50`: заборонити пряме укладення миру (повернути `peace_goodwill_too_low`).
  - `[-50,-21]`: заборонити `make_peace`, вимагати перейти до завдання мирних переговорів (повернути `peace_talk_required`).
  - `[-20,0]`: дозволити `make_peace` (як і раніше, мають бути виконані наявні умови, зокрема умови воєнного часу та затримки відновлення).
  - `> 0`: зберегти наявні правила без змін.
- Правила рівнів `create_quest`:
  - У діапазоні `[-50,-21]`, якщо вказано `questDefName`, дозволено лише `OpportunitySite_PeaceTalks` (`peace_talk_only_range`).
  - Рівень перевірки шаблону завдання також обмежує цей діапазон: дозволено лише `OpportunitySite_PeaceTalks`.
- Динамічне введення промпту:
  - У дипломатичному response contract додано блок `DYNAMIC PEACE POLICY (GOODWILL-BASED)`.
  - Виводити причину вимкнення, альтернативний шлях або доступний шлях відповідно до поточної прихильності, узгоджуючи їх із правилами рівня виконання.

## Уніфікація тайм-аутів моделі (v0.3.158)

- Стратегію тайм-аутів уніфіковано до `40s` (однаково для локальних і хмарних моделей).
- Реалізація охоплює:
  - `AIChatServiceAsync`
  - `AIChatService`
  - `AIChatClient`

## Відновлення після локального тайм-ауту (v0.3.157)

- Область дії: лише режим локальної моделі (`UseCloudProviders = false`).
- Зміни `AIChatServiceAsync`:
  - Тайм-аут локального запиту збільшено з `60s` до `180s` (для хмари зберігається `60s`).
  - У гілці `ConnectionError` додано розпізнавання семантики тайм-аутів: помилки класу тайм-ауту повертають `RimChat_ErrorTimeout`.
  - Для локальних тимчасових помилок з’єднання (тайм-аут, скидання тощо) додано обмежені повторні спроби (2 спроби, коротка затримка + джиттер).
- Спостережуваність: до внутрішнього журналу додано запис `local_conn_retry` для фіксації рішення про повторну спробу (під контролем `LogInternals`).

## Стійкість до помилок 500 і діагностика локальної моделі（v0.3.154）

- Область дії: лише `UseCloudProviders = false` (режим локальної моделі).
- До життєвого циклу запитів `AIChatServiceAsync` додано:
  - Черга одночасного виконання локальних запитів (ліміт одночасності `1`): локальні запити виконуються послідовно за `enqueue -> wait turn -> execute -> release`.
  - Автоматичні повторні спроби для локальних помилок 5xx: спрацьовують лише для `500/502/503/504`, максимум `3` спроб запиту (перша спроба + 2 повторні спроби).
  - Стратегія повторних спроб: коротка затримка перед першою повторною спробою, довша — перед другою; в обох випадках із незначним джитером.
  - Зберігається наявна логіка повторних спроб після зниження рівня обслуговування `HTTP 400 user input rejected`; вона не взаємовиключна з повторними спробами для 5xx.
- Додано діагностичні журнали (керуються наявним перемикачем Debug Internals, без додаткової конфігурації UI):
  - Для кожної спроби запиту виводиться структурований відбиток: `requestId/attempt/channel/model/host/messageCount/jsonBytes/elapsedMs/httpCode`.
  - Вивід рішення щодо повторної спроби локального запиту 5xx: `attempt -> nextAttempt`, `backoffMs`, `responseSummary`.
- Сумісність:
  - Поведінка хмарних provider щодо одночасності та повторних спроб не змінюється.
  - Нові налаштування, видимі користувачеві, не додаються; поля сторінки конфігурації API не змінюються.

## Примусова офіційна адреса DeepSeek（v0.4.6）

- Провайдер DeepSeek повинен використовувати офіційну адресу: `https://api.deepseek.com/v1`.
- Під час читання конфігурації, якщо виявлено неофіційний `BaseUrl`, його автоматично буде нормалізовано до офіційної адреси та записано назад до конфігурації.
- Ланцюжок тестування списку моделей і підключення для DeepSeek більше не використовує власний `BaseUrl`.

## Посилення нормалізації API URL（v0.3.151）

- Виправлено пробільні символи в константах типових URL хмарних постачальників:
  - `AIProviderRegistry.Defs[*].EndpointUrl`
  - `AIProviderRegistry.Defs[*].ListModelsUrl`
- Виправлено локальну типову адресу:
  - Типове значення `LocalModelConfig.BaseUrl` змінено на `http://localhost:11434`.
- Додано інтерфейс нормалізації URL:
  - `ApiConfig.NormalizeUrl(string value)`
  - `ApiConfig.ToModelsEndpoint(string value)`
  - `ApiConfig.EnsureChatCompletionsEndpoint(string baseUrl)`
- Перероблено ланцюжок викликів під час виконання:
  - `ApiConfig.GetEffectiveEndpoint()` уніфіковано повертає нормалізований URL.
  - `AIChatService` / `AIChatServiceAsync` / `AIChatClient` у локальному режимі уніфіковано генерують endpoint chat-completions на основі нормалізованого `BaseUrl`.
  - Ланцюжок отримання моделей і перевірки підключення на сторінці налаштувань змінено на використання нормалізованого URL (хмарного/власного/локального).
- Сумісність і поведінка:
  - Семантика наявних інтерфейсів не змінюється; виправлено лише аномальний шлях, за якого «пробіли в значенні конфігурації призводили до неможливості пройти перевірку URL/до помилки запиту».

## Інтерфейс світових новин соціального кола (v0.3.143)

- Ланцюжок виконання змінено на: `真实事件/公开声明 -> SocialNewsSeed -> LLM 严格 JSON -> PublicSocialPost`.
- `GameComponent_DiplomacyManager.ForceGeneratePublicPost(DebugGenerateReason reason = DebugGenerateReason.ManualButton)`
  - Семантику змінено на «надіслати запит на створення наступної придатної світової новини».
  - Якщо наразі немає подій, про які можна повідомити, AI не налаштовано або створення JSON завершилося невдало, незавершений допис не записується.
  - Обмеження постановки в чергу: кандидат `SocialNewsSeed` більше не зобов’язаний містити фракцію, яку можна розібрати; достатньо пройти базові перевірки коректності, дедуплікації та доступності запиту, щоб потрапити в чергу.
  - Стратегія повторних спроб планувальника: автоматичне планування повторює спробу для джерела `Failed` після 2 днів затримки відновлення; ручна кнопка може негайно повторити спробу для джерела, що завершилося невдало.
- `GameComponent_DiplomacyManager.TryForceGeneratePublicPost(DebugGenerateReason reason, out SocialForceGenerateFailureReason failureReason)`
  - Додано примусовий метод створення з виведенням причини невдачі для точної діагностики.
  - Перелік причин невдачі: `Disabled` (систему вимкнено), `AiUnavailable` (AI недоступний), `QueueFull` (черга запитів заповнена), `NoAvailableSeed` (немає доступних подій), `Unknown` (невідома помилка).
  - Повторна спроба миттєвого збору: якщо під час першого вибору seed сталася помилка, негайно запускається `WorldEventLedgerComponent.CollectNow()` для збору стека Letter і звітів про події, після чого виконується повторний вибір seed.
  - Якщо вдруге seed також не знайдено, повертається `NoAvailableSeed`, а вигадана новина як запасний варіант не створюється.
- `WorldEventLedgerComponent.CollectNow()`
  - Додано метод ручного примусового збору, який негайно виконує `PollLetterStackEvents` і `UpdateRaidBattleStates`.
  - Використовується для синхронного збору найновіших світових подій і зведень перед примусовою генерацією, щоб забезпечити негайну доступність.
- `GameComponent_DiplomacyManager.EnqueuePublicPost(...)`
  - Як і раніше є точкою входу під час виконання для `publish_public_post`, але тепер усередині не компонує текст шаблону безпосередньо, а надсилає діалогове новинне зерно з коротким викладом фактів.
- Нові внутрішні типи:
  - `SocialNewsSeed`: уніфіковані фактичні вхідні дані про світові події, зведення боїв, спогади лідерів, дипломатичні підсумки та публічні заяви.
  - `SocialNewsOriginType`: позначає тип джерела новин.
  - `SocialNewsGenerationState`: позначає стан дедуплікації джерела / результату генерації.
  - `SocialForceGenerateFailureReason`: позначає причину невдалої примусової генерації (v0.3.144).
  - `SocialNewsJsonParser`: перевіряє суворий контракт `headline / lead / cause / process / outlook / quote / quote_attribution` JSON.
- Нові поля збереження `PublicSocialPost`:
  - `OriginType`, `OriginKey`, `Headline`, `Lead`, `Cause`, `Process`, `Outlook`, `Quote`, `QuoteAttribution`, `SourceLabel`, `CredibilityLabel`, `CredibilityValue`, `GenerationState`.
  - Обмеження очищення під час завантаження: історичні дописи не видалятимуться через одночасну відсутність `SourceFaction/TargetFaction`; чи відображати рядок дійових осіб у дописах із двома порожніми сторонами, визначає лише UI.
  - Обмеження рядка дійових осіб UI (v0.3.144): за наявності двох фракцій відображається `A → B`; за наявності лише однієї фракції відображається рядок однієї фракції (`RimChat_SocialNewsSingleFactionLine`); якщо обидві фракції відсутні, рядок дійових осіб не відтворюється. Фракція гравця вважається дійсною однією фракцією.
- Додано новий розподіл сховищ промптів соціального кола:
  - `SocialCircleNewsStyleTemplate`
  - `SocialCircleNewsJsonContractTemplate`
  - `SocialCircleNewsFactTemplate`
- RimChat Захист від ланцюга рейдів (v0.3.144):
  - `DiplomacyEventManager.TriggerRaidEvent` Додано нормалізацію стратегії/режиму прибуття та попередню перевірку можливості виконання.
  - Якщо стратегію/режим прибуття не вказано або їх неможливо виконати, автоматично обирається виконуване значення за замовчуванням (пріоритет `ImmediateAttack` / `EdgeWalkIn`).
  - У разі невдачі ведеться зрозумілий журнал, що запобігає винятку порожньої множини RandomElement.

## Поточна файлова система промптів (v0.3.137)

- Тепер стандартні промпти розподілено за сферами у 5 файлів:
  - `Prompt/Default/SystemPrompt_Default.json`
  - `Prompt/Default/DiplomacyDialoguePrompt_Default.json`
  - `Prompt/Default/PawnDialoguePrompt_Default.json`
  - `Prompt/Default/FactionPrompts_Default.json`
  - `Prompt/Default/SocialCirclePrompt_Default.json`
- Користувацькі промпти під час виконання записуються у `Prompt/Custom/*_Custom.json` відповідної сфери; `system_prompt_config.json` і `RpgPrompts_Custom.json` більше не використовуються.
- `PromptPersistenceService.LoadConfig/SaveConfig/ExportConfig/ImportConfig` тепер відповідає за складання/розділення трьох типів агрегованих конфігурацій: системних, дипломатичних і соціального кола; файл персонажа/RPG читається та зберігається через ланцюг `PawnDialoguePrompt_*`.

## Поточний контракт промпту (v0.3.120)

- Стандартний контракт вихідних даних дипломатичного каналу уніфіковано: повертати лише один об’єкт верхнього рівня JSON; `visible_dialogue` містить репліку персонажа, а якщо потрібен gameplay effect, його слід надати в тому самому об’єкті верхнього рівня через `actions`.
- Дипломатичний канал більше не приймає старий шаблон виводу з одним `action / parameters / response`; приймається лише протокол `{"actions":[...]}`.
- Типові дипломатичні тексти й шаблони тепер надає `Prompt/Default/DiplomacyDialoguePrompt_Default.json`.
- Налаштування ролі RPG, обмеження формату, надійність дій, початкові цілі та типові значення topic shift тепер надає `Prompt/Default/PawnDialoguePrompt_Default.json`.
- `reject_request` використовується лише для «офіційної відмови на чіткий запит гравця»; звичайну усну відмову слід безпосередньо висловлювати реплікою персонажа.
- `publish_public_post` — це публічна дія зі значним впливом на світ, тому її слід застосовувати лише для публічних заяв, адресованих усій фракції, а не для звичайної розмови чи приватного торгу.

## Скорочення дій дипломатичного промпту (v0.3.142)

- Із типового дипломатичного промпту вилучено `send_gift`; ця дія більше не доступна LLM.
- `send_gift` у конфігураціях старих збережень/старих користувацьких промптів буде вилучено під час автоматичного виправлення конфігурації промпту.
- `GameAIInterface.SendGift(...)` залишається довідником для старої логіки API; ця зміна впливає лише на доступність дій у дипломатичному промпті.

## Фіксовані витрати дипломатичних діалогів (v0.3.116)

- Фіксовані поведінкові витрати в дипломатичних діалогах більше не виражаються опосередковано через LLM за допомогою `adjust_goodwill`, а автоматично додаються системою після успішного виконання API.
- `request_caravan`: лише якщо `parameters.apply_goodwill_cost=true`, після успішного виконання фіксовано витрачається базове значення `-15` прихильності (типово `false`).
- `request_aid`: лише якщо `parameters.apply_goodwill_cost=true`, після успішного виконання фіксовано витрачається базове значення `-25` прихильності (типово `false`); `Military` / `Medical` / `Resources` обробляються однаково відповідно до `-25`.
- `create_quest`: після успішного виконання фіксовано витрачається базове значення `-10` прихильності.
- `send_gift`: реалізацію старої логіки збережено, але типові дипломатичні промпти більше не містять цієї дії.
- Лише `adjust_goodwill` використовується для вираження «додаткової зміни прихильності, спричиненої контекстом»; не використовуйте його повторно для позначення наведених вище фіксованих системних витрат.

## Динамічне впровадження та оригінальна затримка відновлення（v0.3.117）

- Динамічне впровадження дій через промпт спочатку перевіряє `request_caravan`、`request_aid`、`create_quest` за фіксованою вартістю.
- Якщо після виконання цієї дії поточна прихильність стане нижчою за `0`, дія не з’явиться у списку доступних дій, впровадженому для LLM.
- Затримку відновлення `request_aid` змінено на `1` днів, а затримку відновлення `request_caravan` — на `4` днів, щоб узгодити їх з оригінальною версією.

## Ключові особливості

- **Обмеження безпеки**: зміна прихильності має разовий ліміт і добовий сукупний ліміт
- **Контроль частоти**: кожен метод API має незалежний час затримки відновлення
- **Детальні журнали**: повний журнал викликів API і відстеження помилок
- **Налаштовуваність**: усі порогові значення обмежень можна змінити в параметрах моду
- **Ініціативний діалог**: NPC може самостійно розпочати діалог, перебуваючи онлайн (лист праворуч / безпосередній перехід до сеансу)

## Виправлення аналізу відповіді та життєвого циклу UI（v0.3.114）

### Аналіз відповіді AI

- Додано: `AIJsonContentExtractor`
  - Надає `IsErrorPayload(string json)` і `TryExtractPrimaryText(string json, out string content)`.
  - Призначення: сумісність із коливаннями форматування, як-от пробілами, перенесеннями рядків і escape-символами, щоб зменшити ймовірність помилки вилучення `"content"`.
- `AIChatService.ParseResponse(...)` / `AIChatServiceAsync.ParseResponse(...)`
  - Уніфіковано виклик `AIJsonContentExtractor`, вилучено жорстко закодований розбір фрагментів рядка `IndexOf`.

### Життєвий цикл події MainTab

- `MainTabWindow_RimChat`
  - Додано `EnsureGoodwillEventSubscription()` / `ClearGoodwillEventSubscription()`.
  - У `PreOpen` гарантовано виконання підписки, а в `PreClose` — скасування підписки; виправлено проблему, через яку після повторного відкриття вікна анімація прихильності більше не відтворювалася.

### Резервний розбір назви збереження

- `LeaderMemoryManager.GetCurrentSaveName()`
  - Якщо `Current.Game?.Info` недоступний, додано резервне рефлексивне читання `ScribeMetaHeaderUtility.loadedGameName`.
  - Додано евристичне сканування рядкових членів, щоб зменшити ризик помилки розбору через зміни внутрішніх членів рушія.

## Посилення життєвого циклу асинхронних запитів (v0.3.113)

### Додано/змінено в AIChatServiceAsync

- `NotifyGameContextChanged(string reason)`
  - Призначення: під час завантаження збереження або початку нової гри повідомляти асинхронну службу про зміну контексту та скасовувати відкладені запити старого контексту.
- `CancelAllPendingRequests(string reason = "...")`
  - Призначення: пакетне скасування запитів Pending/Processing.
- `CleanupCompletedRequests()` (покращення поведінки)
  - Тепер внутрішній планувальник сервісу запускає його за розкладом (10 секунд) і обрізає кількість запитів у кінцевому стані, щоб запобігти нескінченному накопиченню історичних запитів.

### Новий контролер дипломатичного каналу

- `DiplomacyConversationController.TrySendDialogueRequest(...)`
  - Уніфіковано надсилання дипломатичних запитів AI, із внутрішньою перевіркою життєвого циклу `FactionDialogueSession`.
- `DiplomacyConversationController.CancelPendingRequest(FactionDialogueSession session)`
  - Під час закриття вікна діалогу скасовуються призупинені запити, щоб зворотний виклик не записував дані в недійсний контекст вікна.

### Точки підключення GameComponent

- `GameComponent_DiplomacyManager.StartedNewGame()` / `LoadedGame()`
  - Тепер викликається `AIChatServiceAsync.NotifyGameContextChanged(...)`, що гарантує: запити між збереженнями не забруднюють стан нового сеансу.

## Зміни інтерфейсу Prompt Policy V4 (v0.3.163)

### Модель конфігурації

- `PromptTemplateTextConfig` додано нові поля:
  - `DecisionPolicyTemplate`
  - `TurnObjectiveTemplate`
  - `TopicShiftRuleTemplate`
- `SystemPromptConfig` додано нові поля:
  - `PromptPolicySchemaVersion` (поточне значення за замовчуванням: `4`)
  - `PromptPolicy`
- Поточна публічна конфігурація `PromptPolicyConfig`:
  - `Enabled`
  - `EnableIntentDrivenActionMapping`
  - `IntentActionCooldownTurns`
  - `IntentMinAssistantRoundsForMemory`
  - `IntentNoActionStreakThreshold`
  - `ResetPromptCustomOnSchemaUpgrade`
  - `SummaryTimelineTurnLimit`
  - `SummaryCharBudget`

### Точка входу складання промпту (сигнатура не змінюється)

- `BuildFullSystemPrompt(Faction faction, SystemPromptConfig config, bool isProactive, IEnumerable<string> additionalSceneTags)`
- `BuildRPGFullSystemPrompt(Pawn initiator, Pawn target, bool isProactive, IEnumerable<string> additionalSceneTags)`

Сигнатури наведених вище інтерфейсів залишаються незмінними; усередині зберігається складання на рівні політик (обрізання бюджету токенів промпту більше не виконується):
- Нові вузли: `decision_policy`, `turn_objective`, `topic_shift_rule`;
- Додатковий вузол першого раунду RPG: `opening_objective`;
- Обмеження дипломатичного каналу API (поріг/затримка відновлення/ліміт) і вміст підказки `api_limits` не змінюються.

### Додатковий інтерфейс RPG

- `RpgNpcDialogueArchiveManager.BuildPromptMemoryBlock(Pawn targetNpc, Pawn currentInterlocutor = null, int summaryTurnLimit = 8, int summaryCharBudget = 1200)`
  - Тепер підтримуються параметри бюджету підсумку: вивід «спочатку підсумок історії сеансів + кілька дослівних фрагментів із найновішого повного сеансу».
- `RpgNpcDialogueArchiveManager.BuildUnresolvedIntentSummary(Pawn targetNpc, Pawn currentInterlocutor = null)`
  - Невирішені наміри виокремлюються лише з «найновіших збережених повних сеансів» і використовуються для складання вузлів `turn_objective`.

## Використання токенів в останньому діалозі (v0.3.28)

- UI Розташування: внизу сторінки `Mod 设置 -> API 配置`.
- Формат відображення: `最近一次对话Token使用量：xxxx（低/中/高）`.
- Обсяг статистики: лише запити, ініційовані у вікні дипломатичних діалогів і вікні діалогів RPG.
- Методика підрахунку:
  - Спочатку зчитувати поле token з об’єкта відповіді `usage` (сумісність із `prompt_tokens/input_tokens/promptTokenCount`, `completion_tokens/output_tokens/candidatesTokenCount`, `total_tokens/totalTokenCount`).
  - Якщо usage відсутнє або надто сильно відрізняється від локальної оцінки, використовувати резервну оцінку за «текстом запиту + відповіді (4 символи ≈ 1 token)» і позначати стан оцінки.
- Порогові рівні:
  - Низький: `<=1200`
  - Середній: `1201~3000`
- Високий: `>3000`

---

## Оновлення поведінки діалогів RPG (v0.3.137)

- `ExitDialogueCooldown`: тривалість затримки відновлення — `60000` ticks (1 день).
- Відкат пам’яті: після того, як діалог RPG досягне `5` раундів, один раз виконується перевірка ймовірності `80%`; у разі успіху автоматично додається `TryGainMemory`.
- `TryGainMemory`: стандартний пул пам’яті перемкнено на 28 багаторівневих Def пам’яті RimChat; старі token (як-от `Chitchat` / `DeepTalk` / `Slighted` і старі 3 власні DefName) автоматично перенаправлятимуться до нових Def.
- Автоматичне доповнення пам’яті: звичайний резервний варіант вибирає лише з пулу позитивної прогресії (легкі -> середні -> глибокі позитивні спогади) і не переходить автоматично до четвертого рівня — філософських/ключових спогадів.
- Системний зворотний зв’язок: під час застосування/додавання пам’яті відображати локалізовану мітку пам’яті, а не оригінальний `defName`.
- Однорядковий діалог: RPG NPC видимі репліки під час відображення згортають переноси рядків/табуляцію/послідовні пробіли до однорядкового тексту.
- Пагінація довгих повідомлень: коли RPG основний текст діалогу перевищує текстову область діалогового вікна, після завершення введення тексту в повідомленні вмикається пагінація; перегляд історії також підтримує пагінацію.
- Стратегія виконання Recruit: автоматичне доповнення більше не виконується; виконується лише дія Recruit з оригінального виводу моделі.
- Візуалізація системних підказок: RPG у діалозі системна інформація тепер відображається на напівпрозорій панелі, де також показано залишковий час затримки відновлення та результат перевірки пам’яті.

---

## RPG-дипломатичний двобічний ланцюжок пам’яті（v0.3.29）

### Основна модель

- `CrossChannelSummaryRecord`
  - Поля: `Source`、`FactionId`、`PawnLoadId`、`PawnName`、`SummaryText`、`KeyFacts`、`GameTick`、`Confidence`、`ContentHash`、`IsLlmFallback`、`CreatedTimestamp`。

### Основні служби

- `DialogueSummaryService.TryRecordDiplomacySessionSummary(Faction faction, List<DialogueMessageData> allMessages, int baselineMessageCount)`
  - Викликається після закриття дипломатичного вікна: лише за наявності нових повідомлень створює та записує 1 підсумок дипломатичної сесії.
- `DialogueSummaryService.TryRecordRpgDepartSummary(Pawn pawn, RpgDialogueTraceSnapshot trace)`
  - Викликається, коли персонаж неігрової фракції виконує `ExitMap` і відповідає умовам фільтрації: створює підсумок подій поза мапою та записує його в пам’ять фракції.
- `DialogueSummaryService.BuildRpgDynamicFactionMemoryBlock(Faction faction, Pawn targetPawn)`
  - Під час початку розмови RPG створює спільний динамічний блок пам’яті фракції (складається під час виконання, не перезаписує постійні поля Persona).

### Ланцюжок запуску

- RPG -> Дипломатія:
  - `PawnExitMapPatch_RpgMemory` Patch `Pawn.ExitMap(bool, Rot4)`.
  - Умова фільтрації: `非玩家派系 + 人形 + 玩家家园地图 + 最近有 RPG 对话痕迹`.
  - текст：`RpgDialogueTraceTracker.RegisterTurn(...)`（RPG текст、текст、NPCтекст）。
- Дипломатія -> RPG:
  - `Dialog_DiplomacyDialogue` записує базову лінію повідомлень під час відкриття вікна;
  - Якщо під час `PreClose` виконується `session.messages.Count > baseline`, запускається підсумок сеансу.

### Стратегія та бюджет підсумків

- Стратегія: пріоритет правил; низька впевненість (<0.65) запускає резервний варіант LLM; якщо AI недоступний, зберігається підсумок на основі правил.
- Бюджет:
  - Ліміт пулу підсумків: `20` (пул виходу з карти — 20, дипломатичний пул — 20)
  - Кількість ін'єкцій RPG: `6`
  - Максимальна загальна довжина ін'єкцій: `2200` символів

### Сумісність із персистентністю

- `LeaderMemoryJsonCodec` Виправлено невідповідність у відображенні полів читання й запису (наприклад, `ownerFactionId/leaderName` сумісне зі старим полем).
- Додано поля `rpgDepartSummaries` / `diplomacySessionSummaries` JSON.
- Якщо в старому збереженні відсутні нові поля, автоматично використовується порожній список, без помилки.

### Ініціалізація прийняття збереження (v0.3.30)

- `LeaderMemoryManager.OnNewGame()`
  - На початку нового збереження для всіх непідконтрольних гравцеві фракцій одразу створюється базовий знімок пам’яті із записом поточної прихильності/типу відносин, щоб під час першого діалогу пам’ять не була порожньою.
- `LeaderMemoryManager.OnAfterGameLoad(IEnumerable<FactionDialogueSession> loadedSessions)`
  - Після завантаження збереження та пам’яті JSON наявні в збереженні `FactionDialogueSession.messages` додаються до пам’яті фракції.
  - Для фракцій без ініціалізованої базової лінії одноразово записується подія `init-snapshot` і початкові значення 5 вимірів відносин (синхронізовані з поточною прихильністю).
  - Під час додавання використовується дедуплікація: імпортуються лише повідомлення сеансів, які «новіші» за поточну пам’ять.

### Незалежне збереження RPG за NPC (v0.3.31)

- Додано менеджер: `RpgNpcDialogueArchiveManager`
  - `RecordTurn(Pawn initiator, Pawn targetNpc, bool isPlayerSpeaker, string text, int tick, string sessionId = null)`
  - `FinalizeSession(Pawn initiator, Pawn targetNpc, string sessionId, List<ChatMessageData> chatHistory)`
  - `OnBeforeGameSave()`
  - `OnAfterGameLoad()`
- Шлях збереження: `Prompt/NPC/<saveName>/rpg_npc_dialogues/npc_<pawnId>.json` (старий `save_data/<saveName>/rpg_npc_dialogues` автоматично мігрується)
- Поля NPC досьє:
  - `PawnLoadId`, `PawnName`, `FactionId`, `FactionName`
  - `LastInteractionTick`, `CooldownUntilTick`
  - `PersonaPrompt`
  - `Sessions[]`（`SessionId/StartedTick/EndedTick/TurnCount/IsFinalized/Interlocutor*/SummaryText/SummaryState/LastSummaryAttemptTick/Turns[]`）
  - Стратегія стиснення: повністю зберігати лише останні `TurnCount>=2` сесій; для решти «завершених сесій（IsFinalized=true）」використовувати суворе однореченнєве резюме LLM, у разі помилки позначати `summary_failed` і зберігати оригінальний текст.

- Додано інтерфейси стану виконання `GameComponent_RPGManager` (для заповнення досьє):
  - `TryGetRelation(Pawn pawn, out RPGRelationValues relation)`
  - `SetRelationValues(Pawn pawn, RPGRelationValues relationValues)`
  - `GetDialogueCooldownUntilTick(Pawn pawn)`
  - `SetDialogueCooldownUntilTick(Pawn pawn, int untilTick)`

---

## Інтерфейс промпту середовища（v0.3.23）

текст `PromptPersistenceService` текст/RPG-текст.

### Основна точка входу

- `BuildFullSystemPrompt(Faction faction, SystemPromptConfig config, bool isProactive, IEnumerable<string> additionalSceneTags)`
- `BuildRPGFullSystemPrompt(Pawn initiator, Pawn target, bool isProactive, IEnumerable<string> additionalSceneTags)`
- `BuildEnvironmentPromptBlocks(SystemPromptConfig config, DialogueScenarioContext context)` (внутрішня точка входу складання)
- `AppendRecentWorldEventIntel(StringBuilder sb, EnvironmentPromptConfig env, DialogueScenarioContext context)` (внутрішній блок ін'єкції)

### Розділення оркестрації побудови промпту (v0.3.61)

- Точкою входу складання дипломатії все ще є:
  - `PromptPersistenceService.BuildFullSystemPrompt(...)`
  - Тепер передається до `RimChat.Prompting.Builders.DiplomacyPromptBuilder.Build(...)`, а потім переходить до побудови ядра ієрархії.
- Точкою входу складання RPG все ще є:
  - `PromptPersistenceService.BuildRPGFullSystemPrompt(...)`
  - Тепер передається до `RimChat.Prompting.Builders.RpgPromptBuilder.Build(...)`, а потім переходить до побудови ядра ієрархії.
- Складання ядра ієрархії (внутрішнє):
  - `PromptPersistenceService.BuildFullSystemPromptHierarchicalCore(...)`
  - `PromptPersistenceService.BuildRpgSystemPromptHierarchicalCore(...)`
- Файл конфігурації промпту IO (внутрішній):
  - `RimChat.Persistence.PromptConfigStore.Exists()`
  - `RimChat.Persistence.PromptConfigStore.ReadAllText()`
  - `RimChat.Persistence.PromptConfigStore.WriteAllText(string content)`

Пояснення: це розділення змінює лише структуру рівня компонування, не змінюючи дипломатію/RPG остаточний промпт і поведінку виконання.

### Кодування й декодування промпту JSON（v0.3.62）

- Додано внутрішній кодувальник: `RimChat.Persistence.PromptConfigJsonCodec`
  - `TrySerialize(SystemPromptConfig config, bool prettyPrint, out string json)`
  - `TryDeserialize(string json, out SystemPromptConfig config, out string error)`
- `PromptPersistenceService` зміни:
  - `SerializeConfigToJson(...)`: спочатку використовується типізований кодек, а в разі невдачі виконується відкат до серіалізації через конкатенацію рядків у старій версії.
  - `ParseJsonToConfigInternal(...)`: спочатку використовується типізований кодек, а в разі невдачі виконується відкат до аналізу рядків у старій версії.

Пояснення: ця зміна передусім підвищує надійність читання та запису конфігурації, водночас зберігаючи старий ланцюжок аналізу як сумісний резервний варіант.

### Виправлення промпту JSON під час виконання（v0.3.63）

- `PromptConfigJsonCodec` зміни:
  - Серіалізація: `UnityEngine.JsonUtility.ToJson(...)`
  - Десеріалізація: `UnityEngine.JsonUtility.FromJson<SystemPromptConfig>(...)`
- Сумісність із моделями:
  - Для `SystemPromptConfig` додайте відповідні класи конфігурації та `EventIntelPromptConfig` збільште `[Serializable]`.
- Налаштування залежностей проєкту:
  - Видалити `System.Web.Extensions`, додати `UnityEngine.JSONSerializeModule`.

Ціль виправлення: усунути RimWorld під час виконання `TypeLoadException`(`System.Web.Script.Serialization.JavaScriptSerializer` неможливо розібрати) і зберегти незмінним сумісний ланцюжок запасного варіанта читання та запису конфігурації.

### Винесення текстових шаблонів промптів назовні (v0.3.64)

- Додати модель конфігурації: `PromptTemplateTextConfig`
  - `Enabled`
  - `FactGroundingTemplate`
  - `OutputLanguageTemplate`
- Додати до кореневого вузла `SystemPromptConfig`:
  - `PromptTemplates`
- Рендерер шаблонів:
  - `PromptTemplateRenderer.Render(string templateText, IReadOnlyDictionary<string, string> variables)`
  - Синтаксис: `{{variable_name}}`
  - Незіставлені змінні залишаються без змін (для полегшення налагодження)
- Інтеграція пошарове складання:
  - Вузол `fact_grounding`: спочатку рендерити `PromptTemplates.FactGroundingTemplate`
  - Вузол `output_language`: спочатку рендерити `PromptTemplates.OutputLanguageTemplate`
  - Якщо шаблон порожній або не ввімкнений, використовувати стару логіку
- Доступні змінні (спільні):
  - `{{channel}}`（`diplomacy` / `rpg`）
  - `{{mode}}`（`manual` / `proactive`）
  - `{{target_language}}`
  - `{{faction_name}}`
  - `{{initiator_name}}`
  - `{{target_name}}`

### Зовнішнє розширення шаблону тексту промпту（v0.3.65）

- `PromptTemplateTextConfig` Нові поля:
  - `DiplomacyFallbackRoleTemplate`
- Додано підключення пошарової побудови:
  - Дипломатичний `faction_characteristics` за відсутності промпту, спеціального для фракції, спочатку відтворює `DiplomacyFallbackRoleTemplate`.
  - RPG `role_setting`, обмеження формату, надійність, початкові цілі та topic shift тепер надаються через `Prompt/Default/RpgPrompts_Default.json`, а не зчитуються з дипломатичного `PromptTemplates`.

### Шаблон правил дій соціального кола（v0.3.105）

- `PromptTemplateTextConfig` Нові поля:
  - `SocialCircleActionRuleTemplate`
- Підключення пошарової побудови:
  - Дипломатичний канал `instruction_stack` доповнено вузлом `social_circle_action_rule`;
  - Якщо `PromptTemplates.Enabled == true` і шаблон не порожній, відтворюється `SocialCircleActionRuleTemplate`;
  - Якщо шаблон порожній, використовується вбудований мінімальний текст правил.
- Ланцюжок персистентності:
  - Значення за замовчуванням береться з `Prompt/Default/SystemPrompt_Default.json`;
  - Зміни під час виконання зберігаються в `Prompt/Custom/system_prompt_config.json`;
  - Якщо в старій конфігурації це поле відсутнє, на етапі завантаження його автоматично заповнюють із шаблону за замовчуванням.

### Шаблон обгортки вузла промпту (v0.3.66)

- Нові поля `PromptTemplateTextConfig`:
  - `ApiLimitsNodeTemplate`
  - `QuestGuidanceNodeTemplate`
  - `ResponseContractNodeTemplate`
- На етапі пошарового формування додано етап рендерингу обгортки (дипломатичний канал):
  - `api_limits`: спочатку формується динамічний текст, потім його обгортає шаблон
  - `quest_guidance`: спочатку формується динамічний текст, потім його обгортає шаблон
  - `response_contract`: спочатку формується динамічний текст, потім його обгортає шаблон
- Заповнювач за замовчуванням:
  - `{{api_limits_body}}`
  - `{{quest_guidance_body}}`
  - `{{response_contract_body}}`

### Очищення дублікатів тексту промпту (v0.3.67)

- Змінено джерело тексту полів шаблону за замовчуванням:
  - Довгі значення за замовчуванням більше не дублюються в коді конструктора.
  - `Prompt/Default/SystemPrompt_Default.json` є єдиним джерелом довгого тексту шаблону за замовчуванням.
- Змінено fallback під час формування:
  - Якщо шаблон відсутній, повертати стислий резервний промпт замість повторного копіювання всього тексту шаблону за замовчуванням.

### Єдине джерело констант Prompt（v0.3.68）

- Додано: `PromptTextConstants`
  - Уніфіковано зберігання констант текстів повторюваних промптів（RPG промпт за замовчуванням, описи й параметри деяких промптів дій API）.
- Скориговано:
  - `RimChatSettings` За замовчуванням RPG читання промпту змінено на посилання на константу (ініціалізація, значення Scribe за замовчуванням, резервний варіант під час міграції).
  - `SystemPromptConfig` текст `PromptPersistenceService` текст API текст.

### Уніфікація констант розділів Prompt（v0.3.69）

- `PromptTextConstants` Додано константи заголовків розділів контракту відповіді:
  - `ACTIONS`
  - `DECISION GUIDELINES`
  - `RESPONSE FORMAT`
  - А також універсальні рядки промпту relation/important/no-action
- `AppendSimpleConfig` / `AppendAdvancedConfig` тепер уніфіковано посилаються на наведені вище константи, щоб уникнути повторного обслуговування промптів в одному розділі.

### Виправлення заповнення шаблону Prompt（v0.3.70）

- Додано стратегію міграції:
  - Якщо певне поле `PromptTemplates` у запущеній конфігурації порожнє, під час завантаження воно автоматично заповнюється зі стандартного шаблону конфігурації.
- Джерело заповнення:
  - `Prompt/Default/SystemPrompt_Default.json`
- Поведінка:
  - Заповнюються лише «відсутні значення», уже заповнені користувачем поля шаблону не перезаписуються.
  - Після заповнення конфігурація автоматично зберігається, щоб надалі поля знову не залишалися порожніми.

### Порядок ін'єкції

- `Worldview -> Environment Parameters -> Recent World Events & Battle Intel -> Scene Prompt Layers -> Existing Prompt Stack`

### EnvironmentContextSwitches (нове)

- `Enabled`
- `IncludeTime`
- `IncludeDate`
- `IncludeSeason`
- `IncludeWeather`
- `IncludeLocationAndTemperature`
- `IncludeTerrain`
- `IncludeBeauty`
- `IncludeCleanliness`
- `IncludeSurroundings`
- `IncludeWealth`

Наведені перемикачі керують поетапною ін'єкцією параметрів середовища; якщо конфігурація відсутня, автоматично використовуються стандартні значення (сумісність зі старими конфігураціями).

### EventIntelPrompt (нове)

- `Enabled`
- `ApplyToDiplomacy`
- `ApplyToRpg`
- `IncludeMapEvents`
- `IncludeRaidBattleReports`
- `DaysWindow`
- `MaxStoredRecords`
- `MaxInjectedItems`
- `MaxInjectedChars`

### Інтерфейс книги обліку світових подій (нове)

- `WorldEventLedgerComponent : GameComponent`
- `WorldEventRecord`
- `RaidBattleReportRecord`
- `GetRecentWorldEvents(Faction observerFaction, int daysWindow, bool includePublic, bool includeDirect)`
- `GetRecentRaidBattleReports(Faction observerFaction, int daysWindow, bool includeDirect)`

Правила доступності інформації:
- `PublicKnown` (публічні події на мапі) додаються до зведення відповідно до `IsPublic=true`.
- `DirectKnown` (події з безпосередньою участю фракції) фільтруються відповідно до `KnownFactionIds`; підтримується повне зведення втрат у звіті про напад.

---

## Інтерфейс активних діалогів NPC (v0.3.9)

Активні діалоги централізовано координує `GameComponent_NpcDialoguePushManager`, а зовнішні Patch повідомляють про події-тригери через метод входу.

### Визначення типів

- `NpcDialogueTriggerType`
  - `Ambient` / `Conditional` / `Causal`
- `NpcDialogueCategory`
  - `Social` / `DiplomacyTask` / `WarningThreat`
- `NpcDialogueTriggerContext`
  - Контекст тригера під час виконання (фракція, тип тригера, причина, серйозність, зміна прихильності тощо)
- `QueuedNpcDialogueTrigger`
  - Підготовлений до збереження елемент черги затримки (містить `dueTick/expireTick`)
- `FactionNpcPushState`
  - Стан надсилання фракції (затримка відновлення, остання взаємодія, останній негативний сплеск)

### Точка входу для звітування Patch

```csharp
// 交易后置：玩家卖出 Poor 及以下武器
GameComponent_NpcDialoguePushManager.Instance?.RegisterLowQualityTradeTrigger(
    faction,
    lowQualityCount,
    worstQuality
);

// 好感变动后置：单次绝对变化 >= 10
GameComponent_NpcDialoguePushManager.Instance?.RegisterGoodwillShiftTrigger(
    faction,
    goodwillDelta,
    reasonTag,
    likelyHostile
);

// UI 帧内鼠标左键采样（忙碌判定）
GameComponent_NpcDialoguePushManager.Instance?.RegisterPlayerLeftClick();
```

### Точка входу для налагодження

```csharp
// 强制触发一条随机主动对话（调试按钮调用）
bool ok = GameComponent_NpcDialoguePushManager.Instance?.DebugForceRandomProactiveDialogue() == true;
```

### Інтерфейс доставки

- `ChoiceLetter_NpcInitiatedDialogue`
  - `Setup(Faction faction, TaggedString labelText, TaggedString bodyText, LetterDef letterDef)`
  - `IsDialogueAlreadyOpen(Faction faction)`
  - Варіант листа містить «Відкрити дипломатичний діалог», що безпосередньо запускає `Dialog_DiplomacyDialogue`

### Правила роботи (фіксована стратегія)

- Частота оцінювання: одна звичайна оцінка кожні `6000` тіків; обробка черги кожні `600` тіків.
- Затримка відновлення: після успішного проактивного повідомлення тієї самої фракції настає випадкова затримка відновлення на `1~3` днів.
- Перевірка зайнятості (потрійна):`Drafted` / ворожий загін на мапі поселення гравця / `6` секунд натисніть лівою кнопкою миші на `>=12`.
- Контроль доступу онлайн: лише `Online` ініціює безпосередньо, `Offline/DoNotDisturb` стає в чергу.
- Контроль затримки відновлення сеансу: якщо діалог завершено через NPC і все ще триває затримка відновлення повторного підключення, проактивний запуск відкладається до її завершення (з урахуванням закінчення терміну черги).
- Черга: стандартний ліміт для кожної фракції — `3`, стандартний термін дії — `12` годин.
- LLM: кожне проактивне повідомлення проходить через LLM; після `1` невдалих повторних спроб воно відкидається, а подія записується в журнал.

---

## Інтерфейс проактивного каналу PawnRPG (v0.3.19)

`GameComponent_PawnRpgDialoguePushManager` — це PawnRPG планувальник проактивних діалогів, незалежний від старого проактивного каналу фракцій. Старий канал зберігає попередню поведінку без змін; канал PawnRPG підтримує проактивні діалоги неігрових фракцій із персонажами гравця, а також діалоги персонажа з персонажем усередині фракції гравця.

### Визначення типів

- `PawnRpgTriggerContext`
  - Контекст тригера під час виконання (фракція, тип тригера, категорія, причина, серйозність, метадані).
- `QueuedPawnRpgTrigger`
  - PawnRPG елемент постійної черги відкладених повідомлень (`enqueuedTick/dueTick/expireTick`).
- `PawnRpgNpcPushState`
  - На основі NPC записувати часову прив’язку успішного доставлення (`lastNpcEvaluateTick`).
- `PawnRpgThreatState`
  - Вести стан порогу загрози за фракціями (щоб уникати повторного спаму попереджень про постійні стани на кшталт гнізд комах або ворожості).
- `PawnRpgProtagonistEntry`
  - PawnRPG запис головного персонажа зі списку активних цілей (`Pawn` посилання + `pawnThingId` запасний варіант).

### Точка входу для надсилання звіту про Patch

```csharp
// 交易完成后置
GameComponent_PawnRpgDialoguePushManager.Instance?.RegisterTradeCompletedTrigger(
    faction,
    soldCount,
    boughtCount
);

// 好感大幅变动后置（|delta| >= 10）
GameComponent_PawnRpgDialoguePushManager.Instance?.RegisterGoodwillShiftTrigger(
    faction,
    goodwillDelta,
    reasonTag,
    likelyHostile
);

// UI 帧内鼠标左键采样（忙碌判定）
GameComponent_PawnRpgDialoguePushManager.Instance?.RegisterPlayerLeftClick();
```

### Точка входу налагодження

```csharp
// 强制触发一条 PawnRPG 主动对话（调试按钮调用）
bool ok = GameComponent_PawnRpgDialoguePushManager.Instance?.DebugForcePawnRpgProactiveDialogue() == true;
```

### Інтерфейс списку головних персонажів (v0.5.6)

- `GetRpgProactiveProtagonists()`: отримати список із PawnRPG головних персонажів поточного збереження (повертати лише персонажів, яких можна розібрати).
- `ContainsRpgProactiveProtagonist(Pawn pawn)`: визначити, чи входить персонаж до списку головних персонажів.
- `TryAddRpgProactiveProtagonist(Pawn pawn)`: спробувати додати головного персонажа; у разі досягнення ліміту повернути `false`.
- `RemoveRpgProactiveProtagonist(Pawn pawn)`: вилучити вказаного персонажа зі списку головних персонажів.
- `ClearRpgProactiveProtagonists()`: очистити список головних персонажів.
- `GetRpgProactiveProtagonistCap()` / `SetRpgProactiveProtagonistCap(int)`: отримати/встановити максимальну кількість головних персонажів (за замовчуванням `20`).
- `GetEligibleRpgProactiveTargetsOnMap(Map map)`: отримати кандидатів-персонажів на поточній мапі, які входять до списку та доступні під час виконання.

### Інтерфейс доставлення

- `ChoiceLetter_PawnRpgInitiatedDialogue`
  - `Setup(Pawn npcPawn, Pawn playerPawn, TaggedString labelText, TaggedString bodyText, LetterDef letterDef)`
  - `IsDialogueAlreadyOpen(Pawn playerPawn, Pawn npcPawn)`
  - Варіанти листа містять «Відкрити діалог PawnRPG», що безпосередньо запускає `Dialog_RPGPawnDialogue(playerPawn, npcPawn)`.

### Правила роботи (фіксована стратегія)

- Частота оцінювання: звичайне оцінювання кожні `6000` тактів; обробка черги кожні `600` тактів.
- 6текстNPCтекст：текст NPC текст `150000` ticks текст/текст.
- Глобальне обмеження на 3 дні: після успішної доставки не попереджувального типу `75000` тіків у всій колонії більше не буде успішних доставок PawnRPG активних повідомлень.
- Виняток для попереджень: `WarningThreat` обходить лише 3-денне глобальне обмеження, але не 6-денне обмеження для одного NPC.
- Поріг стосунків: близькі стосунки (чоловік/дружина, наречений/наречена, коханець/коханка) проходять без перевірки, інакше `Opinion >= 35`.
- Поріг низького настрою: лише `Mood <= 0.30` активує умови цього типу.
- Потрійна перевірка зайнятості:`Drafted` / ворожий юніт / `6` протягом секунд клацніть лівою кнопкою миші `>=12`.
- Контроль доступності: NPC ставиться в чергу й очікує, якщо персонаж гравця спить/непритомний/працює.
- Черга: стандартний ліміт для кожної фракції — `3`, стандартний час очікування — `12` годин.
- LLM: після `1` невдалих повторних спроб відкидається; лічильник затримки відновлення оновлюється лише за «успішною доставкою».
- Відкриття листа: коли з активного листа PawnRPG переходять до `Dialog_RPGPawnDialogue`, активне повідомлення вводиться як перша репліка NPC, без повторного запиту вступної репліки.
- Контроль списку головних персонажів: цілі обираються лише зі «списку головних персонажів, заданого вручну»; правила оцінювання зберігають початкову логіку пріоритету близьких стосунків/прихильності.
- Поведінка за порожнього списку: коли список персонажів порожній, PawnRPG активний ланцюжок категорично не запускається (зокрема примусово через налагодження), а до журналу записується зрозуміле повідомлення.

---

## Швидкий початок

### Отримання екземпляра інтерфейсу

```csharp
// 获取单例实例
GameAIInterface aiInterface = GameAIInterface.Instance;
```

### Базовий приклад виклику

```csharp
// 获取派系信息
var result = aiInterface.GetFactionInfo(someFaction);
if (result.Success)
{
    Log.Message(result.Message);
    // 使用 result.Data 获取详细数据
}

// 调整好感度
var adjustResult = aiInterface.AdjustGoodwill(targetFaction, 10, "Diplomatic dialogue");
if (!adjustResult.Success)
{
    Log.Warning($"Failed to adjust goodwill: {adjustResult.Message}");
}
```

---

## Детальний опис методів API

### 1. Керування прихильністю

#### AdjustGoodwill
Змінює прихильність до цільової фракції.

**Параметри:**
| Назва параметра | Тип | Опис |
|--------|------|------|
| faction | Faction | Цільова фракція |
| amount | int | Значення зміни (додатне число збільшує, від’ємне зменшує) |
| reason | string | Причина зміни (для журналу) |

**Повертає:** `APIResult`
- `Success`: чи успішно виконано
- `Message`: опис результату операції
- `Data`: об’єкт, що містить старе/нове значення прихильності

**Обмеження:**
- Максимальна одноразова зміна: 15 за замовчуванням (можна змінити в налаштуваннях)
- Максимальна сумарна зміна за день: 30 за замовчуванням
- Затримка відновлення: 1 година за замовчуванням

**Приклад:**
```csharp
var result = GameAIInterface.Instance.AdjustGoodwill(
    targetFaction, 
    10, 
    "Successful trade negotiation"
);

if (result.Success)
{
    var data = result.Data as dynamic;
    Log.Message($"Goodwill changed from {data.OldGoodwill} to {data.NewGoodwill}");
}
```

---

#### GetCurrentGoodwill
Отримати поточну прихильність до вказаної фракції.

**Параметри:**
| Назва параметра | Тип | Опис |
|--------|------|------|
| faction | Faction | Цільова фракція |

**Дані, що повертаються:**
```csharp
{
    FactionName: string,
    Goodwill: int,
    RelationKind: string,  // "Hostile", "Neutral", "Ally"
    IsHostile: bool,
    IsAlly: bool
}
```

---

### 2. Дипломатичні дії

#### SendGift
Надіслати фракції подарунок, щоб підвищити прихильність.

> Примітка: під час виконання `SendGift` API усе ще зберігається, але стандартний дипломатичний промпт більше не показує цю дію LLM.

**Параметри:**
| Назва параметра | Тип | Опис |
|--------|------|------|
| faction | Faction | Цільова фракція |
| silverAmount | int | Кількість срібла |
| goodwillGain | int | Очікуваний приріст прихильності |

**Обмеження:**
- Максимум срібла: за замовчуванням 1000
- Максимальний приріст прихильності: за замовчуванням 10
- Затримка відновлення: за замовчуванням 1 день

---

#### RequestAid
Запросити фракцію надати допомогу.

**Параметри:**
| Назва параметра | Тип | Опис |
|--------|------|------|
| faction | Faction | Цільова фракція |
| aidType | string | Тип допомоги ("Military", "Medical", "Resources") |

**Обмеження:**
- Запитувати можна лише в союзників
- Мінімальна необхідна прихильність: за замовчуванням 40
- Затримка відновлення: за замовчуванням 1 день (відповідає оригінальній військовій допомозі)
- Після успіху в дипломатичному діалозі автоматично додається фіксована базова вартість `-25`

---

#### DeclareWar
Оголосити війну фракції.

**Параметри:**
| Назва параметра | Тип | Опис |
|--------|------|------|
| faction | Faction | Цільова фракція |
| reason | string | Причина оголошення війни |

**Обмеження:**
- Прихильність має бути нижчою за поріг: за замовчуванням -50
- Затримка відновлення: за замовчуванням 1 день
- Не можна оголошувати війну фракції, яка вже є ворожою

---

#### MakePeace
Укласти мирний договір із фракцією.

**Параметри:**
| Назва параметра | Тип | Опис |
|--------|------|------|
| faction | Фракція | Цільова фракція |
| peaceCost | int | Вартість миру (срібло) |

**Обмеження:**
- Можна укладати мир лише з ворожими фракціями
- Максимальна вартість миру: за замовчуванням 5000
- Час відновлення: за замовчуванням 1 день

---

### 3. Торгівля та каравани

#### RequestTradeCaravan
Запитати фракцію про відправлення торгового каравану.

**Параметри:**
| Назва параметра | Тип | Опис |
|--------|------|------|
| faction | Faction | Цільова фракція |
| requestedGoods | string | Запитуваний тип товару (необов’язково) |

**Обмеження:**
- Не можна надсилати запити ворожим фракціям
- Час відновлення: за замовчуванням 4 дні (відповідає оригінальному запиту каравану)
- Після успішного завершення дипломатичного діалогу автоматично додаються фіксовані базові витрати `-15`

---

#### RequestRaid
Запросити фракцію здійснити напад.

**Параметри:**
| Назва параметра | Тип | Опис |
|--------|------|------|
| faction | Faction | Цільова фракція |
| points | float | Кількість очок нападу (за замовчуванням -1 — автоматичний розрахунок) |
| strategyDefName | string | Стратегія нападу DefName (наприклад, "ImmediateAttack", "Siege") |
| arrivalModeDefName | string | Спосіб прибуття DefName (наприклад, "EdgeWalkIn", "CenterDrop") |
| delayed | bool | Чи виконувати із затримкою (за замовчуванням true) |

**Обмеження:**
- Час відновлення: за замовчуванням 3 дні
- Фракція повинна мати доступний тип `Combat` `pawnGroupMaker` (це також застосовується до HAR/расових фракцій); інакше буде повернуто чітке пояснення невдачі, а планування буде відхилено.
- Коли `points <= 0`, автоматичне визначення очок переходить на базову лінію параметрів `RaidEnemy` з оригінальної гри (`StorytellerUtility.DefaultParmsNow`), замість використання `0.5x DefaultThreatPointsNow`.
- Автоматична кількість очок додатково коригується налаштуваннями:
  - Глобальні: `RaidPointsMultiplier`, `MinRaidPoints`
  - Перекриття Def за фракціями: `RaidPointsFactionOverrides`（`FactionDefName + RaidPointsMultiplier + MinRaidPoints`）
- Час затримки:
  - EdgeWalkIn/Siege: 6–8 годин
  - DropPods: 1–2 години

---

#### CreateQuest
Створити завдання за допомогою шаблону завдання оригінальної гри та опублікувати його для гравця.

**Параметри:**
| Назва параметра | Тип | Опис |
|--------|------|------|
| questDefName | string | **Обов’язково**. DefName шаблону завдання оригінальної гри. Потрібно вибрати зі списку Available, динамічно переданого в поточний промпт. |
| askerFaction | string | Необов’язково. Назва фракції, яка ініціює завдання. Типово — поточна фракція. |
| points | int | Необов’язково. Кількість очок загрози завдання. Якщо не вказано, система автоматично обчислить її на основі поточної сили гравця. |

---

### 4. Публічне оголошення в соціальному колі（v0.3.14）

#### publish_public_post（протокол дії AI）
Перетворює поточний дипломатичний вміст на публічне оголошення, «видиме всій фракції», і додає його до стрічки соціального кола. Використовуйте обережно; не застосовуйте для звичайного спілкування чи приватних переговорів.

**Параметри (рекомендовано передавати через об’єкт `parameters`):**
| Назва параметра | Тип | Опис |
|--------|------|------|
| category | string | Категорія оголошення: `Military/Economic/Diplomatic/Anomaly` |
| sentiment | int | Напрямок настрою, діапазон `-2..2` |
| summary | string | Текст оголошення (необов’язково; якщо не вказано, використовується шаблон правил) |
| targetFaction | string | Назва або defName згаданої фракції (необов’язково) |
| intentHint | string | Підказка щодо наміру дії (необов’язково) |

#### GameComponent_DiplomacyManager.EnqueuePublicPost
Записати публічну заяву до черги запитів новин соціального кола:
- Створити `SocialNewsSeed` із джерелом діалогу та подати його до LLM суворим ланцюжком новин JSON.
- Новини з джерелом діалогу можуть спричиняти незначні дипломатичні наслідки (зміна прихильності + оцінка соціального наміру), але більше не запускають додаткові світові incident.
- Картка новини зберігається як структуровані поля (заголовок/вступ/причина/перебіг/оцінка/цитата), а не як текст за старим фіксованим шаблоном.

#### GameComponent_DiplomacyManager.ForceGeneratePublicPost
Точка входу для налагодження: негайно просканувати та подати наступний доступний запит на фактичну новину й перенести час наступного автоматичного сканування.

#### GameComponent_DiplomacyManager.GetSocialPosts / GetUnreadSocialPostCount / MarkSocialPostsRead
Надає інтерфейс feed і стану непрочитаних повідомлень, потрібні для UI соціального кола.

**Список рекомендованих шаблонів завдань:**
- `ThreatReward_Raid_MiscReward`: Відбити напад і отримати нагороду
- `Mission_BanditCamp`: Знищити ворожий табір
- `OpportunitySite_PeaceTalks`: Мирні переговори
- `TradeRequest`: Доставити конкретні припаси
- `Hospitality_Refugee`: Прийняти біженців
- `PawnLend`: Орендувати колоністів
- `AncientComplex_Mission`: Дослідити стародавні руїни
- `SurveySite`: Провести польове дослідження

**Приклад:**
```json
{
  "action": "create_quest",
  "parameters": {
    "questDefName": "Mission_BanditCamp",
    "points": 1000
  }
}
```

**Обмеження:**
- Необхідно надати `questDefName`. Спеціальні завдання без шаблону більше не підтримуються.
- Система автоматично доповнить `map` та інші параметри середовища.

---

### 4. Запит стану

#### GetFactionInfo
Отримати детальну інформацію про фракцію.

**Дані, що повертаються:**
```csharp
{
    Name: string,
    DefName: string,
    Goodwill: int,
    RelationKind: string,
    IsPlayer: bool,
    IsDefeated: bool,
    IsHidden: bool,
    LeaderName: string,
    SettlementCount: int,
    TodayAdjustment: int  // 今日已调整的好感度
}
```

---

#### GetAllFactions
Отримати список усіх доступних фракцій.

**Дані, що повертаються:**
```csharp
List<{
    Name: string,
    Goodwill: int,
    RelationKind: string,
    IsAIControlled: bool
}>
```

---

#### GetColonyStatus
Отримати поточний стан колонії.

**Дані, що повертаються:**
```csharp
{
    ColonyName: string,
    MapCount: int,
    TotalColonists: int,
    TotalWealth: float,
    GameDate: string,
    ThreatLevel: float
}
```

---

## Механізми безпеки

### Система затримки відновлення

Кожен метод API має окрему затримку відновлення, щоб запобігти надмірному викликанню AI:

| Метод | Затримка відновлення за замовчуванням | Діапазон налаштування |
|------|----------|------------|
| AdjustGoodwill | 0 годин (без затримки відновлення) | 0–24 години |
| SendGift | 1 день | 0.5-5 днів |
| RequestAid | 1 день | 1–7 днів |
| DeclareWar | 1 день | 1–7 днів |
| MakePeace | 1 день | 1–7 днів |
| RequestTradeCaravan | 4 дні | 0.5-5 днів |

**Перевірити залишок затримки відновлення:**
```csharp
int remainingSeconds = GameAIInterface.Instance.GetRemainingCooldownSeconds("AdjustGoodwill");
```

### Обмеження коригування прихильності

1. **Максимум за одне коригування**: за замовчуванням 15 пунктів (діапазон: 0–50)
2. **Щоденний сумарний ліміт**: за замовчуванням 30 очок (діапазон: 0–100)
3. **Автоматичне обрізання**: запити, що перевищують обмеження, буде автоматично обрізано до дозволеного діапазону

### Перевірка дозволів

```csharp
// 验证 AI 是否有权限操作指定派系
bool hasPermission = GameAIInterface.Instance.ValidateAIPermission(targetFaction);
```

---

## Параметри конфігурації

Усі обмеження можна змінити в налаштуваннях моду:

### Налаштування прихильності
- `MaxGoodwillAdjustmentPerCall`: ліміт зміни за один раз (1–50)
- `MaxDailyGoodwillAdjustment`: щоденний сумарний ліміт (10–100)
- `GoodwillCooldownTicks`: затримка відновлення (0.5-24 годин)

### Налаштування подарунків
- `MaxGiftSilverAmount`: максимальна кількість срібла (100–5000)
- `MaxGiftGoodwillGain`: максимальний приріст прихильності (1–25)
- `GiftCooldownTicks`: затримка відновлення (0.5-5 днів)

### Війна та мир
- `MaxGoodwillForWarDeclaration`: максимальна прихильність для оголошення війни (-100–0)
- `MaxPeaceCost`: Максимальна ціна миру (0–10000)
- `PeaceGoodwillReset`: Значення прихильності після укладення миру (-100–0)

### Регулювання очок рейду
- `RaidPointsMultiplier`: Глобальний множник очок рейду (0.1-5.0)
- `MinRaidPoints`: Глобальна мінімальна кількість очок рейду (0–1000)
- `RaidPointsFactionOverrides`: Перевизначення для фракції DefName (кожен елемент містить `FactionDefName`, `RaidPointsMultiplier`, `MinRaidPoints`)

---

## Обробка помилок

Усі методи API повертають об’єкти `APIResult`:

```csharp
public class APIResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
}
```

**Поширені повідомлення про помилки:**
- `"Settings not initialized"` — налаштування не ініціалізовано
- `"Faction cannot be null"` — параметри фракції порожні
- `"Method X is on cooldown"` — метод перебуває на затримці відновлення
- `"Daily goodwill adjustment limit reached"` — перевищено щоденний ліміт коригувань
- `"Can only request aid from allied factions"` — вимоги до відносин не виконано

---

## Налагодження та журналювання

### Увімкнення журналу викликів API

У вкладці «AI» налаштувань моду Control увімкніть «`Enable API Call Logging`».

### Отримання історії викликів

```csharp
// 获取最近的 50 条调用记录
var history = GameAIInterface.Instance.GetAPICallHistory(maxRecords: 50);

// 获取特定方法的调用记录
var goodwillHistory = GameAIInterface.Instance.GetAPICallHistory("AdjustGoodwill");

foreach (var record in history)
{
    Log.Message($"[{record.TickCalled}] {record.MethodName}: {record.Parameters} - {(record.Success ? "Success" : "Failed")}");
}
```

---

## Найкращі практики

### 1. Перевірка затримки відновлення

Перед викликом методу, для якого може діяти затримка відновлення, спочатку перевірте час, що залишився:

```csharp
int cooldown = GameAIInterface.Instance.GetRemainingCooldownSeconds("AdjustGoodwill");
if (cooldown > 0)
{
    Log.Message($"Please wait {cooldown} seconds before adjusting goodwill again");
    return;
}
```

### 2. Перевірка дозволів

Перед виконанням чутливих операцій перевірте дозволи AI:

```csharp
if (!GameAIInterface.Instance.ValidateAIPermission(faction))
{
    Log.Warning("AI does not have permission to interact with this faction");
    return;
}
```

### 3. Обробка невдалих результатів

Завжди перевіряйте результати викликів API:

```csharp
var result = GameAIInterface.Instance.AdjustGoodwill(faction, amount, reason);
if (!result.Success)
{
    // 根据错误类型采取不同措施
    if (result.Message.Contains("cooldown"))
    {
        // 等待冷却结束
    }
    else if (result.Message.Contains("limit"))
    {
        // 调整策略
    }
}
```

---

## Приклад інтеграції з LLM

Нижче наведено повний приклад інтеграції з LLM API:

```csharp
public class AIDiplomacyService
{
    private GameAIInterface _interface;
    
    public AIDiplomacyService()
    {
        _interface = GameAIInterface.Instance;
    }
    
    public async Task ProcessDialogue(Faction faction, string playerMessage)
    {
        // 1. 获取当前状态
        var statusResult = _interface.GetColonyStatus();
        var factionResult = _interface.GetFactionInfo(faction);
        
        // 2. 构建 LLM 提示
        var prompt = BuildPrompt(faction, playerMessage, statusResult.Data, factionResult.Data);
        
        // 3. 调用 LLM
        var llmResponse = await CallLLM(prompt);
        
        // 4. 解析 LLM 的 API 调用意图
        var intendedAction = ParseAction(llmResponse);
        
        // 5. 执行游戏 API 调用
        switch (intendedAction.Type)
        {
            case "adjust_goodwill":
                var result = _interface.AdjustGoodwill(
                    faction, 
                    intendedAction.Amount, 
                    intendedAction.Reason
                );
                
                if (!result.Success)
                {
                    // 通知 LLM 调用失败，请求调整策略
                    await NotifyFailure(llmResponse, result.Message);
                }
                break;
                
            case "declare_war":
                _interface.DeclareWar(faction, intendedAction.Reason);
                break;
                
            // ... 其他操作
        }
    }
}
```

---

## Посібник з інтеграції LLM

### Огляд

RimChat Підтримка LLM (велика мовна модель) за допомогою JSON форматованої відповіді викликає ігрові API . Це дає змогу AI динамічно змінювати стан гри відповідно до змісту діалогу, реалізуючи розумну дипломатичну взаємодію.

### Системний промпт

Коли гравець спілкується з фракцією AI, LLM отримує системний промпт, що містить наведену нижче інформацію. Системний промпт **динамічно містить поточні параметри налаштувань моду**, щоб LLM знала про актуальні обмеження API.

```
=== FACTION INFO ===
Name: {派系名称}
Type: {派系类型}
Current Goodwill: {好感度}
Relation: {关系状态}
Leader: {领袖名称}
Leader Traits: {特质列表}
Ideology: {意识形态}

=== AVAILABLE ACTIONS ===
You can perform diplomatic actions by including a JSON block in your response.

=== CURRENT API LIMITS (MUST FOLLOW) ===
- Max goodwill adjustment per call: {当前设置值} (range: 0 to {当前设置值})
- Max daily goodwill adjustment: {当前设置值}
- Goodwill cooldown: {当前设置值} hours
- Min goodwill for aid: {当前设置值}
- Max goodwill for war declaration: {当前设置值}
- Max peace cost: {当前设置值}
- Peace goodwill reset: {当前设置值}

ENABLED FEATURES:
- Goodwill adjustment: {YES/NO}
- War declaration: {YES/NO}
- Peace making: {YES/NO}
- Trade caravan: {YES/NO}
- Aid request: {YES/NO}

ACTIONS:
1. adjust_goodwill - Change faction relations
   Parameters: amount (int, -{当前单次上限} to {当前单次上限}), reason (string)
   Daily limit remaining: {当前每日上限} total per day
2. request_aid - Request military/medical aid (requires ally)
   Parameters: type (string: Military/Medical/Resources)
   Requirement: goodwill >= {当前最低要求}
3. declare_war - Declare war
   Parameters: reason (string)
   Requirement: goodwill <= {当前宣战阈值}
4. make_peace - Offer peace treaty (requires war)
   Parameters: cost (int, max {当前最大代价} silver)
   Result: goodwill reset to {当前重置值}
6. request_caravan - Request trade caravan
   Parameters: goods (string, optional)
   Requirement: not hostile
7. reject_request - Reject player's request
   Parameters: reason (string)

DECISION GUIDELINES:
- Current goodwill {value}: {行为建议}
- Consider your leader's traits and ideology when making decisions
- You can accept or reject player requests based on current relations
- Small goodwill changes (1-{当前单次上限/3}) for minor interactions
- Medium changes ({当前单次上限/3}-{当前单次上限*2/3}) for moderate events
- Large changes ({当前单次上限*2/3}-{当前单次上限}) for significant diplomatic events

RESPONSE FORMAT:
Respond with your in-character dialogue first. If gameplay effects are needed, append one raw JSON object using the `actions` array contract:

```json
{
  "actions": [
    {
      "action": "snake_case_action",
      "parameters": {
        "param1": "value"
      }
    }
  ]
}
```

IMPORTANT RULES:
1. NEVER exceed the max values shown above
2. ONLY use enabled features
3. ALWAYS check requirements before using an action
4. If an action is unavailable, refuse through an in-world reason instead of exposing system state

If no action is needed, respond normally without JSON.
```

**Примітка**: `{当前设置值}` у системному промпті динамічно змінюється відповідно до конфігурації гравця в налаштуваннях моду. Це означає:
- Якщо гравець встановив максимальне значення зміни прихильності на 0, LLM знатиме, що не може змінювати прихильність
- Якщо гравець вимкнув певну функцію, LLM знатиме, що не може нею користуватися
- LLM завжди знає про актуальні обмеження API, що гарантує невихід за встановлені межі

### Формат відповіді JSON

LLM Можна викликати ігровий JSON надавши у єдиному об’єкті верхнього рівня `actions` масив.API**ЄДИНИЙ дійсний протокол дій** — `visible_dialogue` + необов’язковий `actions`.

```json
{
  "actions": [
    {
      "action": "adjust_goodwill",
      "parameters": {
        "amount": 10,
        "reason": "Successful trade negotiation"
      }
    }
  ],
  "strategy_suggestions": [
    {
      "strategy_name": "以势压人",
      "reason": "[F1] 财富压制，先用威慑抢占主导",
      "content": "你若继续拖延，我们会把谈判变成最后通牒。"
    },
    {
      "strategy_name": "缓和周旋",
      "reason": "[F2] 社交较高，先争取缓和与让步空间",
      "content": "我们愿意先降一阶条件，只要你给出可验证承诺。"
    },
    {
      "strategy_name": "极端威慑",
      "reason": "[F3] 激进特质下需快速施压迫使表态",
      "content": "再无结果，我们将按敌对预案执行，不再二次警告。"
    }
  ]
}
```

#### Опис полів

| Поле | Тип | Обов’язкове | Опис |
|------|------|------|------|
| actions | array | Ні | Масив дій; це поле необхідно використовувати, якщо дії мають ігровий ефект |
| actions[].action | string | Так (якщо наявне actions) | Тип дії, яку потрібно виконати |
| actions[].parameters | object | Ні | Параметри дії, що залежать від типу action |
| strategy_suggestions | array | Ні | Дані кнопок стратегій, які можна повернути в сценаріях зниження прихильності; має містити рівно 3 елементи |

#### Опис підполів `strategy_suggestions`

| Вкладене поле | Тип | Обов’язкове | Опис |
|--------|------|------|------|
| strategy_name | string | Так | Короткий заголовок кнопки (рекомендовано <= 6 китайських символів) |
| reason | string | Так | Підстава для спрацьовування, бажано містити фактологічний тег (наприклад, `[F1]`) |
| content | string | Так | Повна чернетка відповіді (надсилається після натискання кнопки) |

**Обмеження виводу:**
- Якщо стратегічна здібність доступна, спочатку виведіть `strategy_suggestions` (доступність у межах сеансу визначається рівнем соціальних навичок і кількістю використань, що залишилася).
- Якщо це поле виведено, потрібно строго повернути 3 елементи; інакше клієнт відкине все поле.
- Якщо клієнт виявить чисте зниження прихильності за відсутності або некоректності поля, він один раз надішле додатковий запит лише з `strategy_suggestions`; це не впливає на звичайний текст діалогу та виконання дій у цьому раунді.
- Якщо додатковий запит поверне природну мову замість JSON, клієнт спробує витягти з тексту опису 3 стратегічні фрази та заповнити ними кнопки (резервна логіка).
- Принаймні 2 пропозиції мають чітко ґрунтуватися на характеристиках/контексті гравця (соціальні навички, риси, багатство колонії, тон нещодавньої взаємодії).
- Сторона UI під час `EnableDiplomacyStrategyToggle=false` згортає область стану стратегії до мінімалістичного входу й надалі блокує додаткові стратегічні запити та показ/автоматичне надсилання кнопок; це згортання змінює лише макет дипломатичного вікна, але не поля протоколу.
- Старий протокол з одним об’єктом заборонено: `{"action":"...","parameters":{...},"response":"..."}`.

#### Допустимі типи дій

| Дія | Опис | Обов’язкові параметри | Необов’язкові параметри |
|------|------|----------|----------|
| adjust_goodwill | Змінити прихильність | amount (int) | reason (string) |
| request_aid | Запросити допомогу | - | type (string), apply_goodwill_cost (bool, default=false) |
| declare_war | Оголосити війну | - | reason (string) |
| make_peace | Укласти мир | - | cost (int) |
| request_caravan | Запросити торговий караван | - | goods (string), apply_goodwill_cost (bool, default=false) |
| request_raid | Атакувати колонію гравця (рейд) | strategy (string) | arrival (string) |
| create_quest | Створити завдання з нативного шаблону | questDefName (string) | points (int), askerFaction (string) |
| reject_request | Офіційно відхилити чіткий запит | - | reason (string) |
| none | Без дії | - | - |

### Посібник із прийняття рішень

#### Рекомендації щодо поведінки на основі прихильності

| Діапазон прихильності | Відносини | Рекомендації щодо поведінки |
|------------|------|----------|
| 80-100 | Союзники | Дуже дружні, ймовірно приймуть більшість запитів |
| 40-79 | Друзі | Дружні, відкриті до торгівлі та співпраці |
| 0-39 | Нейтральні | Обережні, але готові вести переговори |
| -39-(-1) | Ворожі | Навряд чи співпрацюватимуть, можуть погрожувати |
| -100-(-40) | Вороги | Ворожі, можуть оголосити війну |

#### Логіка прийняття/відхилення

LLM має вирішувати, приймати чи відхиляти запити гравця, на основі таких чинників:

1. **Поточна прихильність**: вища прихильність підвищує ймовірність прийняття запиту
2. **Риси лідера**: впливають на стиль прийняття рішень (наприклад, «розумний» лідер обережніший)
3. **Ідеологія**: впливає на дипломатичні схильності
4. **Стан відносин**:
   - Союзники можуть просити про допомогу
   - Вороги не можуть просити торговий караван
   - Мирні переговори можливі лише під час війни
   - Оголошувати війну можна лише за прихильності нижче -50

### Приклад використання

#### Приклад 1: Гравець просить торговий караван

**Гравець**: «Чи можете ви надіслати торговий караван до нашої колонії?»

**Відповідь LLM** (дружня фракція):
```json
{
  "action": "request_caravan",
  "parameters": {
    "goods": "general"
  },
  "response": "Of course! Our traders would be delighted to visit your colony. Expect them within a few days."
}
```

**LLM Відповідь** (ворожа фракція):
Я не можу на це погодитися. Ваша колонія завдала нам чимало клопоту. Спершу покращте наші відносини, а тоді ми зможемо поговорити про торгівлю.
```json
{"actions":[{"action":"reject_request","parameters":{"reason":"Our relations are too strained for trade at this time."}}]}
```

#### Приклад 2: гравець просить допомоги

**Гравець**: «На нас напали! Чи можете ви надіслати військову допомогу?»

**LLM Відповідь** (союзники, висока прихильність):
Як союзники, ми не покинемо вас у скрутну хвилину. Підкріплення вже готується й вирушить, щойно це стане можливим.
```json
{"actions":[{"action":"request_aid","parameters":{"type":"Military"}}]}
```

**LLM Відповідь** (нейтральна фракція):
Я співчуваю вашому становищу, але нас іще не пов’язує союз. Зміцніть наші зв’язки — можливо, тоді ми зможемо обговорити взаємну оборону.
```json
{"actions":[{"action":"reject_request","parameters":{"reason":"We are not yet close enough allies for such assistance."}}]}
```

#### Приклад 3: покращення прихильності

**Гравець**: «Дякую за ваш щедрий подарунок. Ми цінуємо нашу дружбу.»

**LLM Відповідь**:
Ваші слова зігрівають моє серце. Я радий бачити, як наша дружба міцнішає з кожним днем.
```json
{"actions":[{"action":"adjust_goodwill","parameters":{"amount":8,"reason":"Player expressed gratitude for gift"}}]}
```

#### Приклад 4: лише діалог (без дій)

**Гравець**: «Розкажіть мені про історію вашої фракції.»

**LLM Відповідь** (лише текст, без JSON):
«Наш народ поколіннями мандрував цими землями, укладаючи союзи й долаючи труднощі. Ми цінуємо силу, мудрість і понад усе — вірність друзям.»

### Обробка помилок

Якщо формат LLM , повернутий JSON, недійсний або виконання дії не вдалося:

1. **Помилка розбору**: система відобразить усю відповідь як звичайний текст
2. **Помилка виконання дії**: система запише журнал помилок і покаже в діалозі причину невдачі
3. **Затримка відновлення**: якщо дія перебуває на періоді затримки відновлення, буде показано час, що залишився
4. **Недостатньо прав**: якщо AI не має права взаємодіяти з цією фракцією, виконання буде відхилено

### Найкращі практики

#### Рекомендації для розробників LLM

1. **Поступове коригування прихильності**:
   - Мала взаємодія: ±5
   - Середня взаємодія: ±10
   - Важлива подія: ±15

2. **Обґрунтоване прийняття/відхилення**:
   - Не приймайте всі запити безумовно
   - Враховуйте поточні відносини та особливості фракції
   - Наводьте обґрунтовані причини для відмови

3. **Рольова гра**:
   - Дотримуйтеся послідовності ролі
   - Враховуйте риси лідера (наприклад, запальний лідер охочіше оголошує війну)
   - Відображайте відмінності в ідеологіях

4. **Формат JSON**:
   - Переконайтеся, що формат JSON правильний
   - Типи параметрів мають відповідати одне одному (ціле число проти рядка)
   - Якщо не впевнені, надавайте перевагу відповіді у вигляді звичайного тексту

---

## Журнал змін

### v0.3.6
- Додано дії `exit_dialogue`, `go_offline`, `set_dnd` AI для керування присутністю фракцій.
- Додано систему станів присутності фракцій (`Online/Offline/DoNotDisturb`) із кешем для кожної фракції та підтримкою примусового переходу в офлайн.
- Додано обмеження введення діалогів: офлайн-фракції/DND доступні лише для читання й не можуть надсилати повідомлення.
- Додано процес повторного ініціювання після `exit_dialogue`.

### v0.9.11
- Додати підтримку відповіді LLM JSON
- Реалізувати аналізатор дії AI
- Додати виконавець дії AI
- Розширити системний промпт, додавши інструкції щодо виклику API
- Реалізувати логіку прийняття/відхилення

### v0.9.10
- Випустити початкову версію
- Реалізувати основний метод API
- Додати обмеження безпеки та механізм затримки відновлення
- Додати підтримку конфігурації параметрів моду

---

## Дії онлайн-стану (v0.3.6)

### Action: exit_dialogue
Завершує поточний раунд діалогу, не змінюючи онлайн-стан.

**Приклад JSON:**
```json
{
  "action": "exit_dialogue",
  "parameters": {
    "reason": "I need to review your proposal first."
  }
}
```

**Ефект:**
- Поточне вікно переходить у режим лише для читання.
- Гравець може натиснути «Розпочати діалог знову», щоб продовжити сеанс із тією самою фракцією (якщо ця фракція наразі онлайн).

### Action: go_offline
Завершити поточний діалог і перейти в офлайн-стан.

**JSON приклад:**
```json
{
  "action": "go_offline",
  "parameters": {
    "reason": "Communications terminal shutting down."
  }
}
```

**Ефект:**
- Поточне вікно доступне лише для читання.
- Онлайн-стан змінюється на `Offline`.
- Повідомлення не можна надсилати протягом «примусової тривалості офлайн-режиму».

### Action: set_dnd
Перейти в режим «Не турбувати» та припинити обмін повідомленнями.

**JSON приклад:**
```json
{
  "action": "set_dnd",
  "parameters": {
    "reason": "We are in emergency council."
  }
}
```

**Ефект:**
- Поточне вікно доступне лише для читання.
- Онлайн-стан змінюється на `DoNotDisturb`.
- Надсилання повідомлень заборонено.

### Пов’язані налаштування
- `EnableFactionPresenceStatus`
- `PresenceCacheHours` (за замовчуванням 8 годин)
- `PresenceForcedOfflineHours` (за замовчуванням 24 години)
- `PresenceNightBiasEnabled`
- `PresenceNightStartHour` / `PresenceNightEndHour`
- `PresenceNightOfflineBias`
- `PresenceUseAdvancedProfiles` та онлайн-шаблони кожного TechLevel (початкова година/тривалість онлайн-сеансу)

## Примусове оновлення тривалості онлайн-статусу (v0.9.88)
- `GameComponent_DiplomacyManager.GetPresenceForcedOfflineTicks()`
  - Тепер завжди повертає `2 * GenDate.TicksPerHour`.
- `GameComponent_DiplomacyManager.GetPresenceDoNotDisturbTicks()`
  - Тепер завжди повертає `4 * GenDate.TicksPerHour`.
- `GameComponent_DiplomacyManager.RefreshPresenceOnDialogueOpen(Faction faction)`
  - Після завершення примусового офлайн-режиму або режиму «Не турбувати» негайно відновлюється `Online`, а також очищуються `forcedOfflineUntilTick` / `doNotDisturbUntilTick`.
  - Після завершення більше не виконується повторний розрахунок розкладу, щоб уникнути блокування, коли «після завершення все ще неможливо говорити».
- `GameComponent_DiplomacyManager.EnforcePresenceForcedDurationCaps(...)`
  - Додано логіку обрізання довгих таймерів у старих збереженнях: під час оновлення залишковий час примусового стану обмежується значенням «поточний tick + тривалість за новими правилами».
- `PresenceForcedOfflineHours` (параметр налаштувань)
  - Поле збереження та повзунок UI зберігаються, але більше не керують станом примусової тривалості `go_offline/set_dnd`.

## Інтерфейс промпту персони Pawn RPG

### `GameComponent_RPGManager.GetPawnPersonaPrompt(Pawn pawn)`
- Призначення: зчитує незалежний промпт особистості вказаного персонажа.
- Повертає: якщо не налаштовано, повертає порожній рядок.

### `GameComponent_RPGManager.SetPawnPersonaPrompt(Pawn pawn, string prompt)`
- Призначення: записати або очистити незалежний промпт особистості вказаного персонажа.
- Поведінка: якщо `prompt` порожній або містить лише пробіли, конфігурацію цього персонажа буде видалено.

### Поведінка ін’єкції промпту RPG
- Точка складання: `PromptPersistenceService.BuildRPGFullSystemPrompt(Pawn initiator, Pawn target)`.
- Місце ін’єкції: після `ROLE SETTING` і перед `DIALOGUE STYLE`.
- Умова ін’єкції: цільовий персонаж має непорожній незалежний промпт особистості.

### Профілі особистості NPC під час першого завантаження старого збереження (v0.3.109)
- Компонент: `GameComponent_RPGManager`
  - Поля збереження: `npcPersonaBootstrapCompleted`, `npcPersonaBootstrapVersion` (одноразові позначки виконання для версії проведення).
  - Точка запуску: `GameComponentTick()` -> асинхронна обробка черги профілів особистості.
  - Цільова сукупність: наявні гуманоїдні персонажі (персонажі, згенеровані на мапі + видимі лідери фракцій).
  - Інтерфейс запису: повторно використовується `SetPawnPersonaPrompt(Pawn pawn, string prompt)`.
- Побудова контексту:
  - `PromptPersistenceService.BuildPawnPersonaBootstrapProfile(Pawn pawn)`
  - Використовуйте стислий профіль, призначений для особистості (передісторія/риси/ключові навички/роль у фракції/ідеологія).
  - Явно виключайте здоров’я/потреби/настрій/травми й хвороби/спорядження/гени/тимчасові події та інші сигнали, не пов’язані з особистістю.
- Протокол генерації:
  - Фіксований шаблон виводу:
    - `He/She is a [core temperament] person who tends to [emotional pattern], usually handles situations by [behavioral strategy], because deep down they seek [core motivation], but this also makes them [defense/weakness], often leading to [personality cost].`
  - Приклад:
    - `He is a calm and analytical person who rarely shows his emotions and tends to approach problems through careful observation and planning, because deep down he seeks control and security, but this also makes him distant and slow to trust others.`
  - Обмеження довжини виводу: один рядок, фокус на особистості, короткі фрази; під час виконання займенник узгоджується зі статтю персонажа (`He/She/They`).
  - Недійсний вивід буде повторно оброблено; якщо повторна спроба не вдасться, буде записано шаблонний резервний текст, щоб гарантувати доступність поля.
- RimTalk автоматичне копіювання особистості (v0.5.10):
  - Перед генеруванням особистості в AI спочатку спробуйте відрендерити шаблон RimTalk, щоб скопіювати особистість.
  - Фільтрація цілей: лише людські персонажі колонії (`pawn.Faction == Faction.OfPlayer`).
  - Стратегія заповнення: лише заповнювати порожні значення, не перезаписувати наявні непорожні значення `GetPawnPersonaPrompt`.
  - Джерело шаблону: `RimChatSettings.RimTalkPersonaCopyTemplate` (strict Scriban, підтримується лише синтаксис `{{ pawn.personality }}`).
  - Помилка рендерингу/порожній результат: негайно викинути структурований виняток і перервати цей ланцюжок (без silent fallback).
  - На сторінці налаштувань можна переглянути результат останньої міграції шаблонів (список успішних/невдалих + причини блокування).
  - Повна синхронізація вручну: `GameComponent_RPGManager.TrySyncAllColonyPawnPersonasFromRimTalk(out int updated, out int cleared, out int unchanged, out int skipped)`, використовується для синхронізації особистості RimTalk персонажів колонії з RimChat в один клік на сторінці налаштувань (зі статистикою оновлень/очищень/пропусків).

## Інтерфейс системи промптів середовища (v0.3.25)

### Нова структура конфігурації
- `SystemPromptConfig.EnvironmentPrompt`
  - `Worldview.Enabled` / `Worldview.Content`
  - `SceneSystem.Enabled` / `MaxSceneChars` / `MaxTotalChars` / `PresetTagsEnabled`
  - `SceneEntries[]`
    - `Id`, `Name`, `Enabled`, `ApplyToDiplomacy`, `ApplyToRPG`, `Priority`, `MatchTags[]`, `Content`
  - `RpgSceneParamSwitches`
    - `IncludeSkills`, `IncludeEquipment`, `IncludeGenes`, `IncludeNeeds`, `IncludeHediffs`, `IncludeRecentEvents`, `IncludeColonyInventorySummary`, `IncludeHomeAlerts`, `IncludeRecentJobState`, `IncludeAttributeLevels`
  - `EventIntelPrompt`
    - `Enabled`, `ApplyToDiplomacy`, `ApplyToRpg`
    - `IncludeMapEvents`, `IncludeRaidBattleReports`
    - `DaysWindow`, `MaxStoredRecords`, `MaxInjectedItems`, `MaxInjectedChars`

### Нові типи контексту
- `DialogueScenarioContext`
  - `CreateDiplomacy(Faction faction, bool isProactive, IEnumerable<string> additionalTags = null)`
  - `CreateRpg(Pawn initiator, Pawn target, bool isProactive, IEnumerable<string> additionalTags = null)`

### Розширення точки входу складання промпту
- Дипломатичний канал:
  - `BuildFullSystemPrompt(Faction faction, SystemPromptConfig config, bool isProactive, IEnumerable<string> additionalSceneTags)`
- Канал RPG:
- `BuildRPGFullSystemPrompt(Pawn initiator, Pawn target, bool isProactive, IEnumerable<string> additionalSceneTags)`
### Змінні шаблонів сцен (v0.3.34)

Елементи сцен середовища `SceneEntries[].Content` підтримують лише синтаксис змінних простору імен `{{ namespace.path }}`; під час виконання використовується Scriban strict.<br>
Поточні вбудовані змінні:

- `{{ world.scene_tags }}`
- `{{ world.environment_params }}`
- `{{ world.recent_world_events }}`
- `{{ world.colony_status }}`
- `{{ world.colony_factions }}`
- `{{ world.current_faction_profile }}`
- `{{ pawn.target.profile }}`
- `{{ pawn.initiator.profile }}`

Примітки:
- Нерозпізнані змінні, помилки розбору та доступ до порожніх об’єктів спричиняють `PromptRenderException`; оригінальний текст не передається без змін.
- Сторінка налаштувань промпту надає діагностику компіляції Scriban у реальному часі (код помилки + рядок і стовпець) і кнопку ручної перевірки.

### Правила ін’єкції на рівні середовища
- Порядок ін’єкції: `Worldview -> Environment Parameters -> Recent World Events & Battle Intel -> Scene Layers -> Existing Prompt Stack`.
- Правило зіставлення: інʼєкція відбувається лише за повного збігу `SceneEntries.MatchTags` (ALL).
- Стратегія збігів: усі збіглі записи додаються в порядку спадання `Priority`.
- Контроль довжини: спочатку обрізати окремий запис за `MaxSceneChars`, потім обрізати загальний обсяг за `MaxTotalChars`.
- Контроль памʼяті подій: подвійне обмеження частоти для `MaxInjectedItems` і `MaxInjectedChars`, фільтрація за відомими межами для кожної фракції.
- Блок обмежень фактів: завжди додавати `FACT GROUNDING RULES`, вимагаючи відповідати лише на основі відомої інформації; твердження без підстав слід чітко позначати як невизначені та ставити під сумнів.










## Мова виведення промпту API (v0.3.44)
- Налаштування: RimChatSettings.PromptLanguageFollowSystem, RimChatSettings.PromptLanguageOverride.
- Розвʼязувач під час виконання: RimChatSettings.GetEffectivePromptLanguage().
- Інʼєкція промпту: ієрархічні конструктори дипломатії/RPG додають вузол настанов output_language.
- Контракт: мовні настанови застосовуються лише до відповіді природною мовою; ключі JSON/дія IDs залишаються без змін.

## Сумісність RimTalk API (v0.4.11 в архіві, основна гілка зі строгим виконанням)
- Settings:
  - `RimChatSettings.EnableRimTalkPromptCompat` (типове значення `true`)
  - `RimChatSettings.RimTalkSummaryHistoryLimit` (типове значення `10`, обмежено до `1..30`)
  - `RimChatSettings.RimTalkPresetInjectionMaxEntries` (типове значення `0`, обмежено до `0..200`, `0 = unlimited`)
  - `RimChatSettings.RimTalkPresetInjectionMaxChars` (типове значення `0`, обмежено до `0..200000`, `0 = unlimited`)
  - `RimChatSettings.RimTalkCompatTemplate` (шаблон Scriban, який використовують промпти дипломатії та RPG)
  - `RimChatSettings.RimTalkPersonaCopyTemplate` (типовий `{{ pawn.personality }}`, використовується для автоматичного копіювання персонажа RPG)
- Примітка щодо виконання:
  - Активний конвеєр рендерингу промптів уніфіковано до внутрішнього суворого Scriban (`PromptTemplateRenderer.RenderOrThrow(...)`).
  - Файли `RimChat.Compat.RimTalkCompatBridge*` видалено з поточної кодової бази (лише в історичній документації).
  - Браузер змінних налаштувань використовує локальний простір імен знімка `PromptVariableCatalog`.
- Ключі надсилання підсумків (RimTalk глобальне сховище змінних):
  - `rimchat_last_session_summary`
  - `rimchat_last_diplomacy_summary`
  - `rimchat_last_rpg_summary`
  - `rimchat_recent_session_summaries`
- Інтеграція конвеєра промптів:
  - Дипломатія: блок сумісності додано в кінці `instruction_stack`.
  - RPG: блок сумісності додано в кінці `role_stack`, а також активний блок рендерингу запису модуля попереднього набору RimTalk (`rimtalk_preset_mod_entries`).
  - Обмеження ін’єкції записів модуля активного попереднього набору тепер задаються налаштуваннями (записи/символи), а типове значення — без обмежень.
  - Політика помилок рендерингу: суворе виняткове завершення (`PromptRenderException`), без резервного передавання необробленого шаблону.
- Інтеграція завершення сеансу:
  - Підсумок завершення дипломатії: надіслано після створення запису підсумку.
  - Підсумок завершення RPG (включно із завершенням вручну): створено на основі наявних правил історії чату (без додаткового виклику AI), а потім надіслано.
- `GameComponent_RPGManager` шлях запуску/завантаження/завершення виконує прогрів сумісності для відкладеної реєстрації.

## Ізоляція середовища виконання промптів і самовідновлення API (v0.7.24)

- `RimChat.Persistence.PromptPersistenceService.WorkbenchComposer`
  - `BuildUnifiedChannelSystemPrompt(..., deterministicPreview=false)`
    - Шлях середовища виконання тепер явно використовує `deterministicPreview=false`.
    - Структурований попередній перегляд Workbench залишається детермінованим (`true`) через APIs попереднього перегляду розділу/робочого простору.
  - Компоновник середовища виконання тепер перевіряє обов’язкові вихідні дані вузлів за каналами та викидає `PromptRenderException` для порожніх критичних вузлів.

- `RimChat.Persistence.PromptPersistenceService`
  - `BuildFullSystemPrompt(...)`
  - `BuildDiplomacyStrategySystemPrompt(...)`
  - `BuildRPGFullSystemPrompt(...)`
    - Усі точки входу середовища виконання зафіксовано на рендерингу без попереднього перегляду.
  - `BuildDiplomacyStrategySystemPrompt(...)` тепер вимагає непорожнього корисного навантаження середовища виконання стратегії (`negotiator_context/fact_pack/scenario_dossier`) і негайно завершує роботу за відсутності сегментів.
  - `LoadConfig()`
    - Додає семантичну перевірку домену, автоматичне самовідновлення під час запуску, запис резервної копії користувача та журналювання підсумку міграції.
    - Недійсні дані користувацького домену повертаються до завантаження лише стандартних даних, тільки якщо семантична перевірка пройдена.
    - Якщо семантична перевірка лише стандартних даних також завершується невдало: зберегти кешовану конфігурацію та заблокувати запис назад; якщо кешу немає, викинути `PromptRenderException` із негайним завершенням роботи.
  - `CreateDefaultConfig()`
    - Суворий шлях завантаження лише стандартних даних (не читає файли користувацьких промптів).
    - Застарілий мінімальний резервний варіант стандартних даних через `SystemPromptConfig.InitializeDefaults()` вилучено.
  - `AppendDiplomacyResponseFormatSection(...)`
    - Викликає помилку для порожнього `ResponseFormat.JsonTemplate` (негайне аварійне завершення під час виконання).

- `RimChat.Persistence.PromptPersistenceService.DomainStorage`
  - `TryLoadPromptDomains(bool includeCustom, out SystemPromptConfig, out int loadedDomainSchemaVersion, out List<string> validationErrors)`
    - Нове діагностичне перевантаження з деталями семантичної перевірки.
    - `includeCustom=false` виключає всі власні джерела, зокрема `PawnDialoguePrompt_Custom.json`.
    - Якщо пряма композиція лише зі стандартних даних не проходить семантичні перевірки, завантажувач повторно створює дані з агрегованого домену лише зі стандартними даними JSON і перевіряє їх знову.
  - Вимоги до семантичної перевірки домену:
    - `ApiActions` має містити повний стандартний набір дипломатичних дій.
    - `ResponseFormat.JsonTemplate` не має бути порожнім.
    - `PromptTemplates.ApiLimitsNodeTemplate / QuestGuidanceNodeTemplate / ResponseContractNodeTemplate` не має бути порожнім.
  - Джерело дій нормалізовано:
    - `ApiActions` тепер надходить лише з дипломатичного домену.
    - Соціальний домен містить лише текст шаблонів.

- `RimChat.Persistence.PromptDomainFileCatalog`
  - `ResolveModRoot()` тепер нормалізує корені завантажених модифікацій і приймає лише каталоги, що містять `Prompt/Default`.
  - Якщо завантажений кореневий шлях вказує на підпапку версії (наприклад, `.../RimChat/1.6`), розв’язання шляху автоматично переміщується до кореня батьківського моду.

- `RimChat.Persistence.PromptDomainJsonUtility`
  - `LoadSingle<T>()` і `TryDeserialize<T>()` тепер спочатку використовують `ReflectionJsonFieldDeserializer`, а потім переходять до резервного `JsonUtility`.
  - Мета: уникнути непомітного зчитування порожніх об’єктів із даних JSON, розділених за доменами.

- `RimChat.Persistence.SystemPromptDomainConfig`
  - Додано поле: `PromptDomainSchemaVersion` (маркер однієї опорної точки схеми для відстежуваності міграції домену та ідемпотентності).

## Захист цілісності тексту API (v0.7.48)

- `RimChat.AI.TextIntegrityGuard`
  - `ValidateVisibleDialogue(string rawOutput)`
    - Область: лише видимі діалоги diplomacy/RPG.
    - Поведінка: розділяти видимий текст і кінцеві `{"actions":[...]}` JSON, очищати видимий текст, виявляти спотворення кодування та фрагментів.
  - `SanitizeSummaryText(string text, int maxChars = 280)`
    - Область: шлях збереження підсумку.
  - `SanitizeKeyFact(string text, int maxChars = 100)`
    - Область: шлях збереження ключових фактів підсумку.
  - `TryDetectCorruption(string text, out TextIntegrityIssue issue, out string reasonTag)`
    - Теги правил: `replacement_char`, `control_noise`, `low_printable_ratio`, `fragmented_text`.

- `RimChat.AI.AIChatServiceAsync`
  - `ProcessRequestCoroutine(...)`
    - Додано етап повторної спроби перевірки цілісності тексту для `DialogueUsageChannel.Diplomacy` і `DialogueUsageChannel.Rpg`.
    - Ліміт повторних спроб: 1.
    - У разі невдалої повторної спроби: перехід до локалізованого локального рядка, безпечного для занурення.

- `RimChat.Memory.LeaderMemoryManager`
  - `UpsertSummaryInternal(...)`
    - Додано санітизацію перед оновленням і перевірку на пошкодження.
  - `TryQueueSummaryRepair(...)` (внутрішній допоміжний засіб у partial)
    - У разі пошкодженого підсумку: поставити один запит на виправлення в чергу.
    - Помилка виправлення або результат усе ще пошкоджений: відкинути й записати структуроване попередження.

## Виправлення ін'єкції каталогу дипломатичних дій (v0.7.49)

- Problem
  - `request_raid_call_everyone` і `request_raid_waves` були відсутні в каталозі дипломатичних дій під час виконання в деяких збереженнях/конфігураціях.

- Root cause
  - `Prompt/Default/DiplomacyDialoguePrompt_Default.json` не містив цих двох дій.
  - У сценаріях із перевизначенням власного домену відсутні записи в масиві `ApiActions` передавалися до середовища виконання промпту.

- Changes
  - `RimChat.Persistence.PromptPersistenceService.DomainStorage`
    - `BuildApiActions(...)` тепер забезпечує виконання обов’язкових дій для варіантів рейду, додаючи відсутні записи:
      - `request_raid_call_everyone`
      - `request_raid_waves`
    - Наявні записи, налаштовані користувачем, зберігаються; доповнюються лише відсутні поля.
  - `RimChat.Persistence.PromptPersistenceService`
    - `BuildCompactActionParameterHint(...)` додає `request_raid_waves -> waves(2-6)`.
  - `Prompt/Default/DiplomacyDialoguePrompt_Default.json`
    - Додано стандартні визначення для обох зазначених вище дій.




## Ін’єкція фіксованого расового профілю (v0.9.38)

- Додано вузол фіксованої ін’єкції: `mandatory_race_profile` (дипломатія + канал RPG, позиція ін’єкції: після `environment`, перед основним ланцюжком).
- Додано поле шаблону: `PromptTemplateTextConfig.MandatoryRaceInjectionTemplate`.
- Додано змінну: `dialogue.mandatory_race_profile_body`.
- Джерело дипломатичного каналу: `Leader + Negotiator`; джерело каналу RPG: `Target + Initiator`.
- Фіксовані поля кожного персонажа: `Role`, `Name`, `RaceKind`, `RaceDef`, `Xenotype`.
- Стратегія відсутніх даних: для значення поля виводиться `N/A`, запит не блокується.

## Спостереження за журналом, статистика поточного проходження та розбиття на сторінки (v0.9.51)

- Розширення моделі:
  - `RimChat.AI.AIRequestDebugSnapshot`
  - Нове поле: `SessionSummary: AIRequestDebugSessionSummary`
  - Поле `AIRequestDebugSessionSummary`:
    - `SessionElapsedMinutes`
    - `TotalRequestCount`
    - `TotalTokens`
    - `AverageRequestsPerMinute`
    - `AverageTokensPerMinute`
    - `AverageTokensPerRequest`
- Знімок телеметрії:
  - `RimChat.AI.AIChatServiceAsync.BuildRequestDebugSnapshot(DateTime nowUtc)`
  - Коригування формату виводу:
    - `Records`: повний запис у межах цього ігрового процесу (без обмеження 30 хвилинами)
    - `Buckets` / `Summary`: як і раніше, дані лише за останні 30 хвилин
    - `SessionSummary`: сукупна агрегація за цю гру
- Сумісність зовнішнього інтерфейсу:
  - Сигнатура `AIChatServiceAsync.TryGetRequestDebugSnapshot(out AIRequestDebugSnapshot snapshot)` не змінюється, повертаються лише розширені дані моделі.
