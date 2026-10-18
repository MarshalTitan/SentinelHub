using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using SentinelHub.Configuration;
using SentinelHub.Diagnostics;
using SentinelHub.Fashion;
namespace SentinelHub.Identification;
public sealed class PlayerIdentificationMenu : IDisposable
{
    private readonly IContextMenu menu;
    private readonly IObjectTable objects;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IFramework framework;
    private readonly IDataManager data;
    private readonly HubSettings settings;
    private readonly Func<bool> ready;
    private readonly Action<PlayerIdentification> open;
    private readonly Action clear;
    private readonly FaultJournal faults;
    private readonly Action<IdentificationTicket>? inspect;
    private readonly IdentificationQueue queue=new();
    private ulong session;
    private uint territory;
    private bool disposed;
    public PlayerIdentificationMenu(IContextMenu menu,IObjectTable objects,IClientState client,IPlayerState player,IFramework framework,
        IDataManager data,HubSettings settings,Func<bool> ready,Action<PlayerIdentification> open,Action clear,FaultJournal faults,Action<IdentificationTicket>? inspect=null)
    {
        this.menu=menu;this.objects=objects;this.client=client;this.player=player;this.framework=framework;this.data=data;
        this.settings=settings;this.ready=ready;this.open=open;this.clear=clear;this.faults=faults;this.inspect=inspect;
        menu.OnMenuOpened+=OnOpened;
    }
    private PlayerIdentification Unavailable(long now)=>new(
        new(settings.MountContextMenuEnabled?IdentificationState.Unavailable:IdentificationState.Off),
        new(settings.FashionContextMenuEnabled?IdentificationState.Unavailable:IdentificationState.Off),now);
    private bool Enabled=>settings.MountContextMenuEnabled||settings.FashionContextMenuEnabled||settings.PlayerInspectorEnabled;
    private void OnOpened(IMenuOpenedArgs args)
    {
        if(disposed||!Enabled)return;
        try{
            if(!ready()||args.MenuType!=ContextMenuType.Default||args.Target is not MenuTargetDefault target
                ||target.TargetObject is not IPlayerCharacter pc||!pc.IsValid()||!pc.IsTargetable||pc.Address==0
                ||pc.EntityId is 0 or 0xE0000000||pc.GameObjectId==objects.LocalPlayer?.GameObjectId)return;
            // Preserve Mount-only behavior on unmounted players. Fashion opt-in adds the explicit no-accessory result.
            if(!settings.PlayerInspectorEnabled&&!IdentificationPolicy.Offer(settings.MountContextMenuEnabled,settings.FashionContextMenuEnabled,
                settings.MountContextMenuEnabled&&(pc.CurrentMount?.RowId??0)!=0))return;
            var ticket=new IdentificationTicket(pc.GameObjectId,pc.EntityId,pc.ObjectIndex,player.ContentId,client.TerritoryType,Environment.TickCount64);
            args.AddMenuItem(new MenuItem{Name="Sentinel Hub: Inspect player",PrefixChar='S',
                OnClicked=_=>{if(!disposed&&Enabled)queue.Request(ticket);}});
        }catch{faults.Record(FaultCode.PlayerIdentificationUnavailable);}
    }
    public void Tick()
    {
        if(disposed)return;
        if(!ready()||!Enabled||player.ContentId!=session||client.TerritoryType!=territory)
        {queue.Clear();clear();session=player.ContentId;territory=client.TerritoryType;return;}
        var request=queue.Take();if(request is not {} ticket)return;
        try{
            if(!framework.IsInFrameworkUpdateThread){clear();return;}
            var now=Environment.TickCount64;
            var pc=objects.SearchById(ticket.ObjectId) as IPlayerCharacter;
            if(!IdentificationPolicy.TicketMatches(ticket,pc?.GameObjectId??0,pc?.EntityId??0,pc?.ObjectIndex??0,player.ContentId,
                client.TerritoryType,now,Enabled,ready(),pc is not null,pc?.IsValid()==true,disposed)||pc is null||!pc.IsTargetable)
            {open(Unavailable(now));return;}
            if(settings.PlayerInspectorEnabled&&inspect is not null){inspect(ticket);return;}
            var mountId=settings.MountContextMenuEnabled?pc.CurrentMount?.RowId??0:0;
            var mount=new IdentificationPart(!settings.MountContextMenuEnabled?IdentificationState.Off:mountId==0?IdentificationState.None:IdentificationState.Found,mountId);
            var observation=settings.FashionContextMenuEnabled?LiveOrnamentReader.Capture(pc,framework):default;
            var current=objects.SearchById(ticket.ObjectId) as IPlayerCharacter;
            if(current is null||current.Address!=pc.Address||!current.IsValid()||current.EntityId!=ticket.EntityId
                ||current.ObjectIndex!=ticket.Index){open(Unavailable(now));return;}
            var fashion=IdentificationPolicy.Resolve(observation,settings.FashionContextMenuEnabled,id=>{
                var row=data.GetExcelSheet<Lumina.Excel.Sheets.Ornament>().GetRowOrDefault(id);
                return row is {} r&&FashionPolicy.Candidate(r.RowId,r.Singular.ToString(),r.Icon,r.Model,r.Order);
            });
            open(new(mount,fashion,now));
        }catch{clear();faults.Record(FaultCode.PlayerIdentificationUnavailable);}
    }
    public void Dispose(){if(disposed)return;disposed=true;queue.Clear();clear();menu.OnMenuOpened-=OnOpened;}
}

