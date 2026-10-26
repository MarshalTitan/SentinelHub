using System.Diagnostics;
using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SentinelHub.Activity;
using SentinelHub.Compatibility;
using SentinelHub.Configuration;
using SentinelHub.Diagnostics;
using SentinelHub.Fashion;
using SentinelHub.Health;
using SentinelHub.Inspection;
using SentinelHub.Mounts;
using SentinelHub.Utilities.Latency;
using SentinelHub.Utilities.Quest;

// Fixture timings describe controller overhead on the runner, not game frame times.
internal static class IdlePerformanceTests
{
    public static void Run()
    {
        var reads = 0;
        var pi = Fake<IDalamudPluginInterface>((method, _) => {
            if (method.Name == "get_InstalledPlugins") reads++;
            return Default(method.ReturnType);
        });
        var client = Fake<IClientState>();
        var objects = Fake<IObjectTable>();
        var player = Fake<IPlayerState>();
        var condition = Fake<ICondition>();
        var data = Fake<IDataManager>();
        var unlocks = Fake<IUnlockState>();
        var faults = new FaultJournal();
        var settings = new HubSettings();
        using var quest = new QuestConvenienceController(pi, client, Fake<IGameGui>(), condition, objects, data, settings, faults);
        using var mounts = new MountController(data, unlocks, client, player, objects, condition, faults);
        using var fashion = new FashionController(data, unlocks, client, player, objects, condition, faults);
        var inspector = new PlayerInspector(objects, player, client, Fake<IFramework>(), settings, () => false,
            new InspectionGameData(data, unlocks, () => false));
        var activity = new ActivityService();
        var health = HealthView.Empty;
        var compatibility = CompatibilityView.Empty;
        Measure("quest-off", quest.Tick);
        Measure("mounts-closed", () => mounts.Tick(false));
        Measure("fashion-closed", () => fashion.Tick(false));
        Measure("inspector-inactive", () => inspector.Tick(false));
        Measure("activity-unchanged", () => activity.Observe(health, compatibility));
        Console.WriteLine($"PERF fixture installed-plugin reads={reads}");
        if(reads!=0)throw new Exception("Disabled Quest enumerated installed plugins.");
        var conflict = new LatencyConflictProbe(pi);
        conflict.Read(true);
        Measure("cached-native-conflict-check", () => conflict.ReadCached());
        Measure("full-module-conflict-scan", () => conflict.Read(true), 50);
    }

    internal static void Measure(string name, Action call, int count = 10000)
    {
        for (var i = 0; i < 200; i++) call();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        long worst = 0;
        for (var i = 0; i < count; i++)
        {
            var tick = Stopwatch.GetTimestamp();
            call();
            worst = Math.Max(worst, Stopwatch.GetTimestamp() - tick);
        }
        var elapsed = Stopwatch.GetElapsedTime(start);
        Console.WriteLine($"PERF fixture {name}: n={count}; mean-us={elapsed.TotalMicroseconds/count:F3}; max-us={worst*1000000d/Stopwatch.Frequency:F3}; bytes/call={(GC.GetAllocatedBytesForCurrentThread()-allocated)/(double)count:F1}");
    }

    internal static T Fake<T>(Func<MethodInfo, object?[]?, object?>? invoke = null) where T : class
        => (T)Proxy(typeof(T), invoke);
    internal static object Proxy(Type type, Func<MethodInfo, object?[]?, object?>? invoke = null)
    {
        var value = DispatchProxy.Create(type, typeof(ServiceProxy));
        ((ServiceProxy)value).Handler = invoke ?? ((method, _) => Default(method.ReturnType));
        return value;
    }
    internal static object? Default(Type type)
    {
        if (type == typeof(void)) return null;
        if (type == typeof(string)) return "";
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        var sequence = type.GetInterfaces().Append(type).FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (sequence is not null) return Array.CreateInstance(sequence.GetGenericArguments()[0], 0);
        if (type.IsInterface) return Proxy(type);
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
    public class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
}
