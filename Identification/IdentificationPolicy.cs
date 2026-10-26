namespace SentinelHub.Identification;
public enum IdentificationState { Off, None, Found, UnknownId, Unavailable }
public sealed record IdentificationPart(IdentificationState State,uint Id=0);
public sealed record PlayerIdentification(IdentificationPart Mount,IdentificationPart Fashion,long ObservedAt)
{
    public static PlayerIdentification Unavailable(long now)=>new(new(IdentificationState.Unavailable),new(IdentificationState.Unavailable),now);
}
public readonly record struct IdentificationTicket(ulong ObjectId,uint EntityId,ushort Index,ulong Session,uint Territory,long CreatedAt);
public readonly record struct ActorObservation(bool Valid,bool DrawLoaded,bool Transformed,uint OrnamentId);
public static class IdentificationPolicy
{
    public static bool Offer(bool mountEnabled,bool fashionEnabled,bool mounted) => fashionEnabled || mountEnabled && mounted;
    public static bool TicketMatches(IdentificationTicket t,ulong objectId,uint entity,ushort index,ulong session,uint territory,
        long now,bool enabled,bool ready,bool player,bool valid,bool disposed) =>
        !disposed&&enabled&&ready&&player&&valid&&t.ObjectId!=0&&t.ObjectId==objectId&&t.EntityId!=0&&t.EntityId!=0xE0000000
        &&t.EntityId==entity&&t.Index==index&&t.Session!=0&&t.Session==session&&t.Territory==territory
        &&now>=t.CreatedAt&&now-t.CreatedAt<=10000;
    public static IdentificationPart Resolve(ActorObservation actor,bool enabled,Func<uint,bool> known)
    {
        if(!enabled)return new(IdentificationState.Off);
        if(!actor.Valid||!actor.DrawLoaded||actor.Transformed)return new(IdentificationState.Unavailable);
        if(actor.OrnamentId==0)return new(IdentificationState.None);
        return new(known(actor.OrnamentId)?IdentificationState.Found:IdentificationState.UnknownId,actor.OrnamentId);
    }
}
// One short-lived numeric request, not a retained actor/native pointer. A fresh click replaces the pending request.
public sealed class IdentificationQueue
{
    private IdentificationTicket? pending;
    public bool HasPending=>pending.HasValue;
    public void Request(IdentificationTicket ticket)=>pending=ticket;
    public IdentificationTicket? Take(){var value=pending;pending=null;return value;}
    public void Clear()=>pending=null;
}
