using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using SentinelHub.Diagnostics;
namespace SentinelHub.Fashion;

public sealed class FashionController : IDisposable
{
    private readonly IDataManager data;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IObjectTable objects;
    private readonly ICondition condition;
    private readonly IUnlockState unlocks;
    private readonly FaultJournal faults;
    private readonly FashionObservation session = new();
    private FashionGameDataBuilder? builder;
    private IEnumerator<int>? loading;
    private FashionCatalog catalog = FashionCatalog.Empty;
    private FashionLoad state;
    private ulong lastSession;
    private uint lastTerritory;
    private bool requested, wasVisible, disposed;
    private long nextObservation;
    public FashionView View { get; private set; } = FashionView.Empty;
    public FashionController(IDataManager data, IUnlockState unlocks, IClientState client, IPlayerState player,
        IObjectTable objects, ICondition condition, FaultJournal faults)
    {
        this.data = data; this.client = client; this.player = player; this.objects = objects; this.condition = condition;
        this.faults = faults; this.unlocks=unlocks;
    }
    public bool Ready => !disposed && client.IsLoggedIn && player.IsLoaded && player.ContentId != 0
        && objects.LocalPlayer is not null
        && !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51];
    public void RequestRefresh() { if (!disposed) { requested = true; nextObservation = 0; } }
    public void Invalidate()
    {
        session.Clear();lastSession=0;nextObservation=0;
        View=new(state,catalog,session.Snapshot);
    }
    public void Retry()
    {
        if (disposed) return;
        loading?.Dispose(); loading = null; builder = null; catalog = FashionCatalog.Empty;
        state = FashionLoad.NotLoaded; session.Clear(); RequestRefresh();
        View = new(state, catalog, session.Snapshot);
    }
    public void Tick(bool visible)
    {
        if (disposed) return;
        if (!visible) { wasVisible=false; return; }
        try
        {
            var ready = Ready;
            var key = ready ? player.ContentId : 0;
            if (key != lastSession || client.TerritoryType != lastTerritory || !ready)
            { session.Clear(); nextObservation = 0; }
            lastSession = key; lastTerritory = client.TerritoryType;
            if (visible && !wasVisible) RequestRefresh();
            wasVisible = visible;
            if ((visible || requested) && state == FashionLoad.NotLoaded)
            { builder = new(data); loading = builder.Build(); state = FashionLoad.Loading; }
            if (loading is not null && !loading.MoveNext())
            {
                catalog = builder!.Result ?? throw new InvalidOperationException();
                if (catalog.Entries.Length == 0) throw new InvalidOperationException();
                loading.Dispose(); loading = null; builder = null; state = FashionLoad.Ready;
                nextObservation = 0;
            }
            var now = Environment.TickCount64;
            if (state == FashionLoad.Ready && ready && (visible || requested) && now >= nextObservation)
            {
                session.Observe(key, true, () => {
                    var sheet=data.GetExcelSheet<Lumina.Excel.Sheets.Ornament>();
                    var result=new Dictionary<uint,bool>();
                    foreach(var row in catalog.Entries)result.Add(row.Id,unlocks.IsOrnamentUnlocked(sheet.GetRow(row.Id)));
                    return new FashionCharacter(true,result);
                });
                nextObservation = now + 2000; requested = false;
                if (!session.Snapshot.Available) faults.Record(FaultCode.FashionCollectionUnavailable);
            }
            if(View.State!=state || !ReferenceEquals(View.Catalog,catalog) || !ReferenceEquals(View.Character,session.Snapshot))
                View = new(state, catalog, session.Snapshot);
        }
        catch
        {
            loading?.Dispose(); loading = null; builder = null; session.Clear();
            state = FashionLoad.Unavailable; View = new(state, catalog, session.Snapshot);
            faults.Record(FaultCode.FashionCollectionUnavailable);
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; loading?.Dispose(); loading = null; builder = null;
        session.Clear(); catalog = FashionCatalog.Empty; View = FashionView.Empty;
    }
}
