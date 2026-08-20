using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
    public class GoodwillChangeAnimation
    {
        public Faction TargetFaction { get; set; }
        public int ChangeAmount { get; set; }
        public float StartTime { get; set; }
        public Vector2 StartPosition { get; set; }
        public float Duration { get; set; }
        public bool IsComplete => Time.time - StartTime >= Duration;

        public GoodwillChangeAnimation(Faction faction, int changeAmount, Vector2 startPosition, float duration = 1.5f)
        {
            TargetFaction = faction;
            ChangeAmount = changeAmount;
            StartPosition = startPosition;
            StartTime = Time.time;
            Duration = duration;
        }

        public float GetProgress()
        {
            return Mathf.Clamp01((Time.time - StartTime) / Duration);
        }

        public Vector2 GetCurrentPosition()
        {
            float progress = GetProgress();
            float floatDistance = 50f;
            return new Vector2(StartPosition.x, StartPosition.y - progress * floatDistance);
        }

        public float GetCurrentAlpha()
        {
            float progress = GetProgress();
            if (progress < 0.3f)
                return 1f;
            return Mathf.Lerp(1f, 0f, (progress - 0.3f) / 0.7f);
        }

        public Color GetColor()
        {
            if (ChangeAmount >= 0)
            {
                return new Color(0.3f, 0.9f, 0.4f);
            }
            else
            {
                return new Color(0.95f, 0.35f, 0.35f);
            }
        }

        /// <summary>/// getdisplaytext
 ///</summary>
        public string GetDisplayText()
        {
            return ChangeAmount >= 0 ? $"+{ChangeAmount}" : ChangeAmount.ToString();
        }
    }

    public static class GoodwillChangeAnimator
    {
        private static readonly List<GoodwillChangeAnimation> activeAnimations = new List<GoodwillChangeAnimation>();
        private static readonly Dictionary<Faction, int> lastKnownGoodwill = new Dictionary<Faction, int>();

        private const float ANIMATION_DURATION = 1.8f;
        private const float FLOAT_DISTANCE = 50f;
        private const float TEXT_SCALE = 1.2f;

        public static void CheckGoodwillChanges(List<Faction> factions)
        {
            if (factions == null) return;

            foreach (var faction in factions)
            {
                if (faction == null) continue;

                int currentGoodwill = faction.PlayerGoodwill;

                if (lastKnownGoodwill.TryGetValue(faction, out int lastGoodwill))
                {
                    int change = currentGoodwill - lastGoodwill;
                    if (change != 0)
                    {
                        TriggerGoodwillChangeEvent(faction, change);
                    }
                }

                lastKnownGoodwill[faction] = currentGoodwill;
            }

            var factionsToRemove = new List<Faction>();
            foreach (var recordedFaction in lastKnownGoodwill.Keys)
            {
                if (!factions.Contains(recordedFaction))
                {
                    factionsToRemove.Add(recordedFaction);
                }
            }
            foreach (var faction in factionsToRemove)
            {
                lastKnownGoodwill.Remove(faction);
            }
        }

        public static void TriggerGoodwillChangeEvent(Faction faction, int changeAmount)
        {
            if (faction == null || changeAmount == 0) return;

            OnGoodwillChanged?.Invoke(faction, changeAmount);
        }

        public static void CreateAnimation(Faction faction, int changeAmount, Vector2 screenPosition)
        {
            if (faction == null || changeAmount == 0) return;

            var animation = new GoodwillChangeAnimation(faction, changeAmount, screenPosition, ANIMATION_DURATION);
            activeAnimations.Add(animation);
        }

        public static void UpdateAndDrawAnimations()
        {
            activeAnimations.RemoveAll(a => a.IsComplete);

            foreach (var animation in activeAnimations)
            {
                DrawAnimation(animation);
            }
        }

        private static void DrawAnimation(GoodwillChangeAnimation animation)
        {
            Vector2 position = animation.GetCurrentPosition();
            float alpha = animation.GetCurrentAlpha();
            Color color = animation.GetColor();
            color.a = alpha;

            string text = animation.GetDisplayText();

            Text.Font = GameFont.Medium;
            Vector2 textSize = Text.CalcSize(text);

            Rect shadowRect = new Rect(position.x + 2f, position.y + 2f, textSize.x, textSize.y);
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.5f);
            Widgets.Label(shadowRect, text);

            Rect textRect = new Rect(position.x, position.y, textSize.x, textSize.y);
            GUI.color = color;
            Widgets.Label(textRect, text);

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        public static void ClearAll()
        {
            activeAnimations.Clear();
            lastKnownGoodwill.Clear();
        }

        public static event Action<Faction, int> OnGoodwillChanged;
    }
}
