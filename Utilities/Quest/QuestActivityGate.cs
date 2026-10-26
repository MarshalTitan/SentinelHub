namespace SentinelHub.Utilities.Quest;

// Pure scheduling policy; the accepted native engine still owns all callbacks and action timing.
public sealed class QuestActivityGate
{
    public const int FallbackMilliseconds=100;
    private long nextProbe;
    private bool dirty=true,active,processed;
    public void Wake()=>dirty=true;
    public void Reset(){dirty=true;active=false;processed=false;nextProbe=0;}
    public bool ShouldProbe(long now)=>dirty||now>=nextProbe;
    public void Observed(bool relevant,long now){dirty=false;active=relevant;nextProbe=now+FallbackMilliseconds;}
    // One cleanup update on exit clears pending rewards/cutscene context.
    public bool ShouldProcess(bool pending)
    {
        var process=active||pending||processed;
        processed=active||pending;
        return process;
    }
}
