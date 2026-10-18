using System.Net;
using System.Text.Json;
using SentinelHub.Ecosystem;
namespace SentinelHub.Health;

public sealed class PublicCatalog : IDisposable
{
    private readonly HttpClient http;
    private readonly CancellationTokenSource lifetime=new();
    private Task<Dictionary<string,string>>? pending;
    public IReadOnlyDictionary<string,string> Versions {get;private set;}=new Dictionary<string,string>();
    public DateTimeOffset? At {get;private set;}
    public string Status {get;private set;}="Public versions not fetched";
    public PublicCatalog(HttpMessageHandler? handler=null) => http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(15)};
    public void Request()
    {
        if(pending is {IsCompleted:false})return;
        pending=Read();Status="Fetching public version metadata";
    }
    private async Task<Dictionary<string,string>> Read()
    {
        // A single fixed public URL; no credentials, user IDs or arbitrary destinations.
        using var response=await http.GetAsync("https://raw.githubusercontent.com/MarshalTitan/Sentinel/main/repo.json",
            HttpCompletionOption.ResponseHeadersRead,lifetime.Token).ConfigureAwait(false);
        if(response.StatusCode!=HttpStatusCode.OK)throw new InvalidDataException();
        await using var stream=await response.Content.ReadAsStreamAsync(lifetime.Token).ConfigureAwait(false);
        using var output=new MemoryStream();var buffer=new byte[8192];int read;
        while((read=await stream.ReadAsync(buffer,lifetime.Token).ConfigureAwait(false))>0){
            if(output.Length+read>128*1024)throw new InvalidDataException();output.Write(buffer,0,read);
        }
        return Parse(output.ToArray());
    }
    public static Dictionary<string,string> Parse(byte[] bytes)
    {
        if(bytes.Length>128*1024)throw new InvalidDataException();
        using var doc=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=12});
        if(doc.RootElement.ValueKind!=JsonValueKind.Array||doc.RootElement.GetArrayLength()>32)throw new InvalidDataException();
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var row in doc.RootElement.EnumerateArray()){
            if(!row.TryGetProperty("InternalName",out var n)||n.ValueKind!=JsonValueKind.String)continue;
            var id=n.GetString()!;if(!Catalog.Components.Any(c=>c.Id==id))continue;
            if(!row.TryGetProperty("AssemblyVersion",out var v)||v.ValueKind!=JsonValueKind.String)throw new InvalidDataException();
            var version=StatusPolicy.NumericVersion(v.GetString());
            if(version=="Unknown"||!result.TryAdd(id,version))throw new InvalidDataException();
        }
        if(result.Count==0)throw new InvalidDataException();return result;
    }
    public void Tick()
    {
        if(pending is not {IsCompleted:true} task)return;pending=null;
        try{Versions=task.GetAwaiter().GetResult();At=DateTimeOffset.UtcNow;Status="Dated public catalog; not an automatic update check";}
        catch{Versions=new Dictionary<string,string>();At=null;Status="Public catalog unavailable; public versions are Unknown";}
    }
    public void Dispose(){lifetime.Cancel();http.Dispose();lifetime.Dispose();}
}
