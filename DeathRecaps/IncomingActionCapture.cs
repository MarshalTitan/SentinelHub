using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
namespace SentinelHub.DeathRecaps;

internal readonly record struct IncomingAction(DateTimeOffset At, uint Source, uint Target, uint Action,
    uint Sequence, byte Slot, RecapEventKind Kind, uint Amount);

// Optional native evidence. The callback copies primitives only; all game-service work stays on Framework.
internal sealed unsafe class IncomingActionCapture(IGameInteropProvider interop, ISigScanner scanner) : IDisposable
{
    private readonly object sync = new();
    private readonly IncomingAction[] ring = new IncomingAction[1024];
    private readonly uint[] watched = new uint[8];
    private int head, count, watchedCount;
    private Hook<ActionEffectHandler.Delegates.Receive>? hook;
    private bool attempted;
    public int Dropped { get; private set; }
    public string Availability { get; private set; } = "Detailed incoming action capture is off.";
    public void Watch(ReadOnlySpan<uint> ids) { lock (sync) { watchedCount = Math.Min(ids.Length, 8); ids[..watchedCount].CopyTo(watched); } }
    public void SetEnabled(bool enabled, bool conflict)
    {
        if (!enabled || conflict) {
            hook?.Disable(); lock (sync) { head = count = watchedCount = 0; }
            Availability = conflict ? "Detailed capture paused: another death-recap plugin is loaded." : "Detailed incoming action capture is off.";
            return;
        }
        if (hook is not null) { hook.Enable(); Availability = "Experimental action effects active; fatal attribution and exact pre-hit HP unavailable."; return; }
        if (attempted) return;
        attempted = true;
        try {
            var address = (nint)ActionEffectHandler.MemberFunctionPointers.Receive;
            var start = scanner.Module.BaseAddress + (nint)scanner.TextSectionOffset;
            if (address < start || address + 16 > start + scanner.TextSectionSize) throw new InvalidOperationException();
            // Refuse a pre-existing detour instead of creating competing Receive hooks.
            var live = new byte[16]; var clean = new byte[16];
            Marshal.Copy(address, live, 0, 16); Marshal.Copy(scanner.TextSectionBase + (nint)(address - start), clean, 0, 16);
            if (!live.AsSpan().SequenceEqual(clean) || live[0] is 0xE9 or 0xCC) throw new InvalidOperationException();
            hook = interop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(address, Receive);
            hook.Enable(); Availability = "Experimental action effects active; fatal attribution and exact pre-hit HP unavailable.";
        } catch { hook?.Dispose(); hook = null; Availability = "Detailed capture unavailable: native binding or hook ownership could not be validated."; }
    }
    private void Receive(uint source, Character* caster, Vector3* position, ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects, GameObjectId* targets)
    {
        try { ObservePacket(source, header, effects, targets); }
        catch { /* Never unwind observation failures through the game. */ }
        finally { hook!.Original(source, caster, position, header, effects, targets); }
    }
    internal void ObservePacket(uint source, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targets)
    {
            if (header != null && effects != null && targets != null && header->NumTargets is > 0 and <= 32 && header->ActionType == 1) {
                lock (sync) {
                    for (var i = 0; i < header->NumTargets; i++) {
                        var target = targets[i].ObjectId;
                        if (!watched.AsSpan(0, watchedCount).Contains(target)) continue;
                        for (byte slot = 0; slot < 8; slot++) {
                            var e = effects[i].Effects[slot];
                            // Source-side reflected effects require distinct attribution and are deliberately excluded.
                            if ((e.Param4 & 0x80) != 0 || e.Type is not (3 or 4 or 5 or 6)) continue;
                            if (count == ring.Length) { Dropped++; continue; }
                            ring[(head + count++) % ring.Length] = new(DateTimeOffset.UtcNow, source, target, header->ActionId,
                                header->GlobalSequence, slot, e.Type == 4 ? RecapEventKind.Healing : RecapEventKind.Damage,
                                DeathPolicy.DecodeAmount(e.Value, e.Param3, e.Param4));
                        }
                    }
                }
            }
    }
    public bool TryRead(out IncomingAction value)
    {
        lock (sync) { if (count == 0) { value = default; return false; } value = ring[head]; head = (head + 1) % ring.Length; count--; return true; }
    }
    public void Dispose() { lock (sync) { watchedCount = count = 0; } hook?.Disable(); hook?.Dispose(); hook = null; }
}
