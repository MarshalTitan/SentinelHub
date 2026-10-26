using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using SentinelHub.Diagnostics;
namespace SentinelHub.Mounts;

public sealed class MountController : IDisposable
{
    private readonly IDataManager data;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IObjectTable objects;
    private readonly ICondition condition;
    private readonly MountCharacterReader reader;
    private readonly FaultJournal faults;
    private readonly MountObservationSession session = new();
    private MountGameDataBuilder? builder;
    private IEnumerator<int>? loading;
    private MountCatalog catalog = MountCatalog.Empty;
    private MountLoadState state;
    private ulong lastSession;
    private uint lastTerritory;
    private bool requested, wasVisible, disposed;
    private long nextObservation;
    public MountCollectionView View { get; private set; } = MountCollectionView.Empty;
    public MountController(IDataManager data, IUnlockState unlocks, IClientState client, IPlayerState player,
        IObjectTable objects, ICondition condition, FaultJournal faults)
    {
        this.data = data; this.client = client; this.player = player; this.objects = objects; this.condition = condition;
        this.faults = faults; reader = new(data, unlocks);
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
        loading?.Dispose(); loading = null; builder = null; catalog = MountCatalog.Empty;
        state = MountLoadState.NotLoaded; session.Clear(); RequestRefresh();
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
            if ((visible || requested) && state == MountLoadState.NotLoaded)
            { builder = new(data); loading = builder.Build(); state = MountLoadState.Loading; }
            if (loading is not null && !loading.MoveNext())
            {
                catalog = builder!.Result ?? throw new InvalidOperationException();
                if (catalog.Entries.Length == 0) throw new InvalidOperationException();
                loading.Dispose(); loading = null; builder = null; state = MountLoadState.Ready;
                nextObservation = 0;
            }
            var now = Environment.TickCount64;
            if (state == MountLoadState.Ready && ready && (visible || requested) && now >= nextObservation)
            {
                session.Observe(key, true, () => reader.Capture(catalog));
                nextObservation = now + 2000; requested = false;
                if (!session.Snapshot.Available) faults.Record(FaultCode.MountCollectionUnavailable);
            }
            if(View.State!=state || !ReferenceEquals(View.Catalog,catalog) || !ReferenceEquals(View.Character,session.Snapshot))
                View = new(state, catalog, session.Snapshot);
        }
        catch
        {
            loading?.Dispose(); loading = null; builder = null; session.Clear();
            state = MountLoadState.Unavailable; View = new(state, catalog, session.Snapshot);
            faults.Record(FaultCode.MountCollectionUnavailable);
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; loading?.Dispose(); loading = null; builder = null;
        session.Clear(); catalog = MountCatalog.Empty; View = MountCollectionView.Empty;
    }
}
