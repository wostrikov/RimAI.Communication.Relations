using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class DiplomacyDialogueMessageView : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueMessageView(Dialog_DiplomacyDialogue owner) : base(owner) { }



internal void DrawChatArea(Rect rect)
{
    Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.1f));
    Widgets.DrawBox(rect);

    Rect innerRect = rect.ContractedBy(10f);

    float inputHeight = Dialog_DiplomacyDialogue.INPUT_AREA_HEIGHT;
    float controlsHeight = Owner.Parts.StrategyUi.GetStrategyControlsHeight();
    float spacing = 10f;
    float messagesHeight = innerRect.height - inputHeight - controlsHeight - spacing * 2f;
    if (messagesHeight < 60f)
    {
        messagesHeight = 60f;
    }

    // Message区域
    Rect messagesRect = new Rect(innerRect.x, innerRect.y, innerRect.width, messagesHeight);
    DrawMessages(messagesRect);

    // 分隔线1 - message与控制区之间
    float line1Y = innerRect.y + messagesHeight + 5f;
    Color oldLineColor = GUI.color;
    GUI.color = new Color(0.55f, 0.58f, 0.66f, 0.35f);
    Widgets.DrawLineHorizontal(innerRect.x, line1Y, innerRect.width);

    // 单行控制区: 策略button
    float controlsY = line1Y + 5f;
    Rect controlsRect = new Rect(innerRect.x, controlsY, innerRect.width, controlsHeight);
    Owner.Parts.StrategyUi.DrawControlsRow(controlsRect);

    // 分隔线2 - 控制区与input框之间
    float line2Y = controlsY + controlsHeight + 5f;
    Widgets.DrawLineHorizontal(innerRect.x, line2Y, innerRect.width);
    GUI.color = oldLineColor;

    // Input区域
    float inputY = line2Y + 5f;
    Rect inputRect = new Rect(innerRect.x, inputY, innerRect.width, inputHeight);
    Owner.Parts.Input.DrawInputArea(inputRect);
}



internal void DrawMessages(Rect rect)
{
    fallbackRetryRequestedThisFrame = false;
    lastMessagesViewRect = rect;
    if (session == null || session.messages.Count == 0)
    {
        GUI.color = new Color(0.4f, 0.4f, 0.45f);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, "RimChat_StartConversation".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        lastMessageCount = 0;
        return;
    }

    float viewportWidth = rect.width - 16f;
    Owner.Parts.MessageLayout.EnsureLayoutCache(viewportWidth);
    float contentHeight = Owner.Parts.MessageLayout._cachedTotalContentHeight;

    float viewHeight = Mathf.Max(contentHeight, rect.height);
    Rect viewRect = new Rect(0f, 0f, viewportWidth, viewHeight);

    bool hasNewMessage = session.messages.Count > lastMessageCount;
    float maxScroll = Mathf.Max(0f, contentHeight - rect.height);

    Vector2 beforeScroll = messageScrollPosition;

    if ((hasNewMessage || Owner.Parts.MessageLayout._typewriterActiveMsg != null) && !userIsScrolling)
    {
        messageScrollPosition = new Vector2(messageScrollPosition.x, maxScroll);
    }
    lastMessageCount = session.messages.Count;

    messageScrollPosition = GUI.BeginScrollView(rect, messageScrollPosition, viewRect);

    if (Event.current.type == EventType.ScrollWheel ||
        (Event.current.type == EventType.MouseDrag && Mouse.IsOver(rect)))
    {
        userIsScrolling = true;
    }

    float curY = 10f;
    DialogueMessageData prevMsg = null;

    for (int i = 0; i < session.messages.Count; i++)
    {
        var msg = session.messages[i];
        if (prevMsg != null && ShouldShowTimeGap(prevMsg.GetGameTick(), msg.GetGameTick()))
        {
            DrawTimeGapLine(prevMsg.GetGameTick(), msg.GetGameTick(), viewRect.width, curY);
            curY += 35f;
        }

        float bubbleWidth;
        float msgHeight;
        if (Owner.Parts.MessageLayout.TryGetCachedLayout(msg, out MessageLayoutEntry entry))
        {
            bubbleWidth = entry.BubbleWidth;
            msgHeight = entry.MessageHeight;
        }
        else
        {
            float maxSystemWidth = Owner.Parts.Speakers.GetMaxSystemMessageWidth(viewRect.width);
            float maxBubbleWidth = Owner.Parts.Speakers.GetMaxBubbleWidth(viewRect.width);
            bubbleWidth = msg.IsSystemMessage() ? CalculateBubbleWidth(msg, maxSystemWidth) : CalculateBubbleWidth(msg, maxBubbleWidth);
            msgHeight = CalculateMessageHeight(msg, bubbleWidth);
            // Do NOT invalidate here: EnsureLayoutCache already rebuilt the cache at
            // the top of DrawMessages. A miss means this message is still mutating
            // (e.g. typewriter), so just compute its layout inline this frame.
        }

        if (msg.IsSystemMessage())
        {
            Rect msgRect = new Rect(20f, curY, bubbleWidth, msgHeight);
            DrawRoundedMessageBubble(msg, msgRect);
        }
        else
        {
            float msgX = Owner.Parts.Speakers.GetBubbleXForMessage(msg, viewRect.width, bubbleWidth);
            Rect msgRect = new Rect(msgX, curY, bubbleWidth, msgHeight);
            Owner.Parts.Speakers.TryLogBubbleLayoutOutOfTrackOnce(msg, msgRect, viewRect.width);
            DrawRoundedMessageBubble(msg, msgRect);
            Owner.Parts.Speakers.DrawMessageAvatar(msg, msgRect);
        }

        curY += msgHeight + ResolveMessageBottomGap(msg);
        prevMsg = msg;
    }

    if (messageScrollPosition.y >= maxScroll - 10f)
    {
        userIsScrolling = false;
    }

    GUI.EndScrollView();
}



internal bool ShouldShowTimeGap(int prevGameTick, int currentGameTick)
{
    int tickDiff = currentGameTick - prevGameTick;
    float minutes = tickDiff / 2500f;
    return minutes >= Dialog_DiplomacyDialogue.TIME_GAP_THRESHOLD_MINUTES;
}



internal static float ResolveMessageBottomGap(DialogueMessageData msg)
{
    if (msg != null && msg.IsSystemMessage())
    {
        return 4f;
    }

    return 6f;
}



internal void DrawTimeGapLine(int prevGameTick, int currentGameTick, float width, float y)
{
    int tickDiff = currentGameTick - prevGameTick;
    string gapText = FormatGameTimeGap(tickDiff);

    float textWidth = Text.CalcSize(gapText).x;
    float centerX = width / 2f;
    float lineWidth = (width - textWidth - 40f) / 2f;

    GUI.color = new Color(0.4f, 0.4f, 0.45f, 0.6f);
    
    Widgets.DrawLineHorizontal(20f, y + 12f, lineWidth - 10f);
    Widgets.DrawLineHorizontal(centerX + textWidth / 2f + 10f, y + 12f, lineWidth - 10f);

    Text.Font = GameFont.Tiny;
    GUI.color = new Color(0.5f, 0.5f, 0.55f, 0.8f);
    Rect textRect = new Rect(centerX - textWidth / 2f, y + 4f, textWidth, 16f);
    Widgets.Label(textRect, gapText);
    Text.Font = GameFont.Small;
    GUI.color = Color.white;
}



internal string FormatGameTimeGap(int tickDiff)
{
    float minutes = tickDiff / 2500f;
    float hours = minutes / 60f;
    float days = hours / 24f;

    if (minutes < 60f)
    {
        return "RimChat_MinutesAgo".Translate(Mathf.RoundToInt(minutes));
    }
    else if (hours < 24f)
    {
        return "RimChat_HoursAgo".Translate(Mathf.RoundToInt(hours));
    }
    else
    {
        return "RimChat_DaysAgo".Translate(Mathf.RoundToInt(days));
    }
}



internal void DrawRoundedMessageBubble(DialogueMessageData msg, Rect rect)
{
    if (msg.IsSystemMessage())
    {
        DrawSystemMessage(msg, rect);
    }
    else if (msg.HasInlineImage())
    {
        Owner.Parts.ImageBubbles.DrawImageMessageBubble(msg, rect);
    }
    else if (msg.IsAirdropTradeCard())
    {
        Owner.Parts.AirdropCards.DrawAirdropTradeCardBubble(msg, rect);
    }
    else
    {
        DrawNormalMessageBubble(msg, rect);
    }
}



internal void DrawSystemMessage(DialogueMessageData msg, Rect rect)
{
    float padding = 3f;
    float contentX = rect.x + padding;
    float contentY = rect.y + padding;
    float contentWidth = rect.width - padding * 2f;

    GUI.color = new Color(0.5f, 0.5f, 0.55f, 0.9f);
    
    Text.Font = GameFont.Tiny;
    Rect contentRect = new Rect(contentX, contentY, contentWidth, rect.height - padding * 2f);
    Widgets.Label(contentRect, msg.message);
    
    GUI.color = Color.white;
    Text.Font = GameFont.Small;
}



internal void DrawNormalMessageBubble(DialogueMessageData msg, Rect rect)
{
    Color bubbleColor;
    Color textColor;
    Color senderColor;
    
    if (msg.isPlayer)
    {
        bubbleColor = DiplomacySessionApplication.PlayerBubbleColor;
        textColor = new Color(0.1f, 0.1f, 0.1f);
        senderColor = new Color(0.2f, 0.3f, 0.15f);
    }
    else
    {
        bubbleColor = DiplomacySessionApplication.AIBubbleColor;
        textColor = new Color(0.95f, 0.95f, 0.97f);
        senderColor = new Color(0.75f, 0.8f, 0.9f);
    }

    // 绘制阴影 (更柔和, 现代的下拉阴影)
    Rect shadowRect = new Rect(rect.x + 1f, rect.y + 3f, rect.width, rect.height);
    DrawRoundedRect(shadowRect, new Color(0f, 0f, 0f, 0.12f), Dialog_DiplomacyDialogue.BUBBLE_CORNER_RADIUS);

    // 绘制气泡背景 (圆角)
    DrawRoundedRect(rect, bubbleColor, Dialog_DiplomacyDialogue.BUBBLE_CORNER_RADIUS);

    float padding = 10f;
    float contentX = rect.x + padding;
    float contentY = rect.y + 8f;
    float contentWidth = rect.width - padding * 2f;

    // 发送者name与时间戳 (头部)
    Text.Font = GameFont.Tiny;
    float headerHeight = 18f; // Ensure enough vertical space for text
    
    GUI.color = senderColor;
    Rect senderRect = new Rect(contentX, contentY, contentWidth * 0.7f, headerHeight);
    Widgets.Label(senderRect, Owner.Parts.Speakers.GetDisplaySenderName(msg));

    string timeStr = GetTimestampString(msg);
    float timeWidth = Text.CalcSize(timeStr).x + 5f;
    Rect timeRect = new Rect(rect.xMax - timeWidth - padding, contentY, timeWidth, headerHeight);
    GUI.color = new Color(senderColor.r, senderColor.g, senderColor.b, 0.65f);
    Widgets.Label(timeRect, timeStr);

    // Contents区域起始位置
    contentY += headerHeight + 2f;
    
    Text.Font = GameFont.Small;
    GUI.color = textColor;

    // Messagecontents (使用真正的逐字outputtext进行排版渲染)
    string displayText = GetDisplayText(msg);
    float retryReservedWidth = ShouldShowFallbackRetryButton(msg)
        ? Dialog_DiplomacyDialogue.FallbackRetryButtonSize + Dialog_DiplomacyDialogue.FallbackRetryButtonMargin
        : 0f;
    float effectiveContentWidth = Mathf.Max(40f, contentWidth - retryReservedWidth);
    float actualTextHeight = Text.CalcHeight(displayText, effectiveContentWidth);
    Rect contentRect = new Rect(contentX, contentY, effectiveContentWidth, actualTextHeight);
    Widgets.Label(contentRect, displayText);
    DrawFallbackRetryButton(msg, rect, contentY, headerHeight);

    GUI.color = Color.white;
    Text.Font = GameFont.Small;
}



internal void DrawRoundedRect(Rect rect, Color color, float radius)
{
    GUI.color = color;
    float r = Mathf.Min(radius, rect.width / 2f, rect.height / 2f);

    // 绘制中心rectangle及十字区域
    GUI.DrawTexture(new Rect(rect.x + r, rect.y, rect.width - r * 2f, rect.height), Dialog_DiplomacyDialogue.WhiteTexture);
    GUI.DrawTexture(new Rect(rect.x, rect.y + r, rect.width, rect.height - r * 2f), Dialog_DiplomacyDialogue.WhiteTexture);

    // 左侧圆角沿用原始坐标，右侧圆角单独做像素对齐，修复 1.25x 缩放下稳定右移 1px 的问题。
    float rightCornerX = Mathf.Floor(rect.x + rect.width - r);

    // 使用高清抗锯齿圆角纹理进行圆滑边角绘制 (Unity GUI texCoords 中 0,0 为左下角)
    // 左上角
    GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, r, r), Dialog_DiplomacyDialogue.CircleTexture, new Rect(0f, 0.5f, 0.5f, 0.5f));
    // 右上角
    GUI.DrawTextureWithTexCoords(new Rect(rightCornerX, rect.y, r, r), Dialog_DiplomacyDialogue.CircleTexture, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
    // 左下角
    GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.yMax - r, r, r), Dialog_DiplomacyDialogue.CircleTexture, new Rect(0f, 0f, 0.5f, 0.5f));
    // 右下角
    GUI.DrawTextureWithTexCoords(new Rect(rightCornerX, rect.yMax - r, r, r), Dialog_DiplomacyDialogue.CircleTexture, new Rect(0.5f, 0f, 0.5f, 0.5f));

    GUI.color = Color.white;
}



internal float CalculateMessageHeight(DialogueMessageData msg, float width)
{
    string displayText = GetDisplayText(msg);

    if (msg.IsSystemMessage())
    {
        float systemTextWidth = Mathf.Min(width - 6f, 600f);
        GameFont oldFont = Text.Font;
        Text.Font = GameFont.Tiny;
        float systemTextHeight = Text.CalcHeight(displayText, systemTextWidth);
        Text.Font = oldFont;
        return Mathf.Max(14f, systemTextHeight + 6f);
    }

    if (msg.HasInlineImage())
    {
        return Owner.Parts.ImageBubbles.CalculateImageMessageHeight(msg, width);
    }

    if (msg.IsAirdropTradeCard())
    {
        return Owner.Parts.AirdropCards.CalculateAirdropTradeCardBubbleHeight(msg, width);
    }

    // 精确计算text高度: based ondynamicoutput的字符重新计算
    float contentWidth = width - 20f; // padding 10f * 2
    float retryReserved = (msg != null && msg.allowFallbackRetry && !msg.isPlayer)
        ? Dialog_DiplomacyDialogue.FallbackRetryButtonSize + Dialog_DiplomacyDialogue.FallbackRetryButtonMargin : 0f;
    float effectiveWidth = Mathf.Max(40f, contentWidth - retryReserved);
    float textHeight = Text.CalcHeight(displayText, effectiveWidth);

    // 总高度 = 上内边距(8f) + 头高度(18f) + 间距(2f) + contents高度 + 下内边距(6f) = 34f + textHeight
    float totalHeight = 34f + textHeight;
    return Mathf.Max(50f, totalHeight);
}



internal float CalculateBubbleWidth(DialogueMessageData msg, float maxWidth)
{
    string fullText = msg?.message ?? string.Empty;
    string displayText = GetDisplayText(msg);
    float textWidth = Text.CalcSize(fullText).x;
    
    if (msg.IsSystemMessage())
    {
        return Mathf.Min(textWidth + 40f, maxWidth);
    }

    if (msg.HasInlineImage())
    {
        if (DiplomacyDialogueSpeakers.IsOutboundPrisonerInfoMessage(msg))
        {
            // Widen ransom proof cards only so long ID lines do not wrap.
            float preferredWidth = Mathf.Clamp(maxWidth * 0.72f, 360f, 540f);
            return Mathf.Min(maxWidth, preferredWidth);
        }

        if (maxWidth >= 260f)
        {
            return maxWidth;
        }

        return Mathf.Max(140f, maxWidth);
    }

    if (msg.IsAirdropTradeCard())
    {
        return Mathf.Clamp(maxWidth * 0.65f, 280f, 420f);
    }

    // Get头部名字和日期的自然宽度
    GameFont oldFont = Text.Font;
    Text.Font = GameFont.Tiny;
    float headerWidth = Text.CalcSize(Owner.Parts.Speakers.GetDisplaySenderName(msg)).x + Text.CalcSize(GetTimestampString(msg)).x + 25f;
    Text.Font = oldFont;

    float minBubbleWidth = 140f;
    float contentMaxWidth = Mathf.Max(108f, maxWidth - 32f);
    float displayHeightAtMaxWidth = Text.CalcHeight(displayText, contentMaxWidth);
    float singleLineHeight = Mathf.Max(16f, Text.CalcHeight("A", contentMaxWidth));
    bool multiline = displayHeightAtMaxWidth > singleLineHeight * 1.35f;

    if (multiline)
    {
        return Mathf.Clamp(contentMaxWidth + 32f, minBubbleWidth, maxWidth);
    }

    float compactContentWidth = Mathf.Min(contentMaxWidth, Mathf.Max(textWidth, headerWidth));
    float estimatedWidth = compactContentWidth + 32f;
    return Mathf.Clamp(estimatedWidth, minBubbleWidth, maxWidth);
}



internal string GetTimestampString(DialogueMessageData msg)
{
    int currentTick = Find.TickManager.TicksGame;
    int messageTick = msg.GetGameTick();
    int tickDiff = currentTick - messageTick;
    
    float minutes = tickDiff / 2500f;
    float hours = minutes / 60f;
    float days = hours / 24f;

    if (minutes < 1f)
    {
        return "RimChat_JustNow".Translate();
    }
    else if (minutes < 60f)
    {
        return "RimChat_MinutesAgo".Translate(Mathf.RoundToInt(minutes));
    }
    else if (hours < 24f)
    {
        return "RimChat_HoursAgo".Translate(Mathf.RoundToInt(hours));
    }
    else
    {
        return "RimChat_DaysAgo".Translate(Mathf.RoundToInt(days));
    }
}



       /// <summary>/// 更新逐字output效果
///</summary>
       internal void UpdateTypewriterEffect()
       {
           if (session == null || session.messages == null) return;

           float deltaTime = Time.realtimeSinceStartup - lastTypewriterUpdate;
           lastTypewriterUpdate = Time.realtimeSinceStartup;

           if (_typewriterDirty)
           {
               Owner.Parts.MemorySync.RemoveStaleTypewriterStates(session);
               _typewriterDirty = false;
           }

           Owner.Parts.MessageLayout._typewriterActiveMsg = null;
           for (int i = session.messages.Count - 1; i >= 0; i--)
           {
               var msg = session.messages[i];
               if (msg.isPlayer || msg.IsSystemMessage()) continue;

               if (!typewriterStates.TryGetValue(msg, out TypewriterState state))
               {
                   state = new TypewriterState
                   {
                       FullText = msg.message,
                       VisibleCharCount = 0,
                       AccumulatedTime = 0f,
                       IsComplete = false,
                       DisplayText = string.Empty
                   };
                   typewriterStates[msg] = state;
                   Owner.Parts.MessageLayout.InvalidateLayoutCache();
               }

               DiplomacyMemorySyncCoordinator.SyncTypewriterStateText(msg, state);

               if (!state.IsComplete)
               {
                   Owner.Parts.MessageLayout._typewriterActiveMsg = msg;
                   state.AccumulatedTime += deltaTime;
                   int targetCount = Mathf.FloorToInt(state.AccumulatedTime * 30f);
                   if (targetCount > state.VisibleCharCount)
                   {
                       if (targetCount % 3 == 0)
                       {
                           SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                       }

                       state.VisibleCharCount = Math.Min(targetCount, state.FullText.Length);
                       state.DisplayText = state.FullText.Substring(0, state.VisibleCharCount);

                       if (state.VisibleCharCount >= state.FullText.Length)
                       {
                           state.IsComplete = true;
                           Owner.Parts.MessageLayout._typewriterActiveMsg = null;
                       }
                   }
                   break;
               }
           }

           Owner.Parts.Airdrop.TryProcessPendingAirdropDialog();
       }



internal string GetDisplayText(DialogueMessageData msg)
{
    if (msg.isPlayer || msg.IsSystemMessage()) return msg.message;

    if (typewriterStates.TryGetValue(msg, out TypewriterState state))
    {
        return state.DisplayText;
    }
    return msg.message;
}



internal bool ShouldShowFallbackRetryButton(DialogueMessageData msg)
{
    return session != null &&
           msg != null &&
           !msg.isPlayer &&
           !msg.IsSystemMessage() &&
           msg.allowFallbackRetry &&
           !session.isWaitingForResponse &&
           !fallbackRetryRequestedThisFrame &&
           string.Equals(msg.message ?? string.Empty, "RimChat_ImmersionFallback_Diplomacy".Translate().ToString(), StringComparison.Ordinal);
}



internal void DrawFallbackRetryButton(DialogueMessageData msg, Rect bubbleRect, float contentY, float headerHeight)
{
    if (!ShouldShowFallbackRetryButton(msg))
    {
        return;
    }

    Rect buttonRect = new Rect(
        bubbleRect.xMax - Dialog_DiplomacyDialogue.FallbackRetryButtonSize - 10f,
        contentY + headerHeight + 2f,
        Dialog_DiplomacyDialogue.FallbackRetryButtonSize,
        Dialog_DiplomacyDialogue.FallbackRetryButtonSize);
    bool hovered = Mouse.IsOver(buttonRect);
    Color bg = hovered
        ? new Color(0.30f, 0.36f, 0.44f, 0.92f)
        : new Color(0.24f, 0.29f, 0.36f, 0.85f);
    DrawRoundedRect(buttonRect, bg, 6f);
    Text.Anchor = TextAnchor.MiddleCenter;
    Text.Font = GameFont.Tiny;
    GUI.color = Color.white;
    Widgets.Label(buttonRect, "↻");
    Text.Anchor = TextAnchor.UpperLeft;
    Text.Font = GameFont.Small;
    GUI.color = Color.white;
    TooltipHandler.TipRegion(buttonRect, "RimChat_Retry".Translate().ToString());
    if (Widgets.ButtonInvisible(buttonRect))
    {
        fallbackRetryRequestedThisFrame = true;
        Owner.Parts.Fallback.TryRetryImmersionFallbackMessage(msg);
        Event.current.Use();
    }
}
}
