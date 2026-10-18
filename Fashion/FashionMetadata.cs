using System.Globalization;
using System.Text.Json;
namespace SentinelHub.Fashion;
public sealed record FashionFactSource(string Type,string Text);
public sealed record FashionFact(uint Id,string EnglishName,string Patch,uint? ItemId,FashionFactSource[] Sources);
public sealed record FashionSnapshot(string Provider,string Endpoint,string RetrievedUtc,string ResponseSha256,string Permission,FashionFact[] Accessories);
public sealed record FashionCorrection(uint Id,string EnglishName,string? Note,string? SourceUrl,string? Availability,
    string? CheckedUtc,string? ExpiresUtc,string? AvailabilityEvidence);
public sealed record FashionCorrections(string Provenance,FashionCorrection[] Entries);
public static class FashionMetadata
{
    public static readonly FashionSnapshot Snapshot=ValidateSnapshot(Read<FashionSnapshot>("SentinelHub.FashionSources.json"));
    public static readonly FashionCorrections Corrections=Read<FashionCorrections>("SentinelHub.FashionCorrections.json");
    private static T Read<T>(string name){using var s=typeof(FashionMetadata).Assembly.GetManifestResourceStream(name)??throw new InvalidDataException(name);
        return JsonSerializer.Deserialize<T>(s)??throw new InvalidDataException(name);}
    public static FashionSnapshot ValidateSnapshot(FashionSnapshot value)
    {
        if(value.Provider!="FFXIV Collect"||value.Accessories.Length is <1 or >200
            ||value.Accessories.Select(m=>m.Id).Distinct().Count()!=value.Accessories.Length
            ||!DateTimeOffset.TryParse(value.RetrievedUtc,out _)||value.ResponseSha256.Length!=64
            ||value.Accessories.Any(m=>m.Id is 0 or >ushort.MaxValue||FashionPolicy.Retired(m.Id)
                ||string.IsNullOrWhiteSpace(m.EnglishName)||m.EnglishName.Length>200||m.EnglishName.Any(char.IsControl)
                ||m.Sources.Length>30||m.Sources.Any(s=>string.IsNullOrWhiteSpace(s.Type)||string.IsNullOrWhiteSpace(s.Text)
                    ||s.Type.Length>80||s.Text.Length>1000||s.Text.Any(char.IsControl))))
            throw new InvalidDataException("Invalid fashion source snapshot");
        return value;
    }
    public static FashionFact? Find(uint id,string english) =>
        Snapshot.Accessories.FirstOrDefault(m=>m.Id==id&&m.EnglishName.Equals(english,StringComparison.OrdinalIgnoreCase));
    public static FashionCorrection? Correction(uint id,string english) =>
        Corrections.Entries.FirstOrDefault(m=>m.Id==id&&m.EnglishName.Equals(english,StringComparison.OrdinalIgnoreCase));
    public static int? Expansion(string? patch)
    {
        if(patch==null)return null;var parts=patch.Split('.');
        return parts.Length==2&&parts[1].Length is >0 and <=2&&parts[1].All(char.IsAsciiDigit)
            &&int.TryParse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture,out var n)&&n is >=2 and <=7?n-2:null;
    }
    public static string Category(FashionFactSource source) => source.Type switch
    {"Premium"=>source.Text=="Online Store"?"Online Store":"Premium / promotion","Purchase"=>"Gil vendor","V&C Dungeon"=>"Variant / criterion",
        "Event"=>"Seasonal / event",_=>source.Type};
    public static FashionSource[] Sources(FashionFact fact)
    {
        var correction=Correction(fact.Id,fact.EnglishName);
        return fact.Sources.Select(s=>new FashionSource(Category(s),s.Text+(s.Type=="Event"?" (event source; may be historical)":""),
            "Community: FFXIV Collect; snapshot "+Snapshot.RetrievedUtc[..10],
            s.Type=="Premium"&&s.Text=="Online Store"&&SafeUrl(correction?.SourceUrl)?correction!.SourceUrl:"https://ffxivcollect.com/fashions/"+fact.Id)).ToArray();
    }
    public static FashionKnowledge Availability(FashionCorrection? correction,DateTimeOffset now)
    {
        if(correction is null||string.IsNullOrWhiteSpace(correction.AvailabilityEvidence)||!SafeUrl(correction.SourceUrl)
            ||!DateTimeOffset.TryParse(correction.CheckedUtc,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var check)
            ||!DateTimeOffset.TryParse(correction.ExpiresUtc,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var expires)
            ||now<check||now>=expires||expires<=check)return FashionKnowledge.Unknown;
        return correction.Availability switch{"Yes"=>FashionKnowledge.Yes,"No"=>FashionKnowledge.No,_=>FashionKnowledge.Unknown};
    }
    public static bool DirectCost(byte mode,uint itemId,uint type,long collectability) =>
        mode is 0 or 4 or 8 or 16&&itemId>=8&&type==0&&collectability==0;
    public static FashionSource GilSource(uint shop,ushort subrow,string name,string item,long price,bool hq,string[] requirements) =>
        new("Gil vendor","Gil shop: "+(string.IsNullOrWhiteSpace(name)?"#"+shop:name)+"; "+item
            +(hq?"; HQ price unresolved":price>0?"; base price "+price.ToString("N0",CultureInfo.InvariantCulture)+" gil":"; price unknown")
            +(requirements.Length>0?"; requires "+string.Join(", ",requirements):""),
            "Game data: GilShop #"+shop+", subrow "+subrow+"; access/availability not checked");
    public static bool SafeUrl(string? value) => Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&
        (uri.Host=="ffxivcollect.com"&&uri.AbsolutePath.StartsWith("/fashions/",StringComparison.Ordinal)
         ||uri.Host=="store.finalfantasyxiv.com"&&uri.AbsolutePath.StartsWith("/ffxivstore/",StringComparison.Ordinal)
         ||uri.Host.EndsWith(".finalfantasyxiv.com",StringComparison.Ordinal)&&uri.AbsolutePath.StartsWith("/lodestone/",StringComparison.Ordinal));
}
