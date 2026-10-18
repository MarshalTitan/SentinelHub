using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using SentinelHub.Configuration;
using SentinelHub.Identification;
namespace SentinelHub.Inspection;
public sealed class PlayerInspector(IObjectTable objects,IPlayerState own,IClientState client,IFramework framework,
    HubSettings settings,Func<bool> ready,InspectionGameData data)
{
    private IdentificationTicket? selected;
    private long next;
    public InspectorView View {get;private set;}=InspectorView.Empty;
    public void Select(IdentificationTicket ticket){Clear();selected=ticket;}
    public void Clear(){selected=null;View=InspectorView.Empty;next=0;}
    public void Tick(bool visible)
    {
        data.Tick(visible);
        if(!visible||!settings.PlayerInspectorEnabled||!ready()||!framework.IsInFrameworkUpdateThread){Clear();return;}
        if(selected is not {} t)return;
        var now=Environment.TickCount64;if(now<next)return;next=now+250;
        try{
            if(objects.SearchById(t.ObjectId) is not IPlayerCharacter player||!player.IsTargetable
                ||!InspectionPolicy.SessionMatches(t,player.GameObjectId,player.EntityId,player.ObjectIndex,own.ContentId,client.TerritoryType,now,ready(),player.IsValid())){Clear();return;}
            var actor=LiveOrnamentReader.Capture(player,framework);
            if(!actor.Valid||!actor.DrawLoaded||actor.Transformed){View=InspectorView.Empty;return;}
            var emote=LiveEmoteReader.Capture(player,framework);
            var mount=InspectionPolicy.Resolve(InspectionKind.Mount,player.CurrentMount?.RowId??0,data.Find);
            var fashion=InspectionPolicy.Resolve(InspectionKind.Fashion,actor.OrnamentId,data.Find);
            var current=objects.SearchById(t.ObjectId) as IPlayerCharacter;
            if(current is null||current.Address!=player.Address||current.EntityId!=t.EntityId||current.ObjectIndex!=t.Index||!current.IsValid()){Clear();return;}
            View=new(true,DateTimeOffset.UtcNow,mount,fashion,
                data.Failed?new(InspectionState.Unavailable,null,"Local inspection catalog unavailable."):
                !data.Loaded?new(InspectionState.Unavailable,null,"Loading local Emote/BuddyEquip data…"):
                InspectionPolicy.Emote(emote,id=>data.Find(InspectionKind.Emote,id),data.EmoteMode),InspectorView.Empty.Barding);
        }catch{Clear();}
    }
}
