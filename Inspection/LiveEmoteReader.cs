using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using SentinelHub.Identification;
using CSCharacter=FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
namespace SentinelHub.Inspection;
public static class LiveEmoteReader
{
    public static unsafe EmoteObservation Capture(IPlayerCharacter player,IFramework framework)
    {
        if(!framework.IsInFrameworkUpdateThread||!player.IsValid()||!player.IsTargetable||player.Address==0)return default;
        var guard=LiveOrnamentReader.Capture(player,framework);
        return ObserveFields(player.Address,player.EntityId,player.ObjectIndex,true,guard);
    }
    internal static unsafe EmoteObservation ObserveFields(nint address,uint entity,ushort index,bool frameworkThread,ActorObservation guard)
    {
        if(!frameworkThread||address==0||!guard.Valid||!guard.DrawLoaded||guard.Transformed)return new(guard.Valid,guard.DrawLoaded,guard.Transformed,0,0,0);
        var actor=(CSCharacter*)address;
        if(actor->GameObject.EntityId!=entity||actor->GameObject.ObjectIndex!=index)return default;
        return new(true,true,false,actor->EmoteController.EmoteId,(byte)actor->Mode,actor->ModeParam);
    }
}
