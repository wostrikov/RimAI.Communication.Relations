using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.AI;

namespace Ustas.RimAI.Communication.Relations.NpcDialogue
{
    /// <summary>
    /// Dependencies: proactive dialogue generation prompt assembly.
    /// Responsibility: inject manual social-post context into proactive NPC dialogue prompts.
    /// </summary>
        internal sealed class NpcDialoguePushManagerManualSocialPost : GameComponent_NpcDialoguePushManagerCollaborator
    {
        internal NpcDialoguePushManagerManualSocialPost(GameComponent_NpcDialoguePushManager owner) : base(owner)
        {
        }


        internal void AppendManualSocialPostPrompt(List<ChatMessageData> messages, NpcDialogueTriggerContext context)
        {
            if (messages == null || context == null || !string.Equals(context.SourceTag, "manual_social_post", StringComparison.Ordinal))
            {
                return;
            }

            if (!GameComponent_NpcDialoguePushManager.TryParseManualSocialPostReason(context.Reason, out string title, out string body))
            {
                return;
            }

            string prompt =
                "Це проактивне дипломатичне повідомлення є прямою реакцією на публікацію в соціальному колі, написану гравцем.\n" +
                "Публічний контекст: усі фракції потенційно можуть бачити цю публікацію.\n" +
                "Автор: колонія гравця.\n" +
                $"Post title: {title}\n" +
                $"Post body: {body}\n" +
                "Твоя відповідь має явно реагувати на зміст і позицію цієї публікації, а не створювати загальну світську бесіду.\n" +
                "Дозволені тони: підтримка, скепсис, переговори, попередження, тиск, провокація або вербування залежно від позиції фракції.\n";
            messages.Add(new ChatMessageData { role = "user", content = prompt });
        }

        internal static bool TryParseManualSocialPostReason(string reason, out string title, out string body)
        {
            title = string.Empty;
            body = string.Empty;
            if (string.IsNullOrWhiteSpace(reason) || !reason.StartsWith("manual_social_post|", StringComparison.Ordinal))
            {
                return false;
            }

            string payload = reason.Substring("manual_social_post|".Length);
            string[] segments = payload.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i] ?? string.Empty;
                int separator = segment.IndexOf('=');
                if (separator <= 0 || separator >= segment.Length - 1)
                {
                    continue;
                }

                string key = segment.Substring(0, separator).Trim();
                string value = segment.Substring(separator + 1).Trim();
                if (string.Equals(key, "title", StringComparison.OrdinalIgnoreCase))
                {
                    title = value;
                }
                else if (string.Equals(key, "body", StringComparison.OrdinalIgnoreCase))
                {
                    body = value;
                }
            }

            return title.Length > 0 || body.Length > 0;
        }
        }

}
