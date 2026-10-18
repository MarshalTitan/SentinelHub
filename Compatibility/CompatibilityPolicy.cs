using SentinelHub.Utilities.Quest;
namespace SentinelHub.Compatibility;
public enum OverlapKind { Information, Possible, Definite }
public enum CompatibilityFeature { Latency, Dialogue, Cutscene, Rewards, AntiAfk }
public enum OverlapProvider { NoClippy, XivAlexander, TextAdvance, YesAlready, SimpleTweaks, PandorasBox, DailyRoutines }
public sealed record Overlap(OverlapProvider Provider,CompatibilityFeature Feature,OverlapKind Kind,bool SuspendHub,string Explanation);
public sealed record CompatibilityView(Overlap[] Findings)
{
    public static CompatibilityView Empty=>new([]);
    public bool Blocks(CompatibilityFeature feature)=>Findings.Any(x=>x.Feature==feature&&x.SuspendHub);
    public QuestConvenienceOptions Effective(QuestConvenienceOptions saved)=>new(){
        SkipDialogue=saved.SkipDialogue&&!Blocks(CompatibilityFeature.Dialogue),
        SkipCutscenes=saved.SkipCutscenes&&!Blocks(CompatibilityFeature.Cutscene),
        QuestRewardSelection=Blocks(CompatibilityFeature.Rewards)?QuestRewardSelectionMode.Manual:saved.QuestRewardSelection};
}
public static class CompatibilityPolicy
{
    public static Overlap[] TextAdvance(bool loaded,bool? enabled,bool? paused,bool? talk,bool? cutscene,bool? rewards)
    {
        if(!loaded)return [];
        if(enabled==false||paused==true)return [new(OverlapProvider.TextAdvance,CompatibilityFeature.Dialogue,OverlapKind.Information,false,"TextAdvance responds inactive/paused; no overlap inferred.")];
        var uncertain=enabled is null||paused is null;
        return new[]{(CompatibilityFeature.Dialogue,talk),(CompatibilityFeature.Cutscene,cutscene),(CompatibilityFeature.Rewards,rewards)}
            .Select(pair=>new Overlap(OverlapProvider.TextAdvance,pair.Item1,
                !uncertain&&pair.Item2==true?OverlapKind.Definite:uncertain||pair.Item2 is null?OverlapKind.Possible:OverlapKind.Information,
                uncertain||pair.Item2!=false,
                !uncertain&&pair.Item2==false?"TextAdvance reports this feature off; Hub may run.":
                uncertain||pair.Item2 is null?"TextAdvance is loaded but this feature's active state is unavailable. Hub suspends this feature conservatively.":
                "TextAdvance reports this feature enabled and not paused. Hub suspends its overlapping feature; saved preferences are retained.")).ToArray();
    }
}
