using SentinelHub.Utilities.Quest;
using SentinelHub.Utilities;
namespace SentinelHub.Configuration;
public enum HubPage { Dashboard, Plugins, Profiles, Dependencies, Utilities, Diagnostics, Appearance, About, Mounts }
public class HubSettings
{
    public int Version { get; set; } = 1;
    public int Theme { get; set; } = 1;
    public HubPage Page { get; set; }
    public LatencyMode LatencyMode { get; set; } // Additive preference; new installs/upgrades stay Off.
    public bool AntiAfkEnabled { get; set; } // New installs and upgrades default OFF.
    public bool AntiAfkPreferenceImported { get; set; }
    public bool QuestConvenienceEnabled { get; set; }
    public bool QuestDialogue { get; set; }
    public bool QuestCutscenes { get; set; }
    public QuestRewardSelectionMode QuestRewardMode { get; set; }
    public bool QuestPreferenceImported { get; set; }
    public QuestConvenienceOptions QuestOptions() => new() { SkipDialogue = QuestDialogue, SkipCutscenes = QuestCutscenes, QuestRewardSelection = QuestRewardMode };
    public void ImportQuestPreferences(int packed)
    {
        if (!QuestPreferenceCodec.TryDecode(packed, out var options)) return;
        QuestDialogue = options.SkipDialogue; QuestCutscenes = options.SkipCutscenes;
        QuestRewardMode = options.QuestRewardSelection; QuestConvenienceEnabled = options.AnyEnabled;
        QuestPreferenceImported = true;
    }
    public bool MountContextMenuEnabled { get; set; } // Optional passive lookup; new installs/upgrades leave it off.
    public bool ReducedMotion { get; set; }
    public bool Collapsed { get; set; }
    public float ExpandedWidth { get; set; } = 860;
    public float ExpandedHeight { get; set; } = 680;
    public void Normalize()
    {
        Version = 1;
        if (!Enum.IsDefined(LatencyMode)) LatencyMode = SentinelHub.Utilities.LatencyMode.Off;
        if (!Enum.IsDefined(QuestRewardMode)) QuestRewardMode = QuestRewardSelectionMode.Manual;
        if (Theme is not (0 or 1)) Theme = 1;
        if (!Enum.IsDefined(Page)) Page = HubPage.Dashboard;
        if (!float.IsFinite(ExpandedWidth)) ExpandedWidth = 860;
        if (!float.IsFinite(ExpandedHeight)) ExpandedHeight = 680;
        ExpandedWidth = Math.Clamp(ExpandedWidth, 480, 4000);
        ExpandedHeight = Math.Clamp(ExpandedHeight, 500, 3000);
    }
}
