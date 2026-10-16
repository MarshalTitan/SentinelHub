namespace SentinelHub.Utilities.Quest;

public readonly record struct QuestRewardCandidate(
    int Index,
    uint ItemId,
    uint Quantity,
    string Name,
    bool IsCurrentJobCompatible,
    int JobSpecificity,
    uint ItemLevel,
    bool IsAllaganPiece,
    uint VendorValue);

public readonly record struct QuestRewardDecision(
    int Index,
    uint ItemId,
    string Name,
    string Reason);

public static class QuestRewardSelectionPolicy
{
    public const string FirstRewardReason = "First Reward";
    public const string CurrentJobMatchReason = "Current Job Match";
    public const string NoJobMatchFallbackReason = "No Job Match -> First Reward";
    public const string AllaganPieceReason = "Allagan Piece";
    public const string NoAllaganPieceFallbackReason = "No Allagan Piece -> First Reward";

    public static QuestRewardDecision? Select(
        QuestRewardSelectionMode mode,
        IReadOnlyList<QuestRewardCandidate> candidates)
    {
        if (mode == QuestRewardSelectionMode.Manual || candidates.Count == 0)
            return null;

        var first = candidates.OrderBy(candidate => candidate.Index).First();
        return mode switch
        {
            QuestRewardSelectionMode.FirstReward => ToDecision(first, FirstRewardReason),
            QuestRewardSelectionMode.CurrentJobReward => SelectCurrentJob(candidates, first),
            QuestRewardSelectionMode.AllaganPiece => SelectAllaganPiece(candidates, first),
            _ => null,
        };
    }

    private static QuestRewardDecision SelectCurrentJob(
        IReadOnlyList<QuestRewardCandidate> candidates,
        QuestRewardCandidate fallback)
    {
        var matches = candidates
            .Where(candidate => candidate.IsCurrentJobCompatible)
            .OrderByDescending(candidate => candidate.JobSpecificity)
            .ThenByDescending(candidate => candidate.ItemLevel)
            .ThenBy(candidate => candidate.Index)
            .ToArray();
        return matches.Length > 0
            ? ToDecision(matches[0], CurrentJobMatchReason)
            : ToDecision(fallback, NoJobMatchFallbackReason);
    }

    private static QuestRewardDecision SelectAllaganPiece(
        IReadOnlyList<QuestRewardCandidate> candidates,
        QuestRewardCandidate fallback)
    {
        var matches = candidates
            .Where(candidate => candidate.IsAllaganPiece)
            .OrderByDescending(candidate => (ulong)candidate.VendorValue * Math.Max(1u, candidate.Quantity))
            .ThenBy(candidate => candidate.Index)
            .ToArray();
        return matches.Length > 0
            ? ToDecision(matches[0], AllaganPieceReason)
            : ToDecision(fallback, NoAllaganPieceFallbackReason);
    }

    private static QuestRewardDecision ToDecision(QuestRewardCandidate candidate, string reason)
        => new(candidate.Index, candidate.ItemId, candidate.Name, reason);
}
