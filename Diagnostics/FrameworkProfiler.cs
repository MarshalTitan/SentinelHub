using System.Diagnostics;
using System.Globalization;
namespace SentinelHub.Diagnostics;

public enum HubService { Update,Discovery,Health,Compatibility,AntiAfk,Quest,Latency,Activity,Mounts,Fashion,Menu,Inspector,Profiles,Draw,Export,TreasureMaps }
// Explicit, bounded, RAM-only measurement. Disabled scopes don't read a clock or GC counter.
public sealed class FrameworkProfiler
{
    private readonly Metric[] metrics=new Metric[Enum.GetValues<HubService>().Length*2];
    private long expires;
    private bool windowOpen;
    public bool Enabled {get;private set;}
    public void Start(){Array.Clear(metrics);expires=Environment.TickCount64+600_000;Enabled=true;}
    public void Stop()=>Enabled=false;
    public void Context(bool open)=>windowOpen=open;
    public Scope Measure(HubService service)
    {
        if(!Enabled)return default;
        if(Environment.TickCount64>=expires){Stop();return default;}
        return new(this,(int)service*2+(windowOpen?1:0));
    }
    public readonly struct Scope : IDisposable
    {
        private readonly FrameworkProfiler? owner;
        private readonly int index;
        private readonly long time,bytes;
        internal Scope(FrameworkProfiler owner,int index){this.owner=owner;this.index=index;time=Stopwatch.GetTimestamp();bytes=GC.GetAllocatedBytesForCurrentThread();}
        public void Dispose(){if(owner is null)return;ref var m=ref owner.metrics[index];
            var elapsed=Stopwatch.GetTimestamp()-time;m.Calls++;m.Ticks+=elapsed;m.Worst=Math.Max(m.Worst,elapsed);m.Bytes+=Math.Max(0,GC.GetAllocatedBytesForCurrentThread()-bytes);if(elapsed>Stopwatch.Frequency/500)m.Spikes++;}
    }
    private struct Metric {public long Calls,Ticks,Worst,Bytes,Spikes;}
    public string[] Report()
    {
        var lines=new List<string>{"Hub performance: "+(Enabled?"recording (10-minute limit)":"stopped")+"; RAM only. Inclusive timings/allocations; open includes floating and collapsed windows."};
        for(var i=0;i<metrics.Length;i++){var m=metrics[i];if(m.Calls==0)continue;
            lines.Add(string.Create(CultureInfo.InvariantCulture,$"{(HubService)(i/2)} {(i%2==1?"open":"closed")}: {m.Calls} calls, mean {m.Ticks*1000d/Stopwatch.Frequency/m.Calls:F4} ms, worst {m.Worst*1000d/Stopwatch.Frequency:F4} ms, {m.Bytes/(double)m.Calls:F1} B/call, {m.Spikes} over 2 ms"));}
        return lines.ToArray();
    }
}
