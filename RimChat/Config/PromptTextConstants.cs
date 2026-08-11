using RimChat.Persistence;

namespace RimChat.Config
{
    /// <summary>/// Dependencies: none.
 /// Responsibility: provide a single source of truth for repeated prompt default text literals.
 ///</summary>
    internal static class PromptTextConstants
    {
        private static RpgPromptDefaultsConfig _cachedRpgDefaults;
        private static SocialCirclePromptDomainConfig _cachedSocialDefaults;

        private static RpgPromptDefaultsConfig RpgDefaults =>
            _cachedRpgDefaults ?? (_cachedRpgDefaults = RpgPromptDefaultsProvider.GetDefaults());

        private static SocialCirclePromptDomainConfig SocialDefaults =>
            _cachedSocialDefaults ?? (_cachedSocialDefaults = SocialCirclePromptDefaultsProvider.GetDefaults());

        public static string RpgRoleSettingDefault =>
            RpgDefaults.RoleSetting;

        public static string RpgDialogueStyleDefault =>
            RpgDefaults.DialogueStyle;

        public static string RpgFormatConstraintDefault =>
            RpgDefaults.FormatConstraint;

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

        public static string PublishPublicPostActionDescription =>
            SocialDefaults.PublishPublicPostAction?.Description ?? string.Empty;

        public static string PublishPublicPostActionParameters =>
            SocialDefaults.PublishPublicPostAction?.Parameters ?? string.Empty;

        public static string PublishPublicPostActionRequirement =>
            SocialDefaults.PublishPublicPostAction?.Requirement ?? string.Empty;

        public const string SendImageActionDescription =
            "Цю дію вимкнено; функцію зображень дозволено запускати лише гравцеві вручну через вхід для селфі.";

        public const string SendImageActionParameters =
            "disabled";

        public const string SendImageActionRequirement =
            "AI заборонено самостійно запускати send_image.";

        public const string SendImageCaptionStylePromptDefault =
            "写一句生动的图片文案，像派系领袖在聊天中分享一张新照片。语气要自然、沉浸并带情绪表达。";
        public const string SendImageCaptionFallbackTemplateDefault =
            "这是我们首领{{ pawn.leader.name }}，怎么样够帅吧？";

        public const string SendImageDefaultTemplateName = "领袖肖像";
        public const string SendImageDefaultTemplateDescription =
            "基于 RimWorld 科技水平与派系身份的电影感领袖肖像场景。";
        public const string SendImageDefaultTemplateText =
            "为这位派系领袖创建一个贴合设定、符合世界观的 RimWorld 肖像场景。"
            + "环境、服饰与科技水平需与派系背景一致。"
            + "使用自然材质与实用装备，并保证面部清晰可辨。";

        public static string SocialCircleNewsStyleTemplateDefault =>
            SocialDefaults.SocialCircleNewsStyleTemplate ?? string.Empty;

        public static string SocialCircleNewsJsonContractTemplateDefault =>
            SocialDefaults.SocialCircleNewsJsonContractTemplate ?? string.Empty;

        public static string SocialCircleNewsFactTemplateDefault =>
            SocialDefaults.SocialCircleNewsFactTemplate ?? string.Empty;

        public const string ApiLimitsNodeLiteralDefault =
            "{{ dialogue.api_limits_body }}";

        public const string QuestGuidanceNodeLiteralDefault =
            "{{ dialogue.quest_guidance_body }}";

        public const string ResponseContractNodeLiteralDefault =
            "{{ dialogue.response_contract_body }}";

        public const string OutputSpecificationAuthorityHeader = "输出规范权威区：";
        public const string OutputSpecificationAuthorityReference =
            "响应协议仅在本权威区定义。其他分段只能引用，禁止重复定义规则。";
        public const string OutputSpecificationAuthorityBoundaryRule =
            "- 自然语言中关于 AI 身份、数值或游戏机制的禁令仅适用于 visible_dialogue；结构化字段仅服务解析器与动作执行。";
        public const string OutputSpecificationAuthorityLegacyRule =
            "- 禁止使用旧版单动作包装格式（如 {\"action\":\"...\",\"parameters\":{...},\"response\":\"...\"}）以及 dialogue/content/text 旧包装；仅 visible_dialogue + actions 契约有效。";
        public const string OutputSpecificationAuthorityHistoryStyleRule =
            "- 不要模仿历史中的元注释风格；历史只提供剧情事实，不提供输出样式。";

        public const string ActionsHeader = "动作目录：";
        public const string ResponseFormatHeader = "响应格式：";
        public const string ResponseFormatReference =
            "唯一有效的响应契约请以上方“输出规范权威区”为准；默认输出一个 JSON 对象，主字段为 visible_dialogue。";
        public const string CriticalActionRulesHeader = "关键动作规则：";
        public const string CriticalActionRulesReference =
            "所有协议与边界规则以上方“输出规范权威区”为准。";
        public const string NoActionResponseHint = "如果不需要动作，请仍输出一个 JSON 对象，只保留 visible_dialogue，不要附加 actions。";
        public const string StrictJsonFormatHeader = "### 格式要求（最高优先级，必须严格遵守）";
        public const string StrictJsonFormatRequirement = "你的整条回复必须是一个 JSON 对象，首字符 { 末字符 }，不得在 JSON 外附加任何文本、解释或 Markdown。禁止用 ```json ``` 代码块包裹，禁止在 JSON 前后写任何对白或说明——直接输出原始 JSON。";
        public const string StrictJsonFormatTemplate = "{\n  \"visible_dialogue\":\"外交发言文本\"\n}";
        public const string StrictJsonFormatTemplateWithAction = "{\n  \"visible_dialogue\":\"外交发言文本\",\n  \"actions\":[\n    {\"action\":\"request_item_airdrop\",\"parameters\":{\"need\":\"1000原木\",\"payment_items\":[{\"item\":\"Silver\",\"count\":1200}]}}\n  ]\n}";

        public const string GoodwillPeacePolicyHeader = "动态和平策略（基于好感）：";
        public const string GoodwillPeacePolicyVeryLowLine1 =
            "- 当前好感：{0}。禁止使用 make_peace 或直接和约动作。";
        public const string GoodwillPeacePolicyVeryLowLine2 =
            "- 原因：敌意已深于 {0} 以下，不允许立即缔约。";
        public const string GoodwillPeacePolicyTalkOnlyLine1 =
            "- 当前好感：{0}。在该区间禁止使用 make_peace。";
        public const string GoodwillPeacePolicyTalkOnlyLine2 =
            "- 和谈必须使用 create_quest，并指定 questDefName '{0}'。";
        public const string GoodwillPeacePolicyTalkOnlyLine3 =
            "- 原因：好感处于 [{0},{1}] 区间，直接和平前必须先进行和谈。";
        public const string GoodwillPeacePolicyReenabledLine1 =
            "- 当前好感：{0}。make_peace 与和谈任务均可使用。";
        public const string GoodwillPeacePolicyReenabledLine2 =
            "- 若选择 create_quest，和谈应使用 questDefName '{0}'。";
    }
}

