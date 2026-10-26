using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using SentinelHub.Ecosystem;
namespace SentinelHub.Profiles;

public sealed class ProfilesIntegration : IDisposable
{
    private readonly Func<InstalledPlugin[]> inventory;
    private readonly Func<bool> available;
    private readonly ICallGateSubscriber<string> snapshot;
    private readonly ICallGateSubscriber<long,object?> changed;
    private readonly ICallGateSubscriber<string,bool> open;
    private readonly Action<long> onChanged;
    private volatile bool dirty=true;
    private bool wasVisible,disposed;
    private string? pendingOpen;
    private long retryAfter;
    private InstalledPlugin[]? previous;
    private bool loaded,installed;
    public ProfilesView View {get;private set;}=ProfilesView.Unavailable;
    public string? Notice {get;private set;}
    public ProfilesIntegration(IDalamudPluginInterface pi,Func<InstalledPlugin[]> inventory,Func<bool> available)
    {
        this.inventory=inventory;this.available=available;
        snapshot=pi.GetIpcSubscriber<string>("SentinelProfiles.Hub.v1.Snapshot");
        changed=pi.GetIpcSubscriber<long,object?>("SentinelProfiles.Hub.v1.Changed");
        open=pi.GetIpcSubscriber<string,bool>("SentinelProfiles.Hub.v1.Open");
        onChanged=_=>{dirty=true;System.Threading.Volatile.Write(ref retryAfter,0);};changed.Subscribe(onChanged);
    }
    public void Refresh(){dirty=true;retryAfter=0;}
    public void RequestOpen(string id){if(View.Availability==ProfilesAvailability.Ready)pendingOpen=id;}
    // Called only on the framework thread; UI and notification callbacks merely queue work.
    public void Tick(bool visible)
    {
        if(disposed)return;
        var all=inventory();
        if(!ReferenceEquals(previous,all)){
            previous=all;dirty=true;retryAfter=0;loaded=false;installed=false;
            foreach(var p in all)if(p.Id.Equals("SentinelProfiles",StringComparison.OrdinalIgnoreCase)){installed=true;loaded|=p.Loaded&&!p.Restricted;}
        }
        if(!available()||!loaded)
        {
            var state=!available()?ProfilesAvailability.Unavailable:installed?ProfilesAvailability.Disabled:ProfilesAvailability.Missing;
            if(View.Availability!=state)View=new(state,[],null,null,"Unavailable",false);
            pendingOpen=null;dirty=true;wasVisible=false;return;
        }
        if(visible&&!wasVisible){dirty=true;retryAfter=0;}
        wasVisible=visible;
        if((visible||pendingOpen is not null)&&dirty&&Environment.TickCount64>=retryAfter)
        {
            dirty=false;retryAfter=Environment.TickCount64+5000;
            try{View=ProfilesPolicy.Parse(snapshot.InvokeFunc());}catch{View=ProfilesView.Unavailable;}
            dirty=View.Availability!=ProfilesAvailability.Ready;
        }
        if(pendingOpen is {} id)
        {
            pendingOpen=null;
            try{Notice=View.Availability==ProfilesAvailability.Ready&&!View.Applying&&View.Profiles.Any(p=>p.Id==id)&&open.InvokeFunc(id)
                ?"Opened in Sentinel Profiles; nothing was applied.":"Profile could not be opened. Refresh Profiles or open its settings.";}
            catch{Notice="Sentinel Profiles is unavailable. Refresh or reopen its settings.";dirty=true;}
        }
    }
    public void Dispose(){if(disposed)return;disposed=true;pendingOpen=null;
        try{changed.Unsubscribe(onChanged);}catch{/* Provider teardown must not prevent utility/native cleanup. */}
        View=ProfilesView.Unavailable;
    }
}
