using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Relation;
using Ustas.RimAI.Communication.Relations.Config;

namespace Ustas.RimAI.Communication.Relations.UI
{
        internal abstract class MainTabWindow_RelationsCollaborator
    {
        internal readonly MainTabWindow_Relations Owner;

        protected MainTabWindow_RelationsCollaborator(MainTabWindow_Relations owner)
        {
            Owner = owner;
        }

        protected MainTabWindow_RelationsParts Parts => Owner.Parts;

        protected void Close(bool doCloseSound = true) => Owner.Close(doCloseSound);
        protected bool absorbInputAroundWindow
        {
            get => Owner.absorbInputAroundWindow;
            set => Owner.absorbInputAroundWindow = value;
        }
        protected Vector2 factionListScrollPosition
        {
            get => Owner.factionListScrollPosition;
            set => Owner.factionListScrollPosition = value;
        }
        protected Vector2 detailScrollPosition
        {
            get => Owner.detailScrollPosition;
            set => Owner.detailScrollPosition = value;
        }
        protected Faction selectedFaction
        {
            get => Owner.selectedFaction;
            set => Owner.selectedFaction = value;
        }
        protected List<Faction> allFactions
        {
            get => Owner.allFactions;
            set => Owner.allFactions = value;
        }
        protected static Color BackgroundColor => MainTabWindow_Relations.BackgroundColor;
        protected static Color PanelColor => MainTabWindow_Relations.PanelColor;
        protected static Color HeaderColor => MainTabWindow_Relations.HeaderColor;
        protected static Color AccentColor => MainTabWindow_Relations.AccentColor;
        protected static Color TextPrimary => MainTabWindow_Relations.TextPrimary;
        protected static Color TextSecondary => MainTabWindow_Relations.TextSecondary;
        protected static Color BorderColor => MainTabWindow_Relations.BorderColor;
        protected Dictionary<Faction, Rect> factionRowRects => Owner.factionRowRects;
        protected bool goodwillEventSubscribed
        {
            get => Owner.goodwillEventSubscribed;
            set => Owner.goodwillEventSubscribed = value;
        }
    }

}
