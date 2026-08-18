using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.Diagnostics;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.WorldState;

namespace Ustas.RimAI.Communication.Relations.DiplomacySystem
{
        internal abstract class GameComponent_RPGManagerCollaborator
    {
        internal readonly GameComponent_RPGManager Owner;

        protected GameComponent_RPGManagerCollaborator(GameComponent_RPGManager owner)
        {
            Owner = owner;
        }
        protected GameComponent_RPGManagerParts Parts => Owner.Parts;


        protected Dictionary<string, int> pawnDialogueCooldownUntilTickById
        {
            get => Owner.pawnDialogueCooldownUntilTickById;
            set => Owner.pawnDialogueCooldownUntilTickById = value;
        }
        protected List<string> cooldownKeysByIdWorkingList
        {
            get => Owner.cooldownKeysByIdWorkingList;
            set => Owner.cooldownKeysByIdWorkingList = value;
        }
        protected List<int> cooldownValuesByIdWorkingList
        {
            get => Owner.cooldownValuesByIdWorkingList;
            set => Owner.cooldownValuesByIdWorkingList = value;
        }
        protected Dictionary<string, string> pawnPersonaPromptsById
        {
            get => Owner.pawnPersonaPromptsById;
            set => Owner.pawnPersonaPromptsById = value;
        }
        protected List<string> pawnPersonaPromptKeysByIdWorkingList
        {
            get => Owner.pawnPersonaPromptKeysByIdWorkingList;
            set => Owner.pawnPersonaPromptKeysByIdWorkingList = value;
        }
        protected List<string> pawnPersonaPromptValuesByIdWorkingList
        {
            get => Owner.pawnPersonaPromptValuesByIdWorkingList;
            set => Owner.pawnPersonaPromptValuesByIdWorkingList = value;
        }
        protected Dictionary<Pawn, int> legacyPawnDialogueCooldownUntilTick
        {
            get => Owner.legacyPawnDialogueCooldownUntilTick;
            set => Owner.legacyPawnDialogueCooldownUntilTick = value;
        }
        protected List<Pawn> legacyCooldownKeysWorkingList
        {
            get => Owner.legacyCooldownKeysWorkingList;
            set => Owner.legacyCooldownKeysWorkingList = value;
        }
        protected List<int> legacyCooldownValuesWorkingList
        {
            get => Owner.legacyCooldownValuesWorkingList;
            set => Owner.legacyCooldownValuesWorkingList = value;
        }
        protected Dictionary<Pawn, string> legacyPawnPersonaPrompts
        {
            get => Owner.legacyPawnPersonaPrompts;
            set => Owner.legacyPawnPersonaPrompts = value;
        }
        protected List<Pawn> legacyPawnPersonaPromptKeysWorkingList
        {
            get => Owner.legacyPawnPersonaPromptKeysWorkingList;
            set => Owner.legacyPawnPersonaPromptKeysWorkingList = value;
        }
        protected List<string> legacyPawnPersonaPromptValuesWorkingList
        {
            get => Owner.legacyPawnPersonaPromptValuesWorkingList;
            set => Owner.legacyPawnPersonaPromptValuesWorkingList = value;
        }
        protected HashSet<int> pawnPersonaSyncGuards => Owner.pawnPersonaSyncGuards;
        protected string persistentRpgSaveSlotId
        {
            get => Owner.persistentRpgSaveSlotId;
            set => Owner.persistentRpgSaveSlotId = value;
        }
        protected const float DefaultExitCooldownHours = 2f;
        protected const string PersistentRpgSaveSlotPrefix = "slot";
    }

}
