using SentinelHub.Storage;
namespace SentinelHub.Diagnostics;
// Only invoked by the explicit export action. Never writes a session history automatically.
public static class DiagnosticsFile
{
    public const string FileName="sentinel-diagnostics.zip";
    public static void Save(string directory,byte[] bytes)=>AtomicFile.Write(Path.Combine(directory,FileName),bytes);
}
