using Dalamud.Hooking;
using Dalamud.Plugin.Services;

namespace SentinelHub.TreasureMaps;

// Observes only the map-display call; never suppresses or replaces the game's result.
// Both caller argument setup and callee argument reads must match the audited ABI.
public sealed unsafe class TreasureDetection(ISigScanner scanner, IGameInteropProvider interop,
    Action<ushort, ushort> observed) : IDisposable
{
    private delegate nint ShowMap(nint manager, ushort rank, ushort spot, byte mode);
    private Hook<ShowMap>? hook;
    private bool attempted;
    private volatile bool enabled;
    public string Status { get; private set; } = "Disabled";
    public void SuspendObservation() { enabled = false; attempted = false; }
    public void SetEnabled(bool value)
    {
        if (!value) { enabled = false; hook?.Disable(); attempted = false; Status = "Disabled"; return; }
        if (enabled || attempted) return;
        attempted = true;
        var failure = "map display signature";
        try
        {
            if (hook is null)
            {
                var sites = scanner.ScanAllText(TreasureNativeBinding.CallSite);
                if (sites.Length != 1) throw new InvalidOperationException();
                var site = sites[0];
                var start = scanner.Module.BaseAddress + (nint)scanner.TextSectionOffset;
                var end = start + scanner.TextSectionSize;
                failure = "map display call";
                if (site < start || site > end - TreasureNativeBinding.CallSiteLength
                    || !TreasureNativeBinding.TryGetTarget(new ReadOnlySpan<byte>((void*)(scanner.TextSectionBase + (site - start)), TreasureNativeBinding.CallSiteLength),
                        site, start, end, out var target)) throw new InvalidOperationException();
                failure = "map display handler";
                // Validate the scanner's pristine image, not another plugin's live detour bytes.
                if (!TreasureNativeBinding.IsTreasureHandler(new ReadOnlySpan<byte>((void*)(scanner.TextSectionBase + ((nint)target - start)), TreasureNativeBinding.HandlerLength)))
                    throw new InvalidOperationException();
                failure = "map display hook";
                hook = interop.HookFromAddress<ShowMap>((nint)target, OnShow);
            }
            failure = "map display hook";
            if (!hook.IsEnabled) hook.Enable(); enabled = true; Status = "Listening for treasure-map views";
        }
        catch { hook?.Dispose(); hook = null; Status = "Automatic detection unavailable (" + failure + "); chat queue still works. Toggle detection off/on to retry."; }
    }
    private nint OnShow(nint manager, ushort rank, ushort spot, byte mode)
    {
        return TreasureNativeBoundary.Forward(() => hook!.Original(manager, rank, spot, mode),
            () => { if (enabled && rank != 0) observed(rank, spot); });
    }
    public void Dispose() { enabled = false; hook?.Dispose(); hook = null; Status = "Disabled"; }
}
