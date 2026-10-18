namespace SentinelHub.Activity;
public static class LegacyActivityCleanup
{
    // Retire only the exact history file created by the earlier operations candidate.
    // Never enumerate/delete user exports, backups, preferences or other plugin files.
    public static bool TryRemove(string directory)
    {
        try{
            var path=Path.Combine(directory,"recent-activity.json");
            if(!File.Exists(path))return true;
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
            File.Delete(path);return true;
        }catch{return false;}
    }
}
