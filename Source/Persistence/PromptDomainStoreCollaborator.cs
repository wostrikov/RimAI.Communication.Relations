using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Module;
using Verse;
using Ustas.RimAI.Communication.Relations.Prompting.Transfer;
using Ustas.RimAI.Communication.Relations.Serialization;

namespace Ustas.RimAI.Communication.Relations.Persistence
{
        internal abstract class PromptDomainStoreCollaborator
    {
        internal readonly PromptDomainStore Owner;

        protected PromptDomainStoreCollaborator(PromptDomainStore owner)
        {
            Owner = owner;
        }

        protected PromptDomainStoreParts Parts => Owner.Parts;
        protected PromptPersistenceService host => Owner.host;
        protected const int CurrentPromptDomainSchemaVersion = 1;
        protected PromptConfigJsonCodec _configJsonCodec => Owner._configJsonCodec;
        protected SystemPromptConfig _cachedConfig
        {
            get => Owner._cachedConfig;
            set => Owner._cachedConfig = value;
        }
        protected DateTime _cachedConfigWriteTimeUtc
        {
            get => Owner._cachedConfigWriteTimeUtc;
            set => Owner._cachedConfigWriteTimeUtc = value;
        }
        protected bool _hasPendingPromptDomainRepairs
        {
            get => Owner._hasPendingPromptDomainRepairs;
            set => Owner._hasPendingPromptDomainRepairs = value;
        }
        protected object _typedParseWarningLock => Owner._typedParseWarningLock;
        protected HashSet<int> _typedParseIncompleteWarningHashes => Owner._typedParseIncompleteWarningHashes;
        protected HashSet<int> _typedParseFailureWarningHashes => Owner._typedParseFailureWarningHashes;
        protected HashSet<int> _typedParseRecoveredInfoHashes => Owner._typedParseRecoveredInfoHashes;
        protected static string[] CustomPromptDomainFiles => PromptDomainStore.CustomPromptDomainFiles;
        protected string ConfigFilePath => Owner.ConfigFilePath;
        protected string BasePath => Owner.BasePath;
    }

}
