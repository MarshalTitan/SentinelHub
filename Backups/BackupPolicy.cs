using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SentinelHub.Backups;

public enum BackupValue { Boolean, Integer, Number }
public sealed record BackupField(string Path,BackupValue Type,double Minimum=-100000,double Maximum=100000);
public sealed record BackupOwner(string Id,int Schema,string Version,BackupField[] Fields,string Exclusions);
public sealed record BackupEntry(string Owner,int ConfigSchema,string OwnerVersion,Dictionary<string,JsonElement> Data,string Sha256);
public sealed record BackupManifest(int Format,string Name,DateTimeOffset CreatedAt,BackupEntry[] Entries);
public static class BackupPolicy
{
    public const int MaximumBytes=512*1024;
    public static BackupEntry Project(BackupOwner owner,string version,byte[] json)
    {
        if(version!=owner.Version)throw new InvalidDataException("Unreviewed owner version.");
        var root=Parse(json);RequireSchema(root,owner);
        var data=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
        foreach(var rule in owner.Fields){
            var value=Get(root,rule.Path);if(value is null)continue;
            var element=JsonSerializer.SerializeToElement(value);Validate(rule,element);data.Add(rule.Path,element);
        }
        if(data.Count==0)throw new InvalidDataException("No supported settings.");
        var entry=new BackupEntry(owner.Id,owner.Schema,version,data,"");return entry with{Sha256=Hash(entry)};
    }
    public static void Validate(BackupManifest manifest)
    {
        if(manifest.Format!=1||!ValidName(manifest.Name)||manifest.CreatedAt.Year<2000||manifest.CreatedAt>DateTimeOffset.UtcNow.AddDays(1)
            ||manifest.Entries is not {Length:>0 and <=7}||manifest.Entries.Select(x=>x.Owner).Distinct(StringComparer.Ordinal).Count()!=manifest.Entries.Length)throw new InvalidDataException();
        foreach(var entry in manifest.Entries){
            var owner=BackupRegistry.Find(entry.Owner)??throw new InvalidDataException();
            if(entry.ConfigSchema!=owner.Schema||entry.OwnerVersion!=owner.Version||entry.Data is null||entry.Data.Count==0||entry.Data.Count>owner.Fields.Length)throw new InvalidDataException();
            foreach(var pair in entry.Data){var rule=owner.Fields.SingleOrDefault(f=>f.Path==pair.Key)??throw new InvalidDataException();Validate(rule,pair.Value);}
            if(entry.Sha256!=Hash(entry))throw new InvalidDataException("Backup checksum mismatch.");
        }
    }
    public static byte[] Restore(BackupEntry entry,BackupOwner owner,string currentVersion,byte[] current)
    {
        Validate(new(1,"validation",DateTimeOffset.UtcNow,[entry]));
        if(owner.Id!=entry.Owner||currentVersion!=owner.Version||currentVersion!=entry.OwnerVersion)throw new InvalidDataException("Owner/version mismatch.");
        var root=Parse(current);RequireSchema(root,owner);
        foreach(var pair in entry.Data)Set(root,pair.Key,JsonNode.Parse(pair.Value.GetRawText()));
        var bytes=Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
        if(bytes.Length>MaximumBytes)throw new InvalidDataException();return bytes;
    }
    public static BackupManifest Read(byte[] bytes)
    {
        var root=Parse(bytes);var manifest=root.Deserialize<BackupManifest>(new JsonSerializerOptions{MaxDepth=24})??throw new InvalidDataException();Validate(manifest);return manifest;
    }
    public static byte[] Write(BackupManifest manifest){Validate(manifest);var bytes=JsonSerializer.SerializeToUtf8Bytes(manifest,new JsonSerializerOptions{WriteIndented=true});if(bytes.Length>MaximumBytes)throw new InvalidDataException();return bytes;}
    public static bool ValidName(string? name)=>name is {Length:>=1 and <=60}&&name.All(c=>char.IsAsciiLetterOrDigit(c)||c is ' ' or '-' or '_' or '.');
    public static string Hash(BackupEntry entry)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{
        entry.Owner,entry.ConfigSchema,entry.OwnerVersion,Data=entry.Data.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value)}))).ToLowerInvariant();
    private static void Validate(BackupField rule,JsonElement value)
    {
        if(rule.Type==BackupValue.Boolean){if(value.ValueKind is not(JsonValueKind.True or JsonValueKind.False))throw new InvalidDataException();return;}
        if(value.ValueKind!=JsonValueKind.Number||!value.TryGetDouble(out var number)||!double.IsFinite(number)||number<rule.Minimum||number>rule.Maximum
            ||rule.Type==BackupValue.Integer&&(!value.TryGetInt64(out _)))throw new InvalidDataException();
    }
    private static void RequireSchema(JsonObject root,BackupOwner owner)
    {
        if(root["Version"] is not JsonValue value||!value.TryGetValue<int>(out var schema)||schema!=owner.Schema)throw new InvalidDataException("Unreviewed configuration schema.");
    }
    public static JsonObject Parse(byte[] bytes)
    {
        if(bytes.Length>MaximumBytes)throw new InvalidDataException();
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=24});
        Unique(doc.RootElement);return JsonNode.Parse(bytes,new JsonNodeOptions{PropertyNameCaseInsensitive=false},new JsonDocumentOptions{MaxDepth=24}) as JsonObject??throw new InvalidDataException();
    }
    private static void Unique(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.Ordinal);foreach(var p in element.EnumerateObject()){if(!names.Add(p.Name))throw new InvalidDataException();Unique(p.Value);}}
        else if(element.ValueKind==JsonValueKind.Array)foreach(var child in element.EnumerateArray())Unique(child);
    }
    private static JsonNode? Get(JsonObject root,string path)
    {
        JsonNode? current=root;foreach(var part in path.Split('.')){if(current is not JsonObject obj||!obj.TryGetPropertyValue(part,out current))return null;}return current;
    }
    private static void Set(JsonObject root,string path,JsonNode? value)
    {
        var parts=path.Split('.');var current=root;
        for(var i=0;i<parts.Length-1;i++){
            if(current[parts[i]] is null)current[parts[i]]=new JsonObject();
            current=current[parts[i]] as JsonObject??throw new InvalidDataException("Unsupported current shape.");
        }
        current[parts[^1]]=value;
    }
}
