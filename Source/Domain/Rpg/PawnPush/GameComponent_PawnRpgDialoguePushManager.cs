using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Module;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.PawnRpgPush
{
    /// <summary>
    /// Keeps the protagonist list that the diplomacy "Protagonist" negotiator mode picks from.
    ///
    /// This component used to run pawn-initiated RPG dialogue as well: it picked a pawn
    /// that wanted to talk to a colonist, or two colonists, generated a line and posted it
    /// as a letter. That push is gone; a pawn with something to say says it out loud
    /// through RimAI.Communication instead.
    ///
    /// The class keeps its name and namespace because every save lists it by full name
    /// among the game's components, and a component class that cannot be found is a load
    /// error. The push's own save fields (pawnRpgNpcPushStates, pawnRpgThreatStates,
    /// pawnRpgQueuedTriggers, the delivery ticks and the dedup maps) are not read: Scribe
    /// visits only the nodes it is asked for, so they are skipped on load and gone from the
    /// next save. Letters the push left on the letter stack are taken off it after a load.
    /// </summary>
    public class GameComponent_PawnRpgDialoguePushManager : GameComponent
    {
        public static GameComponent_PawnRpgDialoguePushManager Instance;

        List<PawnRpgProtagonistEntry> proactiveProtagonists = new List<PawnRpgProtagonistEntry>();
        List<Pawn> _cachedProtagonists;
        int _cachedProtagonistsTick = -1;

        public GameComponent_PawnRpgDialoguePushManager(Game game) : base()
        {
            Instance = this;
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            Instance = this;
            AutoSelectDefaultProtagonist();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            Instance = this;
            RemoveLeftoverPushLetters();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref proactiveProtagonists, "pawnRpgProactiveProtagonists", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                proactiveProtagonists ??= new List<PawnRpgProtagonistEntry>();
                proactiveProtagonists.RemoveAll(entry => entry == null || !entry.HasConfiguredIdentifier);
                _cachedProtagonists = null;
                AutoSelectDefaultProtagonist();
            }
        }

        public List<Pawn> GetProtagonists()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (_cachedProtagonists != null && _cachedProtagonistsTick == currentTick)
            {
                return _cachedProtagonists;
            }

            _cachedProtagonists = proactiveProtagonists == null
                ? new List<Pawn>()
                : proactiveProtagonists
                    .Select(entry => entry?.TryResolvePawn())
                    .Where(pawn => pawn != null)
                    .Distinct()
                    .ToList();
            _cachedProtagonistsTick = currentTick;
            return _cachedProtagonists;
        }

        public bool TryAddProtagonist(Pawn pawn)
        {
            if (pawn == null || pawn.Faction != Faction.OfPlayer || pawn.Destroyed || pawn.Dead)
            {
                return false;
            }

            if (GetProtagonists().Contains(pawn))
            {
                return true;
            }

            if (GetConfiguredProtagonistCount() >= GetProtagonistCap())
            {
                return false;
            }

            proactiveProtagonists.Add(PawnRpgProtagonistEntry.FromPawn(pawn));
            _cachedProtagonists = null;
            return true;
        }

        public bool RemoveProtagonist(Pawn pawn)
        {
            if (pawn == null || proactiveProtagonists == null || proactiveProtagonists.Count == 0)
            {
                return false;
            }

            int before = proactiveProtagonists.Count;
            proactiveProtagonists.RemoveAll(entry => entry != null && entry.Matches(pawn));
            _cachedProtagonists = null;
            return proactiveProtagonists.Count < before;
        }

        public void ClearProtagonists()
        {
            proactiveProtagonists.Clear();
            _cachedProtagonists = null;
        }

        public int GetConfiguredProtagonistCount()
        {
            return proactiveProtagonists?.Count(entry => entry?.HasConfiguredIdentifier == true) ?? 0;
        }

        public int GetProtagonistCap()
        {
            return Mathf.Clamp(RelationsMod.Instance?.InstanceSettings?.PawnRpgProtagonistCap ?? 20, 1, 100);
        }

        public void SetProtagonistCap(int value)
        {
            RelationsSettings settings = RelationsMod.Instance?.InstanceSettings;
            if (settings != null)
            {
                settings.PawnRpgProtagonistCap = Mathf.Clamp(value, 1, 100);
            }
        }

        /// <summary>
        /// Name the colonist with the highest total skills when nobody is configured, so the
        /// Protagonist negotiator mode has someone to pick from in a game nobody has set up.
        /// An entry that lost its pawn keeps the list non-empty and still names no one, which
        /// is why the check is on configured identifiers rather than on the list's length.
        /// </summary>
        void AutoSelectDefaultProtagonist()
        {
            proactiveProtagonists ??= new List<PawnRpgProtagonistEntry>();
            if (GetConfiguredProtagonistCount() > 0)
            {
                return;
            }

            proactiveProtagonists.RemoveAll(entry => entry == null || !entry.HasConfiguredIdentifier);
            Pawn best = FindBestSkillColonist();
            if (best == null)
            {
                return;
            }

            proactiveProtagonists.Add(PawnRpgProtagonistEntry.FromPawn(best));
            _cachedProtagonists = null;
            ModuleLog.Message($"[RimAI.Relations] Auto-selected default protagonist: {best.LabelShortCap} (highest skills)");
        }

        static Pawn FindBestSkillColonist()
        {
            Pawn best = null;
            int bestScore = -1;
            foreach (Map map in Find.Maps)
            {
                if (map == null) continue;
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn?.skills == null || pawn.Dead || pawn.Destroyed || pawn.IsPrisoner || pawn.Faction != Faction.OfPlayer) continue;
                    int score = pawn.skills.skills.Sum(skill => skill.Level);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = pawn;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Take the removed push's letters off the letter stack, so a save made while one was
        /// waiting on the right of the screen does not keep showing it. The archive keeps its
        /// copy, which still opens and offers only Close.
        /// </summary>
        static void RemoveLeftoverPushLetters()
        {
            LetterStack stack = Find.LetterStack;
            if (stack?.LettersListForReading == null)
            {
                return;
            }

            List<Letter> leftovers = stack.LettersListForReading
                .Where(letter => letter is ChoiceLetter_PawnRpgInitiatedDialogue)
                .ToList();
            foreach (Letter letter in leftovers)
            {
                stack.RemoveLetter(letter);
            }

            // Says which mechanism took them off, for a load that shows them gone.
            if (leftovers.Count > 0)
            {
                ModuleLog.Message($"[RimAI.Relations] Removed {leftovers.Count} leftover pawn-push letter(s) from the letter stack on load.");
            }
        }
    }
}
