using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
namespace SentinelHub.Utilities.Latency;
internal sealed unsafe class NativeLatencyRuntime : ILatencyRuntime
{
    private readonly object sync = new();
    private readonly IObjectTable objects;
    private readonly Func<LatencyMode> requested;
    private readonly Func<bool, LatencyState> readiness;
    private readonly LatencySession session;
    private readonly HashSet<uint> instantActions;
    private Hook<ActionManager.Delegates.UseActionLocation> send = null!;
    private Hook<ActionEffectHandler.Delegates.Receive> receive = null!;
    private bool active;
    private bool disposed;
    public LatencyView View { get { lock (sync) return session.View; } }
    public NativeLatencyRuntime(IGameInteropProvider interop, IObjectTable objects, HashSet<uint> instantActions, LatencyMode mode,
        Func<LatencyMode> requested, Func<bool, LatencyState> readiness)
    {
        this.objects = objects; this.instantActions = instantActions; this.requested = requested; this.readiness = readiness;
        session = new(mode);
        try
        {
            var manager = ActionManager.Instance();
            if (manager == null || !float.IsFinite(manager->AnimationLock) || manager->AnimationLock < 0 || manager->AnimationLock > 60
                || ActionManager.Addresses.UseActionLocation.Value == 0 || ActionEffectHandler.Addresses.Receive.Value == 0
                || ActionManager.Addresses.GetSpellIdForAction.Value == 0)
                throw new InvalidOperationException("Latency bindings unavailable.");
            send = interop.HookFromAddress<ActionManager.Delegates.UseActionLocation>(
                ActionManager.Addresses.UseActionLocation.Value, OnSend);
            receive = interop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                ActionEffectHandler.Addresses.Receive.Value, OnReceive);
            send.Enable(); receive.Enable();
            active = true;
        }
        catch
        {
            active = false;
            try { receive?.Dispose(); } finally { send?.Dispose(); }
            throw;
        }
    }
    private bool Permit(bool fullScan)
    {
        if (!active || disposed || session.View.State is not (LatencyState.Measuring or LatencyState.Compensating)) return false;
        if (requested() != session.Mode) { session.Stop(LatencyState.Paused); return false; }
        var state = readiness(fullScan);
        if (state == LatencyState.Compensating) return true;
        session.Stop(state); return false;
    }
    private bool IsLocal(Character* actor) => actor != null && objects.LocalPlayer is { } local && local.Address == (nint)actor;
    private void Fail() => session.Stop(LatencyState.Faulted);
    private bool OnSend(ActionManager* manager, ActionType type, uint actionId, ulong target, Vector3* location, uint extra, byte a7)
    {
        lock (sync)
        {
            var call = new SendCall { Owner = this, Manager = manager, Type = type, ActionId = actionId,
                Target = target, Location = location, Extra = extra, A7 = a7 };
            return LatencyInterception.Invoke(ref call);
        }
    }
    private void OnReceive(uint casterId, Character* caster, Vector3* targetPos, ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects, GameObjectId* targets)
    {
        lock (sync)
        {
            var call = new EffectCall { Owner = this, CasterId = casterId, Caster = caster, TargetPos = targetPos,
                Header = header, Effects = effects, Targets = targets };
            LatencyInterception.Invoke(ref call);
        }
    }
    private struct SendCall : ILatencyInterceptedCall
    {
        public NativeLatencyRuntime Owner;
        public ActionManager* Manager;
        public ActionType Type;
        public uint ActionId, Extra;
        public ulong Target;
        public Vector3* Location;
        public byte A7;
        private ushort previous;
        private long started;
        private bool observe;
        public void Before()
        {
            observe = Owner.Permit(false) && Manager != null && Manager == ActionManager.Instance();
            if (observe) previous = Manager->LastUsedActionSequence;
        }
        public bool Original()
        {
            started = Environment.TickCount64;
            return Owner.send.Original(Manager, Type, ActionId, Target, Location, Extra, A7);
        }
        public void After(bool result)
        {
            if (!observe || !result || !Owner.Permit(false) || Manager != ActionManager.Instance()) return;
            var sequence = Manager->LastUsedActionSequence;
            if (sequence == previous) return;
            Owner.session.ClearPending();
            // Natural cast-time actions (even Swiftcast), items and special action types are excluded.
            if (Type != ActionType.Action || !Owner.instantActions.Contains(ActionId)
                || !float.IsFinite(Manager->CastTimeTotal) || !float.IsFinite(Manager->CastTimeElapsed)
                || Manager->CastTimeTotal > 0 && Manager->CastTimeElapsed < Manager->CastTimeTotal) return;
            var spell = ActionManager.GetSpellIdForAction(Type, ActionId);
            if (Owner.instantActions.Contains(spell)) Owner.session.Track(sequence, spell, Manager->AnimationLock, started);
        }
        public void Fault() => Owner.Fail();
    }
    private struct EffectCall : ILatencyInterceptedCall
    {
        public NativeLatencyRuntime Owner;
        public uint CasterId;
        public Character* Caster;
        public Vector3* TargetPos;
        public ActionEffectHandler.Header* Header;
        public ActionEffectHandler.TargetEffects* Effects;
        public GameObjectId* Targets;
        private ActionManager* manager;
        private ushort sequence;
        private uint action;
        private float wireLock, oldLock;
        private long arrived;
        private bool observe;
        public void Before()
        {
            arrived = Environment.TickCount64;
            observe = false;
            if (Header == null || !Owner.IsLocal(Caster) || Header->SourceSequence == 0
                || Header->ActionType != (byte)ActionType.Action || !Owner.Permit(false)) return;
            manager = ActionManager.Instance();
            if (manager == null) return;
            sequence = Header->SourceSequence; action = Header->ActionId;
            wireLock = Header->AnimationLock; oldLock = manager->AnimationLock;
            observe = true;
        }
        public bool Original()
        {
            Owner.receive.Original(CasterId, Caster, TargetPos, Header, Effects, Targets);
            return false;
        }
        public void After(bool result)
        {
            if (!observe || !Owner.session.Matches(sequence, action) || !Owner.Permit(true)) return;
            if (manager != ActionManager.Instance() || !Owner.IsLocal(Caster)
                || !float.IsFinite(manager->CastTimeTotal) || !float.IsFinite(manager->CastTimeElapsed)
                || manager->LastUsedActionSequence != sequence
                || manager->CastTimeTotal > 0 && manager->CastTimeElapsed < manager->CastTimeTotal)
            { Owner.session.ClearPending(); return; }
            if (Header->SourceSequence != sequence || Header->ActionId != action || Header->ActionType != (byte)ActionType.Action
                || Math.Abs(Header->AnimationLock - wireLock) > 0.001f)
            { Owner.session.Stop(LatencyState.Contested); return; }
            var decision = Owner.session.Receive(sequence, action, wireLock, oldLock, manager->AnimationLock, arrived);
            if (decision.Write && Owner.active && Owner.requested() == LatencyMode.Compensate)
            {
                // The only game write in this module. Packet headers, cooldowns and action arguments are unchanged.
                manager->AnimationLock = decision.RemainingLock;
                Owner.session.RecordApplied();
            }
        }
        public void Fault() => Owner.Fail();
    }
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true; active = false; session.Stop(LatencyState.Off);
            // Stop both entry points and attempt every cleanup even if one hook reports an error.
            try
            {
                try { receive?.Disable(); } finally { send?.Disable(); }
            }
            finally
            {
                try { receive?.Dispose(); } finally { send?.Dispose(); }
            }
        }
    }
}
