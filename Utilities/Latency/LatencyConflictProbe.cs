using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
namespace SentinelHub.Utilities.Latency;
internal sealed class LatencyConflictProbe(IDalamudPluginInterface pi,Func<SentinelHub.Ecosystem.InstalledPlugin[]>? inventory=null)
{
    private readonly object sync = new();
    private long next;
    private LatencyState cached = LatencyState.ConflictCheckUnavailable;
    private volatile bool noClippy;
    private long validUntil;
    public const int ScanIntervalMilliseconds=10_000;
    // No enumeration, managed discovery or IPC from native callbacks. Expired observations fail closed.
    public LatencyState ReadCached()
    {
        if(noClippy)return LatencyState.NoClippyConflict;
        try{
            foreach(var name in Names)if(GetModuleHandle(name)!=0)return LatencyState.XivAlexanderConflict;
            return Environment.TickCount64<=System.Threading.Volatile.Read(ref validUntil)?cached:LatencyState.ConflictCheckUnavailable;
        }catch{return LatencyState.ConflictCheckUnavailable;}
    }
    public LatencyState Read(bool force)
    {
        lock (sync)
        {
            try
            {
                // Any loaded copy matters here, including a dev copy beside disabled live.
                noClippy=inventory is not null?inventory().Any(p=>LatencyConflictPolicy.IsNoClippy(p.Id,p.Loaded)):
                    pi.InstalledPlugins.Any(p => LatencyConflictPolicy.IsNoClippy(p.InternalName, p.IsLoaded));
                if (noClippy)
                    return LatencyState.NoClippyConflict;
                foreach (var name in Names)
                    if (GetModuleHandle(name) != 0) return LatencyState.XivAlexanderConflict;
                var now = Environment.TickCount64;
                if (!force && now < next) return cached;
                next = now + ScanIntervalMilliseconds;
                System.Threading.Volatile.Write(ref validUntil,now+ScanIntervalMilliseconds+1000);
                using var process = Process.GetCurrentProcess();
                foreach (ProcessModule module in process.Modules)
                {
                    // Hold a brief loader reference while inspecting exports; never invoke them.
                    if (!GetModuleHandleEx(4, module.BaseAddress, out var handle)) continue;
                    try
                    {
                        if (LatencyConflictPolicy.IsAlexander(module.ModuleName,
                            GetProcAddress(handle, "XA_Initialize") != 0,
                            GetProcAddress(handle, "XA_InjectEntryPoint") != 0,
                            GetProcAddress(handle, "EnableXivAlexander") != 0))
                            return cached = LatencyState.XivAlexanderConflict;
                    }
                    finally { FreeLibrary(handle); }
                }
                return cached = LatencyState.Compensating; // Eligibility only; mode decides measure vs write.
            }
            catch { return cached = LatencyState.ConflictCheckUnavailable; }
        }
    }
    private static readonly string[] Names = ["XivAlexander64.dll", "XivAlexander32.dll", "XivAlexanderLoader64.dll", "XivAlexanderLoader32.dll"];
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string name);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetModuleHandleEx(uint flags, nint address, out nint module);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern nint GetProcAddress(nint module, string name);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(nint module);
}
