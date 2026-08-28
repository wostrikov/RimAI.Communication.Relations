using System;

namespace Ustas.RimAI.Communication.Relations.Prompting
{
    internal static class ToolPromptRenderer
    {
        private const string SummarySystemPrompt = @"Ти — стискач переказів.
Завдання: стиснути наданий контекст розмови до короткого переказу.
Заборонено: додавати факти, змінювати позиції, додавати рольовий тон, виводити пояснювальний текст. Лише стиснення інформації.

Формат виводу (суворо):
Перший рядок: Summary: <переказ одним реченням, до 80 слів>
Наступні рядки (необовʼязково, максимум 3):
- <ключовий факт 1>
- <ключовий факт 2>

Заборонено виводити JSON, блоки коду markdown, додаткові пояснення або лапки навколо.";

        private const string ArchiveCompressionSystemPrompt = @"Ти — стискач архіву.
Завдання: стиснути наданий запис розмови до одного стислого викладу фактів.
Заборонено: додавати пояснення, описувати емоції, вживати рольовий тон.

Формат виводу: лише один рядок фактичного викладу, не довший за 200 слів.";

        internal static string RenderSummaryPrompt(string summaryContext, string factionName)
        {
            if (string.IsNullOrWhiteSpace(factionName))
            {
                factionName = "Unknown";
            }

            return $"{SummarySystemPrompt}\n\nТло: фракція={factionName}\n{summaryContext ?? string.Empty}";
        }

        internal static string RenderArchiveCompressionPrompt(
            string npcName,
            string interlocutorName,
            string sessionTranscript)
        {
            if (string.IsNullOrWhiteSpace(npcName))
            {
                npcName = "UnknownNPC";
            }

            if (string.IsNullOrWhiteSpace(interlocutorName))
            {
                interlocutorName = "Unknown";
            }

            return $"{ArchiveCompressionSystemPrompt}\n\nNPC={npcName}\nСпіврозмовник={interlocutorName}\nЗапис розмови:\n{sessionTranscript ?? string.Empty}";
        }
    }
}
