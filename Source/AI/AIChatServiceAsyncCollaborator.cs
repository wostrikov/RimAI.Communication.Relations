using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Core.AI;
using Ustas.RimAI.Core.Net;
using Ustas.RimAI.Core.Threading;

namespace Ustas.RimAI.Communication.Relations.AI
{
    internal abstract class AIChatServiceAsyncCollaborator
    {
        internal readonly AIChatServiceAsync Owner;

        protected AIChatServiceAsyncCollaborator(AIChatServiceAsync owner)
        {
            Owner = owner;
        }

        protected AIChatServiceAsyncParts Parts => Owner.Parts;
        protected Coroutine StartCoroutine(System.Collections.IEnumerator routine) => Owner.StartCoroutine(routine);
        protected void EnsureCollaborators() => Owner.EnsureCollaborators();
        protected void ExecuteOnMainThread(Action action) => Owner.ExecuteOnMainThread(action);

        protected static AIChatServiceAsync _instance
        {
            get => AIChatServiceAsync._instance;
            set => AIChatServiceAsync._instance = value;
        }

        protected static readonly object _instanceLock = AIChatServiceAsync._instanceLock;
        protected const int LocalRequestTimeoutSeconds = 60;
        protected const int CloudRequestTimeoutSeconds = 60;
        protected const float RequestCleanupIntervalSeconds = 10f;

        protected RelationsAiRequestSession session => Owner.session;
        protected Queue<Action> mainThreadActions => Owner.mainThreadActions;
        protected DialogueTokenUsageTracker usageTracker
        {
            get => Owner.usageTracker;
            set => Owner.usageTracker = value;
        }
        protected RelationsAiDebugTelemetry telemetry
        {
            get => Owner.telemetry;
            set => Owner.telemetry = value;
        }
        protected float nextCleanupAtRealtime
        {
            get => Owner.nextCleanupAtRealtime;
            set => Owner.nextCleanupAtRealtime = value;
        }
        protected int contextVersion
        {
            get => Owner.contextVersion;
            set => Owner.contextVersion = value;
        }
        protected int lastObservedGameContextId
        {
            get => Owner.lastObservedGameContextId;
            set => Owner.lastObservedGameContextId = value;
        }
        protected object Gate => Owner.Gate;
    }
}
