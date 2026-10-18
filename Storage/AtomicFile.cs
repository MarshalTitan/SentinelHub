namespace SentinelHub.Storage;
public static class AtomicFile
{
    public static void Write(string path,byte[] bytes,Action<string,string>? replace=null)
    {
        var directory=Path.GetDirectoryName(Path.GetFullPath(path))!;Directory.CreateDirectory(directory);
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked destination not supported.");
        if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked file not supported.");
        var temp=Path.Combine(directory,".sentinel-"+Guid.NewGuid().ToString("N")+".tmp");
        try{
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,8192,FileOptions.WriteThrough)){file.Write(bytes);file.Flush(true);}
            if(replace is not null)replace(temp,path);
            else if(File.Exists(path))File.Replace(temp,path,null);
            else File.Move(temp,path);
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
