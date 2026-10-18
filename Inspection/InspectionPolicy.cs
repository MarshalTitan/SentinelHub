using SentinelHub.Identification;
namespace SentinelHub.Inspection;
public enum InspectionKind { Mount, Fashion, Emote, Barding }
public enum InspectionState { Unavailable, None, Found, UnknownId, Unsupported }
public sealed record InspectionEntry(InspectionKind Kind,uint Id,string Name,uint Icon,string Command,bool? Owned,
    string[] Sources,string Category,string? Patch=null,string? Expansion=null,bool? Obtainable=null);
public sealed record InspectionPart(InspectionState State,InspectionEntry? Entry=null,string Detail="");
public sealed record InspectorView(bool Tracking,DateTimeOffset At,InspectionPart Mount,InspectionPart Fashion,InspectionPart Emote,InspectionPart Barding)
{
    public InspectionPart[] BardingSlots {get;init;}=[];
    public static InspectorView Empty=>new(false,DateTimeOffset.MinValue,new(InspectionState.Unavailable),new(InspectionState.Unavailable),
        new(InspectionState.Unavailable),new(InspectionState.Unavailable,null,"No current chocobo observation. Inspect a loaded player."));
}
public readonly record struct EmoteObservation(bool Valid,bool DrawLoaded,bool Transformed,uint EmoteId,byte Mode,byte ModeParam);
public static class InspectionPolicy
{
    // Only an explicit controller ID in EmoteLoop mode with matching sheet mode is supported initially.
    // Pose/idle/InPositionLoop/combat/action-timeline states are never interpreted as an emote.
    public static InspectionPart Emote(EmoteObservation actor,Func<uint,InspectionEntry?> lookup,Func<uint,uint?> mode)
    {
        if(!actor.Valid||!actor.DrawLoaded||actor.Transformed)return new(InspectionState.Unavailable,null,"Actor is unavailable/loading/transformed.");
        if(actor.EmoteId==0||actor.Mode!=3)return new(InspectionState.None,null,"No identifiable emote currently active. Short one-shot emotes and poses are not reconstructed.");
        var entry=lookup(actor.EmoteId);
        if(entry is null)return new(InspectionState.UnknownId,null,"Explicit emote ID is not in the usable local Emote catalog.");
        var expected=mode(actor.EmoteId);
        if(expected is null||expected==0||expected!=actor.ModeParam)return new(InspectionState.None,null,"Emote controller and mode disagree; no current emote inferred.");
        return new(InspectionState.Found,entry);
    }
    public static bool SessionMatches(IdentificationTicket t,ulong id,uint entity,ushort index,ulong session,uint territory,long now,bool ready,bool valid)
        =>IdentificationPolicy.TicketMatches(t with{CreatedAt=now},id,entity,index,session,territory,now,true,ready,true,valid,false)
        &&now>=t.CreatedAt&&now-t.CreatedAt<=300000;
    public static InspectionPart Resolve(InspectionKind kind,uint id,Func<InspectionKind,uint,InspectionEntry?> lookup)
    {
        if(id==0)return new(InspectionState.None);
        var entry=lookup(kind,id);return entry is null?new(InspectionState.UnknownId):new(InspectionState.Found,entry);
    }
}
