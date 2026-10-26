using System.Text.Json;
namespace SentinelHub.Profiles;

public enum ProfilesAvailability { Missing, Disabled, Unavailable, Ready }
public sealed record ProfileName(string Id,string Name);
public sealed record ProfilesView(ProfilesAvailability Availability,ProfileName[] Profiles,string? SelectedId,
    string? LastAppliedId,string Match,bool Applying,long Revision=0)
{
    public static readonly ProfilesView Unavailable=new(ProfilesAvailability.Unavailable,[],null,null,"Unavailable",false);
    public string Selected=>Profiles.FirstOrDefault(p=>p.Id==SelectedId)?.Name??"None selected";
    public string LastApplied=>Profiles.FirstOrDefault(p=>p.Id==LastAppliedId)?.Name??"None applied";
}
public static class ProfilesPolicy
{
    // Only approved fields cross the boundary; unknown properties never become diagnostics or UI text.
    public static ProfilesView Parse(string json)
    {
        try
        {
            if(string.IsNullOrEmpty(json)||json.Length>512*1024)return ProfilesView.Unavailable;
            using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=8});
            var root=doc.RootElement;
            if(root.GetProperty("Schema").GetInt32()!=1||root.GetProperty("Revision").GetInt64()<0)return ProfilesView.Unavailable;
            var list=root.GetProperty("Profiles");
            if(list.ValueKind!=JsonValueKind.Array||list.GetArrayLength()>2048)return ProfilesView.Unavailable;
            var profiles=new List<ProfileName>();var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in list.EnumerateArray())
            {
                var id=entry.GetProperty("Id").GetString();var name=entry.GetProperty("Name").GetString();
                if(!Guid.TryParseExact(id,"D",out var key)||key==Guid.Empty||string.IsNullOrWhiteSpace(name)||name.Length>64||name.Any(char.IsControl))return ProfilesView.Unavailable;
                id=key.ToString("D");if(!ids.Add(id))return ProfilesView.Unavailable;
                profiles.Add(new(id,name));
            }
            string? Id(string field){var p=root.GetProperty(field);if(p.ValueKind==JsonValueKind.Null)return null;
                var s=p.GetString();if(!Guid.TryParseExact(s,"D",out var g)||!ids.Contains(g.ToString("D")))throw new InvalidDataException();return g.ToString("D");}
            var match=root.GetProperty("Match").GetString();
            if(match is not ("NoProfileApplied" or "Matched" or "Drifted" or "Unavailable"))return ProfilesView.Unavailable;
            return new(ProfilesAvailability.Ready,profiles.ToArray(),Id("SelectedProfileId"),Id("LastAppliedProfileId"),match,
                root.GetProperty("Applying").GetBoolean(),root.GetProperty("Revision").GetInt64());
        }
        catch{return ProfilesView.Unavailable;}
    }
}
