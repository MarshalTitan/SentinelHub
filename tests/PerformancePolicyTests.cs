using System.Text.Json;
using SentinelHub.Activity;
using SentinelHub.Compatibility;
using SentinelHub.Diagnostics;
using SentinelHub.Health;
using SentinelHub.Profiles;
using SentinelHub.Utilities;
using SentinelHub.Utilities.Quest;
internal static class PerformancePolicyTests
{
    public static void Run(Action<string,Action> test,Action<bool> check)
    {
        test("Quest gate sleeps idle, wakes immediately and retains confirmation/exit cleanup",()=>{
            var g=new QuestActivityGate();check(g.ShouldProbe(0));g.Observed(false,0);
            check(!g.ShouldProcess(false)&&!g.ShouldProbe(99)&&g.ShouldProbe(100));
            g.Wake();check(g.ShouldProbe(1));g.Observed(true,1);check(g.ShouldProcess(false));
            g.Observed(false,2);check(g.ShouldProcess(false)&&!g.ShouldProcess(false));
            check(g.ShouldProcess(true));g.Reset();check(!g.ShouldProcess(false)&&g.ShouldProbe(3));
        });
        test("Dormant Quest session preserves activation, preference reset and immediate Off",()=>{
            var actions=0;var resets=0;var session=new QuestExecutionSession(_=>actions++,()=>resets++);
            session.Tick(QuestConvenienceState.Active,1,0,false);check(actions==0&&resets==1&&session.State==QuestConvenienceState.Active);
            for(var i=1;i<10000;i++)session.Tick(QuestConvenienceState.Active,1,i,false);
            check(actions==0&&resets==1);session.Tick(QuestConvenienceState.Active,1,10001);check(actions==1);
            session.Tick(QuestConvenienceState.Active,2,10002,false);check(resets==2);
            session.Tick(QuestConvenienceState.Off,2,10003,false);check(resets==3&&session.State==QuestConvenienceState.Off);
        });
        test("Profiles contract projects only schema-approved names, IDs and authoritative state",()=>{
            var id=Guid.NewGuid().ToString("D");var json=JsonSerializer.Serialize(new{Schema=1,Revision=2,Profiles=new[]{new{Id=id,Name="Hunts"}},SelectedProfileId=id,LastAppliedProfileId=id,Match="Matched",Applying=false,PrivateConfig="not projected"});
            var view=ProfilesPolicy.Parse(json);check(view.Availability==ProfilesAvailability.Ready&&view.Selected=="Hunts"&&view.LastApplied=="Hunts"&&view.Revision==2);
            foreach(var broken in new[]{json.Replace("\"Schema\":1","\"Schema\":99"),json.Replace("Matched","Invented"),json.Replace(id,"invalid"),"{}",new string('x',600000)})
                check(ProfilesPolicy.Parse(broken).Availability==ProfilesAvailability.Unavailable);
            check(ProfilesPolicy.Parse(json.Replace("\"Name\":\"Hunts\"","\"Name\":\"\" ")).Availability==ProfilesAvailability.Unavailable);
        });
        test("Profiles contract rejects duplicate IDs and missing selected identifiers",()=>{
            var id=Guid.NewGuid().ToString("D");var other=Guid.NewGuid().ToString("D");
            string Payload(object[] profiles,string? selected)=>JsonSerializer.Serialize(new{Schema=1,Revision=1,Profiles=profiles,SelectedProfileId=selected,LastAppliedProfileId=(string?)null,Match="NoProfileApplied",Applying=true});
            check(ProfilesPolicy.Parse(Payload([new{Id=id,Name="One"},new{Id=id,Name="Two"}],id)).Availability==ProfilesAvailability.Unavailable);
            check(ProfilesPolicy.Parse(Payload([new{Id=id,Name="One"}],other)).Availability==ProfilesAvailability.Unavailable);
            var empty=ProfilesPolicy.Parse(Payload([],null));check(empty.Availability==ProfilesAvailability.Ready&&empty.Profiles.Length==0&&empty.Applying);
        });
        test("Unchanged Activity snapshots allocate no transition sets or duplicate events",()=>{
            var service=new ActivityService();var health=HealthView.Empty;var compatibility=CompatibilityView.Empty;
            service.Observe(health,compatibility);var start=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<10000;i++)service.Observe(health,compatibility);
            check(GC.GetAllocatedBytesForCurrentThread()==start&&service.Journal.Snapshot(DateTimeOffset.UtcNow).Length==0);
        });
        test("Disabled profiler is dormant and explicitly started measurements stay in RAM",()=>{
            var profiler=new FrameworkProfiler();using(profiler.Measure(HubService.Update)){}
            check(profiler.Report().Length==1);profiler.Start();profiler.Context(false);using(profiler.Measure(HubService.Update)){}
            profiler.Context(true);using(profiler.Measure(HubService.Update)){}profiler.Stop();check(!profiler.Enabled&&profiler.Report().Length==3);
        });
        test("Latency lifecycle keeps its Off view without per-frame allocations",()=>{
            using var lifecycle=new LatencyLifecycle(_=>throw new Exception());var initial=lifecycle.View;
            for(var i=0;i<10000;i++)lifecycle.Tick(LatencyMode.Off,LatencyState.Off);
            check(ReferenceEquals(initial,lifecycle.View));
        });
    }
}
