using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace SentinelHub.Utilities.Quest;

internal sealed unsafe class QuestActivityMonitor : IDisposable
{
    private static readonly string[] Names=["Talk","JournalResult","SelectString","CutSceneSelectString"];
    private static readonly AddonEvent[] Events=[AddonEvent.PostSetup,AddonEvent.PostRefresh,AddonEvent.PreFinalize];
    private readonly IAddonLifecycle? lifecycle;
    private readonly IGameGui gui;
    private readonly ICondition condition;
    private readonly QuestActivityGate gate=new();
    private readonly IAddonLifecycle.AddonEventDelegate wake;
    public QuestActivityMonitor(IGameGui gui,ICondition condition,IAddonLifecycle? lifecycle)
    {
        this.gui=gui;this.condition=condition;this.lifecycle=lifecycle;wake=OnAddon;
        if(lifecycle is not null)foreach(var e in Events)lifecycle.RegisterListener(e,Names,wake);
        condition.ConditionChange+=OnCondition;
    }
    private void OnAddon(AddonEvent _,AddonArgs __)=>gate.Wake();
    private void OnCondition(ConditionFlag flag,bool _)
    {
        if(flag is ConditionFlag.OccupiedInCutSceneEvent or ConditionFlag.WatchingCutscene or ConditionFlag.WatchingCutscene78)gate.Wake();
    }
    public void Reset()=>gate.Reset();
    public bool ShouldProcess(QuestConvenienceOptions options,bool pending)=>ShouldProcess(options,pending,Environment.TickCount64);
    internal bool ShouldProcess(QuestConvenienceOptions options,bool pending,long now)
    {
        if(gate.ShouldProbe(now))
        {
            var active=options.SkipDialogue&&Visible("Talk")
                ||options.QuestRewardSelection!=QuestRewardSelectionMode.Manual&&Visible("JournalResult")
                ||options.SkipCutscenes&&(condition[ConditionFlag.OccupiedInCutSceneEvent]||condition[ConditionFlag.WatchingCutscene]
                    ||condition[ConditionFlag.WatchingCutscene78]||Visible("SelectString")||Visible("CutSceneSelectString"));
            gate.Observed(active,now);
        }
        return gate.ShouldProcess(pending);
    }
    private bool Visible(string name)
    {
        try{var addon=gui.GetAddonByName<AtkUnitBase>(name);return addon is not null&&addon->IsVisible;}
        catch{return false;}
    }
    public void Dispose()
    {
        condition.ConditionChange-=OnCondition;
        if(lifecycle is not null)foreach(var e in Events)lifecycle.UnregisterListener(e,Names,wake);
    }
}
