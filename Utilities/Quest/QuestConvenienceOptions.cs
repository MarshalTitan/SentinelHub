namespace SentinelHub.Utilities.Quest;
public enum QuestRewardSelectionMode { Manual, FirstReward, CurrentJobReward, AllaganPiece }
public sealed class QuestConvenienceOptions
{
    public bool SkipDialogue { get; set; }
    public bool SkipCutscenes { get; set; }
    public QuestRewardSelectionMode QuestRewardSelection { get; set; }
    public bool AnyEnabled => SkipDialogue || SkipCutscenes || QuestRewardSelection != QuestRewardSelectionMode.Manual;
}
public static class QuestPreferenceCodec
{
    public static int Encode(QuestConvenienceOptions options) =>
        (options.SkipDialogue ? 1 : 0) | (options.SkipCutscenes ? 2 : 0) |
        ((Enum.IsDefined(options.QuestRewardSelection) ? (int)options.QuestRewardSelection : 0) << 2);
    public static bool TryDecode(int packed, out QuestConvenienceOptions options)
    {
        options = new();
        if (packed is < 0 or > 15) return false;
        options.SkipDialogue = (packed & 1) != 0;
        options.SkipCutscenes = (packed & 2) != 0;
        options.QuestRewardSelection = (QuestRewardSelectionMode)(packed >> 2);
        return true;
    }
}
