using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using SentinelHub.Configuration;
using SentinelHub.Diagnostics;
namespace SentinelHub.Mounts;

public sealed class MountContextMenu : IDisposable
{
    private readonly IContextMenu menu;
    private readonly IObjectTable objects;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly HubSettings settings;
    private readonly Func<bool> ready;
    private readonly Action<uint> open;
    private readonly FaultJournal faults;
    private bool disposed;
    public MountContextMenu(IContextMenu menu, IObjectTable objects, IClientState client, IPlayerState player,
        HubSettings settings, Func<bool> ready, Action<uint> open, FaultJournal faults)
    {
        this.menu = menu; this.objects = objects; this.client = client; this.player = player;
        this.settings = settings; this.ready = ready; this.open = open; this.faults = faults;
        menu.OnMenuOpened += OnOpened;
    }
    private void OnOpened(IMenuOpenedArgs args)
    {
        if (disposed || !settings.MountContextMenuEnabled) return;
        try
        {
            if (!ready() || args.MenuType != ContextMenuType.Default || args.Target is not MenuTargetDefault target
                || target.TargetObject is not IPlayerCharacter character || character.CurrentMount is not { } mount
                || mount.RowId == 0) return;
            // Capture only a short-lived lookup ticket. Never retain a character/pointer/name from this callback.
            var ticket = new MountLookupTicket(character.GameObjectId, player.ContentId, client.TerritoryType);
            args.AddMenuItem(new MenuItem { Name = "View mount in Sentinel Hub", PrefixChar = 'S', OnClicked = _ => OpenCurrent(ticket) });
        }
        catch { faults.Record(FaultCode.MountCollectionUnavailable); }
    }
    private void OpenCurrent(MountLookupTicket ticket)
    {
        if (disposed || !settings.MountContextMenuEnabled) return;
        try
        {
            if (!ready()) return;
            var current = objects.SearchById(ticket.ObjectId) as IPlayerCharacter;
            var mountId = current?.CurrentMount?.RowId ?? 0;
            if (MountLookupPolicy.CanOpen(ticket, current?.GameObjectId ?? 0, player.ContentId, client.TerritoryType,
                settings.MountContextMenuEnabled, ready(), current is not null, mountId, disposed)) open(mountId);
        }
        catch { faults.Record(FaultCode.MountCollectionUnavailable); }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; menu.OnMenuOpened -= OnOpened;
    }
}
