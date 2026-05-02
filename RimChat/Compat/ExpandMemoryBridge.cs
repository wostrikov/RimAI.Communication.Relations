using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimChat.Core;
using Verse;

namespace RimChat.Compat
{
    /// <summary>
    /// Dependencies: ExpandMemory mod (optional), HarmonyLib AccessTools.
    /// Responsibility: reflectively invoke ExpandMemory keyword matching for common knowledge injection.
    /// </summary>
    internal static class ExpandMemoryBridge
    {
        private static readonly object InitSyncRoot = new object();
        private static bool _initAttempted;
        private static bool _available;

        private static MethodInfo _getCommonKnowledgeMethod;
        private static MethodInfo _injectKnowledgeMethod;

        internal static bool IsAvailable()
        {
            if (!RimChatMod.Settings.IsExpandMemoryCompatEnabled())
            {
                return false;
            }

            if (_initAttempted)
            {
                return _available;
            }

            lock (InitSyncRoot)
            {
                if (_initAttempted)
                {
                    return _available;
                }

                _initAttempted = true;
                _available = TryInitializeReflection();
                if (!_available)
                {
                    Log.Message("[RimChat] ExpandMemory CommonKnowledge bridge not available.");
                }
            }

            return _available;
        }

        /// <summary>
        /// Invoke ExpandMemory's keyword matching: InjectKnowledgeWithDetails(matchText, maxEntries, out scores).
        /// Returns only knowledge entries whose tags match the provided context text.
        /// </summary>
        internal static string GetMatchedKnowledge(string matchText, int maxEntries = 10)
        {
            if (!IsAvailable() || string.IsNullOrWhiteSpace(matchText))
            {
                return string.Empty;
            }

            try
            {
                object library = _getCommonKnowledgeMethod.Invoke(null, null);
                if (library == null)
                {
                    return string.Empty;
                }

                object[] args = { matchText, maxEntries, null, null, null };
                try
                {
                    string result = _injectKnowledgeMethod.Invoke(library, args) as string;
                    return result ?? string.Empty;
                }
                catch (TargetInvocationException tie)
                {
                    Log.Warning($"[RimChat] ExpandMemory bridge: InjectKnowledgeWithDetails failed: {tie.InnerException?.Message ?? tie.Message}");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimChat] ExpandMemory bridge matching failed: {ex.Message}");
                return string.Empty;
            }
        }

        private static bool TryInitializeReflection()
        {
            Type memoryManagerType = AccessTools.TypeByName("RimTalk.Memory.MemoryManager");
            if (memoryManagerType == null)
            {
                Log.Message("[RimChat] ExpandMemory bridge: type MemoryManager not found.");
                return false;
            }

            _getCommonKnowledgeMethod = AccessTools.Method(memoryManagerType, "GetCommonKnowledge");
            if (_getCommonKnowledgeMethod == null)
            {
                Log.Message("[RimChat] ExpandMemory bridge: method GetCommonKnowledge not found.");
                return false;
            }

            Type libraryType = AccessTools.TypeByName("RimTalk.Memory.CommonKnowledgeLibrary");
            if (libraryType == null)
            {
                Log.Message("[RimChat] ExpandMemory bridge: type CommonKnowledgeLibrary not found.");
                return false;
            }

            _injectKnowledgeMethod = libraryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m =>
                    m.Name == "InjectKnowledgeWithDetails" &&
                    m.GetParameters().Length == 5 &&
                    m.GetParameters()[0].ParameterType == typeof(string) &&
                    m.GetParameters()[1].ParameterType == typeof(int));

            if (_injectKnowledgeMethod == null)
            {
                Log.Message("[RimChat] ExpandMemory bridge: method InjectKnowledgeWithDetails not found.");
                return false;
            }

            return true;
        }
    }
}
