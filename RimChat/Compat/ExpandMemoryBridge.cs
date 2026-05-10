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
    /// Responsibility: reflectively invoke ExpandMemory APIs for common knowledge and per-pawn memory injection.
    /// </summary>
    internal static class ExpandMemoryBridge
    {
        private static readonly object InitSyncRoot = new object();
        private static bool _initAttempted;
        private static bool _available;

        private static MethodInfo _getCommonKnowledgeMethod;
        private static MethodInfo _injectKnowledgeMethod;
        private static Type _fourLayerMemoryCompType;
        private static PropertyInfo _activeMemoriesProp;
        private static PropertyInfo _eventLogMemoriesProp;
        private static PropertyInfo _archiveMemoriesProp;
        private static MethodInfo _memoryFormatterFormatMethod;

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
                    Log.Message("[RimChat] ExpandMemory bridge not available.");
                }
            }

            return _available;
        }

        internal static bool IsPawnMemoryAvailable()
        {
            if (!RimChatMod.Settings.IsExpandMemoryPawnMemoryEnabled())
            {
                return false;
            }

            return IsAvailable();
        }

        internal static string GetPawnMemory(Pawn pawn, int maxEntriesPerLayer = 20, int maxTotalEntries = 50)
        {
            if (!IsPawnMemoryAvailable() || pawn == null || _fourLayerMemoryCompType == null)
            {
                return string.Empty;
            }

            try
            {
                ThingComp comp = pawn.AllComps?.FirstOrDefault(c => c != null && _fourLayerMemoryCompType.IsInstanceOfType(c));
                if (comp == null)
                {
                    return string.Empty;
                }

                // Collect raw List<MemoryEntry> from each layer, keeping only the most recent entries per layer.
                // Active > EventLog > Archive priority order.
                var activeList = ReadLayerTail(_activeMemoriesProp, comp, maxEntriesPerLayer);
                var eventList = ReadLayerTail(_eventLogMemoriesProp, comp, maxEntriesPerLayer);
                var archiveList = ReadLayerTail(_archiveMemoriesProp, comp, maxEntriesPerLayer);

                // Merge in priority order: Active + EventLog first, then Archive.
                object combinedList = null;
                if (activeList != null) combinedList = activeList;
                if (eventList != null) combinedList = combinedList == null ? eventList : MergeMemoryLists(combinedList, eventList);
                if (archiveList != null) combinedList = combinedList == null ? archiveList : MergeMemoryLists(combinedList, archiveList);

                if (combinedList == null)
                {
                    return string.Empty;
                }

                // Enforce total entry cap — trim oldest entries from the front.
                combinedList = TrimListHead(combinedList, maxTotalEntries);

                // Try MemoryFormatter.Format(List<MemoryEntry>, int) — the runtime type is List<MemoryEntry>
                if (_memoryFormatterFormatMethod != null)
                {
                    try
                    {
                        string formatted = _memoryFormatterFormatMethod.Invoke(null, new[] { combinedList, 1 }) as string;
                        if (!string.IsNullOrWhiteSpace(formatted))
                        {
                            return formatted.Trim();
                        }
                    }
                    catch (TargetInvocationException tie)
                    {
                        Log.Warning($"[RimChat] MemoryFormatter.Format failed: {tie.InnerException?.Message ?? tie.Message}");
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimChat] ExpandMemory bridge GetPawnMemory failed: {ex.Message}");
                return string.Empty;
            }
        }

        private static object ReadLayerTail(PropertyInfo prop, ThingComp comp, int maxEntries)
        {
            if (prop == null || maxEntries <= 0) return null;
            try
            {
                object value = prop.GetValue(comp);
                if (!(value is System.Collections.IList list) || list.Count == 0) return null;

                int count = list.Count;
                int take = count <= maxEntries ? count : maxEntries;
                int skip = count - take;

                Type entryType = list.GetType().GetGenericArguments()[0];
                Type listType = typeof(List<>).MakeGenericType(entryType);
                var result = (System.Collections.IList)Activator.CreateInstance(listType);
                for (int i = skip; i < count; i++) result.Add(list[i]);
                return result;
            }
            catch
            {
                return null;
            }
        }

        private static object TrimListHead(object listObj, int maxEntries)
        {
            if (listObj == null || maxEntries <= 0) return listObj;
            try
            {
                var list = (System.Collections.IList)listObj;
                if (list.Count <= maxEntries) return listObj;

                int trim = list.Count - maxEntries;
                Type entryType = list.GetType().GetGenericArguments()[0];
                Type listType = typeof(List<>).MakeGenericType(entryType);
                var trimmed = (System.Collections.IList)Activator.CreateInstance(listType);
                for (int i = trim; i < list.Count; i++) trimmed.Add(list[i]);
                return trimmed;
            }
            catch
            {
                return listObj;
            }
        }

        private static object MergeMemoryLists(object listA, object listB)
        {
            try
            {
                if (listA == null) return listB;
                if (listB == null) return listA;
                // Both are List<MemoryEntry>. Create a new list and add range via reflection.
                Type entryType = listA.GetType().GetGenericArguments()[0];
                Type listType = typeof(List<>).MakeGenericType(entryType);
                var merged = (System.Collections.IList)Activator.CreateInstance(listType);
                foreach (object item in (System.Collections.IList)listA) merged.Add(item);
                foreach (object item in (System.Collections.IList)listB) merged.Add(item);
                return merged;
            }
            catch
            {
                return listA ?? listB;
            }
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

            _fourLayerMemoryCompType = AccessTools.TypeByName("RimTalk.Memory.FourLayerMemoryComp");
            if (_fourLayerMemoryCompType != null)
            {
                _activeMemoriesProp = _fourLayerMemoryCompType.GetProperty("ActiveMemories", BindingFlags.Public | BindingFlags.Instance);
                _eventLogMemoriesProp = _fourLayerMemoryCompType.GetProperty("EventLogMemories", BindingFlags.Public | BindingFlags.Instance);
                _archiveMemoriesProp = _fourLayerMemoryCompType.GetProperty("ArchiveMemories", BindingFlags.Public | BindingFlags.Instance);
            }

            Type formatterType = AccessTools.TypeByName("RimTalk.Memory.Injection.MemoryFormatter");
            if (formatterType != null)
            {
                _memoryFormatterFormatMethod = formatterType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m =>
                        m.Name == "Format" &&
                        m.GetParameters().Length == 2 &&
                        m.GetParameters()[1].ParameterType == typeof(int));
            }

            if (_fourLayerMemoryCompType == null)
            {
                Log.Message("[RimChat] ExpandMemory bridge: FourLayerMemoryComp not found (per-pawn memory disabled).");
            }

            return true;
        }
    }
}
