using System.Text.Json;
using System.Text.RegularExpressions;
namespace SentinelHub.Inspection;
public sealed record InspectionFactSource(string Type,string Text);
public sealed record InspectionFact(uint Id,string EnglishName,string? Patch,InspectionFactSource[] Sources);
public sealed record InspectionSnapshot(string Kind,string Provider,string Endpoint,string RetrievedUtc,string ResponseSha256,string Permission,InspectionFact[] Rows);
public static class InspectionMetadata
{
    public static readonly InspectionSnapshot Emotes=Read("SentinelHub.InspectionEmotes.json","Emote","emotes");
    public static readonly InspectionSnapshot Bardings=Read("SentinelHub.InspectionBardings.json","Barding","bardings");
    private static InspectionSnapshot Read(string resource,string kind,string endpoint)
    {
        using var stream=typeof(InspectionMetadata).Assembly.GetManifestResourceStream(resource)??throw new InvalidDataException();
        return Validate(JsonSerializer.Deserialize<InspectionSnapshot>(stream)??throw new InvalidDataException(),kind,endpoint);
    }
    public static InspectionSnapshot Validate(InspectionSnapshot value,string kind,string endpoint)
    {
        if(value.Kind!=kind||value.Provider!="FFXIV Collect"||value.Endpoint!="https://ffxivcollect.com/api/"+endpoint+"?language=en"
            ||!DateTimeOffset.TryParse(value.RetrievedUtc,out _)||value.ResponseSha256.Length!=64||!value.ResponseSha256.All(char.IsAsciiHexDigit)
            ||value.Rows.Length is <1 or >1000||value.Rows.Select(r=>r.Id).Distinct().Count()!=value.Rows.Length
            ||value.Rows.Any(r=>r.Id is 0 or >ushort.MaxValue||string.IsNullOrWhiteSpace(r.EnglishName)||r.EnglishName.Length>200||r.EnglishName.Any(char.IsControl)
                ||r.Sources.Length>30||r.Sources.Any(s=>string.IsNullOrWhiteSpace(s.Type)||s.Type.Length>80||s.Type.Any(char.IsControl)
                    ||string.IsNullOrWhiteSpace(s.Text)||s.Text.Length>1000||s.Text.Any(char.IsControl))))throw new InvalidDataException("Invalid inspection source metadata.");
        return value;
    }
    public static InspectionFact? Find(InspectionKind kind,uint id,string english)=>kind is not (InspectionKind.Emote or InspectionKind.Barding)?null:(kind==InspectionKind.Emote?Emotes:Bardings).Rows.FirstOrDefault(r=>r.Id==id&&r.EnglishName.Equals(english,StringComparison.OrdinalIgnoreCase));
    public static string? Patch(string? patch)=>patch is not null&&Regex.IsMatch(patch,"^[2-9]\\.[0-9]{1,2}[a-z]?$")?patch:null;
    public static string? Expansion(string? patch)=>Patch(patch) is {} valid?valid[0] switch{'2'=>"A Realm Reborn",'3'=>"Heavensward",'4'=>"Stormblood",'5'=>"Shadowbringers",'6'=>"Endwalker",'7'=>"Dawntrail",_=>null}:null;
    public static string Category(InspectionFactSource source)=>source.Type switch{"Premium"=>source.Text=="Online Store"?"Online Store (recorded source)":"Premium / promotion","Event"=>"Seasonal / event (may be historical)","Purchase"=>"Vendor / purchase",_=>source.Type};
    public static string[] Sources(InspectionKind kind,InspectionFact fact)
    {
        var snapshot=kind==InspectionKind.Emote?Emotes:Bardings;var date=DateTimeOffset.Parse(snapshot.RetrievedUtc).UtcDateTime.ToString("yyyy-MM-dd");
        return fact.Sources.Select(s=>Category(s)+": "+s.Text+" [FFXIV Collect snapshot "+date+"; current availability not verified]").ToArray();
    }
}
