using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SentinelHub.Ecosystem;
using SentinelHub.Health;
namespace SentinelHub.Diagnostics;

public enum ProviderState { Unavailable, Ready, Disabled, Paused, Degraded }
public sealed record ProviderDiagnostic(string Id,ProviderState State,int Schema,int Warnings,int Errors);
public static class DiagnosticProviderPolicy
{
    public static ProviderDiagnostic Parse(string id,string? json)
    {
        if(!Catalog.Components.Any(c=>c.Id==id))throw new InvalidDataException();
        try{
            if(json is null||json.Length>16384)throw new InvalidDataException();
            using var d=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=6});var r=d.RootElement;
            if(r.ValueKind!=JsonValueKind.Object||r.EnumerateObject().GroupBy(x=>x.Name).Any(g=>g.Count()!=1))throw new InvalidDataException();
            if(r.GetProperty("Schema").GetInt32()!=1)throw new InvalidDataException();
            var state=r.GetProperty("State").GetInt32();if(!Enum.IsDefined((ProviderState)state))throw new InvalidDataException();
            return new(id,(ProviderState)state,1,Math.Clamp(r.GetProperty("Warnings").GetInt32(),0,100000),Math.Clamp(r.GetProperty("Errors").GetInt32(),0,100000));
        }catch{return new(id,ProviderState.Unavailable,0,0,0);}
    }
}
public static class DiagnosticsBundle
{
    public static byte[] Create(HubSnapshot snapshot,HealthView health,IEnumerable<Fault> faults,
        IEnumerable<ProviderDiagnostic> providers,int configSchema,uint territory,uint world,
        SentinelHub.Utilities.AntiAfkView? antiAfk=null,SentinelHub.Utilities.QuestConvenienceView? quest=null,SentinelHub.Utilities.LatencyView? latency=null)
    {
        // Everything entering the ZIP is rebuilt from typed, bounded, allowlisted fields.
        // No file enumeration, config serialization, provider strings or exception text.
        var checks=health.Checks.Where(x=>HealthPolicy.KnownIds.Contains(x.Id,StringComparer.Ordinal)||x.Id=="Faloop")
            .Take(32).Select(x=>new{Id=x.Id,State=Enum.IsDefined(x.State)?x.State:HealthState.Unknown,
                RuntimeVersion=StatusPolicy.NumericVersion(x.RuntimeVersion),PublicVersion=StatusPolicy.NumericVersion(x.PublicVersion),x.DevelopmentOverride}).ToArray();
        var safeProviders=providers.Where(p=>Catalog.Components.Any(c=>c.Id==p.Id)).Take(7).Select(p=>new{
            p.Id,State=Enum.IsDefined(p.State)?p.State:ProviderState.Unavailable,Schema=Math.Clamp(p.Schema,0,1),Warnings=Math.Clamp(p.Warnings,0,100000),Errors=Math.Clamp(p.Errors,0,100000)}).ToArray();
        var options=new JsonSerializerOptions{WriteIndented=true,Converters={new JsonStringEnumConverter()}};
        var report=JsonSerializer.Serialize(new{Schema="sentinel.diagnostics.v2",CreatedAt=DateTimeOffset.UtcNow,
            HubVersion=StatusPolicy.NumericVersion(typeof(DiagnosticsBundle).Assembly.GetName().Version?.ToString()),
            ConfigurationSchema=Math.Clamp(configSchema,0,1000),Territory=territory<=100000?territory:0,World=world<=10000?world:0,
            Health=checks,Providers=safeProviders,Profiles=new{State="Unavailable unless supported status provider responds",Name="Excluded"},
            Privacy="No credentials, configuration documents, player/account/entity IDs, names, chat, paths, arbitrary provider text or exception messages."},options);
        using var output=new MemoryStream();
        using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){
            Add(zip,"report.json",report);
            Add(zip,"hub-state.json",DiagnosticExport.Serialize(snapshot,faults,antiAfk,quest,latency));
            Add(zip,"summary.txt","Sentinel diagnostics\nHub version: "+StatusPolicy.NumericVersion(typeof(DiagnosticsBundle).Assembly.GetName().Version?.ToString())+
                "\nConfiguration schema: "+Math.Clamp(configSchema,0,1000)+"\n"+
                string.Join("\n",checks.Select(c=>c.Id+": "+c.State+"; runtime "+c.RuntimeVersion+"; catalog "+c.PublicVersion))+
                "\n\nProvider Unavailable means no compatible response, not healthy. Collection/inspection identities and configuration contents are excluded.\n");
        }
        return output.ToArray();
    }
    private static void Add(ZipArchive zip,string name,string value)
    {
        var entry=zip.CreateEntry(name,CompressionLevel.Optimal);
        using var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false));writer.Write(value);
    }
}
