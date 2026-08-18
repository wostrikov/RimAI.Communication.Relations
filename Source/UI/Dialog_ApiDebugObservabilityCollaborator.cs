using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.Prompting;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.UI
{
        internal abstract class Dialog_ApiDebugObservabilityCollaborator
    {
        internal readonly Dialog_ApiDebugObservability Owner;

        protected Dialog_ApiDebugObservabilityCollaborator(Dialog_ApiDebugObservability owner)
        {
            Owner = owner;
        }

        protected Dialog_ApiDebugObservabilityParts Parts => Owner.Parts;

        protected void Close(bool doCloseSound = true) => Owner.Close(doCloseSound);
        protected bool absorbInputAroundWindow
        {
            get => Owner.absorbInputAroundWindow;
            set => Owner.absorbInputAroundWindow = value;
        }
        protected const float RefreshIntervalSeconds = 2f;
        protected const float HeaderHeight = 30f;
        protected const float SummaryHeight = 128f;
        protected const float TrendHeight = 180f;
        protected const float SectionGap = 8f;
        protected const float RowHeight = 24f;
        protected const float TrendStatsPanelWidth = 300f;
        protected const string DetailSearchHighlightColor = "#0539A3";
        protected static GUIStyle DetailTextStyle => Dialog_ApiDebugObservability.DetailTextStyle;
        protected static float[] TableColumnWeights => Dialog_ApiDebugObservability.TableColumnWeights;
        protected AIRequestDebugSnapshot snapshot
        {
            get => Owner.snapshot;
            set => Owner.snapshot = value;
        }
        protected float nextRefreshAtRealtime
        {
            get => Owner.nextRefreshAtRealtime;
            set => Owner.nextRefreshAtRealtime = value;
        }
        protected Vector2 detailScrollPosition
        {
            get => Owner.detailScrollPosition;
            set => Owner.detailScrollPosition = value;
        }
        protected string selectedRequestId
        {
            get => Owner.selectedRequestId;
            set => Owner.selectedRequestId = value;
        }
        protected Dialog_ApiDebugObservability.SourceFilterMode sourceFilter
        {
            get => Owner.sourceFilter;
            set => Owner.sourceFilter = value;
        }
        protected Dialog_ApiDebugObservability.StatusFilterMode statusFilter
        {
            get => Owner.statusFilter;
            set => Owner.statusFilter = value;
        }
        protected int currentPageIndex
        {
            get => Owner.currentPageIndex;
            set => Owner.currentPageIndex = value;
        }
        protected string detailSearchInput
        {
            get => Owner.detailSearchInput;
            set => Owner.detailSearchInput = value;
        }
        protected string detailSearchApplied
        {
            get => Owner.detailSearchApplied;
            set => Owner.detailSearchApplied = value;
        }
        protected float detailSearchApplyAtRealtime
        {
            get => Owner.detailSearchApplyAtRealtime;
            set => Owner.detailSearchApplyAtRealtime = value;
        }
        protected string detailCacheRequestId
        {
            get => Owner.detailCacheRequestId;
            set => Owner.detailCacheRequestId = value;
        }
        protected string detailCacheSearchQuery
        {
            get => Owner.detailCacheSearchQuery;
            set => Owner.detailCacheSearchQuery = value;
        }
        protected string detailCacheContent
        {
            get => Owner.detailCacheContent;
            set => Owner.detailCacheContent = value;
        }
    }

}
