using System.Text.Json;
namespace SentinelHub.Health;
public sealed record SRankSafeStatus(bool Available,int Health,int QueueCount,bool Recovering)
{
    public static SRankSafeStatus Unknown=>new(false,-1,0,false);
}
public static class SRankStatusProjection
{
    // Existing live SRank read-only contract is character-scoped. Its raw name/status/hunt strings are never retained or exported.
    public static SRankSafeStatus Parse(string? json)
    {
        try{
            if(json is null||json.Length>8192)return SRankSafeStatus.Unknown;
            using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=8});var root=doc.RootElement;
            if(root.GetProperty("Schema").GetString()!="sentinel.srank.remote.v1"||!root.GetProperty("Success").GetBoolean())return SRankSafeStatus.Unknown;
            var state=root.GetProperty("Status");if(!state.GetProperty("Loaded").GetBoolean())return SRankSafeStatus.Unknown;
            return new(true,!state.GetProperty("Enabled").GetBoolean()?1:state.GetProperty("Ready").GetBoolean()?0:2,
                Math.Clamp(state.GetProperty("QueueCount").GetInt32(),0,100000),state.GetProperty("Recovering").GetBoolean());
        }catch{return SRankSafeStatus.Unknown;}
    }
}
