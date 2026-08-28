namespace Ustas.RimAI.Communication.Relations.Config
{
    /// <summary>
    /// Dependencies: none.
    /// Responsibility: provide a single source of truth for repeated prompt default text literals.
    /// </summary>
    internal static partial class PromptTextConstants
    {
        public const string RequestRaidActionDescription =
            "Здійснити напад на гравця (прибуття із затримкою). Можна використовувати як тактичне рішення після образи, погрози або під час ворожості.";

        public const string RequestRaidActionRequirement = "Використовуй лише тоді, коли твоя фракція вже вороже налаштована до гравця.";

        public const string RequestRaidActionParameters =
            "strategy (string: 'ImmediateAttack', 'ImmediateAttackSmart', 'StageThenAttack', 'ImmediateAttackSappers' або 'Siege'), arrival (string: 'EdgeWalkIn', 'EdgeDrop', 'EdgeWalkInGroups', 'RandomDrop' або 'CenterDrop')";

        public const string RequestRaidCallEveryoneActionDescription =
            "Закликати всі відповідні ворожі фракції до генерального наступу й організувати спільний міжфракційний рейд." +
            "Це не синонім звичайного рейду, а спільна бойова дія вищого рівня." +
            "Коли гравець явно каже 'call everyone', 'joint raid', 'everyone attack', 'all in', 'заклич усіх' або 'спільний напад', зазвичай це прямий запит на такий генеральний наступ." +
            "Війська прибуватимуть поступово протягом 16-30 годин; якщо кількість ворожих фракцій не переважає дружні/нейтральні, дружні/нейтральні фракції вилучаються за прихильністю від нижчої до вищої, доки ворожих не стане більше.";

        public const string RequestRaidCallEveryoneActionRequirement =
            "Дія спільного наступу високої інтенсивності. Явні формулювання гравця на кшталт 'call everyone' або 'joint raid' трактуй як прямий запит на скоординовану загальну атаку, водночас дотримуючись глобального часу відновлення та перевірок придатності під час виконання.";

        public const string RequestRaidWavesActionDescription =
            "Розгорнути тривалу атаку в кілька хвиль. Параметр waves (int, 2-6) визначає кількість хвиль; інтервал між хвилями 12-20 годин." +
            "Це означає кілька послідовних атак для безперервного тиску й підходить, коли гравець прямо просить тривале випробування або коли замість спільного генерального наступу потрібні кілька хвиль.";

        public const string RequestRaidWavesActionRequirement =
            "Час відновлення для фракції: 5 днів. Використовуй, коли гравець прямо просить тривалого тиску або коли атаки хвилями доречніші за скоординований загальний наступ.";

        public const string RequestRaidWavesActionParameters =
            "waves (int, 2-6, кількість хвиль рейду)";

        public const string GoOfflineActionDescription =
            "Завершити розмову й перемкнутися в офлайн-статус";

        public const string SetDndActionDescription =
            "Перемкнутися в статус 'не турбувати' й припинити обмін повідомленнями";

        public const string SendImageActionDescription =
            "Цю дію вимкнено; функцію зображень дозволено запускати лише гравцеві вручну через вхід для селфі.";

        public const string SendImageActionParameters =
            "disabled";

        public const string SendImageActionRequirement =
            "AI заборонено самостійно запускати send_image.";

        public const string SendImageCaptionStylePromptDefault =
            "Напиши живий підпис до зображення, наче лідер фракції ділиться новим фото в чаті. Тон природний, занурений, з емоцією.";
        public const string SendImageCaptionFallbackTemplateDefault =
            "Це наш ватажок {{ pawn.leader.name }}, гарний, правда?";

        public const string SendImageDefaultTemplateName = "Портрет лідера";
        public const string SendImageDefaultTemplateDescription =
            "Кінематографічна портретна сцена лідера, що відповідає рівню технологій RimWorld і статусу фракції.";
        public const string SendImageDefaultTemplateText =
            "Створи для цього лідера фракції портретну сцену RimWorld, що пасує сеттингу й світові."
            + "Середовище, одяг і рівень технологій мають відповідати тлу фракції."
            + "Природні матеріали та практичне спорядження, обличчя має лишатися чітко впізнаваним.";

        public const string ApiLimitsNodeLiteralDefault =
            "{{ dialogue.api_limits_body }}";

        public const string QuestGuidanceNodeLiteralDefault =
            "{{ dialogue.quest_guidance_body }}";

        public const string ResponseContractNodeLiteralDefault =
            "{{ dialogue.response_contract_body }}";

        public const string OutputSpecificationAuthorityHeader = "Авторитет правил виводу:";
        public const string OutputSpecificationAuthorityReference =
            "Протокол відповіді визначається лише в цьому авторитетному розділі. Інші розділи можуть тільки посилатися на нього, повторно визначати правила заборонено.";
        public const string OutputSpecificationAuthorityBoundaryRule =
            "- Заборони щодо ШІ-природи, числових показників та ігрових механік у природній мові стосуються лише visible_dialogue; структуровані поля слугують тільки парсеру й виконанню дій.";
        public const string OutputSpecificationAuthorityLegacyRule =
            "- Заборонено вживати старий формат обгортки однієї дії (наприклад {\"action\":\"...\",\"parameters\":{...},\"response\":\"...\"}), а також старі обгортки dialogue/content/text; чинний лише контракт visible_dialogue + actions.";
        public const string OutputSpecificationAuthorityHistoryStyleRule =
            "- Не наслідуй стиль метакоментарів з історії; історія дає лише сюжетні факти, а не форму виводу.";

        public const string ActionsHeader = "Каталог дій:";
        public const string ResponseFormatHeader = "Формат відповіді:";
        public const string ResponseFormatReference =
            "Єдиний чинний контракт відповіді визначає розділ «Авторитет правил виводу» вище; типово виводиться один обʼєкт JSON із головним полем visible_dialogue.";
        public const string CriticalActionRulesHeader = "Ключові правила дій:";
        public const string CriticalActionRulesReference =
            "Усі правила протоколу й меж визначає розділ «Авторитет правил виводу» вище.";
        public const string NoActionResponseHint = "Якщо дія не потрібна, усе одно виведи обʼєкт JSON лише з visible_dialogue, не додаючи actions.";
        public const string StrictJsonFormatHeader = "### Вимоги до формату (найвищий пріоритет, дотримуватися суворо)";
        public const string StrictJsonFormatRequirement = "Уся твоя відповідь має бути одним обʼєктом JSON: перший символ { останній символ }, поза JSON не можна додавати ані тексту, ані пояснень, ані Markdown. Заборонено обгортати у блок ```json ```, заборонено писати будь-які репліки чи примітки до або після JSON — виводь чистий JSON.";
        public const string StrictJsonFormatTemplate = "{\n  \"visible_dialogue\":\"текст дипломатичної репліки\"\n}";
        public const string StrictJsonFormatTemplateWithAction = "{\n  \"visible_dialogue\":\"текст дипломатичної репліки\",\n  \"actions\":[\n    {\"action\":\"request_item_airdrop\",\"parameters\":{\"need\":\"1000 деревини\",\"payment_items\":[{\"item\":\"Silver\",\"count\":1200}]}}\n  ]\n}";

        public const string GoodwillPeacePolicyHeader = "Динамічна мирна політика (за прихильністю):";
        public const string GoodwillPeacePolicyVeryLowLine1 =
            "- Поточна прихильність: {0}. Використовувати make_peace чи пряму мирну угоду заборонено.";
        public const string GoodwillPeacePolicyVeryLowLine2 =
            "- Причина: ворожість глибша за {0}, тож укладати угоду одразу не можна.";
        public const string GoodwillPeacePolicyTalkOnlyLine1 =
            "- Поточна прихильність: {0}. У цьому діапазоні make_peace використовувати заборонено.";
        public const string GoodwillPeacePolicyTalkOnlyLine2 =
            "- Для перемовин обовʼязково використовуй create_quest із questDefName '{0}'.";
        public const string GoodwillPeacePolicyTalkOnlyLine3 =
            "- Причина: прихильність у діапазоні [{0},{1}], тож перед прямим миром потрібні перемовини.";
        public const string GoodwillPeacePolicyReenabledLine1 =
            "- Поточна прихильність: {0}. Доступні і make_peace, і завдання на перемовини.";
        public const string GoodwillPeacePolicyReenabledLine2 =
            "- Якщо обрано create_quest, для перемовин має бути questDefName '{0}'.";
    }
}
