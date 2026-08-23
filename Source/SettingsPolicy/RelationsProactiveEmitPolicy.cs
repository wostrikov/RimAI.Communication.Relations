namespace Ustas.RimAI.Communication.Relations.Settings
{
    public enum RelationsProactiveKind
    {
        Skip,
        AmbientSocial,
        FriendlyDiplomacy
    }

    /// <summary>
    /// Authoritative proactive NPC/RPG emit gates: master toggle, hostile
    /// goodwill skip, causal goodwill-shift threshold, and cooldown/pending.
    /// </summary>
    public static class RelationsProactiveEmitPolicy
    {
        public const int HostileSkipGoodwill = -40;
        public const int FriendlyDiplomacyGoodwill = 40;
        public const int CausalMinAbsDelta = 10;

        public static bool AllowRegularSweep(bool enableNpcInitiatedDialogue) =>
            enableNpcInitiatedDialogue;

        public static RelationsProactiveKind ClassifyRegular(int playerGoodwill)
        {
            if (playerGoodwill <= HostileSkipGoodwill)
                return RelationsProactiveKind.Skip;
            if (playerGoodwill >= FriendlyDiplomacyGoodwill)
                return RelationsProactiveKind.FriendlyDiplomacy;
            return RelationsProactiveKind.AmbientSocial;
        }

        public static bool AllowCausalGoodwillShift(int goodwillDelta) =>
            goodwillDelta <= -CausalMinAbsDelta || goodwillDelta >= CausalMinAbsDelta;

        public static bool ShouldEmit(bool enabled, bool pending, bool cooldownBlocked, bool chancePassed) =>
            enabled && !pending && !cooldownBlocked && chancePassed;
    }
}
