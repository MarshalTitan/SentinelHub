using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;

namespace SentinelHub.TreasureMaps;

// Observes only the map-display call; never suppresses or replaces the game's result.
// Call-site anchored to the current SDK's Client.Game.UI.Map singleton signature.
public sealed unsafe class TreasureDetection(ISigScanner scanner, IGameInteropProvider interop,
    Action<ushort, ushort> observed) : IDisposable
{
    internal const string CallSite = "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? 40 84 F6";
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
        try
        {
            if (hook is null)
            {
                var sites = scanner.ScanAllText(CallSite);
                if (sites.Length != 1) throw new InvalidOperationException();
                var site = sites[0];
                var start = scanner.Module.BaseAddress + (nint)scanner.TextSectionOffset;
                var end = start + scanner.TextSectionSize;
                if (site < start || site + 23 > end || Marshal.ReadByte(site + 15) != 0xE8) throw new InvalidOperationException();
                var target = site + 20 + Marshal.ReadInt32(site + 16);
                if (target < start || target >= end) throw new InvalidOperationException();
                hook = interop.HookFromAddress<ShowMap>(target, OnShow);
            }
            if (!hook.IsEnabled) hook.Enable(); enabled = true; Status = "Listening for treasure-map views";
        }
        catch { hook?.Dispose(); hook = null; Status = "Automatic detection unavailable; chat queue still works. Toggle detection off/on to retry."; }
    }
    private nint OnShow(nint manager, ushort rank, ushort spot, byte mode)
    {
        return TreasureNativeBoundary.Forward(() => hook!.Original(manager, rank, spot, mode),
            () => { if (enabled && rank != 0) observed(rank, spot); });
    }
    public void Dispose() { enabled = false; hook?.Dispose(); hook = null; Status = "Disabled"; }
}
